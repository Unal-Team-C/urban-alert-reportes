#!/usr/bin/env bash
# Smoke test of the Reportes service through the shared API Gateway (spec 001-migracion-aws HU-6, RF-021;
# plan D-015). Read-only: it never creates, changes or deletes reports, and never emits audit events. The
# full creation flow (Geoespacial, Multimedia, EventBridge) is checked manually with scripts/crear-reporte-prueba.sh.
# Usage: scripts/smoke-gateway-reportes.sh https://<api-id>.execute-api.<region>.amazonaws.com
# Exit 0 when every check passes. SMOKE_ATTEMPTS (default 30, every SMOKE_SLEEP=10 s) bounds the wait of the
# first check.
set -euo pipefail

if [[ $# -ne 1 || "$1" != https://* ]]; then
  echo "usage: $0 https://<api-id>.execute-api.<region>.amazonaws.com" >&2
  exit 2
fi
gateway="${1%/}"
base="$gateway/api/v1/reportes"
body="$(mktemp)"
trap 'rm -f "$body"' EXIT
failures=0

request() { # url -> prints the status code, body in $body
  curl -s -o "$body" -w '%{http_code}' --max-time 15 "$1" || true
}

pass() { echo "ok   $1"; }
fail() {
  echo "FAIL $1: $(head -c 300 "$body")"
  failures=$((failures + 1))
}

# 1. Public list: 200 with a page. Retries while the new routes and tasks settle.
code=""
for attempt in $(seq 1 "${SMOKE_ATTEMPTS:-30}"); do
  code="$(request "$base")"
  [[ "$code" == "200" ]] && break
  echo "attempt $attempt: GET /api/v1/reportes returned ${code:-no answer}"
  sleep "${SMOKE_SLEEP:-10}"
done
if [[ "$code" == "200" ]] && jq -e '(.elementos | type == "array") and (.totalElementos | type == "number")' "$body" >/dev/null 2>&1; then
  pass "GET /api/v1/reportes -> 200 with a page"
else
  fail "GET /api/v1/reportes -> ${code:-no answer}, expected 200 with {elementos, totalElementos}"
fi

# 2. The query string reaches the service (only the path is rewritten).
code="$(request "$base?pagina=1&tamanoPagina=1")"
if [[ "$code" == "200" ]] && jq -e '.tamanoPagina == 1' "$body" >/dev/null 2>&1; then
  pass "GET /api/v1/reportes?tamanoPagina=1 -> page of size 1"
else
  fail "GET /api/v1/reportes?tamanoPagina=1 -> ${code:-no answer}, expected tamanoPagina 1"
fi

# 3. Catalogue through the {proxy+} route.
code="$(request "$base/catalogo")"
if [[ "$code" == "200" ]] && jq -e 'type == "array" and length > 0 and (.[0].tipos | length > 0)' "$body" >/dev/null 2>&1; then
  pass "GET /api/v1/reportes/catalogo -> 200"
else
  fail "GET /api/v1/reportes/catalogo -> ${code:-no answer}, expected the categories"
fi

# 4. Unknown id: the service itself answers 404 (problem details with status), not the gateway.
uuid="$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"
code="$(request "$base/$uuid")"
if [[ "$code" == "404" ]] && jq -e '.status == 404' "$body" >/dev/null 2>&1; then
  pass "GET /api/v1/reportes/{unknown} -> 404 from the service"
else
  fail "GET /api/v1/reportes/{unknown} -> ${code:-no answer}, expected the service's problem details with status 404"
fi

# 5. Paths outside the prefix stay unregistered: the gateway answers 404 without reaching the service.
for path in "/health" "/scalar/v1" "/openapi/v1.json" "/api/v1/reportes-x" "/api/v1/Reportes"; do
  code="$(request "$gateway$path")"
  if [[ "$code" == "404" ]] && ! jq -e '.status' "$body" >/dev/null 2>&1; then
    pass "GET $path -> 404 from the gateway"
  else
    fail "GET $path -> ${code:-no answer}, expected the gateway's 404"
  fi
done

if ((failures > 0)); then
  echo "$failures smoke check(s) failed"
  exit 1
fi
echo "All smoke checks passed."
