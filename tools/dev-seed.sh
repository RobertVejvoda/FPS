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
#   Profiles:  25 demo employees by default, plus role users
#   Vehicles:  one regular vehicle for each demo employee
#   Bookings:  25 future Draw requests by default, one per profiled employee
#   Draw:      runs the next future workday Draw so the demo immediately shows allocated/waitlisted outcomes
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
DEMO_EMPLOYEE_COUNT="${FPS_DEMO_EMPLOYEE_COUNT:-25}"
DEMO_BOOKING_COUNT="${FPS_DEMO_BOOKING_COUNT:-$DEMO_EMPLOYEE_COUNT}"
DEMO_DRAW_MIN_OFFSET="${FPS_DEMO_DRAW_MIN_OFFSET:-2}"
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

future_date() {
  local days="$1"
  # macOS: date -v+Nd   Linux: date -d "+N days"
  date -v+"${days}"d +%Y-%m-%d 2>/dev/null || date -d "+${days} days" +%Y-%m-%d
}

weekday_for_offset() {
  python3 - "$1" << 'PYEOF'
from datetime import date, timedelta
import sys

print((date.today() + timedelta(days=int(sys.argv[1]))).isoweekday())
PYEOF
}

next_workday_offset() {
  local min_offset="$1"
  local offset="$min_offset"
  local max_offset=$((min_offset + 14))
  local weekday

  while [ "$offset" -le "$max_offset" ]; do
    weekday=$(weekday_for_offset "$offset")
    if [ "$weekday" -ge 1 ] && [ "$weekday" -le 5 ]; then
      echo "$offset"
      return 0
    fi
    offset=$((offset + 1))
  done

  return 1
}

seed_profile() {
  local username="$1" display_name="$2" has_company_car="$3" accessibility="$4" vehicles="$5"

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
      \"displayName\": \"$display_name\",
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

display_name_for_index() {
  case "$1" in
    1) echo "Jan Novak" ;;
    2) echo "Petra Svobodova" ;;
    3) echo "Tomas Dvorak" ;;
    4) echo "Pavel Cerny" ;;
    5) echo "Hana Vesela" ;;
    6) echo "Martin Horak" ;;
    7) echo "Jana Kucerova" ;;
    8) echo "Petr Svoboda" ;;
    9) echo "Lenka Maresova" ;;
    10) echo "Michal Prochazka" ;;
    11) echo "Veronika Dvorakova" ;;
    12) echo "Tomas Kral" ;;
    13) echo "Barbora Urbanova" ;;
    14) echo "Filip Sedlak" ;;
    15) echo "Lucie Novakova" ;;
    16) echo "Jakub Sima" ;;
    17) echo "Alena Pokorna" ;;
    18) echo "Radek Fiala" ;;
    19) echo "Marketa Blazkova" ;;
    20) echo "David Vacek" ;;
    21) echo "Katerina Hruba" ;;
    22) echo "Ondrej Marek" ;;
    23) echo "Zuzana Krejci" ;;
    24) echo "Milan Tichy" ;;
    25) echo "Ivana Ruzickova" ;;
    *) printf 'Demo Employee %02d\n' "$1" ;;
  esac
}

license_plate_for_index() {
  case "$1" in
    1) echo "1AA 2345" ;;
    *) printf '1AA %04d\n' "$((1000 + $1))" ;;
  esac
}

vehicle_json_for_index() {
  local index="$1" plate
  plate=$(license_plate_for_index "$index")
  printf '[{"vehicleId":"VEH-%03d","licensePlate":"%s","vehicleType":"Sedan","isElectric":false,"isActive":true,"isDefault":true}]' "$index" "$plate"
}

seed_demo_employee_profile() {
  local index="$1" username display_name vehicles
  username="employee$index"
  display_name=$(display_name_for_index "$index")
  vehicles=$(vehicle_json_for_index "$index")
  seed_profile "$username" "$display_name" "false" "false" "$vehicles"
}

seed_booking() {
  local username="$1" license_plate="$2" vehicle_type="$3" is_electric="$4" \
        is_company_car="$5" requires_accessible="$6" date_offset="$7"

  local token booking_date arrival departure
  token=$(get_token "$username")
  [ -z "$token" ] && { err "No token for $username"; return 1; }
  if [ -z "$(jwt_claim "$token" tenant_id)" ]; then
    err "Token for $username has no tenant_id claim — rerun ./tools/dev-setup-auth.sh"
    return 1
  fi

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

verify_hr_display_names() {
  local hr_token
  hr_token=$(get_token "hr-admin")
  [ -z "$hr_token" ] && { err "No token for hr-admin"; return 1; }
  if [ -z "$(jwt_claim "$hr_token" tenant_id)" ]; then
    err "Token for hr-admin has no tenant_id claim — rerun ./tools/dev-setup-auth.sh"
    return 1
  fi

  local sample_indices=("1")
  if [ "$DEMO_EMPLOYEE_COUNT" -ge 4 ]; then
    sample_indices+=("4")
  fi
  if [ "$DEMO_EMPLOYEE_COUNT" -ge 25 ]; then
    sample_indices+=("25")
  elif [ "$DEMO_EMPLOYEE_COUNT" -gt 4 ]; then
    sample_indices+=("$DEMO_EMPLOYEE_COUNT")
  fi

  local user_ids=()
  local expected_pairs=()
  local index username token user_id display_name
  for index in "${sample_indices[@]}"; do
    username="employee$index"
    token=$(get_token "$username")
    [ -z "$token" ] && { err "No token for $username while verifying display names"; return 1; }
    user_id=$(jwt_sub "$token")
    display_name=$(display_name_for_index "$index")
    user_ids+=("$user_id")
    expected_pairs+=("$user_id=$display_name")
  done

  local user_ids_json payload response http_code body
  user_ids_json=$(python3 -c 'import json,sys; print(json.dumps(sys.argv[1:]))' "${user_ids[@]}")
  payload="{\"userIds\": $user_ids_json}"
  response=$(curl -s -w "\n%{http_code}" \
    -X POST "$PROFILE_URL/profile/hr/display-names" \
    -H "Authorization: Bearer $hr_token" \
    -H "Content-Type: application/json" \
    -d "$payload" 2>/dev/null || true)

  http_code=$(printf '%s' "$response" | tail -n 1)
  body=$(printf '%s' "$response" | sed '$d')
  if [ "$http_code" != "200" ]; then
    err "HR display-name lookup HTTP ${http_code:-000}"
    [ -n "$body" ] && echo "$body"
    return 1
  fi

  python3 - "$body" "${expected_pairs[@]}" << 'PYEOF'
import json
import sys

body = json.loads(sys.argv[1])
names = body.get("names") or body.get("Names") or {}
missing = []
for pair in sys.argv[2:]:
    user_id, expected = pair.split("=", 1)
    actual = names.get(user_id)
    if actual != expected:
        missing.append(f"{user_id[:8]}... expected {expected!r}, got {actual!r}")

if missing:
    print("Missing or incorrect HR display names:")
    for item in missing:
        print(f"  - {item}")
    sys.exit(1)
PYEOF

  ok "HR display-name lookup resolves seeded employee names"
}

verify_hr_booking_display_names() {
  local draw_date="$1"
  local hr_token
  hr_token=$(get_token "hr-admin")
  [ -z "$hr_token" ] && { err "No token for hr-admin"; return 1; }

  local response http_code body requestor_refs_json
  response=$(curl -s -w "\n%{http_code}" \
    -H "Authorization: Bearer $hr_token" \
    "$BOOKING_URL/bookings/operations?locationId=$DEMO_LOCATION_ID&from=$draw_date&to=$draw_date&pageSize=200" 2>/dev/null || true)

  http_code=$(printf '%s' "$response" | tail -n 1)
  body=$(printf '%s' "$response" | sed '$d')
  if [ "$http_code" != "200" ]; then
    err "HR booking lookup HTTP ${http_code:-000}"
    [ -n "$body" ] && echo "$body"
    return 1
  fi

  requestor_refs_json=$(python3 - "$body" << 'PYEOF'
import json
import sys

data = json.loads(sys.argv[1])
items = data.get("items") or data.get("Items") or []
refs = []
for item in items:
    ref = item.get("requestorRef") or item.get("RequestorRef")
    if ref and ref not in refs:
        refs.append(ref)
print(json.dumps(refs))
PYEOF
)

  local requestor_count
  requestor_count=$(python3 - "$requestor_refs_json" << 'PYEOF'
import json
import sys

print(len(json.loads(sys.argv[1])))
PYEOF
)

  if [ "$requestor_count" -eq 0 ]; then
    err "No HR booking rows found for $draw_date — demo seed did not create visible HR requests"
    return 1
  fi

  local name_response name_http_code name_body payload
  payload="{\"userIds\": $requestor_refs_json}"
  name_response=$(curl -s -w "\n%{http_code}" \
    -X POST "$PROFILE_URL/profile/hr/display-names" \
    -H "Authorization: Bearer $hr_token" \
    -H "Content-Type: application/json" \
    -d "$payload" 2>/dev/null || true)

  name_http_code=$(printf '%s' "$name_response" | tail -n 1)
  name_body=$(printf '%s' "$name_response" | sed '$d')
  if [ "$name_http_code" != "200" ]; then
    err "HR booking display-name lookup HTTP ${name_http_code:-000}"
    [ -n "$name_body" ] && echo "$name_body"
    return 1
  fi

  python3 - "$requestor_refs_json" "$name_body" << 'PYEOF'
import json
import sys

refs = json.loads(sys.argv[1])
body = json.loads(sys.argv[2])
names = body.get("names") or body.get("Names") or {}
missing = [ref for ref in refs if not names.get(ref)]
if missing:
    print("HR booking rows contain requestors without seeded display names:")
    for ref in missing[:10]:
        print(f"  - {ref[:8]}...")
    if len(missing) > 10:
        print(f"  - ...and {len(missing) - 10} more")
    print("This usually means stale Booking state references old Keycloak user IDs.")
    print("Run: ./tools/stop-local-harness.sh --reset")
    print("Then: ./tools/start-smoke-web.sh")
    sys.exit(1)
PYEOF

  ok "HR Parking Requests rows resolve requestor names ($requestor_count requestors)"
}

# ── profiles ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Profiles --"

for index in $(seq 1 "$DEMO_EMPLOYEE_COUNT"); do
  seed_demo_employee_profile "$index"
done

# Role users — parking not eligible, no vehicles
# Lucie Prochazkova (hr-admin), Karel Urban (tenant-admin), Eva Kralova (report-viewer), Martin Cerny (auditor)
seed_profile "hr-admin"      "Lucie Prochazkova" "false" "false" '[]'
seed_profile "tenant-admin"  "Karel Urban" "false" "false" '[]'
seed_profile "report-viewer" "Eva Kralova" "false" "false" '[]'
seed_profile "auditor"       "Martin Cerny" "false" "false" '[]'

# ── bookings ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Bookings (generates notifications, audit records, and reporting data) --"

# Dates start at the next workday at least +2 days out to stay clear of the
# draw cutoff and keep the demo visible in the HR workday navigation.
# The fairness demo intentionally uses regular employee requests only. Company-car
# fixed-slot handling is a separate policy path and should not be mixed into this draw.
DEMO_DRAW_OFFSET=$(next_workday_offset "$DEMO_DRAW_MIN_OFFSET")
DEMO_DRAW_DATE=$(future_date "$DEMO_DRAW_OFFSET")
echo "Demo Draw date: $DEMO_DRAW_DATE (+$DEMO_DRAW_OFFSET days, next workday)"

for index in $(seq 1 "$DEMO_BOOKING_COUNT"); do
  if [ "$index" -gt "$DEMO_EMPLOYEE_COUNT" ]; then
    break
  fi
  seed_booking "employee$index" "$(license_plate_for_index "$index")" "Sedan" "false" "false" "false" "$DEMO_DRAW_OFFSET"
done

# ── demo Draw ────────────────────────────────────────────────────────────────

echo ""
echo "-- Demo Draw ($DEMO_DRAW_DATE, $DEMO_LOCATION_ID 08:00-18:00) --"
trigger_demo_draw "$DEMO_DRAW_OFFSET"

echo ""
echo "-- HR display names --"
verify_hr_display_names
verify_hr_booking_display_names "$DEMO_DRAW_DATE"

# ── summary ──────────────────────────────────────────────────────────────────

echo ""
echo "== Seed complete =="
echo "Profiles: $DEMO_EMPLOYEE_COUNT employees with display names, plus Lucie Prochazkova, Karel Urban, Eva Kralova, Martin Cerny (roles)"
echo "Facility/location: $DEMO_FACILITY_LABEL / $DEMO_LOCATION_ID"
echo "Vehicles: one regular vehicle per demo employee"
echo "Bookings: $DEMO_BOOKING_COUNT regular employee requests; $DEMO_DRAW_DATE demo Draw has already run and should show 15 numbered slots and visible waitlist pressure"
echo ""
echo "Verify:"
echo "  TOKEN=\$(./tools/dev-auth.sh employee1)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/profile/snapshot"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/bookings"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/notifications/unread-count"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/me"
echo "  TOKEN=\$(./tools/dev-auth.sh tenant-admin)"
echo "  DATE=$DEMO_DRAW_DATE"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" \"http://localhost:10000/draws/\$DATE/status?locationId=$DEMO_LOCATION_ID&timeSlotStart=\${DATE}T08:00:00&timeSlotEnd=\${DATE}T18:00:00\""
echo ""
echo "Admin/reporting:"
echo "  TOKEN=\$(./tools/dev-auth.sh tenant-admin)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/tenants/$DEMO_TENANT/readiness"
echo "  TOKEN=\$(./tools/dev-auth.sh report-viewer)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/reports/parking/summary"
echo "  TOKEN=\$(./tools/dev-auth.sh auditor)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/audit"
