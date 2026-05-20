#!/bin/sh
# start-local-harness.sh — Start the FPS full-stack local test harness.
#
# Starts Docker Compose infrastructure, sets up Keycloak auth, launches Identity
# and six Dapr-paired services in the background, then seeds demo data.
# Service logs go to logs/local-harness/. PIDs are saved for stop-local-harness.sh.
#
# Prerequisites:
#   - Docker Desktop running
#   - Dapr CLI >= 1.12 installed and initialised (dapr init)
#   - .NET 10.0.203 SDK from $HOME/.dotnet/dotnet on PATH
#
# Usage (from repo root):
#   ./tools/start-local-harness.sh
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

mkdir -p "$LOG_DIR"
: > "$PID_FILE"

log()  { printf '[harness] %s\n' "$*"; }
fail() { printf '[harness] ERROR: %s\n' "$*" >&2; exit 1; }

wait_port() {
  port="$1"
  label="$2"
  limit="${3:-60}"
  i=0
  log "Waiting for $label on :$port..."
  while [ "$i" -lt "$limit" ]; do
    if nc -z localhost "$port" 2>/dev/null; then
      log "$label ready"
      return 0
    fi
    i=$((i + 1))
    sleep 2
  done
  log "WARNING: $label (:$port) did not become ready in time — check $LOG_DIR/"
  return 0
}

# ── Prerequisites ─────────────────────────────────────────────────────────────

command -v docker > /dev/null || fail "Docker not found. Start Docker Desktop first."
command -v dapr   > /dev/null || fail "Dapr CLI not found. Install: https://docs.dapr.io/getting-started/install-dapr-cli/ then run: dapr init"
command -v dotnet > /dev/null || fail "dotnet not found. Add \$HOME/.dotnet to PATH."

DOTNET_PATH="$(command -v dotnet)"
case "$DOTNET_PATH" in
  */usr/local/share/dotnet*) fail "Resolving system dotnet at $DOTNET_PATH; need \$HOME/.dotnet/dotnet (SDK 10.0.203). Prepend \$HOME/.dotnet to PATH." ;;
esac

# ── Docker Compose infrastructure ─────────────────────────────────────────────

log "Starting Docker Compose infrastructure..."
docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" up -d

# ── Keycloak health ───────────────────────────────────────────────────────────

wait_port 8180 "Keycloak" 60

# ── Auth setup ────────────────────────────────────────────────────────────────

log "Running dev-setup-auth.sh (realm import + demo users)..."
"$REPO_ROOT/tools/dev-setup-auth.sh"

# Source auth env vars so child processes inherit them
# shellcheck source=tools/dev-env.sh
. "$REPO_ROOT/tools/dev-env.sh"

# ── Identity (no Dapr sidecar needed) ────────────────────────────────────────

log "Starting Identity service (logs -> $LOG_DIR/identity.log)..."
cd "$REPO_ROOT"
dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj \
  > "$LOG_DIR/identity.log" 2>&1 &
echo "$!" >> "$PID_FILE"
wait_port 5192 "Identity" 60

# ── Six services with Dapr sidecars ───────────────────────────────────────────

log "Starting Booking, Notification, Profile, Audit, Reporting, Configuration"
log "  with Dapr sidecars (logs -> $LOG_DIR/dapr-run.log)..."
cd "$REPO_ROOT"
dapr run -f dapr.yaml > "$LOG_DIR/dapr-run.log" 2>&1 &
echo "$!" >> "$PID_FILE"

wait_port 5131 "Booking"       90
wait_port 5157 "Notification"  90
wait_port 5197 "Profile"       90
wait_port 5161 "Audit"         90
wait_port 5171 "Reporting"     90
wait_port 5141 "Configuration" 90

# ── Seed demo data ────────────────────────────────────────────────────────────

log "Seeding demo profile data..."
"$REPO_ROOT/tools/dev-seed.sh" || log "WARNING: seed step failed — run ./tools/dev-seed.sh manually"

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
printf ' Logs:            %s/\n' "$LOG_DIR"
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
