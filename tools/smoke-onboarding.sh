#!/usr/bin/env bash
# Tenant onboarding E2E smoke — synthetic acme-corp path.
# Each step prints PASS / FAIL / SKIP and a note on current implementation status.
# Run after: ./tools/start-local-harness.sh && ./tools/dev-seed.sh
#
# Usage: ./tools/smoke-onboarding.sh
set -euo pipefail

GATEWAY="${GATEWAY_URL:-http://localhost:10000}"
CUSTOMER_SVC="${CUSTOMER_URL:-http://localhost:5181}"
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m'

pass()  { echo -e "  ${GREEN}PASS${NC}  $1"; }
fail()  { echo -e "  ${RED}FAIL${NC}  $1"; FAILURES=$((FAILURES+1)); }
skip()  { echo -e "  ${YELLOW}SKIP${NC}  $1 (evaluation-grade or manual — see docs/production/tenant-onboarding-smoke.md)"; }
header(){ echo; echo "=== $1 ==="; }

FAILURES=0

# ── helpers ─────────────────────────────────────────────────────────────────

get_token() {
  local user="$1"
  ./tools/dev-auth.sh "$user" 2>/dev/null || { fail "dev-auth.sh failed for $user"; echo ""; }
}

check_health() {
  local port="$1" name="$2"
  local status
  status=$(curl -sf "http://localhost:$port/health" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$status" == "Healthy" ]]; then
    pass "$name health: $status"
  else
    fail "$name health: $status"
  fi
}

# ── pre-conditions ───────────────────────────────────────────────────────────

header "Pre-conditions: service health"
for spec in "5192:Identity" "5131:Booking" "5157:Notification" "5197:Profile" \
            "5161:Audit" "5171:Reporting" "5141:Configuration" "5181:Customer"; do
  IFS=: read -r port name <<< "$spec"
  check_health "$port" "$name"
done

# ── step 1: tenant workspace ─────────────────────────────────────────────────

header "Step 1 — Tenant workspace (evaluation-grade: seeded on startup)"
TOKEN=$(get_token tenant-admin)
if [[ -z "$TOKEN" ]]; then
  fail "Could not obtain tenant-admin token"
else
  STATUS=$(curl -sf -H "Authorization: Bearer $TOKEN" "$CUSTOMER_SVC/customer/tenant/readiness" \
    | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$STATUS" != "UNREACHABLE" ]]; then
    pass "Tenant readiness endpoint reachable (status: $STATUS)"
  else
    fail "Tenant readiness endpoint unreachable"
  fi
  skip "POST /customer/tenants not yet implemented — using seeded tenant-1"
fi

# ── step 2: identity and role mapping ───────────────────────────────────────

header "Step 2 — Identity and role mapping"
TOKEN=$(get_token employee1)
if [[ -n "$TOKEN" ]]; then
  ME=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/me" 2>/dev/null || echo "UNREACHABLE")
  TENANT=$(echo "$ME" | python3 -c "import sys,json; print(json.load(sys.stdin).get('tenantId','MISSING'))" 2>/dev/null || echo "PARSE_FAIL")
  ROLES=$(echo "$ME" | python3 -c "import sys,json; print(','.join(json.load(sys.stdin).get('roles',[])))" 2>/dev/null || echo "PARSE_FAIL")
  if [[ "$TENANT" == "tenant-1" && "$ROLES" == *"employee"* ]]; then
    pass "GET /me for employee1: tenantId=$TENANT roles=$ROLES"
  else
    fail "GET /me for employee1: unexpected tenantId=$TENANT or roles=$ROLES"
  fi
else
  fail "Could not obtain employee1 token"
fi
skip "Per-tenant OIDC client and group mapping UI — manual Keycloak configuration"

# ── step 3: first administrator ──────────────────────────────────────────────

header "Step 3 — First administrator"
TOKEN=$(get_token tenant-admin)
if [[ -n "$TOKEN" ]]; then
  ME=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/me" 2>/dev/null || echo "UNREACHABLE")
  ROLES=$(echo "$ME" | python3 -c "import sys,json; print(','.join(json.load(sys.stdin).get('roles',[])))" 2>/dev/null || echo "PARSE_FAIL")
  if [[ "$ROLES" == *"admin"* ]]; then
    pass "tenant-admin has admin role: $ROLES"
  else
    fail "tenant-admin missing admin role: $ROLES"
  fi
else
  fail "Could not obtain tenant-admin token"
fi
skip "First-admin provisioning API (CUST004) — using Keycloak-pre-configured user"

# ── step 4: parking bootstrap ────────────────────────────────────────────────

header "Step 4 — Parking bootstrap (location, policy, slots)"
TOKEN=$(get_token tenant-admin)
if [[ -n "$TOKEN" ]]; then
  POLICY=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/configuration/policy" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$POLICY" != "UNREACHABLE" && "$POLICY" != "" ]]; then
    SLOTS=$(echo "$POLICY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('slotCount','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "Configuration policy reachable (slotCount: $SLOTS)"
  else
    fail "Configuration policy unreachable"
  fi
else
  fail "Could not obtain tenant-admin token"
fi
skip "Tenant admin web UI for location/slot setup — using seeded Configuration data"

# ── step 5: employee and profile bootstrap ───────────────────────────────────

header "Step 5 — Employee and profile bootstrap"
TOKEN=$(get_token employee1)
if [[ -n "$TOKEN" ]]; then
  PROFILE=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/profile/snapshot" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$PROFILE" != "UNREACHABLE" && "$PROFILE" != "" ]]; then
    ELIGIBLE=$(echo "$PROFILE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('parkingEligible','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "GET /profile/snapshot for employee1 (parkingEligible: $ELIGIBLE)"
  else
    fail "GET /profile/snapshot unreachable for employee1 — run ./tools/dev-seed.sh first"
  fi
else
  fail "Could not obtain employee1 token"
fi

# Validate HR templates
if ./tools/validate-hr-import.sh tools/templates/demo-employees.csv tools/templates/demo-vehicles.csv > /dev/null 2>&1; then
  pass "HR import templates validate cleanly"
else
  fail "HR import template validation errors — run ./tools/validate-hr-import.sh for details"
fi
skip "Web HR import upload (DATA002) — using dev-seed.sh and validate-hr-import.sh"

# ── step 6: readiness check ──────────────────────────────────────────────────

header "Step 6 — Readiness check"
TOKEN=$(get_token tenant-admin)
if [[ -n "$TOKEN" ]]; then
  READY=$(curl -sf -H "Authorization: Bearer $TOKEN" "$CUSTOMER_SVC/customer/tenant/readiness" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$READY" != "UNREACHABLE" && "$READY" != "" ]]; then
    STATUS=$(echo "$READY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "Readiness check endpoint reachable (status: $STATUS)"
  else
    fail "Readiness check endpoint unreachable"
  fi
else
  fail "Could not obtain tenant-admin token"
fi

# ── step 7: first booking smoke ──────────────────────────────────────────────

header "Step 7 — First booking smoke"
TOKEN=$(get_token employee1)
if [[ -n "$TOKEN" ]]; then
  TOMORROW=$(date -v+1d +%Y-%m-%d 2>/dev/null || date -d tomorrow +%Y-%m-%d 2>/dev/null || echo "2099-01-01")
  BOOKING=$(curl -sf -X POST "$GATEWAY/bookings" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"locationId\":\"LOC-MAIN\",\"date\":\"$TOMORROW\",\"reason\":\"onboarding smoke\"}" \
    2>/dev/null || echo "UNREACHABLE")
  if [[ "$BOOKING" != "UNREACHABLE" && "$BOOKING" != "" ]]; then
    STATUS=$(echo "$BOOKING" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "POST /bookings for employee1 (status: $STATUS)"
  else
    fail "POST /bookings failed or unreachable"
  fi

  BOOKINGS=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/bookings" 2>/dev/null || echo "UNREACHABLE")
  COUNT=$(echo "$BOOKINGS" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d) if isinstance(d,list) else d.get('total',0))" 2>/dev/null || echo "0")
  if [[ "$COUNT" -ge 1 ]]; then
    pass "GET /bookings returns $COUNT booking(s)"
  else
    fail "GET /bookings returned 0 bookings after submission"
  fi
else
  fail "Could not obtain employee1 token"
fi

# ── step 8: audit evidence ───────────────────────────────────────────────────

header "Step 8 — Audit evidence"
TOKEN=$(get_token auditor)
if [[ -n "$TOKEN" ]]; then
  AUDIT=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/audit" 2>/dev/null || echo "UNREACHABLE")
  COUNT=$(echo "$AUDIT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d) if isinstance(d,list) else d.get('total',0))" 2>/dev/null || echo "0")
  if [[ "$AUDIT" != "UNREACHABLE" && "$COUNT" -ge 1 ]]; then
    pass "GET /audit returns $COUNT record(s)"
  else
    fail "GET /audit returned 0 records or is unreachable"
  fi
else
  fail "Could not obtain auditor token"
fi

# ── summary ──────────────────────────────────────────────────────────────────

echo
echo "=== Onboarding Smoke Summary ==="
if [[ $FAILURES -eq 0 ]]; then
  echo -e "${GREEN}All checks passed.${NC} SKIP items require manual or evaluation-grade steps — see docs/production/tenant-onboarding-smoke.md."
else
  echo -e "${RED}$FAILURES check(s) failed.${NC} Review output above and consult docs/production/tenant-onboarding-smoke.md for blockers."
  exit 1
fi
