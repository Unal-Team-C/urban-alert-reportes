# Verificación: Entrega garantizada del evento de creación y resiliencia ante dependencias

- **Fecha**: 2026-10-19
- **Rama**: `002-resiliencia-eventos` (base `origin/main` c5f09ab), 15 commits locales sin push
- **Resultado global**: ✅ en local, con dos observaciones ⚠️ que no bloquean (MS-002 por equivalencia; CI y despliegue pendientes del merge)

## Comandos

| Comando | Resultado |
|---|---|
| `$DOTNET build UrbanAlert.Reportes.slnx -c Release` | ✅ 0 errores, 0 advertencias |
| `$DOTNET test tests/UnitTests` | ✅ 108/108 (86 antes de la feature) |
| PostgreSQL 16 vacío + `--migrar` | ✅ 7 migraciones (incluida `AgregarOutbox`); `reportes_app` con permisos sobre `MensajesOutbox`; segunda ejecución: 0 pendientes |
| `$IT` (PostgreSQL + RabbitMQ reales, Podman) | ✅ 83/83 (77 antes); 3 corridas seguidas sin fallos intermitentes |
| `git diff origin/main -- infra .github` | ✅ 0 líneas (sin Terraform ni workflows: P1, P7, RNF-006) |
| Búsqueda de credenciales en el diff | ✅ solo valores de prueba ya existentes (`AKIDEXAMPLE`, `Password=x`) |
| Job `test` de CI y Provision | ⚠️ no ejecutados: corren al hacer merge a `main` (P1) |

## Matriz de requisitos

| Requisito | Evidencia | Estado |
|---|---|---|
| RF-001 atomicidad reporte + evento | `ReporteRepository.cs:16` (un `SaveChanges`); IT `OutboxGuardadoTests.cs:26` (fila del evento junto al reporte) y `:50` (Id duplicado → ninguna fila); UT `CrearReporteHandlerTests.cs:57` | ✅ |
| RF-002 entrega sin depender de la petición | `DespachadorOutbox.cs` (BackgroundService); IT `DespachadorOutboxTests.cs:36`, `:137` | ✅ |
| RF-003 reintento con espera creciente | `MensajeOutbox.RegistrarFallo`; UT `SerializacionEventosTests.cs:63` (2 s, 8 s, tope 300 s); IT `DespachadorOutboxTests.cs:36` (sin reintento antes del backoff; entrega al recuperarse) | ✅ |
| RF-004 FALLIDO tras N sin bloquear | UT `SerializacionEventosTests.cs:75`; IT `DespachadorOutboxTests.cs:71` (problemático FALLIDO con 3 intentos, sano ENTREGADO) | ✅ |
| RF-005 201 aunque falle el bus | IT `DespachadorOutboxTests.cs:36` (publicador caído → 201) | ✅ |
| RF-006 evento idéntico | UT `EventBridgeEventPublisherTests.cs:41` (formato), `:61` (object = tipado), `SerializacionEventosTests.cs:22`/`:33`; `MassTransitEventPublisher` con el tipo en tiempo de ejecución; IT con RabbitMQ real (`:137`) | ✅ |
| RF-007 reintentos (Geoespacial ×2; Multimedia solo sin conexión) | UT `PoliticasHttpTests.cs:59`, `:69`, `:143`, `:155` | ✅ |
| RF-008 sin reintento de errores de cliente | UT `PoliticasHttpTests.cs:81` (400, 409) | ✅ |
| RF-009 circuito: 50 %/30 s/mín. 5 → 503 | UT `PoliticasHttpTests.cs:103` ("circuito abierto", sin llamada) | ✅ |
| RF-010 half-open a los 15 s | UT `PoliticasHttpTests.cs:122` (sigue abierto a 10 s; cierra tras 16 s) | ✅ |
| RF-011 circuitos independientes | UT `PoliticasHttpTests.cs:183` | ✅ |
| RF-012 timeouts por intento configurables | UT `PoliticasHttpTests.cs:165` (`Multimedia:TimeoutSegundos=1`) | ✅ |
| RF-013 logs de reintentos, circuito y FALLIDO | UT `PoliticasHttpTests.cs:195` (OnRetry, OnCircuitOpened); IT `DespachadorOutboxTests.cs:71` (log Error con "FALLIDO") | ✅ |
| RNF-001 95 % en < 5 s | IT `DespachadorOutboxTests.cs:137` (entregado en < 5 s con RabbitMQ real; intervalo de 1 s) | ✅ |
| RNF-002 < 50 ms con el circuito abierto | UT `PoliticasHttpTests.cs:103` (Stopwatch < 50 ms) | ✅ |
| RNF-003 creación < 25 s en el peor caso | D-006: Geoespacial ≈ 9,7 s (tope 12 s) + Multimedia 10 s (tope 12 s) ≈ 22 s; `HttpClient.Timeout` = `TimeoutTotalSegundos` (12) | ✅ por diseño y configuración |
| RNF-004 ≤ 1 conexión extra por instancia | El despachador es secuencial: un `DbContext` por vuelta | ✅ por revisión |
| RNF-005 al menos una vez | IT `:36`, `:71`, `:94` (cada evento publicado exactamente una vez con 2 despachadores) | ✅ |
| RNF-006 sin recursos nuevos | diff sin `infra/` | ✅ |

### Criterios de aceptación y métricas

| Criterio | Evidencia | Estado |
|---|---|---|
| HU-1.1, HU-1.2, HU-1.4 | RF-002, RF-005, RF-001 | ✅ |
| HU-1.3 / MS-002 (servicio detenido entre guardado y entrega) | La fila `PENDIENTE` es durable y la toma cualquier despachador: lo muestran `:36` (sigue pendiente tras fallar) y `:94` (otro despachador la entrega). No se probó un reinicio real del proceso. | ⚠️ por equivalencia |
| HU-2.1, HU-2.2, HU-2.3 | `PoliticasHttpTests.cs:59`, `:81`, `:143`/`:155`/`:165` | ✅ |
| HU-3.1, HU-3.2, HU-3.3 | `:103`, `:122`, `:183` | ✅ |
| MS-001, MS-003, MS-004 | `:36`; `:59`; `:103` | ✅ |

## Constitución

| Principio | Estado | Nota |
|---|---|---|
| P1 | ✅ | Sin Terraform; la tabla la crea `--migrar` desde Provision |
| P2 | ✅ | Bus existente, no gestionado |
| P3 | ✅ | Sin secretos nuevos |
| P4 | ✅ | Migración aditiva |
| P5 | ✅ | 0 USD nuevos |
| P6 | ✅ en local | Unitarias e integración antes de cada commit de tarea; el job de CI corre en el merge |
| P7 | N/A | Sin cambios de infraestructura |
| P8 | ✅ | `Microsoft.Extensions.Http.Resilience` 10.10.0 fijado; `dotnet-ef` 10.0.4 en la herramienta |
| P9 | ✅ | Mismo `Source`/`DetailType`/`Detail` |
| P10 | ✅ | Migración idempotente; `SKIP LOCKED` entre tareas |

## Hallazgos

- **Corregido en T010**: Polly rechaza `MaxRetryAttempts = 0` (ValidationException); `Reintentos=0` ahora omite la estrategia y las opciones se validan.
- **Corregido en T013**: RF-013 en dependencias se suponía y no se probaba; ahora tiene prueba.
- **Desviación documentada**: D-002 usa `IEventoDominio` en lugar de `object` (plan §8).
- **Cosmético**: varios títulos de commit son la descripción de la tarea recortada (T003–T009). No afecta al código.
- Sin TODOs ni código muerto. `IEventPublisher` ya no lo usa ningún handler, solo el despachador.
- **Fuera de alcance, pendiente** (spec §8): saga o compensación de imagen o coordenada huérfanas, reproceso de FALLIDO, alarmas, purga del outbox.
