#!/bin/sh
# start-smoke-web.sh — Start backend smoke services and the FPS web app.
#
# Intended flow:
#   docker compose -f code/infrastructure/docker-compose.yaml up -d
#   ./tools/start-smoke-web.sh
#
# Stop with Ctrl-C. The script stops app services but leaves Docker
# infrastructure running.
set -eu

# Prefer user-installed Node/npm over embedded tool runtimes.
export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEB_DIR="$REPO_ROOT/code/web/fps-web"
WEB_PID=""

cleanup() {
  if [ -n "$WEB_PID" ] && kill -0 "$WEB_PID" 2>/dev/null; then
    kill "$WEB_PID" 2>/dev/null || true
  fi
  "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
}

trap cleanup INT TERM EXIT

cd "$REPO_ROOT"
"$REPO_ROOT/tools/start-local-harness.sh" --skip-infra

TOKEN="$("$REPO_ROOT/tools/dev-auth.sh" employee1)"

printf '\n'
printf '================================================\n'
printf ' FPS Web Smoke — Ready\n'
printf '================================================\n'
printf ' Web app:      http://localhost:5200\n'
printf ' API base URL: http://localhost:10000\n'
printf ' Demo user:    employee1\n'
printf ' Bearer token: %s\n' "$TOKEN"
printf '\n'
printf 'Paste the API base URL and bearer token into the web Session page.\n'
printf 'Stop with Ctrl-C. Docker infrastructure will stay running.\n'
printf '================================================\n'
printf '\n'

cd "$WEB_DIR"
sh "$REPO_ROOT/tools/ensure-node-app-deps.sh" "$WEB_DIR" 'node -e "require(\"rollup\")"'

npm run dev -- --host 0.0.0.0 &
WEB_PID="$!"
wait "$WEB_PID"
