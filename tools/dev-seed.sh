#!/usr/bin/env bash
# dev-seed.sh — Seed the Green Logistics showcase demo (DEMOSEED003 / #704).
#
# The default evaluation showcase: the Green Logistics tenant (tenant_id
# greenlogistics, location GL-HQ) told as one small, legible fairness story you can
# understand in a single screen — 6 named slots, 10 named people, a visible waitlist,
# and one reallocation. Bulk/load-test data lives on a separate explicit path
# (tools/perf-seed-greenlogistics.sh), never in this default showcase.
#
# Requires:
#   - Services running: Identity (:5192), Profile (:5197), Booking (:5131), Configuration (:5141)
#   - ./tools/dev-setup-auth.sh completed (FPS_GL_EMPLOYEE_COUNT defaults to 10)
#
# Usage:
#   ./tools/dev-seed.sh   — seed the Green Logistics showcase
#
# Idempotency:
#   Local runtime state is cleared by default before seeding.
#   Profile seeding is idempotent (overwrites existing snapshot).
#   Set FPS_DEV_SEED_RESET_STATE=false to append to existing local state.
#
# What is seeded (all synthetic, demo-only):
#   Parking:   6 human-labelled GL-HQ slots — A-01, A-02 general, EV-01 charger,
#              ACC-01 accessible, MOTO-01 motorcycle, VIP-01 company-car (reserved).
#   Profiles:  10 Green Logistics employees plus role users. Four special-need
#              personas — company-car (#1), EV (#2), accessible (#3), motorcycle
#              (#4) — and six general drivers (#5..#10) who compete for the two
#              general slots. Realistic CZ plates; two general personas carry a
#              seeded fairness history (see below).
#   History:   Before the showcase Draw, one earlier real Draw gives the "recent
#              winner" (#7) an allocation on record and the "penalised" persona (#8)
#              an active late-cancellation penalty (via a real allocate-then-cancel).
#              This is real event history — no direct projection seeding — so the
#              showcase Draw's fair outcome is explainable in HR/reports/audit.
#   Bookings:  10 requests for the showcase Draw date, one per employee, each
#              carrying that employee's real attributes.
#   Draw:      triggers the next future workday Draw and asserts the outcome
#              (verify_demo_draw): the company-car holder takes VIP-01 (Tier-1) at
#              submission, the motorcycle takes MOTO-01, and the general lottery
#              allocates the scarce slots and leaves a visible waitlist.
#   Reallocation: one allocated general request is cancelled; the fair next
#              waitlisted driver is promoted automatically (verify_reallocation).
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
# Showcase default: 10 named people. Larger counts stay opt-in for local isolation
# only (the extra indices are plain general drivers); bulk/load data has its own path.
GL_EMPLOYEE_COUNT="${FPS_GL_EMPLOYEE_COUNT:-10}"
GL_BOOKING_COUNT="${FPS_GL_BOOKING_COUNT:-$GL_EMPLOYEE_COUNT}"
GL_DRAW_MIN_OFFSET="${FPS_GL_DRAW_MIN_OFFSET:-2}"
# Fairness-history personas (indices into the showcase roster).
GL_RECENT_WINNER_INDEX="${FPS_GL_RECENT_WINNER_INDEX:-7}"
GL_PENALISED_INDEX="${FPS_GL_PENALISED_INDEX:-8}"
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

# Configure the 6 human-labelled GL-HQ parking slots. The SlotId is what the parking
# map and HR views render (there is no separate label field), so the IDs read as human
# labels. Six slots keep the showcase understandable in one screen while still exercising
# every allocation path: company-car Tier-1, EV charger, accessible, motorcycle, and the
# fair general lottery. MOTO-01 holds a single unit so the layout reads as exactly six slots.
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

slots = [
    slot("A-01"),                          # general
    slot("A-02"),                          # general
    slot("EV-01", charger=True),           # EV charger
    slot("ACC-01", accessible=True),       # accessible
    slot("MOTO-01", motorcycle=True, units=1),  # motorcycle (single unit)
    slot("VIP-01", company_car=True),      # company-car reserved (owner stamped after profiles)
]
print(json.dumps({"slots": slots, "changeReason": "Green Logistics showcase seed: parking layout"}))
PYEOF
)

  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "$CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots" \
    -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" \
    -d "$slots_json" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"

  if [ "$http_code" = "204" ]; then
    ok "Configured 6 $GL_LOCATION_ID slots (A-01, A-02 general, EV-01 charger, ACC-01 accessible, MOTO-01 motorcycle, VIP-01 company-car)"
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

# Indices 1..10 mirror the provisioning showcase roster
# (TenantDemoSeedService.GreenLogisticsDataset) exactly — same name, persona, and plate
# per index — so a provisioned sandbox tells the identical named-person story. The two
# paths differ only in how operator roles are modelled: provisioning attaches hr_manager
# to #9 and admin to #10, whereas the local harness provisions dedicated role accounts
# (gl-hr-admin, gl-tenant-admin, gl-report-viewer, gl-auditor) below, so here #9/#10 are
# plain general drivers. Indices 11+ are extra generic drivers for the opt-in larger roster.
display_name_for_index() {
  case "$1" in
    1) echo "Jan Novak" ;;
    2) echo "Petra Svobodova" ;;
    3) echo "Hana Vesela" ;;
    4) echo "Tomas Dvorak" ;;
    5) echo "Pavel Cerny" ;;
    6) echo "Martin Horak" ;;
    7) echo "Jana Kucerova" ;;
    8) echo "Petr Novotny" ;;
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
    1) echo "1AB 2345" ;;   2) echo "2SC 4417" ;;   3) echo "5BL 6628" ;;
    4) echo "3AH 8820" ;;   5) echo "4EK 1193" ;;   6) echo "1AP 3092" ;;
    7) echo "6CT 7741" ;;   8) echo "7AZ 2284" ;;   9) echo "3BM 9087" ;;
    10) echo "4EH 4451" ;;  11) echo "2SD 5510" ;;  12) echo "8AK 6673" ;;
    13) echo "1AN 1208" ;;  14) echo "5BX 3390" ;;  15) echo "2SE 7715" ;;
    16) echo "9AT 4462" ;;  17) echo "3BR 8829" ;;  18) echo "4EP 1147" ;;
    19) echo "6CV 5583" ;;  20) echo "7AM 9921" ;;  21) echo "1AS 2034" ;;
    22) echo "8AL 6690" ;;  23) echo "2SF 4418" ;;  24) echo "3BH 7752" ;;
    25) echo "5BY 1106" ;;  *) printf '9ZZ %04d\n' "$((1000 + $1))" ;;
  esac
}

# ── Green Logistics showcase personas ─────────────────────────────────────────
# Ten named people, each a clear persona so one Draw exercises every allocation path
# and the fair outcome is explainable:
#   #1  company-car        → VIP-01 fixed slot at submission (Tier-1, guaranteed)
#   #2  EV                 → the charger slot (EV-01)
#   #3  accessibility      → the accessible slot (ACC-01)
#   #4  motorcycle         → the motorcycle area (MOTO-01, only motorcycles fit)
#   #5,#6  general         → fair lottery for the two general slots (A-01, A-02)
#   #7  recent winner      → seeded recent allocation history → lower fair weight
#   #8  penalised          → seeded active penalty → lower fair weight
#   #9,#10 general         → fair lottery (no history → full weight, incl. "unlucky")
# The special personas' slots and the general lottery are realised in the live Draw:
# Booking reads the seeded Configuration slots over Dapr (#666). Only #1 (Tier-1) and
# #4 (motorcycle-only) are deterministic; the general slots are a genuine fair lottery,
# which is the point — recent winners and penalised drivers are less likely to win again.
has_company_car_for_index() { case "$1" in 1) echo "true" ;; *) echo "false" ;; esac; }
accessibility_for_index()   { case "$1" in 3) echo "true" ;; *) echo "false" ;; esac; }
is_electric_for_index()     { case "$1" in 2) echo "true" ;; *) echo "false" ;; esac; }
vehicle_type_for_index()    { case "$1" in 4) echo "Motorcycle" ;; *) echo "Sedan" ;; esac; }

# One vehicle per employee — a single, business-readable vehicle keeps the showcase legible.
vehicle_json_for_index() {
  local index="$1" plate vehicle_type is_electric
  plate=$(license_plate_for_index "$index")
  vehicle_type=$(vehicle_type_for_index "$index")
  is_electric=$(is_electric_for_index "$index")
  printf '[{"vehicleId":"VEH-%03d","licensePlate":"%s","vehicleType":"%s","isElectric":%s,"isActive":true,"isDefault":true}]' \
    "$index" "$plate" "$vehicle_type" "$is_electric"
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

  # A visible waitlist is part of the story: with six slots and ten people, the general
  # lottery must leave at least one driver waiting (otherwise the scarcity story is lost).
  if [ "${waitlisted:-0}" -lt 1 ]; then
    err "Green Logistics Draw completed but left an empty waitlist (expected a visible waitlist from scarce general slots)"
    return 1
  fi
  ok "Visible waitlist present: $waitlisted driver(s) waiting on scarce general slots"

  # Tier-1 evidence: the single company-car fixed slot (VIP-01) must be held by its
  # reserved owner (employee #1), allocated at submission outside the lottery.
  local ops
  ops=$(curl -sf -H "Authorization: Bearer $token" \
    "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$draw_date&to=$draw_date&pageSize=200" 2>/dev/null || true)
  vip_alloc=$(printf '%s' "$ops" | python3 -c "
import json, sys
d = json.load(sys.stdin)
vips = {i.get('allocatedSlotId') for i in d.get('items', []) if i.get('status') == 'Allocated'}
print(1 if 'VIP-01' in vips else 0)
" 2>/dev/null || echo 0)

  if [ "${vip_alloc:-0}" -eq 1 ]; then
    ok "Company-car Tier-1: VIP-01 fixed slot pre-allocated to its reserved holder"
  else
    err "Company-car Tier-1: expected VIP-01 to be allocated to its reserved holder, found ${vip_alloc:-0}"
    return 1
  fi
}

# Reserve the company-car fixed slots for the seeded company-car employees, in the
# Configuration service. The VIP-* slots are configured without an owner because
# the employees' Keycloak user IDs are not known until they exist; here — after the
# profiles are seeded — we resolve each company-car employee's `sub` and stamp it
# onto the next company-car-only slot (this drives the HR config / parking-map views).
# Booking submission and the Draw read slot capacity from these Configuration-service
# slots over Dapr (#666), so this reservation drives Tier-1 fixed-slot allocation:
# the holder is allocated their VIP slot immediately at submission.
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

# ── showcase draw / history / reallocation helpers ────────────────────────────

# Submit one employee's request for a given date offset, carrying that persona's real
# attributes. Shared by the history draws and the showcase draw.
book_employee_for_offset() {
  local index="$1" offset="$2"
  seed_booking "${EMPLOYEE_PREFIX}$index" \
    "$(license_plate_for_index "$index")" \
    "$(vehicle_type_for_index "$index")" \
    "$(is_electric_for_index "$index")" \
    "$(has_company_car_for_index "$index")" \
    "$(accessibility_for_index "$index")" \
    "$offset"
}

# Poll a Draw's lifecycle until Completed. Echoes "allocated waitlisted"; returns 1 on timeout.
wait_for_draw_complete() {
  local date_offset="$1"
  local token draw_date start end status body
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
  [ "$status" != "Completed" ] && { err "Draw $draw_date did not reach Completed (last: ${status:-none})"; return 1; }

  local allocated waitlisted
  allocated=$(printf '%s' "$body" | python3 -c "import json,sys; print(json.load(sys.stdin).get('allocatedCount',0))")
  waitlisted=$(printf '%s' "$body" | python3 -c "import json,sys; print(json.load(sys.stdin).get('waitlistedCount',0))")
  echo "$allocated $waitlisted"
}

# Run one earlier real Draw for a subset of employees, so their wins land as genuine
# allocation history for the later showcase Draw. This is real event history, not a
# projection write.
history_draw() {
  local date_offset="$1"; shift
  local draw_date index
  draw_date=$(future_date "$date_offset")
  echo "  History Draw $draw_date (+$date_offset days): employees $*"
  for index in "$@"; do
    book_employee_for_offset "$index" "$date_offset" || return 1
  done
  trigger_demo_draw "$date_offset" || return 1
  wait_for_draw_complete "$date_offset" > /dev/null || return 1
}

# Cancel an employee's own Allocated booking on a date. Cancelling an allocated request
# applies a real late-cancellation penalty (policy.LateCancellationPenalty) via a live
# event — the honest way to give the "penalised" persona an active penalty. Echoes the
# cancelled requestId.
cancel_own_allocated_booking() {
  local username="$1" date="$2" max_tries="${3:-1}"
  local token body req_id http_code
  token=$(get_token "$username")
  [ -z "$token" ] && { err "No token for $username (cancel)"; return 1; }

  # Poll for the Allocated booking: the Draw's lifecycle can report Completed a moment
  # before the booking read-model reflects the persisted allocation, so retry briefly
  # when the caller expects an allocation to be settling (max_tries > 1).
  req_id=""
  for _ in $(seq 1 "$max_tries"); do
    body=$(curl -sf -H "Authorization: Bearer $token" "$BOOKING_URL/bookings?pageSize=100" 2>/dev/null || true)
    req_id=$(python3 - "$date" "$body" << 'PYEOF'
import json, sys
date = sys.argv[1]
try:
    items = (json.loads(sys.argv[2]).get("items") or [])
except Exception:
    items = []
for it in items:
    if str(it.get("requestedDate")) == date and it.get("status") == "Allocated":
        print(it.get("requestId")); break
PYEOF
)
    [ -n "$req_id" ] && break
    sleep 2
  done
  [ -z "$req_id" ] && { err "$username has no Allocated booking on $date to cancel"; return 1; }

  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X DELETE "$BOOKING_URL/bookings/$req_id?reason=Green%20Logistics%20demo%20late%20cancellation" \
    -H "Authorization: Bearer $token" 2>/dev/null || true)
  [ -n "$http_code" ] || http_code="000"
  [ "$http_code" != "200" ] && { err "Cancel $username booking $req_id HTTP $http_code"; return 1; }
  echo "$req_id"
}

# The showcase reallocation finale: cancel the first Allocated general booking on the
# showcase date. Cancelling releases the general slot, and the policy's automatic
# reallocation promotes the next fair waitlisted driver (BookingRequestReallocatedEvent).
# Echoes the general index that was cancelled.
cancel_first_allocated_general() {
  local date="$1" index
  for index in $(general_indices); do
    # Decide on the exit status only. cancel_own_allocated_booking prints diagnostic
    # ok/err text to stdout even when it fails, so capturing its stdout would mistake a
    # "no allocated booking" message for a real cancellation. Discarding stdout/stderr and
    # testing the exit code means only a genuine 200 DELETE (exit 0) is accepted, and the
    # scan continues past waitlisted general drivers.
    if cancel_own_allocated_booking "${EMPLOYEE_PREFIX}$index" "$date" >/dev/null 2>&1; then
      echo "$index"
      return 0
    fi
  done
  return 1
}

# Confirm the reallocation happened: the cancelled slot is filled by a promoted driver,
# so the allocated count is unchanged and the waitlist shrank by one.
verify_reallocation() {
  local date="$1" allocated_before="$2" waitlisted_before="$3"
  local token body allocated_now waitlisted_now
  token=$(get_token "$HR_ADMIN_USER")
  [ -z "$token" ] && { err "No token for $HR_ADMIN_USER"; return 1; }

  body=$(curl -sf -H "Authorization: Bearer $token" \
    "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$date&to=$date&pageSize=200" 2>/dev/null || true)
  # The operations view reports still-waiting (waitlisted) requests as "Pending" once the
  # Draw has completed, so count both as "waiting".
  allocated_now=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(sum(1 for i in d.get('items',[]) if i.get('status')=='Allocated'))" 2>/dev/null || echo -1)
  waitlisted_now=$(printf '%s' "$body" | python3 -c "import json,sys; d=json.load(sys.stdin); print(sum(1 for i in d.get('items',[]) if i.get('status') in ('Waitlisted','Pending')))" 2>/dev/null || echo -1)

  if [ "${allocated_now:-0}" -eq "${allocated_before:-0}" ] && [ "${waitlisted_now:-0}" -eq $(( waitlisted_before - 1 )) ]; then
    ok "Reallocation: released general slot promoted the next fair waiting driver (allocated stays $allocated_now, waiting $waitlisted_before → $waitlisted_now)"
  else
    err "Reallocation not observed: allocated $allocated_before → ${allocated_now}, waiting $waitlisted_before → ${waitlisted_now} (expected allocated unchanged, waiting -1)"
    return 1
  fi
}

# Indices of general drivers (no company-car / accessibility / EV / motorcycle persona).
general_indices() {
  local index
  for index in $(seq 1 "$GL_EMPLOYEE_COUNT"); do
    if [ "$(has_company_car_for_index "$index")" = "false" ] \
      && [ "$(accessibility_for_index "$index")" = "false" ] \
      && [ "$(is_electric_for_index "$index")" = "false" ] \
      && [ "$(vehicle_type_for_index "$index")" = "Sedan" ]; then
      echo "$index"
    fi
  done
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

# ── dates ──────────────────────────────────────────────────────────────────
# The showcase Draw sits at least +2 workdays out (clear of the cutoff, visible in the
# HR workday navigation). One earlier workday carries the fairness-history Draw so the
# recent-winner/penalised personas already have real history when the showcase runs.
GL_HISTORY_OFFSET=$(next_workday_offset "$GL_DRAW_MIN_OFFSET")
GL_DRAW_OFFSET=$(next_workday_offset "$((GL_HISTORY_OFFSET + 1))")
GL_HISTORY_DATE=$(future_date "$GL_HISTORY_OFFSET")
GL_DRAW_DATE=$(future_date "$GL_DRAW_OFFSET")

# ── fairness history (real prior Draw) ───────────────────────────────────────
# Give the fair showcase Draw an explainable backstory using only real events:
#   • the recent winner (#7) wins the earlier Draw → an allocation on record;
#   • the penalised persona (#8) wins then late-cancels → an active penalty.
# Both lower their fair weight going into the showcase Draw. No projection is written
# directly, so HR/reports/audit stay internally consistent.
echo ""
echo "-- Fairness history ($GL_HISTORY_DATE) --"
history_draw "$GL_HISTORY_OFFSET" "$GL_RECENT_WINNER_INDEX" "$GL_PENALISED_INDEX"
if cancel_own_allocated_booking "${EMPLOYEE_PREFIX}${GL_PENALISED_INDEX}" "$GL_HISTORY_DATE" 20 > /dev/null; then
  ok "Penalised persona (${EMPLOYEE_PREFIX}${GL_PENALISED_INDEX}) late-cancelled its earlier allocation → active penalty"
else
  err "Could not seed the penalised persona's late-cancellation penalty"
  exit 1
fi
ok "Recent winner (${EMPLOYEE_PREFIX}${GL_RECENT_WINNER_INDEX}) holds an allocation from the earlier Draw"

# ── bookings ─────────────────────────────────────────────────────────────────

echo ""
echo "-- Bookings ($GL_DRAW_DATE — generates notifications, audit records, and reporting data) --"
echo "Green Logistics showcase Draw date: $GL_DRAW_DATE (+$GL_DRAW_OFFSET days, next workday)"

for index in $(seq 1 "$GL_BOOKING_COUNT"); do
  if [ "$index" -gt "$GL_EMPLOYEE_COUNT" ]; then
    break
  fi
  book_employee_for_offset "$index" "$GL_DRAW_OFFSET"
done

# ── Draw ─────────────────────────────────────────────────────────────────────

echo ""
echo "-- Green Logistics showcase Draw ($GL_DRAW_DATE, $GL_LOCATION_ID 08:00-18:00) --"
trigger_demo_draw "$GL_DRAW_OFFSET"
verify_demo_draw "$GL_DRAW_OFFSET"

# ── reallocation finale ──────────────────────────────────────────────────────
# Cancel one allocated general request; the policy's automatic reallocation promotes the
# next fair waitlisted driver into the freed slot — one small, visible fairness story.
echo ""
echo "-- Reallocation finale --"
GL_OPS_BEFORE=$(curl -sf -H "Authorization: Bearer $(get_token "$HR_ADMIN_USER")" \
  "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$GL_DRAW_DATE&to=$GL_DRAW_DATE&pageSize=200" 2>/dev/null || true)
GL_ALLOC_BEFORE=$(printf '%s' "$GL_OPS_BEFORE" | python3 -c "import json,sys; d=json.load(sys.stdin); print(sum(1 for i in d.get('items',[]) if i.get('status')=='Allocated'))" 2>/dev/null || echo 0)
GL_WAIT_BEFORE=$(printf '%s' "$GL_OPS_BEFORE" | python3 -c "import json,sys; d=json.load(sys.stdin); print(sum(1 for i in d.get('items',[]) if i.get('status') in ('Waitlisted','Pending')))" 2>/dev/null || echo 0)

GL_CANCELLED_INDEX=$(cancel_first_allocated_general "$GL_DRAW_DATE" || true)
if [ -n "$GL_CANCELLED_INDEX" ]; then
  ok "Cancelled ${EMPLOYEE_PREFIX}${GL_CANCELLED_INDEX}'s allocated general request (freed a general slot)"
  verify_reallocation "$GL_DRAW_DATE" "$GL_ALLOC_BEFORE" "$GL_WAIT_BEFORE"
else
  err "No allocated general request found to cancel for the reallocation finale"
  exit 1
fi

echo ""
echo "-- HR display names --"
verify_hr_display_names
verify_hr_booking_display_names "$GL_DRAW_DATE"

# ── summary ──────────────────────────────────────────────────────────────────

echo ""
echo "== Seed complete =="
echo "Tenant: $GL_TENANT (Green Logistics showcase)"
echo "Profiles: $GL_EMPLOYEE_COUNT employees (1 company-car, 1 EV, 1 accessible, 1 motorcycle, 6 general incl. a recent winner and a penalised driver), plus Lucie Prochazkova, Karel Urban, Eva Kralova, Martin Cerny (roles)"
echo "Facility/location: $GL_FACILITY_LABEL / $GL_LOCATION_ID"
echo "Parking: 6 named slots (A-01, A-02 general, EV-01 charger, ACC-01 accessible, MOTO-01 motorcycle, VIP-01 company-car)"
echo "History: earlier Draw on $GL_HISTORY_DATE gave the recent winner an allocation and the penalised driver an active late-cancellation penalty (real events)."
echo "Bookings: $GL_BOOKING_COUNT requests for $GL_DRAW_DATE; showcase Draw triggered."
echo "Draw: company-car takes VIP-01 (Tier-1) at submission; motorcycle takes MOTO-01; the general lottery allocates the scarce slots and leaves a visible waitlist (verified above)."
echo "Reallocation: one allocated general request was cancelled and the next fair waitlisted driver was promoted (verified above)."
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
