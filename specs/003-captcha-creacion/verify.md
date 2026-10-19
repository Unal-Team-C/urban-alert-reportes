# Verificación: Verificación humana (captcha) al crear reportes

- **Fecha**: 2026-10-19
- **Rama**: `003-captcha-creacion`, apilada sobre `002-resiliencia-eventos` (PR #7 sin merge). Commits locales sin push.
- **Resultado global**: ✅ en local, incluida una prueba de punta a punta contra el `siteverify` real de Cloudflare con el secreto de prueba. ⚠️ CI, despliegue y activación con claves reales quedan pendientes del equipo.

## Comandos

| Comando | Resultado |
|---|---|
| `$DOTNET build -c Release` | ✅ 0 errores, 0 advertencias |
| `$DOTNET test tests/UnitTests` | ✅ 131/131 (112 tras T001; 19 nuevas del verificador) |
| Base nueva + `--migrar` + `$IT` | ✅ 89/89 (83 de la 002 sin cambios + 6 nuevas) |
| Terraform `base` / `service` / `routes` (`validate` + `test`) | ✅ 13/13, 9/9 (2 nuevas), 7/7; `fmt` limpio |
| `git diff origin/main -- .github` | ✅ sin cambios |
| E2E local (Reportes en contenedor, `siteverify` real con el secreto de prueba, Geoespacial y Multimedia simulados) | ✅ `curl` sin token → 400, con `XXXX.DUMMY.TOKEN.XXXX` → 201; desde el portal (004) → 201, con `siteverify` 200 en 74 ms **antes** de Multimedia y Geoespacial (según los logs) |

## Matriz de requisitos

| Requisito | Evidencia | Estado |
|---|---|---|
| RF-001 prueba exigida en JSON y multipart | IT `CaptchaEndpointsTests` (JSON y multipart: válido 201, sin token 400) | ✅ |
| RF-002 verificar antes de todo | UT `CrearReporteHandlerTests` (sin llamadas si falla; antes de validar el reporte); orden en los logs del E2E | ✅ |
| RF-003 vacía, larga, inválida, vencida o usada → 400 | UT `VerificadorTurnstileTests` (`SinPrueba…`, `DemasiadoLarga…`, `invalid-input-response`, `timeout-or-duplicate`) | ✅ |
| RF-004 dominio y acción | UT `UnaPruebaDeOtroDominio…`, `UnaPruebaDeOtraAccion…`, `SinDominiosNiAccion…` | ✅ |
| RF-005 falla cerrado → 503 | UT (500, JSON ilegible, `invalid-input-secret`, `internal-error`, sin conexión); IT `ConElProveedorCaido_Responde503` | ✅ |
| RF-006 timeout, reintento solo sin conexión, circuito | UT `SinConexion…` (3 intentos), `UnErrorHttp…NoSeReintenta` (1), `ElCircuitoAbre…` (menos de 50 ms) | ✅ |
| RF-007 desactivado = como antes | UT `Desactivado_…`; IT: las 83 anteriores sin cambios; tftest `captcha_disabled_by_default` | ✅ |
| RF-008 configuración y secreto | `CaptchaOpciones` (no arranca si está activa sin clave: UT); tftest `captcha_enabled_reads_the_bootstrap_secret` (`value_from` del secreto de arranque) | ✅ |
| RF-009 log sin token ni secreto | UT `LosRechazosQuedanEnElLog_SinLaPruebaNiLaClaveSecreta` | ✅ |
| RNF-001 tiempo | Timeout de 5 s por intento; `siteverify` real en 74–228 ms | ✅ |
| RNF-002 coste | README: +0,4 USD/mes por el secreto; Turnstile gratuito | ✅ |
| RNF-003 sin secretos en logs | ídem RF-009; secreto fuera del repo, la imagen y el estado (D-002) | ✅ |

## Constitución

P1, P3–P10 ✅. **P2**: excepción justificada en el plan (D-002): el secreto de arranque `urban-alert-reportes-captcha` lo crea el equipo, porque su valor viene de Cloudflare y sobrevive a un Deprovision.

## Hallazgos

- **Corregido (T007)**: las claves de prueba devuelven `hostname: example.com` y ninguna `action`. El README ahora indica dejar vacíos los dos valores en local.
- **Pendiente del equipo, no bloquea**: crear el widget y el secreto en Cloudflare, cargar el secreto en Secrets Manager y activar en el orden documentado (primero el portal).
- **Fuera de alcance**: las rutas `PATCH`, `PUT` y `DELETE` siguen públicas (debilidad 1).
