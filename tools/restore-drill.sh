#!/usr/bin/env bash
# tools/restore-drill.sh — rebuild the FairSpot stack from a backup and prove
# recovery (NAS production-readiness, #765).
#
# The drill: verify a backup's integrity -> DESTROY the current stack + volumes
# -> bring infra back up -> restore every store from the backup -> bring the
# services up -> run the hosted smoke -> assert data actually returned. This is
# how we evidence that backups are restorable, not just producible.
#
#   MongoDB            mongorestore --archive --gzip --drop --nsExclude admin/config
#   Postgres/DataHub   psql < dump   (dump is --clean --if-exists)
#   Keycloak Postgres  psql < dump   (NAS only)
#   MinIO              restore volume tar into a stopped minio, then start
#   Vault              NOT auto-restored — secret-store DR (init/unseal/raft
#                      snapshot restore/re-unseal) is a human-supervised manual
#                      runbook step (#684); unseal keys must not be handled by an
#                      unattended script. The drill prints the exact steps.
#
# So this drill automates recovery of the durable DATA, OBJECT, and IDENTITY
# stores; Vault secret-store restore is a declared manual step (see #684).
#
# DESTRUCTIVE. Requires --yes. Targets the LOCAL stack by default. A hosted
# restore overwrites live customer data, so it refuses each hosted profile unless
# its OWN force flag is given: --nas needs --force-nas, --digitalocean needs
# --force-digitalocean (a DigitalOcean restore never falls through to NAS or
# local behavior).
#
# Usage:
#   ./tools/restore-drill.sh --from <backup-dir> [--yes]
#       [--local | --nas --force-nas | --digitalocean --force-digitalocean]
#       [--env-file PATH] [--skip-smoke]
#
# Exit codes: 0 data/object/identity stores restored and assertions passed; the
#             hosted smoke also passed unless DigitalOcean explicitly deferred
#             it at the manual Vault DR boundary. 1 means a hard failure.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=tools/lib/backup-common.sh
source "$SCRIPT_DIR/lib/backup-common.sh"

# ── Args ─────────────────────────────────────────────────────────────────────
MODE="local"
ENV_FILE=""
FROM=""
CONFIRMED=false
FORCE_NAS=false
FORCE_DO=false
SKIP_SMOKE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from)                FROM="${2:-}"; shift ;;
    --nas)                 MODE="nas" ;;
    --digitalocean)        MODE="digitalocean" ;;
    --local)               MODE="local" ;;
    --force-nas)           FORCE_NAS=true ;;
    --force-digitalocean)  FORCE_DO=true ;;
    --env-file)            ENV_FILE="${2:-}"; shift ;;
    --yes)                 CONFIRMED=true ;;
    --skip-smoke)          SKIP_SMOKE=true ;;
    -h|--help)             sed -n '2,32p' "$0"; exit 0 ;;
    *)                     die "Unknown argument: $1 (see --help)" ;;
  esac
  shift
done

command -v docker >/dev/null 2>&1 || die "docker is required"
[[ -n "$FROM" ]]     || die "--from <backup-dir> is required"
[[ -d "$FROM" ]]     || die "backup dir not found: $FROM"
# Absolute path: docker -v bind mounts (MinIO restore) reject relative paths.
FROM="$(cd "$FROM" && pwd)"
[[ -f "$FROM/manifest.json" ]] || die "no manifest.json in $FROM — not a backup dir"

if [[ "$MODE" == "nas" && "$FORCE_NAS" != "true" ]]; then
  die "Refusing to run a restore drill against NAS (it overwrites live data). Add --force-nas if you really mean it."
fi
if [[ "$MODE" == "digitalocean" && "$FORCE_DO" != "true" ]]; then
  die "Refusing to run a restore drill against the DigitalOcean profile (it overwrites live data). Add --force-digitalocean if you really mean it."
fi
if [[ "$CONFIRMED" != "true" ]]; then
  die "This DESTROYS the '$MODE' stack and its volumes, then restores from $FROM. Re-run with --yes to proceed."
fi

resolve_compose

# ── 1. Integrity check ───────────────────────────────────────────────────────
log "Verifying backup integrity ($FROM)"
( cd "$FROM" && _sha256 -c SHA256SUMS >/dev/null 2>&1 ) \
  || die "SHA256SUMS verification FAILED — backup is corrupt or altered"
ok "Checksums verified"

_has() { [[ -f "$FROM/$1" ]]; }

# ── 2. Destroy the stack (volumes included) ──────────────────────────────────
log "Tearing down the '$MODE' stack and wiping volumes (down -v)"
"${COMPOSE_CMD[@]}" down -v --remove-orphans || true
ok "Stack + volumes removed"

# ── 3. Bring infra back up (clean volumes) ───────────────────────────────────
INFRA_SERVICES=(mongodb mongodb-init postgres minio vault)
is_hosted_profile && INFRA_SERVICES+=(keycloak-postgres)
log "Starting infra: ${INFRA_SERVICES[*]}"
"${COMPOSE_CMD[@]}" up -d "${INFRA_SERVICES[@]}"

# Wait for the stores we will restore into to report healthy/running.
_wait_healthy() {
  local svc="$1" timeout="${2:-90}" cid state
  local waited=0
  while (( waited < timeout )); do
    cid="$(_cid "$svc")"
    if [[ -n "$cid" ]]; then
      state="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2>/dev/null || true)"
      [[ "$state" == "healthy" || "$state" == "running" ]] && { ok "$svc $state"; return 0; }
    fi
    sleep 3; waited=$((waited+3))
  done
  die "$svc did not become healthy within ${timeout}s"
}
_wait_healthy mongodb
_wait_healthy postgres
_wait_healthy minio
is_hosted_profile && _wait_healthy keycloak-postgres

# ── 4. Restore each store ────────────────────────────────────────────────────
if _has "$MONGO_ARTIFACT"; then
  log "MongoDB: mongorestore --drop"
  # Defence in depth: never restore admin/config (infra users/roles) even if an
  # older archive contains them — restoring users mid-stream breaks auth/indexes.
  "${COMPOSE_CMD[@]}" exec -T mongodb sh -c \
    'mongorestore --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" \
       --authenticationDatabase admin --nsExclude "admin.*" --nsExclude "config.*" \
       --archive --gzip --drop' \
    < "$FROM/$MONGO_ARTIFACT"
  ok "MongoDB restored"
fi

if _has "$PG_DATAHUB_ARTIFACT"; then
  log "Postgres (DataHub): psql restore"
  gunzip -c "$FROM/$PG_DATAHUB_ARTIFACT" | "${COMPOSE_CMD[@]}" exec -T postgres sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" psql -q -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null'
  ok "Postgres restored"
fi

if is_hosted_profile && _has "$KC_PG_ARTIFACT"; then
  log "Keycloak Postgres: psql restore"
  gunzip -c "$FROM/$KC_PG_ARTIFACT" | "${COMPOSE_CMD[@]}" exec -T keycloak-postgres sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" psql -q -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null'
  ok "Keycloak Postgres restored"
fi

if _has "$MINIO_ARTIFACT"; then
  log "MinIO: restore volume tar into /data"
  local_minio_cid="$(_cid minio)"
  [[ -n "$local_minio_cid" ]] || die "minio container missing for restore"
  "${COMPOSE_CMD[@]}" stop minio >/dev/null
  docker run --rm --volumes-from "$local_minio_cid" -v "$FROM":/backup "$BACKUP_HELPER_IMAGE" \
    sh -c 'rm -rf /data/* /data/..?* 2>/dev/null; tar xzf "/backup/'"$MINIO_ARTIFACT"'" -C /data'
  "${COMPOSE_CMD[@]}" start minio >/dev/null
  _wait_healthy minio
  ok "MinIO restored"
fi

# ── Vault: secret-store restore is a MANUAL DR runbook step (#684) ───────────
# A fresh server-mode Vault must be initialised AND unsealed before a raft
# snapshot restore can authenticate, and the unseal keys are the operator's most
# sensitive split-knowledge secret — they must not be consumed by an unattended
# script. So this drill deliberately does NOT auto-restore Vault: it reconstructs
# the data, object, and identity stores and prints the exact manual Vault DR
# steps. (The Vault backup artifact is produced automatically by
# backup-stack.sh; only its restore is human-supervised.)
VAULT_MANUAL=false
VAULT_CONTAINER_SNAPSHOT="/tmp/restore-drill-vault.snap"

# Print a fully shell-escaped, copy-pasteable command line for an argv array.
_print_cmd() {
  local step="$1"; shift
  local out="" arg
  for arg in "$@"; do
    printf -v arg '%q' "$arg"
    out="$out $arg"
  done
  echo "     ${step}.${out}"
}

if _has "$VAULT_SNAPSHOT_ARTIFACT"; then
  VAULT_MANUAL=true
  warn "Vault raft snapshot present — secret-store restore is a MANUAL DR step, not run by this drill:"
  _print_cmd 1 "${COMPOSE_CMD[@]}" exec vault vault operator init
  echo "        (record keys + root token)"
  _print_cmd 2 "${COMPOSE_CMD[@]}" exec vault vault operator unseal
  echo "        (with the new keys)"
  _print_cmd 3 "${COMPOSE_CMD[@]}" cp "$FROM/$VAULT_SNAPSHOT_ARTIFACT" "vault:$VAULT_CONTAINER_SNAPSHOT"
  _print_cmd 4 "${COMPOSE_CMD[@]}" exec vault vault operator raft snapshot restore -force "$VAULT_CONTAINER_SNAPSHOT"
  _print_cmd 5 "${COMPOSE_CMD[@]}" exec vault vault operator unseal
  echo "        (with the SNAPSHOT cluster's original keys)"
  echo "     6. verify a canary secret reads back"
  echo "     Full procedure + evidence: private runbook (#684)."
elif _has "$VAULT_TAR_ARTIFACT"; then
  warn "Vault volume-tar present (local/dev, no durable secrets) — nothing to restore."
fi

# ── 5. DigitalOcean: Vault seal/init boundary before full-stack start ─────────
# A DigitalOcean server-mode Vault is sealed/uninitialized after `down -v`.
# The full-stack start path (start-container-stack.sh --digitalocean) calls
# require_vault_unsealed and will exit immediately on a fresh node. Attempting
# an automated full-stack smoke here would race the operator or silently fail.
# The drill scope therefore stops at the data/object/identity assertion point;
# full-stack smoke requires manual Vault init + unseal first (see notes above).
if [[ "$MODE" == "digitalocean" ]] && [[ "$SKIP_SMOKE" != "true" ]]; then
  warn "DigitalOcean: the full-stack smoke requires an initialized, unsealed Vault."
  warn "  A fresh server-mode Vault is sealed/uninitialized after 'down -v'."
  warn "  This run continues through the restored data/object/identity assertions."
  warn "  After completing the manual Vault DR steps above, start the stack and"
  warn "  run the hosted smoke manually (do not rerun this destructive drill):"
  warn "    ./tools/start-container-stack.sh --digitalocean [--env-file PATH]"
  warn ""
  warn "Stopping drill at the data/object/identity store assertions (Vault DR boundary)."
  SKIP_SMOKE=true
fi

# ── 6 (was 5). Bring the full stack up (only needed for the smoke) ───────────
# The data-return assertions below query the restored stores directly, which are
# already up from step 3 — so a --skip-smoke drill proves recovery without the
# full app stack (and without services like Grafana that the smoke would need).
if [[ "$SKIP_SMOKE" != "true" ]]; then
  log "Starting the full stack"
  "${COMPOSE_CMD[@]}" up -d
fi

# ── 6. Prove data returned (not just services healthy) ───────────────────────
log "Asserting restored data is present"
DATA_OK=true
if _has "$MONGO_ARTIFACT"; then
  # Count non-system databases; a restored stack has application data.
  mongo_dbs="$("${COMPOSE_CMD[@]}" exec -T mongodb sh -c \
    'mongosh --quiet --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" \
       --authenticationDatabase admin --eval "db.adminCommand({listDatabases:1}).databases.filter(d => ![\"admin\",\"config\",\"local\"].includes(d.name)).length"' \
    2>/dev/null | tr -dc '0-9')"
  if [[ -n "$mongo_dbs" && "$mongo_dbs" -gt 0 ]]; then ok "MongoDB: $mongo_dbs application database(s) restored"
  else warn "MongoDB: no application databases found after restore"; DATA_OK=false; fi
fi
if _has "$PG_DATAHUB_ARTIFACT"; then
  pg_tables="$("${COMPOSE_CMD[@]}" exec -T postgres sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" psql -tA -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='"'"'public'"'"';"' \
    2>/dev/null | tr -dc '0-9')"
  if [[ -n "$pg_tables" && "$pg_tables" -gt 0 ]]; then ok "Postgres: $pg_tables table(s) restored"
  else warn "Postgres: no public tables found after restore"; DATA_OK=false; fi
fi

# ── 7. Hosted smoke ──────────────────────────────────────────────────────────
if [[ "$SKIP_SMOKE" == "true" ]]; then
  warn "Smoke skipped (--skip-smoke)"
else
  log "Running stack smoke (health/readiness)"
  smoke_args=()
  # Pass the hosted profile flag through so the smoke resolves the same compose
  # files (nas -> --nas, digitalocean -> --digitalocean).
  is_hosted_profile && smoke_args+=("--$MODE")
  [[ -n "$ENV_FILE" ]] && smoke_args+=(--env-file "$ENV_FILE")
  if "$SCRIPT_DIR/start-container-stack.sh" ${smoke_args[@]+"${smoke_args[@]}"} --skip-e2e; then
    ok "Stack smoke passed"
  else
    die "Stack smoke FAILED after restore"
  fi
fi

echo
[[ "$DATA_OK" == "true" ]] || die "Restore drill: data-return assertions FAILED"

msg="Restore drill PASSED — data/object/identity stores rebuilt from $FROM; data returned"
if [[ "$SKIP_SMOKE" != "true" ]]; then
  msg="$msg; smoke green"
elif [[ "$MODE" == "digitalocean" ]]; then
  msg="$msg; full-stack smoke deferred (Vault DR boundary — complete manual Vault init/unseal first)"
fi
ok "$msg"
if [[ "$VAULT_MANUAL" == "true" ]]; then
  warn "SCOPE: Vault secret-store restore is a separate MANUAL DR step (see above / #684)"
  warn "       — it is NOT covered by this automated drill."
fi
echo "  Record the drill (date, scope, result, operator) in the private runbook (#684)."
