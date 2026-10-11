# Password of the application role (created by the migration task, plan D-003). Lives in the encrypted, private
# Terraform state. Alphanumeric only: it goes inside a Npgsql connection string without quoting.
resource "random_password" "app" {
  length  = 32
  special = false
}

locals {
  app_db = {
    username = var.db_app_user
    password = random_password.app.result
    host     = aws_db_instance.this.address
    port     = aws_db_instance.this.port
    dbname   = var.db_name
  }
}

# recovery_window_in_days = 0: deleted immediately on deprovision, so the next provision can recreate the name.
resource "aws_secretsmanager_secret" "app" {
  name                    = "${var.name}-db"
  description             = "Connection of ${var.name} to its database (application role, TLS required)"
  recovery_window_in_days = 0
}

resource "aws_secretsmanager_secret_version" "app" {
  secret_id = aws_secretsmanager_secret.app.id
  secret_string = jsonencode(merge(local.app_db, {
    # TLS required (plan D-010); pool sized by the connection budget (D-011); no GSS/Kerberos probe in the image.
    connection_string = join(";", [
      "Host=${local.app_db.host}",
      "Port=${local.app_db.port}",
      "Database=${local.app_db.dbname}",
      "Username=${local.app_db.username}",
      "Password=${local.app_db.password}",
      "SSL Mode=Require",
      "GSS Encryption Mode=Disable",
      "Maximum Pool Size=${var.db_pool_max}",
      "Minimum Pool Size=0",
    ])
  }))
}
