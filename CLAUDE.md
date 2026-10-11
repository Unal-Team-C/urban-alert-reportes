# CLAUDE.md

Microservicio de Reportes de UrbanAlert (.NET 10 + ASP.NET Core, EF Core/Npgsql, PostgreSQL; MassTransit/RabbitMQ en local), extraído del monorepo UrbanAlert para migrarlo a AWS con CI/CD en GitHub Actions.

- Local: `docker compose -f deploy/docker/docker-compose.yml up -d` (PostgreSQL + RabbitMQ) y `dotnet run --project src/API/API.csproj` (Development, :5039).
- Pruebas: `dotnet test UrbanAlert.Reportes.slnx` (integración con Testcontainers; requiere Docker/Podman).

AWS (spec `specs/001-migracion-aws/`): cuenta 348364742283, us-east-2. Terraform se aplica solo desde GitHub Actions.

- Se despliega sobre la plataforma compartida `Unal-Team-C/urban-alert-plataforma` (contrato SSM `/urban-alert/plataforma/`, guía `docs/integracion.md`), siguiendo el patrón de `urban-alert-usuarios` (spec 001) y `urban-alert-geoespacial` (spec 002).
- No reutilizar ni gestionar recursos de otros servicios ni el bus de EventBridge (creado a mano, fuera de todo repo).

## Desarrollo guiado por especificaciones (SDD)

Este proyecto usa un arnés SDD con persistencia en disco para sobrevivir a interrupciones de sesión.
El protocolo es obligatorio en todas las sesiones:

@.sdd/PROTOCOL.md
