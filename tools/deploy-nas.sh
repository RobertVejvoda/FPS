#!/usr/bin/env bash
# tools/deploy-nas.sh — one-command NAS deployment wrapper for FairSpot.
#
# This is the operator entry point for the NAS/Cloudflare profile. It starts the
# containerized FairSpot stack, starts the Cloudflare Tunnel connector, and
# checks the public app/auth hostnames.
#
# One-time setup still happens outside this script:
#   1. Copy and fill code/infrastructure/nas.env from nas.env.example.
#   2. Create the Cloudflare Tunnel and fill code/infrastructure/cloudflared/.env.nas.
#   3. Configure Cloudflare public hostnames:
#        app.<domain>  -> http://fps-web:80     (web SPA; proxies /api/ to Envoy)
#        auth.<domain> -> http://keycloak:8080  (Keycloak public login)
#      The web container serves the SPA at "/" and reverse-proxies "/api/" to the
#      Envoy gateway, so the browser uses a single origin. Set the web app's
#      apiBaseUrl to https://app.<domain>/api via FPS_WEB_API_BASE_URL in nas.env.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

ENV_FILE="$INFRA_DIR/nas.env"
TUNNEL_ENV_FILE="$INFRA_DIR/cloudflared/.env.nas"
DOMAIN=""
SKIP_TUNNEL=false
SKIP_PUBLIC=false

read_env_value() {
  key="$1"
  awk -F= -v key="$key" '
    $1 == key {
      value = substr($0, index($0, "=") + 1)
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", value)
      gsub(/^"|"$/, "", value)
      gsub(/^'\''|'\''$/, "", value)
      print value
      exit
    }
  ' "$ENV_FILE"
}

usage() {
  cat <<'USAGE'
Usage:
  ./tools/deploy-nas.sh --domain fairspot.net

Options:
  --env-file PATH          NAS stack env file. Default: code/infrastructure/nas.env
  --tunnel-env-file PATH   Cloudflare tunnel env file. Default: code/infrastructure/cloudflared/.env.nas
  --domain DOMAIN          Public domain for smoke checks, e.g. fairspot.net
  --skip-tunnel            Internal troubleshooting only. Do not start cloudflared.
  --skip-public            Internal troubleshooting only. Do not run public hostname checks.

One-time setup:
  cp code/infrastructure/nas.env.example code/infrastructure/nas.env
  cp code/infrastructure/cloudflared/nas-env.template code/infrastructure/cloudflared/.env.nas
  # Fill both files with real secrets before running this script.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file) ENV_FILE="$2"; shift ;;
    --tunnel-env-file) TUNNEL_ENV_FILE="$2"; shift ;;
    --domain) DOMAIN="$2"; shift ;;
    --skip-tunnel) SKIP_TUNNEL=true ;;
    --skip-public) SKIP_PUBLIC=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown flag: $1"; usage; exit 1 ;;
  esac
  shift
done

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: NAS env file not found: $ENV_FILE"
  echo "Create it from the template and fill all required values:"
  echo "  cp code/infrastructure/nas.env.example code/infrastructure/nas.env"
  exit 1
fi

if [[ -z "$DOMAIN" ]]; then
  DOMAIN="$(read_env_value FPS_PUBLIC_DOMAIN)"
fi
AUTH_AUTHORITY="$(read_env_value FPS_AUTH_AUTHORITY)"

if [[ "$SKIP_TUNNEL" != "true" && ! -f "$TUNNEL_ENV_FILE" ]]; then
  echo "ERROR: Cloudflare tunnel env file not found: $TUNNEL_ENV_FILE"
  echo "NAS hosted deployment requires Cloudflare Tunnel."
  echo "Create it from the template and paste the real tunnel token:"
  echo "  cp code/infrastructure/cloudflared/nas-env.template code/infrastructure/cloudflared/.env.nas"
  echo
  echo "For internal stack troubleshooting only, rerun with --skip-tunnel --skip-public."
  exit 1
fi

if [[ "$SKIP_PUBLIC" != "true" && -z "$DOMAIN" ]]; then
  echo "ERROR: Public domain is required for NAS hosted deployment."
  echo "Pass --domain fairspot.net or set FPS_PUBLIC_DOMAIN=fairspot.net in $ENV_FILE."
  echo
  echo "For internal stack troubleshooting only, rerun with --skip-public."
  exit 1
fi

if [[ "$SKIP_PUBLIC" != "true" && -n "$AUTH_AUTHORITY" && "$AUTH_AUTHORITY" != https://* ]]; then
  echo "ERROR: NAS hosted deployment requires encrypted public auth."
  echo "Set FPS_AUTH_AUTHORITY to an https:// URL in $ENV_FILE."
  echo "Local HTTP auth is allowed only in the local Docker profile."
  exit 1
fi

echo "== FairSpot NAS deployment =="
echo "Stack env:  $ENV_FILE"
echo "Domain:     ${DOMAIN:-not set}"
echo

"$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE"

if [[ "$SKIP_TUNNEL" != "true" ]]; then
  echo
  echo "== Starting Cloudflare Tunnel connector =="
  docker compose \
    -f "$INFRA_DIR/cloudflared/docker-compose.cloudflared.yml" \
    --env-file "$TUNNEL_ENV_FILE" \
    up -d
else
  echo
  echo "INFO: Cloudflare Tunnel skipped for internal troubleshooting only."
fi

if [[ "$SKIP_PUBLIC" != "true" && -n "$DOMAIN" ]]; then
  echo
  echo "== Public-domain smoke =="
  "$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE" --domain "$DOMAIN"
elif [[ "$SKIP_PUBLIC" != "true" ]]; then
  echo
  echo "INFO: Public-domain smoke skipped because --domain was not provided."
fi

echo
echo "Deployment command completed."
echo "Internal Grafana: http://<NAS-LAN-IP>:3000, or via operator-only Cloudflare Access if configured."
