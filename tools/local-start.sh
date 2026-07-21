#!/usr/bin/env bash
# Start the full local FairSpot developer/demo experience.
#
# This scenario is local-only: no Cloudflare, no public domain, no public IP.
# It starts the container backend, seeds local demo data, then starts web and
# mobile developer servers in the background.

set -euo pipefail

export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:$PATH"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LOG_DIR="$REPO_ROOT/logs/local-runtime"
PID_FILE="$LOG_DIR/pids"
WEB_DIR="$REPO_ROOT/code/web/fps-web"
MOBILE_DIR="$REPO_ROOT/code/mobile/fps-mobile"
LOCAL_ENV_FILE="$REPO_ROOT/code/infrastructure/local-docker.env"
WEB_HOST="${FPS_WEB_HOST:-127.0.0.1}"
EXPO_MODE="${EXPO_MODE:-lan}"

mkdir -p "$LOG_DIR"
: > "$PID_FILE"

start_background() {
  name="$1"
  logfile="$2"
  shift 2

  printf '[local] Starting %s...\n' "$name"
  ("$@") > "$logfile" 2>&1 &
  pid="$!"
  printf '%s %s\n' "$pid" "$name" >> "$PID_FILE"
  printf '[local] %s PID %s, log %s\n' "$name" "$pid" "${logfile#$REPO_ROOT/}"
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

read_env_value() {
  key="$1"
  file="$2"
  [ -f "$file" ] || return 0
  awk -F= -v key="$key" '
    /^[[:space:]]*#/ || /^[[:space:]]*$/ { next }
    {
      k = $1
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", k)
      if (k == key) {
        value = substr($0, index($0, "=") + 1)
        gsub(/^[[:space:]]+|[[:space:]]+$/, "", value)
        gsub(/^"|"$/, "", value)
        print value
        exit
      }
    }
  ' "$file"
}

GRAFANA_HOST_PORT="${FPS_GRAFANA_HOST_PORT:-}"
if [ -z "$GRAFANA_HOST_PORT" ]; then
  GRAFANA_HOST_PORT="$(read_env_value FPS_GRAFANA_HOST_PORT "$LOCAL_ENV_FILE")"
fi
GRAFANA_HOST_PORT="${GRAFANA_HOST_PORT:-3001}"

cd "$REPO_ROOT"

printf '== FairSpot local scenario ==\n'
printf 'Mode: local Docker only; Cloudflare is not used.\n\n'

"$REPO_ROOT/tools/start-container-stack.sh" --seed

sh "$REPO_ROOT/tools/ensure-node-app-deps.sh" "$WEB_DIR" 'node -e "const fs = require(\"fs\"); fs.statSync(\"node_modules/@robertvejvoda/fairspot-api-client/package.json\"); fs.statSync(\"node_modules/@robertvejvoda/fairspot-ui/package.json\"); (async () => { try { await import(\"vite\"); process.exit(0); } catch { process.exit(1); } })();"'
start_background "web" "$LOG_DIR/web.log" \
  "$WEB_DIR/node_modules/.bin/vite" --host "$WEB_HOST"

LAN_IP="$(detect_lan_ip || true)"
MOBILE_HOST="${FPS_MOBILE_HOST:-${LAN_IP:-localhost}}"
export KEYCLOAK_URL="${FPS_MOBILE_KEYCLOAK_URL:-http://$MOBILE_HOST:8180}"
export FPS_MOBILE_AUTH_ISSUER_URL="${FPS_MOBILE_AUTH_ISSUER_URL:-$KEYCLOAK_URL/realms/fps-local}"
export FPS_MOBILE_AUTH_CLIENT_ID="${FPS_MOBILE_AUTH_CLIENT_ID:-fps-mobile-dev}"
export FPS_MOBILE_AUTH_SCOPES="${FPS_MOBILE_AUTH_SCOPES:-openid profile email}"
export FPS_MOBILE_API_BASE_URL="${FPS_MOBILE_API_BASE_URL:-http://$MOBILE_HOST:10000}"

sh "$REPO_ROOT/tools/ensure-node-app-deps.sh" "$MOBILE_DIR" 'node -e "require(\"expo/package.json\")"'
case "$EXPO_MODE" in
  lan) mobile_args="--lan --clear" ;;
  tunnel) mobile_args="--tunnel --clear" ;;
  localhost) mobile_args="--localhost --clear" ;;
  *) printf 'ERROR: unsupported EXPO_MODE=%s. Use lan, tunnel, or localhost.\n' "$EXPO_MODE" >&2; exit 1 ;;
esac
start_background "mobile" "$LOG_DIR/mobile.log" \
  sh -c "cd '$MOBILE_DIR' && npm run start -- $mobile_args"

printf '\n== FairSpot local scenario ready ==\n'
printf 'Web:      http://localhost:5200\n'
printf 'Gateway:  http://localhost:10000\n'
printf 'Keycloak: http://localhost:8180\n'
printf 'Grafana:  http://localhost:%s\n' "$GRAFANA_HOST_PORT"
printf 'Mobile:   Expo started in %s mode. See logs/local-runtime/mobile.log for QR/output.\n' "$EXPO_MODE"
printf '\nDemo users (Green Logistics): gl-employee1, gl-hr-admin, gl-tenant-admin, gl-auditor, gl-report-viewer\n'
printf 'Password:   %s\n' "${FPS_DEV_PASSWORD:-Dev1234!}"
printf '\nStop:       ./tools/local-stop.sh\n'
printf 'Reset data: ./tools/local-stop.sh --reset\n'
