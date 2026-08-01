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
#      restore force/confirm gates, --skip-public without --skip-tunnel, a
#      blank Cloudflare tunnel token, --smoke-only flag-combination guards, a
#      direct start-container-stack.sh --digitalocean --domain run failing at
#      the FPS_WEB_* contract before any start mutation).
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
FPS_WEB_API_BASE_URL=https://app.example.test/api
FPS_WEB_OIDC_AUTHORITY=https://auth.example.test/realms/fairspot
FPS_WEB_OIDC_CLIENT_ID=fps-web
FPS_WEB_OIDC_REDIRECT_URI=https://app.example.test/auth/callback
FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI=https://app.example.test/
ENV

# Same fixture, but with a blank FPS_AUTH_AUTHORITY — mirrors the shipped
# nas.env.example, which leaves the key present but empty.
BLANK_AUTH_ENV="$TMP/do-blank-auth.env"
sed 's/^FPS_AUTH_AUTHORITY=.*/FPS_AUTH_AUTHORITY=/' "$FIX_ENV" > "$BLANK_AUTH_ENV"

# Same fixture, but with a blank FPS_WEB_API_BASE_URL — mirrors an operator who
# copied nas.env.example straight to do.env without filling the public web
# runtime contract, which would otherwise leave fairspot-web serving its baked
# localhost/dev config.json.
MISSING_WEB_ENV="$TMP/do-missing-web.env"
sed 's#^FPS_WEB_API_BASE_URL=.*#FPS_WEB_API_BASE_URL=#' "$FIX_ENV" > "$MISSING_WEB_ENV"

# Same fixture, but FPS_WEB_OIDC_AUTHORITY points at a different issuer than
# FPS_AUTH_AUTHORITY — a browser would receive tokens the APIs reject.
MISMATCHED_WEB_ENV="$TMP/do-mismatched-web.env"
sed 's#^FPS_WEB_OIDC_AUTHORITY=.*#FPS_WEB_OIDC_AUTHORITY=https://auth.wrong.test/realms/fairspot#' "$FIX_ENV" > "$MISMATCHED_WEB_ENV"

# Same fixture, but FPS_WEB_OIDC_REDIRECT_URI is same-origin (still under
# app.example.test) yet not the exact documented callback path — proves the
# check is an EXACT match against the contract path, not a same-origin
# prefix, so a same-origin open-redirect/phishing path is still rejected.
MISMATCHED_REDIRECT_ENV="$TMP/do-mismatched-redirect.env"
sed 's#^FPS_WEB_OIDC_REDIRECT_URI=.*#FPS_WEB_OIDC_REDIRECT_URI=https://app.example.test/auth/callback-evil#' "$FIX_ENV" > "$MISMATCHED_REDIRECT_ENV"

render_do() {
  # $1 = image tag to pin (exported as FPS_IMAGE_TAG). Renders the full DO profile.
  FPS_IMAGE_TAG="$1" docker compose --project-directory "$INFRA_DIR" --env-file "$FIX_ENV" \
    -f "$INFRA_DIR/docker-compose.yaml" \
    -f "$INFRA_DIR/docker-compose.services.images.yml" \
    -f "$INFRA_DIR/docker-compose.dapr.yml" \
    -f "$INFRA_DIR/docker-compose.nas.yml" \
    -f "$INFRA_DIR/docker-compose.services.nas.yml" \
    -f "$INFRA_DIR/docker-compose.no-host-ports.yml" \
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

    if grep -q 'GF_SERVER_ROOT_URL: http://localhost:3001' "$RENDER"; then
      pass "Grafana absolute URLs use the default loopback-published port"
    else
      fail "Grafana root URL does not match the default loopback-published port"
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
# Minimal env + tunnel files for deploy ordering tests. A non-blank fixture token
# so tests further down the preflight (auth authority, FPS_WEB_*, tag) reach
# their own gate instead of tripping the tunnel-token check first.
printf 'CLOUDFLARED_TUNNEL_TOKEN=fixture-not-a-secret\n' > "$TMP/tunnel.env"
# Same tunnel file, but with a blank token — mirrors an operator who created the
# file but left the value empty (or copied a template without filling it in).
BLANK_TUNNEL_ENV="$TMP/tunnel-blank-token.env"
printf 'CLOUDFLARED_TUNNEL_TOKEN=\n' > "$BLANK_TUNNEL_ENV"

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
  expect_fail "deploy: missing public web runtime setting rejected" "missing public web runtime setting" -- \
    "$DEPLOY" --env-file "$MISSING_WEB_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag sha-x
  expect_fail "deploy: web OIDC authority inconsistent with auth authority rejected" "does not match FPS_AUTH_AUTHORITY" -- \
    "$DEPLOY" --env-file "$MISMATCHED_WEB_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag sha-x
  expect_fail "deploy: web redirect URI same-origin-but-wrong-path rejected" "FPS_WEB_OIDC_REDIRECT_URI does not match" -- \
    "$DEPLOY" --env-file "$MISMATCHED_REDIRECT_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag sha-x
  expect_fail "deploy: missing image tag"   "immutable image tag" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test
  expect_fail "deploy: latest tag rejected" "mutable" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag latest
  expect_fail "deploy: mutable tag rejected" "not an immutable" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --domain example.test --tag staging
  expect_fail "deploy: --skip-public requires --skip-tunnel" "requires --skip-tunnel" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$TMP/tunnel.env" --skip-public
  expect_fail "deploy: blank tunnel token rejected" "CLOUDFLARED_TUNNEL_TOKEN is missing or blank" -- \
    "$DEPLOY" --env-file "$FIX_ENV" --tunnel-env-file "$BLANK_TUNNEL_ENV" --domain example.test --tag sha-x
else
  skip "docker compose CLI unavailable — deploy preflight negative paths skipped"
fi

pre_tunnel_start_line="$(grep -n -- '--skip-public-smoke' "$DEPLOY" | head -1 | cut -d: -f1)"
tunnel_start_line="$(grep -n '== Starting Cloudflare Tunnel connector ==' "$DEPLOY" | head -1 | cut -d: -f1)"
# Match the literal shell variables in the deployment script.
# shellcheck disable=SC2016
post_tunnel_smoke_line="$(grep -n 'start-container-stack.sh" --digitalocean --env-file "$ENV_FILE" --domain "$DOMAIN"' "$DEPLOY" | head -1 | cut -d: -f1)"
# Match the literal $DOMAIN token passed by the wrapper.
# shellcheck disable=SC2016
if grep -B 1 -- '--skip-public-smoke' "$DEPLOY" | grep -q -- '--domain "$DOMAIN"' \
  && [[ -n "$pre_tunnel_start_line" && -n "$tunnel_start_line" && -n "$post_tunnel_smoke_line" ]] \
  && [[ "$pre_tunnel_start_line" -lt "$tunnel_start_line" ]] \
  && [[ "$tunnel_start_line" -lt "$post_tunnel_smoke_line" ]]; then
  pass "deploy defers public smoke until after the Tunnel start"
else
  fail "deploy does not preserve the pre-tunnel start / post-tunnel smoke handoff"
fi

# start-container-stack.sh --smoke-only flag-combination guards run before any
# docker call, so they need no daemon/CLI.
START_STACK="$REPO_ROOT/tools/start-container-stack.sh"
expect_fail "start-stack: --smoke-only requires --nas or --digitalocean" "requires --nas or --digitalocean" -- \
  "$START_STACK" --smoke-only
expect_fail "start-stack: --smoke-only + --seed still rejected (--seed is LOCAL-ONLY)" "LOCAL-ONLY" -- \
  "$START_STACK" --digitalocean --smoke-only --seed
expect_fail "start-stack: --smoke-only rejects --down" "cannot be combined with --down" -- \
  "$START_STACK" --digitalocean --smoke-only --down

# A direct `--digitalocean --domain` invocation (skipping deploy-digitalocean.sh)
# must fail at the same FPS_WEB_* contract check, before any mutation (Alertmanager
# render, network create, pull/up) — reuses the deploy MISSING_WEB_ENV fixture
# (a full DO env with FPS_WEB_API_BASE_URL blanked). Needs the Compose CLI, since
# start-container-stack.sh checks `docker compose version` before this gate.
if docker compose version >/dev/null 2>&1; then
  expect_fail "start-stack: --digitalocean --domain fails at missing FPS_WEB_* contract before mutation" \
    "missing public web runtime setting" -- \
    "$START_STACK" --digitalocean --domain example.test --env-file "$MISSING_WEB_ENV"
else
  skip "docker compose CLI unavailable — start-stack contract preflight skipped"
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

# Vault manual DR contract: restore-drill.sh must print an executable `docker
# compose cp` of the verified host snapshot into a fixed container path, then
# an executable `docker compose exec vault ... raft snapshot restore` step
# must use that SAME container path (not the host path) — otherwise the
# printed runbook is not copy-pasteable.
cp_line="$(grep -m1 '_print_cmd 3 .*cp .*vault:' "$RESTORE" || true)"
restore_line="$(grep -m1 '_print_cmd 4 .*exec vault .*raft snapshot restore' "$RESTORE" || true)"
cp_path="$(printf '%s' "$cp_line" | sed -n 's/.*vault:\([^"]*\)".*/\1/p')"
if [[ -n "$cp_path" ]] && printf '%s' "$restore_line" | grep -qF "$cp_path"; then
  pass "restore-drill: Vault DR contract copies snapshot into container before raft restore"
else
  fail "restore-drill: Vault DR contract missing matching docker compose cp / raft restore container path"
fi

# ── Summary ──────────────────────────────────────────────────────────────────
hdr "Summary"
echo "  PASS=$PASS  FAIL=$FAIL  SKIP=$SKIP"
[[ $FAIL -eq 0 ]] || { echo "  DigitalOcean profile validation FAILED."; exit 1; }
echo "  DigitalOcean profile validation PASSED."
