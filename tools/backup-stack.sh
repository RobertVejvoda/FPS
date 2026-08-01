#!/usr/bin/env bash
# tools/backup-stack.sh — automated backup of every durable store in the
# FairSpot Docker Compose stack (NAS production-readiness, #765).
#
# Backs up, using each store's native tooling and referencing only the
# containers' own injected credentials (no secrets on the host):
#
#   MongoDB (rs0)        mongodump --archive --gzip           -> mongodb.archive.gz
#   Postgres (DataHub)   pg_dump                              -> postgres-datahub.sql.gz
#   Keycloak Postgres    pg_dump (NAS only)                   -> keycloak-postgres.sql.gz
#   MinIO object storage volume tar                           -> minio-data.tar.gz
#   Vault (raft)         operator raft snapshot save          -> vault.snap
#
# The Mongo dump is a full-instance logical dump (per-collection consistent). It
# is NOT a global point-in-time: --oplog conflicts with the admin/config exclude
# the restore requires, so for a busy instance use --quiesce (stops the app
# writers around the dumps) or back up in a low-write window.
#
# Vault: only the native raft snapshot is a valid recovery backup. In --nas mode
# the script FAILS CLOSED if it cannot take one (no live-directory tar, which
# would be inconsistent). A local -dev Vault has no durable secrets.
#
# Each run writes a timestamped directory under --out with a SHA256SUMS integrity
# file and a manifest.json. Old runs beyond --retention are pruned. The output
# directory is git-ignored; NOTHING here belongs in the repo. Schedules, storage
# locations, and the encrypted off-box copy are operator concerns (private
# fairspot-platform runbook, #684) — this script only produces the artifacts.
#
# Usage:
#   ./tools/backup-stack.sh [--nas|--digitalocean|--local] [--env-file PATH]
#                           [--out DIR] [--retention N] [--quiesce]
#
#   --nas / --digitalocean / --local
#                      Which stack to back up (default: local). --nas and
#                      --digitalocean are the hosted durable profiles.
#   --env-file PATH    Compose env file (NAS default: code/infrastructure/nas.env;
#                      DigitalOcean default: code/infrastructure/do.env).
#   --out DIR          Backup root (default: ./backups).
#   --retention N      Keep the newest N runs, prune older (default: 7).
#   --quiesce          Stop the currently running writers (fairspot-* app
#                      services + keycloak) around the dumps for a consistent
#                      snapshot, then resume only those services (trap-guarded).
#
# VAULT_TOKEN (shell env or --env-file) authenticates the raft snapshot. It must
# carry the sys/storage/raft/snapshot policy; if the stack's Dapr token is
# least-privilege (KV read only), export a snapshot-capable VAULT_TOKEN for the
# backup. Vault RESTORE is a manual DR runbook step (#684), not part of this
# script — its unseal keys are split-knowledge secrets.
#
# Exit codes: 0 all requested stores backed up; 1 prerequisite/store failure
#             (in a hosted profile, --nas/--digitalocean, a Vault it cannot
#             snapshot is a failure).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=tools/lib/backup-common.sh
source "$SCRIPT_DIR/lib/backup-common.sh"

# ── Args ─────────────────────────────────────────────────────────────────────
MODE="local"
ENV_FILE=""
OUT="$REPO_ROOT/backups"
RETENTION=7
QUIESCE=""   # non-empty when --quiesce: stop app writers around the dumps

while [[ $# -gt 0 ]]; do
  case "$1" in
    --nas)          MODE="nas" ;;
    --digitalocean) MODE="digitalocean" ;;
    --local)        MODE="local" ;;
    --env-file)     ENV_FILE="${2:-}"; shift ;;
    --out)          OUT="${2:-}"; shift ;;
    --retention)    RETENTION="${2:-}"; shift ;;
    --quiesce)      QUIESCE="true" ;;
    -h|--help)      sed -n '2,45p' "$0"; exit 0 ;;
    *)              die "Unknown argument: $1 (see --help)" ;;
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

# ── Optional quiesce: stop the writers so logical dumps are consistent ────────
# Everything that writes to a backed-up store must pause: the fairspot-* app
# services (Mongo/Postgres via Dapr) AND keycloak (its Postgres store). The
# stores themselves stay up; the trap guarantees writers resume even on failure.
APP_SERVICES=()
_quiesce_start() {
  [[ -n "$QUIESCE" ]] || return 0
  local svc running_services
  if ! running_services="$("${COMPOSE_CMD[@]}" ps --services --status running 2>/dev/null)"; then
    die "quiesce: could not determine which writer services are running"
  fi
  while IFS= read -r svc; do
    case "$svc" in
      fairspot-*|keycloak) APP_SERVICES+=("$svc") ;;
    esac
  done <<< "$running_services"
  if (( ${#APP_SERVICES[@]} > 0 )); then
    log "Quiescing writers: ${APP_SERVICES[*]}"
    trap _quiesce_stop EXIT
    "${COMPOSE_CMD[@]}" stop "${APP_SERVICES[@]}" >/dev/null 2>&1 || warn "quiesce: some services did not stop"
  else
    warn "quiesce: no writer services found to stop"
  fi
}
_quiesce_stop() {
  (( ${#APP_SERVICES[@]} > 0 )) || return 0
  log "Resuming app writers"
  "${COMPOSE_CMD[@]}" start "${APP_SERVICES[@]}" >/dev/null 2>&1 || warn "quiesce: some services did not restart — check the stack"
}

# ── MongoDB (all Dapr state stores live here; rs0 replica set) ────────────────
backup_mongo() {
  _running mongodb || { warn "mongodb not running — skipping"; return; }
  log "MongoDB: mongodump --archive --gzip"
  # Credentials come from the container's own env. This is a full-instance
  # logical dump (mongodump has no --excludeDatabase, so admin/config are dumped
  # and skipped at restore via --nsExclude — restoring admin.system.users
  # mid-stream would reset the session auth and break index creation).
  #
  # NOT --oplog: --oplog is incompatible with the --nsExclude the restore needs,
  # so this is a per-collection snapshot, not a global point-in-time. For a busy
  # instance run with --quiesce (stops the app writers around the dump) so the
  # snapshot is consistent; otherwise back up in a low-write window.
  if "${COMPOSE_CMD[@]}" exec -T mongodb sh -c \
      'mongodump --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" \
         --authenticationDatabase admin --archive --gzip' \
      > "$DEST/$MONGO_ARTIFACT" 2>/dev/null; then
    ok "MongoDB -> $MONGO_ARTIFACT ($(_size "$DEST/$MONGO_ARTIFACT"))${QUIESCE:+ (quiesced)}"
    _record mongodb "$MONGO_ARTIFACT" "mongodump --archive --gzip${QUIESCE:+ (quiesced)}"
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

# ── Keycloak Postgres (hosted durable identity; #768) ────────────────────────
backup_keycloak_pg() {
  if ! _running keycloak-postgres; then
    is_hosted_profile && warn "keycloak-postgres not running — skipping" \
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

# ── Vault (native raft snapshot; NAS fails closed if it can't take one) ───────
backup_vault() {
  local cid; cid="$(_cid vault)"
  [[ -n "$cid" ]] || { warn "vault container absent — skipping"; return; }
  # Token from the shell env OR the resolved --env-file (compose gets the file,
  # this process does not — so read the one key we need, without leaking others).
  local token="${VAULT_TOKEN:-}"
  [[ -z "$token" ]] && token="$(env_file_value VAULT_TOKEN)"

  # The native raft snapshot is the ONLY valid recovery backup for server mode.
  if [[ -n "$token" ]] && "${COMPOSE_CMD[@]}" exec -T -e VAULT_TOKEN="$token" vault sh -c \
        'vault operator raft snapshot save /tmp/v.snap 1>&2 && cat /tmp/v.snap && rm -f /tmp/v.snap' \
        > "$DEST/$VAULT_SNAPSHOT_ARTIFACT" 2>/dev/null && [[ -s "$DEST/$VAULT_SNAPSHOT_ARTIFACT" ]]; then
    log "Vault: raft snapshot"
    ok "Vault -> $VAULT_SNAPSHOT_ARTIFACT ($(_size "$DEST/$VAULT_SNAPSHOT_ARTIFACT")) [SENSITIVE]"
    _record vault "$VAULT_SNAPSHOT_ARTIFACT" "operator raft snapshot save"
    return
  fi
  rm -f "$DEST/$VAULT_SNAPSHOT_ARTIFACT"

  # Hosted server mode MUST NOT silently fall back to a live-directory tar:
  # tarring /vault/file while Vault is writing produces an inconsistent,
  # unrestorable artifact that only looks like a backup. Fail closed so the gap
  # is visible. Applies to every hosted profile (nas, digitalocean).
  if is_hosted_profile; then
    FAILED+=(vault)
    if [[ -z "$token" ]]; then
      warn "Vault: no VAULT_TOKEN (shell env or --env-file) — cannot take a raft snapshot. FAILING CLOSED."
    else
      warn "Vault: raft snapshot failed (sealed, or token lacks the snapshot policy). FAILING CLOSED."
    fi
    return
  fi

  # Local/dev only: -dev Vault has no durable raft storage. Tar /vault/file only
  # if it actually holds data (an empty dev dir means there is nothing to back up).
  log "Vault (local/dev): no raft snapshot — checking for durable volume data"
  if docker run --rm --volumes-from "$cid" -v "$DEST":/backup "$BACKUP_HELPER_IMAGE" \
        sh -c 'test -n "$(ls -A /vault/file 2>/dev/null)" && tar czf "/backup/'"$VAULT_TAR_ARTIFACT"'" -C /vault/file . ' 2>/dev/null \
        && [[ -s "$DEST/$VAULT_TAR_ARTIFACT" ]]; then
    ok "Vault -> $VAULT_TAR_ARTIFACT ($(_size "$DEST/$VAULT_TAR_ARTIFACT")) [SENSITIVE, local/dev volume tar]"
    _record vault "$VAULT_TAR_ARTIFACT" "volume tar (/vault/file, local/dev)"
  else
    rm -f "$DEST/$VAULT_TAR_ARTIFACT"
    warn "Vault (local/dev): no durable secret material — nothing to back up"
  fi
}

_quiesce_start
backup_mongo
backup_postgres
backup_keycloak_pg
backup_minio
backup_vault
_quiesce_stop; trap - EXIT; APP_SERVICES=()

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
