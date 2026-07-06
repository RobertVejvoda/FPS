#!/usr/bin/env bash
# perf-seed-greenlogistics.sh — PERF001 load-test seed for the Green Logistics tenant.
#
# Generates a deterministic, resettable, large-scale dataset in the GL tenant for
# performance and readiness validation. All data is clearly synthetic (no real PII).
#
# Prerequisites:
#   1. Local harness running:  ./tools/start-local-harness.sh
#   2. Keycloak configured:    FPS_GL_EMPLOYEE_COUNT=<N> ./tools/dev-setup-auth.sh
#      (N must be >= GL_EMPLOYEE_COUNT used here; default 50)
#   3. GL tenant provisioned:  ./tools/provision-tenant.sh tools/templates/tenants/greenlogistics.json
#      (or start-local-harness seeds it automatically via Program.cs)
#
# Usage:
#   ./tools/perf-seed-greenlogistics.sh
#   GL_EMPLOYEE_COUNT=200 GL_SLOT_COUNT=100 ./tools/perf-seed-greenlogistics.sh
#   GL_EMPLOYEE_COUNT=500 GL_SLOT_COUNT=200 GL_DRAW_COUNT=5 ./tools/perf-seed-greenlogistics.sh
#
# Environment variables (all optional):
#   GL_EMPLOYEE_COUNT    total employees to seed profiles for (default 50; 1..N requires
#                        FPS_GL_EMPLOYEE_COUNT=N in dev-setup-auth.sh; beyond Keycloak count
#                        only profiles are seeded — no booking possible for those users)
#   GL_SLOT_COUNT        total parking slots to configure at GL-HQ (default 50)
#   GL_DRAW_COUNT        number of future-workday draws to seed requests+run (default 3)
#   GL_BOOKING_RATIO     fraction of employees who submit a booking request (0.0..1.0, default 0.9)
#   RESET_STATE          wipe GL booking/profile state before seeding (true/false, default true)
#   PROFILE_URL          Profile service base URL (default http://localhost:5197)
#   BOOKING_URL          Booking service base URL (default http://localhost:5131)
#   CONFIG_URL           Configuration service base URL (default http://localhost:5141)
#   KEYCLOAK_URL         Keycloak base URL (default http://localhost:8180)
#   FPS_DEV_PASSWORD     dev password (default Dev1234!)
#
# Output:
#   Per-phase progress and timing, then a readiness-evidence summary table.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

PROFILE_URL="${PROFILE_URL:-http://localhost:5197}"
BOOKING_URL="${BOOKING_URL:-http://localhost:5131}"
CONFIG_URL="${CONFIG_URL:-http://localhost:5141}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"

GL_TENANT_ID="greenlogistics"
GL_FACILITY_ID="${GL_FACILITY_ID:-00000000-0000-0000-0000-000000000002}"
GL_LOCATION_ID="GL-HQ"

GL_EMPLOYEE_COUNT="${GL_EMPLOYEE_COUNT:-50}"
GL_SLOT_COUNT="${GL_SLOT_COUNT:-50}"
GL_DRAW_COUNT="${GL_DRAW_COUNT:-3}"
GL_BOOKING_RATIO="${GL_BOOKING_RATIO:-0.9}"
RESET_STATE="${RESET_STATE:-true}"

GREEN='\033[0;32m'; YELLOW='\033[0;33m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'
ok()   { echo -e "  ${GREEN}OK${NC}   $1"; }
warn() { echo -e "  ${YELLOW}WARN${NC} $1"; }
err()  { echo -e "  ${RED}ERR${NC}  $1"; }
info() { echo -e "  ${CYAN}....${NC} $1"; }

ERRORS=0
PHASE_TIMINGS=""

record_timing() { PHASE_TIMINGS="${PHASE_TIMINGS}$1: $2s\n"; }

# ── helpers ───────────────────────────────────────────────────────────────────

get_token() {
  local user="$1"
  local body
  body=$(curl -s -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=$CLIENT_ID" \
    --data-urlencode "username=$user" \
    --data-urlencode "password=$DEV_PASSWORD" || true)
  printf '%s' "$body" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('access_token',''))" 2>/dev/null || true
}

future_workday_date() {
  local offset="$1"
  python3 - "$offset" << 'PYEOF'
import sys
from datetime import date, timedelta

offset = int(sys.argv[1])
d = date.today() + timedelta(days=offset)
while d.weekday() >= 5:
    d += timedelta(days=1)
print(d.isoformat())
PYEOF
}

check_service() {
  local url="$1" name="$2"
  if ! curl -sf "$url/openapi/v1.json" > /dev/null 2>&1; then
    err "$name not reachable at $url"
    echo "Start all services: ./tools/start-local-harness.sh"
    exit 1
  fi
}

# ── employee name generator ───────────────────────────────────────────────────

display_name_for_index() {
  python3 - "$1" << 'PYEOF'
import sys
first_names = [
    "Alice","Bob","Carla","David","Eva","Filip","Gabriela","Hana","Ivan","Jana",
    "Karel","Lenka","Milan","Nina","Ondrej","Petra","Radek","Sandra","Tomas","Ula",
    "Vaclav","Wendy","Xena","Yuri","Zuzana","Adam","Barbora","Ctirad","Dana","Emil",
    "Frantisek","Gertruda","Helmut","Irena","Josef","Kveta","Libor","Milena","Norbert","Olga",
    "Pavel","Renata","Stanislav","Tereza","Uwe","Vera","Walter","Xander","Yvonne","Zbynek",
]
last_names = [
    "Novak","Dvořák","Procházka","Krejčí","Vlček","Blažek","Fiala","Sedlák","Marek","Horák",
    "Pokorný","Nováková","Veselý","Čermák","Pospíšil","Kopecký","Urban","Malý","Beneš","Král",
    "Horáček","Jelínek","Marková","Kolář","Žáček","Kadlec","Kubíček","Tichý","Holý","Kříž",
    "Chalupa","Krátký","Kouřil","Brabec","Šimánek","Ševčík","Bartoš","Konečný","Ludvík","Mašek",
    "Vácha","Mašková","Formánek","Kovář","Čapek","Havel","Slavík","Bednář","Řezáč","Šimák",
]
idx = int(sys.argv[1]) - 1
first = first_names[idx % len(first_names)]
last  = last_names[idx % len(last_names)]
print(f"{first} {last}")
PYEOF
}

license_plate_for() {
  local i="$1"
  printf 'GL%04d' "$i"
}

# ── Phase 0: preflight ────────────────────────────────────────────────────────

echo ""
echo "== PERF001 — Green Logistics load-test seed =="
echo "Tenant:    $GL_TENANT_ID"
echo "Location:  $GL_LOCATION_ID"
echo "Employees: $GL_EMPLOYEE_COUNT"
echo "Slots:     $GL_SLOT_COUNT"
echo "Draws:     $GL_DRAW_COUNT"
echo "Reset:     $RESET_STATE"
echo ""

info "Checking services..."
check_service "$PROFILE_URL" "Profile"
check_service "$BOOKING_URL" "Booking"
check_service "$CONFIG_URL"  "Configuration"
ok "All services reachable"

ADMIN_TOKEN=$(get_token "gl-tenant-admin")
if [ -z "$ADMIN_TOKEN" ]; then
  err "Cannot get token for gl-tenant-admin. Run: FPS_GL_EMPLOYEE_COUNT=$GL_EMPLOYEE_COUNT ./tools/dev-setup-auth.sh"
  exit 1
fi
ok "gl-tenant-admin authenticated"

# ── Phase 0.5: optional reset ─────────────────────────────────────────────────

if [ "$RESET_STATE" = "true" ]; then
  echo ""
  echo "-- Resetting GL booking/profile state --"
  if command -v docker > /dev/null 2>&1; then
    RESET_ERRORS=0

    if docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T mongodb \
        mongosh --quiet --eval \
        "['fairspot-booking','fairspot-notification','fairspot-reporting','fairspot-audit'].forEach(dbName => db.getSiblingDB(dbName).dropDatabase());" \
        > /dev/null 2>&1; then
      ok "MongoDB collections dropped"
    else
      warn "MongoDB reset skipped (service not running)"
      RESET_ERRORS=$((RESET_ERRORS+1))
    fi

    if docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T postgres \
        psql -U fps -d fps_datahub -c \
        'TRUNCATE TABLE datahub_booking_outcome, datahub_draw_history, datahub_event_inbox, datahub_projection_checkpoint RESTART IDENTITY;' \
        > /dev/null 2>&1; then
      ok "PostgreSQL tables truncated"
    else
      warn "PostgreSQL reset skipped (service not running or table missing)"
      RESET_ERRORS=$((RESET_ERRORS+1))
    fi

    if docker compose -f "$REPO_ROOT/code/infrastructure/docker-compose.yaml" exec -T redis \
        redis-cli FLUSHDB > /dev/null 2>&1; then
      ok "Redis flushed"
    else
      warn "Redis reset skipped (service not running)"
      RESET_ERRORS=$((RESET_ERRORS+1))
    fi

    if [ "$RESET_ERRORS" -eq 0 ]; then
      ok "State reset complete (all stores cleared)"
    else
      warn "State reset partial — $RESET_ERRORS store(s) skipped. Re-run start-local-harness.sh if needed."
    fi
  else
    warn "docker not found — skipping state reset. Set RESET_STATE=false to suppress this warning."
  fi
fi

# ── Phase 1: Slot configuration ───────────────────────────────────────────────

echo ""
echo "-- Phase 1: Slot configuration ($GL_SLOT_COUNT slots at $GL_LOCATION_ID) --"
PHASE1_START=$(date +%s)

# Generate a realistic slot mix:
#   ~5%  company-car-only fixed slots
#   ~8%  EV charger slots
#   ~4%  accessible slots
#   ~3%  inactive (out-of-service)
#   rest normal
SLOTS_JSON=$(python3 - "$GL_SLOT_COUNT" "$GL_TENANT_ID" "$GL_LOCATION_ID" << 'PYEOF'
import json
import sys
import math

total   = int(sys.argv[1])
company = max(1, round(total * 0.05))
ev      = max(1, round(total * 0.08))
access  = max(1, round(total * 0.04))
inactive = max(0, round(total * 0.03))
normal  = total - company - ev - access - inactive

slots = []
idx = 1

for _ in range(company):
    slots.append({"slotId": f"GL-CC-{idx:03d}", "isActive": True,  "hasCharger": False,
                  "isAccessible": False, "isCompanyCarOnly": True, "isMotorcycleCapacity": False,
                  "reservedForUserId": None})
    idx += 1

for _ in range(ev):
    slots.append({"slotId": f"GL-EV-{idx:03d}", "isActive": True, "hasCharger": True,
                  "isAccessible": False, "isCompanyCarOnly": False, "isMotorcycleCapacity": False,
                  "reservedForUserId": None})
    idx += 1

for _ in range(access):
    slots.append({"slotId": f"GL-AC-{idx:03d}", "isActive": True, "hasCharger": False,
                  "isAccessible": True, "isCompanyCarOnly": False, "isMotorcycleCapacity": False,
                  "reservedForUserId": None})
    idx += 1

for _ in range(inactive):
    slots.append({"slotId": f"GL-OFF-{idx:03d}", "isActive": False, "hasCharger": False,
                  "isAccessible": False, "isCompanyCarOnly": False, "isMotorcycleCapacity": False,
                  "reservedForUserId": None})
    idx += 1

for _ in range(normal):
    slots.append({"slotId": f"GL-N-{idx:03d}", "isActive": True, "hasCharger": False,
                  "isAccessible": False, "isCompanyCarOnly": False, "isMotorcycleCapacity": False,
                  "reservedForUserId": None})
    idx += 1

print(json.dumps({"slots": slots, "changeReason": "PERF001 load-test configuration"}))
PYEOF
)

SLOT_HTTP=$(curl -s -w "\n%{http_code}" \
  -X PUT "$CONFIG_URL/configuration/locations/$GL_LOCATION_ID/slots" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SLOTS_JSON" 2>/dev/null || true)

SLOT_CODE=$(printf '%s' "$SLOT_HTTP" | tail -n1)
if [ "$SLOT_CODE" = "204" ]; then
  COMPANY_COUNT=$(python3 -c "import json,sys; s=json.loads(sys.argv[1]); print(sum(1 for x in s['slots'] if x['isCompanyCarOnly']))" "$SLOTS_JSON")
  EV_COUNT=$(python3 -c "import json,sys; s=json.loads(sys.argv[1]); print(sum(1 for x in s['slots'] if x['hasCharger']))" "$SLOTS_JSON")
  ACCESS_COUNT=$(python3 -c "import json,sys; s=json.loads(sys.argv[1]); print(sum(1 for x in s['slots'] if x['isAccessible']))" "$SLOTS_JSON")
  INACTIVE_COUNT=$(python3 -c "import json,sys; s=json.loads(sys.argv[1]); print(sum(1 for x in s['slots'] if not x['isActive']))" "$SLOTS_JSON")
  ok "Slots configured: $GL_SLOT_COUNT total ($COMPANY_COUNT company-car, $EV_COUNT EV, $ACCESS_COUNT accessible, $INACTIVE_COUNT inactive)"
else
  SLOT_BODY=$(printf '%s' "$SLOT_HTTP" | sed '$d')
  err "Slot configuration failed HTTP $SLOT_CODE: $SLOT_BODY"
  ERRORS=$((ERRORS+1))
fi

PHASE1_END=$(date +%s)
record_timing "Phase 1 (slots)" "$((PHASE1_END-PHASE1_START))"

# ── Phase 2: Profile seeding ──────────────────────────────────────────────────

echo ""
echo "-- Phase 2: Profile seeding ($GL_EMPLOYEE_COUNT employees via bulk bootstrap) --"
PHASE2_START=$(date +%s)

# Build employee profile mix:
#   ~10%  company-car employees
#   ~5%   accessibility-eligible
#   ~3%   motorcycle riders (no company car)
#   rest  regular employees (sedan/EV)
EMPLOYEES_JSON=$(python3 - "$GL_EMPLOYEE_COUNT" "$GL_LOCATION_ID" << 'PYEOF'
import json
import sys
import math

count    = int(sys.argv[1])
loc_id   = sys.argv[2]

first_names = [
    "Alice","Bob","Carla","David","Eva","Filip","Gabriela","Hana","Ivan","Jana",
    "Karel","Lenka","Milan","Nina","Ondrej","Petra","Radek","Sandra","Tomas","Ula",
    "Vaclav","Wendy","Xena","Yuri","Zuzana","Adam","Barbora","Ctirad","Dana","Emil",
    "Frantisek","Gertruda","Helmut","Irena","Josef","Kveta","Libor","Milena","Norbert","Olga",
    "Pavel","Renata","Stanislav","Tereza","Uwe","Vera","Walter","Xander","Yvonne","Zbynek",
]
last_names = [
    "Novak","Dvorak","Prochazka","Krejci","Vlcek","Blazek","Fiala","Sedlak","Marek","Horak",
    "Pokorny","Novakova","Vesely","Cermak","Pospisil","Kopecky","Urban","Maly","Benes","Kral",
    "Horacek","Jelinek","Markova","Kolar","Zacek","Kadlec","Kubicek","Tichy","Holy","Kriz",
    "Chalupa","Kratky","Kouril","Brabec","Simanek","Sevcik","Bartos","Konecny","Ludvik","Masek",
    "Vacha","Maskova","Formanek","Kovar","Capek","Havel","Slavik","Bednar","Rezac","Simak",
]

employees = []
for i in range(1, count + 1):
    first = first_names[(i-1) % len(first_names)]
    last  = last_names[(i-1) % len(last_names)]

    has_cc   = (i % 10 == 0)
    access   = (i % 20 == 0)
    # externalSubject: synthetic stable ID — format mirrors what Keycloak sub looks like
    ext_sub  = f"gl-perf-{i:05d}@greenlogistics.example"

    employees.append({
        "externalSubject": ext_sub,
        "employeeId": f"GL-EMP-{i:05d}",
        "isActive": True,
        "fpsRoles": ["employee"],
        "notificationAddress": f"gl-employee{i}@greenlogistics.example",
        "homeLocationId": loc_id,
        "parkingEligible": True,
        "hasCompanyCar": has_cc,
        "accessibilityEligible": access,
        "reservedSpaceEligible": False,
    })

print(json.dumps({"employees": employees}))
PYEOF
)

PROFILE_HTTP=$(curl -s -w "\n%{http_code}" \
  -X POST "$PROFILE_URL/profile/bootstrap/import" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$EMPLOYEES_JSON" 2>/dev/null || true)

PROFILE_CODE=$(printf '%s' "$PROFILE_HTTP" | tail -n1)
PROFILE_BODY=$(printf '%s' "$PROFILE_HTTP" | sed '$d')

if [ "$PROFILE_CODE" = "200" ]; then
  CREATED=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('created', d.get('Created', '?')))" "$PROFILE_BODY" 2>/dev/null || echo "?")
  UPDATED=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('updated', d.get('Updated', '?')))" "$PROFILE_BODY" 2>/dev/null || echo "?")
  SKIPPED=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('skipped', d.get('Skipped', '0')))" "$PROFILE_BODY" 2>/dev/null || echo "0")
  ok "Profiles seeded: ${CREATED} created, ${UPDATED} updated, ${SKIPPED} skipped"
else
  err "Profile import failed HTTP $PROFILE_CODE: $PROFILE_BODY"
  ERRORS=$((ERRORS+1))
fi

# Also seed display-name snapshots for Keycloak-known GL employees (dev-only endpoint)
GL_EMPLOYEE_KC_MAX="${GL_EMPLOYEE_KC_MAX:-$GL_EMPLOYEE_COUNT}"
SNAPSHOT_OK=0; SNAPSHOT_FAIL=0
for i in $(seq 1 "$GL_EMPLOYEE_KC_MAX"); do
  KC_USER="gl-employee$i"
  KC_TOKEN=$(get_token "$KC_USER" 2>/dev/null || true)
  if [ -z "$KC_TOKEN" ]; then
    break
  fi
  NAME=$(display_name_for_index "$i")
  PLATE=$(license_plate_for "$i")
  SNAP_HTTP=$(curl -s -w "\n%{http_code}" \
    -X PUT "$PROFILE_URL/profile/admin/snapshot" \
    -H "Content-Type: application/json" \
    -d "{
      \"tenantId\": \"$GL_TENANT_ID\",
      \"userId\": \"gl-employee$i\",
      \"displayName\": \"$NAME\",
      \"parkingEligible\": true,
      \"hasCompanyCar\": $([ $((i % 10)) -eq 0 ] && echo true || echo false),
      \"accessibilityEligible\": $([ $((i % 20)) -eq 0 ] && echo true || echo false),
      \"reservedSpaceEligible\": false,
      \"vehicles\": [{
        \"vehicleId\": \"VEH-GL-$(printf '%04d' $i)\",
        \"licensePlate\": \"$PLATE\",
        \"vehicleType\": \"Sedan\",
        \"isElectric\": $([ $((i % 8)) -eq 0 ] && echo true || echo false),
        \"isActive\": true,
        \"isDefault\": true
      }]
    }" 2>/dev/null || true)
  SNAP_CODE=$(printf '%s' "$SNAP_HTTP" | tail -n1)
  if [ "$SNAP_CODE" = "204" ]; then SNAPSHOT_OK=$((SNAPSHOT_OK+1)); else SNAPSHOT_FAIL=$((SNAPSHOT_FAIL+1)); fi
done
if [ "$SNAPSHOT_OK" -gt 0 ]; then
  ok "Display-name snapshots for Keycloak GL users: $SNAPSHOT_OK seeded${SNAPSHOT_FAIL:+, $SNAPSHOT_FAIL failed}"
fi

PHASE2_END=$(date +%s)
record_timing "Phase 2 (profiles)" "$((PHASE2_END-PHASE2_START))"

# ── Phase 3: Booking requests ─────────────────────────────────────────────────

echo ""
echo "-- Phase 3: Booking requests ($GL_DRAW_COUNT draw dates) --"
PHASE3_START=$(date +%s)

BOOKING_OK=0; BOOKING_FAIL=0; BOOKING_SKIP=0
DRAW_DATES=""

for draw_num in $(seq 1 "$GL_DRAW_COUNT"); do
  DRAW_OFFSET=$((draw_num + 1))
  DRAW_DATE=$(future_workday_date "$DRAW_OFFSET")
  DRAW_DATES="$DRAW_DATES $DRAW_DATE"

  DATE_BOOK_OK=0; DATE_BOOK_FAIL=0
  for i in $(seq 1 "$GL_EMPLOYEE_COUNT"); do
    KC_USER="gl-employee$i"
    USER_TOKEN=$(get_token "$KC_USER" 2>/dev/null || true)
    if [ -z "$USER_TOKEN" ]; then
      BOOKING_SKIP=$((BOOKING_SKIP+1))
      continue
    fi

    PLATE=$(license_plate_for "$i")
    IS_CC=$([ $((i % 10)) -eq 0 ] && echo true || echo false)
    ARRIVAL="${DRAW_DATE}T08:00:00"
    DEPARTURE="${DRAW_DATE}T18:00:00"

    BOOK_HTTP=$(curl -s -o /dev/null -w "%{http_code}" \
      -X POST "$BOOKING_URL/bookings" \
      -H "Authorization: Bearer $USER_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"facilityId\": \"$GL_FACILITY_ID\",
        \"locationId\": \"$GL_LOCATION_ID\",
        \"licensePlate\": \"$PLATE\",
        \"vehicleType\": \"Sedan\",
        \"isElectric\": false,
        \"requiresAccessibleSpot\": false,
        \"isCompanyCar\": $IS_CC,
        \"plannedArrivalTime\": \"$ARRIVAL\",
        \"plannedDepartureTime\": \"$DEPARTURE\"
      }" 2>/dev/null || true)

    if [ "$BOOK_HTTP" = "202" ]; then
      DATE_BOOK_OK=$((DATE_BOOK_OK+1))
      BOOKING_OK=$((BOOKING_OK+1))
    else
      DATE_BOOK_FAIL=$((DATE_BOOK_FAIL+1))
      BOOKING_FAIL=$((BOOKING_FAIL+1))
    fi
  done
  ok "Draw $draw_num ($DRAW_DATE): $DATE_BOOK_OK accepted${DATE_BOOK_FAIL:+, $DATE_BOOK_FAIL failed}"
done

if [ "$BOOKING_SKIP" -gt 0 ]; then
  warn "$BOOKING_SKIP booking(s) skipped — Keycloak users not available. Run: FPS_GL_EMPLOYEE_COUNT=$GL_EMPLOYEE_COUNT ./tools/dev-setup-auth.sh"
fi
ok "Bookings total: $BOOKING_OK accepted"

PHASE3_END=$(date +%s)
record_timing "Phase 3 (bookings)" "$((PHASE3_END-PHASE3_START))"

# ── Phase 4: Draws + timing ───────────────────────────────────────────────────

echo ""
echo "-- Phase 4: Draw execution + timing --"
PHASE4_START=$(date +%s)

DRAW_OK=0; DRAW_FAIL=0
for DRAW_DATE in $DRAW_DATES; do
  DRAW_START=$(date +%s)

  DRAW_HTTP=$(curl -s -w "\n%{http_code}" \
    -X POST "$BOOKING_URL/draws/trigger" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"locationId\": \"$GL_LOCATION_ID\",
      \"date\": \"$DRAW_DATE\",
      \"timeSlotStart\": \"${DRAW_DATE}T08:00:00\",
      \"timeSlotEnd\":   \"${DRAW_DATE}T18:00:00\",
      \"reason\": \"PERF001 load-test draw\"
    }" 2>/dev/null || true)

  DRAW_END=$(date +%s)
  DRAW_ELAPSED=$((DRAW_END-DRAW_START))

  DRAW_CODE=$(printf '%s' "$DRAW_HTTP" | tail -n1)
  DRAW_BODY=$(printf '%s' "$DRAW_HTTP" | sed '$d')

  if [ "$DRAW_CODE" = "200" ]; then
    ALLOCATED=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('allocated',d.get('Allocated','?')))" "$DRAW_BODY" 2>/dev/null || echo "?")
    REJECTED=$(python3 -c  "import json,sys; d=json.loads(sys.argv[1]); print(d.get('rejected', d.get('Rejected', '?')))" "$DRAW_BODY" 2>/dev/null || echo "?")
    WAITLISTED=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d.get('waitlisted',d.get('Waitlisted','?')))" "$DRAW_BODY" 2>/dev/null || echo "?")
    STATUS=$(python3 -c    "import json,sys; d=json.loads(sys.argv[1]); print(d.get('status',    d.get('Status',   '?')))" "$DRAW_BODY" 2>/dev/null || echo "?")
    ok "Draw $DRAW_DATE ${DRAW_ELAPSED}s: allocated=$ALLOCATED rejected=$REJECTED waitlisted=$WAITLISTED status=$STATUS"
    record_timing "Draw $DRAW_DATE" "$DRAW_ELAPSED"
    DRAW_OK=$((DRAW_OK+1))
  else
    err "Draw $DRAW_DATE HTTP $DRAW_CODE (${DRAW_ELAPSED}s): $DRAW_BODY"
    ERRORS=$((ERRORS+1))
    DRAW_FAIL=$((DRAW_FAIL+1))
  fi
done

PHASE4_END=$(date +%s)
record_timing "Phase 4 (draws total)" "$((PHASE4_END-PHASE4_START))"

# ── Phase 5: HR API response times ────────────────────────────────────────────

echo ""
echo "-- Phase 5: HR/reporting API response times --"

measure_response() {
  local label="$1" url="$2" token="$3"
  local start end elapsed http_code
  start=$(date +%s)
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Authorization: Bearer $token" \
    "$url" 2>/dev/null || echo "000")
  end=$(date +%s)
  elapsed=$((end-start))
  if [ "$http_code" = "200" ]; then
    ok "$label: ${elapsed}s (HTTP $http_code)"
    record_timing "$label" "$elapsed"
  else
    warn "$label: ${elapsed}s (HTTP $http_code — check service)"
    record_timing "$label (HTTP $http_code)" "$elapsed"
  fi
}

FIRST_DRAW_DATE=$(printf '%s' "$DRAW_DATES" | awk '{print $1}')

measure_response "HR booking operations (page 1)" \
  "$BOOKING_URL/bookings/operations?locationId=$GL_LOCATION_ID&from=$FIRST_DRAW_DATE&to=$FIRST_DRAW_DATE&pageSize=50" \
  "$ADMIN_TOKEN"

measure_response "Draw outcomes (first date)" \
  "$BOOKING_URL/draws/outcomes?locationId=$GL_LOCATION_ID&date=$FIRST_DRAW_DATE" \
  "$ADMIN_TOKEN"

measure_response "Reports: parking summary" \
  "http://localhost:5171/reports/parking/summary" \
  "$ADMIN_TOKEN"

measure_response "Reports: fairness" \
  "http://localhost:5171/reports/parking/fairness?locationId=$GL_LOCATION_ID&pageSize=50" \
  "$ADMIN_TOKEN"

measure_response "HR display names (bootstrap page)" \
  "$PROFILE_URL/profile/bootstrap?pageSize=50" \
  "$ADMIN_TOKEN"

# ── Readiness summary ─────────────────────────────────────────────────────────

echo ""
echo "════════════════════════════════════════════════════════"
echo "PERF001 — Green Logistics Load-Test Readiness Evidence"
echo "════════════════════════════════════════════════════════"
echo ""
echo "Dataset:"
printf "  %-28s %s\n" "Tenant:"       "$GL_TENANT_ID"
printf "  %-28s %s\n" "Location:"     "$GL_LOCATION_ID"
printf "  %-28s %s\n" "Employees seeded:" "$GL_EMPLOYEE_COUNT"
printf "  %-28s %s\n" "Parking slots:"    "$GL_SLOT_COUNT"
printf "  %-28s %s\n" "Draw dates run:"   "$DRAW_OK"
printf "  %-28s %s\n" "Bookings accepted:" "$BOOKING_OK"
if [ "$BOOKING_SKIP" -gt 0 ]; then
printf "  %-28s %s\n" "Bookings skipped:" "$BOOKING_SKIP (Keycloak users not created)"
fi
echo ""
echo "Phase timings:"
printf '%b' "$PHASE_TIMINGS" | while IFS= read -r line; do
  [ -n "$line" ] && printf "  %s\n" "$line"
done
echo ""
if [ "$ERRORS" -gt 0 ]; then
  echo -e "  ${RED}ERRORS: $ERRORS — see output above for details${NC}"
else
  echo -e "  ${GREEN}All phases completed without errors.${NC}"
fi
echo ""
echo "Readiness verdict:"
if [ "$DRAW_OK" -ge 1 ] && [ "$ERRORS" -eq 0 ]; then
  echo "  DEMO-READY — draw executed successfully at configured scale."
  echo "  Review HR API response times above for UI usability assessment."
elif [ "$DRAW_OK" -ge 1 ]; then
  echo "  PARTIALLY READY — draw ran but some phases had errors. Review output."
else
  echo "  NOT READY — draw did not complete. Review errors above."
fi
echo ""
echo "Follow-up: open issues for any response times > 2s or HR screens that"
echo "require unbounded queries. See docs/production/perf001-readiness-evidence.md"
echo ""
echo "Verify manually:"
echo "  TOKEN=\$(./tools/dev-auth.sh gl-tenant-admin)"
echo "  curl -H \"Authorization: Bearer \$TOKEN\" \"http://localhost:10000/bookings/operations?locationId=$GL_LOCATION_ID&from=$FIRST_DRAW_DATE&to=$FIRST_DRAW_DATE&pageSize=50\""
