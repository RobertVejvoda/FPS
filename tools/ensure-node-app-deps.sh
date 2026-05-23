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

# Prefer user-installed Node/npm over any embedded tool runtime that may appear
# first on PATH. On macOS, native optional packages such as Rollup can fail
# dlopen when loaded by a differently signed embedded Node binary.
export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:/usr/local/bin:$PATH"

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
  repair_macos_native_modules
}

repair_macos_native_modules() {
  if [ "$(uname -s)" != "Darwin" ] || ! command -v codesign >/dev/null 2>&1; then
    return 0
  fi

  # Some npm-installed native optional dependencies can fail dlopen on macOS
  # with a Team ID / code signature mismatch. Re-sign local .node binaries
  # ad hoc after install so Vite/Rollup/Expo can load them.
  find node_modules -type f -name '*.node' -print 2>/dev/null | while IFS= read -r native_module; do
    codesign --force --sign - "$native_module" >/dev/null 2>&1 || true
  done
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
  if ! sh -c "$PROBE" >/dev/null 2>&1; then
    echo "ERROR: dependency probe still fails after reinstall in $APP_DIR." >&2
    echo "Try removing node_modules and rerun the smoke script, or inspect the npm/native package error above." >&2
    exit 1
  fi
fi
