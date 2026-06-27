#!/usr/bin/env bash
# tools/start-container-stack.sh — Container-only FairSpot stack start and smoke.
#
# Release-gate contract (the --nas path): the host needs only Docker Engine and
# the Docker Compose v2 plugin. Container state is read with `docker inspect`,
# and every HTTP probe runs inside a throwaway curl container on the Docker
# network — the host needs no curl, python, jq, .NET SDK, or Dapr CLI. This
# script never calls dotnet, dapr, dapr run, start-local-harness.sh, or
# start-with-dapr.sh.
#
# The --seed path is LOCAL-ONLY. It runs the developer helper scripts
# (dev-setup-auth.sh, dev-seed.sh, smoke-hosted.sh) which target the fps-local
# Keycloak realm with dev credentials and use host curl/python3. Seeding a NAS
# with enforced credentials is a separate follow-up; --nas --seed is rejected.
#
# Usage (local-container — dev defaults, no env file needed):
#   ./tools/start-container-stack.sh
#   ./tools/start-container-stack.sh --seed     # also seed demo data + local E2E
#
# Usage (NAS/hosted — real credentials enforced via .env, Docker/Compose only):
#   ./tools/start-container-stack.sh --nas --env-file code/infrastructure/.env
#   ./tools/start-container-stack.sh --nas --env-file code/infrastructure/.env --domain fairspot.net
#
# Flags:
#   --nas              Apply NAS overlay (restart policies + required credential check).
#   --env-file PATH    Env file for --nas mode. Default: code/infrastructure/.env
#   --seed             LOCAL ONLY. After services are healthy, configure Keycloak
#                      and seed demo + Green Logistics data, then run the local E2E
#                      smoke (booking -> notification -> audit) to validate pub/sub
#                      and workflow. Requires host curl + python3. Rejected with --nas.
#   --skip-e2e         Bring up the stack and verify container/service/sidecar
#                      health, then stop. Skips the gateway, OIDC, seeded E2E, and
#                      public-domain smoke checks. Health/readiness still runs.
#   --domain DOMAIN    After local checks pass, probe https://app.DOMAIN and
#                      https://auth.DOMAIN through Cloudflare (Docker-only).
#   --down             Tear down the stack (same compose files) and exit.
#
# Override the probe image with CURL_IMAGE (default curlimages/curl:8.11.1).
#
# Exit codes:
#   0  Stack running and all requested checks passed.
#   1  Prerequisite missing, env file absent, bad flag combo, or a check failed.

set -euo pipefail

# ── Repo and infra paths ────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

NET="fps_network"
CURL_IMAGE="${CURL_IMAGE:-curlimages/curl:8.11.1}"

# ── Argument parsing ────────────────────────────────────────────────────────────

MODE="local"
ENV_FILE="$INFRA_DIR/.env"
SKIP_E2E=false
TEARDOWN=false
PUBLIC_DOMAIN=""
SEED=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --nas)        MODE="nas" ;;
    --env-file)   ENV_FILE="$2"; shift ;;
    --skip-e2e)   SKIP_E2E=true ;;
    --seed)       SEED=true ;;
    --down)       TEARDOWN=true ;;
    --domain)     PUBLIC_DOMAIN="$2"; shift ;;
    *) echo "Unknown flag: $1"; exit 1 ;;
  esac
  shift
done

# ── Output helpers ──────────────────────────────────────────────────────────────

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

# ── Flag-combination guards ──────────────────────────────────────────────────────

if [[ "$MODE" == "nas" && "$SEED" == "true" ]]; then
  echo "ERROR: --seed is LOCAL-ONLY and cannot be combined with --nas."
  echo
  echo "The seed/E2E helpers (dev-setup-auth.sh, dev-seed.sh, smoke-hosted.sh) use the"
  echo "fps-local Keycloak realm and local dev credentials, which do not match the"
  echo "NAS-enforced secrets in your .env file."
  echo
  echo "For NAS validation, start the stack and probe the public domain instead:"
  echo "  ./tools/start-container-stack.sh --nas --env-file <env> --domain <domain>"
  echo "  APP_URL=https://app.<domain> AUTH_URL=https://auth.<domain> \\"
  echo "    OIDC_REALM=fairspot ./tools/smoke-hosted.sh"
  echo
  echo "NAS-aware seeding is tracked as a follow-up to #604."
  exit 1
fi

# ── Build compose command ───────────────────────────────────────────────────────

COMPOSE_FILES=(
  "-f" "$INFRA_DIR/docker-compose.yaml"
  "-f" "$INFRA_DIR/docker-compose.services.yml"
  "-f" "$INFRA_DIR/docker-compose.dapr.yml"
)
if [[ "$MODE" == "nas" ]]; then
  COMPOSE_FILES+=("-f" "$INFRA_DIR/docker-compose.nas.yml")
fi

if [[ "$MODE" == "nas" ]]; then
  COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" "${COMPOSE_FILES[@]}")
else
  COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" "${COMPOSE_FILES[@]}")
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

# ── Docker-only inspection helpers ───────────────────────────────────────────────
# Container state is read via docker inspect (Go templates) so the host needs no
# python/jq. HTTP probes run inside a throwaway curl container so the host needs
# no curl.

_cid() { "${COMPOSE_CMD[@]}" ps -aq "$1" 2>/dev/null | head -1 || true; }

# Health if a healthcheck is defined, otherwise the raw container state.
_health() {
  docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$1" 2>/dev/null || true
}
_state()    { docker inspect -f '{{.State.Status}}' "$1" 2>/dev/null || true; }
_exitcode() { docker inspect -f '{{.State.ExitCode}}' "$1" 2>/dev/null || true; }

# probe_net <curl-args...> — HTTP probe on the Docker network (internal service names).
# Returns curl's real exit code. In command substitution, append `|| true` at the
# call site so `set -e` does not abort on an expected probe failure.
probe_net() { docker run --rm --network "$NET" "$CURL_IMAGE" -s "$@" 2>/dev/null; }
# probe_pub <curl-args...> — HTTP probe with default egress (public internet).
probe_pub() { docker run --rm "$CURL_IMAGE" -s "$@" 2>/dev/null; }

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

# --seed is local-only and leans on host dev tooling; verify it up front.
if [[ "$SEED" == "true" ]]; then
  for tool in curl python3; do
    if ! command -v "$tool" >/dev/null 2>&1; then
      echo "ERROR: --seed (local only) requires host '$tool' for the dev seed/E2E helpers."
      exit 1
    fi
  done
  ok "host curl + python3 present (required only for --seed)"
fi

# ── Teardown ─────────────────────────────────────────────────────────────────────

if [[ "$TEARDOWN" == "true" ]]; then
  hdr "Tearing down"
  echo "Stopping containers (volumes preserved)..."
  "${COMPOSE_CMD[@]}" down
  echo "Done. Data volumes are intact. To remove data volumes too:"
  echo "  docker volume rm \$(docker volume ls -q | grep fps)"
  exit 0
fi

# ── External network ─────────────────────────────────────────────────────────────

hdr "Docker network"

if ! docker network inspect "$NET" >/dev/null 2>&1; then
  echo "Creating $NET..."
  docker network create "$NET" >/dev/null
  ok "$NET created"
else
  ok "$NET exists"
fi

# Pre-pull the probe image once so the polling loops below do not emit pull noise.
if ! docker image inspect "$CURL_IMAGE" >/dev/null 2>&1; then
  echo "Pulling probe image $CURL_IMAGE..."
  docker pull "$CURL_IMAGE" >/dev/null 2>&1 || {
    echo "ERROR: could not pull $CURL_IMAGE. Set CURL_IMAGE to an available image and retry."
    exit 1
  }
fi
ok "probe image: $CURL_IMAGE"

# ── Start the stack ──────────────────────────────────────────────────────────────

hdr "Starting stack ($MODE mode)"
echo "Command: $COMPOSE_HUMAN up -d"
echo

"${COMPOSE_CMD[@]}" up -d

# ── Wait for infrastructure health ───────────────────────────────────────────────

hdr "Waiting for infrastructure health"

INFRA_TIMEOUT=120
INFRA_INTERVAL=5

for svc in vault rabbitmq mongodb postgres; do
  elapsed=0
  printf "  Waiting for %s" "$svc"
  ready=false
  while [[ $elapsed -lt $INFRA_TIMEOUT ]]; do
    cid="$(_cid "$svc")"
    if [[ -n "$cid" ]]; then
      state="$(_health "$cid")"
      if [[ "$state" == "healthy" || "$state" == "running" ]]; then
        ready=true
        break
      fi
    fi
    printf "."
    sleep "$INFRA_INTERVAL"
    elapsed=$((elapsed + INFRA_INTERVAL))
  done
  if [[ "$ready" == "true" ]]; then
    printf " — OK\n"
  else
    printf " — TIMEOUT\n"
    fail "$svc did not become healthy within ${INFRA_TIMEOUT}s"
    echo "    Logs:  $COMPOSE_HUMAN logs $svc"
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
elapsed=0
printf "  Waiting for vault-init to complete"
done_state=""
while [[ $elapsed -lt $VAULT_INIT_TIMEOUT ]]; do
  cid="$(_cid vault-init)"
  if [[ -n "$cid" ]]; then
    state="$(_state "$cid")"
    code="$(_exitcode "$cid")"
    if [[ "$state" == "exited" && "$code" == "0" ]]; then
      printf " — OK\n"
      ok "Vault secrets seeded (vault-init exited 0)"
      done_state="ok"
      break
    elif [[ "$state" == "exited" && -n "$code" && "$code" != "0" ]]; then
      printf " — FAILED\n"
      fail "vault-init exited with code $code — Vault secrets were not seeded"
      echo "    Logs:  $COMPOSE_HUMAN logs vault-init"
      echo "    Rerun: $COMPOSE_HUMAN up --force-recreate vault-init"
      done_state="fail"
      break
    fi
  fi
  printf "."
  sleep 5
  elapsed=$((elapsed + 5))
done

if [[ -z "$done_state" ]]; then
  printf " — TIMEOUT\n"
  fail "vault-init did not complete within ${VAULT_INIT_TIMEOUT}s"
  echo "    Logs: $COMPOSE_HUMAN logs vault-init"
fi

if [[ $FAILURES -gt 0 ]]; then
  echo
  echo "Vault seed failed. Dapr sidecars cannot read secrets. Resolve before continuing."
  exit 1
fi

# ── Wait for app services to respond ─────────────────────────────────────────────

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
    if probe_net -sf -o /dev/null "http://$svc:$port/health"; then
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
  state=""
  while [[ $elapsed -lt $DAPR_TIMEOUT ]]; do
    cid="$(_cid "$svc")"
    if [[ -n "$cid" ]]; then
      state="$(_state "$cid")"
      if [[ "$state" == "running" ]]; then
        ready=true
        break
      fi
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

# ── Stop here if E2E/smoke is skipped ────────────────────────────────────────────

if [[ "$SKIP_E2E" == "true" ]]; then
  hdr "Summary (--skip-e2e)"
  if [[ $FAILURES -gt 0 ]]; then
    printf "${RED}%d health/readiness check(s) failed.${NC}\n" "$FAILURES"
    exit 1
  fi
  printf "${GREEN}Stack up; container/service/sidecar health verified.${NC}\n"
  info "Gateway, OIDC, and E2E smoke skipped (--skip-e2e)."
  exit 0
fi

# ── Seed demo + Green Logistics data (local only) ────────────────────────────────

if [[ "$SEED" == "true" ]]; then
  hdr "Seeding Keycloak + demo data (local only)"

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

# ── Gateway + OIDC smoke ─────────────────────────────────────────────────────────

hdr "Gateway + OIDC smoke"

echo "Keycloak OIDC discovery (internal: keycloak:8080, realm fps-local)..."
KC_DISC="$(probe_net -sf http://keycloak:8080/realms/fps-local/.well-known/openid-configuration || true)"
if printf '%s' "$KC_DISC" | grep -q '"issuer"'; then
  ISS="$(printf '%s' "$KC_DISC" | grep -o '"issuer":"[^"]*"' | head -1)"
  ok "Keycloak OIDC discovery ($ISS)"
else
  fail "Keycloak OIDC discovery unreachable (realm fps-local)"
  echo "    Logs:  $COMPOSE_HUMAN logs keycloak"
  echo "    Rerun: $COMPOSE_HUMAN up -d keycloak"
fi

echo
echo "Service health through the Envoy gateway (internal: envoy-proxy:10000)..."

GATEWAY_SERVICES=(identity booking notification profile audit reporting configuration customer datahub)
for svc in "${GATEWAY_SERVICES[@]}"; do
  body="$(probe_net -sf http://envoy-proxy:10000/health/$svc || true)"
  if printf '%s' "$body" | grep -q '"status":"Healthy"'; then
    ok "$svc via gateway: Healthy"
  else
    fail "$svc via gateway: not Healthy (/health/$svc)"
    echo "    App logs:   $COMPOSE_HUMAN logs fps-$svc"
    echo "    Envoy logs: $COMPOSE_HUMAN logs envoy-proxy"
    echo "    Rerun:      $COMPOSE_HUMAN up -d fps-$svc fps-${svc}-dapr"
  fi
done

# ── Local E2E smoke (only when data was seeded) ──────────────────────────────────
# Proves state stores, pub/sub, and workflow: a booking submitted through the
# gateway must produce notification + audit records via Dapr events. Local only.

if [[ "$SEED" == "true" && $FAILURES -eq 0 ]]; then
  echo
  echo "Running local E2E smoke (booking -> notification -> audit)..."
  if APP_URL="http://localhost:10000" AUTH_URL="http://localhost:8180" OIDC_REALM="fps-local" \
       "$REPO_ROOT/tools/smoke-hosted.sh"; then
    ok "Local E2E smoke passed (seeded data, pub/sub, and workflow verified)"
  else
    fail "Local E2E smoke failed — see smoke-evidence-*.txt and output above"
    echo "    Pub/sub or workflow may not be wired. Check Dapr sidecar logs:"
    echo "    $COMPOSE_HUMAN logs fps-booking-dapr fps-notification-dapr"
  fi
fi

# ── Public-domain smoke (optional, Docker-only) ──────────────────────────────────

if [[ -n "$PUBLIC_DOMAIN" ]]; then
  hdr "Public-domain smoke (https://$PUBLIC_DOMAIN)"

  APP_URL="https://app.$PUBLIC_DOMAIN"
  AUTH_URL="https://auth.$PUBLIC_DOMAIN"
  REALM="fairspot"

  echo "Cloudflare tunnel connectivity..."
  APP_STATUS="$(probe_pub -o /dev/null -w '%{http_code}' "$APP_URL/health/identity" || true)"
  if [[ "$APP_STATUS" == "200" || "$APP_STATUS" == "401" ]]; then
    ok "app.$PUBLIC_DOMAIN reachable (HTTP $APP_STATUS)"
  elif [[ -z "$APP_STATUS" || "$APP_STATUS" == "000" ]]; then
    fail "app.$PUBLIC_DOMAIN unreachable — is the Cloudflare Tunnel running?"
    echo "    Start tunnel: docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml --env-file code/infrastructure/cloudflared/.env.nas up -d"
  else
    fail "app.$PUBLIC_DOMAIN returned HTTP $APP_STATUS (expected 200 or 401)"
  fi

  AUTH_DISC="$(probe_pub -sf "$AUTH_URL/realms/$REALM/.well-known/openid-configuration" || true)"
  if printf '%s' "$AUTH_DISC" | grep -q '"issuer"'; then
    ISS="$(printf '%s' "$AUTH_DISC" | grep -o '"issuer":"[^"]*"' | head -1)"
    ok "auth.$PUBLIC_DOMAIN OIDC discovery ($ISS)"
  else
    fail "auth.$PUBLIC_DOMAIN OIDC discovery unreachable (realm $REALM)"
    echo "    Check tunnel: docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml logs cloudflared"
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
  echo "Stack is running in $MODE-container mode."
  echo "  Gateway:   http://localhost:10000"
  echo "  Keycloak:  http://localhost:8180"
  echo "  Grafana:   http://localhost:3000"
  if [[ "$SEED" != "true" && "$MODE" != "nas" ]]; then
    echo
    echo "To seed demo data and run the local E2E smoke, re-run with --seed."
  fi
fi
