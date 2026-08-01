#!/usr/bin/env bash
# tools/deploy-nas.sh — repeatable NAS/Cloudflare deployment entry point.
#
# Recurring deployment is one command. One-time Cloudflare, Vault, and ignored
# env-file preparation remain explicit operator steps because they create or
# handle external credentials and recovery material.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

ENV_FILE="$INFRA_DIR/nas.env"
TUNNEL_ENV_FILE="$INFRA_DIR/cloudflared/.env.nas"
DOMAIN=""
APP_HOST=""
AUTH_HOST=""
OPS_HOST=""
EXISTING_TUNNEL_CONTAINER=""
IMAGE_TAG=""
ALLOW_LATEST=false
SKIP_TUNNEL=false
SKIP_PUBLIC=false
DOWN=false

read_env_value_from() {
  local key="$1" file="$2"
  [[ -f "$file" ]] || return 0
  awk -F= -v key="$key" '
    /^[[:space:]]*#/ || /^[[:space:]]*$/ { next }
    {
      k = $1
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", k)
      if (k == key) {
        value = substr($0, index($0, "=") + 1)
        gsub(/^[[:space:]]+|[[:space:]]+$/, "", value)
        gsub(/^"|"$/, "", value)
        gsub(/^'\''|'\''$/, "", value)
        print value
        exit
      }
    }
  ' "$file"
}

read_env_value() { read_env_value_from "$1" "$ENV_FILE"; }

usage() {
  cat <<'USAGE'
Usage:
  ./tools/deploy-nas.sh --app-host app-dev.example.net \
    --auth-host auth-dev.example.net --ops-host ops-dev.example.net \
    --tag sha-<commit> --existing-tunnel-container fairspot-cloudflared

  # Backward-compatible Production shorthand:
  ./tools/deploy-nas.sh --domain example.net --tag sha-<commit>

Options:
  --env-file PATH          NAS stack env file. Default: code/infrastructure/nas.env
  --tunnel-env-file PATH   Compose-managed tunnel env file. Default: cloudflared/.env.nas
  --app-host HOST          Exact application hostname (or FPS_PUBLIC_APP_HOST in nas.env).
  --auth-host HOST         Exact authentication hostname (or FPS_PUBLIC_AUTH_HOST).
  --ops-host HOST          Optional Access-protected Grafana hostname (or FPS_PUBLIC_OPS_HOST).
  --domain DOMAIN          Compatibility shorthand deriving app.DOMAIN/auth.DOMAIN.
  --tag TAG                Immutable sha-<commit> or v* image tag. Required publicly.
  --allow-latest           Allow a mutable tag for a non-evidence experiment.
  --existing-tunnel-container NAME
                           Reuse an independently managed cloudflared container;
                           attach it idempotently to fairspot_network.
  --skip-tunnel            Internal troubleshooting only; requires --skip-public
                           and refuses an active cloudflared connector on the
                           FairSpot network.
  --skip-public            Skip public hostname smoke; requires --skip-tunnel.
  --down                   Stop the stack, preserving data volumes, then exit.

Normal recurring operation:
  Store FPS_PUBLIC_APP_HOST, FPS_PUBLIC_AUTH_HOST, FPS_PUBLIC_OPS_HOST, and all
  real values only in ignored nas.env. Then the recurring command is:
    ./tools/deploy-nas.sh --tag sha-<commit> \
      --existing-tunnel-container fairspot-cloudflared
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file) ENV_FILE="$2"; shift ;;
    --tunnel-env-file) TUNNEL_ENV_FILE="$2"; shift ;;
    --app-host) APP_HOST="$2"; shift ;;
    --auth-host) AUTH_HOST="$2"; shift ;;
    --ops-host) OPS_HOST="$2"; shift ;;
    --domain) DOMAIN="$2"; shift ;;
    --tag) IMAGE_TAG="$2"; shift ;;
    --allow-latest) ALLOW_LATEST=true ;;
    --existing-tunnel-container) EXISTING_TUNNEL_CONTAINER="$2"; shift ;;
    --skip-tunnel) SKIP_TUNNEL=true ;;
    --skip-public) SKIP_PUBLIC=true ;;
    --down) DOWN=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown flag: $1"; usage; exit 1 ;;
  esac
  shift
done

if [[ "$SKIP_PUBLIC" == "true" && "$SKIP_TUNNEL" != "true" ]]; then
  echo "ERROR (NAS profile): --skip-public requires --skip-tunnel."
  echo "  The Tunnel must not publish a stack whose public configuration was not checked."
  exit 1
fi
if [[ -n "$EXISTING_TUNNEL_CONTAINER" && "$SKIP_TUNNEL" == "true" ]]; then
  echo "ERROR (NAS profile): --existing-tunnel-container cannot be combined with --skip-tunnel."
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR (NAS profile): env file not found: $ENV_FILE"
  echo "  Copy code/infrastructure/nas.env.example, fill it outside Git, and retry."
  exit 1
fi

APP_HOST="${APP_HOST:-$(read_env_value FPS_PUBLIC_APP_HOST)}"
AUTH_HOST="${AUTH_HOST:-$(read_env_value FPS_PUBLIC_AUTH_HOST)}"
OPS_HOST="${OPS_HOST:-$(read_env_value FPS_PUBLIC_OPS_HOST)}"
DOMAIN="${DOMAIN:-$(read_env_value FPS_PUBLIC_DOMAIN)}"

if [[ -z "$APP_HOST" && -z "$AUTH_HOST" && -n "$DOMAIN" ]]; then
  APP_HOST="app.$DOMAIN"
  AUTH_HOST="auth.$DOMAIN"
fi

if [[ "$DOWN" != "true" && "$SKIP_PUBLIC" != "true" ]]; then
  if [[ -z "$APP_HOST" || -z "$AUTH_HOST" ]]; then
    echo "ERROR (NAS profile): exact app and auth hostnames are required."
    echo "  Pass --app-host/--auth-host, set FPS_PUBLIC_APP_HOST/FPS_PUBLIC_AUTH_HOST,"
    echo "  or use the backward-compatible --domain shorthand."
    exit 1
  fi
fi

for host in "$APP_HOST" "$AUTH_HOST" "$OPS_HOST"; do
  [[ -z "$host" ]] && continue
  case "$host" in
    *://*|*/*|*:*|*' '*)
      echo "ERROR (NAS profile): hostname values must not include a scheme, path, port, or spaces: $host"
      exit 1
      ;;
  esac
done

if [[ "$DOWN" == "true" ]]; then
  if [[ -z "$EXISTING_TUNNEL_CONTAINER" && -f "$TUNNEL_ENV_FILE" ]]; then
    echo "== Stopping Compose-managed Cloudflare Tunnel connector =="
    docker compose -f "$INFRA_DIR/cloudflared/docker-compose.cloudflared.yml" \
      --env-file "$TUNNEL_ENV_FILE" down
  elif [[ -n "$EXISTING_TUNNEL_CONTAINER" ]]; then
    echo "INFO: leaving independently managed tunnel container '$EXISTING_TUNNEL_CONTAINER' running."
  fi
  "$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE" --down
  echo "Stopped. Durable volumes are preserved."
  exit 0
fi

IMAGE_TAG="${IMAGE_TAG:-${FPS_IMAGE_TAG:-$(read_env_value FPS_IMAGE_TAG)}}"
tag_is_immutable() { [[ "$1" == sha-* || "$1" == v* ]]; }
if [[ "$SKIP_PUBLIC" != "true" ]]; then
  if [[ -z "$IMAGE_TAG" || "$IMAGE_TAG" == "latest" ]] && [[ "$ALLOW_LATEST" != "true" ]]; then
    echo "ERROR (NAS profile): a public deployment requires an immutable image tag."
    echo "  Use --tag sha-<commit> (recommended) or an explicit v* release tag."
    exit 1
  fi
  if [[ -n "$IMAGE_TAG" && "$IMAGE_TAG" != "latest" ]] && ! tag_is_immutable "$IMAGE_TAG" && [[ "$ALLOW_LATEST" != "true" ]]; then
    echo "ERROR (NAS profile): tag '$IMAGE_TAG' is not an immutable sha-* or v* deployment tag."
    exit 1
  fi
fi
if [[ "$ALLOW_LATEST" == "true" ]] && { [[ -z "$IMAGE_TAG" ]] || ! tag_is_immutable "${IMAGE_TAG:-latest}"; }; then
  echo "WARNING: deploying a mutable image selection. This is not valid release evidence."
fi
if [[ -n "$IMAGE_TAG" ]]; then
  export FPS_IMAGE_TAG="$IMAGE_TAG"
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR (NAS profile): docker is not installed."
  exit 1
fi
if ! docker compose version >/dev/null 2>&1; then
  echo "ERROR (NAS profile): Docker Compose v2 is required."
  exit 1
fi
if ! docker info >/dev/null 2>&1; then
  echo "ERROR (NAS profile): cannot reach the Docker daemon."
  exit 1
fi

if [[ "$SKIP_TUNNEL" != "true" && -n "$EXISTING_TUNNEL_CONTAINER" ]]; then
  tunnel_state="$(docker inspect -f '{{.State.Status}}' "$EXISTING_TUNNEL_CONTAINER" 2>/dev/null || true)"
  if [[ "$tunnel_state" != "running" ]]; then
    echo "ERROR (NAS profile): existing tunnel container '$EXISTING_TUNNEL_CONTAINER' is not running (state: ${tunnel_state:-missing})."
    echo "  Correct the name or start the connector before any stack mutation."
    exit 1
  fi
fi

if [[ "$SKIP_TUNNEL" == "true" ]]; then
  active_tunnel_containers="$(
    docker ps --filter status=running --filter network=fairspot_network \
      --format '{{.Names}}\t{{.Image}}' 2>/dev/null \
      | awk 'tolower($0) ~ /cloudflared/ { print $1 }' \
      || true
  )"
  if [[ -n "$active_tunnel_containers" ]]; then
    echo "ERROR (NAS profile): an active Cloudflare Tunnel connector is still attached to fairspot_network."
    echo "  --skip-public --skip-tunnel is safe only after every connector is stopped or disconnected."
    printf '%s\n' "$active_tunnel_containers" | sed 's/^/  Active connector: /'
    exit 1
  fi
fi

if [[ "$SKIP_TUNNEL" != "true" && -z "$EXISTING_TUNNEL_CONTAINER" ]]; then
  if [[ ! -f "$TUNNEL_ENV_FILE" ]]; then
    echo "ERROR (NAS profile): tunnel env file not found: $TUNNEL_ENV_FILE"
    echo "  Or pass --existing-tunnel-container <name> for an independently managed connector."
    exit 1
  fi
  tunnel_token="$(read_env_value_from CLOUDFLARED_TUNNEL_TOKEN "$TUNNEL_ENV_FILE")"
  if [[ -z "$tunnel_token" ]]; then
    echo "ERROR (NAS profile): CLOUDFLARED_TUNNEL_TOKEN is missing or blank (value not shown)."
    exit 1
  fi
  unset tunnel_token
fi

# Grafana needs its external origin for redirects and secure cookies when an
# Access-protected ops hostname is configured. It is still not host-published.
if [[ -n "$OPS_HOST" ]]; then
  export FPS_GRAFANA_ROOT_URL="https://$OPS_HOST"
  export FPS_GRAFANA_COOKIE_SECURE=true
fi

# Synology bind mounts preserve NAS ACL/mode semantics. Prepare only the exact
# committed read-only config paths and the ignored log directory Compose uses.
mkdir -p "$REPO_ROOT/logs/local-harness"
for dir in \
  "$INFRA_DIR/dapr/components/container" \
  "$INFRA_DIR/dapr/configuration" \
  "$INFRA_DIR/envoy" \
  "$INFRA_DIR/prometheus" \
  "$INFRA_DIR/vault/config"
do
  chmod a+rx "$dir"
done
for file in \
  "$INFRA_DIR/dapr/components/container/bookingstore.yaml" \
  "$INFRA_DIR/dapr/components/container/s3store.yaml" \
  "$INFRA_DIR/dapr/components/container/vault.yaml" \
  "$INFRA_DIR/dapr/configuration/fairspot-config.yaml" \
  "$INFRA_DIR/datahub/run-migrations.sh" \
  "$INFRA_DIR/envoy/envoy.yaml" \
  "$INFRA_DIR/prometheus/prometheus.yaml" \
  "$INFRA_DIR/prometheus/prometheus.containers.yaml" \
  "$INFRA_DIR/prometheus/alerts.yaml" \
  "$INFRA_DIR/vault/config/vault.hcl"
do
  chmod a+r "$file"
done

echo "== FairSpot NAS deployment — preflight =="
echo "  Docker:    $(docker --version | head -1)"
echo "  Compose:   $(docker compose version | head -1)"
echo "  Env file:  $ENV_FILE (values not printed)"
echo "  App host:  ${APP_HOST:-internal only}"
echo "  Auth host: ${AUTH_HOST:-internal only}"
echo "  Ops host:  ${OPS_HOST:-not configured}"
echo "  Image tag: ${IMAGE_TAG:-<compose default>}"
echo "  Tunnel:    ${EXISTING_TUNNEL_CONTAINER:-Compose-managed connector}"

render_err="$(mktemp)"
trap 'find "$render_err" -maxdepth 0 -type f -delete 2>/dev/null || true' EXIT
if ! rendered="$(docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" \
  -f "$INFRA_DIR/docker-compose.yaml" \
  -f "$INFRA_DIR/docker-compose.services.images.yml" \
  -f "$INFRA_DIR/docker-compose.dapr.yml" \
  -f "$INFRA_DIR/docker-compose.nas.yml" \
  -f "$INFRA_DIR/docker-compose.services.nas.yml" \
  -f "$INFRA_DIR/docker-compose.no-host-ports.yml" config 2>"$render_err")"
then
  echo "ERROR (NAS profile): merged Compose config did not render."
  sed 's/^/  /' "$render_err"
  exit 1
fi
if printf '%s\n' "$rendered" | grep -q 'published:'; then
  echo "ERROR (NAS profile): a service still publishes a host port; Tunnel must be the only ingress."
  exit 1
fi
unset rendered
echo "  Boundary:  no host-published ports"

start_args=(--nas --env-file "$ENV_FILE" --skip-public-smoke)
if [[ -n "$APP_HOST" && -n "$AUTH_HOST" ]]; then
  start_args+=(--app-host "$APP_HOST" --auth-host "$AUTH_HOST")
fi

echo
echo "== Starting and verifying the NAS stack =="
"$REPO_ROOT/tools/start-container-stack.sh" "${start_args[@]}"

if [[ "$SKIP_TUNNEL" != "true" ]]; then
  echo
  echo "== Cloudflare Tunnel connector =="
  if [[ -n "$EXISTING_TUNNEL_CONTAINER" ]]; then
    # Re-check after the stack start to catch a connector that stopped after
    # preflight but before the attachment/public-smoke handoff.
    tunnel_state="$(docker inspect -f '{{.State.Status}}' "$EXISTING_TUNNEL_CONTAINER" 2>/dev/null || true)"
    if [[ "$tunnel_state" != "running" ]]; then
      echo "ERROR: existing tunnel container '$EXISTING_TUNNEL_CONTAINER' is not running (state: ${tunnel_state:-missing})."
      exit 1
    fi
    network_state="$(docker inspect -f '{{with index .NetworkSettings.Networks "fairspot_network"}}connected{{end}}' "$EXISTING_TUNNEL_CONTAINER" 2>/dev/null || true)"
    if [[ "$network_state" != "connected" ]]; then
      docker network connect fairspot_network "$EXISTING_TUNNEL_CONTAINER"
      echo "  Attached $EXISTING_TUNNEL_CONTAINER to fairspot_network."
    else
      echo "  $EXISTING_TUNNEL_CONTAINER is already attached to fairspot_network."
    fi
  else
    docker compose -f "$INFRA_DIR/cloudflared/docker-compose.cloudflared.yml" \
      --env-file "$TUNNEL_ENV_FILE" up -d
  fi
fi

if [[ "$SKIP_PUBLIC" != "true" ]]; then
  echo
  echo "== Exact-host public smoke =="
  "$REPO_ROOT/tools/start-container-stack.sh" --nas --env-file "$ENV_FILE" \
    --smoke-only --app-host "$APP_HOST" --auth-host "$AUTH_HOST"
fi

echo
echo "Deployment completed."
echo "  Application: https://${APP_HOST:-<not-published>}"
echo "  Auth:        https://${AUTH_HOST:-<not-published>}"
if [[ -n "$OPS_HOST" ]]; then
  echo "  Operations:  https://$OPS_HOST (must remain protected by Cloudflare Access)"
fi
echo "  Stop safely: ./tools/deploy-nas.sh --down --existing-tunnel-container ${EXISTING_TUNNEL_CONTAINER:-<name-if-external>}"
