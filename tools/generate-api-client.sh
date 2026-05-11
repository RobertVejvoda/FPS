#!/usr/bin/env bash
# Regenerates TypeScript API client from running FPS services.
# Usage: ./tools/generate-api-client.sh
# Run from repo root. Services are started temporarily on localhost.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$REPO_ROOT/code/clients/typescript"
OPENAPI_DIR="$OUT_DIR/openapi"
SRC_DIR="$OUT_DIR/src"

SERVICES="identity:code/server/Identity/FPS.Identity:5100
booking:code/server/Booking/FPS.Booking.API:5101
profile:code/server/Profile/FPS.Profile:5102"

mkdir -p "$OPENAPI_DIR" "$SRC_DIR"

# Start a service, wait for it, capture OpenAPI JSON, stop it.
capture_openapi() {
  local name="$1"
  local project_path="$2"
  local port="$3"
  local out_file="$OPENAPI_DIR/${name}.json"

  echo "[generate] Starting $name on :$port"
  # Fail fast if something else is already listening on this port — otherwise
  # curl would silently capture stale output from the squatter process.
  if lsof -i ":$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo "[generate] ERROR: port $port already in use — kill the process before re-running" >&2
    exit 1
  fi
  ASPNETCORE_ENVIRONMENT=Production dotnet run \
    --project "$REPO_ROOT/$project_path" \
    --no-build \
    -c Release \
    --no-launch-profile \
    --urls "http://localhost:$port" \
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

while IFS=: read -r name path port; do
  capture_openapi "$name" "$path" "$port"
done <<< "$SERVICES"

# Generate TypeScript types from captured OpenAPI JSON.
if ! command -v npx &>/dev/null; then
  echo "[generate] ERROR: npx not found — run: npm install -g npm" >&2
  exit 1
fi

# Pin generator version exactly so output stays deterministic.
OPENAPI_TS_VERSION="$(node -e "console.log(require('$OUT_DIR/package.json').devDependencies['openapi-typescript'])")"
echo "[generate] Generating TypeScript types with openapi-typescript@${OPENAPI_TS_VERSION}..."
while IFS=: read -r name path port; do
  npx --yes --package="openapi-typescript@${OPENAPI_TS_VERSION}" -- openapi-typescript "$OPENAPI_DIR/${name}.json" -o "$SRC_DIR/${name}.d.ts"
  echo "[generate] Generated $SRC_DIR/${name}.d.ts"
done <<< "$SERVICES"

echo "[generate] Done. Commit $OPENAPI_DIR/ and $SRC_DIR/ to keep them up to date."
