# tools/lib/backup-common.sh — shared helpers for backup-stack.sh and restore-drill.sh.
#
# Sourced, not executed. Resolves the same compose command that
# start-container-stack.sh builds (so backups target the exact running stack),
# and provides logging + store helpers. Credentials are NEVER read on the host:
# every dump runs inside its container and references the container's own
# injected env (MONGO_INITDB_ROOT_*, POSTGRES_*), so no secret ever touches the
# host shell. The one exception is Vault, which needs a token to snapshot — that
# is read from VAULT_TOKEN in the environment/--env-file and, if absent, the
# raft snapshot is skipped in favour of a volume-level tar fallback.

# ── Logging ──────────────────────────────────────────────────────────────────
_c() { printf '\033[%sm' "$1" 2>/dev/null || true; }
log()  { echo "$(_c '1;34')==>$(_c 0) $*"; }
ok()   { echo "$(_c '1;32') ✓$(_c 0) $*"; }
warn() { echo "$(_c '1;33') !$(_c 0) $*" >&2; }
die()  { echo "$(_c '1;31')✗ $*$(_c 0)" >&2; exit 1; }

# ── Cross-platform sha256 (Linux sha256sum, macOS shasum) ────────────────────
_sha256() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$@"
  else shasum -a 256 "$@"
  fi
}

# ── Paths ────────────────────────────────────────────────────────────────────
# BACKUP_LIB_DIR is this file's dir; REPO_ROOT is two levels up (tools/lib → repo).
BACKUP_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$BACKUP_LIB_DIR/../.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

# True for the hosted durable profiles (nas, digitalocean): image-mode services,
# durable Keycloak Postgres + server-mode Vault, and the NAS hardening overlays.
# Both share one code path so a DigitalOcean change can never drift from NAS.
is_hosted_profile() { [[ "$MODE" == "nas" || "$MODE" == "digitalocean" ]]; }

# ── Resolve the compose command for the selected mode ────────────────────────
# Mirrors start-container-stack.sh: MODE ∈ {local,nas,digitalocean}. ENV_FILE is
# optional for local; it defaults to nas.env for nas and do.env for digitalocean.
# Sets the COMPOSE_CMD array and MODE.
resolve_compose() {
  MODE="${MODE:-local}"
  # Default the env file and export the same interpolation vars that
  # start-container-stack.sh does, so any compose subcommand (ps/exec/down/up)
  # can render the config. DataHub fails closed without POSTGRES_PASSWORD, and
  # the Vault/Dapr wiring needs VAULT_TOKEN; hosted profiles supply real values
  # via nas.env / do.env.
  if is_hosted_profile; then
    SERVICES_FILE="docker-compose.services.images.yml"
    export ALERTMANAGER_CONFIG_FILE="${ALERTMANAGER_CONFIG_FILE:-runtime/config.yaml}"
    if [[ "$MODE" == "digitalocean" ]]; then
      ENV_FILE="${ENV_FILE:-$INFRA_DIR/do.env}"
    else
      ENV_FILE="${ENV_FILE:-$INFRA_DIR/nas.env}"
    fi
  else
    ENV_FILE="${ENV_FILE:-$INFRA_DIR/local-docker.env}"
    SERVICES_FILE="docker-compose.services.yml"
    export VAULT_TOKEN="${VAULT_TOKEN:-dev-only-token}"
    export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-fps}"
  fi

  local files=(
    "-f" "$INFRA_DIR/docker-compose.yaml"
    "-f" "$INFRA_DIR/$SERVICES_FILE"
    "-f" "$INFRA_DIR/docker-compose.dapr.yml"
  )
  if is_hosted_profile; then
    files+=("-f" "$INFRA_DIR/docker-compose.nas.yml")
    files+=("-f" "$INFRA_DIR/docker-compose.services.nas.yml")
  fi
  if [[ "$MODE" == "digitalocean" ]]; then
    files+=("-f" "$INFRA_DIR/docker-compose.digitalocean.yml")
  fi

  if [[ -n "${ENV_FILE:-}" && -f "$ENV_FILE" ]]; then
    COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" "${files[@]}")
  else
    COMPOSE_CMD=(docker compose --project-directory "$INFRA_DIR" "${files[@]}")
  fi
}

# Read a single KEY's value from the resolved env file WITHOUT sourcing it (so
# other secrets are never executed or exported). Returns empty if unset/absent.
# Strips optional surrounding single/double quotes; last assignment wins.
env_file_value() {
  local key="$1" file="${ENV_FILE:-}"
  [[ -n "$file" && -f "$file" ]] || return 0
  sed -n "s/^[[:space:]]*${key}=//p" "$file" | tail -1 \
    | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'\$//"
}

# Container id for a compose service (empty if not present/created).
_cid() { "${COMPOSE_CMD[@]}" ps -aq "$1" 2>/dev/null | head -1 || true; }

# True if a compose service has a running container.
_running() {
  local cid; cid="$(_cid "$1")"
  [[ -n "$cid" ]] && [[ "$(docker inspect -f '{{.State.Running}}' "$cid" 2>/dev/null)" == "true" ]]
}

# ── The durable stores this stack backs up ───────────────────────────────────
# MinIO and Vault use volume/snapshot tooling; the DBs use logical dumps. Each
# artifact name is stable so restore-drill.sh can find it.
MONGO_ARTIFACT="mongodb.archive.gz"
PG_DATAHUB_ARTIFACT="postgres-datahub.sql.gz"
KC_PG_ARTIFACT="keycloak-postgres.sql.gz"
MINIO_ARTIFACT="minio-data.tar.gz"
VAULT_SNAPSHOT_ARTIFACT="vault.snap"
VAULT_TAR_ARTIFACT="vault-raft.tar.gz"

# Helper image used for volume-level tar of MinIO/Vault (no host tooling needed).
BACKUP_HELPER_IMAGE="${BACKUP_HELPER_IMAGE:-alpine:3.20}"
