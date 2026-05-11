#!/usr/bin/env bash
# Regenerates TypeScript API client from running FPS services.
# Usage: ./tools/generate-api-client.sh
# Run from repo root. Services are started temporarily on localhost.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$REPO_ROOT/code/clients/typescript"
OPENAPI_DIR="$OUT_DIR/openapi"
SRC_DIR="$OUT_DIR/src"

declare -A SERVICES=(
  ["identity"]="code/server/Identity/FPS.Identity:5100"
  ["booking"]="code/server/Booking/FPS.Booking.API:5101"
  ["profile"]="code/server/Profile/FPS.Profile:5102"
)

mkdir -p "$OPENAPI_DIR" "$SRC_DIR"

# Start a service, wait for it, capture OpenAPI JSON, stop it.
capture_openapi() {
  local name="$1"
  local project_path="$2"
  local port="$3"
  local out_file="$OPENAPI_DIR/${name}.json"

  echo "[generate] Starting $name on :$port"
  dotnet run \
    --project "$REPO_ROOT/$project_path" \
    --no-build \
    --urls "http://localhost:$port" \
    --environment Development \
    2>/dev/null &
  local pid=$!

  local ready=false
  for i in {1..30}; do
    if curl -sf "http://localhost:$port/openapi/v1.json" -o /dev/null 2>/dev/null; then
      ready=true
      break
    fi
    sleep 1
  done

  if ! $ready; then
    kill "$pid" 2>/dev/null || true
    echo "[generate] ERROR: $name did not become ready on :$port" >&2
    exit 1
  fi

  curl -sf "http://localhost:$port/openapi/v1.json" -o "$out_file"
  echo "[generate] Captured $out_file"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
}

# Build all services once before starting them.
echo "[generate] Building services..."
dotnet build "$REPO_ROOT/code/server/FPS.sln" -c Release -v q --nologo 2>/dev/null

for name in "${!SERVICES[@]}"; do
  IFS=: read -r path port <<< "${SERVICES[$name]}"
  capture_openapi "$name" "$path" "$port"
done

# Generate TypeScript types from captured OpenAPI JSON.
if ! command -v npx &>/dev/null; then
  echo "[generate] ERROR: npx not found — run: npm install -g npm" >&2
  exit 1
fi

echo "[generate] Generating TypeScript types..."
for name in "${!SERVICES[@]}"; do
  npx openapi-typescript "$OPENAPI_DIR/${name}.json" -o "$SRC_DIR/${name}.d.ts"
  echo "[generate] Generated $SRC_DIR/${name}.d.ts"
done

echo "[generate] Done. Commit $OPENAPI_DIR/ and $SRC_DIR/ to keep them up to date."
