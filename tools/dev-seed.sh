#!/usr/bin/env bash
# dev-seed.sh — Seed local demo data for FPS smoke testing.
#
# Requires:
#   - Services running: Identity (:5192), Profile (:5197), Booking (:5131)
#   - ./tools/dev-setup-auth.sh completed (Keycloak clients configured)
#
# Usage:
#   ./tools/dev-seed.sh   — seed all demo data
#
# Idempotency:
#   Profile seeding is idempotent (overwrites existing snapshot).
#   Booking seeding is NOT idempotent — repeated runs create duplicate requests.
#   To reset bookings, restart the local harness (in-memory store is cleared on restart):
#     ./tools/stop-local-harness.sh && ./tools/start-local-harness.sh && ./tools/dev-seed.sh
#
# What is seeded:
#   Profiles:  all 7 Keycloak demo users with parking eligibility, roles, and vehicles
#   Vehicles:  employee1 (sedan + EV), employee2 (company car), employee3 (accessible)
#   Bookings:  7 requests across upcoming dates, including a +2 demo Draw with all employees
#   Draw:      runs the +2 demo Draw so the demo immediately shows allocated/waitlisted outcomes
#   Admin profiles: hr-admin, tenant-admin, report-viewer, auditor (no parking)
#
#   Configuration (policy + slots) — seeded automatically by Configuration service on startup
#   Notifications, audit records, reporting — populated via Dapr events from booking submissions
#
# Seed data is demo-only. For pilot use, seed via tools/validate-hr-import.sh + POST /profile/bootstrap.

set -euo pipefail

PROFILE_URL="${PROFILE_URL:-http://localhost:5197}"
BOOKING_URL="${BOOKING_URL:-http://localhost:5131}"
DEMO_TENANT="${FPS_DEMO_TENANT_ID:-demo}"
DEMO_FACILITY_ID="${FPS_DEMO_FACILITY_ID:-00000000-0000-0000-0000-000000000001}"
DEMO_FACILITY_LABEL="${FPS_DEMO_FACILITY_LABEL:-Headquarters}"
DEMO_LOCATION_ID="${FPS_DEMO_LOCATION_ID:-Prague}"
IDENTITY_URL="${IDENTITY_URL:-http://localhost:5192}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"

GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'
ok()  { echo -e "  ${GREEN}OK${NC}  $1"; }
err() { echo -e "  ${RED}ERR${NC} $1"; }

echo "== FPS local demo seed =="

# Check required services
for check_url in "$IDENTITY_URL/openapi/v1.json" "$PROFILE_URL/openapi/v1.json" "$BOOKING_URL/openapi/v1.json"; do
  if ! curl -sf "$check_url" > /dev/null 2>&1; then
    echo "ERROR: Service not reachable at $check_url"
    echo "  Start all services: ./tools/start-local-harness.sh"
    exit 1
  fi
done

# ── helpers ─────────────────────────────────────────────────────────────────

get_token() {
  local username="$1"
  curl -sf \
    -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=$CLIENT_ID&username=$username&password=$DEV_PASSWORD" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])" 2>/dev/null || echo ""
}

jwt_sub() {
  python3 - "$1" << 'PYEOF'
import base64, json, sys
token = sys.argv[1]
payload = token.split('.')[1]
payload += '=' * (-len(payload) % 4)
print(json.loads(base64.urlsafe_b64decode(payload))['sub'])
PYEOF
}

future_date() {
  local days="$1"
  # macOS: date -v+Nd   Linux: date -d "+N days"
  date -v+"${days}"d +%Y-%m-%d 2>/dev/null || date -d "+${days} days" +%Y-%m-%d
}

seed_profile() {
  local username="$1" has_company_car="$2" accessibility="$3" vehicles="$4"

  local token user_id
  token=$(get_token "$username")
  if [ -z "$token" ]; then
    err "Could not get token for $username — run ./tools/dev-setup-auth.sh first"
    return 1
  fi
  user_id=$(jwt_sub "$token")

  local parking_eligible="true"
  [[ "$username" == "tenant-admin" || "$username" == "report-viewer" || "$username" == "auditor" ]] && parking_eligible="false"

  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$PROFILE_URL/profile/admin/snapshot" \
    -H "Content-Type: application/json" \
    -d "{
      \"tenantId\": \"$DEMO_TENANT\",
      \"userId\": \"$user_id\",
      \"parkingEligible\": $parking_eligible,
      \"hasCompanyCar\": $has_company_car,
      \"accessibilityEligible\": $accessibility,
      \"reservedSpaceEligible\": false,
      \"vehicles\": $vehicles
    }" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [ "$http_code" = "204" ]; then
    ok "Profile $username (userId=${user_id:0:8}...)"
  else
    err "Profile $username HTTP $http_code"
    return 1
  fi
}

seed_booking() {
  local username="$1" license_plate="$2" vehicle_type="$3" is_electric="$4" \
        is_company_car="$5" requires_accessible="$6" date_offset="$7"

  local token booking_date arrival departure
  token=$(get_token "$username")
  [ -z "$token" ] && { err "No token for $username"; return 1; }

  booking_date=$(future_date "$date_offset")
  arrival="${booking_date}T08:00:00"
  departure="${booking_date}T18:00:00"

  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$BOOKING_URL/bookings" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{
      \"facilityId\": \"$DEMO_FACILITY_ID\",
      \"locationId\": \"$DEMO_LOCATION_ID\",
      \"licensePlate\": \"$license_plate\",
      \"vehicleType\": \"$vehicle_type\",
      \"isElectric\": $is_electric,
      \"requiresAccessibleSpot\": $requires_accessible,
      \"isCompanyCar\": $is_company_car,
      \"plannedArrivalTime\": \"$arrival\",
      \"plannedDepartureTime\": \"$departure\"
    }" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [[ "$http_code" = "202" ]]; then
    ok "Booking $username $booking_date (202 Accepted → pending)"
  else
    err "Booking $username $booking_date HTTP $http_code (expected 202 — check policy cutoff, eligibility, or service logs)"
    return 1
  fi
}

trigger_demo_draw() {
  local date_offset="$1"

  local token draw_date start end response http_code body allocated rejected waitlisted status
  token=$(get_token "tenant-admin")
  [ -z "$token" ] && { err "No token for tenant-admin"; return 1; }

  draw_date=$(future_date "$date_offset")
  start="${draw_date}T08:00:00"
  end="${draw_date}T18:00:00"

  response=$(curl -s -w "\n%{http_code}" \
    -X POST "$BOOKING_URL/draws/trigger" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{
      \"locationId\": \"$DEMO_LOCATION_ID\",
      \"date\": \"$draw_date\",
      \"timeSlotStart\": \"$start\",
      \"timeSlotEnd\": \"$end\",
      \"reason\": \"Local demo seed Draw\"
    }" 2>/dev/null || true)

  http_code=$(printf '%s' "$response" | tail -n 1)
  body=$(printf '%s' "$response" | sed '$d')

  if [[ "$http_code" = "200" || "$http_code" = "202" ]]; then
    status=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('status',''))")
    allocated=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('allocatedCount',0))")
    rejected=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('rejectedCount',0))")
    waitlisted=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('waitlistedCount',0))")
    ok "Demo Draw $draw_date ($status): $allocated allocated, $rejected rejected, $waitlisted waitlisted"
  else
    err "Demo Draw $draw_date HTTP ${http_code:-000}"
    [ -n "$body" ] && echo "$body"
    return 1
  fi
}

# ── profiles ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Profiles --"

# Jan Novak (employee1): sedan + EV (two vehicles for guided vehicle selection demo)
seed_profile "employee1" "false" "false" \
  '[{"vehicleId":"VEH-JN-SEDAN","licensePlate":"1AA 2345","vehicleType":"Sedan","isElectric":false,"isActive":true,"isDefault":true},
    {"vehicleId":"VEH-JN-EV","licensePlate":"2AB 3456","vehicleType":"Sedan","isElectric":true,"isActive":true,"isDefault":false}]'

# Petra Svobodova (employee2): company car registered as vehicle so booking plate validation passes
seed_profile "employee2" "true" "false" \
  '[{"vehicleId":"VEH-PS-FLEET","licensePlate":"3AC 4567","vehicleType":"Sedan","isElectric":false,"isActive":true}]'

# Tomas Dvorak (employee3): accessibility-eligible
seed_profile "employee3" "false" "true" \
  '[{"vehicleId":"VEH-TD-ACCESS","licensePlate":"4AD 5678","vehicleType":"Sedan","isElectric":false,"isActive":true}]'

# Role users — parking not eligible, no vehicles
# Lucie Prochazkova (hr-admin), Karel Urban (tenant-admin), Eva Kralova (report-viewer), Martin Cerny (auditor)
seed_profile "hr-admin"      "false" "false" '[]'
seed_profile "tenant-admin"  "false" "false" '[]'
seed_profile "report-viewer" "false" "false" '[]'
seed_profile "auditor"       "false" "false" '[]'

# ── bookings ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Bookings (generates notifications, audit records, and reporting data) --"

# Dates start at +2 to stay clear of the draw cutoff that applies to +1/same-day requests.
# The +2 date intentionally has all three employees competing for the demo Draw.
# Booking's local development AvailableSlots config exposes two Prague slots, so this
# Draw produces visible allocated/waitlisted outcomes immediately after seeding.
# Jan Novak: two regular bookings + one EV booking
seed_booking "employee1" "1AA 2345" "Sedan" "false" "false" "false" "2"
seed_booking "employee1" "2AB 3456" "Sedan" "true"  "false" "false" "4"
seed_booking "employee1" "1AA 2345" "Sedan" "false" "false" "false" "6"

# Petra Svobodova: company car bookings
seed_booking "employee2" "3AC 4567" "Sedan" "false" "true" "false" "2"
seed_booking "employee2" "3AC 4567" "Sedan" "false" "true" "false" "5"

# Tomas Dvorak: accessible spot requests
seed_booking "employee3" "4AD 5678" "Sedan" "false" "false" "true" "2"
seed_booking "employee3" "4AD 5678" "Sedan" "false" "false" "true" "4"

# ── demo Draw ────────────────────────────────────────────────────────────────

echo ""
echo "-- Demo Draw (+2 days, $DEMO_LOCATION_ID 08:00-18:00) --"
trigger_demo_draw "2"

# ── summary ──────────────────────────────────────────────────────────────────

echo ""
echo "== Seed complete =="
echo "Profiles: 7 users — Jan Novak, Petra Svobodova, Tomas Dvorak (employees); Lucie Prochazkova, Karel Urban, Eva Kralova, Martin Cerny (roles)"
echo "Facility/location: $DEMO_FACILITY_LABEL / $DEMO_LOCATION_ID"
echo "Vehicles: Jan has sedan + EV (1AA 2345 / 2AB 3456), Petra company fleet (3AC 4567), Tomas accessible (4AD 5678)"
echo "Bookings: 7 requests across 3 employees; +2 demo Draw has already run and should show allocated/waitlisted results"
echo ""
echo "Verify:"
echo "  TOKEN=\$(./tools/dev-auth.sh employee1)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/profile/snapshot"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/bookings"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/notifications/unread-count"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/me"
echo "  TOKEN=\$(./tools/dev-auth.sh tenant-admin)"
echo "  DATE=\$(date -v+2d +%Y-%m-%d 2>/dev/null || date -d '+2 days' +%Y-%m-%d)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" \"http://localhost:10000/draws/\$DATE/status?locationId=$DEMO_LOCATION_ID&timeSlotStart=\${DATE}T08:00:00&timeSlotEnd=\${DATE}T18:00:00\""
echo ""
echo "Admin/reporting:"
echo "  TOKEN=\$(./tools/dev-auth.sh tenant-admin)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/tenants/$DEMO_TENANT/readiness"
echo "  TOKEN=\$(./tools/dev-auth.sh report-viewer)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/reports/parking/summary"
echo "  TOKEN=\$(./tools/dev-auth.sh auditor)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/audit"
