# Inventario de recursos de AWS

Cuenta `348364742283`, región `us-east-2`. Spec 001-migracion-aws (RF-014, RF-015, RF-023).

## 1. Recursos creados por Terraform

Ninguno se importó: no existían antes del código. Los crea y destruye Terraform desde los workflows. Todos llevan el prefijo `urban-alert-reportes` y las etiquetas `Project=urban-alert`, `Service=reportes`, `ManagedBy=terraform`, `Stack=<stack>`.

| Stack (estado) | Recursos |
|---|---|
| `base` (`reportes/base.tfstate`) | Repositorio ECR `urban-alert-reportes` y su regla de ciclo de vida; roles IAM `urban-alert-reportes-ecs-execution` (con `AmazonECSTaskExecutionRolePolicy` y la política en línea de lectura de sus secretos), `urban-alert-reportes-ecs-infrastructure` (con `AmazonECSInfrastructureRoleforExpressGatewayServices`) y `urban-alert-reportes-ecs-task` (política en línea: `execute-api:Invoke` sobre `POST /api/v1/geoespacial/coordinates` y `events:PutEvents` sobre el bus); en la VPC compartida, grupos de seguridad `urban-alert-reportes-tasks` y `urban-alert-reportes-rds`; subnet group y RDS `urban-alert-reportes-db` (con su secreto `rds!…` gestionado por RDS); secreto `urban-alert-reportes-db`; clúster ECS `urban-alert-reportes`; grupos de logs `/ecs/urban-alert-reportes` y `/ecs/urban-alert-reportes-migrate`; task definition `urban-alert-reportes-migrate` |
| `service` (`reportes/service.tfstate`) | Servicio ECS Express `urban-alert-reportes` (con su regla en el balanceador **interno** de Express, certificado y escalado, que crea ECS con el rol de infraestructura) |
| `routes` (`reportes/routes.tfstate`) | En el API Gateway compartido: 2 integraciones HTTP_PROXY por el VPC Link de la plataforma y 2 rutas públicas: `ANY /api/v1/reportes` y `ANY /api/v1/reportes/{proxy+}` |

Fecha del primer aprovisionamiento: _pendiente_.

Al desaprovisionar queda la copia final de la base `urban-alert-reportes-final-<run_id>` (no la gestiona Terraform; borrarla a mano cuando ya no se necesite).

## 2. Recursos externos de arranque (no gestionados aquí)

Los usa este repo; no aparecen en su estado y el desaprovisionamiento no los toca. Detalle: [bootstrap/README.md](bootstrap/README.md).

| Recurso | ID en AWS |
|---|---|
| Proveedor OIDC de GitHub | `arn:aws:iam::348364742283:oidc-provider/token.actions.githubusercontent.com` |
| Rol de los workflows | ARN en el secreto `AWS_ROLE_ARN` de GitHub (`github-actions-…`) |
| Políticas del rol | `github-actions-urban-alertPolicy`, `github-actions-urban-alert-terraform`, `github-actions-urban-alert-platform` y, de este repo, `github-actions-urban-alert-reportes` ([github-reportes-policy.json](bootstrap/github-reportes-policy.json)) |
| Bucket del estado | `urban-alert-tfstate-348364742283` |

## 3. Recursos de otros repos o creados a mano (no gestionados aquí)

| Recurso | Dueño | Cómo lo usa Reportes |
|---|---|---|
| VPC, subredes privadas, NAT, endpoint S3 | `urban-alert-plataforma` | Lee `vpc-id`, `vpc-cidr` y `subredes-privadas` de SSM `/urban-alert/plataforma/` |
| API Gateway (HTTP API), stage, VPC Link | `urban-alert-plataforma` | Lee `api-id`, `api-endpoint`, `api-execution-arn`, `vpc-link-id`; registra solo sus rutas bajo `/api/v1/reportes` |
| Balanceador interno de ECS Express | ECS Express (compartido) | Express añade la regla por cabecera `Host` de Reportes |
| Ruta `POST /api/v1/geoespacial/coordinates` (`AWS_IAM`) | `urban-alert-geoespacial` | La invoca firmada (SigV4) con su rol de tarea |
| Ruta `POST /api/v1/multimedia/images` (pública) | `Urban-Alert-multimedia` | Sube las imágenes de los reportes |
| Bus de EventBridge `urban-alert-bus-eventos` | Creado a mano (ningún repo) | Publica `ReporteCreado`; Provision comprueba que existe |
| Roles `urban-alert-ecs-execution-role` y `urban-alert-ecs-infrastructure-role` | `urban-alert-geoespacial` | **No los usa** (tiene roles propios) |
| RDS, clústeres, ECR y grupos de seguridad de otros servicios | Cada servicio | No los usa |
