# Tareas: Verificación humana (captcha) al crear reportes

- **Plan**: ./plan.md
- **Estado**: aprobado <!-- borrador | aprobado -->

Leyenda: `[ ]` pendiente · `[~]` en progreso · `[x]` hecha · `[!]` bloqueada · `[-]` descartada · `[P]` paralelizable

Herramientas: `$DOTNET` e `$IT` como en la 002; `$TF` = `podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5`.

## Fase 1: Núcleo

- [x] T001 Puerto y excepciones: `IVerificadorCaptcha`, `CaptchaInvalidoException`, `CaptchaNoDisponibleException`; `CaptchaToken` en el comando; el handler verifica primero; pruebas unitarias del handler (verifica antes de todo; si falla, no hay llamadas ni guardado) — archivos: src/Application/Interfaces/Captcha/IVerificadorCaptcha.cs, src/Application/Captcha/*.cs, src/Application/Reportes/CrearReporte/CrearReporteCommand.cs, src/Application/Reportes/CrearReporte/CrearReporteHandler.cs, tests/UnitTests/Application/Reportes/CrearReporteHandlerTests.cs — cubre: RF-002 — verifica: `$DOTNET test tests/UnitTests` en verde
- [x] T002 `VerificadorTurnstile`, `VerificadorDesactivado`, `CaptchaOpciones` (validación de arranque), `AgregarResilienciaCaptcha` y DI; sección Captcha en appsettings; pruebas unitarias del verificador (éxito, success=false, timeout-or-duplicate, dominio, acción, vacío o largo sin red, 500, red, JSON ilegible, circuito abierto en menos de 50 ms, cuerpo form-urlencoded, logs sin token ni secreto, arranque con clave vacía) — archivos: src/Infrastructure/Captcha/*.cs, src/Infrastructure/Resiliencia/PoliticasHttp.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json, tests/UnitTests/Infrastructure/VerificadorTurnstileTests.cs — depende: T001 — cubre: RF-003, RF-004, RF-005, RF-006, RF-007, RF-008, RF-009, RNF-001, RNF-003 — verifica: `$DOTNET test tests/UnitTests` en verde
- [x] T003 API: `captchaToken` en los dos DTO y en el controlador; el middleware responde 400 o 503; pruebas de integración con verificación activa y un verificador falso (JSON y multipart: válido 201, sin token 400, no disponible 503) y las actuales sin cambios — archivos: src/API/DTOs/Reportes/CrearReporteRequest.cs, src/API/DTOs/Reportes/CrearReporteConImagenRequest.cs, src/API/Controllers/ReportesController.cs, src/API/Middleware/ExceptionHandlingMiddleware.cs, tests/IntegrationTests/Reportes/CaptchaEndpointsTests.cs — depende: T002 — cubre: RF-001, RF-005, RF-007 — verifica: `$IT` en verde

## Fase 2: Integración

- [x] T004 Terraform: `captcha_habilitado`, `captcha_hostnames`, `captcha_accion`; `data` del secreto de arranque y bloque `secret`/variables solo si está activo; dos `run` en service.tftest.hcl — archivos: infra/service/main.tf, infra/service/variables.tf, infra/service/terraform.tfvars, infra/service/tests/service.tftest.hcl — cubre: RF-008, P7 — verifica: `$TF -chdir=infra/service init -backend=false`, `validate`, `test` en verde y `fmt -check -recursive infra` limpio

## Fase 3: Pulido

- [x] T005 Documentar: arranque del secreto (comando, formato JSON, permisos), orden de activación (primero el portal, después el servicio), configuración y comportamiento en README, docs/funcionamiento.md y CLAUDE.md; coste del secreto — archivos: infra/bootstrap/README.md, README.md, docs/funcionamiento.md, CLAUDE.md — cubre: RNF-002, D-002 — verifica: revisión cruzada de las claves con appsettings y las variables de Terraform
- [x] T006 Comprobación completa: base nueva + `--migrar` + IT, unitarias, Terraform de los tres stacks y diff sin `.github/` — cubre: P6 — verifica: todo en verde

## Correcciones de verificación

<!-- /sdd-verify añade aquí tareas si detecta fallos -->

- [x] T007 Corregir la configuración local documentada: las claves de prueba de Cloudflare devuelven `hostname: example.com` y ninguna `action` (comprobado con `siteverify` real), así que en local hay que dejar vacíos `HostnamesPermitidos` **y** `Accion` — archivos: README.md — cubre: RF-004, RF-008 — verifica: el README lo indica; E2E local con `Captcha__Accion=` → 201

## Cobertura

| Requisito | Tareas |
|---|---|
| RF-001 | T003 |
| RF-002 | T001 |
| RF-003 | T002 |
| RF-004 | T002 |
| RF-005 | T002, T003 |
| RF-006 | T002 |
| RF-007 | T002, T003 |
| RF-008 | T002, T004 |
| RF-009 | T002 |
| RNF-001 | T002 |
| RNF-002 | T005 |
| RNF-003 | T002 |
