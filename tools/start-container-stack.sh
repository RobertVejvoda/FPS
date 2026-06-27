#!/usr/bin/env bash
# tools/start-container-stack.sh — Container-only FairSpot stack start and smoke.
#
# Requires: Docker Engine 24+ and Docker Compose v2 plugin only.
# No host .NET SDK, Dapr CLI, or dapr run command is needed or used.
#
# Usage (local-container — dev credential defaults, no env file needed):
#   ./tools/start-container-stack.sh
#
# Usage (NAS/hosted — real credentials enforced via .env):
#   ./tools/start-container-stack.sh --nas [--env-file /path/to/.env]
#
# Flags:
#   --nas              Apply NAS overlay (restart policies + required credential check).
#   --env-file PATH    Env file for --nas mode. Default: code/infrastructure/.env
#   --skip-smoke       Bring up the stack only; skip post-start health checks.
#   --seed             After services are healthy, configure Keycloak and seed
#                      demo + Green Logistics data, then run the local E2E smoke
#                      (booking -> notification -> audit) to validate pub/sub and
#                      workflow. Uses HTTP-only helper scripts; no host .NET/Dapr.
#   --domain DOMAIN    After local checks pass, probe https://app.DOMAIN and
#                      https://auth.DOMAIN (requires Cloudflare Tunnel to be running).
#   --down             Tear down the stack (same compose files) and exit.
#
# Exit codes:
#   0  Stack running and all smoke checks passed.
#   1  Prerequisite missing, env file absent, or one or more health checks failed.

set -euo pipefail

# ── Repo and infra paths ────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

# ── Argument parsing ────────────────────────────────────────────────────────────

MODE="local"
ENV_FILE="$INFRA_DIR/.env"
SKIP_SMOKE=false
TEARDOWN=false
PUBLIC_DOMAIN=""
SEED=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --nas)        MODE="nas" ;;
    --env-file)   ENV_FILE="$2"; shift ;;
    --skip-smoke) SKIP_SMOKE=true ;;
    --seed)       SEED=true ;;
    --down)       TEARDOWN=true ;;
    --domain)     PUBLIC_DOMAIN="$2"; shift ;;
    *) echo "Unknown flag: $1"; exit 1 ;;
  esac
  shift
done

# ── Helpers ─────────────────────────────────────────────────────────────────────

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m'

ok()   { printf "  ${GREEN}OK${NC}    %s\n" "$*"; }
err()  { printf "  ${RED}FAIL${NC}  %s\n" "$*"; }
info() { printf "  ${YELLOW}INFO${NC}  %s\n" "$*"; }
hdr()  { printf "\n=== %s ===\n" "$*"; }

FAILURES=0
fail() {
  err "$1"
  FAILURES=$((FAILURES + 1))
}

# ── Build compose command ───────────────────────────────────────────────────────

COMPOSE_FILES=(
  "-f" "$INFRA_DIR/docker-compose.yaml"
  "-f" "$INFRA_DIR/docker-compose.services.yml"
  "-f" "$INFRA_DIR/docker-compose.dapr.yml"
)

if [[ "$MODE" == "nas" ]]; then
  COMPOSE_FILES+=("-f" "$INFRA_DIR/docker-compose.nas.yml")
fi

COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" "${COMPOSE_FILES[@]}")
if [[ "$MODE" == "nas" ]]; then
  COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" "${COMPOSE_FILES[@]}")
fi

# Human-readable compose command shown in log messages (no secrets).
COMPOSE_HUMAN="docker compose --project-directory code/infrastructure"
if [[ "$MODE" == "nas" ]]; then
  COMPOSE_HUMAN+=" --env-file code/infrastructure/.env"
fi
COMPOSE_HUMAN+=" -f docker-compose.yaml -f docker-compose.services.yml -f docker-compose.dapr.yml"
if [[ "$MODE" == "nas" ]]; then
  COMPOSE_HUMAN+=" -f docker-compose.nas.yml"
fi

# ── Prerequisites ────────────────────────────────────────────────────────────────

hdr "Prerequisites"

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: 'docker' not found. Install Docker Engine 24+ and try again."
  exit 1
fi
ok "docker: $(docker --version | head -1)"

if ! docker compose version >/dev/null 2>&1; then
  echo "ERROR: 'docker compose' plugin not found. Install Docker Compose v2 and try again."
  exit 1
fi
ok "docker compose: $(docker compose version | head -1)"

if [[ "$MODE" == "nas" ]]; then
  if [[ ! -f "$ENV_FILE" ]]; then
    echo "ERROR: --nas mode requires an env file at: $ENV_FILE"
    echo "Copy the template and fill in all values:"
    echo "  cp code/infrastructure/.env.example code/infrastructure/.env"
    exit 1
  fi
  ok "env file: $ENV_FILE (exists — values not printed)"
fi

# ── Teardown ─────────────────────────────────────────────────────────────────────

if [[ "$TEARDOWN" == "true" ]]; then
  hdr "Tearing down"
  echo "Stopping containers (volumes preserved)..."
  "${COMPOSE_CMD[@]}" down
  echo "Done. Data volumes are intact. To remove data volumes too: docker volume rm \$(docker volume ls -q | grep fps)"
  exit 0
fi

# ── External network ─────────────────────────────────────────────────────────────

hdr "Docker network"

if ! docker network inspect fps_network >/dev/null 2>&1; then
  echo "Creating fps_network..."
  docker network create fps_network
  ok "fps_network created"
else
  ok "fps_network exists"
fi

# ── Start the stack ──────────────────────────────────────────────────────────────

hdr "Starting stack ($MODE mode)"
echo "Command: $COMPOSE_HUMAN up -d"
echo

"${COMPOSE_CMD[@]}" up -d

# ── Wait for infrastructure health ───────────────────────────────────────────────

hdr "Waiting for infrastructure health"

INFRA_HEALTH_TIMEOUT=120
INFRA_HEALTH_INTERVAL=5

_wait_healthy() {
  local service="$1"
  local timeout="$2"
  local elapsed=0
  printf "  Waiting for %s to be healthy" "$service"
  while [[ $elapsed -lt $timeout ]]; do
    local state
    state=$("${COMPOSE_CMD[@]}" ps --format json 2>/dev/null \
      | python3 -c "
import sys,json
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    try:
        d=json.loads(line)
    except Exception:
        continue
    if d.get('Service')=='$service' or d.get('Name','').endswith('$service'):
        # Health is '' for services without a healthcheck; fall back to State.
        print(d.get('Health') or d.get('State',''))
        break
" 2>/dev/null || echo "")
    if [[ "$state" == "healthy" || "$state" == "running" ]]; then
      printf " — OK\n"
      return 0
    fi
    printf "."
    sleep "$INFRA_HEALTH_INTERVAL"
    elapsed=$((elapsed + INFRA_HEALTH_INTERVAL))
  done
  printf " — TIMEOUT\n"
  return 1
}

for svc in vault rabbitmq mongodb postgres; do
  if ! _wait_healthy "$svc" "$INFRA_HEALTH_TIMEOUT"; then
    fail "$svc did not become healthy within ${INFRA_HEALTH_TIMEOUT}s"
    echo "    Logs: $COMPOSE_HUMAN logs $svc"
    echo "    Rerun: $COMPOSE_HUMAN up -d $svc"
  fi
done

if [[ $FAILURES -gt 0 ]]; then
  echo
  echo "Infrastructure services failed. Resolve before continuing."
  exit 1
fi

# ── Wait for vault-init to complete ──────────────────────────────────────────────

hdr "Vault secret seed"

VAULT_INIT_TIMEOUT=60
VAULT_INIT_ELAPSED=0
printf "  Waiting for vault-init to complete"
VAULT_INIT_STATUS=""
while [[ $VAULT_INIT_ELAPSED -lt $VAULT_INIT_TIMEOUT ]]; do
  VAULT_INIT_STATUS=$("${COMPOSE_CMD[@]}" ps --format json 2>/dev/null \
    | python3 -c "
import sys,json
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    try:
        d=json.loads(line)
    except Exception:
        continue
    svc=d.get('Service','')
    if 'vault-init' in svc:
        print(d.get('State','') + ':' + str(d.get('ExitCode','')))
        break
" 2>/dev/null || echo "")
  state="${VAULT_INIT_STATUS%%:*}"
  code="${VAULT_INIT_STATUS##*:}"
  if [[ "$state" == "exited" && "$code" == "0" ]]; then
    printf " — OK\n"
    ok "Vault secrets seeded (vault-init exited 0)"
    break
  elif [[ "$state" == "exited" && "$code" != "0" && "$code" != "" ]]; then
    printf " — FAILED\n"
    fail "vault-init exited with code $code — Vault secrets were not seeded"
    echo "    Logs: $COMPOSE_HUMAN logs vault-init"
    echo "    Rerun: $COMPOSE_HUMAN up --force-recreate vault-init"
    break
  fi
  printf "."
  sleep 5
  VAULT_INIT_ELAPSED=$((VAULT_INIT_ELAPSED + 5))
done

if [[ $VAULT_INIT_ELAPSED -ge $VAULT_INIT_TIMEOUT && "$state" != "exited" ]]; then
  printf " — TIMEOUT\n"
  fail "vault-init did not complete within ${VAULT_INIT_TIMEOUT}s"
  echo "    Logs: $COMPOSE_HUMAN logs vault-init"
fi

if [[ $FAILURES -gt 0 ]]; then
  echo
  echo "Vault seed failed. Dapr sidecars cannot read secrets. Resolve before continuing."
  exit 1
fi

# ── Wait for app services to start ───────────────────────────────────────────────

hdr "App service readiness"

APP_SERVICES=(
  "fps-booking:5131"
  "fps-identity:5192"
  "fps-profile:5197"
  "fps-notification:5157"
  "fps-audit:5161"
  "fps-reporting:5171"
  "fps-configuration:5141"
  "fps-customer:5181"
  "fps-datahub:5211"
)

APP_TIMEOUT=120
APP_INTERVAL=5

for spec in "${APP_SERVICES[@]}"; do
  svc="${spec%%:*}"
  port="${spec##*:}"
  elapsed=0
  printf "  Waiting for %s :%s" "$svc" "$port"
  ready=false
  while [[ $elapsed -lt $APP_TIMEOUT ]]; do
    if curl -sf "http://localhost:$port/health" >/dev/null 2>&1; then
      ready=true
      break
    fi
    printf "."
    sleep "$APP_INTERVAL"
    elapsed=$((elapsed + APP_INTERVAL))
  done
  if [[ "$ready" == "true" ]]; then
    printf " — OK\n"
  else
    printf " — TIMEOUT\n"
    fail "$svc did not respond on :$port within ${APP_TIMEOUT}s"
    echo "    Logs:  $COMPOSE_HUMAN logs $svc"
    echo "    Rerun: $COMPOSE_HUMAN up -d $svc"
  fi
done

if [[ $FAILURES -gt 0 ]]; then
  echo
  echo "One or more app services failed to start. Resolve before running smoke."
  exit 1
fi

# ── Dapr sidecar check ────────────────────────────────────────────────────────────

hdr "Dapr sidecar status"

SIDECAR_SERVICES=(
  fps-audit-dapr
  fps-booking-dapr
  fps-configuration-dapr
  fps-customer-dapr
  fps-datahub-dapr
  fps-identity-dapr
  fps-notification-dapr
  fps-profile-dapr
  fps-reporting-dapr
)

DAPR_TIMEOUT=60
DAPR_INTERVAL=5

for svc in "${SIDECAR_SERVICES[@]}"; do
  elapsed=0
  printf "  Waiting for %s" "$svc"
  ready=false
  while [[ $elapsed -lt $DAPR_TIMEOUT ]]; do
    state=$("${COMPOSE_CMD[@]}" ps --format json 2>/dev/null \
      | python3 -c "
import sys,json
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    try:
        d=json.loads(line)
    except Exception:
        continue
    if d.get('Service')=='$svc':
        print(d.get('State',''))
        break
" 2>/dev/null || echo "")
    if [[ "$state" == "running" ]]; then
      ready=true
      break
    fi
    printf "."
    sleep "$DAPR_INTERVAL"
    elapsed=$((elapsed + DAPR_INTERVAL))
  done
  if [[ "$ready" == "true" ]]; then
    printf " — running\n"
  else
    printf " — NOT RUNNING\n"
    fail "$svc is not running (state: ${state:-unknown})"
    echo "    Logs:  $COMPOSE_HUMAN logs $svc"
    echo "    Rerun: $COMPOSE_HUMAN up -d $svc"
  fi
done

# ── Seed demo + Green Logistics data (optional) ──────────────────────────────────

if [[ "$SEED" == "true" ]]; then
  hdr "Seeding Keycloak + demo data"

  if [[ $FAILURES -gt 0 ]]; then
    echo "Skipping seed — services above are not healthy. Resolve first."
    exit 1
  fi

  echo "Configuring Keycloak realm and demo/Green Logistics users..."
  if "$REPO_ROOT/tools/dev-setup-auth.sh"; then
    ok "Keycloak realm + users configured (dev-setup-auth.sh)"
  else
    fail "dev-setup-auth.sh failed — Keycloak realm/users not configured"
    echo "    Logs:  $COMPOSE_HUMAN logs keycloak"
    echo "    Rerun: ./tools/dev-setup-auth.sh"
    exit 1
  fi

  echo "Seeding profiles, vehicles, bookings, and running a Draw..."
  if "$REPO_ROOT/tools/dev-seed.sh"; then
    ok "Demo + Green Logistics data seeded (dev-seed.sh)"
  else
    fail "dev-seed.sh failed — demo data not seeded"
    echo "    App logs: $COMPOSE_HUMAN logs fps-booking fps-profile fps-identity"
    echo "    Rerun:    ./tools/dev-seed.sh"
    exit 1
  fi
fi

# ── Skip smoke if requested ───────────────────────────────────────────────────────

if [[ "$SKIP_SMOKE" == "true" ]]; then
  echo
  info "Smoke checks skipped (--skip-smoke). Stack is up."
  if [[ $FAILURES -gt 0 ]]; then
    echo "$FAILURES container(s) had issues — review output above."
    exit 1
  fi
  exit 0
fi

# ── Local container smoke ─────────────────────────────────────────────────────────

hdr "Local container smoke"

echo "Keycloak OIDC discovery..."
KC_URL="http://localhost:8180/realms/fps-local/.well-known/openid-configuration"
KC_RESP=$(curl -sf "$KC_URL" 2>/dev/null || echo "")
if [[ -n "$KC_RESP" ]]; then
  ISS=$(python3 -c "import sys,json; print(json.load(sys.stdin).get('issuer','?'))" <<< "$KC_RESP" 2>/dev/null || echo "?")
  ok "Keycloak OIDC discovery (issuer: $ISS)"
else
  fail "Keycloak OIDC discovery unreachable at $KC_URL"
  echo "    Logs:  $COMPOSE_HUMAN logs keycloak"
  echo "    Rerun: $COMPOSE_HUMAN up -d keycloak"
fi

echo
echo "Service health via Envoy gateway (http://localhost:10000)..."

GATEWAY="http://localhost:10000"
GATEWAY_SERVICES=(identity booking notification profile audit reporting configuration customer datahub)

for svc in "${GATEWAY_SERVICES[@]}"; do
  url="$GATEWAY/health/$svc"
  resp=$(curl -sf "$url" 2>/dev/null || echo "")
  status=$(python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" <<< "$resp" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$status" == "Healthy" ]]; then
    ok "$svc via gateway: $status"
  else
    fail "$svc via gateway: $status ($url)"
    echo "    App logs:   $COMPOSE_HUMAN logs fps-$svc"
    echo "    Envoy logs: $COMPOSE_HUMAN logs envoy-proxy"
    echo "    Rerun:      $COMPOSE_HUMAN up -d fps-$svc fps-${svc}-dapr"
  fi
done

# ── Local E2E smoke (only when data was seeded) ──────────────────────────────────
# Proves state stores, pub/sub, and workflow: a booking submitted through the
# gateway must produce notification + audit records via Dapr events.

if [[ "$SEED" == "true" && $FAILURES -eq 0 ]]; then
  echo
  echo "Running local E2E smoke (booking -> notification -> audit)..."
  if APP_URL="$GATEWAY" AUTH_URL="http://localhost:8180" OIDC_REALM="fps-local" \
       "$REPO_ROOT/tools/smoke-hosted.sh"; then
    ok "Local E2E smoke passed (seeded data, pub/sub, and workflow verified)"
  else
    fail "Local E2E smoke failed — see smoke-evidence-*.txt and output above"
    echo "    Pub/sub or workflow may not be wired. Check Dapr sidecar logs:"
    echo "    $COMPOSE_HUMAN logs fps-booking-dapr fps-notification-dapr"
  fi
fi

# ── Public-domain smoke (optional) ────────────────────────────────────────────────

if [[ -n "$PUBLIC_DOMAIN" ]]; then
  hdr "Public-domain smoke (https://$PUBLIC_DOMAIN)"

  APP_URL="https://app.$PUBLIC_DOMAIN"
  AUTH_URL="https://auth.$PUBLIC_DOMAIN"
  REALM="fairspot"

  echo "Cloudflare tunnel connectivity..."
  APP_STATUS=$(curl -o /dev/null -sw "%{http_code}" "$APP_URL/health/identity" 2>/dev/null || echo "000")
  if [[ "$APP_STATUS" == "200" || "$APP_STATUS" == "401" ]]; then
    ok "app.$PUBLIC_DOMAIN reachable (HTTP $APP_STATUS)"
  elif [[ "$APP_STATUS" == "000" ]]; then
    fail "app.$PUBLIC_DOMAIN unreachable — is the Cloudflare Tunnel running?"
    echo "    Start tunnel: docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml --env-file code/infrastructure/cloudflared/.env.nas up -d"
  else
    fail "app.$PUBLIC_DOMAIN returned HTTP $APP_STATUS (expected 200 or 401)"
  fi

  AUTH_DISC=$(curl -sf "$AUTH_URL/realms/$REALM/.well-known/openid-configuration" 2>/dev/null || echo "")
  if [[ -n "$AUTH_DISC" ]]; then
    ISS=$(python3 -c "import sys,json; print(json.load(sys.stdin).get('issuer','?'))" <<< "$AUTH_DISC" 2>/dev/null || echo "?")
    ok "auth.$PUBLIC_DOMAIN OIDC discovery (issuer: $ISS)"
  else
    fail "auth.$PUBLIC_DOMAIN OIDC discovery unreachable at $AUTH_URL/realms/$REALM"
    echo "    Check tunnel: docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml logs cloudflared"
  fi

  TLS_APP="${APP_URL:0:5}"
  TLS_AUTH="${AUTH_URL:0:5}"
  if [[ "$TLS_APP" == "https" && "$TLS_AUTH" == "https" ]]; then
    ok "TLS: both endpoints use HTTPS"
  else
    fail "TLS: one or both endpoints are not HTTPS — check Cloudflare SSL/TLS mode"
  fi

  echo
  info "For full OIDC/Keycloak, booking, and WAF checks on the public domain, run:"
  info "  APP_URL=$APP_URL AUTH_URL=$AUTH_URL OIDC_REALM=$REALM ./tools/smoke-hosted.sh"
fi

# ── Summary ───────────────────────────────────────────────────────────────────────

hdr "Summary"

if [[ $FAILURES -gt 0 ]]; then
  printf "${RED}%d check(s) failed.${NC}\n" "$FAILURES"
  echo "Review the FAIL lines above for the failing service, log command, and rerun command."
  exit 1
fi

printf "${GREEN}All checks passed.${NC}\n"
if [[ -z "$PUBLIC_DOMAIN" ]]; then
  echo
  echo "Stack is running in local-container mode."
  echo "  Gateway:   http://localhost:10000"
  echo "  Keycloak:  http://localhost:8180"
  echo "  Grafana:   http://localhost:3000"
  echo
  echo "For full E2E smoke (login, booking, notifications, audit):"
  echo "  APP_URL=http://localhost:10000 AUTH_URL=http://localhost:8180 \\"
  echo "  OIDC_REALM=fps-local ./tools/smoke-hosted.sh"
fi
