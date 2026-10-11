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
