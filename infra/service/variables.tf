variable "region" {
  type = string
}

variable "state_bucket" {
  description = "Bucket holding the base stack state."
  type        = string
}

variable "image_tag" {
  description = "Image tag (commit SHA) to deploy."
  type        = string
}

variable "cpu" {
  description = "CPU units per task."
  type        = string
}

variable "memory" {
  description = "Memory per task, in MiB."
  type        = string
}

variable "container_port" {
  type = number
}

variable "health_check_path" {
  type = string
}

variable "min_tasks" {
  type = number
}

variable "max_tasks" {
  type = number
}

variable "cpu_target_percent" {
  type = number
}

variable "shutdown_timeout_seconds" {
  description = "Time to finish in-flight requests after SIGTERM; below the 30 s ECS stop timeout (plan D-009)."
  type        = number
  default     = 20

  validation {
    condition     = var.shutdown_timeout_seconds > 0 && var.shutdown_timeout_seconds < 30
    error_message = "shutdown_timeout_seconds must be between 1 and 29."
  }
}

variable "geoespacial_assign_path" {
  description = "Path of the internal Geoespacial route in the shared gateway (relative, no leading slash)."
  type        = string
  default     = "api/v1/geoespacial/coordinates"
}
