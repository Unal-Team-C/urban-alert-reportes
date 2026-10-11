# Funcionamiento del servicio de Reportes

## Alcance actual

CRUD de reportes de daño urbano bajo `/api/v1/Reportes`, más el catálogo de categorías/tipos (`GET /api/v1/Reportes/catalogo`). Persiste en PostgreSQL vía EF Core (migraciones en `src/Infrastructure/Persistencia/Migraciones`) y publica un evento de dominio al crear un reporte: por MassTransit/RabbitMQ en local y por Amazon EventBridge en AWS (`Mensajeria:Transporte`, ver README «Configuración: local y AWS»).

Endpoints expuestos por `ReportesController` (ver también `/scalar/v1` en Development para el contrato generado por OpenAPI):

- `POST /api/v1/Reportes` — crear reporte con `urlImagen` ya alojada (JSON).
- `POST /api/v1/Reportes` (`multipart/form-data`) — crear reporte subiendo el archivo de imagen directamente; Reportes lo reenvía al servicio Multimedia.
- `GET /api/v1/Reportes/catalogo` — categorías y tipos válidos.
- `GET /api/v1/Reportes` — lista paginada (máx. 100 por página), filtrable por estado/nivel de emergencia/tipo.
- `GET /api/v1/Reportes/{id}` — detalle.
- `PATCH /api/v1/Reportes/{id}/estado` — transición de estado (reglas en `Domain.Reportes.Reporte`).
- `PATCH /api/v1/Reportes/{id}/nivel-emergencia` — cambio de nivel de emergencia.
- `PATCH /api/v1/Reportes/{id}/asignacion` — asignar responsable.
- `PUT /api/v1/Reportes/{id}/rechazo` — rechazar con motivo obligatorio.
- `DELETE /api/v1/Reportes/{id}` — eliminar.

## Flujo de creación

`CrearReporteHandler` (`src/Application/Reportes/CrearReporte`) hace, en este orden: valida el reporte en memoria (dominio) antes de tocar red, sube la imagen a Multimedia si llegó como archivo, pide a Geoespacial que registre la coordenada y devuelva su id, guarda el reporte en PostgreSQL y por último publica `ReporteCreadoEvent` (MassTransit en local; `PutEvents` al bus `urban-alert-bus-eventos` en AWS, con `source = urban-alert.reportes` y `detail-type = ReporteCreado`). El orden importa: si Geoespacial o Multimedia no responden (503), el reporte no se crea; si el fallo ocurre después de guardar en PostgreSQL pero antes de publicar el evento, el reporte queda creado sin evento (no hay outbox transaccional todavía — ver brechas).

`ReporteCreadoEvent` (`src/Application/Reportes/Eventos`) es el contrato que consume `urban-alert-auditoria` (`Infrastructure.Mensajeria.ReporteCreadoConsumer`) para construir su cadena de auditoría. Es el único evento que este servicio publica hoy: no hay eventos para cambio de estado, asignación, nivel de emergencia, rechazo ni eliminación.

## Identidad y autorización

El servicio no implementa autenticación. `idUsuario` lo envía el cliente en el `POST`; si no llega, `CrearReporteHandler` genera uno aleatorio (ver comentario en el handler). Esto significa que, hasta que exista un proveedor de identidad real, el campo no corresponde necesariamente a un usuario autenticado — el servicio de Auditoría ya documenta esta misma limitación del otro lado.

## Concurrencia e integridad

La entidad usa una columna sombra `xmin` (migración `AgregarTokenConcurrenciaXmin`) como token de concurrencia optimista: dos actualizaciones simultáneas sobre el mismo reporte producen `DbUpdateConcurrencyException` en la segunda, en vez de pisarse silenciosamente (cubierto por `tests/IntegrationTests/Reportes/ReporteConcurrenciaTests.cs`).

## Dependencias externas

- **Geoespacial** (`Geoespacial:BaseUrl`) — síncrono, timeout corto (3s por defecto): la creación del reporte depende de su respuesta. En AWS se llama por el API Gateway compartido (`POST /api/v1/geoespacial/coordinates`, ruta interna con `AWS_IAM`) y cada petición se firma con SigV4 usando el rol de tarea (`Geoespacial:FirmaIam:Region`); las credenciales se resuelven en la primera petición firmada, así que su falta solo afecta a la creación.
- **Multimedia** (`Multimedia:BaseUrl`) — síncrono, timeout más alto (10s por defecto): solo se llama si el reporte trae imagen como archivo. En local se habla con el emulador de Lambda (`Multimedia:Modo=Emulador`); en AWS se envía un multipart normal a `POST /api/v1/multimedia/images` del gateway (`Gateway`). Las respuestas se interpretan igual; el 413 del propio gateway (más de 10 MB) se trata como imagen inválida.
- **Mensajería** — publica `ReporteCreadoEvent`; lo consume Auditoría. En local por RabbitMQ; en AWS por EventBridge (una entrada rechazada cuenta como fallo). No hay cola propia de consumo en este servicio (`Consumers/` existe en `src/API` pero está vacía).

## Esquema en AWS

La API no migra fuera de Development. En AWS, antes de cada despliegue, una tarea ECS puntual ejecuta la misma imagen con `--migrar`: aplica las migraciones como administrador y crea o sincroniza el rol `reportes_app` (permisos de datos, sin DDL), con el que se conecta el servicio por TLS.

## Brechas conocidas

- Sin outbox transaccional: un reporte puede quedar creado en PostgreSQL sin que su evento llegue a publicarse si el proceso falla entre el guardado y el publish.
- Sin autenticación/autorización real — `idUsuario` no es de confianza.
- Solo se audita la creación de reportes (ver arriba); las demás operaciones no tienen contrato de evento.
- `deploy/docker/docker-compose.yml` trae credenciales de PostgreSQL/RabbitMQ en texto plano, pensadas solo para desarrollo local.
