# Journal SDD

<!-- Bitácora append-only: añade entradas AL FINAL, no reescribas las anteriores.
     Formato:
     ## AAAA-MM-DD · <feature o "general"> · <evento>
     - detalle (qué, por qué, archivos, verificación)
     Las horas exactas de cada edición/checkpoint quedan en .sdd/activity.log. -->

## 2026-10-11 · general · Arnés SDD instalado
- Las carpetas copiadas (`.claude/`, `.sdd/`, `specs/`) llegaron vacías; se copió el arnés desde `urban-alert-usuarios` (hooks, comandos, protocolo, plantillas, config) con STATE/journal nuevos.
- Constitución 1.0.0 derivada de la de Usuarios, adaptada a .NET 10 / EF Core; P2 añade el bus de EventBridge creado a mano como recurso ajeno.

## 2026-10-11 · 001-migracion-aws · Creada spec
- Desde la petición del usuario + README de `UrbanAlert/infrastructure` y README/guía de `urban-alert-plataforma` (copiados a `fuentes/`). Referencias: usuarios 001, geoespacial 002, multimedia 002.
- ECS Express + RDS PostgreSQL propio, solo por el gateway (`/api/v1/reportes/*` → `/api/v1/Reportes/*`); Geoespacial por el gateway con SigV4; Multimedia por el gateway (multipart normal); evento a EventBridge (bus externo) en la nube, RabbitMQ en local.
- 3 `[NEEDS CLARIFICATION]`: bus y formato del evento (RF-012), autorización de escrituras (RF-021), lectura de Auditoria (HU-3.3). Indicaciones técnicas en `notes.md`.

## 2026-10-11 · 001-migracion-aws · Clarificaciones
- Bus: `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos`. Evento: source `urban-alert.reportes`, detail-type `ReporteCreado`, detail = ReporteCreadoEvent JSON (enums como códigos).
- Rutas: todas públicas, como en local. Lectura de Auditoria sobre la base: fuera de alcance. Spec en estado `aclarada`.

## 2026-10-11 · 001-migracion-aws · Spec aprobada
- Aprobada por el usuario. Pregunta sobre IAM: las políticas existentes del rol de workflows no tienen ninguna acción `events:*`; solo hace falta `events:DescribeEventBus` sobre el bus para comprobar que existe (RF-022). El rol de tarea con `events:PutEvents` lo crea Terraform con `iam:PutRolePolicy`, ya permitido.

## 2026-10-11 · 001-migracion-aws · Plan (borrador)
- 3 stacks como Usuarios; migración con la misma imagen (`API.dll --migrar`, D-002/D-003); rol de tarea con `execute-api:Invoke` (POST coordinates) + `events:PutEvents` (bus) (D-004).
- Código por configuración: SigV4 con `AwsSignatureVersion4` 5.1.0 (D-005, riesgo de compatibilidad con AWSSDK.Core 4 → plan B propio), Multimedia `Modo=Gateway` (D-006), `EventBridgeEventPublisher` con AWSSDK.EventBridge 4.0.101.2 (D-007), logs JSON + HttpLogging (D-008).
- Rutas `ANY /api/v1/reportes` y `ANY /api/v1/reportes/{proxy+}` públicas (D-012). Política de arranque nueva `github-actions-urban-alert-reportes` solo con `events:DescribeEventBus` (D-016). IT en CI con service containers (D-017).

## 2026-10-11 · 001-migracion-aws · Plan aprobado; tareas en borrador
- 32 tareas en 5 fases; T002 valida pronto el riesgo de AwsSignatureVersion4 con AWSSDK.Core 4.

## 2026-10-11 · 001-migracion-aws · Tareas aprobadas; inicio de implement

## 2026-10-11 · 001-migracion-aws · T001 hecha
- global.json (SDK 10.0.401, rollForward latestFeature en vez de latestPatch: la imagen sdk:10.0 puede cambiar de banda) y .dockerignore. Verificado: dotnet build en verde (Podman); podman build OK; la imagen corre como uid 1654 (app).

## 2026-10-11 · 001-migracion-aws · T002 hecha
- AWSSDK.EventBridge 4.0.101.2 + AwsSignatureVersion4 5.1.0 (resuelve AWSSDK.Core 4.0.102.9). La firma funciona con Core 4 y credenciales AWSCredentials (sesión). Gotcha: AwsSignatureHandler quita Authorization/X-Amz-* tras enviar; las pruebas deben copiar cabeceras en el momento. UnitTests ahora referencia Infrastructure. 80/80 en verde.

## 2026-10-11 · 001-migracion-aws · T003 hecha
- infra/bootstrap/README.md (recursos reutilizados, política nueva D-016 con comandos, GitHub, requisitos) y github-reportes-policy.json (json válido).

## 2026-10-11 · 001-migracion-aws · T004 hecha
- infra/INVENTARIO.md con los 3 stacks, arranque (incluida la política nueva) y externos (bus, rutas de Geoespacial/Multimedia, roles de Geoespacial).

## 2026-10-11 · 001-migracion-aws · T005 hecha
- EventBridgeEventPublisherTests (3 casos; cliente falso = subclase de AmazonEventBridgeClient). Falla solo por compilación (EventBridgeEventPublisher/EventBridgeOpciones no existen).

## 2026-10-11 · 001-migracion-aws · T006 hecha
- FirmaGeoespacialTests + 2 casos por DI. Esperado: con FirmaIam falla (ruta y firma no configurables aún); sin FirmaIam pasa (comportamiento actual).

## 2026-10-11 · 001-migracion-aws · T007 hecha
- 8 casos del modo Gateway en MultimediaHttpClientTests (multipart a /api/v1/multimedia/images, 201, 400/413/415, 413 del gateway sin JSON, 5xx/201 sin URL, red). Fallan por compilación (MultimediaOpciones/ModoMultimedia no existen).

## 2026-10-11 · 001-migracion-aws · T008 hecha
- MigracionNubeTests (3 casos: idempotente con todas las migraciones, CRUD sin DDL para el rol, contraseña resincronizada); base y rol únicos por prueba. Falla por compilación (MigracionNube no existe).

## 2026-10-11 · 001-migracion-aws · T009 hecha
- ConfiguracionNubeTests (Production + config de la nube, sin RabbitMq:Host; AWSCredentials falsas en DI): EventBridge, Multimedia Gateway, /health. Falla por compilación.

## 2026-10-11 · 001-migracion-aws · T010 hecha
- EventBridgeEventPublisher (FailedEntryCount>0 → InvalidOperationException) + Mensajeria:Transporte (RabbitMq por defecto | EventBridge; otro valor → error). AWSCredentials de DI si existen (pruebas), si no la cadena por defecto. EventBridgeEventPublisherTests 3/3; queda solo el fallo esperado de T011.

## 2026-10-11 · 001-migracion-aws · T011 hecha
- GeoespacialOpciones(RutaCoordenadas) + AwsSignatureHandler si Geoespacial:FirmaIam:Region (credenciales: AWSCredentials de DI o DefaultAWSCredentialsIdentityResolver). UnitTests 85/85.

## 2026-10-11 · 001-migracion-aws · T012 hecha
- MultimediaHttpClient: multipart e interpretación comunes, transportes Emulador (sobre RIE) y Gateway (multipart a api/v1/multimedia/images); Multimedia:Modo en DI (valor inválido → error). Clientes Multimedia+Geoespacial 29/29 en verde (se excluyó MigracionNubeTests, que aún no compila; el fallo de CrearReporte_ConArchivo necesita PostgreSQL).

## 2026-10-11 · 001-migracion-aws · T013 hecha
- MigracionNube (Migrate + rol app idempotente vía set_config/format %L; también secuencias y REVOKE CREATE ON SCHEMA public FROM PUBLIC) y API/Migracion/ModoMigracion (--migrar, Migracion:* desde el entorno, SslMode=Require por defecto, código 0/1). Program devuelve int. MigracionNubeTests 3/3 contra postgres:16 local (contenedores reportes-it-pg/rabbit, scripts it.sh/it.env en el scratchpad).

## 2026-10-11 · 001-migracion-aws · T014 hecha
- HttpLogging (CombineLogs: método, ruta, código, duración) + Apagado:TimeoutSegundos; appsettings con claves nuevas (valores locales). Correcciones halladas al probar la imagen: (1) región de EventBridge desde AWS_REGION de configuración; (2) CredencialesPerezosas: la firma resolvía credenciales al crear el HttpClient y sin ellas TODAS las rutas daban 500 → ahora se resuelven en la primera petición firmada (prueba nueva); (3) GssEncryptionMode=Disable (ruido libgssapi_krb5) → el secreto connection_string debe llevar 'GSS Encryption Mode=Disable' (T018). Verificado: UnitTests 86/86, IT 75/75 (local), imagen Production: /health y lecturas 200 sin credenciales, logs JSON, stop < 1 s; --migrar desde la imagen exit 0 y la app lee como reportes_app. Hallazgo previo: IT en base NUEVA → carrera de migraciones entre fixtures (PK___EFMigrationsHistory); en CI ejecutar --migrar antes de dotnet test (T026).

## 2026-10-11 · 001-migracion-aws · T015 hecha
- Local sin cambios: IT 75/75 en Development con RabbitMQ (la creación publica por MassTransit; transporte por defecto) y UnitTests 86/86.

## 2026-10-11 · 001-migracion-aws · T016–T019 hechas (un commit)
- Stack base copiado de Usuarios y adaptado: db urbanalert_reportes / reportes_app; variable event_bus_arn (validada) y geoespacial_assign_route; rol urban-alert-reportes-ecs-task con execute-api:Invoke (api-execution-arn de SSM + /$default/POST/api/v1/geoespacial/coordinates) y events:PutEvents (bus); secreto con connection_string (SSL Mode=Require, GSS Encryption Mode=Disable, Maximum Pool Size=db_pool_max=8); task definition de migración con command dotnet API.dll --migrar y Migracion__*. validate en verde (Terraform 1.16.5 por Podman, lock copiado de Usuarios). Las cuatro partes se hicieron juntas al copiar el stack: un solo commit.

## 2026-10-11 · 001-migracion-aws · T020 hecha
- base.tftest.hcl adaptado: rol de tarea con exactamente 2 permisos (Invoke sobre la ruta exacta, PutEvents sobre el bus), connection_string (TLS, base/usuario de la app, pool 8), migración --migrar como maestro, validación del ARN del bus. 13/13 en verde; lock de Usuarios válido con -lockfile=readonly.

## 2026-10-11 · 001-migracion-aws · T021 hecha
- Stack service: task_role_arn (existe en el recurso Express, comprobado en el esquema del proveedor), entorno de §7 con dynamic environment (api-endpoint de SSM), secreto ConnectionStrings__Postgres = connection_string, /health, precondición con db_pool_max de base × max_tasks ≤ 50, shutdown_timeout_seconds validado (<30). validate + 7/7 tests en verde.

## 2026-10-11 · 001-migracion-aws · T022 hecha
- Stack routes: ANY /api/v1/reportes → /api/v1/Reportes y ANY /api/v1/reportes/{proxy+} → /api/v1/Reportes/$request.path.proxy, NONE, integration_method ANY; sin overwrite:querystring. 7/7 tests; fmt -check limpio en infra/.

## 2026-10-11 · 001-migracion-aws · T023 hecha
- Scripts copiados de Usuarios (buscar-listener-express, run-schema-task, diagnose-service + test-buscar-listener); check-platform.sh añade la comprobación del bus (ARN de infra/base/terraform.tfvars o argumento) con mensajes para inexistente y AccessDenied (política D-016). test-check-platform 6/6, test-buscar-listener 3/3, shellcheck limpio.

## 2026-10-11 · 001-migracion-aws · T024 hecha
- smoke-gateway-reportes.sh (lista con página, query string reenviada, catálogo por {proxy+}, 404 del servicio con problem details vs 404 del gateway para /health, /scalar/v1, /openapi/v1.json, /api/v1/reportes-x, /api/v1/Reportes) + test-smoke 4/4; formas de respuesta comprobadas contra el servicio real. shellcheck limpio.

## 2026-10-11 · 001-migracion-aws · T025 hecha
- crear-reporte-prueba.sh: JSON + multipart (PNG 1x1), idCoordenada, urlImagen 200, DELETE 204 de ambos. shellcheck limpio; sin argumentos → uso y exit 2. Se ejecuta en /sdd-verify contra AWS.

## 2026-10-11 · 001-migracion-aws · T026 hecha
- provision.yml: job test con services postgres:16-alpine y rabbitmq:4-alpine (credenciales desechables ci-only), setup-dotnet desde global.json, build Release, --migrar antes de dotnet test (evita la carrera de migraciones de los fixtures en base nueva), scripts, Terraform x3, actionlint, shellcheck; job provision como Usuarios (check-platform con bus, sin --target en docker build: el Dockerfile no tiene etapa runtime). actionlint limpio.

## 2026-10-11 · 001-migracion-aws · T027 hecha
- deprovision.yml derivado de Usuarios (confirmación urban-alert-reportes, solo main, misma concurrency; routes → service → base allow_destroy → destroy; tolera estados vacíos). actionlint limpio.

## 2026-10-11 · 001-migracion-aws · T028 hecha
- README (configuración local/AWS, evento, migración, despliegue, rutas, arranque D-016, workflows, costes ~33 USD, validación local; corrige Testcontainers), docs/funcionamiento.md (dependencias por entorno, esquema en AWS) y CLAUDE.md (AWS, validación local, gotchas).

## 2026-10-11 · 001-migracion-aws · T029 hecha
- Comprobación completa: base nueva + --migrar + IT 75/75 (orden de CI), UnitTests 86/86, Terraform base 13/13, service 7/7, routes 7/7, fmt limpio, pruebas de scripts 3/3, actionlint y shellcheck limpios; búsqueda de secretos: solo nombres, placeholders de .env.example y credenciales desechables de CI.

## 2026-10-11 · 001-migracion-aws · T030 hecha
- Política D-016 creada por el usuario; AWS_ROLE_ARN y AWS_REGION existen a nivel de organización (no de repo). Rama aws-provisioning publicada y PR #1 abierto a main. T031 espera al merge.
