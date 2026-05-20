#!/bin/sh
# stop-local-harness.sh — Stop the FPS local test harness.
#
# Kills services started by start-local-harness.sh and stops Docker Compose.
#
# Usage:
#   ./tools/stop-local-harness.sh              # stop services, keep Docker volumes
#   ./tools/stop-local-harness.sh --reset      # stop and remove Docker volumes (full reset)

set -eu

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PID_FILE="$REPO_ROOT/logs/local-harness/pids"

log()  { printf '[harness] %s\n' "$*"; }

# Kill tracked PIDs
if [ -f "$PID_FILE" ]; then
  while IFS= read -r pid; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null && log "Stopped PID $pid" || true
    fi
  done < "$PID_FILE"
  rm -f "$PID_FILE"
fi

# Backstop: kill by process name pattern
pkill -f "FPS.Identity" 2>/dev/null && log "Killed remaining Identity processes" || true
pkill -f "daprd"        2>/dev/null && log "Killed remaining Dapr sidecar processes" || true

# Stop Docker Compose
if [ "${1:-}" = "--reset" ]; then
  log "Stopping infrastructure and removing volumes (full reset)..."
  docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" down -v
  log "Full reset complete. Run ./tools/start-local-harness.sh to restart."
else
  log "Stopping infrastructure (Docker volumes preserved)..."
  docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" down
  log "Stopped. Run ./tools/start-local-harness.sh to restart."
fi
