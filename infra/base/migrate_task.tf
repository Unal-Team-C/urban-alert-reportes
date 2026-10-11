# One-off task that applies the EF Core migrations and the application role ("API.dll --migrar") from inside the
# VPC, as the RDS master user, with the same image the service will run. Launched by scripts/run-schema-task.sh
# before every deployment (spec RF-008, RF-009; plan D-002, D-003).
locals {
  migrate_container = "migrate"
}

resource "aws_ecs_task_definition" "migrate" {
  family                   = "${var.name}-migrate"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.migrate_task_cpu
  memory                   = var.migrate_task_memory
  execution_role_arn       = aws_iam_role.execution.arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  container_definitions = jsonencode([{
    name      = local.migrate_container
    image     = "${aws_ecr_repository.this.repository_url}:${var.image_tag}"
    essential = true
    command   = ["dotnet", "API.dll", "--migrar"]

    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "Logging__Console__FormatterName", value = "json" },
      { name = "Migracion__Host", value = aws_db_instance.this.address },
      { name = "Migracion__Puerto", value = tostring(aws_db_instance.this.port) },
      { name = "Migracion__Base", value = var.db_name },
      { name = "Migracion__UsuarioApp", value = var.db_app_user },
    ]

    secrets = [
      { name = "Migracion__UsuarioAdmin", valueFrom = "${aws_db_instance.this.master_user_secret[0].secret_arn}:username::" },
      { name = "Migracion__ContrasenaAdmin", valueFrom = "${aws_db_instance.this.master_user_secret[0].secret_arn}:password::" },
      { name = "Migracion__ContrasenaApp", valueFrom = "${aws_secretsmanager_secret.app.arn}:password::" },
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.migrate.name
        awslogs-region        = var.region
        awslogs-stream-prefix = local.migrate_container
      }
    }
  }])

  # The secret value must exist before a task can read it.
  depends_on = [aws_secretsmanager_secret_version.app]
}
