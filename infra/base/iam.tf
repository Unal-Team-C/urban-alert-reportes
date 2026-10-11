# Roles of this service only (plan D-004). The shared-looking urban-alert-ecs-execution-role and
# urban-alert-ecs-infrastructure-role belong to the state of urban-alert-geoespacial and are not used here.
# Names keep the urban-alert- prefix the pipeline's iam:CreateRole / iam:PassRole permissions are scoped to.
data "aws_caller_identity" "current" {}

locals {
  account_id = data.aws_caller_identity.current.account_id
  db_arn     = "arn:aws:rds:${var.region}:${local.account_id}:db:${var.name}-db"
}

resource "aws_iam_role" "execution" {
  name        = "${var.name}-ecs-execution"
  description = "ECS task execution role of ${var.name}: pull the image, write logs, read its own secrets"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "execution_managed" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Only this service's secrets: the application secret by name, the RDS-managed master secret (rds!db-…) by the
# tag RDS puts on it for this instance.
resource "aws_iam_role_policy" "execution_secrets" {
  name = "read-reportes-secrets"
  role = aws_iam_role.execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "ServiceSecrets"
        Effect   = "Allow"
        Action   = "secretsmanager:GetSecretValue"
        Resource = "arn:aws:secretsmanager:${var.region}:${local.account_id}:secret:${var.name}-*"
      },
      {
        Sid      = "RdsMasterSecret"
        Effect   = "Allow"
        Action   = "secretsmanager:GetSecretValue"
        Resource = "arn:aws:secretsmanager:${var.region}:${local.account_id}:secret:rds!*"
        Condition = {
          StringEquals = { "secretsmanager:ResourceTag/aws:rds:primaryDBInstanceArn" = local.db_arn }
        }
      }
    ]
  })
}

resource "aws_iam_role" "infrastructure" {
  name        = "${var.name}-ecs-infrastructure"
  description = "ECS Express infrastructure role of ${var.name}: load balancer rules, certificate and scaling"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "infrastructure_managed" {
  role       = aws_iam_role.infrastructure.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSInfrastructureRoleforExpressGatewayServices"
}

# Role of the running containers (plan D-004): the only AWS calls the service makes. Least privilege: invoke the
# one internal Geoespacial route through the shared gateway (signed with SigV4) and publish to the shared bus.
data "aws_ssm_parameter" "api_execution_arn" {
  name = "/urban-alert/plataforma/api-execution-arn"
}

resource "aws_iam_role" "task" {
  name        = "${var.name}-ecs-task"
  description = "ECS task role of ${var.name}: call Geoespacial through the gateway and publish to EventBridge"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
      Condition = {
        StringEquals = { "aws:SourceAccount" = local.account_id }
      }
    }]
  })
}

resource "aws_iam_role_policy" "task" {
  name = "reportes-runtime"
  role = aws_iam_role.task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "InvokeGeoespacialAssign"
        Effect   = "Allow"
        Action   = "execute-api:Invoke"
        Resource = "${nonsensitive(data.aws_ssm_parameter.api_execution_arn.value)}/$default/${var.geoespacial_assign_route}"
      },
      {
        Sid      = "PublishToSharedBus"
        Effect   = "Allow"
        Action   = "events:PutEvents"
        Resource = var.event_bus_arn
      }
    ]
  })
}
