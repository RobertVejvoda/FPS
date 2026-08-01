#!/usr/bin/env bash
# Offline/static validation for the repeatable NAS Development profile (#891).
# Uses placeholder fixture values only; it never contacts a NAS or Cloudflare.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"
PASS=0 FAIL=0 SKIP=0

pass() { echo "  PASS  $*"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL  $*"; FAIL=$((FAIL + 1)); }
skip() { echo "  SKIP  $*"; SKIP=$((SKIP + 1)); }
hdr() { printf '\n== %s ==\n' "$*"; }

TMP="$(mktemp -d)"
trap 'find "$TMP" -depth -delete 2>/dev/null || true' EXIT

hdr "1. Shell syntax"
for script in tools/deploy-nas.sh tools/nas-start.sh tools/nas-stop.sh tools/start-container-stack.sh tools/validate-nas-profile.sh; do
  if bash -n "$REPO_ROOT/$script"; then pass "bash -n $script"; else fail "bash -n $script"; fi
done

FIX_ENV="$TMP/nas.env"
cat > "$FIX_ENV" <<'ENV'
DAPR_RUNTIME_VERSION=1.18.0
VAULT_IMAGE_VERSION=1.18
VAULT_TOKEN=fixture-not-a-secret
MONGO_USER=fixture
MONGO_PASS=fixture
RABBITMQ_USER=fixture
RABBITMQ_PASS=fixture
MINIO_ACCESS_KEY=fixture
MINIO_SECRET_KEY=fixture
POSTGRES_USER=fixture
POSTGRES_PASSWORD=fixture
POSTGRES_DB=fps_datahub
KC_ADMIN_USER=fixture
KC_ADMIN_PASS=fixture
KC_HOSTNAME=https://auth-dev.example.test
KC_DB_USER=fixture
KC_DB_PASSWORD=fixture
KC_DB_NAME=keycloak
FPS_APP_ORIGIN=https://app-dev.example.test
GRAFANA_ADMIN_USER=fixture
GRAFANA_ADMIN_PASSWORD=fixture
FPS_PUBLIC_APP_HOST=app-dev.example.test
FPS_PUBLIC_AUTH_HOST=auth-dev.example.test
FPS_PUBLIC_OPS_HOST=ops-dev.example.test
FPS_AUTH_AUTHORITY=https://auth-dev.example.test/realms/fairspot
FPS_AUTH_AUDIENCE=fps-web
FPS_AUTH_ADDITIONAL_AUDIENCES=fps-mobile,fps-cli
FPS_AUTH_ALLOW_HTTP_METADATA=false
FPS_AUTH_ALLOW_LOCAL_ISSUER_HOST_OVERRIDE=false
FPS_WEB_API_BASE_URL=https://app-dev.example.test/api
FPS_WEB_OIDC_AUTHORITY=https://auth-dev.example.test/realms/fairspot
FPS_WEB_OIDC_CLIENT_ID=fps-web
FPS_WEB_OIDC_REDIRECT_URI=https://app-dev.example.test/auth/callback
FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI=https://app-dev.example.test/
ALERTMANAGER_CONFIG_FILE=runtime/config.yaml
ENV

render_nas() {
  FPS_IMAGE_TAG=sha-testfixture \
  FPS_GRAFANA_ROOT_URL=https://ops-dev.example.test \
  FPS_GRAFANA_COOKIE_SECURE=true \
  docker compose --project-directory "$INFRA_DIR" --env-file "$FIX_ENV" \
    -f "$INFRA_DIR/docker-compose.yaml" \
    -f "$INFRA_DIR/docker-compose.services.images.yml" \
    -f "$INFRA_DIR/docker-compose.dapr.yml" \
    -f "$INFRA_DIR/docker-compose.nas.yml" \
    -f "$INFRA_DIR/docker-compose.services.nas.yml" \
    -f "$INFRA_DIR/docker-compose.no-host-ports.yml" \
    config
}

hdr "2. Rendered NAS profile"
if ! docker compose version >/dev/null 2>&1; then
  skip "docker compose unavailable — render checks skipped"
else
  RENDER="$TMP/rendered.yaml"
  if render_nas > "$RENDER" 2>"$TMP/render.err"; then
    pass "Compose profile renders from placeholder values"
    if grep -q 'published:' "$RENDER"; then
      fail "a NAS service publishes a host port"
    else
      pass "Tunnel-only boundary has zero host-published ports"
    fi
    if grep -qE '^\s+build:' "$RENDER"; then
      fail "NAS profile contains a local build"
    else
      pass "NAS consumes published images only"
    fi
    if grep -q 'image: ghcr.io/robertvejvoda/fairspot-datahub:sha-testfixture' "$RENDER"; then
      pass "immutable tag reaches DataHub service and migration job"
    else
      fail "immutable tag missing from rendered DataHub image"
    fi
    if grep -q '^  fairspot-datahub-migrate:' "$RENDER" \
      && grep -q 'DataHub__ApplyMigrationsAndExit: "true"' "$RENDER"; then
      pass "one-shot production DataHub migration service is rendered"
    else
      fail "DataHub migration service/flag missing"
    fi
    if grep -q 'prometheus/prometheus.containers.yaml' "$RENDER"; then
      pass "hosted Prometheus mounts the container-DNS scrape config"
    else
      fail "hosted Prometheus did not select prometheus.containers.yaml"
    fi
    if grep -q 'GF_SERVER_ROOT_URL: https://ops-dev.example.test' "$RENDER" \
      && grep -q 'GF_SECURITY_COOKIE_SECURE: "true"' "$RENDER"; then
      pass "Access-protected ops hostname reaches Grafana external URL/security config"
    else
      fail "Grafana external ops-host configuration missing from render"
    fi
  else
    fail "Compose profile did not render"
    sed 's/^/    /' "$TMP/render.err"
  fi
fi

hdr "3. Observability contract"
if grep -q "targets: \['fairspot-datahub:5211'\]" "$INFRA_DIR/prometheus/prometheus.containers.yaml" \
  && ! grep -q 'host\.docker\.internal' "$INFRA_DIR/prometheus/prometheus.containers.yaml"; then
  pass "Prometheus uses Docker service DNS for FairSpot targets"
else
  fail "Prometheus still depends on host-published FairSpot ports"
fi
if grep -q 'system_runtime_gc_heap_size' "$INFRA_DIR/grafana/dashboards/fairspot-local.json" \
  && grep -q 'system_runtime_threadpool_queue_length' "$INFRA_DIR/grafana/dashboards/fairspot-local.json" \
  && grep -q 'max by (job).*fairspot-' "$INFRA_DIR/grafana/dashboards/fairspot-local.json"; then
  pass "Grafana queries match current .NET metrics and FairSpot jobs"
else
  fail "Grafana dashboard contains stale metric/job queries"
fi

hdr "4. CLI safety gates"
expect_fail() {
  local label="$1" pattern="$2"; shift 2
  [[ "$1" == "--" ]] && shift
  local output rc
  output="$("$@" 2>&1)"; rc=$?
  if [[ $rc -ne 0 ]] && printf '%s' "$output" | grep -q "$pattern"; then
    pass "$label"
  else
    fail "$label (rc=$rc; expected: $pattern)"
  fi
}

DEPLOY="$REPO_ROOT/tools/deploy-nas.sh"
START="$REPO_ROOT/tools/start-container-stack.sh"
printf 'CLOUDFLARED_TUNNEL_TOKEN=fixture-not-a-secret\n' > "$TMP/tunnel.env"

expect_fail "deploy rejects a missing env file" "env file not found" -- \
  "$DEPLOY" --env-file "$TMP/missing.env" --app-host app-dev.example.test \
  --auth-host auth-dev.example.test --tag sha-x
MISSING_AUTH_ENV="$TMP/missing-auth.env"
sed 's/^FPS_PUBLIC_AUTH_HOST=.*/FPS_PUBLIC_AUTH_HOST=/' "$FIX_ENV" > "$MISSING_AUTH_ENV"
expect_fail "deploy requires app/auth hosts together" "exact app and auth hostnames are required" -- \
  "$DEPLOY" --env-file "$MISSING_AUTH_ENV" --tunnel-env-file "$TMP/tunnel.env" \
  --app-host app-dev.example.test --tag sha-x
expect_fail "deploy rejects latest without waiver" "immutable image tag" -- \
  "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --tag latest
expect_fail "deploy requires tunnel off when public smoke is skipped" "requires --skip-tunnel" -- \
  "$DEPLOY" --env-file "$FIX_ENV" --skip-public --tag sha-x

MISMATCH_ENV="$TMP/mismatch.env"
sed 's#^FPS_WEB_API_BASE_URL=.*#FPS_WEB_API_BASE_URL=https://wrong.example.test/api#' "$FIX_ENV" > "$MISMATCH_ENV"
if docker compose version >/dev/null 2>&1; then
  expect_fail "direct NAS start rejects a mismatched exact-host web contract before mutation" \
    "FPS_WEB_API_BASE_URL does not match" -- \
    "$START" --nas --env-file "$MISMATCH_ENV" --app-host app-dev.example.test \
      --auth-host auth-dev.example.test
else
  skip "docker compose unavailable — direct-start contract check skipped"
fi

hdr "5. CI/CD artifact boundary"
PUBLISH="$REPO_ROOT/.github/workflows/publish-images.yml"
if grep -q 'type=sha,format=long' "$PUBLISH" && grep -q 'push: true' "$PUBLISH"; then
  pass "Publish Images produces immutable SHA-tagged GHCR artifacts"
else
  fail "Publish Images lacks immutable SHA publishing"
fi
if grep -q 'validate-nas-profile.sh' "$REPO_ROOT/.github/workflows/ci.yml"; then
  pass "CI runs NAS profile validation"
else
  fail "CI does not run NAS profile validation"
fi

hdr "Summary"
echo "  PASS=$PASS  FAIL=$FAIL  SKIP=$SKIP"
[[ $FAIL -eq 0 ]] || { echo "  NAS profile validation FAILED."; exit 1; }
echo "  NAS profile validation PASSED."
