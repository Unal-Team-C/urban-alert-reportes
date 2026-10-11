#!/usr/bin/env bash
# Manual end-to-end check of report creation in AWS through the shared gateway (spec 001-migracion-aws HU-4,
# MS-002; plan D-015). Creates one report from JSON (Reportes -> Geoespacial, signed) and one with an image
# (Reportes -> Multimedia), checks idCoordenada and that urlImagen opens, and deletes both reports.
# Each creation publishes a ReporteCreado event to the EventBridge bus (MS-003: check the bus metrics).
# Not run by Provision: it writes data and emits events.
# Usage: scripts/crear-reporte-prueba.sh https://<api-id>.execute-api.<region>.amazonaws.com
set -euo pipefail

if [[ $# -ne 1 || "$1" != https://* ]]; then
  echo "usage: $0 https://<api-id>.execute-api.<region>.amazonaws.com" >&2
  exit 2
fi
base="${1%/}/api/v1/reportes"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0
created=()

pass() { echo "ok   $1"; }
fail() {
  echo "FAIL $1: $(head -c 300 "$work/body" 2>/dev/null)"
  failures=$((failures + 1))
}

# Inside the Bogotá range that Geoespacial accepts.
json='{"categoria":"VIAS_Y_ANDENES","tipo":"HUECOS_EN_LA_VIA","descripcion":"Prueba de despliegue en AWS (se borra al terminar)","latitud":4.6512,"longitud":-74.0561,"urlImagen":"https://example.com/prueba.jpg"}'
code="$(curl -s -o "$work/body" -w '%{http_code}' --max-time 30 -X POST -H 'Content-Type: application/json' -d "$json" "$base" || true)"
if [[ "$code" == "201" ]] && id_json="$(jq -er '.idReporte' "$work/body")"; then
  created+=("$id_json")
  pass "POST /api/v1/reportes (JSON) -> 201 ($id_json)"
else
  fail "POST /api/v1/reportes (JSON) -> ${code:-no answer}, expected 201"
fi

# 1x1 PNG accepted by Multimedia.
printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==' | base64 -d >"$work/prueba.png"
code="$(curl -s -o "$work/body" -w '%{http_code}' --max-time 30 -X POST \
  -F categoria=VIAS_Y_ANDENES -F tipo=HUECOS_EN_LA_VIA -F 'descripcion=Prueba de despliegue con imagen (se borra al terminar)' \
  -F latitud=4.6512 -F longitud=-74.0561 -F "imagen=@$work/prueba.png;type=image/png" "$base" || true)"
if [[ "$code" == "201" ]] && id_image="$(jq -er '.idReporte' "$work/body")"; then
  created+=("$id_image")
  pass "POST /api/v1/reportes (multipart) -> 201 ($id_image)"
else
  fail "POST /api/v1/reportes (multipart) -> ${code:-no answer}, expected 201"
fi

for id in "${created[@]}"; do
  code="$(curl -s -o "$work/body" -w '%{http_code}' --max-time 15 "$base/$id" || true)"
  if [[ "$code" == "200" ]] && jq -e '.idCoordenada != null and .idCoordenada != "00000000-0000-0000-0000-000000000000"' "$work/body" >/dev/null; then
    pass "GET /api/v1/reportes/$id -> idCoordenada $(jq -r .idCoordenada "$work/body")"
  else
    fail "GET /api/v1/reportes/$id -> ${code:-no answer}, expected a report with idCoordenada"
  fi
done

if [[ -n "${id_image:-}" ]]; then
  curl -s -o "$work/body" --max-time 15 "$base/$id_image" || true
  url="$(jq -r '.urlImagen // empty' "$work/body")"
  code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$url" || true)"
  if [[ -n "$url" && "$code" == "200" ]]; then pass "urlImagen opens ($url)"; else fail "urlImagen '$url' -> ${code:-no answer}, expected 200"; fi
fi

for id in "${created[@]}"; do
  code="$(curl -s -o "$work/body" -w '%{http_code}' --max-time 15 -X DELETE "$base/$id" || true)"
  if [[ "$code" == "204" ]]; then pass "DELETE /api/v1/reportes/$id -> 204"; else fail "DELETE /api/v1/reportes/$id -> ${code:-no answer}, expected 204"; fi
done

if ((failures > 0)); then
  echo "$failures check(s) failed"
  exit 1
fi
echo "Creation through Geoespacial and Multimedia works."
