#!/bin/sh
# dev-setup-auth.sh — One-time local Keycloak setup for FPS development.
# Imports the fps-local realm and sets dev passwords for demo users.
# Run once after `docker compose up` when Keycloak is ready.
#
# Usage:
#   ./tools/dev-setup-auth.sh
#   FPS_DEV_PASSWORD=MyPass123 ./tools/dev-setup-auth.sh
#
# Default dev password: Dev1234!  (local only, never commit real passwords)
set -eu

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
ADMIN_USER="${KC_BOOTSTRAP_ADMIN_USERNAME:-${KEYCLOAK_ADMIN:-admin}}"
ADMIN_PASS="${KC_BOOTSTRAP_ADMIN_PASSWORD:-${KEYCLOAK_ADMIN_PASSWORD:-admin}}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"
REALM="fps-local"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REALM_FILE="$(dirname "$0")/../code/infrastructure/keycloak/fps-local-realm.json"
USERS="employee1 employee2 employee3 hr-admin tenant-admin report-viewer auditor"

echo "== FPS local Keycloak setup =="
echo "Keycloak: $KEYCLOAK_URL"

get_admin_token() {
  TOKEN_BODY=$(curl -s \
    -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=admin-cli" \
    --data-urlencode "username=$ADMIN_USER" \
    --data-urlencode "password=$ADMIN_PASS" || true)
  printf '%s' "$TOKEN_BODY" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4 || true
}

keycloak_error_message() {
  printf '%s' "$TOKEN_BODY" | grep -o '"error_description":"[^"]*"' | cut -d'"' -f4 || true
}

keycloak_error_code() {
  printf '%s' "$TOKEN_BODY" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || true
}

# Wait for Keycloak to be ready
echo "Waiting for Keycloak..."
for i in $(seq 1 30); do
  if curl -sf "$KEYCLOAK_URL/realms/master" > /dev/null 2>&1; then
    echo "Keycloak is up."
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo "ERROR: Keycloak did not become ready. Is docker compose running?"
    exit 1
  fi
  sleep 2
done

# Get admin access token from master realm. The realm endpoint can respond
# before the admin password grant is ready, especially after a fresh container
# boot, so wait for the actual admin auth path.
echo "Waiting for admin auth..."
ADMIN_TOKEN=""
TOKEN_BODY=""
for i in $(seq 1 5); do
  ADMIN_TOKEN=$(get_admin_token)
  if [ -n "$ADMIN_TOKEN" ]; then
    break
  fi
  ERROR_MESSAGE=$(keycloak_error_message)
  ERROR_CODE=$(keycloak_error_code)
  if [ "$ERROR_MESSAGE" = "HTTPS required" ]; then
    break
  fi
  if [ -n "$ERROR_MESSAGE" ]; then
    echo "Admin auth attempt $i/5 not ready: $ERROR_MESSAGE"
  elif [ -n "$ERROR_CODE" ]; then
    echo "Admin auth attempt $i/5 not ready: $ERROR_CODE"
  else
    echo "Admin auth attempt $i/5 not ready yet."
  fi
  sleep 2
done

if [ -z "$ADMIN_TOKEN" ] && command -v docker > /dev/null 2>&1; then
  ERROR_MESSAGE=$(keycloak_error_message)
  if [ -n "$ERROR_MESSAGE" ]; then
    echo "Admin auth not ready from host: $ERROR_MESSAGE"
  fi
  echo "Trying local-container admin setup..."
  if ! docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T \
      -e FPS_KEYCLOAK_ADMIN_USER="$ADMIN_USER" \
      -e FPS_KEYCLOAK_ADMIN_PASSWORD="$ADMIN_PASS" \
      keycloak sh -lc '
        /opt/keycloak/bin/kcadm.sh config credentials \
          --server http://localhost:8080 \
          --realm master \
          --user "$FPS_KEYCLOAK_ADMIN_USER" \
          --password "$FPS_KEYCLOAK_ADMIN_PASSWORD" >/dev/null &&
        /opt/keycloak/bin/kcadm.sh update realms/master -s sslRequired=NONE
      '; then
    echo "WARNING: Local-container admin setup failed."
  else
    for i in $(seq 1 15); do
      ADMIN_TOKEN=$(get_admin_token)
      if [ -n "$ADMIN_TOKEN" ]; then
        break
      fi
      sleep 2
    done
  fi
fi

if [ -z "$ADMIN_TOKEN" ]; then
  echo "ERROR: Could not get admin token from Keycloak."
  ERROR_MESSAGE=$(keycloak_error_message)
  if [ -n "$ERROR_MESSAGE" ]; then
    echo "Keycloak response: $ERROR_MESSAGE"
  fi
  exit 1
fi
echo "Admin auth: ok"

# Import realm (idempotent: delete first if it already exists)
EXISTING=$(curl -sf -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$KEYCLOAK_URL/admin/realms/$REALM" || true)

if [ "$EXISTING" = "200" ]; then
  echo "Realm '$REALM' already exists, deleting for clean import..."
  curl -sf -X DELETE \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$KEYCLOAK_URL/admin/realms/$REALM"
fi

echo "Importing realm '$REALM'..."
curl -sf -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "@$REALM_FILE" \
  "$KEYCLOAK_URL/admin/realms"
echo "Realm imported."

# Set dev passwords for demo users
for USERNAME in $USERS; do
  USER_ID=$(curl -sf \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$KEYCLOAK_URL/admin/realms/$REALM/users?username=$USERNAME" \
    | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

  if [ -z "$USER_ID" ]; then
    echo "WARNING: User '$USERNAME' not found after import, skipping."
    continue
  fi

  curl -sf -X PUT \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"type\":\"password\",\"value\":\"$DEV_PASSWORD\",\"temporary\":false}" \
    "$KEYCLOAK_URL/admin/realms/$REALM/users/$USER_ID/reset-password"

  echo "Password set: $USERNAME"
done

echo ""
echo "== Setup complete =="
echo "Realm:    $REALM"
echo "Users:    $USERS"
echo "Password: \$FPS_DEV_PASSWORD (default: Dev1234!)"
echo ""
echo "Get a dev token:"
echo "  ./tools/dev-auth.sh employee1"
echo ""
echo "Before running backend services, export local issuer settings:"
echo "  source ./tools/dev-env.sh"
echo "  dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj"
