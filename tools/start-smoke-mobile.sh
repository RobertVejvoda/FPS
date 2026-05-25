#!/bin/sh
# start-smoke-mobile.sh — Start backend smoke services and Expo mobile.
#
# Intended flow:
#   docker compose -f code/infrastructure/docker-compose.yaml up -d
#   ./tools/start-smoke-mobile.sh
#
# Stop with Ctrl-C. The script stops Expo and leaves the shared backend
# harness running unless SMOKE_STOP_HARNESS_ON_EXIT=true is set.
set -eu

# Prefer user-installed Node/npm over embedded tool runtimes.
export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MOBILE_DIR="$REPO_ROOT/code/mobile/fps-mobile"
EXPO_PID=""
EXPO_MODE="${EXPO_MODE:-lan}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
ENV_FILE="${FPS_MOBILE_ENV_FILE:-$MOBILE_DIR/.env.local}"

. "$REPO_ROOT/tools/smoke-harness-lib.sh"

cleanup() {
  if [ -n "$EXPO_PID" ] && kill -0 "$EXPO_PID" 2>/dev/null; then
    kill "$EXPO_PID" 2>/dev/null || true
  fi
  cleanup_smoke_harness
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

detect_tailscale_ip() {
  if command -v tailscale >/dev/null 2>&1; then
    tailscale ip -4 2>/dev/null | awk 'NR == 1 { print; exit }' && return 0
  fi
  return 1
}

load_env_file() {
  file="$1"
  if [ ! -f "$file" ]; then
    return 0
  fi

  log_name="$(printf '%s' "$file" | sed "s|^$REPO_ROOT/||")"
  printf '[mobile-smoke] Loading %s\n' "$log_name"

  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%}"
    case "$line" in
      ""|\#*) continue ;;
      *=*) ;;
      *) continue ;;
    esac

    key="${line%%=*}"
    value="${line#*=}"
    case "$key" in
      FPS_MOBILE_*|EXPO_PUBLIC_*) ;;
      *) continue ;;
    esac

    case "$value" in
      \"*\") value="${value#\"}"; value="${value%\"}" ;;
      \'*\') value="${value#\'}"; value="${value%\'}" ;;
    esac

    export "$key=$value"
  done < "$file"
}

trap cleanup INT TERM EXIT

load_env_file "$ENV_FILE"

LAN_IP="$(detect_lan_ip || true)"
TAILSCALE_IP="$(detect_tailscale_ip || true)"
MOBILE_HOST="${FPS_MOBILE_HOST:-${LAN_IP:-${TAILSCALE_IP:-localhost}}}"
MOBILE_KEYCLOAK_URL="${FPS_MOBILE_KEYCLOAK_URL:-http://$MOBILE_HOST:8180}"
MOBILE_API_BASE_URL="${FPS_MOBILE_API_BASE_URL:-http://$MOBILE_HOST:10000}"

export KEYCLOAK_URL="$MOBILE_KEYCLOAK_URL"
export FPS_MOBILE_AUTH_ISSUER_URL="${FPS_MOBILE_AUTH_ISSUER_URL:-$MOBILE_KEYCLOAK_URL/realms/$REALM}"
export FPS_MOBILE_AUTH_CLIENT_ID="${FPS_MOBILE_AUTH_CLIENT_ID:-$CLIENT_ID}"
export FPS_MOBILE_AUTH_SCOPES="${FPS_MOBILE_AUTH_SCOPES:-openid profile email}"
export FPS_MOBILE_API_BASE_URL="$MOBILE_API_BASE_URL"

cd "$REPO_ROOT"
ensure_smoke_harness

TOKEN="$("$REPO_ROOT/tools/dev-auth.sh" employee1)"

printf '\n'
printf '================================================\n'
printf ' FPS Mobile Smoke — Ready\n'
printf '================================================\n'
printf ' API base URL, simulator/browser: http://localhost:10000\n'
if [ -n "$LAN_IP" ]; then
  printf ' API base URL, physical phone:    http://%s:10000\n' "$LAN_IP"
fi
if [ -n "$TAILSCALE_IP" ]; then
  printf ' API base URL, Tailscale phone:   http://%s:10000\n' "$TAILSCALE_IP"
fi
printf ' Selected mobile host:            %s\n' "$MOBILE_HOST"
printf ' Mobile OIDC issuer:              %s\n' "$FPS_MOBILE_AUTH_ISSUER_URL"
printf ' Mobile OIDC client:              %s\n' "$FPS_MOBILE_AUTH_CLIENT_ID"
printf ' Demo user: employee1\n'
printf ' Demo password: %s\n' "${FPS_DEV_PASSWORD:-Dev1234!}"
printf ' Bearer token: %s\n' "$TOKEN"
printf '\n'
printf 'Use Sign in for OIDC login, or Developer Session for manual token smoke testing.\n'
printf 'Set EXPO_MODE=tunnel before running this script if LAN QR scanning fails.\n'
printf 'Stop with Ctrl-C. Backend harness and Docker infrastructure will stay running.\n'
printf 'Set SMOKE_STOP_HARNESS_ON_EXIT=true to restore stop-on-exit behavior.\n'
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
