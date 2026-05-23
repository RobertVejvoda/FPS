#!/bin/sh
# start-smoke-mobile.sh — Start backend smoke services and Expo mobile.
#
# Intended flow:
#   docker compose -f code/infrastructure/docker-compose.yaml up -d
#   ./tools/start-smoke-mobile.sh
#
# Stop with Ctrl-C. The script stops app services but leaves Docker
# infrastructure running.
set -eu

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MOBILE_DIR="$REPO_ROOT/code/mobile/fps-mobile"
EXPO_PID=""
EXPO_MODE="${EXPO_MODE:-lan}"

cleanup() {
  if [ -n "$EXPO_PID" ] && kill -0 "$EXPO_PID" 2>/dev/null; then
    kill "$EXPO_PID" 2>/dev/null || true
  fi
  "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
}

detect_lan_ip() {
  if command -v ipconfig >/dev/null 2>&1; then
    ipconfig getifaddr en0 2>/dev/null && return 0
    ipconfig getifaddr en1 2>/dev/null && return 0
  fi
  if command -v hostname >/dev/null 2>&1; then
    hostname -I 2>/dev/null | awk '{print $1}' && return 0
  fi
  return 1
}

trap cleanup INT TERM EXIT

cd "$REPO_ROOT"
"$REPO_ROOT/tools/start-local-harness.sh" --skip-infra

TOKEN="$("$REPO_ROOT/tools/dev-auth.sh" employee1)"
LAN_IP="$(detect_lan_ip || true)"

printf '\n'
printf '================================================\n'
printf ' FPS Mobile Smoke — Ready\n'
printf '================================================\n'
printf ' API base URL, simulator/browser: http://localhost:10000\n'
if [ -n "$LAN_IP" ]; then
  printf ' API base URL, physical phone:    http://%s:10000\n' "$LAN_IP"
fi
printf ' Demo user: employee1\n'
printf ' Bearer token: %s\n' "$TOKEN"
printf '\n'
printf 'Use the Developer Session screen if OIDC login is not configured.\n'
printf 'Set EXPO_MODE=tunnel before running this script if LAN QR scanning fails.\n'
printf 'Stop with Ctrl-C. Docker infrastructure will stay running.\n'
printf '================================================\n'
printf '\n'

cd "$MOBILE_DIR"
sh "$REPO_ROOT/tools/ensure-node-app-deps.sh" "$MOBILE_DIR" 'node -e "require(\"expo/package.json\")"'

case "$EXPO_MODE" in
  lan) npm run start -- --lan --clear & ;;
  tunnel) npm run start -- --tunnel --clear & ;;
  localhost) npm run start -- --localhost --clear & ;;
  *) printf 'ERROR: unsupported EXPO_MODE=%s. Use lan, tunnel, or localhost.\n' "$EXPO_MODE" >&2; exit 1 ;;
esac

EXPO_PID="$!"
wait "$EXPO_PID"
