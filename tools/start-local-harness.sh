#!/bin/sh
# start-local-harness.sh — Start the FPS full-stack local test harness.
#
# Starts Docker Compose infrastructure, sets up Keycloak auth, launches Identity
# and eight Dapr-paired services in the background, then seeds demo data.
# Service logs go to logs/local-harness/. PIDs are saved for stop-local-harness.sh.
#
# Prerequisites:
#   - Docker Desktop running
#   - Dapr CLI >= 1.14 installed and initialised (dapr init)
#   - .NET 10.0.203 SDK from $HOME/.dotnet/dotnet on PATH
#
# Usage (from repo root):
#   ./tools/start-local-harness.sh
#   ./tools/start-local-harness.sh --skip-infra
#
# Stop:
#   ./tools/stop-local-harness.sh
#
# Full reset (removes Docker volumes):
#   ./tools/stop-local-harness.sh --reset

set -eu

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG_DIR="$REPO_ROOT/logs/local-harness"
PID_FILE="$LOG_DIR/pids"
REVISION_FILE="$LOG_DIR/revision"
SKIP_INFRA=false
INFRA_HOST="${FPS_INFRA_HOST:-localhost}"

log()  { printf '[harness] %s\n' "$*"; }
fail() { printf '[harness] ERROR: %s\n' "$*" >&2; exit 1; }

for arg in "$@"; do
  case "$arg" in
    --skip-infra) SKIP_INFRA=true ;;
    -h|--help)
      cat <<EOF
Usage:
  ./tools/start-local-harness.sh [--skip-infra]

Options:
  --skip-infra   Do not run docker compose up -d. Use when infrastructure is already running.
EOF
      exit 0
      ;;
    *) fail "Unknown argument: $arg" ;;
  esac
done

mkdir -p "$LOG_DIR"
: > "$PID_FILE"

current_revision() {
  if command -v git > /dev/null 2>&1; then
    git -C "$REPO_ROOT" rev-parse --verify HEAD 2>/dev/null || printf 'unknown\n'
  else
    printf 'unknown\n'
  fi
}

wait_port() {
  port="$1"
  label="$2"
  limit="${3:-60}"
  host="${4:-localhost}"
  i=0
  log "Waiting for $label on $host:$port..."
  while [ "$i" -lt "$limit" ]; do
    if nc -z "$host" "$port" 2>/dev/null; then
      log "$label ready"
      return 0
    fi
    i=$((i + 1))
    sleep 2
  done
  return 1
}

ensure_port_free() {
  port="$1"
  label="$2"
  host="${3:-localhost}"
  if nc -z "$host" "$port" 2>/dev/null; then
    printf '[harness] ERROR: %s port %s is already in use on %s\n' "$label" "$port" "$host" >&2
    printf '[harness]   Run: ./tools/stop-local-harness.sh --services-only\n' >&2
    printf '[harness]   Then retry: ./tools/start-local-harness.sh\n' >&2
    exit 1
  fi
}

require_port() {
  port="$1"
  label="$2"
  limit="${3:-60}"
  logfile="${4:-$LOG_DIR/service.log}"
  host="${5:-localhost}"
  wait_port "$port" "$label" "$limit" "$host" || {
    printf '[harness] ERROR: %s did not bind :%-4s within %ss\n' "$label" "$port" "$((limit * 2))" >&2
    printf '[harness]   Check log: %s\n' "$logfile" >&2
    printf '[harness]   Run: ./tools/stop-local-harness.sh\n' >&2
    exit 1
  }
}

require_process_running() {
  pid="$1"
  label="$2"
  logfile="$3"
  if ! kill -0 "$pid" 2>/dev/null; then
    printf '[harness] ERROR: %s process exited during startup\n' "$label" >&2
    printf '[harness]   Check log: %s\n' "$logfile" >&2
    tail -n 40 "$logfile" >&2 2>/dev/null || true
    exit 1
  fi
}

# ── Prerequisites ─────────────────────────────────────────────────────────────

command -v docker > /dev/null || fail "Docker not found. Start Docker Desktop first."
command -v dapr   > /dev/null || fail "Dapr CLI not found. Install: https://docs.dapr.io/getting-started/install-dapr-cli/ then run: dapr init"
command -v dotnet > /dev/null || fail "dotnet not found. Add \$HOME/.dotnet to PATH."

DOTNET_PATH="$(command -v dotnet)"
case "$DOTNET_PATH" in
  */usr/local/share/dotnet*) fail "Resolving system dotnet at $DOTNET_PATH; need \$HOME/.dotnet/dotnet (SDK 10.0.203). Prepend \$HOME/.dotnet to PATH." ;;
esac

# Refuse to start over stale app or Dapr sidecar processes. Otherwise the harness
# can seed against an old service process after `dapr run` fails validation.
for port_label in \
  "5192 Identity" \
  "5131 Booking" \
  "5157 Notification" \
  "5197 Profile" \
  "5161 Audit" \
  "5171 Reporting" \
  "5141 Configuration" \
  "5181 Customer" \
  "5211 DataHub" \
  "3601 Booking-Dapr-HTTP" \
  "3607 Notification-Dapr-HTTP" \
  "3617 Profile-Dapr-HTTP" \
  "3611 Audit-Dapr-HTTP" \
  "3621 Reporting-Dapr-HTTP" \
  "3631 Configuration-Dapr-HTTP" \
  "3641 Customer-Dapr-HTTP" \
  "3651 DataHub-Dapr-HTTP" \
  "50001 Booking-Dapr-GRPC" \
  "50007 Notification-Dapr-GRPC" \
  "50017 Profile-Dapr-GRPC" \
  "50011 Audit-Dapr-GRPC" \
  "50021 Reporting-Dapr-GRPC" \
  "50031 Configuration-Dapr-GRPC" \
  "50041 Customer-Dapr-GRPC" \
  "50151 DataHub-Dapr-GRPC"; do
  ensure_port_free "${port_label%% *}" "${port_label#* }"
done

# ── Docker Compose infrastructure ─────────────────────────────────────────────

if [ "$SKIP_INFRA" = true ]; then
  log "Skipping Docker Compose startup; expecting infrastructure to be running."
else
  log "Starting Docker Compose infrastructure..."
  docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" up -d
fi

# ── Keycloak health ───────────────────────────────────────────────────────────

require_port 8180 "Keycloak" 60 "docker compose logs keycloak" "$INFRA_HOST"
require_port 8200 "Vault"    60 "docker compose logs vault"    "$INFRA_HOST"

log "Ensuring MongoDB single-node replica set is initialized..."
MONGO_COMPOSE_FILE="$REPO_ROOT/code/infrastructure/docker-compose.yaml"
mongo_exec() {
  docker compose -f "$MONGO_COMPOSE_FILE" exec -T mongodb "$@"
}

# On a fresh Docker volume the compose `command:` override skips the mongo
# entrypoint that would normally honour MONGO_INITDB_ROOT_USERNAME/PASSWORD,
# so the admin user does not yet exist. Mongo's localhost exception lets us
# call rs.initiate() and createUser() without auth — but only until the first
# user is created. We use that one-shot window to bootstrap both.
for attempt in $(seq 1 30); do
  if mongo_exec mongosh -u admin -p admin --authenticationDatabase admin --quiet \
    --eval 'try { rs.status().ok } catch (e) { rs.initiate({_id:"rs0",members:[{_id:0,host:"localhost:27017"}]}).ok }' \
    2>/dev/null | grep -qE '^(1|true)$'; then
    break
  fi

  # Authenticated path failed. If the admin user is genuinely missing
  # (fresh volume), bootstrap via the localhost exception.
  if mongo_exec mongosh --quiet --eval 'db.getSiblingDB("admin").system.users.countDocuments({})' \
    2>/dev/null | grep -qE '^0$'; then
    log "  MongoDB has no users yet — bootstrapping replica set and admin via localhost exception..."
    mongo_exec mongosh --quiet --eval '
      try { rs.status(); }
      catch (e) { rs.initiate({_id:"rs0",members:[{_id:0,host:"localhost:27017"}]}); }
    ' >/dev/null 2>&1 || true

    for wait_primary in $(seq 1 15); do
      if mongo_exec mongosh --quiet --eval 'db.hello().isWritablePrimary' 2>/dev/null | grep -qE '^true$'; then
        break
      fi
      sleep 1
    done

    mongo_exec mongosh --quiet --eval '
      db.getSiblingDB("admin").createUser({
        user: "admin",
        pwd: "admin",
        roles: [{ role: "root", db: "admin" }]
      });
    ' >/dev/null 2>&1 || true
  fi

  if [ "$attempt" -eq 30 ]; then
    log "ERROR: MongoDB replica set did not initialize."
    log "  Inspect with: docker compose -f code/infrastructure/docker-compose.yaml logs mongodb"
    exit 1
  fi

  sleep 2
done

log "Seeding local Dapr secrets in Vault..."
VAULT_ADDR="${VAULT_ADDR:-http://localhost:8200}"
VAULT_TOKEN="${VAULT_TOKEN:-dev-only-token}"
seed_vault_secret() {
  path="$1"
  payload="$2"
  curl -sf \
    -H "X-Vault-Token: $VAULT_TOKEN" \
    -H "Content-Type: application/json" \
    -X POST \
    -d "$payload" \
    "$VAULT_ADDR/v1/secret/data/dapr/$path" > /dev/null
}
seed_vault_secret "mongodb-credentials" '{"data":{"username":"admin","password":"admin"}}'
seed_vault_secret "rabbitmq-credentials" '{"data":{"username":"admin","password":"admin"}}'
seed_vault_secret "minio-credentials" '{"data":{"accessKey":"minioadmin","secretKey":"minioadmin"}}'

# ── Auth setup ────────────────────────────────────────────────────────────────

log "Running dev-setup-auth.sh (realm import + demo users)..."
"$REPO_ROOT/tools/dev-setup-auth.sh"

# Source auth env vars so child processes inherit them
# shellcheck source=tools/dev-env.sh
. "$REPO_ROOT/tools/dev-env.sh"

# Build once before launching services. Running multiple `dotnet run` builds in
# parallel through Dapr is slow and can stall local startup on constrained hosts.
log "Building server solution..."
cd "$REPO_ROOT"
dotnet build code/server/FPS.sln --no-restore

# ── Services with Dapr sidecars ───────────────────────────────────────────────

log "Starting Identity, Booking, Notification, Profile, Audit, Reporting, Configuration, Customer, DataHub"
log "  with Dapr sidecars (logs -> $LOG_DIR/dapr-run.log)..."
cd "$REPO_ROOT"
dapr run -f dapr.yaml > "$LOG_DIR/dapr-run.log" 2>&1 &
DAPR_RUN_PID="$!"
echo "$DAPR_RUN_PID" >> "$PID_FILE"

sleep 2
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"

require_port 5192 "Identity"      90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5131 "Booking"       90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5157 "Notification"  90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5197 "Profile"       90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5161 "Audit"         90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5171 "Reporting"     90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5141 "Configuration" 90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5181 "Customer"      90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"
require_port 5211 "DataHub"       90 "$LOG_DIR/dapr-run.log"
require_process_running "$DAPR_RUN_PID" "Dapr multi-app run" "$LOG_DIR/dapr-run.log"

# ── Seed demo data ────────────────────────────────────────────────────────────

log "Seeding demo profile data..."
"$REPO_ROOT/tools/dev-seed.sh" || {
  printf '[harness] ERROR: Seed step failed.\n' >&2
  printf '[harness]   Services are running — fix the issue and re-run ./tools/dev-seed.sh\n' >&2
  printf '[harness]   Or run ./tools/stop-local-harness.sh to clean up.\n' >&2
  exit 1
}

# ── Summary ───────────────────────────────────────────────────────────────────

printf '\n'
printf '================================================\n'
printf ' FPS Local Harness — Ready\n'
printf '================================================\n'
printf ' Mobile gateway:  http://localhost:10000\n'
printf ' Identity:        http://localhost:5192\n'
printf ' Booking:         http://localhost:5131\n'
printf ' Notification:    http://localhost:5157\n'
printf ' Profile:         http://localhost:5197\n'
printf ' Audit:           http://localhost:5161\n'
printf ' Reporting:       http://localhost:5171\n'
printf ' Configuration:   http://localhost:5141\n'
printf ' Customer:        http://localhost:5181\n'
printf ' DataHub:         http://localhost:5211\n'
printf ' Logs:            %s/\n' "$LOG_DIR"
current_revision > "$REVISION_FILE"
printf '\n'
printf ' Smoke (run in a new shell):\n'
printf '   TOKEN=$(./tools/dev-auth.sh employee1)\n'
printf '   curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/me\n'
printf '   curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings\n'
printf '   curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count\n'
printf '   curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot\n'
printf '\n'
printf ' Stop:       ./tools/stop-local-harness.sh\n'
printf ' Full reset: ./tools/stop-local-harness.sh --reset\n'
printf '================================================\n'
