#!/usr/bin/env bash
# tools/deploy-nas.sh — one-command NAS deployment wrapper for FairSpot.
#
# This is the operator entry point for the NAS/Cloudflare profile. It starts the
# containerized FairSpot stack, starts the Cloudflare Tunnel connector when its
# env file is present, and optionally checks the public app/auth hostnames.
#
# One-time setup still happens outside this script:
#   1. Copy and fill code/infrastructure/.env from .env.example.
#   2. Create the Cloudflare Tunnel and fill code/infrastructure/cloudflared/.env.nas.
#   3. Configure Cloudflare public hostnames:
#        app.<domain>  -> http://envoy-proxy:10000
#        auth.<domain> -> http://keycloak:8080

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

ENV_FILE="$INFRA_DIR/.env"
TUNNEL_ENV_FILE="$INFRA_DIR/cloudflared/.env.nas"
DOMAIN=""
SKIP_TUNNEL=false
SKIP_PUBLIC=false

usage() {
  cat <<'USAGE'
Usage:
  ./tools/deploy-nas.sh --domain fairspot.net

Options:
  --env-file PATH          NAS stack env file. Default: code/infrastructure/.env
  --tunnel-env-file PATH   Cloudflare tunnel env file. Default: code/infrastructure/cloudflared/.env.nas
  --domain DOMAIN          Public domain for smoke checks, e.g. fairspot.net
  --skip-tunnel            Do not start cloudflared.
  --skip-public            Do not run public hostname checks.

One-time setup:
  cp code/infrastructure/.env.example code/infrastructure/.env
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
  echo "  cp code/infrastructure/.env.example code/infrastructure/.env"
  exit 1
fi

echo "== FairSpot NAS deployment =="
echo "Stack env:  $ENV_FILE"
echo "Domain:     ${DOMAIN:-not set}"
echo

"$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE"

if [[ "$SKIP_TUNNEL" != "true" ]]; then
  if [[ ! -f "$TUNNEL_ENV_FILE" ]]; then
    echo
    echo "WARN: Cloudflare tunnel env file not found: $TUNNEL_ENV_FILE"
    echo "Tunnel not started. Create it from the template when ready:"
    echo "  cp code/infrastructure/cloudflared/nas-env.template code/infrastructure/cloudflared/.env.nas"
  else
    echo
    echo "== Starting Cloudflare Tunnel connector =="
    docker compose \
      -f "$INFRA_DIR/cloudflared/docker-compose.cloudflared.yml" \
      --env-file "$TUNNEL_ENV_FILE" \
      up -d
  fi
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
