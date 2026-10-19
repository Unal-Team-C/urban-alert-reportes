# Tareas: Entrega garantizada del evento de creación y resiliencia ante dependencias

- **Plan**: ./plan.md
- **Estado**: aprobado <!-- borrador | aprobado -->

Leyenda: `[ ]` pendiente · `[~]` en progreso · `[x]` hecha · `[!]` bloqueada · `[-]` descartada · `[P]` paralelizable

Formato de cada tarea (una línea):
`- [ ] T001 [P] Descripción imperativa — archivos: ruta/a, ruta/b — cubre: RF-001 — verifica: <comando o criterio observable>`

Dependencias: cada fase depende de la anterior; dentro de una fase se ejecuta en orden salvo `[P]`.

**Herramientas de verificación local** (sin SDK .NET instalado; Podman con `podman machine start`):
- `$DOTNET` = `podman run --rm -v "$PWD":/w -w /w -v ~/.cache/nuget-podman:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:10.0 dotnet`
- `$IT`: PostgreSQL y RabbitMQ en contenedores, `--migrar` sobre la base vacía y después `$DOTNET test tests/IntegrationTests` con `--network host` y `-e ConnectionStrings__Postgres=… -e RabbitMq__*` (mismo orden que CI). Si no es posible en local, se verifica en el job `test` de CI y se anota en el journal.

## Fase 1: Preparación

- [x] T001 Añadir `Microsoft.Extensions.Http.Resilience` 10.10.0 (si NuGet exige subir `Microsoft.Extensions.Http`, subirlo a la versión mínima exacta y anotarlo en el plan) — archivos: src/Infrastructure/Infrastructure.csproj — cubre: D-004, P8 — verifica: `$DOTNET build UrbanAlert.Reportes.slnx` en verde y las pruebas unitarias actuales siguen pasando

## Fase 2: Núcleo: outbox

- [x] T002 Hacer `IEventPublisher` no genérico (`PublicarAsync(object, ct)`): EventBridge usa el tipo en tiempo de ejecución para `DetailType` y la serialización, y su cliente tiene timeout de 5 s; MassTransit usa `Publish(evento, evento.GetType())`. Ajustar las pruebas existentes y añadir una de no regresión (`DetailType = ReporteCreado`, `Detail` idéntico) — archivos: src/Application/Interfaces/Eventos/IEventPublisher.cs, src/Infrastructure/Mensajeria/EventBridgeEventPublisher.cs, src/Infrastructure/Mensajeria/MassTransitEventPublisher.cs, src/Infrastructure/DependencyInjection.cs, tests/UnitTests/Infrastructure/EventBridgeEventPublisherTests.cs, tests/UnitTests/Application/Reportes/CrearReporteHandlerTests.cs — cubre: RF-006, D-003 — verifica: `$DOTNET test tests/UnitTests` en verde, con la prueba de no regresión incluida
- [x] T003 Crear la entidad `MensajeOutbox` (estados PENDIENTE, ENTREGADO, FALLIDO), su configuración EF, el `DbSet` y `SerializacionEventos` (JSON Web con códigos y registro de tipos conocidos), con prueba unitaria de ida y vuelta de `ReporteCreadoEvent` — archivos: src/Infrastructure/Mensajeria/Outbox/MensajeOutbox.cs, src/Infrastructure/Mensajeria/Outbox/SerializacionEventos.cs, src/Infrastructure/Persistencia/Configuraciones/MensajeOutboxConfiguration.cs, src/Infrastructure/Persistencia/ReportesDbContext.cs, tests/UnitTests/Infrastructure/SerializacionEventosTests.cs — cubre: RF-001, RF-006 — verifica: `$DOTNET test tests/UnitTests` en verde
- [x] T004 Generar la migración `AgregarOutbox` (tabla `MensajesOutbox` e índice parcial de pendientes) con `dotnet-ef` en versión fija dentro del contenedor SDK — archivos: src/Infrastructure/Persistencia/Migraciones/*_AgregarOutbox.cs, *.Designer.cs, ReportesDbContextModelSnapshot.cs — depende: T003 — cubre: RF-001 — verifica: `--migrar` sobre una base vacía crea la tabla y el índice (`\d "MensajesOutbox"`), y repetirlo da 0 migraciones pendientes
- [x] T005 Guardado atómico: `IReporteRepository.AgregarAsync(reporte, eventoCreacion, ct)` añade el reporte y el mensaje en un solo `SaveChanges`; `CrearReporteHandler` deja de publicar y pasa el evento al repositorio. Pruebas unitarias del handler y prueba de integración (fila PENDIENTE junto al reporte; si falla el guardado, no queda fila) — archivos: src/Application/Interfaces/Reportes/IReporteRepository.cs, src/Application/Reportes/CrearReporte/CrearReporteHandler.cs, src/Infrastructure/Persistencia/Repositorios/ReporteRepository.cs, tests/UnitTests/Application/Reportes/CrearReporteHandlerTests.cs, tests/IntegrationTests/Reportes/OutboxGuardadoTests.cs — depende: T002, T004 — cubre: RF-001, RF-005 — verifica: unitarias en verde y `$IT` (o CI) en verde
- [x] T006 Implementar `DespachadorOutbox` (BackgroundService) con `OutboxOpciones`: lotes con `FOR UPDATE SKIP LOCKED`, backoff `min(2^n s, 5 min)`, FALLIDO tras `MaxIntentos` con log Error y `UltimoError` recortado, y `Outbox:Habilitado`. Registro en DI y en appsettings — archivos: src/Infrastructure/Mensajeria/Outbox/DespachadorOutbox.cs, src/Infrastructure/Mensajeria/Outbox/OutboxOpciones.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json — depende: T005 — cubre: RF-002, RF-003, RF-004, RF-013, RNF-001, RNF-004 — verifica: `$DOTNET build` en verde; el comportamiento lo prueba T007
- [x] T007 Pruebas de integración del despachador con un publicador controlable: PENDIENTE → ENTREGADO en menos de 5 s; un publicador que falla da 201 y la fila se reintenta y se entrega al recuperarse; tras MaxIntentos queda FALLIDO y las demás filas se entregan; dos despachadores concurrentes publican cada fila una sola vez — archivos: tests/IntegrationTests/Mensajeria/DespachadorOutboxTests.cs, tests/IntegrationTests/ReportesApiFactory.cs — depende: T006 — cubre: RF-002, RF-003, RF-004, RF-005, RNF-001, RNF-005 — verifica: `$IT` (o CI) en verde

## Fase 3: Núcleo: resiliencia HTTP

- [x] T008 Crear `PoliticasHttp` con los pipelines de Geoespacial (Retry ×2 exponencial con jitter → CircuitBreaker 50 %/30 s/mín. 5/pausa 15 s → Timeout por intento) y Multimedia (reintento solo con `ConnectionError`/`NameResolutionError`), configurables en `Resiliencia:*`. Conectarlos en DI antes del `AwsSignatureHandler`; `HttpClient.Timeout` pasa a ser un tope total de 12 s — archivos: src/Infrastructure/Resiliencia/PoliticasHttp.cs, src/Infrastructure/DependencyInjection.cs, src/API/appsettings.json — depende: T001 — cubre: RF-007, RF-009, RF-010, RF-011, RF-012, RNF-003 — verifica: `$DOTNET build` en verde
- [x] T009 Traducir `BrokenCircuitException` y `TimeoutRejectedException` en `GeoespacialHttpClient` y `MultimediaHttpClient` a las excepciones de "no disponible" (503) — archivos: src/Infrastructure/Geoespacial/GeoespacialHttpClient.cs, src/Infrastructure/Multimedia/MultimediaHttpClient.cs — depende: T008 — cubre: RF-009 — verifica: el middleware devuelve 503 (prueba en T010)
- [x] T010 Pruebas de las políticas con un `HttpMessageHandler` falso y `TimeProvider` falso:
  - Geoespacial: reintenta 503 y luego responde bien; no reintenta 400 ni 409; el circuito abre tras 5 fallos y responde 503 en menos de 50 ms sin llamar; tras la pausa pasa a half-open y cierra; cada intento se firma de nuevo.
  - Multimedia: reintenta el error de conexión; no reintenta timeout ni 503.
  - Un circuito de Multimedia abierto no afecta a Geoespacial.

  Archivos: tests/UnitTests/Infrastructure/PoliticasHttpTests.cs. Ajustar tests/IntegrationTests/Geoespacial/GeoespacialHttpClientTests.cs y tests/IntegrationTests/Multimedia/MultimediaHttpClientTests.cs si dependen del timeout. Depende de: T009. Cubre: RF-007, RF-008, RF-009, RF-010, RF-011, RF-012, RF-013, RNF-002. Verifica: `$DOTNET test tests/UnitTests` en verde y `$IT` (o CI) en verde.

## Fase 4: Pulido

- [x] T011 Documentar: quitar la brecha del outbox de docs/funcionamiento.md; describir el outbox, el despachador, las políticas y las claves `Outbox:*`/`Resiliencia:*` en README y docs; anotar la compensación (saga) como brecha restante — archivos: README.md, docs/funcionamiento.md, CLAUDE.md — cubre: RNF-006, spec §8 — verifica: la documentación coincide con appsettings.json y con el código (revisión cruzada de claves)
- [x] T012 Comprobación completa antes de verificar: base nueva + `--migrar` + IT, unitarias, `git diff --stat origin/main` sin cambios en `infra/` ni en `.github/` — archivos: — — cubre: RNF-006, P6 — verifica: todas en verde; diff sin Terraform

## Correcciones de verificación

<!-- /sdd-verify añade aquí tareas si detecta fallos -->

- [x] T013 Comprobar que los reintentos y la apertura del circuito quedan en el log (RF-013 en dependencias; hoy solo se supone por la telemetría de Polly) — archivos: tests/UnitTests/Infrastructure/PoliticasHttpTests.cs — cubre: RF-013 — verifica: `$DOTNET test tests/UnitTests` con aserciones sobre los eventos OnRetry y OnCircuitOpened en el log

## Cobertura

| Requisito | Tareas |
|---|---|
| RF-001 | T003, T004, T005 |
| RF-002 | T006, T007 |
| RF-003 | T006, T007 |
| RF-004 | T006, T007 |
| RF-005 | T005, T007 |
| RF-006 | T002, T003 |
| RF-007 | T008, T010 |
| RF-008 | T010 |
| RF-009 | T008, T009, T010 |
| RF-010 | T008, T010 |
| RF-011 | T008, T010 |
| RF-012 | T008, T010 |
| RF-013 | T006, T010, T013 |
| RNF-001 | T006, T007 |
| RNF-002 | T010 |
| RNF-003 | T008 |
| RNF-004 | T006 |
| RNF-005 | T007 |
| RNF-006 | T011, T012 |
