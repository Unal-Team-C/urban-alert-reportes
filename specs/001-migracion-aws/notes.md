# Notas: 001-migracion-aws

## Indicaciones técnicas de la fuente

Para `/sdd-plan` (no son requisitos):

- Patrón de `urban-alert-usuarios` (spec 001) y `urban-alert-geoespacial` (spec 002): stacks `infra/base` (ECR, roles propios, SG, RDS, secretos, logs, clúster, task definition de migración), `infra/service` (`aws_ecs_express_gateway_service`) e `infra/routes` (HTTP_PROXY por VPC Link, `overwrite:header.host` + `server_name_to_verify` = endpoint, payload 1.0, `overwrite:path`); estado en `s3://urban-alert-tfstate-348364742283/reportes/{base,service,routes}.tfstate`.
- Scripts reutilizables de Usuarios: `check-platform.sh`, `buscar-listener-express.sh`, `run-schema-task.sh`, `diagnose-service.sh`, humo por el gateway.
- Roles propios `urban-alert-reportes-ecs-{execution,infrastructure,task}`; no reutilizar `urban-alert-ecs-*` (Geoespacial). Secretos acotados a `urban-alert-reportes-*`.
- SG de tareas con entrada al puerto del contenedor desde el CIDR de la VPC (lección de Geoespacial T037).
- Rol de tarea: `execute-api:Invoke` sobre `<api-execution-arn>/$default/POST/api/v1/geoespacial/coordinates`; `events:PutEvents` sobre el ARN del bus.
- .NET: firma SigV4 con `AWSSDK.Core` (`AWS4Signer`) o un `DelegatingHandler` propio en el `HttpClient` de Geoespacial, activado solo en la nube. Credenciales del rol de tarea por la cadena por defecto del SDK.
- Multimedia: `MultimediaHttpClient` construye el evento del RIE (payload 2.0). En la nube: modo "gateway" = POST multipart directo a `<api-endpoint>/api/v1/multimedia/images` (respuesta HTTP normal, sin sobre). Configuración `Multimedia:Modo` (`Emulador` | `Http`).
- EventBridge: `AWSSDK.EventBridge` `PutEvents` detrás de `IEventPublisher` (ya existe la abstracción); MassTransit/RabbitMQ solo en local (`Mensajeria:Transporte`). Comprobar `FailedEntryCount`.
- Migraciones EF Core: `dotnet ef migrations bundle` (ejecutable autocontenido) en una tarea puntual como administrador, o un modo `--migrar` del propio API.dll con la misma imagen (más simple: una sola imagen). Tras migrar: crear/sincronizar el rol de la app sin DDL (`GRANT SELECT, INSERT, UPDATE, DELETE`) y `ALTER DEFAULT PRIVILEGES`.
- Npgsql: `SSL Mode=VerifyFull` con el bundle de CA de RDS en la imagen, o `Require`; `Maximum Pool Size`.
- Logs: `builder.Logging.AddJsonConsole()` fuera de Development; `UseHttpLogging` o middleware propio con una línea por petición.
- Apagado: `HostOptions.ShutdownTimeout` < 30 s (Express envía SIGTERM; .NET lo atiende).
- Salud: `/health` ya existe con check de Postgres; puerto 8080 (imagen aspnet ya usa `APP_UID` no root).
- Rutas gateway: `ANY /api/v1/reportes` y `ANY /api/v1/reportes/{proxy+}` → `/api/v1/Reportes` y `/api/v1/Reportes/{proxy}` (o por método si las escrituras van con IAM).
- Límite del gateway: 10 MB por petición (HTTP API); imágenes mayores → 413 del gateway.
- Auditoria lee la tabla `Reportes` en local con `reportes_reader` (`UrbanAlert/infrastructure/postgres-reportes/01-auditoria.sh`).
