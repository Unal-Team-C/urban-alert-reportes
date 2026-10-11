region       = "us-east-2"
state_bucket = "urban-alert-tfstate-348364742283"

cpu               = "512"
memory            = "1024"
container_port    = 8080
health_check_path = "/health"

min_tasks          = 1
max_tasks          = 5
cpu_target_percent = 60

