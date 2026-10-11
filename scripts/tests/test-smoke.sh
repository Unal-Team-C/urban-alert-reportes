#!/usr/bin/env bash
# Tests smoke-gateway-reportes.sh with a fake `curl` on the PATH that plays the gateway (no network needed).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
script="$here/../smoke-gateway-reportes.sh"
fake_bin="$(mktemp -d)"
trap 'rm -rf "$fake_bin"' EXIT

# FAKE_MODE: good (correct gateway), leaky (/health reaches the service), down (every call 503).
cat >"$fake_bin/curl" <<'FAKE'
#!/usr/bin/env bash
out="" url=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -w|--max-time) shift 2 ;;
    -s) shift ;;
    *) url="$1"; shift ;;
  esac
done
path="/${url#https://*/}"
respond() { printf '%s' "$2" >"$out"; printf '%s' "$1"; }
mode="${FAKE_MODE:-good}"
[[ "$mode" == "down" ]] && { respond 503 '{"message":"Service Unavailable"}'; exit 0; }
gateway404='{"message":"Not Found"}'
case "$path" in
  "/api/v1/reportes") respond 200 '{"elementos":[],"pagina":1,"tamanoPagina":20,"totalElementos":0}' ;;
  "/api/v1/reportes?pagina=1&tamanoPagina=1") respond 200 '{"elementos":[],"pagina":1,"tamanoPagina":1,"totalElementos":0}' ;;
  "/api/v1/reportes/catalogo") respond 200 '[{"codigo":"VIAS_Y_ANDENES","nombre":"Vías","tipos":[{"codigo":"HUECOS_EN_LA_VIA","nombre":"Huecos"}]}]' ;;
  "/api/v1/reportes/"*) respond 404 '{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}' ;;
  "/health")
    if [[ "$mode" == "leaky" ]]; then respond 200 'Healthy'; else respond 404 "$gateway404"; fi ;;
  *) respond 404 "$gateway404" ;;
esac
FAKE
chmod +x "$fake_bin/curl"

failures=0
check() {
  local name="$1" mode="$2" target="$3" expected_code="$4" expected_text="$5" output code
  set +e
  output="$(FAKE_MODE="$mode" SMOKE_ATTEMPTS=2 SMOKE_SLEEP=0 PATH="$fake_bin:$PATH" bash "$script" "$target" 2>&1)"
  code=$?
  set -e
  if [[ "$code" == "$expected_code" && "$output" == *"$expected_text"* ]]; then
    echo "ok   $name"
  else
    echo "FAIL $name (exit $code): $output"
    failures=$((failures + 1))
  fi
}

gw="https://api123.execute-api.us-east-2.amazonaws.com"
check "correct gateway passes" good "$gw" 0 "All smoke checks passed."
check "health published through the gateway fails" leaky "$gw" 1 "FAIL GET /health -> 200"
check "unavailable service fails" down "$gw" 1 "attempt 2: GET /api/v1/reportes returned 503"
check "bad argument is rejected" good "http://insecure" 2 "usage:"

exit "$failures"
