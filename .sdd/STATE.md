# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: - -->

feature_activa: 002-resiliencia-eventos
fase: implement
tarea_actual: T004
estado_tarea: -

## Siguiente paso

T004: generar migración AgregarOutbox con dotnet-ef 10.0.4 en el contenedor SDK; verificar con --migrar sobre base vacía (tabla + índice) y 0 pendientes al repetir.

## Contexto de trabajo

- 001-migracion-aws aparcada (T032 bloqueada): contexto en specs/001-migracion-aws/notes.md §Aparcado 2026-10-19.
- Geoespacial es idempotente por reportId (201 nueva, 200 repetición idéntica, 409 otra coordenada): reintentar es seguro.
- Multimedia POST images no es idempotente: un reintento tras timeout puede duplicar la imagen.
- Auditoría deduplica por event_id (AuditStore busca event_id antes de insertar): soporta entrega al menos una vez.
- Hoy CrearReporteHandler: guarda → PutEvents; si PutEvents falla, el cliente recibe error aunque el reporte quedó creado.
- `--migrar` no construye el host web: el despachador no corre ahí.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- (ninguno)
