#!/bin/sh
# dev-auth.sh — Request a local development access token from Keycloak.
# Prints the access token only — pipe to clipboard or paste into the app.
#
# Usage:
#   ./tools/dev-auth.sh <username>
#   ./tools/dev-auth.sh employee1
#   ./tools/dev-auth.sh hr-admin
#
# Environment:
#   KEYCLOAK_URL       default: http://localhost:8180
#   FPS_DEV_PASSWORD   default: Dev1234!  (set by dev-setup-auth.sh)
#
# Run ./tools/dev-setup-auth.sh once before using this script.
set -eu

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="fps-local"
CLIENT_ID="fps-mobile-dev"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"

USERNAME="${1:-}"
if [ -z "$USERNAME" ]; then
  echo "Usage: $0 <username>" >&2
  echo "  Available: employee1..employee25  hr-admin  tenant-admin  report-viewer  auditor" >&2
  exit 1
fi

RESPONSE=$(curl -s \
  -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=$CLIENT_ID" \
  --data-urlencode "username=$USERNAME" \
  --data-urlencode "password=$DEV_PASSWORD" || true)

ACCESS_TOKEN=$(printf '%s' "$RESPONSE" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
  echo "ERROR: Could not get token for '$USERNAME'." >&2
  ERROR_MESSAGE=$(printf '%s' "$RESPONSE" | grep -o '"error_description":"[^"]*"' | cut -d'"' -f4 || true)
  if [ -z "$ERROR_MESSAGE" ]; then
    ERROR_MESSAGE=$(printf '%s' "$RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || true)
  fi
  if [ -n "$ERROR_MESSAGE" ]; then
    echo "  Keycloak response: $ERROR_MESSAGE" >&2
  fi
  echo "  Check that dev-setup-auth.sh has been run and Keycloak is up." >&2
  exit 1
fi

printf '%s\n' "$ACCESS_TOKEN"
