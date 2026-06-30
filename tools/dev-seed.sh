#!/usr/bin/env bash
# dev-seed.sh — Seed the local Green Logistics demo for FPS smoke testing.
#
# This is the single guided-pilot demo: the Green Logistics tenant (tenant_id
# greenlogistics, location GL-HQ) with login-capable gl-* users. A fresh seed
# gives a fresh evaluator a believable, multi-combi Green Logistics workspace.
#
# Requires:
#   - Services running: Identity (:5192), Profile (:5197), Booking (:5131), Configuration (:5141)
#   - ./tools/dev-setup-auth.sh completed with 25 GL users
#     (FPS_GL_EMPLOYEE_COUNT defaults to 25)
#
# Usage:
#   ./tools/dev-seed.sh   — seed the Green Logistics demo
#
# Idempotency:
#   Local runtime state is cleared by default before seeding.
#   Profile seeding is idempotent (overwrites existing snapshot).
#   Set FPS_DEV_SEED_RESET_STATE=false to append to existing local state.
#
# What is seeded (all synthetic, demo-only):
#   Parking:   20 human-labelled GL-HQ slots — A-01..A-13 general, EV-01..EV-03
#              chargers, ACC-01 accessible, VIP-01..VIP-02 company-car, MOTO-01
#              motorcycle (holds 4).
#   Profiles:  25 Green Logistics employees by default, plus role users. The
#              roster mixes — and combines — the special cases: 2 company-car
#              holders (one electric), 2 accessibility users (one electric), 5 EV
#              drivers, 1 motorcycle, and 2 multi-vehicle employees; the rest are
#              regular cars. Realistic CZ plates throughout.
#   Bookings:  25 future Draw requests by default, one per employee, each carrying
#              that employee's real attributes.
#   Draw:      triggers the next future workday Draw over the seeded requests.
#              NOTE: visible allocations / company-car Tier-1 precedence are not yet
#              realised in the Draw (it reads the Booking service's static slot
#              config, not the seeded Configuration slots) — see #665.
#   Role profiles: gl-hr-admin, gl-tenant-admin, gl-report-viewer, gl-auditor (no parking).
#
#   Notifications, audit records, reporting — populated via Dapr events from booking submissions.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE_URL="${PROFILE_URL:-http://localhost:5197}"
BOOKING_URL="${BOOKING_URL:-http://localhost:5131}"
CONFIG_URL="${CONFIG_URL:-http://localhost:5141}"
GL_TENANT="${FPS_GL_TENANT_ID:-greenlogistics}"
GL_FACILITY_ID="${FPS_GL_FACILITY_ID:-00000000-0000-0000-0000-000000000002}"
GL_FACILITY_LABEL="${FPS_GL_FACILITY_LABEL:-Green Logistics HQ}"
GL_LOCATION_ID="${FPS_GL_LOCATION_ID:-GL-HQ}"
GL_EMPLOYEE_COUNT="${FPS_GL_EMPLOYEE_COUNT:-25}"
GL_BOOKING_COUNT="${FPS_GL_BOOKING_COUNT:-$GL_EMPLOYEE_COUNT}"
GL_DRAW_MIN_OFFSET="${FPS_GL_DRAW_MIN_OFFSET:-2}"
IDENTITY_URL="${IDENTITY_URL:-http://localhost:5192}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"
RESET_DEMO_STATE="${FPS_DEV_SEED_RESET_STATE:-true}"

# Green Logistics login users (the gl-* realm users created by dev-setup-auth.sh).
EMPLOYEE_PREFIX="gl-employee"
TENANT_ADMIN_USER="gl-tenant-admin"
HR_ADMIN_USER="gl-hr-admin"
REPORT_VIEWER_USER="gl-report-viewer"
AUDITOR_USER="gl-auditor"

GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'
ok()  { echo -e "  ${GREEN}OK${NC}  $1"; }
err() { echo -e "  ${RED}ERR${NC} $1"; }

echo "== FPS Green Logistics demo seed =="

# Check required services. Configuration does not expose /openapi (no OpenAPI
# mapping), so probe its /health instead.
for check_url in "$IDENTITY_URL/openapi/v1.json" "$PROFILE_URL/openapi/v1.json" "$BOOKING_URL/openapi/v1.json" "$CONFIG_URL/health"; do
  if ! curl -sf "$check_url" > /dev/null 2>&1; then
    echo "ERROR: Service not reachable at $check_url"
    echo "  Start all services: ./tools/start-local-harness.sh"
    exit 1
  fi
done

# ── helpers ─────────────────────────────────────────────────────────────────

reset_local_demo_state() {
  if [ "$RESET_DEMO_STATE" != "true" ]; then
    echo ""
    echo "-- Reset local demo state --"
    ok "Skipped (FPS_DEV_SEED_RESET_STATE=$RESET_DEMO_STATE)"
    return 0
  fi

  if ! command -v docker > /dev/null 2>&1; then
    echo ""
    echo "-- Reset local demo state --"
    err "docker not found; cannot clear local persisted demo state"
    echo "Set FPS_DEV_SEED_RESET_STATE=false only when intentionally appending to an existing environment."
    return 1
  fi

  echo ""
  echo "-- Reset local demo state --"

  if ! docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T mongodb \
    mongosh -u admin -p admin --authenticationDatabase admin --quiet \
    --eval '["fps-booking","fps-workflow","fps-notification","fps-reporting","fps-audit"].forEach(dbName => db.getSiblingDB(dbName).dropDatabase());' \
    > /dev/null; then
    err "Could not clear Mongo-backed local demo state"
    echo "Run: ./tools/stop-local-harness.sh --reset"
    echo "Then: ./tools/start-local-harness.sh"
    return 1
  fi

  if ! docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T postgres \
    psql -U fps -d fps_datahub -v ON_ERROR_STOP=1 \
    -c 'TRUNCATE TABLE datahub_booking_outcome, datahub_draw_history, datahub_event_inbox, datahub_projection_checkpoint RESTART IDENTITY;' \
    > /dev/null; then
    err "Could not clear DataHub local demo projections"
    echo "Run: ./tools/stop-local-harness.sh --reset"
    echo "Then: ./tools/start-local-harness.sh"
    return 1
  fi

  ok "Cleared Booking, workflow, notification, reporting, audit, and DataHub demo state"
}

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

# Seed the Green Logistics tenant default parking policy. The Configuration startup
# seed only covers the bare `demo` scaffold, so the GL tenant needs its policy set
# here for /configuration/parking-policy and the HR config views to resolve.
configure_gl_policy() {
  local admin_token http_code policy_body
  admin_token=$(get_token "$TENANT_ADMIN_USER")
  [ -z "$admin_token" ] && { err "No token for $TENANT_ADMIN_USER — run ./tools/dev-setup-auth.sh first"; return 1; }

  policy_body='{
    "timeZone": "Europe/Prague",
    "dailyRequestCap": 100,
    "drawCutOffTime": "18:00:00",
    "allocationLookbackDays": 10,
    "lateCancellationPenalty": 1,
    "noShowPenalty": 2,
    "sameDayBookingEnabled": true,
    "sameDayUsesRequestCap": true,
    "companyCarTier1Enabled": true,
    "companyCarOverflowBehavior": "reject",
    "automaticReallocationEnabled": true,
    "manualAdjustmentEnabled": true,
    "usageConfirmationRequired": false,
    "usageConfirmationWindowMinutes": 0,
    "usageConfirmationMethods": [],
    "noShowDetectionEnabled": false,
    "publicationReason": "Green Logistics demo seed"
  }'

  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$CONFIG_URL/configuration/parking-policy" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "$policy_body" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [ "$http_code" = "204" ]; then
    ok "Configured Green Logistics parking policy (cutoff 18:00, cap 100, company-car Tier-1 on)"
  else
    err "Green Logistics parking policy PUT HTTP $http_code"
    return 1
  fi
}

# Configure the 20 human-labelled GL-HQ parking slots. The SlotId is what the
# parking map and HR views render (there is no separate label field), so the IDs
# read as human labels. The feature mix exercises every allocation path.
configure_gl_slots() {
  local admin_token slots_json http_code
  admin_token=$(get_token "$TENANT_ADMIN_USER")
  [ -z "$admin_token" ] && { err "No token for $TENANT_ADMIN_USER — run ./tools/dev-setup-auth.sh first"; return 1; }

  slots_json=$(python3 << 'PYEOF'
import json

def slot(slot_id, charger=False, accessible=False, company_car=False, motorcycle=False, units=None):
    return {
        "slotId": slot_id, "isActive": True, "hasCharger": charger,
        "isAccessible": accessible, "isCompanyCarOnly": company_car,
        "isMotorcycleCapacity": motorcycle, "reservedForUserId": None,
        "motorcycleCapacityUnits": units,
    }

slots = [slot(f"A-{i:02d}") for i in range(1, 14)]      # 13 general
slots += [slot(f"EV-{i:02d}", charger=True) for i in range(1, 4)]
slots.append(slot("ACC-01", accessible=True))
# VIP-01 → company-car employee #1 (non-EV); VIP-02 → company-car employee #8 (EV combo),
# so VIP-02 carries a charger or the EV company car can't use its reserved Tier-1 slot.
slots += [slot("VIP-01", company_car=True), slot("VIP-02", company_car=True, charger=True)]
slots.append(slot("MOTO-01", motorcycle=True, units=4))
print(json.dumps({"slots": slots, "changeReason": "Green Logistics demo seed: parking layout"}))
PYEOF
)

  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "$slots_json" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [ "$http_code" = "204" ]; then
    ok "Configured 20 $GL_LOCATION_ID slots (13 general, 3 EV, 1 accessible, 2 company-car, 1 motorcycle)"
  else
    err "GL-HQ slot configuration PUT HTTP $http_code"
    return 1
  fi
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
  [[ "$username" == "$TENANT_ADMIN_USER" || "$username" == "$REPORT_VIEWER_USER" || "$username" == "$AUDITOR_USER" ]] && parking_eligible="false"

  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$PROFILE_URL/profile/admin/snapshot" \
    -H "Content-Type: application/json" \
    -d "{
      \"tenantId\": \"$GL_TENANT\",
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
    *) printf 'GL Employee %02d\n' "$1" ;;
  esac
}

# Realistic, varied CZ-style plates (digit + two letters + four digits) per employee.
license_plate_for_index() {
  case "$1" in
    1) echo "1AB 2345" ;;   2) echo "2SC 4417" ;;   3) echo "3AH 8820" ;;
    4) echo "4EK 1193" ;;   5) echo "5BL 6628" ;;   6) echo "1AP 3092" ;;
    7) echo "6CT 7741" ;;   8) echo "2SD 5510" ;;   9) echo "7AZ 2284" ;;
    10) echo "3BM 9087" ;;  11) echo "4EH 4451" ;;  12) echo "8AK 6673" ;;
    13) echo "1AN 1208" ;;  14) echo "5BX 3390" ;;  15) echo "2SE 7715" ;;
    16) echo "9AT 4462" ;;  17) echo "3BR 8829" ;;  18) echo "4EP 1147" ;;
    19) echo "6CV 5583" ;;  20) echo "7AM 9921" ;;  21) echo "1AS 2034" ;;
    22) echo "8AL 6690" ;;  23) echo "2SF 4418" ;;  24) echo "3BH 7752" ;;
    25) echo "5BY 1106" ;;  *) printf '9ZZ %04d\n' "$((1000 + $1))" ;;
  esac
}

# ── Green Logistics population mix (multi-combi) ──────────────────────────────
# The demo deliberately spreads — and combines — the special cases across the
# roster so one Draw exercises every allocation path and every realistic combo:
#   #1  company-car                        → Tier-1 fixed slot (VIP-*)
#   #2  EV                                 → prefers a charger slot (EV-*)
#   #3  two vehicles (car + motorcycle)    → books the default car
#   #5  accessibility + EV (combo)         → prefers ACC-01, electric
#   #8  company-car + EV (combo)           → Tier-1 fixed slot, electric company car
#   #10,#15 EV                             → charger preference
#   #13 two vehicles (two cars)            → books the default car
#   #17 accessibility                      → prefers ACC-01
#   #20 motorcycle                         → shared MOTO-01 area
# Everyone else competes in the fair Tier-2 lottery on the general slots.
# NOTE: the arrows above describe the intended allocation. The Draw currently reads
# the Booking service's static slot config, not the seeded Configuration slots, so
# these outcomes are not yet realised in the live Draw — see #665.
has_company_car_for_index() { case "$1" in 1|8)      echo "true" ;; *) echo "false" ;; esac; }
accessibility_for_index()   { case "$1" in 5|17)     echo "true" ;; *) echo "false" ;; esac; }
is_electric_for_index()     { case "$1" in 2|5|8|10|15) echo "true" ;; *) echo "false" ;; esac; }
vehicle_type_for_index()    { case "$1" in 20)       echo "Motorcycle" ;; *) echo "Sedan" ;; esac; }

# One vehicle per employee by default; a couple of employees carry a second
# vehicle (the default — used for the booking — is listed first). Secondary
# plates stay distinct from every primary plate above.
vehicle_json_for_index() {
  local index="$1" plate vehicle_type is_electric
  plate=$(license_plate_for_index "$index")
  vehicle_type=$(vehicle_type_for_index "$index")
  is_electric=$(is_electric_for_index "$index")
  case "$index" in
    3)  # car (default) + motorcycle
      printf '[{"vehicleId":"VEH-003A","licensePlate":"%s","vehicleType":"Sedan","isElectric":false,"isActive":true,"isDefault":true},{"vehicleId":"VEH-003B","licensePlate":"3AH 0143","vehicleType":"Motorcycle","isElectric":false,"isActive":true,"isDefault":false}]' "$plate" ;;
    13) # two cars (default first)
      printf '[{"vehicleId":"VEH-013A","licensePlate":"%s","vehicleType":"Sedan","isElectric":false,"isActive":true,"isDefault":true},{"vehicleId":"VEH-013B","licensePlate":"1AN 7781","vehicleType":"Sedan","isElectric":false,"isActive":true,"isDefault":false}]' "$plate" ;;
    *)
      printf '[{"vehicleId":"VEH-%03d","licensePlate":"%s","vehicleType":"%s","isElectric":%s,"isActive":true,"isDefault":true}]' \
        "$index" "$plate" "$vehicle_type" "$is_electric" ;;
  esac
}

seed_gl_employee_profile() {
  local index="$1" username display_name vehicles company_car accessibility
  username="${EMPLOYEE_PREFIX}$index"
  display_name=$(display_name_for_index "$index")
  vehicles=$(vehicle_json_for_index "$index")
  company_car=$(has_company_car_for_index "$index")
  accessibility=$(accessibility_for_index "$index")
  seed_profile "$username" "$display_name" "$company_car" "$accessibility" "$vehicles"
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
      \"facilityId\": \"$GL_FACILITY_ID\",
      \"locationId\": \"$GL_LOCATION_ID\",
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
  elif [[ "$http_code" = "200" ]]; then
    # Company-car Tier-1 fixed-slot holders are allocated immediately at submission
    # against their reserved Configuration slot (no Draw needed) — 200, not 202.
    ok "Booking $username $booking_date (200 OK → allocated immediately, company-car Tier-1)"
  else
    err "Booking $username $booking_date HTTP $http_code (expected 200/202 — check policy cutoff, eligibility, or service logs)"
    return 1
  fi
}

trigger_demo_draw() {
  local date_offset="$1"

  local token draw_date start end response http_code body allocated rejected waitlisted status
  token=$(get_token "$TENANT_ADMIN_USER")
  [ -z "$token" ] && { err "No token for $TENANT_ADMIN_USER"; return 1; }

  draw_date=$(future_date "$date_offset")
  start="${draw_date}T08:00:00"
  end="${draw_date}T18:00:00"

  response=$(curl -s -w "\n%{http_code}" \
    -X POST "$BOOKING_URL/draws/trigger" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "{
      \"locationId\": \"$GL_LOCATION_ID\",
      \"date\": \"$draw_date\",
      \"timeSlotStart\": \"$start\",
      \"timeSlotEnd\": \"$end\",
      \"reason\": \"Green Logistics demo seed Draw\"
    }" 2>/dev/null || true)

  http_code=$(printf '%s' "$response" | tail -n 1)
  body=$(printf '%s' "$response" | sed '$d')

  if [[ "$http_code" = "200" || "$http_code" = "202" ]]; then
    status=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('status',''))")
    allocated=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('allocatedCount',0))")
    rejected=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('rejectedCount',0))")
    waitlisted=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('waitlistedCount',0))")
    ok "Green Logistics Draw $draw_date triggered ($status): $allocated allocated, $rejected rejected, $waitlisted waitlisted (async — verifying outcome)"
  else
    err "Green Logistics Draw $draw_date HTTP ${http_code:-000}"
    [ -n "$body" ] && echo "$body"
    return 1
  fi
}

# Gate: the Draw runs as an async workflow after the trigger returns, so poll the
# lifecycle until it completes and assert the seeded slots actually drove visible
# allocations — the runtime evidence #665 requires (static review can't catch a
# Draw that loads 0 requests or ignores the curated Configuration slots).
verify_demo_draw() {
  local date_offset="$1"
  local token draw_date start end status body allocated waitlisted vip_alloc

  token=$(get_token "$HR_ADMIN_USER")
  [ -z "$token" ] && { err "No token for $HR_ADMIN_USER"; return 1; }

  draw_date=$(future_date "$date_offset")
  start="${draw_date}T08:00:00"
  end="${draw_date}T18:00:00"

  status=""
  for _ in $(seq 1 30); do
    body=$(curl -sf -H "Authorization: Bearer $token" \
      "$BOOKING_URL/draws/$draw_date/lifecycle?locationId=$GL_LOCATION_ID&timeSlotStart=$start&timeSlotEnd=$end" 2>/dev/null || true)
    status=$(printf '%s' "$body" | python3 -c "import json,sys; print(json.load(sys.stdin).get('status',''))" 2>/dev/null || echo "")
    [ "$status" = "Completed" ] && break
    sleep 2
  done

  if [ "$status" != "Completed" ]; then
    err "Green Logistics Draw did not reach Completed (last status: ${status:-none})"
    return 1
  fi

  allocated=$(printf '%s' "$body" | python3 -c "import json,sys; print(json.load(sys.stdin).get('allocatedCount',0))")
  waitlisted=$(printf '%s' "$body" | python3 -c "import json,sys; print(json.load(sys.stdin).get('waitlistedCount',0))")
  if [ "${allocated:-0}" -lt 1 ]; then
    err "Green Logistics Draw completed but allocated 0 requests (expected visible allocations from the seeded slots)"
    return 1
  fi
  ok "Green Logistics Draw completed: $allocated allocated, $waitlisted waitlisted"

  # Tier-1 evidence: both company-car fixed slots must be held by their reserved
  # owners (employee #1 → VIP-01, EV employee #8 → VIP-02).
  local ops
  ops=$(curl -sf -H "Authorization: Bearer $token" \
    "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$draw_date&to=$draw_date&pageSize=200" 2>/dev/null || true)
  vip_alloc=$(printf '%s' "$ops" | python3 -c "
import json, sys
d = json.load(sys.stdin)
vips = {i.get('allocatedSlotId') for i in d.get('items', []) if i.get('status') == 'Allocated'}
print(sum(1 for v in ('VIP-01', 'VIP-02') if v in vips))
" 2>/dev/null || echo 0)

  if [ "${vip_alloc:-0}" -eq 2 ]; then
    ok "Company-car Tier-1: both VIP fixed slots pre-allocated to their reserved holders"
  else
    err "Company-car Tier-1: expected 2 VIP fixed-slot allocations, found ${vip_alloc:-0}"
    return 1
  fi
}

# Reserve the company-car fixed slots for the seeded company-car employees, in the
# Configuration service. The VIP-* slots are configured without an owner because
# the employees' Keycloak user IDs are not known until they exist; here — after the
# profiles are seeded — we resolve each company-car employee's `sub` and stamp it
# onto the next company-car-only slot (this drives the HR config / parking-map views).
# NOTE: the Booking submission and Draw currently read slot capacity from the Booking
# service's own static appsettings (ConfiguredAvailableSlotService), NOT these
# Configuration-service slots, so this reservation does not yet drive Tier-1
# allocation in the Draw. Wiring it through is tracked in #665.
reserve_company_car_slots() {
  local admin_token slots_json put_body http_code index token
  local subs=()

  for index in $(seq 1 "$GL_EMPLOYEE_COUNT"); do
    if [ "$(has_company_car_for_index "$index")" = "true" ]; then
      token=$(get_token "${EMPLOYEE_PREFIX}$index")
      [ -z "$token" ] && { err "No token for ${EMPLOYEE_PREFIX}$index (company-car)"; return 1; }
      subs+=("$(jwt_sub "$token")")
    fi
  done

  if [ "${#subs[@]}" -eq 0 ]; then
    ok "No company-car employees in roster — nothing to reserve"
    return 0
  fi

  admin_token=$(get_token "$TENANT_ADMIN_USER")
  [ -z "$admin_token" ] && { err "No token for $TENANT_ADMIN_USER"; return 1; }

  slots_json=$(curl -sf -H "Authorization: Bearer $admin_token" \
    "$CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots" 2>/dev/null || true)
  if [ -z "$slots_json" ]; then
    err "Could not read slots from Configuration ($CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots)"
    return 1
  fi

  # Stamp each company-car sub onto the next company-car-only slot (stable slotId order),
  # preserving every other slot field, then PUT the full set back.
  put_body=$(python3 - "$slots_json" "${subs[@]}" << 'PYEOF'
import json, sys

slots = json.loads(sys.argv[1])
subs = sys.argv[2:]

def field(slot, *names):
    for name in names:
        if name in slot:
            return slot[name]
    return None

company_car = sorted(
    (s for s in slots if field(s, "isCompanyCarOnly", "IsCompanyCarOnly")),
    key=lambda s: field(s, "slotId", "SlotId"))
for slot, sub in zip(company_car, subs):
    slot["__reserved"] = sub

out = []
for s in slots:
    out.append({
        "slotId": field(s, "slotId", "SlotId"),
        "isActive": field(s, "isActive", "IsActive"),
        "hasCharger": field(s, "hasCharger", "HasCharger"),
        "isAccessible": field(s, "isAccessible", "IsAccessible"),
        "isCompanyCarOnly": field(s, "isCompanyCarOnly", "IsCompanyCarOnly"),
        "isMotorcycleCapacity": field(s, "isMotorcycleCapacity", "IsMotorcycleCapacity"),
        "reservedForUserId": s.get("__reserved") or field(s, "reservedForUserId", "ReservedForUserId"),
        "motorcycleCapacityUnits": field(s, "motorcycleCapacityUnits", "MotorcycleCapacityUnits"),
    })
print(json.dumps({"slots": out, "changeReason": "Green Logistics demo seed: company-car fixed-slot reservations"}))
PYEOF
)

  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "$put_body" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [ "$http_code" = "204" ]; then
    ok "Reserved ${#subs[@]} company-car fixed slot(s) for Tier-1 allocation"
  else
    err "Company-car slot reservation PUT HTTP $http_code"
    return 1
  fi
}

verify_hr_display_names() {
  local hr_token
  hr_token=$(get_token "$HR_ADMIN_USER")
  [ -z "$hr_token" ] && { err "No token for $HR_ADMIN_USER"; return 1; }
  if [ -z "$(jwt_claim "$hr_token" tenant_id)" ]; then
    err "Token for $HR_ADMIN_USER has no tenant_id claim — rerun ./tools/dev-setup-auth.sh"
    return 1
  fi

  local sample_indices=("1")
  if [ "$GL_EMPLOYEE_COUNT" -ge 4 ]; then
    sample_indices+=("4")
  fi
  if [ "$GL_EMPLOYEE_COUNT" -ge 25 ]; then
    sample_indices+=("25")
  elif [ "$GL_EMPLOYEE_COUNT" -gt 4 ]; then
    sample_indices+=("$GL_EMPLOYEE_COUNT")
  fi

  local user_ids=()
  local expected_pairs=()
  local index username token user_id display_name
  for index in "${sample_indices[@]}"; do
    username="${EMPLOYEE_PREFIX}$index"
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
  hr_token=$(get_token "$HR_ADMIN_USER")
  [ -z "$hr_token" ] && { err "No token for $HR_ADMIN_USER"; return 1; }

  local response http_code body requestor_refs_json
  response=$(curl -s -w "\n%{http_code}" \
    -H "Authorization: Bearer $hr_token" \
    "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$draw_date&to=$draw_date&pageSize=200" 2>/dev/null || true)

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
    print("Then: ./tools/start-local-harness.sh")
    print("Then: ./tools/start-smoke-web.sh")
    sys.exit(1)
PYEOF

  ok "HR Parking Requests rows resolve requestor names ($requestor_count requestors)"
}

# ── parking ──────────────────────────────────────────────────────────────────

reset_local_demo_state

echo ""
echo "-- Parking ($GL_LOCATION_ID) --"
configure_gl_policy
configure_gl_slots

# ── profiles ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Profiles --"

for index in $(seq 1 "$GL_EMPLOYEE_COUNT"); do
  seed_gl_employee_profile "$index"
done

# Role users — parking not eligible, no vehicles
seed_profile "$HR_ADMIN_USER"      "Lucie Prochazkova" "false" "false" '[]'
seed_profile "$TENANT_ADMIN_USER"  "Karel Urban" "false" "false" '[]'
seed_profile "$REPORT_VIEWER_USER" "Eva Kralova" "false" "false" '[]'
seed_profile "$AUDITOR_USER"       "Martin Cerny" "false" "false" '[]'

# ── company-car reservations ─────────────────────────────────────────────────
# Must run after profiles (needs the employees to exist) and before bookings (so
# the company-car requests resolve their reserved Tier-1 slot).

echo ""
echo "-- Company-car fixed-slot reservations --"
reserve_company_car_slots

# ── bookings ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Bookings (generates notifications, audit records, and reporting data) --"

# Dates start at the next workday at least +2 days out to stay clear of the
# draw cutoff and keep the demo visible in the HR workday navigation.
# Each request carries the employee's real attributes (company-car, accessibility,
# EV, motorcycle). The Draw is triggered over them, but note the seeded slots do not
# yet drive allocation (the Draw reads the Booking service's static slot config) —
# so company-car Tier-1 precedence and visible draw outcomes are pending #665.
GL_DRAW_OFFSET=$(next_workday_offset "$GL_DRAW_MIN_OFFSET")
GL_DRAW_DATE=$(future_date "$GL_DRAW_OFFSET")
echo "Green Logistics Draw date: $GL_DRAW_DATE (+$GL_DRAW_OFFSET days, next workday)"

for index in $(seq 1 "$GL_BOOKING_COUNT"); do
  if [ "$index" -gt "$GL_EMPLOYEE_COUNT" ]; then
    break
  fi
  seed_booking "${EMPLOYEE_PREFIX}$index" \
    "$(license_plate_for_index "$index")" \
    "$(vehicle_type_for_index "$index")" \
    "$(is_electric_for_index "$index")" \
    "$(has_company_car_for_index "$index")" \
    "$(accessibility_for_index "$index")" \
    "$GL_DRAW_OFFSET"
done

# ── Draw ─────────────────────────────────────────────────────────────────────

echo ""
echo "-- Green Logistics Draw ($GL_DRAW_DATE, $GL_LOCATION_ID 08:00-18:00) --"
trigger_demo_draw "$GL_DRAW_OFFSET"
verify_demo_draw "$GL_DRAW_OFFSET"

echo ""
echo "-- HR display names --"
verify_hr_display_names
verify_hr_booking_display_names "$GL_DRAW_DATE"

# ── summary ──────────────────────────────────────────────────────────────────

echo ""
echo "== Seed complete =="
echo "Tenant: $GL_TENANT (Green Logistics)"
echo "Profiles: $GL_EMPLOYEE_COUNT employees with display names (2 company-car incl. 1 EV, 2 accessibility incl. 1 EV, 5 EV, 1 motorcycle, 2 multi-vehicle, rest regular), plus Lucie Prochazkova, Karel Urban, Eva Kralova, Martin Cerny (roles)"
echo "Facility/location: $GL_FACILITY_LABEL / $GL_LOCATION_ID"
echo "Vehicles: realistic CZ plates; two employees carry a second vehicle"
echo "Parking: 20 labelled slots (A-01..A-13 general, EV-01..EV-03, ACC-01, VIP-01..VIP-02 company-car, MOTO-01)"
echo "Bookings: $GL_BOOKING_COUNT employee requests; $GL_DRAW_DATE Draw triggered."
echo "NOTE: the Draw reads the Booking service's static slot config, not the seeded Configuration slots, so visible allocations/company-car precedence are pending #665."
echo ""
echo "Verify:"
echo "  TOKEN=\$(./tools/dev-auth.sh gl-employee1)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/profile/snapshot"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/bookings"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/notifications/unread-count"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/me"
echo "  TOKEN=\$(./tools/dev-auth.sh $TENANT_ADMIN_USER)"
echo "  DATE=$GL_DRAW_DATE"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" \"http://localhost:10000/draws/\$DATE/status?locationId=$GL_LOCATION_ID&timeSlotStart=\${DATE}T08:00:00&timeSlotEnd=\${DATE}T18:00:00\""
echo ""
echo "Admin/reporting:"
echo "  TOKEN=\$(./tools/dev-auth.sh $TENANT_ADMIN_USER)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/tenants/$GL_TENANT/readiness"
echo "  TOKEN=\$(./tools/dev-auth.sh $REPORT_VIEWER_USER)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/reports/parking/summary"
echo "  TOKEN=\$(./tools/dev-auth.sh $AUDITOR_USER)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:10000/audit"
