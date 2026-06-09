#!/bin/sh
# stop-local-harness.sh — Stop the FPS local test harness.
#
# Kills services started by start-local-harness.sh and stops Docker Compose.
#
# Usage:
#   ./tools/stop-local-harness.sh                  # stop services and infrastructure, keep Docker volumes
#   ./tools/stop-local-harness.sh --services-only  # stop app services only, leave infrastructure running
#   ./tools/stop-local-harness.sh --reset          # stop and remove Docker volumes (full reset)

set -eu

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PID_FILE="$REPO_ROOT/logs/local-harness/pids"
MODE="${1:-}"

log()  { printf '[harness] %s\n' "$*"; }

case "$MODE" in
  ""|--services-only|--reset) ;;
  -h|--help)
    cat <<EOF
Usage:
  ./tools/stop-local-harness.sh [--services-only|--reset]

Options:
  --services-only  Stop FPS app services and Dapr sidecars only.
  --reset          Stop infrastructure and remove Docker volumes.
EOF
    exit 0
    ;;
  *) printf '[harness] ERROR: Unknown argument: %s\n' "$MODE" >&2; exit 1 ;;
esac

# Kill tracked PIDs
if [ -f "$PID_FILE" ]; then
  while IFS= read -r pid; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null && log "Stopped PID $pid" || true
    fi
  done < "$PID_FILE"
  rm -f "$PID_FILE"
fi

# Kill all FPS service processes by name pattern.
# dapr run -f dapr.yaml spawns one dotnet process per app plus one daprd sidecar per app.
# Killing the tracked dapr-run PID sends SIGTERM to the dapr process but the child dotnet
# processes may survive. Kill them explicitly by assembly/project name.
for pattern in \
  "start-local-harness.sh --skip-infra" \
  "dotnet run .*FPS.Identity" \
  "dotnet .*FPS.Identity.dll" \
  "FPS.Identity" \
  "FPS.Booking" \
  "FPS.Notification" \
  "FPS.Profile" \
  "FPS.Audit" \
  "FPS.Reporting" \
  "FPS.Configuration" \
  "FPS.Customer" \
  "daprd"; do
  pkill -TERM -f "$pattern" 2>/dev/null && log "Stopped $pattern processes" || true
done

# Some failed .NET launches can get stuck before binding a port. Give them a
# moment to handle SIGTERM, then force only the known harness process patterns.
sleep 1
for pattern in \
  "start-local-harness.sh --skip-infra" \
  "dotnet run .*FPS.Identity" \
  "dotnet .*FPS.Identity.dll" \
  "FPS.Identity" \
  "FPS.Booking" \
  "FPS.Notification" \
  "FPS.Profile" \
  "FPS.Audit" \
  "FPS.Reporting" \
  "FPS.Configuration" \
  "FPS.Customer" \
  "daprd"; do
  pkill -KILL -f "$pattern" 2>/dev/null && log "Force-stopped $pattern processes" || true
done

if [ "$MODE" = "--services-only" ]; then
  log "Stopped app services. Docker infrastructure left running."
elif [ "$MODE" = "--reset" ]; then
  log "Stopping infrastructure and removing volumes (full reset)..."
  docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" down -v
  log "Full reset complete. Run ./tools/start-local-harness.sh to restart."
else
  log "Stopping infrastructure (Docker volumes preserved)..."
  docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" down
  log "Stopped. Run ./tools/start-local-harness.sh to restart."
fi
