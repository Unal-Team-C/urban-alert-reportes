# Own cluster (plan D-013): the migration task runs before the service exists, and nothing here depends on the
# clusters of other services.
resource "aws_ecs_cluster" "this" {
  name = var.name
}
