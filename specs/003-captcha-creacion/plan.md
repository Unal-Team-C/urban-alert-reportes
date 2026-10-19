# Plan técnico: Verificación humana (captcha) al crear reportes

- **Spec**: ./spec.md
- **Estado**: aprobado <!-- borrador | aprobado -->
- **Fecha**: 2026-10-19

## 1. Resumen técnico

Un puerto `IVerificadorCaptcha` en Application, invocado al principio de `CrearReporteHandler`. Tiene dos implementaciones:

- `VerificadorDesactivado`, el valor por defecto, que no hace nada.
- `VerificadorTurnstile`, un `HttpClient` tipado que hace POST a `siteverify` con el secreto y el token, comprueba `success`, `hostname` y `action`, y traduce el resultado a `CaptchaInvalidoException` (400) o `CaptchaNoDisponibleException` (503).

Su pipeline reutiliza `PoliticasHttp` de la feature 002: reintento solo ante fallos de conexión, circuito y timeout de 5 s. El token llega como campo `captchaToken` en los dos DTO de creación.

En AWS, el secreto es un secreto de arranque (P2) llamado `urban-alert-reportes-captcha`, que el equipo crea a mano con su valor. `infra/service` lo referencia solo si `captcha_habilitado = true`.

## 2. Contexto técnico

| Aspecto | Valor |
|---|---|
| Lenguaje / versión | C# / .NET 10, sobre la rama `002-resiliencia-eventos` (PR #7 sin merge) |
| Dependencias | Ninguna nueva (HttpClient y Microsoft.Extensions.Http.Resilience 10.10.0, ya añadidos en la 002) |
| Proveedor | Cloudflare Turnstile `POST https://challenges.cloudflare.com/turnstile/v0/siteverify` (form-urlencoded `secret`, `response`) → `{success, hostname, action, challenge_ts, "error-codes"}` |
| Claves de prueba | secreto `1x0000000000000000000000000000000AA` (siempre éxito), `2x0000000000000000000000000000000AA` (siempre fallo) |
| Pruebas | xUnit: unitarias (verificador con HttpMessageHandler falso), integración (endpoints con un verificador falso), Terraform `service.tftest.hcl` con `mock_provider` |
| Plataforma | ECS Express; salida a internet por el NAT de la plataforma |

## 3. Verificación contra la constitución

| Principio | ¿Cumple? | Nota |
|---|---|---|
| P1 | Sí | El servicio solo cambia por Terraform y los workflows. |
| P2 | **Excepción justificada** | El secreto `urban-alert-reportes-captcha` es un recurso de arranque: lo crea el equipo con la AWS CLI y fuera del estado, porque su valor viene de un tercero (Cloudflare), Terraform no puede generarlo y debe sobrevivir a un Deprovision. Queda documentado en `infra/bootstrap/README.md`. Terraform solo lo lee (`data`) cuando la verificación está activa. |
| P3 | Sí | El secreto solo vive en Secrets Manager; ECS lo inyecta como variable. Nunca se registran en el log ni el token ni el secreto. |
| P4 | Sí | Nada destructivo. |
| P5 | Sí | Turnstile es gratuito; un secreto más cuesta ≈ 0,40 USD/mes y se añade a la tabla de costes. |
| P6 | Sí | dotnet test, terraform fmt, validate y test antes de cada commit. |
| P7 | Sí | Pruebas de Terraform: desactivada por defecto, sin el secreto; activada, con la referencia al secreto y los dominios permitidos. |
| P8 | Sí | Sin paquetes nuevos. |
| P9 | Sí | Nombre `urban-alert-reportes-captcha`, cubierto por la política `${var.name}-*` del rol de ejecución. |
| P10 | Sí | Plan idempotente. |

## 4. Arquitectura y componentes

```text
POST /api/v1/Reportes (JSON o multipart, campo captchaToken)
  → ReportesController → CrearReporteCommand(…, CaptchaToken)
  → CrearReporteHandler
      1. IVerificadorCaptcha.VerificarAsync(token)        ← nuevo, ANTES de todo
           Desactivado: no hace nada
           Turnstile:  vacío o > 2048 caracteres → CaptchaInvalido (sin red)
                       POST siteverify [Retry solo sin conexión → Circuit → Timeout 5 s]
                       success=false (incluye timeout-or-duplicate) → CaptchaInvalido
                       hostname fuera de HostnamesPermitidos (si los hay) → CaptchaInvalido
                       action ≠ Accion (si está configurada) → CaptchaInvalido
                       red, 5xx, JSON ilegible, circuito abierto → CaptchaNoDisponible
      2. validación local, Multimedia, Geoespacial, guardado (como hoy)
  ExceptionHandlingMiddleware: CaptchaInvalido → 400 {message}; CaptchaNoDisponible → 503 {message}
```

## 5. Estructura de archivos

```text
src/Application/Interfaces/Captcha/IVerificadorCaptcha.cs          (nuevo)
src/Application/Captcha/CaptchaInvalidoException.cs                 (nuevo)
src/Application/Captcha/CaptchaNoDisponibleException.cs             (nuevo)
src/Application/Reportes/CrearReporte/CrearReporteCommand.cs        (mod) + CaptchaToken
src/Application/Reportes/CrearReporte/CrearReporteHandler.cs        (mod) verificar primero
src/Infrastructure/Captcha/VerificadorTurnstile.cs                  (nuevo)
src/Infrastructure/Captcha/VerificadorDesactivado.cs                (nuevo)
src/Infrastructure/Captcha/CaptchaOpciones.cs                       (nuevo)
src/Infrastructure/Resiliencia/PoliticasHttp.cs                     (mod) + AgregarResilienciaCaptcha
src/Infrastructure/DependencyInjection.cs                           (mod) AgregarCaptcha
src/API/DTOs/Reportes/CrearReporte*.cs                              (mod) + CaptchaToken
src/API/Controllers/ReportesController.cs                           (mod) pasa el token
src/API/Middleware/ExceptionHandlingMiddleware.cs                   (mod) 400 y 503
src/API/appsettings.json                                            (mod) sección Captcha
infra/service/{main.tf,variables.tf,terraform.tfvars,tests/service.tftest.hcl}   (mod)
infra/bootstrap/README.md, README.md, docs/funcionamiento.md, CLAUDE.md          (mod)
tests/UnitTests/Infrastructure/VerificadorTurnstileTests.cs         (nuevo)
tests/UnitTests/Application/Reportes/CrearReporteHandlerTests.cs    (mod)
tests/IntegrationTests/Reportes/CaptchaEndpointsTests.cs            (nuevo)
```

## 6. Modelo de datos

Sin cambios.

## 7. Contratos / interfaces

- **API**: `CrearReporteRequest` y `CrearReporteConImagenRequest` reciben `captchaToken` (string opcional). Con la verificación activa, si falta o no es válido, se responde `400 {"message": "No se pudo verificar que la solicitud la hizo una persona. Repite la verificación."}`. Si Turnstile falla, `503 {"message": "No fue posible verificar la solicitud. Intenta nuevamente más tarde."}`.
- **Configuración**:
  - `Captcha:Habilitado` (false)
  - `Captcha:ClaveSecreta` (vacía; en AWS viene del secreto)
  - `Captcha:HostnamesPermitidos` (lista; vacía = no se comprueba)
  - `Captcha:Accion` (`crear-reporte`; vacía = no se comprueba)
  - `Captcha:UrlVerificacion` (la de Turnstile; configurable para pruebas)
  - `Captcha:TimeoutSegundos` (5)
  - `Resiliencia:Captcha:*`, como las demás dependencias
- **Arranque**: con `Habilitado=true` y `ClaveSecreta` vacía, la API no arranca y el mensaje lo explica. Así Provision falla en lugar de rechazar todas las creaciones.
- **Terraform** (`infra/service`): `captcha_habilitado` (bool, false), `captcha_hostnames` (lista, []), `captcha_accion` ("crear-reporte"). Con la verificación activa: `data "aws_secretsmanager_secret" "captcha"` por nombre, el bloque `secret { Captcha__ClaveSecreta = "<arn>:clave_secreta::" }` y las variables `Captcha__Habilitado`, `Captcha__Accion` y `Captcha__HostnamesPermitidos__N`.

## 8. Decisiones

### D-001: Validar en Reportes, no en el API Gateway
- **Motivo**: el JWT authorizer de una HTTP API no puede validar un token opaco, y un Lambda authorizer exigiría enviar el token en un header, que el CORS del gateway compartido no admite. El análisis está en la conversación con el usuario y en la spec §9.

### D-002: Secreto de arranque fuera del estado
- **Decisión**: el equipo crea el secreto con `aws secretsmanager create-secret --name urban-alert-reportes-captcha --secret-string file://captcha.json` (`{"clave_secreta":"…"}`), y Terraform lo lee por nombre.
- **Alternativas**: (a) Terraform crea el secreto con un valor de relleno e `ignore_changes`: se perdería con cada Deprovision y habría que volver a cargarlo a mano. (b) Un secreto de GitHub pasado a Terraform: el valor acabaría en el estado y en el plan.
- **Motivo**: es una credencial de un tercero, que sobrevive a Deprovision y nunca entra en el estado.

### D-003: Reintento solo sin conexión y falla cerrado
- **Decisión**: se usa `AgregarPipeline(soloSinConexion: true)`, la misma variante que Multimedia, con su propio circuito. Cualquier fallo del proveedor resulta en 503.
- **Motivo**: el token es de un solo uso. Reintentar tras un timeout podría consumirlo dos veces y Turnstile respondería `timeout-or-duplicate`. Dejar pasar las creaciones cuando el proveedor falla anularía el control.

### D-004: Verificar antes de cualquier otro trabajo
- **Motivo**: un bot no debe provocar subidas a Multimedia ni registros en Geoespacial. La validación local de la imagen va después, porque es barata pero innecesaria si el token no sirve.

## 9. Estrategia de pruebas

- **Unitarias del verificador**, con un handler HTTP falso:
  - éxito;
  - `success=false` (con `error-codes`), incluido `timeout-or-duplicate`;
  - dominio no permitido y acción distinta;
  - token vacío o demasiado largo, sin llamar al proveedor;
  - 500 y error de red → no disponible;
  - circuito abierto → no disponible y en menos de 50 ms;
  - el cuerpo enviado lleva `secret` y `response` en form-urlencoded;
  - ningún log contiene el token ni el secreto.
- **Unitarias del handler**: verifica primero; si la verificación falla, no se llama a Multimedia, Geoespacial ni al repositorio.
- **Integración**: un `ReportesApiFactory` con `Captcha:Habilitado=true` y un verificador falso controlable. JSON y multipart con token válido → 201; sin token → 400; verificador no disponible → 503. Con la fábrica por defecto (desactivado) las 83 pruebas actuales siguen pasando.
- **Arranque**: `Habilitado=true` con la clave vacía → `InvalidOperationException` al construir la DI.
- **Terraform**: `run "captcha_disabled_by_default"` y `run "captcha_enabled_reads_the_bootstrap_secret"`, con `override_data` del secreto.
- **Comandos**: los de la 002 (`$DOTNET`, `$IT`) y `$TF -chdir=infra/service init -backend=false` + `validate` + `test`, más `fmt -check -recursive infra`.

## 10. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Activar Reportes antes de publicar el portal con el widget | Todas las creaciones del portal fallan con 400 | Orden documentado: primero 004 con la clave pública y después `captcha_habilitado=true`. |
| Activar sin haber creado el secreto | El plan falla (`data` no encuentra el secreto) | Es un error explícito en Provision; el README de arranque explica el paso. |
| Las claves de prueba no devuelven el dominio real | Las pruebas locales fallan la comprobación de dominio | En local, `HostnamesPermitidos` queda vacío; en AWS se fija el dominio de CloudFront. |
| Turnstile caído | Nadie puede crear reportes | Se acepta conscientemente (falla cerrado): el circuito responde rápido y el mensaje pide reintentar más tarde. |

## 11. Trazabilidad

| Requisito | Componentes | Pruebas |
|---|---|---|
| RF-001 | DTO, comando, handler | IT: sin token → 400 (JSON y multipart) |
| RF-002 | Handler (paso 1) | UT del handler: sin llamadas al fallar |
| RF-003 | VerificadorTurnstile | UT: vacío, largo, `success=false`, `timeout-or-duplicate` |
| RF-004 | VerificadorTurnstile | UT: dominio y acción |
| RF-005 | VerificadorTurnstile + middleware | UT: 500, red, JSON ilegible; IT: 503 |
| RF-006 | PoliticasHttp (Captcha) | UT: circuito abierto en menos de 50 ms; sin reintento tras timeout |
| RF-007 | VerificadorDesactivado | IT: las 83 actuales sin cambios; token ignorado |
| RF-008 | CaptchaOpciones, Terraform | UT de arranque; tftest |
| RF-009 | Logs del verificador | UT: log con error-codes, sin token ni secreto |
| RNF-001 | Timeout de 5 s | Configuración, D-003 |
| RNF-002 | README de costes | Revisión |
| RNF-003 | Logs | UT de RF-009 |
