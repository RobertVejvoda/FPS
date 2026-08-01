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
if sh -n "$INFRA_DIR/datahub/run-migrations.sh"; then
  pass "sh -n code/infrastructure/datahub/run-migrations.sh"
else
  fail "sh -n code/infrastructure/datahub/run-migrations.sh"
fi

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
      && grep -q 'DataHub__ApplyMigrationsAndExit: "true"' "$RENDER" \
      && grep -q 'ASPNETCORE_ENVIRONMENT: Production' "$RENDER" \
      && grep -q 'ASPNETCORE_URLS: http://127.0.0.1:5211' "$RENDER" \
      && grep -q 'run-datahub-migrations.sh' "$RENDER"; then
      pass "finite DataHub migration service preserves Production validation with legacy fallback"
    else
      fail "DataHub migration service/compatibility launcher contract missing"
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

hdr "3. DataHub migration launcher compatibility"
MIGRATION_LAUNCHER="$INFRA_DIR/datahub/run-migrations.sh"
FAKE_BIN="$TMP/fake-bin"
mkdir -p "$FAKE_BIN"
cat > "$FAKE_BIN/dotnet" <<'SH'
#!/bin/sh
case "${FAKE_DOTNET_MODE:-current}" in
  current)
    [ "${ASPNETCORE_ENVIRONMENT:-}" = "Production" ] || exit 65
    [ "${DataHub__ApplyMigrationsAndExit:-}" = "true" ] || exit 66
    exit 0
    ;;
  legacy)
    case "${ASPNETCORE_ENVIRONMENT:-}" in
      Production)
        [ "${DataHub__ApplyMigrationsAndExit:-}" = "true" ] || exit 66
        ;;
      Development)
        [ "${DataHub__ApplyMigrationsAndExit:-}" = "false" ] || exit 66
        echo "Legacy Development startup migrations applied"
        ;;
      *) exit 65 ;;
    esac
    echo "Now listening on: http://127.0.0.1:5211"
    trap 'exit 0' TERM INT
    while :; do sleep 1; done
    ;;
  failure)
    echo "fixture migration failure" >&2
    exit 42
    ;;
  *)
    exit 64
    ;;
esac
SH
chmod +x "$FAKE_BIN/dotnet"

if current_output="$(
  PATH="$FAKE_BIN:$PATH" \
  FPS_ASSEMBLY=FPS.DataHub.dll \
  FPS_DATAHUB_MIGRATION_TIMEOUT_SECONDS=5 \
  FAKE_DOTNET_MODE=current \
  "$MIGRATION_LAUNCHER" 2>&1
)" && printf '%s' "$current_output" | grep -q 'exited successfully'; then
  pass "current migration-mode image exits successfully"
else
  fail "current migration-mode image did not complete successfully"
fi

if legacy_output="$(
  PATH="$FAKE_BIN:$PATH" \
  FPS_ASSEMBLY=FPS.DataHub.dll \
  FPS_DATAHUB_MIGRATION_TIMEOUT_SECONDS=5 \
  FAKE_DOTNET_MODE=legacy \
  "$MIGRATION_LAUNCHER" 2>&1
)" && printf '%s' "$legacy_output" | grep -q 'Legacy DataHub image reached listening state'; then
  pass "legacy rollback image is stopped after startup migrations"
else
  fail "legacy rollback image did not complete as a finite job"
fi

failure_output="$(
  PATH="$FAKE_BIN:$PATH" \
  FPS_ASSEMBLY=FPS.DataHub.dll \
  FPS_DATAHUB_MIGRATION_TIMEOUT_SECONDS=5 \
  FAKE_DOTNET_MODE=failure \
  "$MIGRATION_LAUNCHER" 2>&1
)"
failure_rc=$?
if [[ $failure_rc -eq 42 ]] \
  && printf '%s' "$failure_output" | grep -q 'exited with code 42'; then
  pass "migration failure remains fail-closed"
else
  fail "migration failure was not propagated (rc=$failure_rc)"
fi

hdr "4. Observability contract"
if grep -q "targets: \['fairspot-datahub:5211'\]" "$INFRA_DIR/prometheus/prometheus.containers.yaml" \
  && ! grep -q 'host\.docker\.internal' "$INFRA_DIR/prometheus/prometheus.containers.yaml"; then
  pass "Prometheus uses Docker service DNS for FairSpot targets"
else
  fail "Prometheus still depends on host-published FairSpot ports"
fi
if grep -q 'system_runtime_gc_heap_size / 1024 / 1024' "$INFRA_DIR/grafana/dashboards/fairspot-local.json" \
  && grep -q 'system_runtime_threadpool_queue_length' "$INFRA_DIR/grafana/dashboards/fairspot-local.json" \
  && grep -q 'max by (job).*fairspot-' "$INFRA_DIR/grafana/dashboards/fairspot-local.json"; then
  pass "Grafana queries match current .NET metrics and FairSpot jobs"
else
  fail "Grafana dashboard contains stale metric/job queries"
fi

hdr "5. CLI safety gates"
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

tag_gate_line="$(grep -n '^IMAGE_TAG=' "$DEPLOY" | head -1 | cut -d: -f1)"
docker_gate_line="$(grep -n '^if ! command -v docker' "$DEPLOY" | head -1 | cut -d: -f1)"
if [[ -n "$tag_gate_line" && -n "$docker_gate_line" && "$tag_gate_line" -lt "$docker_gate_line" ]]; then
  pass "immutable-tag validation runs before the Docker prerequisite"
else
  fail "immutable-tag validation does not run before the Docker prerequisite"
fi

FAKE_DOCKER_BIN="$TMP/fake-docker-bin"
mkdir -p "$FAKE_DOCKER_BIN"
cat > "$FAKE_DOCKER_BIN/docker" <<'SH'
#!/bin/sh
if [ "${1:-}" = "--version" ]; then
  printf 'Docker version 24.0.0, build fixture\n'
  exit 0
fi
if [ "${1:-}" = "compose" ] && [ "${2:-}" = "version" ]; then
  printf 'Docker Compose version v2.29.2\n'
  exit 0
fi
if [ "${1:-}" = "info" ]; then
  exit 0
fi
if [ "${1:-}" = "ps" ]; then
  printf 'fairspot-cloudflared\tcloudflare/cloudflared:latest\n'
  exit 0
fi
exit 64
SH
chmod +x "$FAKE_DOCKER_BIN/docker"
expect_fail "deploy refuses unchecked mutation while a tunnel remains active" \
  "active Cloudflare Tunnel connector" -- \
  env PATH="$FAKE_DOCKER_BIN:$PATH" "$DEPLOY" --env-file "$FIX_ENV" \
    --skip-public --skip-tunnel

NO_PUBLIC_HOST_ENV="$TMP/no-public-host.env"
sed -e 's/^FPS_PUBLIC_APP_HOST=.*/FPS_PUBLIC_APP_HOST=/' \
  -e 's/^FPS_PUBLIC_AUTH_HOST=.*/FPS_PUBLIC_AUTH_HOST=/' \
  "$FIX_ENV" > "$NO_PUBLIC_HOST_ENV"
expect_fail "direct NAS start refuses unchecked mutation while a tunnel remains active" \
  "active Cloudflare Tunnel connector" -- \
  env PATH="$FAKE_DOCKER_BIN:$PATH" "$START" --nas \
    --env-file "$NO_PUBLIC_HOST_ENV" --skip-public-smoke

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

hdr "6. CI/CD artifact boundary"
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
