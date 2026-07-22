#!/usr/bin/env bash
# tools/deploy-digitalocean.sh — one-command DigitalOcean Droplet deployment for
# FairSpot (#766). The operator entry point for the DigitalOcean profile.
#
# Semantics mirror tools/deploy-nas.sh: it starts the containerized FairSpot
# stack (image-mode, hardened NAS baseline) with the DigitalOcean delta overlay
# that suppresses public host-port bindings, starts the Cloudflare Tunnel
# connector, and runs the public app/auth smoke. The ONLY ingress is the
# outbound Cloudflare Tunnel; no service publishes a public host port.
#
# This script does NOT provision or mutate the host or the DigitalOcean account:
# it never changes users, SSH, the firewall, disks, DNS, reserved IPs, or any
# DigitalOcean/Cloudflare resource. Those are prepared-host and private-operator
# concerns (see docs/production/digitalocean-setup.md and the private companion
# issue RobertVejvoda/fairspot-platform#38). It only runs `docker compose`.
#
# One-time setup (outside this script, on a prepared Ubuntu LTS Droplet):
#   1. cp code/infrastructure/nas.env.example code/infrastructure/do.env
#      and fill every value (do.env is gitignored — never commit it).
#   2. Create a Cloudflare Tunnel, then put its token in
#      code/infrastructure/cloudflared/.env.do (CLOUDFLARED_TUNNEL_TOKEN=...).
#   3. Configure the Cloudflare public hostnames (app.<domain> -> fairspot-web:80,
#      auth.<domain> -> keycloak:8080) — see the operator runbook.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"

ENV_FILE="$INFRA_DIR/do.env"
TUNNEL_ENV_FILE="$INFRA_DIR/cloudflared/.env.do"
DOMAIN=""
SKIP_TUNNEL=false
SKIP_PUBLIC=false
IMAGE_TAG=""
ALLOW_LATEST=false
DOWN=false

read_env_value() {
  key="$1"
  [[ -f "$ENV_FILE" ]] || return 0
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
  ' "$ENV_FILE"
}

usage() {
  cat <<'USAGE'
Usage:
  ./tools/deploy-digitalocean.sh --domain fairspot.net --tag sha-<commit>

Options:
  --env-file PATH          DigitalOcean stack env file. Default: code/infrastructure/do.env
  --tunnel-env-file PATH   Cloudflare tunnel env file. Default: code/infrastructure/cloudflared/.env.do
  --domain DOMAIN          Public domain for smoke checks, e.g. fairspot.net
  --tag TAG                Image tag to deploy (sets FPS_IMAGE_TAG). Use an immutable
                           tag — sha-<commit> or a v* release tag. Required for a
                           public/evidence deployment (see --allow-latest).
  --allow-latest           Permit deploying the moving "latest" tag. Not valid for
                           release/evidence deployments.
  --down                   Stop the DigitalOcean stack, preserving data volumes, and exit.
  --skip-tunnel            Internal troubleshooting only. Do not start cloudflared.
  --skip-public            Internal troubleshooting only. Do not run public hostname checks.

Rollback:
  Re-run with a previous immutable --tag (durable volumes are preserved):
    ./tools/deploy-digitalocean.sh --domain <domain> --tag sha-<previous-commit>

One-time setup:
  cp code/infrastructure/nas.env.example code/infrastructure/do.env
  # put CLOUDFLARED_TUNNEL_TOKEN=... in code/infrastructure/cloudflared/.env.do
  # Fill do.env with real secrets before running this script.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file) ENV_FILE="$2"; shift ;;
    --tunnel-env-file) TUNNEL_ENV_FILE="$2"; shift ;;
    --domain) DOMAIN="$2"; shift ;;
    --tag) IMAGE_TAG="$2"; shift ;;
    --allow-latest) ALLOW_LATEST=true ;;
    --down) DOWN=true ;;
    --skip-tunnel) SKIP_TUNNEL=true ;;
    --skip-public) SKIP_PUBLIC=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown flag: $1"; usage; exit 1 ;;
  esac
  shift
done

# ── Stop / rollback path (preserves volumes) ─────────────────────────────────
if [[ "$DOWN" == "true" ]]; then
  echo "== Stopping FairSpot DigitalOcean stack (data volumes preserved) =="
  "$REPO_ROOT/tools/start-container-stack.sh" --digitalocean --env-file "$ENV_FILE" --down
  echo
  echo "Stopped. Cloudflare Tunnel connector (if running) is separate:"
  echo "  docker compose -f $INFRA_DIR/cloudflared/docker-compose.cloudflared.yml --env-file $TUNNEL_ENV_FILE down"
  exit 0
fi

# ── Non-mutating preflight (checks only; changes nothing) ────────────────────
echo "== FairSpot DigitalOcean deployment — preflight =="
fail=0
note() { echo "  - $*"; }

# 1. Docker Engine CLI + Compose v2 plugin. Daemon reachability is checked last
#    (just before we start containers), so bad files/flags/tags above fail fast
#    without requiring a running daemon.
if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR (DigitalOcean profile): 'docker' not found. Install Docker Engine 24+ on the Droplet."
  exit 1
fi
if ! docker compose version >/dev/null 2>&1; then
  echo "ERROR (DigitalOcean profile): Docker Compose v2 plugin not found. Install it and retry."
  exit 1
fi
note "docker: $(docker --version | head -1)"
note "compose: $(docker compose version | head -1)"

# 2. Execution context — non-root in the docker group is the supported path.
if [[ "$(id -u)" -eq 0 ]]; then
  note "WARNING: running as root. A non-root operator in the 'docker' group is preferred."
fi

# 3. Required operator files.
if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR (DigitalOcean profile): env file not found: $ENV_FILE"
  echo "  cp code/infrastructure/nas.env.example $ENV_FILE   # then fill every value"
  exit 1
fi
note "env file: $ENV_FILE (exists — values not printed)"

if [[ "$SKIP_TUNNEL" != "true" && ! -f "$TUNNEL_ENV_FILE" ]]; then
  echo "ERROR (DigitalOcean profile): Cloudflare tunnel env file not found: $TUNNEL_ENV_FILE"
  echo "  The DigitalOcean profile reaches the internet only through the Cloudflare Tunnel."
  echo "  Create it with CLOUDFLARED_TUNNEL_TOKEN=... (never commit it):"
  echo "    printf 'CLOUDFLARED_TUNNEL_TOKEN=<token>\\n' > $TUNNEL_ENV_FILE"
  echo "  For internal stack troubleshooting only, rerun with --skip-tunnel --skip-public."
  exit 1
fi

# 4. Public domain + encrypted auth authority (Cloudflare-fronted).
if [[ -z "$DOMAIN" ]]; then
  DOMAIN="$(read_env_value FPS_PUBLIC_DOMAIN)"
fi
AUTH_AUTHORITY="$(read_env_value FPS_AUTH_AUTHORITY)"
if [[ "$SKIP_PUBLIC" != "true" && -z "$DOMAIN" ]]; then
  echo "ERROR (DigitalOcean profile): public domain is required."
  echo "  Pass --domain fairspot.net or set FPS_PUBLIC_DOMAIN in $ENV_FILE."
  echo "  For internal stack troubleshooting only, rerun with --skip-public."
  exit 1
fi
if [[ "$SKIP_PUBLIC" != "true" && "$AUTH_AUTHORITY" != https://* ]]; then
  echo "ERROR (DigitalOcean profile): hosted deployment requires encrypted public auth."
  echo "  Set FPS_AUTH_AUTHORITY to a non-empty https:// URL in $ENV_FILE (TLS is terminated at Cloudflare)."
  echo "  A blank value falls back to the internal Keycloak issuer, which public clients cannot use."
  exit 1
fi

# 5. Public web runtime settings (FPS_WEB_*). The fairspot-web container
#    entrypoint (code/web/fps-web/docker-entrypoint.sh) reads these at startup
#    to render its runtime /config.json; if FPS_WEB_API_BASE_URL is unset it
#    silently serves the image's BAKED DEFAULT config instead — http://localhost:10000
#    and a local Keycloak issuer. nas.env.example (the template this runbook
#    copies to do.env) omits these keys entirely, and the public smoke only
#    checks that /config.json is *present*, not that it points at the public origin —
#    so a copied-but-unedited template would deploy a web app that browsers
#    cannot sign in with or call the public API from. Fail closed instead.
WEB_API_BASE_URL="$(read_env_value FPS_WEB_API_BASE_URL)"
WEB_OIDC_AUTHORITY="$(read_env_value FPS_WEB_OIDC_AUTHORITY)"
WEB_OIDC_CLIENT_ID="$(read_env_value FPS_WEB_OIDC_CLIENT_ID)"
WEB_OIDC_REDIRECT_URI="$(read_env_value FPS_WEB_OIDC_REDIRECT_URI)"
WEB_OIDC_POST_LOGOUT_REDIRECT_URI="$(read_env_value FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI)"
if [[ "$SKIP_PUBLIC" != "true" ]]; then
  for pair in \
    "FPS_WEB_API_BASE_URL=$WEB_API_BASE_URL" \
    "FPS_WEB_OIDC_AUTHORITY=$WEB_OIDC_AUTHORITY" \
    "FPS_WEB_OIDC_CLIENT_ID=$WEB_OIDC_CLIENT_ID" \
    "FPS_WEB_OIDC_REDIRECT_URI=$WEB_OIDC_REDIRECT_URI" \
    "FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI=$WEB_OIDC_POST_LOGOUT_REDIRECT_URI" \
  ; do
    if [[ -z "${pair#*=}" ]]; then
      echo "ERROR (DigitalOcean profile): missing public web runtime setting ${pair%%=*}."
      echo "  Without every FPS_WEB_* value, fairspot-web serves its baked default config.json"
      echo "  (http://localhost:10000, local Keycloak) instead of the public app/auth contract."
      echo "  Set every FPS_WEB_* value in $ENV_FILE (see docs/production/digitalocean-setup.md)."
      echo "  For internal stack troubleshooting only, rerun with --skip-public."
      exit 1
    fi
  done
  EXPECTED_WEB_API_BASE_URL="https://app.$DOMAIN/api"
  if [[ "$WEB_API_BASE_URL" != "$EXPECTED_WEB_API_BASE_URL" ]]; then
    echo "ERROR (DigitalOcean profile): FPS_WEB_API_BASE_URL does not match the public domain."
    echo "  Single-origin model: app.<domain> serves the SPA and proxies /api/ to Envoy, so this"
    echo "  must be $EXPECTED_WEB_API_BASE_URL for domain $DOMAIN. Got: $WEB_API_BASE_URL"
    exit 1
  fi
  if [[ "$WEB_OIDC_AUTHORITY" != "$AUTH_AUTHORITY" ]]; then
    echo "ERROR (DigitalOcean profile): FPS_WEB_OIDC_AUTHORITY does not match FPS_AUTH_AUTHORITY."
    echo "  The browser-facing web OIDC authority and the API-validated auth authority must be the"
    echo "  same public issuer, or the browser receives tokens every API rejects."
    echo "  FPS_AUTH_AUTHORITY=$AUTH_AUTHORITY"
    echo "  FPS_WEB_OIDC_AUTHORITY=$WEB_OIDC_AUTHORITY"
    exit 1
  fi
  # Exact-path match, not a same-origin prefix: a same-origin-but-wrong-path
  # value (e.g. an open-redirect or phishing path under app.<domain>) must
  # still be rejected, so compare against the documented callback/post-logout
  # paths exactly rather than accepting any arbitrary path under that origin.
  EXPECTED_WEB_REDIRECT_URI="https://app.$DOMAIN/auth/callback"
  if [[ "$WEB_OIDC_REDIRECT_URI" != "$EXPECTED_WEB_REDIRECT_URI" ]]; then
    echo "ERROR (DigitalOcean profile): FPS_WEB_OIDC_REDIRECT_URI does not match the documented callback path."
    echo "  Expected exactly $EXPECTED_WEB_REDIRECT_URI for domain $DOMAIN. Got: $WEB_OIDC_REDIRECT_URI"
    exit 1
  fi
  EXPECTED_WEB_POST_LOGOUT_REDIRECT_URI="https://app.$DOMAIN/"
  if [[ "$WEB_OIDC_POST_LOGOUT_REDIRECT_URI" != "$EXPECTED_WEB_POST_LOGOUT_REDIRECT_URI" ]]; then
    echo "ERROR (DigitalOcean profile): FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI does not match the documented post-logout path."
    echo "  Expected exactly $EXPECTED_WEB_POST_LOGOUT_REDIRECT_URI for domain $DOMAIN. Got: $WEB_OIDC_POST_LOGOUT_REDIRECT_URI"
    exit 1
  fi
  note "public web runtime settings: FPS_WEB_* present and consistent with domain $DOMAIN"
fi

# 6. Immutable image tag (only sha-<commit> or v* release tags; reject mutable tags).
IMAGE_TAG="${IMAGE_TAG:-${FPS_IMAGE_TAG:-}}"
if [[ "$SKIP_PUBLIC" != "true" ]]; then
  _tag_is_immutable() {
    [[ "$1" == sha-* || "$1" == v*.*.* || "$1" == v*.* || "$1" == v* ]]
  }
  if [[ -z "$IMAGE_TAG" ]]; then
    if [[ "$ALLOW_LATEST" != "true" ]]; then
      echo "ERROR (DigitalOcean profile): a public/evidence deployment requires an immutable image tag."
      echo "  Pin a sha-<commit> (or a v* release tag) so the deploy is reproducible and recorded:"
      echo "    ./tools/deploy-digitalocean.sh --domain ${DOMAIN:-<domain>} --tag sha-<commit>"
      echo "  To deploy with a mutable tag anyway (not valid for evidence), add --allow-latest."
      exit 1
    fi
    echo "  WARNING: no image tag supplied (--allow-latest). Not valid for release evidence."
  elif [[ "$IMAGE_TAG" == "latest" ]]; then
    if [[ "$ALLOW_LATEST" != "true" ]]; then
      echo "ERROR (DigitalOcean profile): tag 'latest' is mutable and not valid for evidence deployments."
      echo "  Pin a sha-<commit> (or a v* release tag) so the deploy is reproducible and recorded:"
      echo "    ./tools/deploy-digitalocean.sh --domain ${DOMAIN:-<domain>} --tag sha-<commit>"
      echo "  To deploy the moving 'latest' tag anyway (not valid for evidence), add --allow-latest."
      exit 1
    fi
    echo "  WARNING: deploying moving tag 'latest' (--allow-latest). Not valid for release evidence."
  elif ! _tag_is_immutable "$IMAGE_TAG"; then
    if [[ "$ALLOW_LATEST" != "true" ]]; then
      echo "ERROR (DigitalOcean profile): tag '$IMAGE_TAG' is not an immutable sha-<commit> or v* tag."
      echo "  Registry tags (other than digests) are mutable by default — the same tag can later point"
      echo "  to a different image, making the deploy non-reproducible and invalid for evidence."
      echo "  Use a pinned tag:  --tag sha-<commit>   or   --tag v1.2.3"
      echo "  To deploy a mutable tag anyway (not valid for evidence), add --allow-latest."
      exit 1
    fi
    echo "  WARNING: deploying mutable tag '$IMAGE_TAG' (--allow-latest). Not valid for release evidence."
  fi
fi
if [[ -n "$IMAGE_TAG" ]]; then
  export FPS_IMAGE_TAG="$IMAGE_TAG"
fi
note "image tag: ${IMAGE_TAG:-<compose default>}"

# 7. Disk-availability guidance (advisory; never mutates disks).
avail_kb="$(df -Pk "$INFRA_DIR" 2>/dev/null | awk 'NR==2 {print $4}')"
if [[ -n "$avail_kb" ]]; then
  avail_gb=$(( avail_kb / 1024 / 1024 ))
  if (( avail_gb < 10 )); then
    note "WARNING: only ${avail_gb} GiB free on the FairSpot volume. Images + durable data want more headroom."
  else
    note "disk: ${avail_gb} GiB free on the FairSpot volume"
  fi
fi

# 8. Public-boundary safety gate (fail-closed). Render the exact merged profile
#    and assert NO service publishes a port on a public interface. The rendered
#    config contains secrets, so it is never printed — only the port structure is
#    inspected. This also proves every required env value is present (compose
#    aborts naming a missing variable, not its value) and that the Compose
#    version supports the overlay's !reset/!override merge tags.
hdr_render() {
  docker compose --project-directory "$INFRA_DIR" --env-file "$ENV_FILE" \
    -f "$INFRA_DIR/docker-compose.yaml" \
    -f "$INFRA_DIR/docker-compose.services.images.yml" \
    -f "$INFRA_DIR/docker-compose.dapr.yml" \
    -f "$INFRA_DIR/docker-compose.nas.yml" \
    -f "$INFRA_DIR/docker-compose.services.nas.yml" \
    -f "$INFRA_DIR/docker-compose.digitalocean.yml" \
    config
}
render_err="$(mktemp)"
trap 'find "$render_err" -maxdepth 0 -type f -delete 2>/dev/null || true' EXIT
if ! rendered="$(hdr_render 2>"$render_err")"; then
  echo "ERROR (DigitalOcean profile): the merged Compose profile did not render."
  echo "  Likely a missing required value in $ENV_FILE, or a Docker Compose too old for the"
  echo "  overlay's !reset/!override merge tags (needs a current Compose v2). Details:"
  sed 's/^/    /' "$render_err"
  exit 1
fi
# Every published port must be pinned to loopback (127.0.0.1). Any published port
# with host_ip 0.0.0.0/:: or with no host_ip (the public default) fails closed.
port_lines="$(printf '%s\n' "$rendered" | grep -E 'published:|host_ip:' || true)"
unset rendered
published_count="$(printf '%s\n' "$port_lines" | grep -c 'published:' || true)"
loopback_count="$(printf '%s\n' "$port_lines" | grep -c 'host_ip: 127\.0\.0\.1' || true)"
public_hits="$(printf '%s\n' "$port_lines" | grep -E 'host_ip: (0\.0\.0\.0|::)' || true)"
if [[ -n "$public_hits" || "$published_count" != "$loopback_count" ]]; then
  echo "ERROR (DigitalOcean profile): refusing to deploy — a service would publish a PUBLIC host port."
  echo "  The DigitalOcean overlay must suppress every public binding (only loopback is allowed)."
  echo "  published=$published_count loopback=$loopback_count"
  [[ -n "$public_hits" ]] && printf '    %s\n' "$public_hits"
  exit 1
fi
note "public-boundary check: no public host ports (published=$published_count, all loopback)"

# Daemon reachability — last preflight step, right before we start containers, so
# every input/flag/render error above fails fast without needing a live daemon.
if ! docker info >/dev/null 2>&1; then
  echo "ERROR (DigitalOcean profile): cannot talk to the Docker daemon."
  echo "  Run as a user in the 'docker' group (preferred) or start the daemon."
  exit 1
fi
note "docker daemon: reachable"

echo
echo "== FairSpot DigitalOcean deployment =="
echo "Stack env:  $ENV_FILE"
echo "Domain:     ${DOMAIN:-not set}"
echo "Image tag:  ${IMAGE_TAG:-<compose default>}"
echo

# ── Start the DigitalOcean profile ───────────────────────────────────────────
"$REPO_ROOT/tools/start-container-stack.sh" --digitalocean --env-file "$ENV_FILE"

# ── Cloudflare Tunnel connector (the only ingress) ───────────────────────────
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

# ── Public-domain smoke ──────────────────────────────────────────────────────
if [[ "$SKIP_PUBLIC" != "true" && -n "$DOMAIN" ]]; then
  echo
  echo "== Public-domain smoke =="
  "$REPO_ROOT/tools/start-container-stack.sh" --digitalocean --env-file "$ENV_FILE" --domain "$DOMAIN"
elif [[ "$SKIP_PUBLIC" != "true" ]]; then
  echo
  echo "INFO: Public-domain smoke skipped because --domain was not provided."
fi

echo
echo "Deployment command completed."
GRAFANA_HOST_PORT="${FPS_GRAFANA_HOST_PORT:-$(read_env_value FPS_GRAFANA_HOST_PORT)}"
GRAFANA_HOST_PORT="${GRAFANA_HOST_PORT:-3001}"
echo "Internal Grafana (loopback only): reach via an SSH tunnel from your workstation:"
echo "  ssh -L $GRAFANA_HOST_PORT:127.0.0.1:$GRAFANA_HOST_PORT <droplet>  # then http://localhost:$GRAFANA_HOST_PORT"
echo "Stop (preserve volumes): ./tools/deploy-digitalocean.sh --down"
