#!/bin/sh
# dev-seed.sh — Seed local demo profile data for FPS smoke testing.
#
# Requires:
#   - Services running (Identity on :5192, Profile on :5197, Notification on :5157)
#   - ./tools/dev-setup-auth.sh completed
#   - source ./tools/dev-env.sh
#
# Usage:
#   ./tools/dev-seed.sh
#   ./tools/dev-seed.sh --reset   (re-seeds; safe to run multiple times)
#
# Seeded data:
#   - Profile snapshots for employee1, employee2, employee3 via GET /me + POST /profile/admin/snapshot
#   - Configuration (policy + slots) is seeded automatically by Configuration service on startup
#   - Bookings: empty list is the documented baseline (GET /bookings returns 200 [])
#   - Notifications: empty is the documented baseline (unread-count returns 0)

set -eu

PROFILE_URL="${PROFILE_URL:-http://localhost:5197}"
IDENTITY_URL="${IDENTITY_URL:-http://localhost:5192}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"

echo "== FPS local demo seed =="
echo "Profile service: $PROFILE_URL"
echo ""

# Check services are reachable
for check_url in "$IDENTITY_URL/openapi/v1.json" "$PROFILE_URL/openapi/v1.json"; do
  if ! curl -sf "$check_url" > /dev/null 2>&1; then
    echo "ERROR: Service not reachable at $check_url"
    echo "  Start Identity and Profile (with Dapr sidecars via dapr run -f dapr.yaml)"
    exit 1
  fi
done

# Decode sub from JWT payload using python3 (no external deps)
jwt_sub() {
  python3 - "$1" << 'PYEOF'
import base64, json, sys
token = sys.argv[1]
payload = token.split('.')[1]
payload += '=' * (-len(payload) % 4)
data = json.loads(base64.urlsafe_b64decode(payload))
print(data['sub'])
PYEOF
}

seed_profile() {
  USERNAME="$1"
  HAS_COMPANY_CAR="$2"
  HAS_VEHICLE="$3"
  ACCESSIBILITY="$4"

  echo "Seeding profile for $USERNAME..."

  TOKEN=$(curl -sf \
    -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=$CLIENT_ID&username=$USERNAME&password=$DEV_PASSWORD" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])")

  if [ -z "$TOKEN" ]; then
    echo "  ERROR: Could not get token for $USERNAME. Run ./tools/dev-setup-auth.sh first."
    return 1
  fi

  USER_ID=$(jwt_sub "$TOKEN")

  if [ "$HAS_VEHICLE" = "true" ]; then
    VEHICLES='[{"vehicleId":"VEH-'$USERNAME'","licensePlate":"'$(echo "$USERNAME" | tr '[:lower:]' '[:upper:]')'001","vehicleType":"Sedan","isElectric":false,"isActive":true}]'
  else
    VEHICLES='[]'
  fi

  STATUS=$(curl -sf -o /dev/null -w "%{http_code}" \
    -X PUT "$PROFILE_URL/profile/admin/snapshot" \
    -H "Content-Type: application/json" \
    -d "{
      \"tenantId\": \"tenant-1\",
      \"userId\": \"$USER_ID\",
      \"parkingEligible\": true,
      \"hasCompanyCar\": $HAS_COMPANY_CAR,
      \"accessibilityEligible\": $ACCESSIBILITY,
      \"reservedSpaceEligible\": false,
      \"vehicles\": $VEHICLES
    }")

  if [ "$STATUS" = "204" ]; then
    echo "  OK ($USERNAME -> userId=$USER_ID)"
  else
    echo "  ERROR: Profile seed returned HTTP $STATUS for $USERNAME"
    return 1
  fi
}

# Seed profiles
seed_profile "employee1" "false" "true"  "false"   # normal employee with vehicle
seed_profile "employee2" "true"  "false" "false"   # company-car employee, no vehicle
seed_profile "employee3" "false" "false" "true"    # accessibility-eligible, no vehicle

echo ""
echo "== Seed complete =="
echo "Configuration (policy + slots) is seeded by the Configuration service on startup."
echo "Bookings: GET /bookings returns 200 [] (empty list is the documented baseline)."
echo "Notifications: unread-count returns 0 (empty baseline)."
echo ""
echo "Verify through gateway:"
echo "  TOKEN=\$(./tools/dev-auth.sh employee1)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/profile/snapshot"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/bookings"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/notifications/unread-count"
