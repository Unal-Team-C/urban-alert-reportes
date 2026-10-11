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
      task_role_arn           = "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-task"
      subnet_ids              = ["subnet-priv-a", "subnet-priv-b"]
      tasks_security_group_id = "sg-tasks"
      ecr_repository_url      = "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes"
      service_log_group       = "/ecs/urban-alert-reportes"
      app_secret_arn          = "arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-db-AbCdEf"
      event_bus_arn           = "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos"
      db_pool_max             = 8
    }
  }
}

override_data {
  target = data.aws_ssm_parameter.api_endpoint
  values = { value = "https://api123.execute-api.us-east-2.amazonaws.com" }
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
    condition     = aws_ecs_express_gateway_service.this.health_check_path == "/health"
    error_message = "The load balancer must check /health (includes the database)."
  }

  assert {
    condition     = aws_ecs_express_gateway_service.this.primary_container[0].image == "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes:0123456789abcdef"
    error_message = "The image must be the ECR repository tagged with the commit SHA."
  }
}

run "cloud_configuration_of_the_service" {
  command = plan

  assert {
    condition = length(setsubtract([
      "ASPNETCORE_ENVIRONMENT=Production",
      "Logging__Console__FormatterName=json",
      "Apagado__TimeoutSegundos=20",
      "AWS_REGION=us-east-2",
      "Geoespacial__BaseUrl=https://api123.execute-api.us-east-2.amazonaws.com",
      "Geoespacial__RutaCoordenadas=api/v1/geoespacial/coordinates",
      "Geoespacial__FirmaIam__Region=us-east-2",
      "Multimedia__BaseUrl=https://api123.execute-api.us-east-2.amazonaws.com",
      "Multimedia__Modo=Gateway",
      "Mensajeria__Transporte=EventBridge",
      "EventBridge__Bus=arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos",
    ], [for e in aws_ecs_express_gateway_service.this.primary_container[0].environment : "${e.name}=${e.value}"])) == 0
    error_message = "The service must get the cloud configuration of plan §7 (gateway, signing, EventBridge, JSON logs)."
  }
}

run "runs_in_every_private_subnet_with_its_own_roles" {
  command = plan

  assert {
    condition     = toset(aws_ecs_express_gateway_service.this.network_configuration[0].subnets) == toset(["subnet-priv-a", "subnet-priv-b"])
    error_message = "Express must use every private subnet of the platform (platform guide §2, RF-020)."
  }

  assert {
    condition     = aws_ecs_express_gateway_service.this.network_configuration[0].security_groups == toset(["sg-tasks"])
    error_message = "Tasks must use the service's own security group (the only one RDS admits)."
  }

  assert {
    condition = (
      aws_ecs_express_gateway_service.this.execution_role_arn == "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-execution" &&
      aws_ecs_express_gateway_service.this.infrastructure_role_arn == "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-infrastructure" &&
      aws_ecs_express_gateway_service.this.task_role_arn == "arn:aws:iam::348364742283:role/urban-alert-reportes-ecs-task"
    )
    error_message = "The service must use its own roles from the base stack, including the task role (RF-010, RF-012, RF-015)."
  }
}

run "credentials_come_from_the_secret" {
  command = plan

  assert {
    condition = contains(
      [for s in aws_ecs_express_gateway_service.this.primary_container[0].secret : "${s.name}=${s.value_from}"],
      "ConnectionStrings__Postgres=arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-db-AbCdEf:connection_string::"
    )
    error_message = "The connection string must be injected from the app secret (RF-001)."
  }

  assert {
    condition = alltrue([
      for e in aws_ecs_express_gateway_service.this.primary_container[0].environment :
      !startswith(e.name, "ConnectionStrings__") && !strcontains(lower(e.value), "password")
    ])
    error_message = "Database credentials must never be plain environment variables."
  }
}

run "connection_budget_exceeded_is_rejected" {
  command = plan

  variables {
    max_tasks = 7
  }

  expect_failures = [aws_ecs_express_gateway_service.this]
}

run "shutdown_timeout_must_fit_the_ecs_stop_timeout" {
  command = plan

  variables {
    shutdown_timeout_seconds = 30
  }

  expect_failures = [var.shutdown_timeout_seconds]
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
