# Tareas: Migración del Servicio de Reportes a AWS

- **Plan**: ./plan.md
- **Estado**: aprobado <!-- borrador | aprobado -->

Leyenda: `[ ]` pendiente · `[~]` en progreso · `[x]` hecha · `[!]` bloqueada · `[-]` descartada · `[P]` paralelizable

Formato de cada tarea (una línea):
`- [ ] T001 [P] Descripción imperativa — archivos: ruta/a, ruta/b — cubre: RF-001 — verifica: <comando o criterio observable>`

Dependencias: cada fase depende de la anterior; dentro de una fase se ejecuta en orden salvo `[P]`.

**Herramientas de verificación local** (sin SDK .NET, `terraform`, `aws` ni `actionlint` instalados; Podman con `podman machine start`):
- `$DOTNET` = `podman run --rm -v "$PWD":/w -w /w -v ~/.cache/nuget-podman:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:10.0 dotnet`
- `$TF` = `podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5` (`-chdir=infra/<stack> init -backend=false`, `validate`, `test`; `fmt -check -recursive infra`)
- `actionlint`: `podman run --rm -v "$PWD":/repo -w /repo docker.io/rhysd/actionlint:1.7.7`
- `shellcheck`: `podman run --rm -v "$PWD":/mnt -w /mnt docker.io/koalaman/shellcheck:v0.10.0 scripts/*.sh scripts/tests/*.sh`
- Integración (`$IT`): `podman compose -f deploy/docker/docker-compose.yml --env-file deploy/docker/.env up -d` y `$DOTNET test tests/IntegrationTests` con `--network host` y `-e ConnectionStrings__Postgres=… -e RabbitMq__Host=localhost -e RabbitMq__Username=… -e RabbitMq__Password=…`. Si no es posible en local, se verifica en el job `test` de CI y se anota en el journal.

## Fase 1: Preparación

- [x] T001 Fijar el SDK (`global.json`, versión de la imagen `sdk:10.0`, `rollForward: latestPatch`) y excluir del contexto de build `infra/`, `.github/`, `.sdd/`, `.claude/`, `specs/`, `scripts/`, `**/bin`, `**/obj` — archivos: global.json, .dockerignore — cubre: P8, RNF-001 — verifica: `$DOTNET build UrbanAlert.Reportes.slnx` en verde; `podman build -f deploy/docker/Dockerfile .` termina bien
- [x] T002 Añadir `AWSSDK.EventBridge` 4.0.101.2 y `AwsSignatureVersion4` 5.1.0 y comprobar que la firma funciona con `AWSSDK.Core` 4.x: prueba que firma una petición con credenciales fijas y obtiene `Authorization: AWS4-HMAC-SHA256 Credential=…/us-east-2/execute-api/aws4_request` — archivos: src/Infrastructure/Infrastructure.csproj, tests/UnitTests/UnitTests.csproj, tests/UnitTests/Infrastructure/FirmaGeoespacialTests.cs — cubre: RF-010 (riesgo D-005) — verifica: `$DOTNET test tests/UnitTests` en verde; si falla por incompatibilidad, aplicar el plan B de D-005 y anotarlo en el plan
- [x] T003 [P] Documentar el arranque reutilizado (OIDC, rol de GitHub y sus políticas, bucket) y el nuevo: `github-reportes-policy.json` (D-016) con los comandos para crearla y adjuntarla; `AWS_ROLE_ARN`/`AWS_REGION` — archivos: infra/bootstrap/README.md, infra/bootstrap/github-reportes-policy.json — cubre: RF-023, D-016 — verifica: JSON válido (`python3 -m json.tool`); el README lista cada recurso con su dueño, sin secretos
- [x] T004 [P] Crear el inventario: recursos de cada stack, recursos de arranque, plataforma, bus de EventBridge y rutas de Geoespacial/Multimedia como externos — archivos: infra/INVENTARIO.md — cubre: RF-015, RF-023 — verifica: cada recurso de §4 del plan aparece en su stack; bus y roles `urban-alert-ecs-*` figuran como ajenos

## Fase 2: Pruebas

- [x] T005 [P] Prueba de `EventBridgeEventPublisher` con un `IAmazonEventBridge` falso (bus, `Source`, `DetailType = ReporteCreado`, `Detail` camelCase con `"estado":"REPORTADO"`, `FailedEntryCount > 0` → excepción) — archivos: tests/UnitTests/Infrastructure/EventBridgeEventPublisherTests.cs — cubre: RF-012 — verifica: falla solo por compilación (la clase no existe)
- [x] T006 [P] Ampliar la prueba de firma: con `Geoespacial:FirmaIam:Region` el cliente registrado por DI firma y usa `Geoespacial:RutaCoordenadas`; sin ella no hay cabecera `Authorization` y la ruta es la de hoy — archivos: tests/UnitTests/Infrastructure/FirmaGeoespacialTests.cs — cubre: RF-010, RF-024 — verifica: falla porque la ruta/firma no son configurables aún
- [x] T007 [P] Casos del modo `Gateway` de Multimedia (multipart `lat`/`lon`/`image` a `api/v1/multimedia/images`; 201 → URL; 400/413/415 → `ImagenInvalidaException`; 500 y error de red → `MultimediaNoDisponibleException`) — archivos: tests/IntegrationTests/Multimedia/MultimediaHttpClientTests.cs — cubre: RF-011 — verifica: los casos nuevos fallan; los del emulador siguen en verde
- [x] T008 [P] Prueba de `MigracionNube` en una base temporal del PostgreSQL de las IT: esquema completo, segunda ejecución sin errores, `reportes_app` con CRUD en `Reportes` y sin `CREATE TABLE`, contraseña resincronizada — archivos: tests/IntegrationTests/Persistencia/MigracionNubeTests.cs — cubre: RF-008, RF-009 — verifica: falla por compilación ($IT o CI)
- [x] T009 [P] Prueba de configuración de la nube: con `Mensajeria:Transporte=EventBridge`, `Multimedia:Modo=Gateway`, firma y sin `RabbitMq:Host`, la DI resuelve `EventBridgeEventPublisher`, el cliente HTTP de Multimedia en modo gateway, y `/health` responde — archivos: tests/IntegrationTests/Configuracion/ConfiguracionNubeTests.cs — cubre: RF-010, RF-011, RF-012 — verifica: falla ($IT o CI)

## Fase 3: Núcleo

- [x] T010 Implementar `EventBridgeEventPublisher` y la selección de transporte (`Mensajeria:Transporte`, MassTransit solo con `RabbitMq`) — archivos: src/Infrastructure/Mensajeria/EventBridgeEventPublisher.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json — cubre: RF-012, RF-024 — verifica: T005 en verde — depende: T005
- [x] T011 Hacer configurables la ruta de Geoespacial y la firma SigV4 — archivos: src/Infrastructure/Geoespacial/GeoespacialHttpClient.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json — cubre: RF-010 — verifica: T006 en verde — depende: T002, T006
- [x] T012 Separar `MultimediaHttpClient` en multipart + interpretación comunes y los modos `Emulador` | `Gateway` — archivos: src/Infrastructure/Multimedia/MultimediaHttpClient.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json — cubre: RF-011, RF-024 — verifica: `MultimediaHttpClientTests` completo en verde (no necesita base) — depende: T007
- [x] T013 Implementar `MigracionNube` y el modo `--migrar` de `Program.cs` (sin registrar web ni dependencias externas; código de salida) — archivos: src/Infrastructure/Persistencia/MigracionNube.cs, src/API/Program.cs — cubre: RF-008, RF-009 — verifica: T008 en verde ($IT o CI) — depende: T008
- [x] T014 Logs y apagado: `AddHttpLogging`/`UseHttpLogging` (método, ruta, código, duración, `CombineLogs`), nivel en `appsettings.json`, `HostOptions.ShutdownTimeout` desde `Apagado:TimeoutSegundos` — archivos: src/API/Program.cs, src/API/appsettings.json — cubre: RF-004, RF-005 — verifica: T009 en verde ($IT o CI); `podman run` de la imagen con `Logging__Console__FormatterName=json` escribe líneas JSON y termina en < 20 s con `podman stop` — depende: T009, T010, T011, T012, T013
- [x] T015 Comprobar el entorno local sin cambios: `docker compose` + `dotnet run` (Development) crea un reporte con RabbitMQ y la suite completa pasa — archivos: (ninguno) — cubre: RF-024 — verifica: $IT en verde o CI; anotar en el journal — depende: T014
- [x] T016 Stack base, parte 1: versiones, backend `reportes/base.tfstate`, proveedor con etiquetas, variables con validaciones (`name` empieza por `urban-alert-reportes`, backups ≥ 7, `event_bus_arn` con formato de ARN de bus), `terraform.tfvars`; red: SSM, SG de tareas (salida total, entrada 8080 desde el CIDR de la VPC), SG de RDS (5432 solo desde tareas) — archivos: infra/base/versions.tf, backend.tf, providers.tf, variables.tf, terraform.tfvars, network.tf — cubre: RF-014, RF-016, RF-020, RNF-001 — verifica: `$TF -chdir=infra/base init -backend=false && validate` → Success
- [x] T017 Base, parte 2: ECR (inmutable, scan, 20 imágenes, `force_delete = allow_destroy`); roles `urban-alert-reportes-ecs-{execution,infrastructure,task}`; política en línea de secretos acotada; política del rol de tarea con `execute-api:Invoke` (`<api-execution-arn>/$default/POST/api/v1/geoespacial/coordinates`) y `events:PutEvents` (bus) — archivos: infra/base/ecr.tf, infra/base/iam.tf — cubre: RF-007, RF-010, RF-012, RF-015, RNF-001 — verifica: `validate` en verde — depende: T016
- [x] T018 Base, parte 3: RDS PG 17 (`db.t4g.micro`, gp3 20→50, Single-AZ, privada, cifrada, backups 7, `manage_master_user_password`, `deletion_protection = !allow_destroy`, copia final) y secreto de la app (`random_password`, JSON con `connection_string` `SSL Mode=Require;Maximum Pool Size=8`) — archivos: infra/base/rds.tf, infra/base/secrets.tf — cubre: RF-001, RF-002, RF-003, RF-018, RNF-006, RNF-007, HU-3 — verifica: `validate` en verde — depende: T016
- [x] T019 Base, parte 4: clúster, logs (30 días), task definition `urban-alert-reportes-migrate` (`--migrar`, secretos del maestro y de la app, X86_64), salidas — archivos: infra/base/ecs.tf, infra/base/logs.tf, infra/base/migrate_task.tf, infra/base/outputs.tf — cubre: RF-008, RF-014 — verifica: `validate` en verde; salidas de §7 del plan — depende: T017, T018
- [x] T020 Pruebas del stack base (RDS no pública/cifrada/backups, protección según `allow_destroy`, SG de RDS solo desde tareas, red de SSM, secretos acotados, rol de tarea con exactamente 2 acciones y sus recursos, `SSL Mode=Require`) y lock con 3 plataformas — archivos: infra/base/tests/base.tftest.hcl, infra/base/.terraform.lock.hcl — cubre: RF-002, RF-010, RF-012, RF-015, RF-018, RF-020, RNF-001, RNF-007 — verifica: `$TF -chdir=infra/base test` en verde — depende: T019
- [x] T021 Stack service (remote state de base, SSM `api-endpoint`, Express con todas las subredes, rol de tarea, 8080, `/health`, variables de §7 del plan, `ConnectionStrings__Postgres` como secreto, escalado 1–5, precondición ≤ 50, salida `service_endpoint`) con pruebas y lock — archivos: infra/service/*.tf, infra/service/terraform.tfvars, infra/service/tests/service.tftest.hcl, infra/service/.terraform.lock.hcl — cubre: RF-001, RF-003, RF-006, RF-013, RF-014, RF-020, RNF-002 — verifica: `init -backend=false && validate && test` en verde (8×5 pasa, 11×5 falla)
- [x] T022 Stack routes (SSM `api-id`/`vpc-link-id`/`api-endpoint`, 2 integraciones y rutas `ANY` de §7, salidas) con pruebas y lock — archivos: infra/routes/*.tf, infra/routes/terraform.tfvars, infra/routes/tests/routes.tftest.hcl, infra/routes/.terraform.lock.hcl — cubre: RF-021, HU-6 — verifica: `init -backend=false && validate && test` en verde; `$TF fmt -check -recursive infra` sin diferencias

## Fase 4: Integración

- [x] T023 [P] Copiar de Usuarios `check-platform.sh` (+ comprobación del bus con `aws events describe-event-bus`), `buscar-listener-express.sh`, `run-schema-task.sh`, `diagnose-service.sh` y sus pruebas — archivos: scripts/check-platform.sh, scripts/buscar-listener-express.sh, scripts/run-schema-task.sh, scripts/diagnose-service.sh, scripts/tests/test-check-platform.sh, scripts/tests/test-buscar-listener.sh — cubre: RF-008, RF-017, RF-020, RF-022 — verifica: pruebas de scripts en verde (incluido «bus inexistente → falla»); shellcheck sin avisos
- [x] T024 [P] Prueba de humo por el gateway (D-015) y su prueba con `curl` simulado — archivos: scripts/smoke-gateway-reportes.sh, scripts/tests/test-smoke.sh — cubre: RF-017, RF-021, HU-6 — verifica: `bash scripts/tests/test-smoke.sh` en verde (falla si `/health` llega al servicio); shellcheck sin avisos
- [x] T025 [P] `crear-reporte-prueba.sh <gateway>`: crea un reporte JSON y otro multipart con una imagen PNG mínima, comprueba `idCoordenada` y que `urlImagen` responde 200, y los borra — archivos: scripts/crear-reporte-prueba.sh — cubre: MS-002, HU-4 — verifica: shellcheck sin avisos; sin argumentos muestra el uso y sale ≠ 0
- [x] T026 `provision.yml` (push a `main`, `concurrency: urban-alert-reportes-aws`; job `test` con *services* PostgreSQL y RabbitMQ; job `provision` en el orden de §7) — archivos: .github/workflows/provision.yml — cubre: RF-007, RF-017, RF-019, RNF-003, RNF-005 — verifica: actionlint sin errores — depende: T020, T021, T022, T023, T024
- [ ] T027 `deprovision.yml` (manual, confirmación `urban-alert-reportes`, solo `main`, misma `concurrency`; routes → service → base `allow_destroy` → destroy; tolera estado vacío) — archivos: .github/workflows/deprovision.yml — cubre: RF-018, RF-019, HU-2 — verifica: actionlint sin errores — depende: T026

## Fase 5: Pulido

- [ ] T028 Documentar: README (arquitectura en AWS, rutas, dependencias y transporte por entorno, workflows, variables/secretos, costes, política D-016, desaprovisionar; corregir la mención a Testcontainers), `docs/funcionamiento.md` (configuración nube/local, evento en EventBridge) y `CLAUDE.md` — archivos: README.md, docs/funcionamiento.md, CLAUDE.md — cubre: RF-023, RNF-006, HU-7 — verifica: el README contiene rutas, formato del evento, `AWS_ROLE_ARN`/`AWS_REGION` y la tabla de costes; sin secretos
- [ ] T029 Comprobación completa antes del PR: `dotnet test` (unitarias local; IT local o en CI), Terraform x3, pruebas de scripts, actionlint, shellcheck; búsqueda de secretos — archivos: (ninguno) — cubre: RF-024, RNF-001, P6 — verifica: todo en verde; `git grep -n -i -E "password|secret" -- ':!*.md'` solo muestra nombres de variables/secretos
- [ ] T030 Pedir al usuario: crear y adjuntar la política D-016, configurar `AWS_ROLE_ARN`/`AWS_REGION` en `Unal-Team-C/urban-alert-reportes`; con su permiso, publicar la rama y abrir el PR a `main` — archivos: (ninguno) — cubre: RF-019, D-016 — verifica: `gh api repos/Unal-Team-C/urban-alert-reportes/actions/variables` muestra `AWS_REGION`; PR abierto
- [ ] T031 Tras el merge (decisión del usuario): seguir el primer Provision, corregir fallos con commits pequeños y fechar el primer aprovisionamiento en el inventario — archivos: según fallos, infra/INVENTARIO.md — cubre: HU-1, RF-017 — verifica: Provision en verde; humo en verde
- [ ] T032 Segundo Provision sin cambios — archivos: (ninguno) — cubre: RNF-003, MS-004 — verifica: los 3 `apply` informan «No changes» (salvo la task definition si cambia el tag)

## Correcciones de verificación

<!-- /sdd-verify añade aquí tareas si detecta fallos -->

## Cobertura

| Requisito | Tareas |
|---|---|
| RF-001 | T018, T021 |
| RF-002 | T018, T020 |
| RF-003 | T018, T021 |
| RF-004 | T014 |
| RF-005 | T014 |
| RF-006 | T001, T021 |
| RF-007 | T017, T026 |
| RF-008 | T008, T013, T019, T023 |
| RF-009 | T008, T013 |
| RF-010 | T002, T006, T011, T017, T020 |
| RF-011 | T007, T012 |
| RF-012 | T005, T010, T017, T020 |
| RF-013 | T021 |
| RF-014 | T016–T019, T021 |
| RF-015 | T004, T017, T020 |
| RF-016 | T016 |
| RF-017 | T023, T024, T026, T031 |
| RF-018 | T018, T027 |
| RF-019 | T026, T027, T030 |
| RF-020 | T016, T020, T021, T023 |
| RF-021 | T022, T024 |
| RF-022 | T023 |
| RF-023 | T003, T004, T028 |
| RF-024 | T006, T010, T012, T015, T029 |
| RNF-001 | T001, T016, T017, T020, T029 |
| RNF-002 | T021 |
| RNF-003 | T026, T032 |
| RNF-004 | Express rolling + T014; se mide en `/sdd-verify` |
| RNF-005 | T026; se mide en `/sdd-verify` |
| RNF-006 | T018, T028 |
| RNF-007 | T018, T020 |
| RNF-008 | Sin cambios de timeouts; se mide en `/sdd-verify` |
