# UrbanAlert.Reportes

Servicio .NET 10 que administra el ciclo de vida de los reportes de daño urbano: creación (con o sin imagen), consulta, cambios de estado/nivel de emergencia/asignación, rechazo y eliminación. Persiste en PostgreSQL y publica `ReporteCreadoEvent` por MassTransit/RabbitMQ, que consume `urban-alert-auditoria`.

## Estructura de la solución

```
src/
  Domain/          Modelo de dominio (Reporte, catálogo de categorías/tipos, reglas de transición de estado).
  Application/     Casos de uso (handlers + interfaces de los puertos) y el contrato de eventos publicados.
  Infrastructure/  EF Core (PostgreSQL), clientes HTTP de Geoespacial/Multimedia, publicación MassTransit.
  API/             Host ASP.NET Core: controlador REST, DTOs, middleware y Program.cs.
tests/
  UnitTests/        Pruebas unitarias (dominio, aplicación), sin dependencias externas.
  IntegrationTests/ Pruebas con PostgreSQL real vía Testcontainers + WebApplicationFactory.
deploy/docker/      Dockerfile y docker-compose (PostgreSQL + RabbitMQ) de desarrollo.
docs/               Notas de implementación y brechas conocidas (docs/funcionamiento.md).
```

## Ejecutar

```bash
dotnet build UrbanAlert.Reportes.slnx
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj   # requiere Docker (Testcontainers)
```

Ninguna credencial va en `appsettings*.json`. `ConnectionStrings:Postgres` y `RabbitMq:Username/Password` se cargan con [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (`UserSecretsId` ya está en `src/API/API.csproj`):

```bash
cd src/API
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=urbanalert_reportes;Username=urbanalert;Password=urbanalert"
dotnet user-secrets set "RabbitMq:Username" "urbanalert"
dotnet user-secrets set "RabbitMq:Password" "urbanalert"
```

Con `deploy/docker/docker-compose.yml` arriba (PostgreSQL + RabbitMQ) y esos secrets cargados, `dotnet run --project src/API/API.csproj` levanta la API en `http://localhost:5039` (Development), con la referencia OpenAPI en `/scalar/v1`. Las migraciones de EF Core se aplican automáticamente al arrancar en Development.

Las credenciales del compose salen de `deploy/docker/.env` (gitignorado); copiar `deploy/docker/.env.example` y ajustar las contraseñas antes del primer `up`:

```bash
cp deploy/docker/.env.example deploy/docker/.env
# editar deploy/docker/.env con las contraseñas reales
docker compose -f deploy/docker/docker-compose.yml up -d
```

## Documentación

- [docs/funcionamiento.md](docs/funcionamiento.md): estado real de la implementación, flujo de creación, dependencias externas y brechas conocidas.
- `/scalar/v1` (en Development): contrato OpenAPI generado desde las anotaciones del controlador.
