#!/bin/sh
# ensure-node-app-deps.sh — Ensure a Node app has a usable dependency tree.
#
# Usage:
#   sh ./tools/ensure-node-app-deps.sh <app-dir> [probe-command]
#
# If node_modules is missing, or the optional probe command fails, the script
# repairs dependencies from the lockfile with npm ci. This handles npm optional
# dependency/native package issues such as Rollup's platform package being
# missing or invalid after a partial install.
set -eu

APP_DIR="${1:-}"
PROBE="${2:-}"

if [ -z "$APP_DIR" ]; then
  echo "Usage: $0 <app-dir> [probe-command]" >&2
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "ERROR: npm not found. Install Node.js/npm before running this smoke script." >&2
  exit 1
fi

cd "$APP_DIR"

install_deps() {
  if [ -f package-lock.json ]; then
    npm ci
  else
    npm install
  fi
}

if [ ! -d node_modules ]; then
  echo "[deps] node_modules missing in $APP_DIR; installing dependencies..."
  install_deps
  exit 0
fi

if [ -n "$PROBE" ] && ! sh -c "$PROBE" >/dev/null 2>&1; then
  echo "[deps] Existing node_modules failed dependency probe in $APP_DIR."
  echo "[deps] Reinstalling from lockfile to repair optional/native dependencies..."
  install_deps
fi
