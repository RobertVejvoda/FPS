#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.dapr/bin:/usr/local/bin:$PATH"

echo "== FPS devcontainer setup =="

if [ ! -x "$HOME/.dapr/bin/daprd" ]; then
  echo "Initializing Dapr runtime in slim mode..."
  dapr init --slim
fi

echo "Dapr: $(dapr --version | tr '\n' ' ')"
echo ".NET: $(dotnet --version)"
echo "Node: $(node --version)"
echo "npm: $(npm --version)"

echo "Restoring .NET solution..."
dotnet restore code/server/FPS.sln

echo "Installing web dependencies..."
npm ci --prefix code/web/fps-web

echo "Installing mobile dependencies..."
npm ci --prefix code/mobile/fps-mobile

cat <<'EOF'

Devcontainer ready.

Recommended smoke flow:
  docker compose -f code/infrastructure/docker-compose.yaml up -d
  ./tools/start-smoke-web.sh

Use the host machine for physical mobile QR testing unless you intentionally
configure Expo tunnel or container networking for device access.
EOF
