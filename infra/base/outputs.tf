# Read by the workflows (scripts/run-schema-task.sh, docker push) and by the service stack (terraform_remote_state).
output "region" {
  value = var.region
}

output "name" {
  value = var.name
}

output "ecr_repository_url" {
  value = aws_ecr_repository.this.repository_url
}

output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "subnet_ids" {
  description = "All private subnets of the shared platform (Express needs every zone, platform guide §2)."
  value       = local.private_subnets
}

output "vpc_id" {
  value = local.vpc_id
}

output "tasks_security_group_id" {
  value = aws_security_group.tasks.id
}

output "execution_role_arn" {
  value = aws_iam_role.execution.arn
}

output "infrastructure_role_arn" {
  value = aws_iam_role.infrastructure.arn
}

output "task_role_arn" {
  value = aws_iam_role.task.arn
}

output "event_bus_arn" {
  value = var.event_bus_arn
}

output "db_pool_max" {
  value = var.db_pool_max
}

output "app_secret_arn" {
  value = aws_secretsmanager_secret.app.arn
}

output "service_log_group" {
  value = aws_cloudwatch_log_group.service.name
}

output "migrate_task_definition_arn" {
  value = aws_ecs_task_definition.migrate.arn
}

output "migrate_container_name" {
  value = local.migrate_container
}

output "migrate_log_group" {
  value = aws_cloudwatch_log_group.migrate.name
}
