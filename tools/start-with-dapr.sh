#!/bin/sh
# start-with-dapr.sh — Start FPS services with Dapr sidecars for local smoke testing.
#
# Uses the Dapr CLI multi-app run file (dapr.yaml) which starts six FPS services
# each paired with a Dapr sidecar loaded with in-memory components.
#
# Identity does not need a sidecar and must be started separately:
#   source ./tools/dev-env.sh
#   dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
#
# Usage:
#   source ./tools/dev-env.sh        # export local Keycloak issuer settings
#   ./tools/start-with-dapr.sh       # start six FPS services with Dapr sidecars
#
# Requirements:
#   - Dapr CLI >= 1.12 installed and initialised (dapr init)
#     https://docs.dapr.io/getting-started/install-dapr-cli/
#   - Docker Compose infrastructure running:
#       docker compose -f code/infrastructure/docker-compose.yaml up -d
#   - Auth set up: ./tools/dev-setup-auth.sh
#   - Local env sourced: source ./tools/dev-env.sh
#
# In-memory components are used so no Vault or MongoDB credentials are required.
# State is not persisted across restarts. Use code/infrastructure/dapr/components/local
# for durable state when Vault and MongoDB are initialised.
set -eu

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if ! command -v dapr > /dev/null 2>&1; then
  echo "ERROR: Dapr CLI not found."
  echo "  Install: https://docs.dapr.io/getting-started/install-dapr-cli/"
  echo "  Then run: dapr init"
  exit 1
fi

DAPR_VERSION=$(dapr --version 2>/dev/null | grep "CLI version" | grep -o '[0-9]\+\.[0-9]\+' | head -1)
echo "Dapr CLI: $(dapr --version | head -2 | tr '\n' ' ')"
echo "Starting FPS services with Dapr sidecars..."
echo "  Config: $REPO_ROOT/dapr.yaml"
echo "  Components: code/infrastructure/dapr/components/smoke (in-memory)"
echo ""
echo "Start Identity separately (no Dapr sidecar needed):"
echo "  source ./tools/dev-env.sh"
echo "  dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj"
echo ""

cd "$REPO_ROOT"
exec dapr run -f dapr.yaml
