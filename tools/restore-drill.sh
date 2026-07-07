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
#   Vault              operator raft snapshot restore, in --nas --force-nas mode
#                      with VAULT_TOKEN + VAULT_UNSEAL_KEYS supplied. If a raft
#                      snapshot is present but not restored, the drill reports
#                      INCOMPLETE (exit 2) — it never claims full recovery with a
#                      blank Vault. A local -dev Vault holds no durable secrets.
#
# DESTRUCTIVE. Requires --yes. Targets the LOCAL stack by default; refuses --nas
# unless --force-nas is also given (a NAS restore overwrites live customer data).
#
# Usage:
#   ./tools/restore-drill.sh --from <backup-dir> [--yes] [--local|--nas --force-nas]
#                            [--env-file PATH] [--skip-smoke]
#
# NAS Vault restore also needs, in the environment or --env-file:
#   VAULT_TOKEN         a root/recovery token for the freshly-initialised node
#   VAULT_UNSEAL_KEYS   comma-separated unseal keys from the snapshot's cluster
#
# Exit codes: 0 restore + smoke + data-return passed; 2 stores restored but Vault
#             secret-store recovery INCOMPLETE; 1 a hard failure.

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
SKIP_SMOKE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from)       FROM="${2:-}"; shift ;;
    --nas)        MODE="nas" ;;
    --local)      MODE="local" ;;
    --force-nas)  FORCE_NAS=true ;;
    --env-file)   ENV_FILE="${2:-}"; shift ;;
    --yes)        CONFIRMED=true ;;
    --skip-smoke) SKIP_SMOKE=true ;;
    -h|--help)    sed -n '2,30p' "$0"; exit 0 ;;
    *)            die "Unknown argument: $1 (see --help)" ;;
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
[[ "$MODE" == "nas" ]] && INFRA_SERVICES+=(keycloak-postgres)
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
[[ "$MODE" == "nas" ]] && _wait_healthy keycloak-postgres

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

if [[ "$MODE" == "nas" ]] && _has "$KC_PG_ARTIFACT"; then
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

# ── Vault: raft snapshot restore (NAS/server-mode; guarded) ──────────────────
# Returns 0 only if the snapshot restores and Vault ends unsealed. The target
# node must be initialised + unsealed first (operator/runbook #684). Any failure
# leaves VAULT_STATUS=INCOMPLETE so the drill never reports a blank Vault as OK.
vault_restore_snapshot() {
  local token="$1" keys_csv="$2" vcid key
  vcid="$(_cid vault)"; [[ -n "$vcid" ]] || return 1
  docker cp "$FROM/$VAULT_SNAPSHOT_ARTIFACT" "$vcid:/tmp/restore.snap" >/dev/null 2>&1 || return 1
  "${COMPOSE_CMD[@]}" exec -T -e VAULT_TOKEN="$token" vault \
    vault operator raft snapshot restore -force /tmp/restore.snap >/dev/null 2>&1 || return 1
  # A snapshot restore replaces the keyring, re-sealing the node — re-unseal with
  # the snapshot cluster's original keys.
  local IFS=','
  # shellcheck disable=SC2086  # intentional comma word-splitting of the key list
  for key in $keys_csv; do
    [[ -n "$key" ]] && "${COMPOSE_CMD[@]}" exec -T vault vault operator unseal "$key" >/dev/null 2>&1 || true
  done
  unset IFS
  "${COMPOSE_CMD[@]}" exec -T vault sh -c 'vault status -format=json 2>/dev/null' \
    | grep -q '"sealed"[[:space:]]*:[[:space:]]*false' || return 1
  "${COMPOSE_CMD[@]}" exec -T vault rm -f /tmp/restore.snap >/dev/null 2>&1 || true
  return 0
}

VAULT_STATUS="n/a"
if _has "$VAULT_SNAPSHOT_ARTIFACT"; then
  vtoken="${VAULT_TOKEN:-$(env_file_value VAULT_TOKEN)}"
  vkeys="${VAULT_UNSEAL_KEYS:-$(env_file_value VAULT_UNSEAL_KEYS)}"
  if [[ "$MODE" == "nas" && "$FORCE_NAS" == "true" && -n "$vtoken" && -n "$vkeys" ]] \
       && vault_restore_snapshot "$vtoken" "$vkeys"; then
    VAULT_STATUS="restored"; ok "Vault raft snapshot restored and unsealed"
  else
    VAULT_STATUS="INCOMPLETE"
    warn "Vault raft snapshot present but NOT restored."
    warn "Needs --nas --force-nas, an initialised+unsealed Vault, VAULT_TOKEN and VAULT_UNSEAL_KEYS."
    warn "Secret-store recovery is therefore NOT proven by this drill (runbook #684)."
  fi
elif _has "$VAULT_TAR_ARTIFACT"; then
  warn "Vault volume-tar present (local/dev, no durable secrets) — nothing to restore."
fi

# ── 5. Bring the full stack up (only needed for the smoke) ───────────────────
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
  [[ "$MODE" == "nas" ]] && smoke_args+=(--nas)
  [[ -n "$ENV_FILE" ]] && smoke_args+=(--env-file "$ENV_FILE")
  if "$SCRIPT_DIR/start-container-stack.sh" ${smoke_args[@]+"${smoke_args[@]}"} --skip-e2e; then
    ok "Stack smoke passed"
  else
    die "Stack smoke FAILED after restore"
  fi
fi

echo
[[ "$DATA_OK" == "true" ]] || die "Restore drill: data-return assertions FAILED"

if [[ "$VAULT_STATUS" == "INCOMPLETE" ]]; then
  warn "Restore drill INCOMPLETE — stores restored and data returned, but Vault"
  warn "secret-store recovery was NOT performed. Recovery is not fully proven."
  echo "  Complete the NAS Vault raft-snapshot restore + unseal, then re-run, or"
  echo "  record the manual Vault restore evidence in the private runbook (#684)."
  exit 2
fi

msg="Restore drill PASSED — stack rebuilt from $FROM; data returned"
[[ "$SKIP_SMOKE" != "true" ]] && msg="$msg; smoke green"
[[ "$VAULT_STATUS" == "restored" ]] && msg="$msg; vault restored"
ok "$msg"
echo "  Record the drill (date, scope, result, operator) in the private runbook (#684)."
