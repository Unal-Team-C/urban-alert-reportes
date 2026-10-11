region = "us-east-2"
name   = "urban-alert-reportes"

# Created by hand outside every repo (spec RF-012); only used, never managed here.
event_bus_arn = "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos"

db_name     = "urbanalert_reportes"
db_app_user = "reportes_app"
# 2: rotated after the first password appeared in the log of Provision run 38160461687 (never applied).
db_app_password_version     = "2"
db_engine_version           = "17"
db_instance_class           = "db.t4g.micro"
db_allocated_storage_gb     = 20
db_max_allocated_storage_gb = 50
db_backup_retention_days    = 7

ecr_keep_images    = 20
log_retention_days = 30

migrate_task_cpu    = 512
migrate_task_memory = 1024
