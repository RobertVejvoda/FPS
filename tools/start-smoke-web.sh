#!/bin/sh
# start-smoke-web.sh — Start backend smoke services and the FPS web app.
#
# Intended flow:
#   docker compose -f code/infrastructure/docker-compose.yaml up -d
#   ./tools/start-smoke-web.sh
#
# Stop with Ctrl-C. The script stops Vite and leaves the shared backend
# harness running unless SMOKE_STOP_HARNESS_ON_EXIT=true is set.
set -eu

# Prefer user-installed Node/npm over embedded tool runtimes.
export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEB_DIR="$REPO_ROOT/code/web/fps-web"
WEB_PID=""
WEB_HOST="${FPS_WEB_HOST:-127.0.0.1}"

. "$REPO_ROOT/tools/smoke-harness-lib.sh"

cleanup() {
  if [ -n "$WEB_PID" ] && kill -0 "$WEB_PID" 2>/dev/null; then
    kill "$WEB_PID" 2>/dev/null || true
  fi
  cleanup_smoke_harness
}

trap cleanup INT TERM EXIT

cd "$REPO_ROOT"
ensure_smoke_harness

printf '\n'
printf '================================================\n'
printf ' FPS Web Smoke — Ready\n'
printf '================================================\n'
printf ' Web app:  http://localhost:5200\n'
if [ "$WEB_HOST" != "127.0.0.1" ] && [ "$WEB_HOST" != "localhost" ]; then
  printf ' Warning: web SSO is configured for localhost:5200. Network hosts need matching Keycloak, web config, and CORS settings.\n'
fi
printf '\n'
printf ' Sign in:  open http://localhost:5200 and click Sign in.\n'
printf '           Use a seeded Green Logistics user, e.g.:\n'
printf '             username: gl-employee1\n'
printf '             password: Dev1234!  (set by dev-setup-auth.sh)\n'
printf '\n'
printf ' Fallback: to use a manual bearer token instead, set\n'
printf '           devTokenFallbackEnabled=true in public/config.json\n'
printf '           and paste the output of: ./tools/dev-auth.sh gl-employee1\n'
printf '\n'
printf ' Stop with Ctrl-C. Backend harness and Docker infrastructure will stay running.\n'
printf ' Set SMOKE_STOP_HARNESS_ON_EXIT=true to restore stop-on-exit behavior.\n'
printf '================================================\n'
printf '\n'

cd "$WEB_DIR"
sh "$REPO_ROOT/tools/ensure-node-app-deps.sh" "$WEB_DIR" 'node -e "require(\"rollup\")"'

"$WEB_DIR/node_modules/.bin/vite" --host "$WEB_HOST" &
WEB_PID="$!"
wait "$WEB_PID"
