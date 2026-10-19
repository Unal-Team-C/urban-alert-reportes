# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: - -->

feature_activa: 002-resiliencia-eventos
fase: implement
tarea_actual: T007
estado_tarea: -

## Siguiente paso

T007: tests/IntegrationTests/Mensajeria/DespachadorOutboxTests.cs en una colección sin paralelismo: entrega < 5 s con el host real; con publicador controlable (Outbox:Habilitado=false y ProcesarLoteAsync a mano): 201 con bus caído, reintento y entrega, FALLIDO tras MaxIntentos sin bloquear, dos despachadores concurrentes sin duplicar.

## Contexto de trabajo

- 001-migracion-aws aparcada (T032 bloqueada): contexto en specs/001-migracion-aws/notes.md §Aparcado 2026-10-19.
- Geoespacial es idempotente por reportId (201 nueva, 200 repetición idéntica, 409 otra coordenada): reintentar es seguro.
- Multimedia POST images no es idempotente: un reintento tras timeout puede duplicar la imagen.
- Auditoría deduplica por event_id (AuditStore busca event_id antes de insertar): soporta entrega al menos una vez.
- Hoy CrearReporteHandler: guarda → PutEvents; si PutEvents falla, el cliente recibe error aunque el reporte quedó creado.
- Migración local: red Podman `sdd002` + contenedor `sdd002-pg` (postgres:16, admin/admin-local-only, base reportes); `--migrar` con `-e Migracion__*` en array zsh (`"${M[@]}"`) y `Migracion__SslMode=Disable`.
- dotnet-ef 10.0.4 en `/tmp/tools` del contenedor; necesita `ConnectionStrings__Postgres`, `Geoespacial__BaseUrl`, `Multimedia__BaseUrl`, `RabbitMq__Host` de relleno.
- IT local: `scratchpad/it.sh [filtro]` (contenedores `sdd002-pg` y `sdd002-rmq` ci/ci-only en la red `sdd002`); base ya migrada con --migrar.
- `--migrar` no construye el host web: el despachador no corre ahí.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- (ninguno)
