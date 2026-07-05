#!/usr/bin/env bash
# Stop the full local FairSpot Docker/web/mobile scenario.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LOG_DIR="$REPO_ROOT/logs/local-runtime"
PID_FILE="$LOG_DIR/pids"
MODE="${1:-}"

case "$MODE" in
  ""|--reset) ;;
  -h|--help)
    cat <<'USAGE'
Usage:
  ./tools/local-stop.sh
  ./tools/local-stop.sh --reset

Options:
  --reset   Stop local runtime and remove Docker volumes for a clean local seed.
USAGE
    exit 0
    ;;
  *) printf 'ERROR: Unknown argument: %s\n' "$MODE" >&2; exit 1 ;;
esac

stop_pids() {
  if [ ! -f "$PID_FILE" ]; then
    return 0
  fi

  while read -r pid name; do
    if [ -n "${pid:-}" ] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      printf '[local] Stopped %s PID %s\n' "${name:-process}" "$pid"
    fi
  done < "$PID_FILE"
  rm -f "$PID_FILE"
}

stop_pids

# Clean up common child processes if the parent shell exited without updating
# the PID file. Keep the patterns narrow to FairSpot web/mobile dev commands.
pkill -TERM -f "code/web/fps-web/node_modules/.bin/vite" 2>/dev/null || true
pkill -TERM -f "code/mobile/fps-mobile.*expo" 2>/dev/null || true
sleep 1
pkill -KILL -f "code/web/fps-web/node_modules/.bin/vite" 2>/dev/null || true
pkill -KILL -f "code/mobile/fps-mobile.*expo" 2>/dev/null || true

if [ "$MODE" = "--reset" ]; then
  printf '[local] Stopping containers and removing local Docker volumes...\n'
  # DataHub's connection string requires POSTGRES_PASSWORD (fail-closed on
  # production-like profiles); supply the LOCAL dev default so compose can
  # interpolate the file for this local-only teardown.
  export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-fps}"
  docker compose --project-directory "$REPO_ROOT/code/infrastructure" \
    -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" \
    -f "$REPO_ROOT/code/infrastructure/docker-compose.services.yml" \
    -f "$REPO_ROOT/code/infrastructure/docker-compose.dapr.yml" \
    down -v
else
  printf '[local] Stopping containers; Docker volumes preserved...\n'
  "$REPO_ROOT/tools/start-container-stack.sh" --down
fi

printf '[local] Stopped.\n'
