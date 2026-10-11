# Plan técnico: Migración del Servicio de Reportes a AWS

- **Spec**: ./spec.md
- **Estado**: aprobado <!-- borrador | aprobado -->
- **Fecha**: 2026-10-11
- **Referencia**: `Unal-Team-C/urban-alert-usuarios` spec/plan 001 (mismo patrón, más reciente) y `Unal-Team-C/urban-alert-geoespacial` spec 002, ambos aplicados en la cuenta. Lo que aquí no se justifica de otra forma se copia de ese patrón.

## 1. Resumen técnico

Tres stacks de Terraform con estado en `s3://urban-alert-tfstate-348364742283/reportes/`: **base** (ECR, roles propios —ejecución, infraestructura y **tarea**—, grupos de seguridad, RDS PostgreSQL 17 propio, secreto de la aplicación, logs, clúster y la task definition de migración), **service** (`aws_ecs_express_gateway_service` en las subredes privadas) y **routes** (2 rutas comodín públicas en el gateway compartido: `ANY /api/v1/reportes` y `ANY /api/v1/reportes/{proxy+}`). El esquema lo aplica la **misma imagen** en modo `--migrar` en una tarea ECS puntual, como administrador, que además crea y sincroniza el rol de la aplicación sin DDL. En el código, todo activado por configuración y con los valores por defecto de hoy en local: llamada a Geoespacial por el gateway **firmada con SigV4**, subida a Multimedia por el gateway como **multipart normal**, publicación del evento en **EventBridge** (`PutEvents`) en lugar de MassTransit/RabbitMQ, logs JSON con una línea por petición y apagado acotado. `provision.yml` (push a `main`) y `deprovision.yml` (manual) siguen el orden de Usuarios, más una comprobación del bus.

## 2. Contexto técnico

| Aspecto | Valor |
|---|---|
| Lenguaje / versión | C# / .NET 10 (`net10.0`), ASP.NET Core, EF Core 10 + Npgsql 10; SDK fijado en un `global.json` nuevo; HCL (Terraform ≥ 1.10), YAML de Actions, bash 3.2-compatible |
| Dependencias nuevas | `AWSSDK.EventBridge` **4.0.101.2** y `AwsSignatureVersion4` **5.1.0** (manejador `HttpClient` que firma con SigV4; usa las credenciales de `AWSSDK.Core`). Terraform: `hashicorp/aws ~> 6.0`, `hashicorp/random ~> 3.6`. Actions: `actions/checkout@v4`, `actions/setup-dotnet@v4`, `aws-actions/configure-aws-credentials@v4`, `aws-actions/amazon-ecr-login@v2`, `hashicorp/setup-terraform@v3` |
| Almacenamiento | RDS for PostgreSQL 17, `db.t4g.micro`, gp3 20 GiB (máx. 50), Single-AZ, cifrado, backups 7 días, `deletion_protection`, base `urbanalert_reportes` |
| Pruebas | `dotnet test` (xUnit; las de integración usan **PostgreSQL y RabbitMQ reales** por configuración —no Testcontainers, contra lo que dice el README—; en CI, *service containers*), `terraform fmt/validate/test` con `mock_provider`, `actionlint`, `shellcheck`, pruebas de scripts con stubs |
| Plataforma objetivo | AWS us-east-2, VPC/subredes privadas de la plataforma (SSM), ECS Express (Fargate, X86_64), 512 CPU / 1024 MiB por tarea, 1–5 tareas |
| Dependencias en la nube | Gateway (`api-endpoint` de SSM): `POST /api/v1/geoespacial/coordinates` (AWS_IAM), `POST /api/v1/multimedia/images` (pública). Bus `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos` (externo) |
| Restricciones | Rol OIDC solo desde `main`; políticas existentes del rol acotadas a `urban-alert-*` y **sin ninguna acción `events:*`** (D-016); ≤ 50 conexiones; sin `aws`, `terraform` ni SDK .NET en el Mac (todo por Podman o en CI) |

**Comandos locales**

```bash
podman run --rm -v "$PWD":/w -w /w mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/UnitTests/UnitTests.csproj
podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5 -chdir=infra/<stack> init -backend=false   # luego validate / test
podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5 fmt -check -recursive infra
podman run --rm -v "$PWD":/repo -w /repo docker.io/rhysd/actionlint:1.7.7
podman run --rm -v "$PWD":/mnt -w /mnt docker.io/koalaman/shellcheck:v0.10.0 scripts/*.sh scripts/tests/*.sh
```

Las de integración, en local: `podman compose -f deploy/docker/docker-compose.yml up -d` + variables `ConnectionStrings__Postgres`, `RabbitMq__*` (o User Secrets) y `dotnet test tests/IntegrationTests` en un contenedor con `--network host`.

## 3. Verificación contra la constitución

| Principio | ¿Cumple? | Nota |
|---|---|---|
| P1 IaC solo desde Actions | Sí | Todo en `infra/`; `apply`/`destroy` solo en los workflows sobre `main` |
| P2 Arranque y recursos ajenos fuera | Sí | Roles y clúster propios (D-004, D-013); el bus solo se referencia por ARN (variable) y se comprueba con `describe-event-bus`; rutas de Geoespacial/Multimedia solo se invocan |
| P3 Sin secretos | Sí | OIDC; contraseñas en Secrets Manager; firma con credenciales temporales del rol de tarea; nada sensible en salidas ni logs |
| P4 Destructivo solo explícito | Sí | `deletion_protection` + `allow_destroy` solo desde `deprovision.yml` con confirmación `urban-alert-reportes`; copia final |
| P5 Coste mínimo | Sí | D-014; tabla de costes en el README. EventBridge: 1 USD por millón de eventos (~0) |
| P6 Validación previa | Sí | Job `test` antes de `provision`; mismas comprobaciones antes de cada commit de tarea |
| P7 Pruebas de Terraform | Sí | `.tftest.hcl` en los 3 stacks (§9), incluidos los permisos mínimos del rol de tarea |
| P8 Versiones fijadas | Sí | `global.json` nuevo, NuGet con versión exacta, Terraform 1.16.5 + lock con 3 plataformas, imágenes de herramientas por versión |
| P9 Contrato y nombres | Sí | Solo SSM `/urban-alert/plataforma/*`; prefijo `urban-alert-reportes`; etiquetas `Service=reportes`; rutas solo bajo `/api/v1/reportes` |
| P10 Serializado e idempotente | Sí | `concurrency: urban-alert-reportes-aws` sin cancelación en ambos workflows |

## 4. Arquitectura y componentes

```text
GitHub push a main ── provision.yml ── OIDC ──┐                 workflow_dispatch ── deprovision.yml
                                              ▼
┌──── AWS us-east-2 · VPC de la plataforma (SSM /urban-alert/plataforma/*) ─────────────────────────────────┐
│ stack base                                                                                                │
│   ECR urban-alert-reportes ◄── docker push :<sha>                                                         │
│   IAM urban-alert-reportes-ecs-{execution,infrastructure,task}                                            │
│        task: execute-api:Invoke POST /api/v1/geoespacial/coordinates · events:PutEvents en el bus         │
│   ECS cluster urban-alert-reportes                                                                        │
│   SG urban-alert-reportes-tasks ──5432──► SG urban-alert-reportes-rds                                     │
│   RDS urban-alert-reportes-db (PG17, privada, TLS) ── secreto rds!… (postgres, gestionado por RDS)       │
│   Secreto urban-alert-reportes-db {username,password,host,port,dbname,connection_string}                 │
│   Logs /ecs/urban-alert-reportes · /ecs/urban-alert-reportes-migrate (30 días)                           │
│   Task definition urban-alert-reportes-migrate (imagen :<sha>, `dotnet API.dll --migrar`)                │
│ paso: run-schema-task.sh → EF Core Migrate() + rol reportes_app → exit 0                                  │
│ stack service: Express urban-alert-reportes (:8080, /health, 1–5 tareas, Production)                     │
│ stack routes: ANY /api/v1/reportes y ANY /api/v1/reportes/{proxy+} (NONE) → VPC Link → listener 443       │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
Tarea Reportes ── NAT ──► API Gateway ─► POST /api/v1/geoespacial/coordinates (SigV4) ─► Geoespacial
                                      └► POST /api/v1/multimedia/images (multipart) ─► Lambda Multimedia
               ── NAT ──► EventBridge PutEvents (bus urban-alert-bus-eventos) ─► (reglas de Auditoria, fuera de alcance)
```

**Componentes**

1. **Aplicación** (todo por configuración; sin ella, el comportamiento local de hoy):
   - `Infrastructure/DependencyInjection.cs`: lee `Mensajeria:Transporte` (`RabbitMq` por defecto | `EventBridge`), `Geoespacial:RutaCoordenadas` (por defecto `api/v1/geospatial/coordinates`), `Geoespacial:FirmaIam:Region` (si tiene valor, añade el manejador SigV4 al `HttpClient`) y `Multimedia:Modo` (`Emulador` por defecto | `Gateway`). Con `EventBridge`, MassTransit no se registra (no exige `RabbitMq:Host`).
   - `Infrastructure/Mensajeria/EventBridgeEventPublisher.cs` (nuevo): `PutEvents` con `EventBusName = EventBridge:Bus`, `Source = urban-alert.reportes`, `DetailType` = nombre del tipo sin el sufijo `Event` (`ReporteCreado`), `Detail` = JSON web (camelCase) con el convertidor de enums de la API. `FailedEntryCount > 0` → excepción con el código de error (mismo efecto que hoy un fallo de RabbitMQ).
   - `Infrastructure/Geoespacial/GeoespacialHttpClient.cs`: la ruta pasa a venir de configuración (en la nube `api/v1/geoespacial/coordinates`). La firma la añade `AwsSignatureHandler` (servicio `execute-api`, región, credenciales del rol de tarea por la cadena por defecto del SDK).
   - `Infrastructure/Multimedia/MultimediaHttpClient.cs`: se separa en la construcción del multipart y la interpretación de la respuesta (comunes) y dos transportes: `Emulador` (sobre del RIE, el de hoy) y `Gateway` (`POST api/v1/multimedia/images` con `multipart/form-data`; el código y el cuerpo HTTP se interpretan con las mismas reglas: 201, 400/413/415, otros).
   - `Infrastructure/Persistencia/MigracionNube.cs` (nuevo): `Database.Migrate()` con la cadena de administrador y, después, SQL idempotente que crea/sincroniza el rol de la aplicación (§6). Devuelve 0/≠0.
   - `API/Program.cs`: si `args` contiene `--migrar`, ejecuta `MigracionNube` con la configuración `Migracion:*` y termina sin levantar la web (no registra Geoespacial, Multimedia ni mensajería). Fuera de eso: `UseHttpLogging` (una línea por petición, D-008), `HostOptions.ShutdownTimeout` desde `Apagado:TimeoutSegundos` (20 s en la nube).
   - `appsettings.json`: claves nuevas con valores por defecto locales; nivel `Information` para `Microsoft.AspNetCore.HttpLogging`.
   - `deploy/docker/Dockerfile`: ya corre sin root (`APP_UID`) en 8080 con `ENTRYPOINT` exec; se añade `--platform` en el build del workflow y `.dockerignore` en la raíz.
   - `global.json` (nuevo): SDK 10.0.x de la imagen `sdk:10.0` usada, `rollForward: latestPatch`.
2. **`infra/base`**, **`infra/service`**, **`infra/routes`**: §5 y §7.
3. **Scripts** (`scripts/`): `check-platform.sh` (+ comprobación del bus), `buscar-listener-express.sh`, `run-schema-task.sh`, `diagnose-service.sh` copiados de Usuarios; `smoke-gateway-reportes.sh` y `crear-reporte-prueba.sh` nuevos; pruebas en `scripts/tests/`.
4. **Workflows**: `provision.yml`, `deprovision.yml` (§7).
5. **Documentación**: `README.md` (despliegue, rutas, dependencias, variables, costes; corrige la mención a Testcontainers), `docs/funcionamiento.md` (transportes por entorno, evento en EventBridge), `infra/INVENTARIO.md`, `infra/bootstrap/README.md` + `infra/bootstrap/github-reportes-policy.json` (D-016).

## 5. Estructura de archivos

```text
global.json  .dockerignore                                            (nuevos)
src/API/Program.cs  src/API/appsettings.json                           (mod.)
src/Infrastructure/Infrastructure.csproj                               (mod.)  AWSSDK.EventBridge, AwsSignatureVersion4
src/Infrastructure/DependencyInjection.cs                              (mod.)
src/Infrastructure/Mensajeria/EventBridgeEventPublisher.cs             (nuevo)
src/Infrastructure/Geoespacial/GeoespacialHttpClient.cs                (mod.)  ruta configurable
src/Infrastructure/Multimedia/MultimediaHttpClient.cs                  (mod.)  modos Emulador | Gateway
src/Infrastructure/Persistencia/MigracionNube.cs                       (nuevo)
tests/UnitTests/Infrastructure/EventBridgeEventPublisherTests.cs       (nuevo)  (UnitTests referencia Infrastructure)
tests/UnitTests/Infrastructure/FirmaGeoespacialTests.cs                (nuevo)
tests/IntegrationTests/Multimedia/MultimediaHttpClientTests.cs         (mod.)  casos del modo Gateway
tests/IntegrationTests/Persistencia/MigracionNubeTests.cs              (nuevo)  contra el PostgreSQL de las IT
tests/IntegrationTests/Configuracion/ConfiguracionNubeTests.cs         (nuevo)  DI con la configuración de la nube
infra/
  INVENTARIO.md  bootstrap/README.md  bootstrap/github-reportes-policy.json
  base/    versions.tf backend.tf providers.tf variables.tf terraform.tfvars
           network.tf ecr.tf iam.tf ecs.tf rds.tf secrets.tf logs.tf migrate_task.tf outputs.tf
           tests/base.tftest.hcl  .terraform.lock.hcl
  service/ versions.tf backend.tf providers.tf variables.tf terraform.tfvars main.tf outputs.tf
           tests/service.tftest.hcl  .terraform.lock.hcl
  routes/  versions.tf backend.tf providers.tf variables.tf terraform.tfvars main.tf outputs.tf
           tests/routes.tftest.hcl  .terraform.lock.hcl
scripts/
  check-platform.sh buscar-listener-express.sh run-schema-task.sh diagnose-service.sh   (de Usuarios)
  smoke-gateway-reportes.sh crear-reporte-prueba.sh                                    (nuevos)
  tests/test-check-platform.sh tests/test-buscar-listener.sh tests/test-smoke.sh
.github/workflows/provision.yml  deprovision.yml
README.md  docs/funcionamiento.md  CLAUDE.md                            (mod.)
```

## 6. Modelo de datos

Sin cambios en la tabla `Reportes` ni en las migraciones de EF Core. Solo en la nube:

- **Rol `reportes_app`**: `LOGIN`; `CONNECT` sobre `urbanalert_reportes`, `USAGE` sobre `public`, `SELECT, INSERT, UPDATE, DELETE` sobre todas las tablas de `public` y privilegios por defecto (`ALTER DEFAULT PRIVILEGES FOR ROLE postgres`) para tablas futuras. Sin `CREATE`. La contraseña se fija con `set_config` + `format('%L')` dentro de un bloque `DO` (sin interpolar texto en el SQL).
- **Secreto `urban-alert-reportes-db`** (JSON): `username`, `password` (`random_password`, 32 alfanuméricos), `host`, `port`, `dbname`, `connection_string = Host=<host>;Port=5432;Database=urbanalert_reportes;Username=reportes_app;Password=<…>;SSL Mode=Require;Maximum Pool Size=8;Minimum Pool Size=0`.
- **Secreto `rds!…`**: usuario maestro `postgres`, gestionado por RDS; solo lo lee la tarea de migración.

## 7. Contratos / interfaces

**API HTTP**: sin cambios en el servicio. En el gateway (RF-021), las dos rutas, `authorization_type = NONE`:

| Ruta pública | Ruta del servicio (`overwrite:path`) |
|---|---|
| `ANY /api/v1/reportes` | `/api/v1/Reportes` |
| `ANY /api/v1/reportes/{proxy+}` | `/api/v1/Reportes/$request.path.proxy` |

La cadena de consulta (`?pagina=…&tipo=…`) se conserva (solo se reescribe la ruta). Integraciones como en Usuarios: `HTTP_PROXY`, `VPC_LINK`, listener 443, payload 1.0, `tls_config.server_name_to_verify` y `overwrite:header.host` = endpoint de Express.

**Evento en EventBridge** (RF-012):

```json
{ "Source": "urban-alert.reportes", "DetailType": "ReporteCreado", "EventBusName": "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos",
  "Detail": "{\"idEvento\":\"…\",\"reporte\":{\"id\":\"…\",\"estado\":\"REPORTADO\",\"idCoordenada\":\"…\",\"idUsuario\":\"…\",\"fecha\":\"2026-10-11T…Z\"}}" }
```

**Variables de entorno**

| Variable | Servicio | Tarea de migración |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Production` |
| `ConnectionStrings__Postgres` | secreto app `connection_string` | — |
| `Migracion__Host` / `__Base` | — | host de RDS / `urbanalert_reportes` |
| `Migracion__UsuarioAdmin` / `__ContrasenaAdmin` | — | secreto `rds!…` `username` / `password` |
| `Migracion__UsuarioApp` / `__ContrasenaApp` | — | `reportes_app` / secreto app `password` |
| `Geoespacial__BaseUrl` / `__RutaCoordenadas` | `<api-endpoint>` / `api/v1/geoespacial/coordinates` | — |
| `Geoespacial__FirmaIam__Region` | `us-east-2` | — |
| `Multimedia__BaseUrl` / `__Modo` | `<api-endpoint>` / `Gateway` | — |
| `Mensajeria__Transporte` / `EventBridge__Bus` | `EventBridge` / ARN del bus | — |
| `Logging__Console__FormatterName` | `json` | `json` |
| `Apagado__TimeoutSegundos` | `20` | — |
| `AWS_REGION` | `us-east-2` | — |

Comando de la tarea de migración: `["dotnet", "API.dll", "--migrar"]`.

**Logs**: con el formateador `json` de `Microsoft.Extensions.Logging.Console` (sin dependencias): una línea por evento con `Timestamp`, `LogLevel`, `Category`, `Message` y `State`; `HttpLogging` con `CombineLogs` deja una línea por petición con método, ruta, código y duración.

**Terraform → workflows**: base → `name`, `ecr_repository_url`, `cluster_name`, `migrate_task_definition_arn`, `migrate_container_name`, `migrate_log_group`, `subnet_ids`, `tasks_security_group_id`; service → `service_endpoint`, `cluster_name`, `service_name`; routes → `gateway_url`, `route_keys`.

**`provision.yml`** (push a `main`; `concurrency: urban-alert-reportes-aws`, sin cancelar)

| Job | Pasos |
|---|---|
| `test` | *services* `postgres:16-alpine` y `rabbitmq:4-alpine` → checkout → setup-dotnet (desde `global.json`) → `dotnet test` (variables de conexión a los *services*) → pruebas de scripts → Terraform `fmt -check`, `init -backend=false -lockfile=readonly`, `validate`, `test` de los 3 stacks → actionlint → shellcheck |
| `provision` (needs `test`) | OIDC → `check-platform.sh` (SSM + bus) → apply base (`image_tag=$SHA`) → login ECR → build `--platform linux/amd64` + push (omitido si el tag existe) → `run-schema-task.sh` → apply service → (si falla) `diagnose-service.sh` → `buscar-listener-express.sh` → apply routes → `smoke-gateway-reportes.sh` → resumen |

**`deprovision.yml`** (`workflow_dispatch`, input `confirm` = `urban-alert-reportes`, solo `main`, misma `concurrency`): routes destroy → service destroy → base apply `allow_destroy=true final_snapshot_suffix=<run_id>` → base destroy. Cada paso comprueba si su estado está vacío. No toca el bus.

**Variables y secretos de GitHub en `urban-alert-reportes`**: secreto `AWS_ROLE_ARN` (el rol compartido) y variable `AWS_REGION=us-east-2`; los configura el usuario.

## 8. Decisiones

### D-001: Tres stacks (base, service, routes), como Usuarios y Geoespacial
- **Decisión**: estados `reportes/{base,service,routes}.tfstate`; service lee base por `terraform_remote_state`; routes recibe endpoint y listener por variables.
- **Motivo**: la migración va entre base y service (RF-017) y el listener solo existe tras desplegar el servicio. Patrón probado.

### D-002: Migración con la misma imagen en modo `--migrar`, en una tarea puntual
- **Decisión**: la task definition `urban-alert-reportes-migrate` ejecuta `dotnet API.dll --migrar` como `postgres`; aplica `Database.Migrate()` (EF Core usa un bloqueo de migración en PostgreSQL desde EF 9) y crea/sincroniza `reportes_app`. El servicio nunca migra fuera de `Development` (ya es así).
- **Alternativas**: `dotnet ef migrations bundle` (otro artefacto y otra imagen o capa); migrar al arrancar (exige DDL al usuario de la app y compite entre tareas).
- **Motivo**: RF-008/RF-009 sin segunda imagen; las migraciones viajan compiladas en `Infrastructure.dll`.

### D-003: Rol de la aplicación creado por la migración, en SQL idempotente
- **Decisión**: tras `Migrate()`, en una transacción: `CREATE ROLE` si no existe, `ALTER ROLE … PASSWORD` (vía `set_config` + `format('%L')`), `GRANT CONNECT/USAGE/SELECT, INSERT, UPDATE, DELETE` y `ALTER DEFAULT PRIVILEGES`. Se repite en cada despliegue.
- **Alternativas**: migración EF con el rol (la contraseña no debe ir en una migración versionada); `null_resource` con `psql` desde el runner (el runner no llega a la base privada).
- **Motivo**: equivalente al `afterMigrate.sql` de Usuarios; la contraseña nunca se registra.

### D-004: Roles IAM propios, con rol de tarea
- **Decisión**: `urban-alert-reportes-ecs-execution` (`AmazonECSTaskExecutionRolePolicy` + lectura solo de `secret:urban-alert-reportes-*` y del `rds!*` de su instancia), `-ecs-infrastructure` (`AmazonECSInfrastructureRoleforExpressGatewayServices`) y **`-ecs-task`** con política en línea: `execute-api:Invoke` sobre `<api-execution-arn>/$default/POST/api/v1/geoespacial/coordinates` y `events:PutEvents` sobre el ARN del bus. La tarea de migración no usa rol de tarea.
- **Alternativas**: reutilizar `urban-alert-ecs-*` de Geoespacial.
- **Motivo**: P2, RF-015, RNF-001 (mínimo privilegio). El rol de GitHub ya puede crearlos (`iam:CreateRole`/`PutRolePolicy` sobre `role/urban-alert-*`, `iam:PassRole` a `ecs-tasks`).

### D-005: Firma SigV4 con `AwsSignatureVersion4`
- **Decisión**: `AddHttpMessageHandler(() => new AwsSignatureHandler(new AwsSignatureHandlerSettings(region, "execute-api", credenciales)))` solo si `Geoespacial:FirmaIam:Region` tiene valor; credenciales del SDK (`DefaultAWSCredentialsIdentityResolver`), que en ECS lee el endpoint de credenciales del contenedor y las renueva.
- **Alternativas**: firmar a mano con HMAC (~100 líneas propias de criptografía); llamar a Geoespacial por el ALB interno (prohibido por la guía §5).
- **Motivo**: biblioteca pequeña y usada para exactamente esto. Riesgo: depende de `AWSSDK.Core ≥ 3.7` y el proyecto usará `AWSSDK.Core` 4.x por EventBridge; se comprueba en la primera tarea con una prueba de firma (plan B: manejador propio con `AWS4Signer` del SDK, §10).

### D-006: Multimedia por el gateway con multipart normal
- **Decisión**: `Multimedia:Modo = Gateway` → `POST {BaseUrl}api/v1/multimedia/images` con `MultipartFormDataContent` (`lat`, `lon`, `image`) y respuesta HTTP directa; mismas reglas de interpretación que hoy (201 → `imageUrl`; 400/413/415 → `ImagenInvalidaException`; otros y errores de red → `MultimediaNoDisponibleException`). `Emulador` sigue siendo el valor por defecto (local).
- **Alternativas**: `lambda:Invoke` directo con el SDK reutilizando el sobre (la guía pide llamar solo por el gateway y no hay permiso sobre la función de otro repo).
- **Motivo**: la ruta ya es pública (`AWS_PROXY` 2.0) y es el contrato que verá cualquier cliente. 3,5 MiB máximo de imagen < 10 MB del gateway.

### D-007: EventBridge detrás de `IEventPublisher`; RabbitMQ solo en local
- **Decisión**: `Mensajeria:Transporte = EventBridge` registra `IAmazonEventBridge` (región de `AWS_REGION`) y `EventBridgeEventPublisher`; sin MassTransit en la nube. Formato de §7.
- **Alternativas**: MassTransit con transporte SQS/SNS (no habla EventBridge); un puente RabbitMQ→EventBridge.
- **Motivo**: RF-012 con la abstracción que ya existe; el handler no cambia.

### D-008: Logs JSON y una línea por petición sin dependencias
- **Decisión**: `Logging__Console__FormatterName=json` en la nube (local sigue en texto); `AddHttpLogging(o => { o.LoggingFields = RequestMethod | RequestPath | ResponseStatusCode | Duration; o.CombineLogs = true; })` y `UseHttpLogging()` en todos los entornos.
- **Motivo**: RF-004 con lo que trae ASP.NET Core 10; no registra cabeceras ni cuerpos (sin datos personales ni imágenes).

### D-009: Apagado acotado
- **Decisión**: `HostOptions.ShutdownTimeout = Apagado:TimeoutSegundos` (20 s en la nube, por defecto el de .NET, 30 s); Kestrel deja de aceptar y espera a las peticiones en curso al recibir `SIGTERM` (`dotnet` es PID 1 por el `ENTRYPOINT` exec).
- **Motivo**: RF-005 dentro de los 30 s de parada de ECS.

### D-010: TLS con `SSL Mode=Require`
- **Decisión**: Npgsql cifra sin validar la CA (comportamiento de `Require` desde Npgsql 8); RDS PG 17 tiene `rds.force_ssl=1`, así que una conexión sin TLS se rechaza.
- **Alternativas**: `VerifyFull` con el bundle de CA de RDS en la imagen.
- **Motivo**: RF-002 como Usuarios (D-008) y Geoespacial, sin certificados en la imagen.

### D-011: Presupuesto de conexiones
- **Decisión**: `Maximum Pool Size=8` × `max_tasks=5` = 40; `precondition` en service `db_pool_max × max_tasks ≤ 50`; la migración usa 1–2.
- **Motivo**: RNF-002.

### D-012: Rutas comodín públicas dentro del prefijo
- **Decisión**: `ANY /api/v1/reportes` y `ANY /api/v1/reportes/{proxy+}`.
- **Alternativas**: 11 rutas por método.
- **Motivo**: todas son públicas (clarificación) y reproducen el `location /api/v1/reportes/` del Nginx local; el controlador solo sirve bajo `/api/v1/Reportes`, y salud y OpenAPI quedan fuera del prefijo (404 del gateway, HU-6.3).

### D-013: Clúster ECS propio
- **Decisión**: `aws_ecs_cluster "urban-alert-reportes"`. **Motivo**: la tarea de migración corre antes que el servicio; P2.

### D-014: Tamaños y coste
- **Decisión**: RDS `db.t4g.micro` gp3 20 GiB Single-AZ (~15 USD/mes), Fargate 1 tarea 0,5 vCPU/1 GiB (~18 USD/mes), logs 30 días, ECR 20 imágenes, EventBridge ~0. ALB de Express y NAT compartidos.
- **Motivo**: RNF-006, P5.

### D-015: Prueba de humo sin escrituras; creación completa en verify
- **Decisión**: `smoke-gateway-reportes.sh <gateway>`: `GET /api/v1/reportes` → 200 con página JSON (reintentos 5 min); `GET /api/v1/reportes/catalogo` → 200; `GET /api/v1/reportes/<uuid aleatorio>` → 404; `GET /health`, `/scalar/v1`, `/api/v1/reportes-x` → 404 del gateway. `crear-reporte-prueba.sh <gateway>` (manual, `/sdd-verify`) crea un reporte con coordenada y otro con imagen, comprueba `idCoordenada`/`urlImagen`, y los borra.
- **Motivo**: cada Provision no debe crear datos ni emitir eventos de auditoría.

### D-016: Política de arranque nueva y mínima para el bus
- **Decisión**: `infra/bootstrap/github-reportes-policy.json` con una sola sentencia `events:DescribeEventBus` sobre el ARN del bus; un administrador la crea como `github-actions-urban-alert-reportes` y la adjunta al rol compartido de GitHub (paso de arranque, como la política propia de Multimedia).
- **Alternativas**: añadirla a la política de despliegue de Geoespacial (otro repo); no comprobar el bus (RF-022 pide fallar antes de cambiar nada).
- **Motivo**: es la única acción que falta (las políticas actuales no tienen `events:*`); el `PutEvents` del rol de tarea solo necesita `iam:PutRolePolicy`, ya permitido.

### D-017: CI con PostgreSQL y RabbitMQ como *service containers*
- **Decisión**: el job `test` levanta `postgres:16-alpine` y `rabbitmq:4-alpine` y pasa `ConnectionStrings__Postgres`, `RabbitMq__Host/Username/Password`; las IT corren en `Development` como hoy.
- **Alternativas**: migrar las IT a Testcontainers o sustituir el publicador por uno falso.
- **Motivo**: RF-024 (las pruebas siguen igual) y sin cambiar su semántica; se corrige el README.

## 9. Estrategia de pruebas

| Nivel | Qué | Cómo |
|---|---|---|
| Regresión | Suites existentes | `dotnet test` (CI con *services*) |
| Unitario | `EventBridgeEventPublisher`: bus, `Source`, `DetailType = ReporteCreado`, `Detail` en camelCase con `"estado":"REPORTADO"`; `FailedEntryCount > 0` → excepción | `IAmazonEventBridge` falso |
| Unitario | Firma: con `FirmaIam:Region`, la petición a Geoespacial sale con `Authorization: AWS4-HMAC-SHA256 … /us-east-2/execute-api/aws4_request` y `X-Amz-Security-Token` (credenciales fijas de prueba); sin la clave, sin cabecera; ruta configurable | `HttpMessageHandler` de captura |
| Integración | Multimedia modo `Gateway`: multipart con `lat`/`lon`/`image`, 201 → URL, 413/415/400 → `ImagenInvalidaException`, 5xx/red → no disponible; modo `Emulador` sin cambios | `MultimediaHttpClientTests` |
| Integración | `MigracionNube` contra el PostgreSQL de las IT, en una base temporal: crea el esquema completo; segunda ejecución sin errores; `reportes_app` puede SELECT/INSERT/UPDATE/DELETE en `Reportes` y no `CREATE TABLE`; cambio de contraseña sincronizado | `MigracionNubeTests` |
| Integración | DI con la configuración de la nube (`EventBridge`, `Gateway`, firma, sin `RabbitMq:Host`) resuelve los servicios correctos; `/health` responde | `ConfiguracionNubeTests` |
| Terraform base | RDS no pública, cifrada, backups ≥ 7, `deletion_protection` y `allow_destroy`; SG de RDS solo desde tareas; política de secretos acotada; rol de tarea con **solo** las dos acciones y recursos exactos; nombres | `base.tftest.hcl` |
| Terraform service | Precondición 8×5 pasa, 11×5 falla; subredes; variables de entorno de §7 (sin secretos en `environment`); `/health` y 8080; rol de tarea | `service.tftest.hcl` |
| Terraform routes | Exactamente las 2 claves; `NONE`; ninguna `$default`; VPC_LINK, payload 1.0, Host/TLS = endpoint, `overwrite:path` | `routes.tftest.hcl` |
| Scripts | `check-platform` (falta parámetro o bus → falla), `buscar-listener`, humo (stub de `curl`) | `scripts/tests/*.sh` |
| Workflows | Sintaxis y expresiones | `actionlint` |
| e2e | Humo por el gateway (D-015) | Último paso de `provision.yml` |
| Manual (`/sdd-verify`) | MS-002 con `crear-reporte-prueba.sh`; MS-003 (métrica `PutEventsApproximateSuccessCount`/`MatchedEvents` del bus o una regla temporal hecha a mano); MS-004; HU-2 y MS-006; logs en CloudWatch; RDS no accesible desde fuera | `verify.md` |

## 10. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| `AwsSignatureVersion4` incompatible con `AWSSDK.Core` 4.x | No compila o firma mal | Prueba de firma en la primera tarea; plan B: manejador propio de ~60 líneas sobre `Amazon.Runtime.Internal.Auth.AWS4Signer`, anotado en D-005 |
| Sin la política D-016 adjunta | Provision falla en `check-platform.sh` con `AccessDenied` | Mensaje explícito; paso de arranque documentado; nada se ha cambiado aún |
| El bus tiene una política de recursos que no admite al rol de tarea | `PutEvents` → `AccessDenied`, la creación responde 500 tras guardar | Misma cuenta: basta la política IAM salvo `Deny` explícito; se verifica en `/sdd-verify` y en `diagnose-service.sh` |
| Auditoria aún no consume de EventBridge | Los eventos no llegan a Auditoria | Fuera de alcance (spec §8); MS-003 se mide en el bus |
| Latencia añadida (NAT + gateway) hacia Geoespacial con timeout de 3 s | 503 intermitentes en creación | Mantener 3 s (RNF-008) y medir en verify; `Geoespacial__TimeoutSegundos` configurable si hace falta |
| .NET 10 arranca lento en 0,5 vCPU | Salud fallida | `diagnose-service.sh`; subir `cpu` en `terraform.tfvars` |
| Bloqueo de migraciones de EF Core | Dos migraciones a la vez | Solo una tarea de migración por Provision (concurrencia serializada) |
| `Require` sin validar CA | Suplantación dentro de la VPC | Aceptado como en Usuarios/Geoespacial (red privada) |
| Primer `apply` real solo en `main` | Errores de IAM tras el merge | Todo lo verificable en `test`; commits pequeños de corrección |
| IT dependen de servicios reales | Fallan en el Mac sin compose | Unitarias por Podman en local; IT en CI con *services* |

## 11. Trazabilidad

| Requisito | Componentes | Pruebas |
|---|---|---|
| RF-001 Config, secretos | `ConnectionStrings__Postgres` desde Secrets Manager; `service/main.tf` | `service.tftest.hcl`; MS-005 |
| RF-002 TLS | `SSL Mode=Require` (D-010) | `base.tftest.hcl` (connection_string); verify |
| RF-003 Pool | D-011 | `service.tftest.hcl` |
| RF-004 Logs | D-008 | `ConfiguracionNubeTests`; verify en CloudWatch |
| RF-005 Apagado | D-009 | Verify (despliegue sin 5xx) |
| RF-006 Imagen | Dockerfile (no root, 8080), `/health`, amd64 | Express saludable |
| RF-007 Tag = commit | `provision.yml`, ECR `IMMUTABLE` | Verify |
| RF-008 Esquema | D-002, `run-schema-task.sh` | `MigracionNubeTests` |
| RF-009 Usuario sin DDL | D-003 | `MigracionNubeTests` |
| RF-010 Geoespacial firmado | D-004, D-005 | `FirmaGeoespacialTests`, `base.tftest.hcl`; MS-002 |
| RF-011 Multimedia por gateway | D-006 | `MultimediaHttpClientTests`; MS-002 |
| RF-012 EventBridge | D-004, D-007 | `EventBridgeEventPublisherTests`, `base.tftest.hcl`; MS-003 |
| RF-013 Datos de dependencias | SSM en service; `event_bus_arn` en `terraform.tfvars` de base y service | `service.tftest.hcl` |
| RF-014 IaC | `infra/*` | `terraform validate/test`; MS-004 |
| RF-015 Sin recursos ajenos | D-004, D-013, D-016 | `base.tftest.hcl`; `INVENTARIO.md` |
| RF-016 Estado remoto | `backend.tf` ×3 | Verify |
| RF-017 Orden de Provision | `provision.yml` | Ejecución en `main` |
| RF-018 Deprovision | `deprovision.yml` | `base.tftest.hcl`; verify HU-2 |
| RF-019 OIDC y concurrencia | workflows | Revisión |
| RF-020 Red privada | `network.tf`, `run-schema-task.sh` | `base.tftest.hcl`; verify |
| RF-021 Rutas | D-012 | `routes.tftest.hcl`; humo |
| RF-022 Sin plataforma o bus | `check-platform.sh` | `test-check-platform.sh` |
| RF-023 Documentación | README, INVENTARIO, bootstrap | Verify HU-7 |
| RF-024 Local sin cambios | Valores por defecto = comportamiento actual | Suites; `docker compose up` |
| RNF-001 Seguridad | D-004, SG, rutas | `base.tftest.hcl`; MS-005 |
| RNF-002 Conexiones | D-011 | `service.tftest.hcl` |
| RNF-003 Idempotencia | D-001, D-003 | MS-004 |
| RNF-004 Sin caída | Express + D-009 | Verify |
| RNF-005 < 20 min | Caché de NuGet y de capas; omitir push si el tag existe | Verify |
| RNF-006 Coste | D-014 | README |
| RNF-007 Backups | `rds.tf` | `base.tftest.hcl` |
| RNF-008 Latencia | Timeouts sin cambios | Verify |
