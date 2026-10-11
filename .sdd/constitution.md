# Constitución del proyecto

- **Versión**: 1.0.0
- **Última modificación**: 2026-10-11

> Principios no negociables. Todo `plan.md` se verifica contra ellos (§3 del plan).
> Una excepción exige justificación escrita en el plan. Cada principio debe ser concreto y verificable.

## Principios

### P1. Infraestructura solo como código, aplicada solo desde GitHub Actions
Todo recurso de AWS del servicio está definido en Terraform en este repositorio y solo lo crean, cambian o eliminan los workflows de `.github/workflows/` ejecutados en `main`. En local solo se ejecuta `init -backend=false`, `fmt`, `validate` y `test`; nunca `plan`/`apply`/`destroy` contra el estado real ni cambios en la consola (salvo los recursos de arranque, P2).
**Por qué**: un único camino de cambio hace el entorno repetible, revisable y reconstruible.

### P2. Recursos de arranque y recursos ajenos fuera de este estado
El proveedor OIDC, el rol de los workflows y sus políticas, y el bucket `urban-alert-tfstate-348364742283` son de arranque (documentados en `urban-alert-geoespacial/infra/bootstrap/`). La plataforma compartida (`urban-alert-plataforma`) y los recursos de otros microservicios (p. ej. los roles `urban-alert-ecs-*` que gestiona Geoespacial) y el bus de Amazon EventBridge que reemplaza a RabbitMQ (creado a mano, fuera de todo repo) nunca aparecen en el estado de este repo: se leen (SSM, `data`) o se usan, pero no se gestionan.
**Por qué**: si este repo los gestionara, su desaprovisionamiento rompería a los workflows o a otro servicio.

### P3. Sin secretos ni credenciales de larga duración
Los workflows se autentican solo con OIDC (`role-to-assume`). Ningún secreto aparece en el repositorio, en la imagen, en variables de GitHub, en salidas de Terraform ni en logs; los valores sensibles van a Secrets Manager y en Terraform se marcan `sensitive`.
**Por qué**: el repositorio y los logs de Actions los ve todo el equipo.

### P4. Cambios destructivos solo explícitos
Solo el workflow manual de desaprovisionamiento, con confirmación escrita, elimina el entorno; la base tiene protección contra borrado que solo ese workflow levanta, y deja copia final.
**Por qué**: un `destroy` accidental pierde los datos de reportes.

### P5. Coste mínimo y documentado
Se elige la opción más barata que cumpla la spec (proyecto académico, un entorno). Todo recurso con coste figura en la tabla de costes del README con su estimación mensual.
**Por qué**: la cuenta la paga el equipo.

### P6. Validación antes de cada commit y antes de cada apply
Antes de cada commit de tarea y como primer job de Provision pasan: `dotnet test` (unitarias e integración), `terraform fmt -check -recursive`, `validate` y `test` de cada stack (`-lockfile=readonly`), `actionlint` y `shellcheck`. En local se ejecutan con Podman cuando no hay herramienta instalada.
**Por qué**: los errores se detectan sin tocar AWS.

### P7. Pruebas de Terraform para lo que protege
Cada garantía de seguridad o aislamiento (base no pública, SG solo desde las tareas, protección contra borrado, presupuesto de conexiones, rutas registradas exactamente) tiene al menos un `run` en un `.tftest.hcl` con `mock_provider`.
**Por qué**: `validate` no comprueba intención.

### P8. Versiones fijadas y reproducibles
Terraform `>= 1.10, < 2.0` (CI y local con `hashicorp/terraform:1.16.5`), proveedor `hashicorp/aws ~> 6.0`, `.terraform.lock.hcl` versionado (hashes `linux_amd64`, `linux_arm64`, `darwin_arm64`); SDK .NET 10 fijado en `global.json` y paquetes NuGet con versión exacta; imágenes de herramientas y actions con versión fijada.
**Por qué**: el mismo commit produce el mismo resultado en local y en CI.

### P9. Contrato con la plataforma y nombres estables
El servicio consume la plataforma solo por su contrato SSM `/urban-alert/plataforma/*` y su guía `docs/integracion.md`; nunca copia IDs a mano. Todo recurso usa el prefijo `urban-alert-reportes` y las etiquetas `Project=urban-alert`, `Service=reportes`, `ManagedBy=terraform`, `Stack=<stack>`. Solo registra rutas bajo `/api/v1/reportes/`.
**Por qué**: la plataforma y el gateway son compartidos; salirse del contrato rompe a otros.

### P10. Ejecuciones serializadas e idempotentes
Provision y Deprovision comparten un grupo de `concurrency` sin cancelación; dos Provision seguidos sobre el mismo commit dan 0 cambios de infraestructura en el segundo.
**Por qué**: dos `apply` simultáneos o un plan que no converge corrompen el estado.

## Stack y restricciones técnicas

- .NET 10 + ASP.NET Core, EF Core (Npgsql) con migraciones versionadas, PostgreSQL; capas `Domain`, `Application`, `Infrastructure`, `API`.
- AWS, cuenta `348364742283`, región `us-east-2`, un entorno; ECS Express Mode (Fargate) en la red privada de la plataforma; RDS PostgreSQL propio del servicio.
- Estado remoto en `s3://urban-alert-tfstate-348364742283/reportes/`, `encrypt = true`, `use_lockfile = true`.
- El rol OIDC solo confía en `refs/heads/main` de los repos de `Unal-Team-C`.

## Calidad y pruebas

- Aplicación: `dotnet test UrbanAlert.Reportes.slnx` (unitarias + integración con Testcontainers; requiere Docker/Podman). Sin SDK local: imagen `mcr.microsoft.com/dotnet/sdk:10.0` por Podman.
- Terraform: `podman run --rm -v "$PWD":/w -w /w docker.io/hashicorp/terraform:1.16.5 -chdir=infra/<stack> init -backend=false`, luego `validate` y `test`; `... fmt -check -recursive infra`
- Workflows: `podman run --rm -v "$PWD":/repo -w /repo docker.io/rhysd/actionlint:1.7.7`
- Scripts: `podman run --rm -v "$PWD":/mnt -w /mnt docker.io/koalaman/shellcheck:v0.10.0 scripts/*.sh`

## Flujo de trabajo

- Desarrollo guiado por especificaciones según `.sdd/PROTOCOL.md`.
- Un commit local por tarea completada y verificada (P6); nunca `push` sin que el usuario lo pida.

## Historial

- 0.0.0 — plantilla inicial
- 1.0.0 — 2026-10-11 — P1–P10 adaptados de `urban-alert-plataforma` (constitución 1.0.1) y de la práctica de `urban-alert-geoespacial` (spec 002) y `urban-alert-usuarios` (spec 001); stack .NET
