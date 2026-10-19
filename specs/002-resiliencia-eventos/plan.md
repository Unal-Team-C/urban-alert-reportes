# Plan técnico: Entrega garantizada del evento de creación y resiliencia ante dependencias

- **Spec**: ./spec.md
- **Estado**: aprobado <!-- borrador | aprobado -->
- **Fecha**: 2026-10-19

## 1. Resumen técnico

**Outbox transaccional propio** en la base de Reportes: el repositorio guarda el reporte y su `ReporteCreadoEvent` serializado en la tabla `MensajesOutbox` con un solo `SaveChanges` (una transacción). Un `BackgroundService` (`DespachadorOutbox`) toma lotes de pendientes con `FOR UPDATE SKIP LOCKED` cada segundo y los entrega por el `IEventPublisher` del transporte configurado (RabbitMQ en local, EventBridge en AWS), sin depender de MassTransit. Los clientes HTTP de Geoespacial y Multimedia reciben un pipeline de **Microsoft.Extensions.Http.Resilience**: reintento (backoff exponencial con jitter) → circuit breaker → timeout por intento. En Multimedia, el reintento solo se dispara ante errores de conexión.

## 2. Contexto técnico

| Aspecto | Valor |
|---|---|
| Lenguaje / versión | C# / .NET 10 (`global.json`), ASP.NET Core |
| Dependencias principales | EF Core 10 + Npgsql 10.0.3, AWSSDK.EventBridge 4.0.101.2, MassTransit 8.5.11 (solo local), **nuevo**: `Microsoft.Extensions.Http.Resilience` **10.10.0** (Polly v8). Se descarta la 10.11.0 por tener 5 días. |
| Almacenamiento | PostgreSQL 16 propio (RDS en AWS); nueva tabla `MensajesOutbox` por migración EF Core |
| Pruebas | xUnit: `tests/UnitTests` y `tests/IntegrationTests` (PostgreSQL y RabbitMQ reales; CI migra antes con `--migrar`) |
| Plataforma objetivo | ECS Express (Fargate), de 1 a 5 tareas, `db_pool_max × max_tasks ≤ 50` |
| Restricciones | API Gateway HTTP: 30 s máximo de integración (RNF-003). Sin recursos de AWS nuevos (RNF-006). Sin SDK .NET local: Podman con `mcr.microsoft.com/dotnet/sdk:10.0` |

## 3. Verificación contra la constitución

| Principio | ¿Cumple? | Nota |
|---|---|---|
| P1 IaC solo desde Actions | Sí | Sin cambios de Terraform. La tabla nueva la crea la migración (`--migrar`) que ya ejecuta Provision. |
| P2 Recursos ajenos | Sí | Se usa el bus existente; no se gestiona. |
| P3 Sin secretos | Sí | No hay credenciales nuevas. `UltimoError` guarda mensajes del SDK, no la cadena de conexión. |
| P4 Destructivos explícitos | Sí | La migración solo añade una tabla; su `Down` la elimina. |
| P5 Coste mínimo | Sí | 0 USD: usa la RDS y el bus existentes. Ver la nota de coste de PutEvents en §10. |
| P6 Validación antes de commit | Sí | `dotnet test` (unitarias + integración) con Podman antes de cada commit de tarea. |
| P7 Pruebas de Terraform | N/A | Sin cambios de infraestructura. |
| P8 Versiones fijadas | Sí | Paquete nuevo con versión exacta (10.10.0). |
| P9 Contrato y nombres | Sí | Eventos con el mismo `Source`/`DetailType`/`Detail` (RF-006). |
| P10 Idempotencia | Sí | La migración es idempotente por el historial de EF. El despachador usa `SKIP LOCKED` entre tareas. |

## 4. Arquitectura y componentes

```text
POST /api/v1/Reportes
  CrearReporteHandler
    ├─ Multimedia (si hay imagen) ── [Retry(solo conexión) → CircuitBreaker → Timeout 10 s] ── HTTP
    ├─ Geoespacial ───────────────── [Retry ×2 → CircuitBreaker → Timeout 3 s] → SigV4 ── HTTP
    └─ IReporteRepository.AgregarAsync(reporte, evento)
         └─ SaveChanges: INSERT "Reportes" + INSERT "MensajesOutbox"  (1 transacción)  → 201

DespachadorOutbox (BackgroundService, 1 por tarea, cada 1 s)
  BEGIN
    SELECT … FROM "MensajesOutbox" WHERE "Estado"='PENDIENTE' AND "ProximoIntento"<=now()
      ORDER BY "RegistradoEn" LIMIT 20 FOR UPDATE SKIP LOCKED
    por cada uno: IEventPublisher.PublicarAsync(evento)
      ok    → Estado=ENTREGADO, EntregadoEn=now()
      error → Intentos+1, ProximoIntento=now()+min(2^Intentos s, 5 min), UltimoError
              si Intentos ≥ MaxIntentos → Estado=FALLIDO (log Error)
  COMMIT
```

- **Atomicidad (RF-001)**: el handler construye el evento antes de guardar y se lo pasa al repositorio, que hace `Add(reporte)` + `Add(mensaje)` + un solo `SaveChangesAsync`. Si falla el guardado, no queda ninguno de los dos.
- **Entrega (RF-002, RF-003, RF-005)**: el handler ya no publica, y el 201 depende solo del guardado. El despachador reintenta con backoff hasta `MaxIntentos`.
- **Aislamiento entre eventos (RF-004)**: cada mensaje lleva su propio contador; un fallido sale de la cola sin bloquear el lote.
- **Concurrencia entre tareas**: `FOR UPDATE SKIP LOCKED` evita que dos tareas tomen la misma fila. Si una tarea muere después de publicar y antes del COMMIT, la fila vuelve a pendiente y se reentrega: es la semántica "al menos una vez", y Auditoría deduplica por `idEvento`.
- **Resiliencia HTTP (RF-007 a RF-012)**: `AddResilienceHandler` en cada `HttpClient`, **antes** del `AwsSignatureHandler`, para que cada intento se firme de nuevo (la fecha SigV4 cambia). `HttpClient.Timeout` pasa a ser el tope total (Geoespacial 12 s, Multimedia 12 s); el timeout por intento lo pone el pipeline. Un `BrokenCircuitException` o un `TimeoutRejectedException` se traducen en los clientes a las excepciones actuales de "no disponible" (503).

## 5. Estructura de archivos

```text
src/Application/
  Interfaces/Eventos/IEventPublisher.cs            (mod) PublicarAsync(object evento, CancellationToken): no genérico
  Interfaces/Reportes/IReporteRepository.cs        (mod) AgregarAsync(Reporte, object eventoCreacion, CancellationToken)
  Reportes/CrearReporte/CrearReporteHandler.cs     (mod) sin IEventPublisher; pasa el evento al repositorio
src/Infrastructure/
  Mensajeria/EventBridgeEventPublisher.cs          (mod) tipo en tiempo de ejecución; timeout del cliente 5 s
  Mensajeria/MassTransitEventPublisher.cs          (mod) Publish(evento, evento.GetType())
  Mensajeria/Outbox/MensajeOutbox.cs               (nuevo) entidad + EstadoMensajeOutbox
  Mensajeria/Outbox/SerializacionEventos.cs        (nuevo) JSON (Web, códigos) y registro de tipos conocidos
  Mensajeria/Outbox/DespachadorOutbox.cs           (nuevo) BackgroundService
  Mensajeria/Outbox/OutboxOpciones.cs              (nuevo) Habilitado, IntervaloMs, Lote, MaxIntentos
  Persistencia/Configuraciones/MensajeOutboxConfiguration.cs   (nuevo)
  Persistencia/ReportesDbContext.cs                (mod) DbSet<MensajeOutbox>
  Persistencia/Repositorios/ReporteRepository.cs   (mod) AgregarAsync con evento
  Persistencia/Migraciones/2026101900xxxx_AgregarOutbox.cs (+Designer, snapshot)  (nuevo)
  Resiliencia/PoliticasHttp.cs                     (nuevo) pipelines de Geoespacial y Multimedia
  Geoespacial/GeoespacialHttpClient.cs             (mod) traduce BrokenCircuit y TimeoutRejected
  Multimedia/MultimediaHttpClient.cs               (mod) ídem
  DependencyInjection.cs                           (mod) pipelines, despachador y opciones
  Infrastructure.csproj                            (mod) + Microsoft.Extensions.Http.Resilience 10.10.0
src/API/appsettings.json                           (mod) secciones Outbox y Resiliencia
tests/UnitTests/…                                  (mod/nuevo) handler, serialización, políticas
tests/IntegrationTests/…                           (nuevo) outbox atómico, despachador, resiliencia HTTP
docs/funcionamiento.md, README.md                  (mod) se quita la brecha y se documenta la configuración
```

## 6. Modelo de datos

Tabla `MensajesOutbox`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid PK | = `IdEvento` del evento (Auditoría deduplica por él) |
| `Tipo` | varchar(200) | nombre del tipo (`ReporteCreadoEvent`); se resuelve con el registro de tipos conocidos |
| `Contenido` | jsonb | evento serializado con la convención de la API (camelCase, enums como códigos) |
| `RegistradoEn` | timestamptz | |
| `Intentos` | int, default 0 | |
| `ProximoIntento` | timestamptz | = `RegistradoEn` al crear |
| `EntregadoEn` | timestamptz null | |
| `Estado` | varchar(20) | `PENDIENTE` → `ENTREGADO` o `FALLIDO` |
| `UltimoError` | varchar(2000) null | recortado |

Índice parcial `IX_MensajesOutbox_Pendientes` sobre (`ProximoIntento`) `WHERE "Estado" = 'PENDIENTE'`. El rol `reportes_app` recibe los permisos por el `GRANT … ON ALL TABLES` que `--migrar` ya sincroniza en cada despliegue.

## 7. Contratos / interfaces

- **API HTTP**: sin cambios. Lo único que cambia es el comportamiento cuando el bus falla: antes 500 con el reporte creado, ahora 201.
- **Evento**: mismo `Source` (`urban-alert.reportes`), `DetailType` (`ReporteCreado`) y `Detail`. Prueba de no regresión en RF-006.
- **Configuración nueva** (con valores por defecto, sin cambios en Terraform):
  - `Outbox:Habilitado` (true), `Outbox:IntervaloMs` (1000), `Outbox:Lote` (20), `Outbox:MaxIntentos` (10)
  - `Resiliencia:<Geoespacial|Multimedia>:Reintentos` (2), `:RetardoBaseMs` (200), `:RatioFallos` (0.5), `:VentanaSegundos` (30), `:MinimoLlamadas` (5), `:PausaSegundos` (15)
  - `Geoespacial:TimeoutSegundos` y `Multimedia:TimeoutSegundos` pasan a ser el timeout por intento.

## 8. Decisiones

### D-001: Outbox propio en lugar del outbox de MassTransit
- **Decisión**: tabla y despachador propios, que publican por `IEventPublisher`.
- **Alternativas**: el outbox de EF Core de MassTransit. Solo funciona con transportes de MassTransit, y en AWS se usa EventBridge sin MassTransit. Tener un outbox por transporte duplicaría la lógica.
- **Motivo**: es un solo mecanismo para los dos transportes. Son unas 150 líneas y se pueden probar.

### D-002: Atomicidad explícita en el repositorio
- **Decisión**: `IReporteRepository.AgregarAsync(reporte, eventoCreacion, ct)`.
- **Alternativas**: un `IOutbox.Registrar(evento)` que dependa de que el siguiente `SaveChanges` del mismo `DbContext` lo confirme. El acoplamiento queda implícito y es fácil romperlo.
- **Motivo**: la firma obliga a guardar ambos juntos y la prueba unitaria lo verifica.

### D-003: `IEventPublisher` no genérico
- **Decisión**: `PublicarAsync(object evento, CancellationToken)`. EventBridge toma `evento.GetType()` para `DetailType` y la serialización; MassTransit usa `Publish(evento, evento.GetType())`.
- **Motivo**: el despachador deserializa a `object` desde la fila. Con `TEvento = object`, la versión genérica produciría `DetailType = "Object"`.

### D-004: Pipeline de Microsoft.Extensions.Http.Resilience
- **Decisión**: `AddResilienceHandler` con Retry (exponencial con jitter) → CircuitBreaker → Timeout por intento.
- **Alternativas**: `AddStandardResilienceHandler`. Sus valores por defecto (3 reintentos, 30 s de timeout total) no cumplen RNF-003 y no permiten el predicado de Multimedia. Implementarlo a mano sería más código y menos probado.
- **Motivo**: es el paquete oficial y permite configurar los valores de la spec.

### D-005: Multimedia solo reintenta errores de conexión
- **Decisión**: el predicado de reintento de Multimedia es `HttpRequestException` con `HttpRequestError` igual a `ConnectionError` o `NameResolutionError`. Sus `ShouldHandle` del circuito son los transitorios por defecto (5xx, 408, 429, timeouts, errores de red).
- **Motivo**: es la clarificación de la spec. El cuerpo es un `ByteArrayContent`, que se puede reenviar.

### D-006: Presupuesto de tiempo (RNF-003)
- Geoespacial: 3 intentos × 3 s + backoff (unos 0,2 s y 0,4 s con jitter) ≈ 9,7 s, con tope de 12 s.
- Multimedia: los errores de conexión fallan rápido; el peor caso realista es un intento de 10 s con tope de 12 s.
- Total en el peor caso ≈ 22 s, por debajo de 25 s.

### D-007: El despachador mantiene la transacción mientras publica
- **Decisión**: lotes de 20 dentro de una transacción con `SKIP LOCKED`, y el cliente de EventBridge con timeout de 5 s y 2 reintentos del SDK.
- **Alternativas**: marcar "en proceso" con un lease y publicar fuera de la transacción. Es más complejo y obliga a recuperar leases vencidos.
- **Motivo**: con el volumen del prototipo, una transacción corta por lote es lo más simple. Usa 1 conexión por tarea (RNF-004).

## 9. Estrategia de pruebas

- **Unitarias** (`tests/UnitTests`):
  - Handler: pasa al repositorio un evento con los datos del reporte; si el repositorio falla, el error se propaga; ya no depende de `IEventPublisher`.
  - `SerializacionEventos`: ida y vuelta de `ReporteCreadoEvent` con enums como códigos.
  - `EventBridgeEventPublisher` con `object`: `DetailType = ReporteCreado` y el mismo `Detail` que antes (no regresión de RF-006).
  - Políticas HTTP con un `HttpMessageHandler` falso:
    - Geoespacial: reintenta 503 y luego responde bien; no reintenta 400 ni 409; abre el circuito tras 5 fallos, y con el circuito abierto responde en menos de 50 ms sin llamar.
    - Multimedia: reintenta un error de conexión; no reintenta un timeout ni un 503.
    - Cada intento de Geoespacial se firma de nuevo.
- **Integración** (`tests/IntegrationTests`, PostgreSQL real):
  - `POST` crea el reporte y su fila de outbox en `PENDIENTE`, que el despachador pasa a `ENTREGADO`.
  - Con un publicador que falla, la respuesta es 201, la fila queda `PENDIENTE` con `Intentos` > 0 y pasa a `ENTREGADO` cuando el publicador se recupera.
  - Tras `MaxIntentos` la fila queda `FALLIDO` y las siguientes se siguen entregando.
  - Dos despachadores concurrentes no publican dos veces la misma fila.
  - Si falla el guardado (por ejemplo, un `Id` duplicado), no queda ninguna fila.
- **Comandos** (Podman, sin SDK local):
  - `podman run --rm -v "$PWD":/w -w /w -v ~/.cache/nuget-podman:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/UnitTests/UnitTests.csproj`
  - Integración: PostgreSQL y RabbitMQ en contenedores, `--network host`, `--migrar` antes de `dotnet test tests/IntegrationTests` (orden de CI).
  - Migración: `dotnet ef migrations add AgregarOutbox` dentro del contenedor SDK, con `dotnet tool install dotnet-ef` en versión fija igual a la de EF Core Design.

## 10. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Tarea detenida entre publicar y COMMIT | Evento duplicado | Aceptado: al menos una vez, y Auditoría deduplica por `idEvento`. |
| Despachador en `--migrar` o en pruebas no deseadas | Publica desde un proceso efímero | `--migrar` no construye el host web (`ModoMigracion` usa su propia configuración), así que no hay hosted services. `Outbox:Habilitado=false` disponible para pruebas. |
| Sondeo cada 1 s con N tareas | Carga en la RDS | Consulta sobre un índice parcial, a lo sumo 5 tareas: despreciable. |
| Coste de PutEvents | Bajo | El mismo número de eventos que hoy (uno por reporte); los reintentos solo ocurren ante fallos. |
| El circuito abre por fallos de un cliente concreto | 503 para todos | Solo cuentan los transitorios (no 400 ni 409). `MinimoLlamadas` = 5. |
| Tabla que crece sin límite | Almacenamiento | Fuera de alcance (spec §8). Volumen bajo; se documenta. |
| Compatibilidad del paquete de resiliencia con `Microsoft.Extensions.Http` 10.0.12 | Fallo de restore | Si NuGet pide una versión mayor, se sube a la mínima que exija, también fijada. |

## 11. Trazabilidad

| Requisito | Componentes | Pruebas |
|---|---|---|
| RF-001 | `ReporteRepository.AgregarAsync`, `MensajeOutbox` | IT: fila junto al reporte; si falla el guardado no hay fila |
| RF-002 | `DespachadorOutbox` | IT: PENDIENTE → ENTREGADO |
| RF-003 | `DespachadorOutbox` (backoff) | IT: publicador que falla y se recupera |
| RF-004 | `DespachadorOutbox` (MaxIntentos) | IT: FALLIDO y las demás se entregan |
| RF-005 | `CrearReporteHandler` | IT: 201 con el publicador caído |
| RF-006 | `EventBridgeEventPublisher`, `MassTransitEventPublisher`, `SerializacionEventos` | UT: no regresión de DetailType y Detail |
| RF-007 | `PoliticasHttp` | UT: reintentos de Geoespacial y predicado de Multimedia |
| RF-008 | `PoliticasHttp` | UT: 400 y 409 sin reintento |
| RF-009 | `PoliticasHttp`, traducción en los clientes | UT: el circuito abre y responde 503 |
| RF-010 | `PoliticasHttp` (PausaSegundos) | UT: half-open y cierre (con `TimeProvider` falso) |
| RF-011 | Pipelines separados por cliente | UT: Multimedia abierto no afecta a Geoespacial |
| RF-012 | `DependencyInjection` | UT: timeout por intento configurable |
| RF-013 | Telemetría de Polly (logs OnRetry y OnCircuitOpened), log Error en FALLIDO | UT/IT: aserción sobre el log de FALLIDO |
| RNF-001 | Intervalo de 1 s | IT: entregado en menos de 5 s |
| RNF-002 | CircuitBreaker | UT: menos de 50 ms con el circuito abierto |
| RNF-003 | D-006, topes de `HttpClient.Timeout` | UT: configuración |
| RNF-004 | Despachador secuencial | Revisión: 1 conexión |
| RNF-005 | Outbox + reintentos + FALLIDO registrado | IT de RF-002 a RF-004 |
| RNF-006 | Sin Terraform | Revisión del diff |
