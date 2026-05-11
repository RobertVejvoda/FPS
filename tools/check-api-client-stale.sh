#!/usr/bin/env bash
# Fails if the generated TypeScript client is out of date with the OpenAPI output.
# Usage: ./tools/check-api-client-stale.sh
# Run from repo root after building services.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

OPENAPI_DIR="$REPO_ROOT/code/clients/typescript/openapi"
SRC_DIR="$REPO_ROOT/code/clients/typescript/src"
TMP_OPENAPI="$TMP_DIR/openapi"
TMP_SRC="$TMP_DIR/src"

mkdir -p "$TMP_OPENAPI" "$TMP_SRC"

SERVICES="identity:code/server/Identity/FPS.Identity:5110
booking:code/server/Booking/FPS.Booking.API:5111
profile:code/server/Profile/FPS.Profile:5112"

stale=false
OPENAPI_TS_VERSION="$(node -e "console.log(require('$REPO_ROOT/code/clients/typescript/package.json').devDependencies['openapi-typescript'])")"

while IFS=: read -r name path port; do
  out_file="$TMP_OPENAPI/${name}.json"

  ASPNETCORE_ENVIRONMENT=Production dotnet run \
    --project "$REPO_ROOT/$path" \
    --no-build \
    -c Release \
    --no-launch-profile \
    --urls "http://localhost:$port" \
    2>/dev/null &
  pid=$!

  ready=false
  for i in {1..30}; do
    if curl -sf "http://localhost:$port/openapi/v1.json" -o /dev/null 2>/dev/null; then
      ready=true; break
    fi
    sleep 1
  done

  if ! $ready; then
    kill "$pid" 2>/dev/null || true
    echo "[stale-check] ERROR: $name did not become ready" >&2
    exit 1
  fi

  curl -sf "http://localhost:$port/openapi/v1.json" -o "$out_file"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true

  if ! diff -q "$OPENAPI_DIR/${name}.json" "$out_file" >/dev/null 2>&1; then
    echo "[stale-check] STALE: $name OpenAPI JSON has changed — run tools/generate-api-client.sh"
    stale=true
  fi

  if command -v npx &>/dev/null; then
    npx --yes --package="openapi-typescript@${OPENAPI_TS_VERSION}" -- openapi-typescript "$out_file" -o "$TMP_SRC/${name}.d.ts" 2>/dev/null
    if ! diff -q "$SRC_DIR/${name}.d.ts" "$TMP_SRC/${name}.d.ts" >/dev/null 2>&1; then
      echo "[stale-check] STALE: $name TypeScript types have changed — run tools/generate-api-client.sh"
      stale=true
    fi
  fi
done <<< "$SERVICES"

if $stale; then
  echo "[stale-check] FAILED — generated client is out of date"
  exit 1
fi

echo "[stale-check] OK — generated client matches current API"
