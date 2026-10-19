# UrbanAlert.Reportes

Servicio .NET 10 que administra el ciclo de vida de los reportes de daño urbano: creación (con o sin imagen), consulta, cambios de estado/nivel de emergencia/asignación, rechazo y eliminación. Persiste en PostgreSQL y publica `ReporteCreadoEvent`, que consume `urban-alert-auditoria`: por MassTransit/RabbitMQ en local y por Amazon EventBridge en AWS.

## Estructura de la solución

```
src/
  Domain/          Modelo de dominio (Reporte, catálogo de categorías/tipos, reglas de transición de estado).
  Application/     Casos de uso (handlers + interfaces de los puertos) y el contrato de eventos publicados.
  Infrastructure/  EF Core (PostgreSQL), clientes HTTP de Geoespacial/Multimedia, publicación (MassTransit o EventBridge),
                   migración en la nube.
  API/             Host ASP.NET Core: controlador REST, DTOs, middleware, Program.cs y el modo --migrar.
tests/
  UnitTests/        Pruebas unitarias (dominio, aplicación, EventBridge, firma SigV4), sin dependencias externas.
  IntegrationTests/ Pruebas con PostgreSQL y RabbitMQ reales + WebApplicationFactory.
deploy/docker/      Dockerfile y docker-compose (PostgreSQL + RabbitMQ) de desarrollo.
infra/              Terraform de AWS: base, service, routes; inventario y recursos de arranque.
scripts/            Scripts de los workflows (comprobaciones, migración, listener, humo) y sus pruebas.
.github/workflows/  Provision (push a main) y Deprovision (manual).
docs/               Notas de implementación y brechas conocidas (docs/funcionamiento.md).
specs/              Especificación, plan y tareas (SDD): 001 migración a AWS, 002 outbox y resiliencia.
```

## Ejecutar en local

```bash
dotnet build UrbanAlert.Reportes.slnx
dotnet test tests/UnitTests/UnitTests.csproj
dotnet test tests/IntegrationTests/IntegrationTests.csproj   # requiere PostgreSQL y RabbitMQ (compose de abajo) y las credenciales
```

Las pruebas de integración usan el PostgreSQL y el RabbitMQ configurados (User Secrets o variables `ConnectionStrings__Postgres`, `RabbitMq__Host/Username/Password`), no Testcontainers. Sobre una base vacía, ejecutar antes `dotnet run --project src/API/API.csproj -- --migrar` (ver «Migración») o lanzar las pruebas dos veces: los fixtures migran a la vez al arrancar.

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

## Configuración: local y AWS

Todo lo que cambia en la nube se elige por configuración; sin ella, el comportamiento es el local.

| Clave | Local (por defecto) | AWS (`infra/service`) |
|---|---|---|
| `Mensajeria:Transporte` | `RabbitMq` (MassTransit) | `EventBridge` (`EventBridge:Bus` = ARN del bus) |
| `Geoespacial:BaseUrl` / `RutaCoordenadas` | `http://localhost:8000` / `api/v1/geospatial/coordinates` | URL del gateway / `api/v1/geoespacial/coordinates` |
| `Geoespacial:FirmaIam:Region` | vacío (sin firma) | `us-east-2`: firma SigV4 con el rol de tarea |
| `Multimedia:BaseUrl` / `Modo` | `http://localhost:9010` / `Emulador` (sobre del emulador de Lambda) | URL del gateway / `Gateway` (multipart normal) |
| `Logging:Console:FormatterName` | texto | `json` (una línea por evento) |
| `Apagado:TimeoutSegundos` | 30 (por defecto de .NET) | 20 |
| `Outbox:*` | `Habilitado=true`, `IntervaloMs=1000`, `Lote=20`, `MaxIntentos=10`, `TimeoutPublicacionSegundos=10` | igual |
| `Captcha:*` (spec 003) | `Habilitado=false` | `false` hasta activarlo (`captcha_habilitado`); activo: `ClaveSecreta` del secreto de arranque `urban-alert-reportes-captcha`, `Accion=crear-reporte`, `HostnamesPermitidos` = dominio de CloudFront |
| `Resiliencia:<Geoespacial\|Multimedia\|Captcha>:*` | `Reintentos=2`, `RetardoBaseMs=200`, `RatioFallos=0.5`, `VentanaSegundos=30`, `MinimoLlamadas=5`, `PausaSegundos=15`, `TimeoutTotalSegundos=12` | igual |

El evento de creación se guarda con el reporte en la tabla `MensajesOutbox` y lo entrega en segundo plano `DespachadorOutbox`: un fallo del bus no hace fallar la creación. Las llamadas a Geoespacial y Multimedia tienen reintentos y circuit breaker. Detalle en [docs/funcionamiento.md](docs/funcionamiento.md) («Entrega de eventos» y «Dependencias externas»).

Con `Captcha:Habilitado=true`, la creación (JSON y multipart) exige `captchaToken`, la prueba de Cloudflare Turnstile del portal. Si falta o no es válida responde 400, y si Turnstile no responde, 503. Para activarla en AWS: [infra/bootstrap/README.md §5](infra/bootstrap/README.md). En local se pueden usar las claves de prueba de Cloudflare: el secreto `1x0000000000000000000000000000000AA` acepta cualquier token, como `XXXX.DUMMY.TOKEN.XXXX`, y `2x0000000000000000000000000000000AA` los rechaza todos. Esas claves devuelven `hostname: "example.com"` y ninguna `action`, así que en local hay que dejar vacíos `Captcha:HostnamesPermitidos` y `Captcha:Accion` (por ejemplo, `Captcha__Accion=`).

Cada petición deja una línea de log con método, ruta, código y duración (`HttpLogging`), sin cabeceras ni cuerpos.

### Evento en EventBridge

```json
{
  "source": "urban-alert.reportes",
  "detail-type": "ReporteCreado",
  "detail": { "idEvento": "…", "reporte": { "id": "…", "estado": "REPORTADO", "idCoordenada": "…", "idUsuario": "…", "fecha": "2026-10-11T15:30:00Z" } }
}
```

Bus `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos` (creado a mano; ningún repo lo gestiona). Las reglas y destinos (Auditoria) se configuran fuera de este repo. Una entrada rechazada por EventBridge cuenta como fallo de publicación, igual que un fallo de RabbitMQ: el outbox la reintenta y, tras `Outbox:MaxIntentos`, la deja como `FALLIDO`.

### Migración

`dotnet API.dll --migrar` aplica las migraciones de EF Core como administrador y crea o sincroniza el rol de la aplicación (`SELECT/INSERT/UPDATE/DELETE`, sin DDL); después termina, sin levantar la web. Lee `Migracion__Host`, `__Puerto`, `__Base`, `__UsuarioAdmin`, `__ContrasenaAdmin`, `__UsuarioApp`, `__ContrasenaApp` y `__SslMode` (por defecto `Require`). Fuera de Development la API nunca migra al arrancar.

## Despliegue en AWS

Cuenta `348364742283`, región `us-east-2`. Spec, plan y decisiones en [specs/001-migracion-aws](specs/001-migracion-aws/).

El servicio corre en **ECS Express Mode** (Fargate) en la red privada de la plataforma compartida [`urban-alert-plataforma`](https://github.com/Unal-Team-C/urban-alert-plataforma), con su **propia instancia RDS PostgreSQL 17**, y solo se publica por el **API Gateway compartido**.

```text
Cliente ─HTTPS─► API Gateway (plataforma) ─ ANY /api/v1/reportes[/*] ─► VPC Link ─► ALB interno de Express ─► Reportes (1–5 tareas)
                                                                                                              │  └─► RDS urban-alert-reportes-db (TLS, rol sin DDL)
Reportes ─NAT─► API Gateway ─► POST /api/v1/geoespacial/coordinates (firma SigV4 del rol de tarea) ─► Geoespacial
                            └► POST /api/v1/multimedia/images (pública) ─► Lambda de Multimedia
Reportes ─NAT─► EventBridge PutEvents (bus urban-alert-bus-eventos) ─► reglas de Auditoria (fuera de este repo)
```

| Stack | Estado | Qué crea |
|---|---|---|
| `infra/base` | `reportes/base.tfstate` | ECR, roles (ejecución, infraestructura, tarea), grupos de seguridad, RDS, secreto de la app, logs, clúster, task definition de migración |
| `infra/service` | `reportes/service.tfstate` | Servicio Express `urban-alert-reportes` |
| `infra/routes` | `reportes/routes.tfstate` | 2 rutas en el gateway compartido |

Inventario completo: [infra/INVENTARIO.md](infra/INVENTARIO.md).

### Rutas en el API Gateway

Todas públicas (sin autenticación de clientes), como en el gateway local:

| Ruta pública | Ruta del servicio |
|---|---|
| `ANY /api/v1/reportes` | `/api/v1/Reportes` |
| `ANY /api/v1/reportes/{proxy+}` | `/api/v1/Reportes/{proxy}` |

La cadena de consulta se conserva. `/health`, `/scalar/v1` y `/openapi/v1.json` no se publican: el gateway responde 404. La respuesta `201` de creación trae `Location` con la ruta interna (`/api/v1/Reportes/{id}`); los clientes deben usar `idReporte` del cuerpo.

### Antes del primer despliegue (una sola vez)

Detalle en [infra/bootstrap/README.md](infra/bootstrap/README.md):

1. Un administrador crea la política [`github-actions-urban-alert-reportes`](infra/bootstrap/github-reportes-policy.json) (solo `events:DescribeEventBus` sobre el bus) y la adjunta al rol de los workflows.
2. En `Unal-Team-C/urban-alert-reportes` → Settings → Secrets and variables → Actions:

   | Tipo | Nombre | Ejemplo |
   |---|---|---|
   | Secreto | `AWS_ROLE_ARN` | `arn:aws:iam::348364742283:role/github-actions-<nombre>` (el de Geoespacial y Usuarios) |
   | Variable | `AWS_REGION` | `us-east-2` |

3. La plataforma, Geoespacial y Multimedia aprovisionados, y el bus existente.

### Workflows

| Workflow | Cuándo | Qué hace |
|---|---|---|
| [Provision](.github/workflows/provision.yml) | Cada push a `main` | Pruebas (con PostgreSQL y RabbitMQ en el runner), Terraform `fmt/validate/test`, actionlint, shellcheck → [comprueba plataforma y bus](scripts/check-platform.sh) → base → imagen `:<sha>` → [migración](scripts/run-schema-task.sh) → servicio → [listener](scripts/buscar-listener-express.sh) → rutas → [humo por el gateway](scripts/smoke-gateway-reportes.sh) |
| [Deprovision](.github/workflows/deprovision.yml) | Solo a mano | Exige escribir `urban-alert-reportes` → rutas → servicio → base (deja la copia final `urban-alert-reportes-final-<run_id>`) |

Los dos comparten el grupo de `concurrency` `urban-alert-reportes-aws`. Deprovision no toca la plataforma, el bus ni otros servicios. Tras desplegar, la creación completa (Geoespacial, Multimedia, EventBridge) se comprueba con `scripts/crear-reporte-prueba.sh <gateway>` (crea y borra dos reportes).

### Costes

Estimación mensual en `us-east-2` con el servicio encendido y sin tráfico (730 h). No incluye la plataforma compartida (NAT ~36,6 USD) ni el ALB interno de Express (~16–20 USD), que comparten todos los servicios.

| Recurso | Mes |
|---|---|
| RDS `db.t4g.micro` Single-AZ + 20 GiB gp3 | ~14 USD |
| Fargate, 1 tarea de 0,5 vCPU / 1 GiB (+ la tarea de migración, segundos por despliegue) | ~18 USD |
| Secrets Manager (1 secreto propio; el de RDS lo gestiona RDS; +1 si se activa el captcha) | ~0,4 USD (~0,8 con captcha) |
| Cloudflare Turnstile (verificación humana, opcional) | 0 |
| ECR (≤ 20 imágenes), CloudWatch Logs (30 días) | < 1 USD |
| EventBridge (1 USD por millón de eventos publicados) | ~0 |
| **Total** | **~33 USD** |

Para no pagar cuando no se usa: workflow Deprovision (queda la copia final de la base, que cobra almacenamiento de snapshot hasta que se borre).

### Validar en local (sin credenciales)

Con Podman (`podman machine start`), desde la raíz:

```bash
podman run --rm -v "$PWD":/w -w /w mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/UnitTests/UnitTests.csproj
podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5 -chdir=infra/base init -backend=false   # y validate / test; igual con service y routes
podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5 fmt -check -recursive infra
bash scripts/tests/test-check-platform.sh && bash scripts/tests/test-buscar-listener.sh && bash scripts/tests/test-smoke.sh
podman run --rm -v "$PWD":/repo -w /repo docker.io/rhysd/actionlint:1.7.7
podman run --rm -v "$PWD":/mnt -w /mnt docker.io/koalaman/shellcheck:v0.10.0 scripts/*.sh scripts/tests/*.sh
```

## Documentación

- [docs/funcionamiento.md](docs/funcionamiento.md): estado real de la implementación, flujo de creación, dependencias externas y brechas conocidas.
- `/scalar/v1` (en Development): contrato OpenAPI generado desde las anotaciones del controlador.

## Desarrollo con SDD

Se desarrolla con desarrollo guiado por especificaciones (SDD) con un arnés con persistencia en disco: [.sdd/README.md](.sdd/README.md), protocolo en [.sdd/PROTOCOL.md](.sdd/PROTOCOL.md). Las features van en `specs/NNN-nombre/`.
