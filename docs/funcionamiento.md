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

`CrearReporteHandler` (`src/Application/Reportes/CrearReporte`) hace, en este orden: verifica la prueba humana (spec 003, solo si `Captcha:Habilitado`), valida el reporte en memoria (dominio) antes de tocar red, sube la imagen a Multimedia si llegó como archivo, pide a Geoespacial que registre la coordenada y devuelva su id, y guarda en PostgreSQL, **en una sola transacción**, el reporte y su `ReporteCreadoEvent` en la tabla `MensajesOutbox` (outbox transaccional, spec 002). La respuesta 201 depende solo de ese guardado. El orden importa: si Geoespacial o Multimedia no responden (503), el reporte no se crea; si el guardado falla, no queda ni reporte ni evento.

## Verificación humana (captcha)

Con `Captcha:Habilitado=true`, `VerificadorTurnstile` (`src/Infrastructure/Captcha`) envía `captchaToken` y la clave secreta a `siteverify` de Cloudflare Turnstile antes de hacer cualquier otro trabajo. Responde **400** si el token falta, está vacío, tiene más de 2048 caracteres, es inválido, venció o ya se usó (`timeout-or-duplicate`), o si el `hostname` o la `action` no coinciden con los configurados. Responde **503** si Turnstile no responde, responde con error, devuelve algo ilegible o informa un error de configuración (`invalid-input-secret`). Es falla cerrado: nunca deja pasar una creación sin comprobar.

El token es de un solo uso, así que el pipeline (`Resiliencia:Captcha`) solo reintenta si la conexión no se estableció. Sí tiene circuito con fallo rápido. Los rechazos quedan en el log con el código de Turnstile; nunca se registran el token ni la clave. Si la verificación está activa sin clave secreta, la API no arranca. Otras claves: `Captcha:TimeoutSegundos` (5, timeout por intento) y `Captcha:UrlVerificacion` (por defecto, la de Turnstile; solo se cambia en pruebas).

Es un control antiabuso para la creación anónima. No autentica a nadie ni protege `PATCH`, `PUT` ni `DELETE` (ver brechas).

## Entrega de eventos (outbox)

`DespachadorOutbox` (`src/Infrastructure/Mensajeria/Outbox`) corre en segundo plano en cada tarea. Cada segundo (`Outbox:IntervaloMs`) toma hasta 20 pendientes (`Outbox:Lote`) con `FOR UPDATE SKIP LOCKED`, dentro de una transacción, y los publica por el transporte configurado: MassTransit en local; en AWS, `PutEvents` al bus `urban-alert-bus-eventos` con `source = urban-alert.reportes` y `detail-type = ReporteCreado`. El evento publicado es idéntico al de antes del outbox.

- Si la publicación sale bien, el mensaje pasa a `ENTREGADO`.
- Si falla, se reintenta con espera `min(2^n s, 5 min)`, y cada publicación tiene un tope de 10 s (`Outbox:TimeoutPublicacionSegundos`).
- Tras 10 intentos (`Outbox:MaxIntentos`) el mensaje queda `FALLIDO`, con `UltimoError` y un log de nivel Error, sin bloquear a los demás. Los mensajes fallidos no se reprocesan solos.
- Con varias tareas activas, `SKIP LOCKED` evita que dos tomen la misma fila.
- Si una tarea muere después de publicar y antes del COMMIT, el evento se vuelve a entregar: la entrega es "al menos una vez", y Auditoría descarta la repetición por `idEvento`.
- `Outbox:Habilitado=false` desactiva el despachador (lo usan las pruebas). `--migrar` no lo arranca.
- Las filas no se purgan.

Para consultar los pendientes y fallidos:

```sql
SELECT "Estado", count(*) FROM "MensajesOutbox" GROUP BY 1;
SELECT "Id", "Intentos", "UltimoError" FROM "MensajesOutbox" WHERE "Estado" = 'FALLIDO';
```

`ReporteCreadoEvent` (`src/Application/Reportes/Eventos`) es el contrato que consume `urban-alert-auditoria` (`Infrastructure.Mensajeria.ReporteCreadoConsumer`) para construir su cadena de auditoría. Es el único evento que este servicio publica hoy: no hay eventos para cambio de estado, asignación, nivel de emergencia, rechazo ni eliminación.

## Identidad y autorización

El servicio no implementa autenticación. `idUsuario` lo envía el cliente en el `POST`; si no llega, `CrearReporteHandler` genera uno aleatorio (ver comentario en el handler). Esto significa que, hasta que exista un proveedor de identidad real, el campo no corresponde necesariamente a un usuario autenticado — el servicio de Auditoría ya documenta esta misma limitación del otro lado.

## Concurrencia e integridad

La entidad usa una columna sombra `xmin` (migración `AgregarTokenConcurrenciaXmin`) como token de concurrencia optimista: dos actualizaciones simultáneas sobre el mismo reporte producen `DbUpdateConcurrencyException` en la segunda, en vez de pisarse silenciosamente (cubierto por `tests/IntegrationTests/Reportes/ReporteConcurrenciaTests.cs`).

## Dependencias externas

Las dos llamadas síncronas tienen un pipeline de resiliencia (`src/Infrastructure/Resiliencia/PoliticasHttp.cs`, spec 002): reintento con espera exponencial y jitter (2 reintentos, 200 ms de base, sin respetar `Retry-After`) → circuit breaker (se abre si falla el 50 % en 30 s con al menos 5 llamadas; prueba de nuevo a los 15 s) → timeout por intento. Con el circuito abierto, la creación responde 503 de inmediato sin llamar a la dependencia. Cada dependencia tiene su propio circuito. Todo se configura en `Resiliencia:<Geoespacial|Multimedia>:*`; `Reintentos=0` desactiva el reintento. `HttpClient.Timeout` (`TimeoutTotalSegundos`, 12 s) es el tope total, por debajo de los 30 s del API Gateway. Los reintentos y los cambios del circuito quedan en el log por la telemetría de Microsoft.Extensions.Resilience.

- **Geoespacial** (`Geoespacial:BaseUrl`) — síncrono, timeout corto por intento (3 s por defecto): la creación del reporte depende de su respuesta. Es idempotente por reporte (200 ante una repetición idéntica), así que se reintentan todos los fallos transitorios (sin conexión, timeout, 408, 429, 5xx); 400, 404 y 409 no se reintentan. El reintento va por fuera de la firma SigV4, así que cada intento se firma de nuevo. En AWS se llama por el API Gateway compartido (`POST /api/v1/geoespacial/coordinates`, ruta interna con `AWS_IAM`) y cada petición se firma con SigV4 usando el rol de tarea (`Geoespacial:FirmaIam:Region`); las credenciales se resuelven en la primera petición firmada, así que su falta solo afecta a la creación.
- **Multimedia** (`Multimedia:BaseUrl`) — síncrono, timeout por intento más alto (10 s por defecto): solo se llama si el reporte trae imagen como archivo. Su subida no es idempotente, así que solo se reintenta si la conexión ni siquiera se estableció (`HttpRequestError.ConnectionError` o `NameResolutionError`). Un timeout o un 5xx no se reintentan, para no dejar copias huérfanas de la imagen, pero sí cuentan para el circuito. En local se habla con el emulador de Lambda (`Multimedia:Modo=Emulador`); en AWS se envía un multipart normal a `POST /api/v1/multimedia/images` del gateway (`Gateway`). Las respuestas se interpretan igual; el 413 del propio gateway (más de 10 MB) se trata como imagen inválida.
- **Mensajería** — publica `ReporteCreadoEvent` desde el outbox; lo consume Auditoría. En local por RabbitMQ; en AWS por EventBridge (una entrada rechazada cuenta como fallo y se reintenta; el cliente tiene 5 s de timeout y 2 reintentos del SDK). No hay cola propia de consumo en este servicio (`Consumers/` existe en `src/API` pero está vacía).

## Esquema en AWS

La API no migra fuera de Development. En AWS, antes de cada despliegue, una tarea ECS puntual ejecuta la misma imagen con `--migrar`: aplica las migraciones como administrador y crea o sincroniza el rol `reportes_app` (permisos de datos, sin DDL), con el que se conecta el servicio por TLS.

## Brechas conocidas

- Sin compensación (saga) cuando la creación falla a medias: si la imagen ya se subió a Multimedia o la coordenada ya se registró en Geoespacial y después falla el guardado, esos recursos quedan huérfanos.
- Los eventos `FALLIDO` del outbox no se reprocesan solos (solo quedan registrados) y no hay alarmas sobre ellos.
- Sin autenticación/autorización real — `idUsuario` no es de confianza, y las rutas de modificación (`PATCH`, `PUT`, `DELETE`) son públicas. El captcha (spec 003) solo cubre la creación.
- Solo se audita la creación de reportes (ver arriba); las demás operaciones no tienen contrato de evento.
- `deploy/docker/docker-compose.yml` trae credenciales de PostgreSQL/RabbitMQ en texto plano, pensadas solo para desarrollo local.
