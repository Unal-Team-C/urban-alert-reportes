data "terraform_remote_state" "base" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = "reportes/base.tfstate"
    region = var.region
  }
}

# URL of the shared gateway: Reportes calls Geoespacial and Multimedia only through it (platform guide §5).
data "aws_ssm_parameter" "api_endpoint" {
  name = "/urban-alert/plataforma/api-endpoint"
}

locals {
  base    = data.terraform_remote_state.base.outputs
  api_url = trimsuffix(nonsensitive(data.aws_ssm_parameter.api_endpoint.value), "/")
  environment = {
    ASPNETCORE_ENVIRONMENT          = "Production"
    Logging__Console__FormatterName = "json"
    Apagado__TimeoutSegundos        = tostring(var.shutdown_timeout_seconds)
    AWS_REGION                      = var.region

    # Geoespacial: internal route of the gateway, signed with the task role (spec RF-010).
    Geoespacial__BaseUrl          = local.api_url
    Geoespacial__RutaCoordenadas  = var.geoespacial_assign_path
    Geoespacial__FirmaIam__Region = var.region

    # Multimedia: public route of the gateway, plain multipart (spec RF-011).
    Multimedia__BaseUrl = local.api_url
    Multimedia__Modo    = "Gateway"

    # Events to the shared EventBridge bus instead of RabbitMQ (spec RF-012).
    Mensajeria__Transporte = "EventBridge"
    EventBridge__Bus       = local.base.event_bus_arn
  }
}

# ECS Express Mode adds this service's rule to the internal load balancer it shares in the VPC and manages the
# certificate and autoscaling with the infrastructure role. Private subnets only: no public URL, no public IP
# (spec RF-020). The schema is already applied by the migration task (outside Development the API never migrates).
resource "aws_ecs_express_gateway_service" "this" {
  service_name            = local.base.name
  cluster                 = local.base.cluster_name
  execution_role_arn      = local.base.execution_role_arn
  infrastructure_role_arn = local.base.infrastructure_role_arn
  task_role_arn           = local.base.task_role_arn
  cpu                     = var.cpu
  memory                  = var.memory
  health_check_path       = var.health_check_path
  wait_for_steady_state   = true

  network_configuration {
    subnets         = local.base.subnet_ids
    security_groups = [local.base.tasks_security_group_id]
  }

  primary_container {
    image          = "${local.base.ecr_repository_url}:${var.image_tag}"
    container_port = var.container_port

    aws_logs_configuration {
      log_group         = local.base.service_log_group
      log_stream_prefix = "service"
    }

    dynamic "environment" {
      for_each = local.environment

      content {
        name  = environment.key
        value = environment.value
      }
    }

    # Connection string of the application role (TLS, pool size) from Secrets Manager (spec RF-001).
    secret {
      name       = "ConnectionStrings__Postgres"
      value_from = "${local.base.app_secret_arn}:connection_string::"
    }
  }

  scaling_target {
    auto_scaling_metric       = "AVERAGE_CPU"
    auto_scaling_target_value = var.cpu_target_percent
    min_task_count            = var.min_tasks
    max_task_count            = var.max_tasks
  }

  lifecycle {
    precondition {
      condition     = local.base.db_pool_max * var.max_tasks <= 50
      error_message = "db_pool_max (infra/base) × max_tasks exceeds the 50-connection database budget (spec RNF-002, plan D-011)."
    }
  }
}
