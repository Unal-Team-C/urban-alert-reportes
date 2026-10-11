# Recursos de arranque

Terraform no gestiona estos recursos: los necesita para poder ejecutarse, y el desaprovisionamiento de este repo no debe tocarlos (constitución P2). Reportes reutiliza los que ya existen y que otros repos documentan, y añade **una sola política pequeña**, versionada aquí (plan D-016).

Cuenta `348364742283`, región `us-east-2`.

## 1. Lo que se reutiliza

| Recurso | ID en AWS | Dónde está documentado y versionado |
|---|---|---|
| Proveedor OIDC de GitHub | `arn:aws:iam::348364742283:oidc-provider/token.actions.githubusercontent.com` | [urban-alert-geoespacial/infra/bootstrap](https://github.com/Unal-Team-C/urban-alert-geoespacial/tree/main/infra/bootstrap) |
| Rol de los workflows (`github-actions-…`) | ARN en el secreto `AWS_ROLE_ARN` | Confianza: `github-oidc-trust.json` de Geoespacial. Admite `repo:Unal-Team-C@335834608/*:ref:refs/heads/main`, así que cubre este repo sin cambios |
| Política `github-actions-urban-alertPolicy` | `arn:aws:iam::348364742283:policy/github-actions-urban-alertPolicy` | `github-deploy-policy.json` de Geoespacial: ECR, ECS Express, `iam:PassRole` sobre `role/urban-alert-*` hacia ECS, RDS, red, secretos `urban-alert-*` |
| Política `github-actions-urban-alert-terraform` | `arn:aws:iam::348364742283:policy/github-actions-urban-alert-terraform` | `github-terraform-policy.json` de Geoespacial: roles `urban-alert-*` (incluidas políticas en línea, con las que se da al rol de tarea `execute-api:Invoke` y `events:PutEvents`), logs `/ecs/urban-alert-*`, secretos `rds!*`, tareas ECS, KMS vía RDS |
| Política `github-actions-urban-alert-platform` | `arn:aws:iam::348364742283:policy/github-actions-urban-alert-platform` | [urban-alert-plataforma/infra/bootstrap](https://github.com/Unal-Team-C/urban-alert-plataforma/tree/main/infra/bootstrap): API Gateway `/apis/*`, SSM `/urban-alert/*`, `elasticloadbalancing:Describe*` |
| Bucket del estado | `urban-alert-tfstate-348364742283` | Geoespacial. Este repo usa las claves `reportes/base.tfstate`, `reportes/service.tfstate` y `reportes/routes.tfstate` |
| Bus de EventBridge `urban-alert-bus-eventos` | `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos` | Creado a mano; **ningún repo lo versiona**. Reportes solo publica en él |

## 2. Lo que añade este repo: política `github-actions-urban-alert-reportes`

Ninguna de las políticas anteriores tiene acciones `events:*`. Provision comprueba que el bus existe antes de cambiar nada (spec RF-022), y para eso el rol de los workflows necesita `events:DescribeEventBus` sobre el bus: [github-reportes-policy.json](github-reportes-policy.json). Nada más.

Un administrador de la cuenta la crea y la adjunta al rol de los workflows **una vez**:

```bash
aws iam create-policy --policy-name github-actions-urban-alert-reportes \
  --policy-document file://infra/bootstrap/github-reportes-policy.json
aws iam attach-role-policy --role-name <rol de AWS_ROLE_ARN> \
  --policy-arn arn:aws:iam::348364742283:policy/github-actions-urban-alert-reportes
```

Para cambiarla más adelante: editar el JSON en un commit y aplicarlo con `aws iam create-policy-version --policy-arn … --policy-document file://… --set-as-default` (máximo 5 versiones).

Sin ella, Provision falla en «Comprobar la plataforma y el bus» con `AccessDenied` y no modifica nada.

Si otro permiso falta en el primer aprovisionamiento, el mensaje `AccessDenied` indica la acción: se añade al JSON correspondiente **en el repo que lo versiona** y lo aplica un administrador.

## 3. Lo que hay que configurar en este repo (una vez)

Settings → Secrets and variables → Actions de `Unal-Team-C/urban-alert-reportes`:

| Tipo | Nombre | Valor de ejemplo |
|---|---|---|
| Secreto | `AWS_ROLE_ARN` | `arn:aws:iam::348364742283:role/github-actions-<nombre>` (el mismo que usan Geoespacial y Usuarios) |
| Variable | `AWS_REGION` | `us-east-2` |

```bash
gh secret set AWS_ROLE_ARN --repo Unal-Team-C/urban-alert-reportes      # pega el ARN cuando lo pida
gh variable set AWS_REGION --repo Unal-Team-C/urban-alert-reportes --body us-east-2
```

El resto de la configuración (nombres, tamaños, ARN del bus, bucket) está en `infra/*/terraform.tfvars` y `backend.tf`.

## 4. Requisitos previos en la cuenta

- La plataforma compartida aprovisionada (Provision de `urban-alert-plataforma`).
- Geoespacial y Multimedia aprovisionados (sus rutas en el gateway). Sin ellos Reportes despliega, pero la creación de reportes responde 503.
- El bus `urban-alert-bus-eventos` existe.
- No debe existir un repositorio ECR `urban-alert-reportes` creado a mano: lo crea Terraform.

```bash
aws ecr describe-repositories --repository-names urban-alert-reportes --region us-east-2   # debe responder RepositoryNotFoundException
aws events describe-event-bus --name urban-alert-bus-eventos --region us-east-2              # debe responder con su Arn
```
