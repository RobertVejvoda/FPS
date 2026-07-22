#!/usr/bin/env bash
# tools/validate-digitalocean-profile.sh — static + negative-path validation for
# the DigitalOcean Droplet profile (#766). No live Droplet, no secrets, no
# mutation: it renders the merged Compose config with sanitized FIXTURE values
# and exercises the CLI safety gates, then reports PASS/FAIL.
#
# Covers the issue's "Required validation before In review":
#   1. bash -n for every changed/new shell script.
#   2. Rendered DigitalOcean Compose config asserts image-mode services, durable
#      volumes + restart policies, and NO public host-port binding.
#   3. CLI negative paths fail closed (missing env/tunnel/tag, latest tag,
#      restore force/confirm gates).
#   4. sha-<commit> flows through to the composed services without a local build.
#
# The rendered config contains interpolated secrets, so it is written to a temp
# file that is never printed — only its structure (services/volumes/ports) is
# inspected. Run from the repo root: ./tools/validate-digitalocean-profile.sh
#
# Exit codes: 0 all checks passed; 1 one or more failed; 2 setup error.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

PASS=0 FAIL=0 SKIP=0
pass() { echo "  PASS  $*"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL  $*"; FAIL=$((FAIL + 1)); }
skip() { echo "  SKIP  $*"; SKIP=$((SKIP + 1)); }
hdr()  { printf "\n== %s ==\n" "$*"; }

TMP="$(mktemp -d)"
trap 'find "$TMP" -depth -delete 2>/dev/null || true' EXIT

# ── 1. Syntax check every changed/new shell script ───────────────────────────
hdr "1. bash -n (shell syntax)"
SCRIPTS=(
  tools/deploy-digitalocean.sh
  tools/start-container-stack.sh
  tools/backup-stack.sh
  tools/restore-drill.sh
  tools/lib/backup-common.sh
  tools/validate-digitalocean-profile.sh
)
for s in "${SCRIPTS[@]}"; do
  if bash -n "$REPO_ROOT/$s" 2>/dev/null; then pass "bash -n $s"; else fail "bash -n $s"; fi
done

# ── Sanitized fixture env (all required :? vars; NON-secret placeholders) ─────
FIX_ENV="$TMP/do.env"
cat > "$FIX_ENV" <<'ENV'
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
KC_HOSTNAME=https://auth.example.test
KC_DB_USER=fixture
KC_DB_PASSWORD=fixture
KC_DB_NAME=keycloak
FPS_APP_ORIGIN=https://app.example.test
GRAFANA_ADMIN_USER=fixture
GRAFANA_ADMIN_PASSWORD=fixture
FPS_AUTH_AUTHORITY=https://auth.example.test/realms/fairspot
FPS_AUTH_AUDIENCE=fps-web
FPS_PUBLIC_DOMAIN=example.test
ALERTMANAGER_CONFIG_FILE=runtime/config.yaml
ENV

# Same fixture, but with a blank FPS_AUTH_AUTHORITY — mirrors the shipped
# nas.env.example, which leaves the key present but empty.
BLANK_AUTH_ENV="$TMP/do-blank-auth.env"
sed 's/^FPS_AUTH_AUTHORITY=.*/FPS_AUTH_AUTHORITY=/' "$FIX_ENV" > "$BLANK_AUTH_ENV"

render_do() {
  # $1 = image tag to pin (exported as FPS_IMAGE_TAG). Renders the full DO profile.
  FPS_IMAGE_TAG="$1" docker compose --project-directory "$INFRA_DIR" --env-file "$FIX_ENV" \
    -f "$INFRA_DIR/docker-compose.yaml" \
    -f "$INFRA_DIR/docker-compose.services.images.yml" \
    -f "$INFRA_DIR/docker-compose.dapr.yml" \
    -f "$INFRA_DIR/docker-compose.nas.yml" \
    -f "$INFRA_DIR/docker-compose.services.nas.yml" \
    -f "$INFRA_DIR/docker-compose.digitalocean.yml" \
    config
}

# ── 2 + 4. Rendered-config assertions (needs docker) ─────────────────────────
hdr "2. Rendered DigitalOcean Compose profile"
if ! docker compose version >/dev/null 2>&1; then
  skip "docker compose unavailable — render assertions skipped (run on a Docker host)"
else
  docker network inspect fairspot_network >/dev/null 2>&1 || docker network create fairspot_network >/dev/null 2>&1 || true
  RENDER="$TMP/rendered.yaml"
  if render_do "sha-testfixture" > "$RENDER" 2>"$TMP/render.err"; then
    pass "compose config renders (merge tags supported, all required vars present)"

    # Image-mode services present, and NO local build context.
    missing_svc=""
    for svc in fairspot-web fairspot-booking fairspot-identity fairspot-datahub keycloak vault; do
      grep -qE "^  $svc:" "$RENDER" || missing_svc="$missing_svc $svc"
    done
    [[ -z "$missing_svc" ]] && pass "expected image-mode services present" \
                            || fail "services missing from render:$missing_svc"
    if grep -qE '^\s+build:' "$RENDER"; then fail "render has a build: section (must be image-mode only)"; \
    else pass "no build: sections (pure image-mode, nothing built on the Droplet)"; fi

    # Durable volumes + restart policies.
    missing_vol=""
    for vol in mongodb_data postgres_data keycloak_postgres_data minio_data vault_data; do
      grep -qE "^  $vol:" "$RENDER" || missing_vol="$missing_vol $vol"
    done
    [[ -z "$missing_vol" ]] && pass "durable named volumes present" \
                            || fail "durable volumes missing:$missing_vol"
    if [[ "$(grep -c 'restart: unless-stopped' "$RENDER")" -ge 20 ]]; then
      pass "restart: unless-stopped present on the service graph"
    else fail "restart policies missing (expected the hosted overlay's unless-stopped)"; fi

    # THE public-boundary assertion: every published port must be loopback.
    pubs="$(grep -c 'published:' "$RENDER" || true)"
    loop="$(grep -c 'host_ip: 127\.0\.0\.1' "$RENDER" || true)"
    open="$(grep -E 'host_ip: (0\.0\.0\.0|::)' "$RENDER" || true)"
    if [[ -z "$open" && "$pubs" == "$loop" ]]; then
      pass "no public host-port bindings (published=$pubs, all loopback; allowlist empty)"
    else
      fail "PUBLIC host-port binding present (published=$pubs loopback=$loop): ${open:-<port with no host_ip>}"
    fi

    # 4. sha-<commit> passthrough — the pinned tag reaches the images, no build.
    if grep -qE 'image: .*/fairspot-web:sha-testfixture' "$RENDER"; then
      pass "sha-<commit> tag flows through to the composed images (no local build)"
    else
      fail "pinned sha tag did not reach the image refs"
    fi
  else
    fail "compose config failed to render"
    sed 's/^/      /' "$TMP/render.err"
  fi
fi

# ── 3. CLI negative-path safety gates ────────────────────────────────────────
hdr "3. CLI negative paths (fail closed)"

# Backup dir stub for restore-drill gate tests.
STUB_BK="$TMP/backup"; mkdir -p "$STUB_BK"; echo '{"mode":"digitalocean"}' > "$STUB_BK/manifest.json"
# Minimal env + tunnel files for deploy ordering tests.
touch "$TMP/tunnel.env"

# expect_fail "<label>" <grep-pattern> -- <command...>
expect_fail() {
  local label="$1" pat="$2"; shift 2; [[ "$1" == "--" ]] && shift
  local out rc
  out="$("$@" 2>&1)"; rc=$?
  if [[ $rc -ne 0 ]] && printf '%s' "$out" | grep -q "$pat"; then pass "$label"; \
  else fail "$label (rc=$rc, expected pattern: $pat)"; fi
}

DEPLOY="$REPO_ROOT/tools/deploy-digitalocean.sh"
RESTORE="$REPO_ROOT/tools/restore-drill.sh"
BACKUP="$REPO_ROOT/tools/backup-stack.sh"

# Deploy's input/flag/tag gates run before the daemon check, so they only need
# the Compose CLI (offline), not a running daemon.
if docker compose version >/dev/null 2>&1; then
  expect_fail "deploy: missing env file"    "env file not found" -- \
    "$DEPLOY" --env-file "$TMP/nope.env" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag sha-x
  expect_fail "deploy: missing tunnel file" "tunnel env file not found" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/nope.env" --domain example.test --tag sha-x
  expect_fail "deploy: blank auth authority rejected" "encrypted public auth" -- \
    "$DEPLOY" --env-file "$BLANK_AUTH_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag sha-x
  expect_fail "deploy: missing image tag"   "immutable image tag" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test
  expect_fail "deploy: latest tag rejected" "mutable" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag latest
  expect_fail "deploy: mutable tag rejected" "not an immutable" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag staging
else
  skip "docker compose CLI unavailable — deploy preflight negative paths skipped"
fi

# restore-drill.sh / backup-stack.sh check `command -v docker` early, so their
# flag/confirmation gate assertions are Docker-dependent. Guard them separately.
if command -v docker >/dev/null 2>&1; then
  expect_fail "restore: --digitalocean needs --force-digitalocean" "force-digitalocean" -- \
    "$RESTORE" --from "$STUB_BK" --digitalocean
  expect_fail "restore: --force-digitalocean still needs --yes"    "Re-run with --yes" -- \
    "$RESTORE" --from "$STUB_BK" --digitalocean --force-digitalocean
  expect_fail "restore: --force-nas does NOT unlock a DO restore"  "force-digitalocean" -- \
    "$RESTORE" --from "$STUB_BK" --digitalocean --force-nas
  expect_fail "backup: unknown argument rejected"                  "Unknown argument" -- \
    "$BACKUP" --bogus-flag
else
  skip "docker CLI unavailable — restore/backup gate assertions skipped"
fi

# ── Summary ──────────────────────────────────────────────────────────────────
hdr "Summary"
echo "  PASS=$PASS  FAIL=$FAIL  SKIP=$SKIP"
[[ $FAIL -eq 0 ]] || { echo "  DigitalOcean profile validation FAILED."; exit 1; }
echo "  DigitalOcean profile validation PASSED."
