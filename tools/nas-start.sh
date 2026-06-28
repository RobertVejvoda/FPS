#!/usr/bin/env bash
# Start the NAS/hosted FairSpot server runtime.
#
# This starts server-side runtime only: backend services, auth, web/API gateway,
# observability, Dapr, state stores, and Cloudflare Tunnel.
# Mobile is a client artifact in hosted profiles; no mobile dev server runs here.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

exec "$REPO_ROOT/tools/deploy-nas.sh" "$@"
