# Routes of the Usuarios service in the shared API Gateway (spec RF-019, plan D-010). Same integration pattern as
# infra/ejemplo-consumidor of Unal-Team-C/urban-alert-plataforma and infra/routes of urban-alert-geoespacial.
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

  # Public route -> path of the service. Reads are public (NONE); writes require a SigV4 signature of an AWS
  # identity allowed to execute-api:Invoke them (AWS_IAM). Explicit routes, no {proxy+}: a new endpoint is
  # never published without being listed here.
  routes = {
    list = {
      route_key = "GET /api/v1/usuarios"
      method    = "GET"
      path      = "/api/v1/users"
      auth      = "NONE"
    }
    get = {
      route_key = "GET /api/v1/reportes/{id}"
      method    = "GET"
      path      = "/api/v1/users/$request.path.id"
      auth      = "NONE"
    }
    create = {
      route_key = "POST /api/v1/usuarios"
      method    = "POST"
      path      = "/api/v1/users"
      auth      = "AWS_IAM"
    }
    update = {
      route_key = "PUT /api/v1/reportes/{id}"
      method    = "PUT"
      path      = "/api/v1/users/$request.path.id"
      auth      = "AWS_IAM"
    }
    update_role = {
      route_key = "PATCH /api/v1/reportes/{id}/role"
      method    = "PATCH"
      path      = "/api/v1/users/$request.path.id/role"
      auth      = "AWS_IAM"
    }
    delete = {
      route_key = "DELETE /api/v1/reportes/{id}"
      method    = "DELETE"
      path      = "/api/v1/users/$request.path.id"
      auth      = "AWS_IAM"
    }
  }
}

# The Express load balancer routes by Host: without overwriting Host with the service endpoint the request
# matches no listener rule and returns 404 (platform guide §4).
resource "aws_apigatewayv2_integration" "this" {
  for_each = local.routes

  api_id                 = local.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = each.value.method
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
  authorization_type = each.value.auth
  target             = "integrations/${aws_apigatewayv2_integration.this[each.key].id}"
}
