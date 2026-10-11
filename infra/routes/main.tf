# Routes of the Reportes service in the shared API Gateway (spec RF-021, plan D-012). Same integration pattern as
# infra/ejemplo-consumidor of Unal-Team-C/urban-alert-plataforma and infra/routes of urban-alert-usuarios.
data "aws_ssm_parameter" "api_id" {
  name = "/urban-alert/plataforma/api-id"
}

data "aws_ssm_parameter" "vpc_link_id" {
  name = "/urban-alert/plataforma/vpc-link-id"
}

data "aws_ssm_parameter" "api_endpoint" {
  name = "/urban-alert/plataforma/api-endpoint"
}

locals {
  # The provider marks SSM values as sensitive; these IDs are not secrets.
  api_id      = nonsensitive(data.aws_ssm_parameter.api_id.value)
  vpc_link_id = nonsensitive(data.aws_ssm_parameter.vpc_link_id.value)

  # Public route -> path of the service, as the local Nginx gateway (location /api/v1/reportes/ ->
  # /api/v1/Reportes/). Every endpoint of the service is public (spec §11): any method, no client authentication.
  # Both routes stay inside the service prefix; /health, /openapi and /scalar are outside it, so the gateway
  # answers 404 for them without reaching the service. The query string is forwarded unchanged.
  routes = {
    root = {
      route_key = "ANY /api/v1/reportes"
      path      = "/api/v1/Reportes"
    }
    proxy = {
      route_key = "ANY /api/v1/reportes/{proxy+}"
      path      = "/api/v1/Reportes/$request.path.proxy"
    }
  }
}

# The Express load balancer routes by Host: without overwriting Host with the service endpoint the request
# matches no listener rule and returns 404 (platform guide §4).
resource "aws_apigatewayv2_integration" "this" {
  for_each = local.routes

  api_id                 = local.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = var.express_listener_arn
  connection_type        = "VPC_LINK"
  connection_id          = local.vpc_link_id
  payload_format_version = "1.0"

  tls_config {
    server_name_to_verify = var.express_endpoint
  }

  request_parameters = {
    "overwrite:header.host" = var.express_endpoint
    "overwrite:path"        = each.value.path
  }
}

resource "aws_apigatewayv2_route" "this" {
  for_each = local.routes

  api_id             = local.api_id
  route_key          = each.value.route_key
  authorization_type = "NONE"
  target             = "integrations/${aws_apigatewayv2_integration.this[each.key].id}"
}
