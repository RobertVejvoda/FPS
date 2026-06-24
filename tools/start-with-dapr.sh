#!/bin/sh
# start-with-dapr.sh — Start FPS services with Dapr sidecars for local smoke testing.
#
# Uses the Dapr CLI multi-app run file (dapr.yaml) which starts nine FPS services
# each paired with a Dapr sidecar using durable local components (MongoDB + Vault).
#
# Identity does not need a sidecar and must be started separately:
#   source ./tools/dev-env.sh
#   dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
#
# Usage:
#   source ./tools/dev-env.sh        # export local Keycloak issuer settings
#   ./tools/start-with-dapr.sh       # start seven FPS services with Dapr sidecars
#
# Requirements:
#   - Dapr CLI >= 1.18 installed and initialised (dapr init)
#     https://docs.dapr.io/getting-started/install-dapr-cli/
#   - Docker Compose infrastructure running:
#       docker compose -f code/infrastructure/docker-compose.yaml up -d
#   - Auth set up: ./tools/dev-setup-auth.sh
#   - Local env sourced: source ./tools/dev-env.sh
#   - Vault seeded with MongoDB credentials (done by start-local-harness.sh):
#       ./tools/start-local-harness.sh
#
# State is persisted to MongoDB. Vault must be running and seeded before starting
# services — this script will abort with a clear error if the preflight check fails.
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

# Preflight: confirm Vault is reachable and mongodb-credentials are seeded.
# Without this the local state-store components cannot initialise and Customer
# will exhaust 60 × 5s health retries before reporting failure. (#564)
VAULT_TOKEN="${VAULT_TOKEN:-root}"
VAULT_ADDR="${VAULT_ADDR:-http://localhost:8200}"
echo "Checking Vault at $VAULT_ADDR ..."
if ! curl -sf -o /dev/null -H "X-Vault-Token: $VAULT_TOKEN" "$VAULT_ADDR/v1/secret/data/mongodb-credentials"; then
  echo ""
  echo "ERROR: Vault is not reachable or mongodb-credentials are not seeded."
  echo "  Run the full harness first:"
  echo "    ./tools/start-local-harness.sh"
  echo ""
  echo "  Or seed Vault manually (Docker Compose must be up):"
  echo "    curl -s --request POST $VAULT_ADDR/v1/secret/data/mongodb-credentials \\"
  echo "      -H 'X-Vault-Token: $VAULT_TOKEN' \\"
  echo "      -H 'Content-Type: application/json' \\"
  echo "      -d '{\"data\":{\"username\":\"admin\",\"password\":\"admin\"}}'"
  exit 1
fi
echo "Vault OK — mongodb-credentials present."

echo "Starting FPS services with Dapr sidecars..."
echo "  Config: $REPO_ROOT/dapr.yaml"
echo ""
echo "Start Identity separately (no Dapr sidecar needed):"
echo "  source ./tools/dev-env.sh"
echo "  dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj"
echo ""

cd "$REPO_ROOT"
exec dapr run -f dapr.yaml
