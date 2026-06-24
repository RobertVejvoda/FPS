#!/bin/sh
# dev-setup-auth.sh — One-time local Keycloak setup for FPS development.
# Imports the fps-local realm and sets dev passwords for demo users.
# Run once after `docker compose up` when Keycloak is ready.
#
# Usage:
#   ./tools/dev-setup-auth.sh
#   FPS_DEV_PASSWORD=MyPass123 ./tools/dev-setup-auth.sh
#   FPS_GL_EMPLOYEE_COUNT=50 ./tools/dev-setup-auth.sh   # add 50 GL employees for PERF001 load tests
#
# Environment variables:
#   FPS_DEMO_EMPLOYEE_COUNT   number of demo-tenant employees (default 25, max supported 25)
#   FPS_GL_EMPLOYEE_COUNT     number of Green Logistics employees (default 1, gl-employee1 is always present)
#
# Default dev password: Dev1234!  (local only, never commit real passwords)
set -eu

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
ADMIN_USER="${KC_BOOTSTRAP_ADMIN_USERNAME:-${KEYCLOAK_ADMIN:-admin}}"
ADMIN_PASS="${KC_BOOTSTRAP_ADMIN_PASSWORD:-${KEYCLOAK_ADMIN_PASSWORD:-admin}}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"
DEMO_EMPLOYEE_COUNT="${FPS_DEMO_EMPLOYEE_COUNT:-25}"
GL_EMPLOYEE_COUNT="${FPS_GL_EMPLOYEE_COUNT:-1}"
REALM="fps-local"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REALM_FILE="$(dirname "$0")/../code/infrastructure/keycloak/fps-local-realm.json"
IMPORT_REALM_FILE="$REALM_FILE"
TMP_REALM_FILE=""
USERS="employee1 employee2 employee3 hr-admin tenant-admin report-viewer auditor gl-employee1 gl-tenant-admin gl-hr-admin gl-auditor gl-report-viewer"

if [ "$DEMO_EMPLOYEE_COUNT" -gt 3 ]; then
  for i in $(seq 4 "$DEMO_EMPLOYEE_COUNT"); do
    USERS="$USERS employee$i"
  done
fi

if [ "$GL_EMPLOYEE_COUNT" -gt 1 ]; then
  for i in $(seq 2 "$GL_EMPLOYEE_COUNT"); do
    USERS="$USERS gl-employee$i"
  done
fi

cleanup() {
  if [ -n "$TMP_REALM_FILE" ] && [ -f "$TMP_REALM_FILE" ]; then
    rm -f "$TMP_REALM_FILE"
  fi
}
trap cleanup EXIT

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

get_user_token() {
  USER_TOKEN_BODY=$(curl -s \
    -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=fps-mobile-dev" \
    --data-urlencode "username=$1" \
    --data-urlencode "password=$DEV_PASSWORD" || true)
  printf '%s' "$USER_TOKEN_BODY" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4 || true
}

jwt_claim() {
  python3 - "$1" "$2" << 'PYEOF'
import base64, json, sys
token, claim = sys.argv[1], sys.argv[2]
payload = token.split('.')[1]
payload += '=' * (-len(payload) % 4)
value = json.loads(base64.urlsafe_b64decode(payload)).get(claim, "")
if isinstance(value, list):
    print(",".join(str(v) for v in value))
else:
    print(value)
PYEOF
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

if [ "$DEMO_EMPLOYEE_COUNT" -gt 3 ] || [ "$GL_EMPLOYEE_COUNT" -gt 1 ]; then
  TMP_REALM_FILE="$(mktemp)"
  python3 - "$REALM_FILE" "$TMP_REALM_FILE" "$DEMO_EMPLOYEE_COUNT" "$GL_EMPLOYEE_COUNT" << 'PYEOF'
import json
import sys

source, target, demo_count_arg, gl_count_arg = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
demo_count = int(demo_count_arg)
gl_count = int(gl_count_arg)

demo_names = {
    4: ("Pavel", "Cerny"),
    5: ("Hana", "Vesela"),
    6: ("Martin", "Horak"),
    7: ("Jana", "Kucerova"),
    8: ("Petr", "Svoboda"),
    9: ("Lenka", "Maresova"),
    10: ("Michal", "Prochazka"),
    11: ("Veronika", "Dvorakova"),
    12: ("Tomas", "Kral"),
    13: ("Barbora", "Urbanova"),
    14: ("Filip", "Sedlak"),
    15: ("Lucie", "Novakova"),
    16: ("Jakub", "Sima"),
    17: ("Alena", "Pokorna"),
    18: ("Radek", "Fiala"),
    19: ("Marketa", "Blazkova"),
    20: ("David", "Vacek"),
    21: ("Katerina", "Hruba"),
    22: ("Ondrej", "Marek"),
    23: ("Zuzana", "Krejci"),
    24: ("Milan", "Tichy"),
    25: ("Ivana", "Ruzickova"),
}

# GL employee names (indices 2..N; index 1 is gl-employee1 = Alice Green in realm JSON)
gl_names = {
    2:  ("Carla",   "Novak"),
    3:  ("David",   "Maly"),
    4:  ("Eva",     "Kratka"),
    5:  ("Filip",   "Dlouhy"),
    6:  ("Gabriela","Silna"),
    7:  ("Hana",    "Bílá"),
    8:  ("Ivan",    "Cerny"),
    9:  ("Jana",    "Ruda"),
    10: ("Karel",   "Zeleny"),
    11: ("Lenka",   "Modra"),
    12: ("Milan",   "Zlaty"),
    13: ("Nina",    "Stribr"),
    14: ("Ondrej",  "Horni"),
    15: ("Petra",   "Dolni"),
    16: ("Radek",   "Levy"),
    17: ("Sandra",  "Pravy"),
    18: ("Tomas",   "Velky"),
    19: ("Ula",     "Maly"),
    20: ("Vaclav",  "Stary"),
    21: ("Wendy",   "Novy"),
    22: ("Xena",    "Prvni"),
    23: ("Yuri",    "Druhy"),
    24: ("Zuzana",  "Treti"),
    25: ("Adam",    "Ctvrty"),
}

with open(source, encoding="utf-8") as f:
    realm = json.load(f)

users = realm.setdefault("users", [])
existing = {u.get("username") for u in users}

for index in range(4, demo_count + 1):
    username = f"employee{index}"
    if username in existing:
        continue
    first, last = demo_names.get(index, ("Demo", f"Employee{index}"))
    users.append({
        "username": username,
        "enabled": True,
        "email": f"{username}@demo-company.local",
        "firstName": first,
        "lastName": last,
        "attributes": {"tenant_id": ["demo"]},
        "realmRoles": ["employee"],
        "credentials": []
    })

for index in range(2, gl_count + 1):
    username = f"gl-employee{index}"
    if username in existing:
        continue
    first, last = gl_names.get(index, ("GL", f"Employee{index}"))
    users.append({
        "username": username,
        "enabled": True,
        "email": f"{username}@greenlogistics.example",
        "firstName": first,
        "lastName": last,
        "attributes": {"tenant_id": ["greenlogistics"]},
        "realmRoles": ["employee"],
        "credentials": []
    })

with open(target, "w", encoding="utf-8") as f:
    json.dump(realm, f, indent=2)
    f.write("\n")
PYEOF
  IMPORT_REALM_FILE="$TMP_REALM_FILE"
fi

echo "Importing realm '$REALM'..."
curl -sf -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "@$IMPORT_REALM_FILE" \
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

echo "Validating demo token claims..."
for USERNAME in employee1 employee4; do
  TOKEN=$(get_user_token "$USERNAME")
  if [ -z "$TOKEN" ]; then
    echo "ERROR: Could not get validation token for '$USERNAME'."
    exit 1
  fi
  TENANT_ID=$(jwt_claim "$TOKEN" tenant_id)
  if [ "$TENANT_ID" != "demo" ]; then
    echo "ERROR: Token for '$USERNAME' has tenant_id='$TENANT_ID' (expected demo)."
    exit 1
  fi
done
echo "Demo token claims: ok"

echo "Validating Green Logistics token claims..."
for USERNAME in gl-employee1 gl-tenant-admin gl-hr-admin gl-auditor gl-report-viewer; do
  TOKEN=$(get_user_token "$USERNAME")
  if [ -z "$TOKEN" ]; then
    echo "ERROR: Could not get validation token for '$USERNAME'."
    exit 1
  fi
  TENANT_ID=$(jwt_claim "$TOKEN" tenant_id)
  if [ "$TENANT_ID" != "greenlogistics" ]; then
    echo "ERROR: Token for '$USERNAME' has tenant_id='$TENANT_ID' (expected greenlogistics)."
    exit 1
  fi
done
echo "Green Logistics token claims: ok"

if [ "$GL_EMPLOYEE_COUNT" -gt 1 ]; then
  echo "Validating extended GL employee token claims..."
  LAST_GL="gl-employee$GL_EMPLOYEE_COUNT"
  TOKEN=$(get_user_token "$LAST_GL")
  if [ -z "$TOKEN" ]; then
    echo "ERROR: Could not get validation token for '$LAST_GL'."
    exit 1
  fi
  TENANT_ID=$(jwt_claim "$TOKEN" tenant_id)
  if [ "$TENANT_ID" != "greenlogistics" ]; then
    echo "ERROR: Token for '$LAST_GL' has tenant_id='$TENANT_ID' (expected greenlogistics)."
    exit 1
  fi
  echo "Extended GL token claims: ok ($GL_EMPLOYEE_COUNT total GL employees)"
fi

echo ""
echo "== Setup complete =="
echo "Realm:    $REALM"
echo "Users:    $USERS"
echo "Password: \$FPS_DEV_PASSWORD (default: Dev1234!)"
echo ""
echo "Demo tenant (tenant_id=demo):"
echo "  ./tools/dev-auth.sh employee1"
echo ""
echo "Green Logistics tenant (tenant_id=greenlogistics):"
echo "  ./tools/dev-auth.sh gl-employee1"
echo "  ./tools/dev-auth.sh gl-tenant-admin"
echo "  ./tools/dev-auth.sh gl-hr-admin"
echo "  ./tools/dev-auth.sh gl-auditor"
echo "  ./tools/dev-auth.sh gl-report-viewer"
if [ "$GL_EMPLOYEE_COUNT" -gt 1 ]; then
  echo "  (plus gl-employee2..gl-employee$GL_EMPLOYEE_COUNT from FPS_GL_EMPLOYEE_COUNT=$GL_EMPLOYEE_COUNT)"
fi
echo ""
echo "For load-test seed (PERF001):"
echo "  FPS_GL_EMPLOYEE_COUNT=50 ./tools/dev-setup-auth.sh"
echo "  ./tools/perf-seed-greenlogistics.sh"
echo ""
echo "Before running backend services, export local issuer settings:"
echo "  source ./tools/dev-env.sh"
echo "  dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj"
