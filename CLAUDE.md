# CLAUDE.md

Microservicio de Reportes de UrbanAlert (.NET 10 + ASP.NET Core, EF Core/Npgsql, PostgreSQL; MassTransit/RabbitMQ en local), extraído del monorepo UrbanAlert para migrarlo a AWS con CI/CD en GitHub Actions.

- Local: `docker compose -f deploy/docker/docker-compose.yml up -d` (PostgreSQL + RabbitMQ) y `dotnet run --project src/API/API.csproj` (Development, :5039).
- Pruebas: `dotnet test UrbanAlert.Reportes.slnx` (integración con Testcontainers; requiere Docker/Podman).

AWS (spec `specs/001-migracion-aws/`): cuenta 348364742283, us-east-2. Terraform se aplica solo desde GitHub Actions.

- Se despliega sobre la plataforma compartida `Unal-Team-C/urban-alert-plataforma` (contrato SSM `/urban-alert/plataforma/`, guía `docs/integracion.md`), siguiendo el patrón de `urban-alert-usuarios` (spec 001) y `urban-alert-geoespacial` (spec 002).
- No reutilizar ni gestionar recursos de otros servicios ni el bus de EventBridge (creado a mano, fuera de todo repo).
- `infra/base` (ECR, roles propios incl. rol de tarea, SG, RDS, secreto, logs, clúster, task definition de migración), `infra/service` (Express) e `infra/routes` (`ANY /api/v1/reportes[/{proxy+}]`, públicas); estado en `s3://urban-alert-tfstate-348364742283/reportes/`. Inventario: `infra/INVENTARIO.md`.
- Configuración de la nube por claves (README «Configuración: local y AWS»): EventBridge, Geoespacial firmado por el gateway, Multimedia `Gateway`. Migración: `dotnet API.dll --migrar`.
- Workflows: `provision.yml` (push a `main`) y `deprovision.yml` (manual, confirmación `urban-alert-reportes`). Arranque: política `github-actions-urban-alert-reportes` (solo `events:DescribeEventBus`).

## Validación local (Podman, sin SDK .NET ni Terraform instalados)

- .NET: `podman run --rm -v "$PWD":/w -w /w -v ~/.cache/nuget-podman:/root/.nuget/packages mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/UnitTests/UnitTests.csproj`. Integración: PostgreSQL y RabbitMQ en contenedores y `--network host` con `ConnectionStrings__Postgres`/`RabbitMq__*`.
- Terraform, actionlint y shellcheck: comandos en el README («Validar en local»).

## Gotchas

- Las pruebas de integración no usan Testcontainers: sobre una base vacía los fixtures migran a la vez y chocan (`PK___EFMigrationsHistory`); migrar antes con `--migrar` (lo hace CI).
- `AwsSignatureHandler` quita `Authorization`/`X-Amz-*` de la petición al terminar el envío: en pruebas, copiar cabeceras en el momento.
- Npgsql intenta cifrado GSS (Kerberos) si no se desactiva: `GSS Encryption Mode=Disable` en las cadenas de la nube (la imagen no trae libgssapi).
- macOS trae bash 3.2: los scripts no usan funciones de bash 4.
- Outbox (spec 002): el evento de creación se guarda con el reporte (`IReporteRepository.AgregarAsync(reporte, evento)`) y lo publica `DespachadorOutbox`; los handlers no llaman a `IEventPublisher`. Un evento nuevo debe implementar `IEventoDominio` y registrarse en `SerializacionEventos`.
- Captcha (spec 003): `IVerificadorCaptcha` se llama primero en `CrearReporteHandler`; desactivado por defecto. En AWS el secreto `urban-alert-reportes-captcha` es de arranque (fuera del estado, `infra/bootstrap/README.md §5`). Las pruebas de integración con la verificación activa usan `CaptchaApiFactory`.
- Terraform local: el `.terraform` de `infra/*` puede traer un symlink que rompe `init`; usar `-e TF_DATA_DIR=/tmp/tfdata` en el contenedor.
- Polly exige `MaxRetryAttempts ≥ 1`: `Resiliencia:*:Reintentos=0` omite la estrategia en vez de pasarle 0.
- Las pruebas del despachador (`tests/IntegrationTests/Mensajeria`) van en una colección sin paralelismo: otro host de prueba con despachador activo entregaría sus filas.

## Desarrollo guiado por especificaciones (SDD)

Este proyecto usa un arnés SDD con persistencia en disco para sobrevivir a interrupciones de sesión.
El protocolo es obligatorio en todas las sesiones:

@.sdd/PROTOCOL.md
