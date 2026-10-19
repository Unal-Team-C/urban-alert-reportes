region       = "us-east-2"
state_bucket = "urban-alert-tfstate-348364742283"

cpu               = "512"
memory            = "1024"
container_port    = 8080
health_check_path = "/health"

min_tasks          = 1
max_tasks          = 5
cpu_target_percent = 60

# Human verification (spec 003). Enable only after the portal publishes the widget (urban-alert-web feature 004) and the
# bootstrap secret urban-alert-reportes-captcha exists; then set the site's CloudFront domain.
captcha_habilitado = false
captcha_hostnames  = []
