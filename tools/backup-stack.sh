#!/usr/bin/env bash
# tools/backup-stack.sh — automated backup of every durable store in the
# FairSpot Docker Compose stack (NAS production-readiness, #765).
#
# Backs up, using each store's native tooling and referencing only the
# containers' own injected credentials (no secrets on the host):
#
#   MongoDB (rs0)        mongodump --archive --gzip --oplog   -> mongodb.archive.gz
#   Postgres (DataHub)   pg_dump                              -> postgres-datahub.sql.gz
#   Keycloak Postgres    pg_dump (NAS only)                   -> keycloak-postgres.sql.gz
#   MinIO object storage volume tar                           -> minio-data.tar.gz
#   Vault (raft)         operator raft snapshot save          -> vault.snap
#                        (falls back to a volume tar when sealed / not raft)
#
# Each run writes a timestamped directory under --out with a SHA256SUMS integrity
# file and a manifest.json. Old runs beyond --retention are pruned. The output
# directory is git-ignored; NOTHING here belongs in the repo. Schedules, storage
# locations, and the encrypted off-box copy are operator concerns (private
# fairspot-platform runbook, #684) — this script only produces the artifacts.
#
# Usage:
#   ./tools/backup-stack.sh [--nas|--local] [--env-file PATH] [--out DIR] [--retention N]
#
#   --nas / --local    Which stack to back up (default: local).
#   --env-file PATH    Compose env file (NAS default: code/infrastructure/nas.env).
#   --out DIR          Backup root (default: ./backups).
#   --retention N      Keep the newest N runs, prune older (default: 7).
#
# VAULT_TOKEN (env or --env-file) is used for the raft snapshot; without it, and
# when Vault is sealed, the raft volume is tarred instead.
#
# Exit codes: 0 all requested stores backed up; 1 prerequisite/store failure.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=tools/lib/backup-common.sh
source "$SCRIPT_DIR/lib/backup-common.sh"

# ── Args ─────────────────────────────────────────────────────────────────────
MODE="local"
ENV_FILE=""
OUT="$REPO_ROOT/backups"
RETENTION=7

while [[ $# -gt 0 ]]; do
  case "$1" in
    --nas)        MODE="nas" ;;
    --local)      MODE="local" ;;
    --env-file)   ENV_FILE="${2:-}"; shift ;;
    --out)        OUT="${2:-}"; shift ;;
    --retention)  RETENTION="${2:-}"; shift ;;
    -h|--help)    sed -n '2,40p' "$0"; exit 0 ;;
    *)            die "Unknown argument: $1 (see --help)" ;;
  esac
  shift
done

command -v docker >/dev/null 2>&1 || die "docker is required"
[[ "$RETENTION" =~ ^[0-9]+$ && "$RETENTION" -ge 1 ]] || die "--retention must be a positive integer"

# Human-readable size of a file (portable).
_size() { du -h "$1" 2>/dev/null | cut -f1 || echo '?'; }

resolve_compose

# A UTC, sortable stamp. date is available on the host running the drill.
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="$OUT/$STAMP"
mkdir -p "$DEST"

log "Backing up the '$MODE' stack -> $DEST"

BACKED_UP=()   # store:artifact:method pairs recorded in the manifest
FAILED=()

# Record a produced artifact for the manifest.
_record() { BACKED_UP+=("$1|$2|$3"); }

# ── MongoDB (all Dapr state stores live here; rs0 replica set) ────────────────
backup_mongo() {
  _running mongodb || { warn "mongodb not running — skipping"; return; }
  log "MongoDB: mongodump --archive --gzip --oplog"
  # Credentials come from the container's own env; --oplog gives a consistent
  # point-in-time across the replica set. This is a full-instance dump (mongodump
  # has no --excludeDatabase); restore-drill.sh uses --nsExclude to skip
  # admin/config on the way back in, so the infra users/roles are never
  # re-applied (restoring admin.system.users mid-stream would reset the session
  # auth and break index creation).
  if "${COMPOSE_CMD[@]}" exec -T mongodb sh -c \
      'mongodump --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" \
         --authenticationDatabase admin --archive --gzip --oplog' \
      > "$DEST/$MONGO_ARTIFACT" 2>/dev/null; then
    ok "MongoDB -> $MONGO_ARTIFACT ($(_size "$DEST/$MONGO_ARTIFACT"))"
    _record mongodb "$MONGO_ARTIFACT" "mongodump --archive --gzip --oplog"
  else
    rm -f "$DEST/$MONGO_ARTIFACT"; FAILED+=(mongodb); warn "MongoDB backup failed"
  fi
}

# ── Postgres (DataHub read models + platform) ────────────────────────────────
backup_postgres() {
  _running postgres || { warn "postgres not running — skipping"; return; }
  log "Postgres (DataHub): pg_dump"
  if "${COMPOSE_CMD[@]}" exec -T postgres sh -c \
      'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' \
      | gzip > "$DEST/$PG_DATAHUB_ARTIFACT"; then
    ok "Postgres -> $PG_DATAHUB_ARTIFACT ($(_size "$DEST/$PG_DATAHUB_ARTIFACT"))"
    _record postgres "$PG_DATAHUB_ARTIFACT" "pg_dump --clean --if-exists"
  else
    rm -f "$DEST/$PG_DATAHUB_ARTIFACT"; FAILED+=(postgres); warn "Postgres backup failed"
  fi
}

# ── Keycloak Postgres (NAS durable identity; #768) ───────────────────────────
backup_keycloak_pg() {
  if ! _running keycloak-postgres; then
    [[ "$MODE" == "nas" ]] && warn "keycloak-postgres not running — skipping" \
                           || log "Keycloak uses ephemeral H2 in local mode — nothing to back up"
    return
  fi
  log "Keycloak Postgres: pg_dump"
  if "${COMPOSE_CMD[@]}" exec -T keycloak-postgres sh -c \
      'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' \
      | gzip > "$DEST/$KC_PG_ARTIFACT"; then
    ok "Keycloak Postgres -> $KC_PG_ARTIFACT ($(_size "$DEST/$KC_PG_ARTIFACT"))"
    _record keycloak-postgres "$KC_PG_ARTIFACT" "pg_dump --clean --if-exists"
  else
    rm -f "$DEST/$KC_PG_ARTIFACT"; FAILED+=(keycloak-postgres); warn "Keycloak Postgres backup failed"
  fi
}

# ── MinIO object storage (byte-exact volume tar via a helper container) ───────
backup_minio() {
  local cid; cid="$(_cid minio)"
  [[ -n "$cid" ]] || { warn "minio container absent — skipping"; return; }
  log "MinIO: volume tar of /data"
  if docker run --rm --volumes-from "$cid" -v "$DEST":/backup "$BACKUP_HELPER_IMAGE" \
      tar czf "/backup/$MINIO_ARTIFACT" -C /data . ; then
    ok "MinIO -> $MINIO_ARTIFACT ($(_size "$DEST/$MINIO_ARTIFACT"))"
    _record minio "$MINIO_ARTIFACT" "volume tar (/data)"
  else
    rm -f "$DEST/$MINIO_ARTIFACT"; FAILED+=(minio); warn "MinIO backup failed"
  fi
}

# ── Vault (native raft snapshot; volume-tar fallback when sealed/not raft) ────
backup_vault() {
  local cid; cid="$(_cid vault)"
  [[ -n "$cid" ]] || { warn "vault container absent — skipping"; return; }
  local token="${VAULT_TOKEN:-}"
  if [[ -n "$token" ]] && "${COMPOSE_CMD[@]}" exec -T -e VAULT_TOKEN="$token" vault sh -c \
        'vault operator raft snapshot save /tmp/v.snap 1>&2 && cat /tmp/v.snap && rm -f /tmp/v.snap' \
        > "$DEST/$VAULT_SNAPSHOT_ARTIFACT" 2>/dev/null && [[ -s "$DEST/$VAULT_SNAPSHOT_ARTIFACT" ]]; then
    log "Vault: raft snapshot"
    ok "Vault -> $VAULT_SNAPSHOT_ARTIFACT ($(_size "$DEST/$VAULT_SNAPSHOT_ARTIFACT")) [SENSITIVE]"
    _record vault "$VAULT_SNAPSHOT_ARTIFACT" "operator raft snapshot save"
    return
  fi
  rm -f "$DEST/$VAULT_SNAPSHOT_ARTIFACT"
  # Fallback: sealed, no token, or -dev (not raft). Tar the raft dir if present.
  log "Vault: raft snapshot unavailable — volume-tar fallback of /vault/file"
  if docker run --rm --volumes-from "$cid" -v "$DEST":/backup "$BACKUP_HELPER_IMAGE" \
        sh -c 'test -d /vault/file && tar czf "/backup/'"$VAULT_TAR_ARTIFACT"'" -C /vault/file . ' 2>/dev/null \
        && [[ -s "$DEST/$VAULT_TAR_ARTIFACT" ]]; then
    ok "Vault -> $VAULT_TAR_ARTIFACT ($(_size "$DEST/$VAULT_TAR_ARTIFACT")) [SENSITIVE, volume tar]"
    _record vault "$VAULT_TAR_ARTIFACT" "volume tar (/vault/file)"
  else
    rm -f "$DEST/$VAULT_TAR_ARTIFACT"
    warn "Vault has no durable raft storage (dev mode?) — nothing to back up"
  fi
}

backup_mongo
backup_postgres
backup_keycloak_pg
backup_minio
backup_vault

# ── Integrity: checksums over every artifact ─────────────────────────────────
log "Writing integrity checksums"
( cd "$DEST" && _sha256 ./* > SHA256SUMS 2>/dev/null ) || die "checksum step failed"
ok "SHA256SUMS written"

# ── Manifest (plain JSON, no jq) ─────────────────────────────────────────────
{
  echo '{'
  echo "  \"timestamp\": \"$STAMP\","
  echo "  \"mode\": \"$MODE\","
  echo "  \"stores\": ["
  entry_first=1
  for entry in ${BACKED_UP[@]+"${BACKED_UP[@]}"}; do
    IFS='|' read -r store artifact method <<< "$entry"
    [[ $entry_first -eq 1 ]] || echo ','
    entry_first=0
    printf '    {"store": "%s", "artifact": "%s", "method": "%s"}' "$store" "$artifact" "$method"
  done
  echo
  echo "  ]"
  echo '}'
} > "$DEST/manifest.json"
ok "manifest.json written (${#BACKED_UP[@]} stores)"

# ── Retention: prune runs beyond the newest N (bash 3.2-portable) ────────────
runs=()
while IFS= read -r d; do
  [[ -n "$d" ]] && runs+=("$d")
done < <(ls -1dt "$OUT"/*/ 2>/dev/null || true)
if (( ${#runs[@]} > RETENTION )); then
  for old in "${runs[@]:RETENTION}"; do
    log "Pruning old backup: $old"
    rm -rf "$old"
  done
fi

echo
if (( ${#FAILED[@]} > 0 )); then
  die "Backup completed with FAILURES: ${FAILED[*]}"
fi
ok "Backup complete: $DEST"
echo "  Stores: ${#BACKED_UP[@]} | Retention: keep $RETENTION | Mode: $MODE"
echo "  Restore drill: ./tools/restore-drill.sh --from \"$DEST\"${ENV_FILE:+ --env-file \"$ENV_FILE\"}${MODE:+ --$MODE}"
