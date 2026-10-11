# Bootstrap bucket shared by the UrbanAlert repos (infra/bootstrap/README.md).
terraform {
  backend "s3" {
    bucket       = "urban-alert-tfstate-348364742283"
    key          = "reportes/service.tfstate"
    region       = "us-east-2"
    encrypt      = true
    use_lockfile = true
  }
}
