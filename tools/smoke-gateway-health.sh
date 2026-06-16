#!/usr/bin/env bash
# Smoke real FairSpot service health through the local Envoy gateway.
#
# Requires:
#   - ./tools/start-local-harness.sh or equivalent services running
#   - local Envoy from code/infrastructure/docker-compose.yaml on :10000
#
# This replaces the old /api/whoami/ Dapr sample smoke route. It checks
# actual FairSpot service /health endpoints through the gateway.

set -euo pipefail

GATEWAY_URL="${FPS_GATEWAY_URL:-http://localhost:10000}"

SERVICES=(
  identity
  booking
  notification
  profile
  audit
  reporting
  configuration
  customer
  datahub
)

failures=0

echo "== FairSpot gateway health smoke =="
echo "Gateway: $GATEWAY_URL"

for service in "${SERVICES[@]}"; do
  url="$GATEWAY_URL/health/$service"
  status="$(curl -sf "$url" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNREACHABLE")"

  if [ "$status" = "Healthy" ]; then
    echo "  OK  $service health: $status"
  else
    echo "  ERR $service health: $status ($url)"
    failures=$((failures + 1))
  fi
done

if [ "$failures" -gt 0 ]; then
  echo "[gateway-health] FAILED: $failures service(s) not healthy through Envoy"
  exit 1
fi

echo "[gateway-health] OK"
