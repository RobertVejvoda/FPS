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
#   Bookings:  3 pending requests per employee for upcoming dates
#   Admin profiles: hr-admin, tenant-admin, report-viewer, auditor (no parking)
#
#   Configuration (policy + slots) — seeded automatically by Configuration service on startup
#   Notifications, audit records, reporting — populated via Dapr events from booking submissions
#
# Seed data is demo-only. For pilot use, seed via tools/validate-hr-import.sh + POST /profile/bootstrap.

set -euo pipefail

PROFILE_URL="${PROFILE_URL:-http://localhost:5197}"
BOOKING_URL="${BOOKING_URL:-http://localhost:5131}"
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
  http_code=$(curl -sf -o /dev/null -w "%{http_code}" \
    -X PUT "$PROFILE_URL/profile/admin/snapshot" \
    -H "Content-Type: application/json" \
    -d "{
      \"tenantId\": \"tenant-1\",
      \"userId\": \"$user_id\",
      \"parkingEligible\": $parking_eligible,
      \"hasCompanyCar\": $has_company_car,
      \"accessibilityEligible\": $accessibility,
      \"reservedSpaceEligible\": false,
      \"vehicles\": $vehicles
    }")

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
  http_code=$(curl -sf -o /dev/null -w "%{http_code}" \
    -X POST "$BOOKING_URL/bookings" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{
      \"facilityId\": \"00000000-0000-0000-0000-000000000001\",
      \"locationId\": \"LOC-MAIN\",
      \"licensePlate\": \"$license_plate\",
      \"vehicleType\": \"$vehicle_type\",
      \"isElectric\": $is_electric,
      \"requiresAccessibleSpot\": $requires_accessible,
      \"isCompanyCar\": $is_company_car,
      \"plannedArrivalTime\": \"$arrival\",
      \"plannedDepartureTime\": \"$departure\"
    }" 2>/dev/null || echo "000")

  if [[ "$http_code" = "202" ]]; then
    ok "Booking $username $booking_date (202 Accepted → pending)"
  else
    err "Booking $username $booking_date HTTP $http_code (expected 202 — check policy cutoff, eligibility, or service logs)"
    return 1
  fi
}

# ── profiles ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Profiles --"

# employee1: normal employee, sedan + EV (two vehicles for guided selection)
seed_profile "employee1" "false" "false" \
  '[{"vehicleId":"VEH-EMP1-A","licensePlate":"EMP1001","vehicleType":"Sedan","isElectric":false,"isActive":true},
    {"vehicleId":"VEH-EMP1-B","licensePlate":"EMP1002","vehicleType":"Sedan","isElectric":true,"isActive":true}]'

# employee2: company car, no personal vehicle
seed_profile "employee2" "true" "false" '[]'

# employee3: accessibility-eligible, accessible vehicle
seed_profile "employee3" "false" "true" \
  '[{"vehicleId":"VEH-EMP3","licensePlate":"EMP3001","vehicleType":"Sedan","isElectric":false,"isActive":true}]'

# admin/role users — parking not eligible, no vehicles
seed_profile "hr-admin"      "false" "false" '[]'
seed_profile "tenant-admin"  "false" "false" '[]'
seed_profile "report-viewer" "false" "false" '[]'
seed_profile "auditor"       "false" "false" '[]'

# ── bookings ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Bookings (generates notifications, audit records, and reporting data) --"

# Dates start at +2 to stay clear of the draw cutoff that applies to +1/same-day requests.
# employee1: two regular bookings + one EV booking
seed_booking "employee1" "EMP1001" "Sedan" "false" "false" "false" "2"
seed_booking "employee1" "EMP1002" "Sedan" "true"  "false" "false" "4"
seed_booking "employee1" "EMP1001" "Sedan" "false" "false" "false" "6"

# employee2: company car bookings
seed_booking "employee2" "COMPANY001" "Sedan" "false" "true" "false" "3"
seed_booking "employee2" "COMPANY001" "Sedan" "false" "true" "false" "5"

# employee3: accessible spot requests
seed_booking "employee3" "EMP3001" "Sedan" "false" "false" "true" "2"
seed_booking "employee3" "EMP3001" "Sedan" "false" "false" "true" "4"

# ── summary ──────────────────────────────────────────────────────────────────

echo ""
echo "== Seed complete =="
echo "Profiles: 7 users (employee1-3, hr-admin, tenant-admin, report-viewer, auditor)"
echo "Vehicles: employee1 has 2 options (sedan + EV), employee2 company car, employee3 accessible"
echo "Bookings: 7 pending requests across 3 employees (triggers Dapr events if sidecars running)"
echo ""
echo "Verify:"
echo "  TOKEN=\$(./tools/dev-auth.sh employee1)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/profile/snapshot"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/bookings"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/notifications/unread-count"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/me"
echo ""
echo "Admin/reporting:"
echo "  TOKEN=\$(./tools/dev-auth.sh tenant-admin)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/tenants/tenant-1/readiness"
echo "  TOKEN=\$(./tools/dev-auth.sh report-viewer)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/reports/parking/summary"
echo "  TOKEN=\$(./tools/dev-auth.sh auditor)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/audit"
