#!/usr/bin/env bash
# Stop the NAS/hosted FairSpot server runtime.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"
ENV_FILE="${FPS_NAS_ENV_FILE:-$INFRA_DIR/nas.env}"
TUNNEL_ENV_FILE="${FPS_TUNNEL_ENV_FILE:-$INFRA_DIR/cloudflared/.env.nas}"
MODE="${1:-}"

case "$MODE" in
  ""|--reset) ;;
  -h|--help)
    cat <<'USAGE'
Usage:
  ./tools/nas-stop.sh
  ./tools/nas-stop.sh --reset

Options:
  --reset   Stop NAS runtime and remove Docker volumes. Use only before a clean hosted reset.
USAGE
    exit 0
    ;;
  *) printf 'ERROR: Unknown argument: %s\n' "$MODE" >&2; exit 1 ;;
esac

if [ -f "$TUNNEL_ENV_FILE" ]; then
  printf '[nas] Stopping Cloudflare Tunnel connector...\n'
  docker compose \
    -f "$INFRA_DIR/cloudflared/docker-compose.cloudflared.yml" \
    --env-file "$TUNNEL_ENV_FILE" \
    down
else
  printf '[nas] Cloudflare tunnel env file not found; skipping tunnel stop.\n'
fi

if [ "$MODE" = "--reset" ]; then
  printf '[nas] Stopping NAS containers and removing volumes...\n'
  docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" \
    -f "$INFRA_DIR/docker-compose.yaml" \
    -f "$INFRA_DIR/docker-compose.services.images.yml" \
    -f "$INFRA_DIR/docker-compose.dapr.yml" \
    -f "$INFRA_DIR/docker-compose.nas.yml" \
    -f "$INFRA_DIR/docker-compose.services.nas.yml" \
    -f "$INFRA_DIR/docker-compose.no-host-ports.yml" \
    down -v
else
  printf '[nas] Stopping NAS containers; volumes preserved...\n'
  "$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE" --down
fi

printf '[nas] Stopped.\n'
