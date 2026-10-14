variable "region" {
  description = "AWS region."
  type        = string
}

variable "name" {
  description = "Prefix for every resource of this service. The pipeline's IAM permissions are scoped to urban-alert-*."
  type        = string

  validation {
    condition     = startswith(var.name, "urban-alert-reportes")
    error_message = "name must start with \"urban-alert-reportes\" (constitution P9)."
  }
}

variable "event_bus_arn" {
  description = "EventBridge bus that replaces RabbitMQ (created by hand, not managed by any repo). The task role may only publish to it."
  type        = string

  validation {
    condition     = can(regex("^arn:aws:events:[a-z0-9-]+:[0-9]{12}:event-bus/[A-Za-z0-9._-]+$", var.event_bus_arn))
    error_message = "event_bus_arn must be the ARN of an EventBridge bus."
  }
}

variable "geoespacial_assign_route" {
  description = "Internal Geoespacial route (AWS_IAM) the task role may invoke through the shared gateway: METHOD/path."
  type        = string
  default     = "POST/api/v1/geoespacial/coordinates"
}

variable "image_tag" {
  description = "Image tag (commit SHA) used by the migration task definition."
  type        = string
}

variable "allow_destroy" {
  description = "Only the deprovision workflow sets it: disables RDS deletion protection and lets ECR be deleted with its images."
  type        = bool
  default     = false
}

variable "final_snapshot_suffix" {
  description = "Suffix of the final RDS snapshot taken on destroy (the deprovision workflow passes its run id)."
  type        = string
  default     = "manual"
}

variable "db_name" {
  description = "Database created in the RDS instance."
  type        = string
}

variable "db_app_user" {
  description = "Login role used by the service: data access only, no DDL (created by the migration task)."
  type        = string
}

variable "db_app_password_version" {
  description = "Rotation counter of the application role password; change it to generate a new one."
  type        = string
}

variable "db_pool_max" {
  description = "Maximum connections per task (Npgsql pool). infra/service checks db_pool_max x max_tasks against the budget."
  type        = number
  default     = 8
}

variable "db_engine_version" {
  description = "PostgreSQL major version."
  type        = string
}

variable "db_instance_class" {
  type = string
}

variable "db_allocated_storage_gb" {
  type = number
}

variable "db_max_allocated_storage_gb" {
  type = number
}

variable "db_backup_retention_days" {
  type = number

  validation {
    condition     = var.db_backup_retention_days >= 7
    error_message = "Backups must be kept at least 7 days (RNF-007)."
  }
}

variable "ecr_keep_images" {
  description = "Number of images kept in ECR."
  type        = number
}

variable "log_retention_days" {
  type = number
}

variable "migrate_task_cpu" {
  type = number
}

variable "migrate_task_memory" {
  type = number
}

variable "container_port" {
  description = "Port the service container listens on (must match infra/service)."
  type        = number
  default     = 8080
}

variable "pausada" {
  description = "Only the Pausar workflow sets it to true: stops the RDS instance (data and storage are kept). Provision leaves it false, which starts a stopped instance before deploying."
  type        = bool
  default     = false
}
