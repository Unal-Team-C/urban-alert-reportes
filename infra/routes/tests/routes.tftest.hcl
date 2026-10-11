# terraform test with mocked providers and a fake platform contract: no AWS credentials, no real resources.
mock_provider "aws" {}

override_data {
  target = data.aws_ssm_parameter.api_id
  values = { value = "api123" }
}

override_data {
  target = data.aws_ssm_parameter.vpc_link_id
  values = { value = "vpclink123" }
}

override_data {
  target = data.aws_ssm_parameter.api_endpoint
  values = { value = "https://api123.execute-api.us-east-2.amazonaws.com" }
}

variables {
  express_endpoint     = "urban-alert-reportes-abc.ecs.us-east-2.on.aws"
  express_listener_arn = "arn:aws:elasticloadbalancing:us-east-2:348364742283:listener/app/ecs-express-gateway-alb-x/0123/4567"
}

run "exactly_the_two_routes_of_rf021" {
  command = plan

  assert {
    condition = toset([for r in aws_apigatewayv2_route.this : r.route_key]) == toset([
      "ANY /api/v1/reportes",
      "ANY /api/v1/reportes/{proxy+}",
    ])
    error_message = "Only the two routes of RF-021 may be registered (no $default, no health or docs)."
  }

  assert {
    condition = alltrue([for r in aws_apigatewayv2_route.this :
      split(" ", r.route_key)[1] == "/api/v1/reportes" || startswith(split(" ", r.route_key)[1], "/api/v1/reportes/")
    ])
    error_message = "Every route must stay under the service prefix /api/v1/reportes (constitution P9)."
  }

  assert {
    condition     = alltrue([for r in aws_apigatewayv2_route.this : r.api_id == "api123"])
    error_message = "Routes must go to the shared gateway read from SSM."
  }
}

run "every_route_is_public" {
  command = plan

  assert {
    condition     = alltrue([for r in aws_apigatewayv2_route.this : r.authorization_type == "NONE"])
    error_message = "Every Reportes route is public, as in the local gateway (clarification 2026-10-11)."
  }
}

run "integrations_reach_the_internal_express_alb" {
  command = plan

  assert {
    condition = alltrue([for i in aws_apigatewayv2_integration.this :
      i.integration_type == "HTTP_PROXY" && i.connection_type == "VPC_LINK" && i.connection_id == "vpclink123" &&
      i.payload_format_version == "1.0" && i.integration_uri == var.express_listener_arn
    ])
    error_message = "Every integration must be an HTTP proxy over the platform VPC link to the 443 listener, payload 1.0."
  }

  assert {
    condition = alltrue([for i in aws_apigatewayv2_integration.this :
      i.request_parameters["overwrite:header.host"] == var.express_endpoint && i.tls_config[0].server_name_to_verify == var.express_endpoint
    ])
    error_message = "Every integration must overwrite Host and verify TLS against the service endpoint (otherwise the ALB returns 404)."
  }

  assert {
    condition     = alltrue([for i in aws_apigatewayv2_integration.this : i.integration_method == "ANY"])
    error_message = "Each integration must forward the method of the request."
  }
}

run "public_paths_are_rewritten_to_the_service_paths" {
  command = plan

  assert {
    condition = (
      aws_apigatewayv2_integration.this["root"].request_parameters["overwrite:path"] == "/api/v1/Reportes" &&
      aws_apigatewayv2_integration.this["proxy"].request_parameters["overwrite:path"] == "/api/v1/Reportes/$request.path.proxy"
    )
    error_message = "/api/v1/reportes/* must be rewritten to /api/v1/Reportes/* (convention of UrbanAlert/infrastructure)."
  }

  assert {
    condition     = alltrue([for i in aws_apigatewayv2_integration.this : !contains(keys(i.request_parameters), "overwrite:querystring")])
    error_message = "The query string (pagina, estado, tipo...) must reach the service unchanged."
  }
}

run "gateway_url_is_published" {
  command = plan

  assert {
    condition     = output.gateway_url == "https://api123.execute-api.us-east-2.amazonaws.com"
    error_message = "gateway_url must be the platform endpoint from SSM."
  }
}

run "endpoint_with_scheme_is_rejected" {
  command = plan

  variables {
    express_endpoint = "https://urban-alert-reportes-abc.ecs.us-east-2.on.aws"
  }

  expect_failures = [var.express_endpoint]
}

run "listener_must_be_an_alb_listener" {
  command = plan

  variables {
    express_listener_arn = "arn:aws:elasticloadbalancing:us-east-2:348364742283:loadbalancer/app/x/0123"
  }

  expect_failures = [var.express_listener_arn]
}
