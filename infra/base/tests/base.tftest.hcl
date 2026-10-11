# terraform test with mocked providers: no AWS credentials, no real resources (constitution P7).
mock_provider "aws" {
  mock_data "aws_caller_identity" {
    defaults = { account_id = "348364742283" }
  }

  mock_resource "aws_db_instance" {
    defaults = {
      address            = "urban-alert-reportes-db.example.us-east-2.rds.amazonaws.com"
      port               = 5432
      master_user_secret = [{ secret_arn = "arn:aws:secretsmanager:us-east-2:348364742283:secret:rds!db-mock", kms_key_id = "", secret_status = "active" }]
    }
  }

  mock_resource "aws_ecr_repository" {
    defaults = { repository_url = "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes" }
  }

  mock_resource "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::348364742283:role/urban-alert-reportes-mock" }
  }

  mock_resource "aws_secretsmanager_secret" {
    defaults = { arn = "arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-db-mock" }
  }

  mock_resource "aws_cloudwatch_log_group" {
    defaults = { arn = "arn:aws:logs:us-east-2:348364742283:log-group:/ecs/urban-alert-reportes" }
  }
}

mock_provider "random" {}

# Platform contract (SSM). StringList values are comma-separated.
override_data {
  target = data.aws_ssm_parameter.vpc_id
  values = { value = "vpc-plataforma" }
}

override_data {
  target = data.aws_ssm_parameter.vpc_cidr
  values = { value = "10.20.0.0/16" }
}

override_data {
  target = data.aws_ssm_parameter.private_subnets
  values = { value = "subnet-priv-a,subnet-priv-b" }
}

variables {
  image_tag = "0123456789abcdef"
}

run "defaults_protect_data" {
  command = apply

  assert {
    condition     = aws_db_instance.this.deletion_protection
    error_message = "RDS must have deletion protection unless allow_destroy is set (RNF-007, P4)."
  }

  assert {
    condition     = !aws_ecr_repository.this.force_delete
    error_message = "ECR must not be force-deletable unless allow_destroy is set."
  }

  assert {
    condition     = !aws_db_instance.this.skip_final_snapshot
    error_message = "A final snapshot must always be taken (RF-015)."
  }

  assert {
    condition     = aws_ecr_repository.this.image_tag_mutability == "IMMUTABLE"
    error_message = "Image tags (commit SHAs) must be immutable (RF-007)."
  }
}

run "own_database_is_private_encrypted_and_backed_up" {
  command = apply

  assert {
    condition     = aws_db_instance.this.identifier == "urban-alert-reportes-db" && aws_db_instance.this.engine == "postgres"
    error_message = "Usuarios must have its own PostgreSQL instance (HU-3)."
  }

  assert {
    condition     = !aws_db_instance.this.publicly_accessible
    error_message = "RDS must not be publicly accessible (RNF-001)."
  }

  assert {
    condition     = aws_db_instance.this.storage_encrypted
    error_message = "RDS storage must be encrypted."
  }

  assert {
    condition     = aws_db_instance.this.backup_retention_period >= 7
    error_message = "Backups must be kept at least 7 days (RNF-007)."
  }

  assert {
    condition     = aws_db_instance.this.manage_master_user_password
    error_message = "The master password must be managed by RDS in Secrets Manager."
  }
}

run "only_tasks_reach_the_database" {
  command = apply

  assert {
    condition     = aws_vpc_security_group_ingress_rule.rds_from_tasks.referenced_security_group_id == aws_security_group.tasks.id
    error_message = "The RDS ingress rule must reference the tasks security group."
  }

  assert {
    condition     = aws_vpc_security_group_ingress_rule.rds_from_tasks.cidr_ipv4 == null
    error_message = "The RDS ingress rule must not allow a CIDR range."
  }

  assert {
    condition     = aws_vpc_security_group_ingress_rule.rds_from_tasks.from_port == 5432 && aws_vpc_security_group_ingress_rule.rds_from_tasks.to_port == 5432
    error_message = "The RDS ingress rule must only open PostgreSQL."
  }
}

run "uses_the_shared_private_network" {
  command = apply

  assert {
    condition     = aws_security_group.tasks.vpc_id == "vpc-plataforma" && aws_security_group.rds.vpc_id == "vpc-plataforma"
    error_message = "Security groups must live in the platform VPC read from SSM (RF-018)."
  }

  assert {
    condition     = toset(aws_db_subnet_group.this.subnet_ids) == toset(["subnet-priv-a", "subnet-priv-b"])
    error_message = "RDS must use every private subnet of the platform."
  }

  assert {
    condition     = output.subnet_ids == tolist(["subnet-priv-a", "subnet-priv-b"])
    error_message = "The service and the migration task must get every private subnet."
  }
}

run "load_balancer_reaches_the_container_only_from_the_vpc" {
  command = apply

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.tasks_from_vpc.cidr_ipv4 == "10.20.0.0/16" &&
      aws_vpc_security_group_ingress_rule.tasks_from_vpc.from_port == 8080 &&
      aws_vpc_security_group_ingress_rule.tasks_from_vpc.to_port == 8080
    )
    error_message = "Tasks must accept the container port only from the shared VPC CIDR."
  }
}

run "roles_and_secrets_belong_to_this_service_only" {
  command = apply

  assert {
    condition = (
      aws_iam_role.execution.name == "urban-alert-reportes-ecs-execution" &&
      aws_iam_role.infrastructure.name == "urban-alert-reportes-ecs-infrastructure"
    )
    error_message = "Usuarios must use its own roles, never the urban-alert-ecs-* roles of Geoespacial (RF-012, P2)."
  }

  assert {
    condition = (
      jsondecode(aws_iam_role_policy.execution_secrets.policy).Statement[0].Resource ==
      "arn:aws:secretsmanager:us-east-2:348364742283:secret:urban-alert-reportes-*"
    )
    error_message = "The execution role may only read this service's secrets."
  }

  assert {
    condition = (
      jsondecode(aws_iam_role_policy.execution_secrets.policy).Statement[1].Condition.StringEquals["secretsmanager:ResourceTag/aws:rds:primaryDBInstanceArn"] ==
      "arn:aws:rds:us-east-2:348364742283:db:urban-alert-reportes-db"
    )
    error_message = "The master secret may only be read for this service's database."
  }
}

run "app_secret_requires_tls_and_is_recreatable" {
  command = apply

  assert {
    condition     = aws_secretsmanager_secret.app.recovery_window_in_days == 0
    error_message = "The app secret must be deleted immediately so it can be recreated after deprovisioning."
  }

  assert {
    condition     = endswith(jsondecode(aws_secretsmanager_secret_version.app.secret_string).jdbc_url, "/urbanalert_users?sslmode=require")
    error_message = "jdbc_url must require TLS (RF-002)."
  }

  assert {
    condition     = jsondecode(aws_secretsmanager_secret_version.app.secret_string).username == "users_app"
    error_message = "The service must connect with the application role, not the master user (RF-010)."
  }
}

run "migration_task_runs_flyway_as_master_with_the_commit_image" {
  command = apply

  assert {
    condition     = jsondecode(aws_ecs_task_definition.migrate.container_definitions)[0].image == "348364742283.dkr.ecr.us-east-2.amazonaws.com/urban-alert-reportes:0123456789abcdef"
    error_message = "The migration task must run the image of the commit being deployed."
  }

  assert {
    condition = contains(
      [for e in jsondecode(aws_ecs_task_definition.migrate.container_definitions)[0].environment : "${e.name}=${e.value}"],
      "SPRING_PROFILES_ACTIVE=aws,migrate"
    )
    error_message = "The migration task must use the aws,migrate profiles."
  }

  assert {
    condition = contains(
      [for s in jsondecode(aws_ecs_task_definition.migrate.container_definitions)[0].secrets : "${s.name}=${s.valueFrom}"],
      "DB_USERNAME=arn:aws:secretsmanager:us-east-2:348364742283:secret:rds!db-mock:username::"
    )
    error_message = "The migration task must connect as the RDS master user."
  }
}

run "allow_destroy_lifts_protection_and_names_the_snapshot" {
  command = apply

  variables {
    allow_destroy         = true
    final_snapshot_suffix = "123456"
  }

  assert {
    condition     = !aws_db_instance.this.deletion_protection
    error_message = "allow_destroy must disable deletion protection."
  }

  assert {
    condition     = aws_ecr_repository.this.force_delete
    error_message = "allow_destroy must let ECR be deleted with its images."
  }

  assert {
    condition     = aws_db_instance.this.final_snapshot_identifier == "urban-alert-reportes-final-123456"
    error_message = "The final snapshot must be named after the workflow run."
  }
}

run "name_must_keep_the_service_prefix" {
  command = plan

  variables {
    name = "urban-alert-geoespacial"
  }

  expect_failures = [var.name]
}

run "backups_below_seven_days_are_rejected" {
  command = plan

  variables {
    db_backup_retention_days = 3
  }

  expect_failures = [var.db_backup_retention_days]
}
