# terraform test with mocked providers and a fake base state: no AWS credentials, no real resources.
mock_provider "aws" {}

override_data {
  target = data.terraform_remote_state.base
  values = {
    outputs = {
      name                    = "urban-alert-reportes"
      cluster_name            = "urban-alert-reportes"
      execution_role_arn      = "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-execution"
      infrastructure_role_arn = "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-infrastructure"
      subnet_ids              = ["subnet-priv-a", "subnet-priv-b"]
      tasks_security_group_id = "sg-tasks"
      ecr_repository_url      = "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes"
      service_log_group       = "/ecs/urban-alert-reportes"
      app_secret_arn          = "arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-db-AbCdEf"
    }
  }
}

variables {
  image_tag = "0123456789abcdef"
}

run "container_matches_the_image_contract" {
  command = plan

  assert {
    condition     = aws_ecs_express_gateway_service.this.primary_container[0].container_port == 8080
    error_message = "The service listens on port 8080."
  }

  assert {
    condition     = aws_ecs_express_gateway_service.this.health_check_path == "/actuator/health"
    error_message = "The load balancer must check /actuator/health (includes the database)."
  }

  assert {
    condition     = aws_ecs_express_gateway_service.this.primary_container[0].image == "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes:0123456789abcdef"
    error_message = "The image must be the ECR repository tagged with the commit SHA."
  }

  assert {
    condition     = contains([for e in aws_ecs_express_gateway_service.this.primary_container[0].environment : "${e.name}=${e.value}"], "SPRING_PROFILES_ACTIVE=aws")
    error_message = "The service runs the aws profile (no Flyway, JSON logs, graceful shutdown)."
  }
}

run "runs_in_every_private_subnet_with_its_own_roles" {
  command = plan

  assert {
    condition     = toset(aws_ecs_express_gateway_service.this.network_configuration[0].subnets) == toset(["subnet-priv-a", "subnet-priv-b"])
    error_message = "Express must use every private subnet of the platform (platform guide §2, RF-018)."
  }

  assert {
    condition     = aws_ecs_express_gateway_service.this.network_configuration[0].security_groups == toset(["sg-tasks"])
    error_message = "Tasks must use the service's own security group (the only one RDS admits)."
  }

  assert {
    condition = (
      aws_ecs_express_gateway_service.this.execution_role_arn == "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-execution" &&
      aws_ecs_express_gateway_service.this.infrastructure_role_arn == "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-infrastructure"
    )
    error_message = "The service must use its own roles from the base stack (RF-012)."
  }
}

run "credentials_come_from_the_secret" {
  command = plan

  assert {
    condition = alltrue([
      for k in ["DB_URL=jdbc_url", "DB_USERNAME=username", "DB_PASSWORD=password"] :
      contains(
        [for s in aws_ecs_express_gateway_service.this.primary_container[0].secret : "${s.name}=${s.value_from}"],
        "${split("=", k)[0]}=arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-db-AbCdEf:${split("=", k)[1]}::"
      )
    ])
    error_message = "DB_URL, DB_USERNAME and DB_PASSWORD must be injected from the app secret (RF-001)."
  }

  assert {
    condition = alltrue([
      for e in aws_ecs_express_gateway_service.this.primary_container[0].environment :
      !contains(["DB_URL", "DB_USERNAME", "DB_PASSWORD"], e.name)
    ])
    error_message = "Database credentials must never be plain environment variables."
  }
}

run "connection_budget_within_50" {
  command = plan

  assert {
    condition     = var.db_pool_max * var.max_tasks <= 50
    error_message = "Default sizing must respect the 50-connection budget."
  }

  assert {
    condition     = contains([for e in aws_ecs_express_gateway_service.this.primary_container[0].environment : "${e.name}=${e.value}"], "DB_POOL_MAX=8")
    error_message = "Tasks must run with DB_POOL_MAX=8 (8 × 5 tasks = 40)."
  }
}

run "connection_budget_exceeded_is_rejected" {
  command = plan

  variables {
    db_pool_max = 11
    max_tasks   = 5
  }

  expect_failures = [aws_ecs_express_gateway_service.this]
}

run "endpoint_is_a_bare_host" {
  command = apply

  override_resource {
    target = aws_ecs_express_gateway_service.this
    values = {
      service_name  = "urban-alert-reportes"
      ingress_paths = [{ access_type = "PRIVATE", endpoint = "https://urban-alert-reportes-abc.ecs.us-east-2.on.aws/" }]
    }
  }

  assert {
    condition     = output.service_endpoint == "urban-alert-reportes-abc.ecs.us-east-2.on.aws"
    error_message = "service_endpoint must be the host only (no scheme, no trailing slash)."
  }
}
