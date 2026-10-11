data "terraform_remote_state" "base" {
  backend = "s3"

  config = {
    bucket = var.state_bucket
    key    = "reportes/base.tfstate"
    region = var.region
  }
}

locals {
  base = data.terraform_remote_state.base.outputs
}

# ECS Express Mode adds this service's rule to the internal load balancer it shares in the VPC and manages the
# certificate and autoscaling with the infrastructure role. Private subnets only: no public URL, no public IP
# (spec RF-018). The schema is already applied by the migration task (profile "aws" has Flyway off).
resource "aws_ecs_express_gateway_service" "this" {
  service_name            = local.base.name
  cluster                 = local.base.cluster_name
  execution_role_arn      = local.base.execution_role_arn
  infrastructure_role_arn = local.base.infrastructure_role_arn
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

    environment {
      name  = "SPRING_PROFILES_ACTIVE"
      value = "aws"
    }

    environment {
      name  = "DB_POOL_MIN"
      value = tostring(var.db_pool_min)
    }

    environment {
      name  = "DB_POOL_MAX"
      value = tostring(var.db_pool_max)
    }

    secret {
      name       = "DB_URL"
      value_from = "${local.base.app_secret_arn}:jdbc_url::"
    }

    secret {
      name       = "DB_USERNAME"
      value_from = "${local.base.app_secret_arn}:username::"
    }

    secret {
      name       = "DB_PASSWORD"
      value_from = "${local.base.app_secret_arn}:password::"
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
      condition     = var.db_pool_max * var.max_tasks <= 50
      error_message = "db_pool_max × max_tasks exceeds the 50-connection database budget (spec RNF-002, plan D-009)."
    }
  }
}
