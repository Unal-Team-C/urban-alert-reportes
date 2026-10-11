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
