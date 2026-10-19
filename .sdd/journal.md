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

## 2026-10-11 · 001-migracion-aws · Primer Provision falló (run 38160461687)
- La tarea de migración falló: 42501 permission denied to alter role (ALTER ROLE ... NOSUPERUSER: el maestro de RDS no es superusuario). Base e imagen aplicadas; servicio y rutas no.
- Fuga: el campo Where de PostgresException incluye el ALTER ROLE con la contraseña de reportes_app → log público de Actions y CloudWatch. La transacción se revirtió (contraseña nunca aplicada). Corrección y rotación en T033 (rama fix/migracion-rds).

## 2026-10-11 · 001-migracion-aws · T033 hecha
- ALTER ROLE sin NOSUPERUSER/NOCREATEDB/NOCREATEROLE; PostgresException del SQL del rol → InvalidOperationException con SqlState y mensaje, sin la excepción original (el Where llevaba la contraseña). Rotación: random_password con keepers db_app_password_version = "2".
- Pruebas nuevas: admin CREATEROLE NOSUPERUSER dueño de la base (como RDS) — falla con el SQL antiguo, pasa con el nuevo — y error sin contraseña. IT 77/77, unitarias 86/86, base 13/13.
- Copias de iCloud "* 2.*" (no versionadas) rompían el build local: movidas al scratchpad e ignoradas.

## 2026-10-11 · 001-migracion-aws · Logs del run 38160461687 borrados
- El usuario borró los logs del run (API: 404 en runs/.../logs y jobs/.../logs). gh conserva una copia en su caché local (~/.cache/gh). La contraseña filtrada se rota con PR #2.

## 2026-10-11 · 001-migracion-aws · T031 hecha: primer Provision en verde (run 38162982758)
- base: 3 reemplazados (random_password, versión del secreto, task definition de migración: rotación); imagen reutilizada (mismo SHA? no: tag del merge #2 ya existía por el run fallido? → "already exists"); migración: 0 pendientes (el run fallido ya había migrado antes del error del rol) y rol sincronizado; service +1; routes +4; humo 9/9 (primer intento 404 mientras se propagaban las rutas).
- crear-reporte-prueba.sh: JSON 201 con idCoordenada de Geoespacial (firma SigV4 OK) y PutEvents sin error; DELETE 204. Multipart 503: Multimedia devuelve 503 también llamado directamente (POST images y GET metadata) → problema de Multimedia, no de Reportes; Reportes lo traduce bien (503 MultimediaNoDisponible).
- Corrección de la entrada anterior: la imagen sí se construyó y publicó con el SHA d75ef71 (solo se reutilizaron capas); la línea "already exists" era el texto del script, no su ejecución.

## 2026-10-19 · general · Cambio de feature 001-migracion-aws → 002-resiliencia-eventos
- 001 aparcada en implement (T032 bloqueada); contexto en specs/001-migracion-aws/notes.md §Aparcado 2026-10-19. Rama 002-resiliencia-eventos desde origin/main c5f09ab.

## 2026-10-19 · 002-resiliencia-eventos · Creada spec desde descripción
- Debilidades 3 (sin outbox: reporte guardado sin evento) y 4 (Geoespacial/Multimedia solo con timeout) del análisis de QAS-02 y QAS-04.1. HU-1 entrega garantizada del evento (P1), HU-2 reintentos ante fallos transitorios (P1), HU-3 circuito con fallo rápido (P2). 13 RF, 6 RNF.
- 1 [NEEDS CLARIFICATION]: política de reintento de Multimedia (su subida no es idempotente).

## 2026-10-19 · 002-resiliencia-eventos · Spec aclarada y aprobada
- RF-007 Multimedia: reintento solo si la conexión no se estableció; timeout/5xx sin reintento; circuito sí. Usuario aprueba la spec.

## 2026-10-19 · 002-resiliencia-eventos · plan.md y tasks.md generados
- D-001 outbox propio (no MassTransit: en AWS no hay MassTransit); D-002 atomicidad en IReporteRepository.AgregarAsync(reporte, evento); D-003 IEventPublisher no genérico; D-004 Microsoft.Extensions.Http.Resilience 10.10.0 (10.11.0 tiene 5 días); D-005 Multimedia reintenta solo ConnectionError/NameResolutionError; D-006 presupuesto ≈ 22 s < 25 s; D-007 transacción por lote con SKIP LOCKED.
- 12 tareas: preparación 1, outbox 6, resiliencia 3, pulido 2. Sin cambios de Terraform.

## 2026-10-19 · 002-resiliencia-eventos · Plan y tareas aprobados
- El usuario aprueba plan.md y tasks.md; empieza la implementación por T001.

## 2026-10-19 · 002-resiliencia-eventos · T001 hecha
- Microsoft.Extensions.Http.Resilience 10.10.0 sin conflicto con Microsoft.Extensions.Http 10.0.12 (no hizo falta subirlo). Build 0 errores/0 advertencias; UnitTests 86/86.

## 2026-10-19 · 002-resiliencia-eventos · T002 hecha
- IEventPublisher.PublicarAsync(object, ct). EventBridge: DetailType y Serialize con evento.GetType(); cliente con Timeout 5 s y MaxErrorRetry 2 (D-007). MassTransit: Publish(evento, evento.GetType()).
- Prueba nueva de no regresión (object vs tipado: mismo DetailType/Source/Detail); la de formato del Detail sigue pasando. UnitTests 87/87; solución compila.

## 2026-10-19 · 002-resiliencia-eventos · T003 hecha
- MensajeOutbox (Crear/MarcarEntregado/RegistrarFallo con backoff min(2^n s, 5 min) y FALLIDO en maxIntentos; error recortado a 2000), EstadoMensajeOutbox con CodigoEnumConverter, configuración (jsonb, índice parcial IX_MensajesOutbox_Pendientes), DbSet.
- SerializacionEventos (opciones Web + códigos, tipos conocidos sin reflexión); EventBridgeEventPublisher reutiliza sus opciones.
- Desviación registrada en plan D-002: IEventoDominio { IdEvento } en lugar de object.
- UnitTests 95/95.

## 2026-10-19 · 002-resiliencia-eventos · T004 hecha
- Migración 20261019150110_AgregarOutbox (dotnet-ef 10.0.4): tabla MensajesOutbox + IX_MensajesOutbox_Pendientes (parcial PENDIENTE). Down: DropTable.
- Verificado: --migrar sobre base vacía aplicó 7 migraciones y sincronizó reportes_app (INSERT/UPDATE/SELECT sobre MensajesOutbox = t); segunda ejecución: 0 pendientes.

## 2026-10-19 · 002-resiliencia-eventos · T005 hecha
- IReporteRepository.AgregarAsync(reporte, IEventoDominio, ct): Add reporte + MensajeOutbox.Crear(evento, TimeProvider) y un SaveChanges. TimeProvider.System registrado con TryAdd. CrearReporteHandler ya no depende de IEventPublisher.
- Unitarias: RepositorioFalso guarda eventos; prueba nueva de fallo del guardado sin reporte ni evento. 96/96.
- IT nuevas (OutboxGuardadoTests): POST deja la fila ReporteCreadoEvent del reporte; Id duplicado → DbUpdateException y ninguna fila del segundo evento. IT 79/79 en local (Podman).

## 2026-10-19 · 002-resiliencia-eventos · T006 hecha
- DespachadorOutbox (BackgroundService): vuelta cada IntervaloMs con Task.Delay(TimeProvider); lote FromSql parametrizado ... FOR UPDATE SKIP LOCKED en transacción; publica por IEventPublisher del scope con tope TimeoutPublicacionSegundos; MarcarEntregado / RegistrarFallo (Warning; Error al quedar FALLIDO, RF-013); sigue sin esperar si el lote viene lleno.
- OutboxOpciones.Desde(config) con validación; DespachadorOutbox singleton + AddHostedService si Habilitado; appsettings Outbox:*.
- Build 0/0; IT 79/79 con el despachador activo: 25 eventos ENTREGADO contra RabbitMQ real en la corrida.

## 2026-10-19 · 002-resiliencia-eventos · T007 hecha
- tests/IntegrationTests/Mensajeria: OutboxApiFactory (Outbox:Habilitado=false, MaxIntentos=3, PublicadorControlable, RelojControlable, LogsCapturados) y colección Outbox sin paralelismo.
- DespachadorOutboxTests: bus caído → 201, PENDIENTE con Intentos=1 y UltimoError, sin reintento antes del backoff, ENTREGADO al recuperarse (publicado 1 vez); FALLIDO tras 3 intentos sin bloquear al sano y log Error (RF-013); 2 despachadores concurrentes con 30 eventos → cada uno publicado exactamente una vez (SKIP LOCKED).
- DespachadorEnSegundoPlanoTests: host real + RabbitMQ, entregado en < 5 s (RNF-001).
- IT 83/83 tres corridas seguidas.
