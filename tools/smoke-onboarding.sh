#!/usr/bin/env bash
# Tenant onboarding E2E smoke — synthetic acme-corp path.
# Each step prints PASS / FAIL / SKIP / DEFERRED and notes current implementation status.
# Run after: ./tools/start-local-harness.sh && ./tools/dev-seed.sh
#
# Usage: ./tools/smoke-onboarding.sh
set -euo pipefail

GATEWAY="${GATEWAY_URL:-http://localhost:10000}"
CUSTOMER_SVC="${CUSTOMER_URL:-http://localhost:5181}"
GL_TENANT="${FPS_GL_TENANT_ID:-greenlogistics}"
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m'

pass()          { echo -e "  ${GREEN}PASS${NC}     $1"; }
fail()          { echo -e "  ${RED}FAIL${NC}     $1"; FAILURES=$((FAILURES+1)); }
skip()          { echo -e "  ${YELLOW}SKIP${NC}     $1 (evaluation-grade or manual — see docs/production/tenant-onboarding-smoke.md)"; }
deferred()      { echo -e "  ${YELLOW}DEFERRED${NC} $1 (pilot limitation — non-blocking; resolve before production)"; DEFERRED_COUNT=$((DEFERRED_COUNT+1)); }
deferred_note() { echo -e "  ${YELLOW}DEFERRED${NC} $1 (reported by readiness; already counted above)"; }
header()        { echo; echo "=== $1 ==="; }

FAILURES=0
DEFERRED_COUNT=0

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
TOKEN=$(get_token gl-tenant-admin)
if [[ -z "$TOKEN" ]]; then
  fail "Could not obtain gl-tenant-admin token"
else
  TENANT_JSON=$(curl -sf -H "Authorization: Bearer $TOKEN" "$CUSTOMER_SVC/tenants/$GL_TENANT" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$TENANT_JSON" != "UNREACHABLE" && "$TENANT_JSON" != "" ]]; then
    TENANT_ID=$(echo "$TENANT_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin).get('tenantId','MISSING'))" 2>/dev/null || echo "MISSING")
    LC_STATE=$(echo "$TENANT_JSON"  | python3 -c "import sys,json; print(json.load(sys.stdin).get('lifecycleState','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    if [[ "$TENANT_ID" == "$GL_TENANT" ]]; then
      pass "GET /tenants/$GL_TENANT: tenantId=$TENANT_ID lifecycleState=$LC_STATE"
    else
      fail "GET /tenants/$GL_TENANT: unexpected tenantId=$TENANT_ID"
    fi
  else
    fail "GET /tenants/$GL_TENANT unreachable or missing"
  fi
fi
skip "Tenant workspace created via POST /tenants (API implemented; no web form yet)"

# ── step 2: identity and role mapping ───────────────────────────────────────

header "Step 2 — Identity and role mapping"
TOKEN=$(get_token gl-employee1)
if [[ -n "$TOKEN" ]]; then
  ME=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/me" 2>/dev/null || echo "UNREACHABLE")
  ME_TENANT=$(echo "$ME" | python3 -c "import sys,json; print(json.load(sys.stdin).get('tenantId','MISSING'))" 2>/dev/null || echo "PARSE_FAIL")
  ROLES=$(echo "$ME"    | python3 -c "import sys,json; print(','.join(json.load(sys.stdin).get('roles',[])))" 2>/dev/null || echo "PARSE_FAIL")
  if [[ "$ME_TENANT" == "$GL_TENANT" && "$ROLES" == *"employee"* ]]; then
    pass "GET /me for gl-employee1: tenantId=$ME_TENANT roles=$ROLES"
  else
    fail "GET /me for gl-employee1: unexpected tenantId=$ME_TENANT or roles=$ROLES"
  fi
else
  fail "Could not obtain gl-employee1 token"
fi
skip "Per-tenant OIDC client and group mapping UI — manual Keycloak configuration"

# ── step 3: first administrator ──────────────────────────────────────────────

header "Step 3 — First administrator"
TOKEN=$(get_token gl-tenant-admin)
if [[ -n "$TOKEN" ]]; then
  ME=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/me" 2>/dev/null || echo "UNREACHABLE")
  ROLES=$(echo "$ME" | python3 -c "import sys,json; print(','.join(json.load(sys.stdin).get('roles',[])))" 2>/dev/null || echo "PARSE_FAIL")
  if [[ "$ROLES" == *"admin"* ]]; then
    pass "gl-tenant-admin has admin role: $ROLES"
  else
    fail "gl-tenant-admin missing admin role: $ROLES"
  fi
else
  fail "Could not obtain gl-tenant-admin token"
fi
skip "First-admin provisioning API (CUST004) — using Keycloak-pre-configured user"

# ── step 4: parking bootstrap ────────────────────────────────────────────────

header "Step 4 — Parking bootstrap (location, policy, slots)"
TOKEN=$(get_token gl-tenant-admin)
if [[ -n "$TOKEN" ]]; then
  POLICY=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/configuration/parking-policy" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$POLICY" != "UNREACHABLE" && "$POLICY" != "" ]]; then
    SLOTS=$(echo "$POLICY" | python3 -c "import sys,json; print(json.load(sys.stdin).get('slotCount','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "Configuration policy reachable (slotCount: $SLOTS)"
  else
    fail "Configuration policy unreachable"
  fi
else
  fail "Could not obtain gl-tenant-admin token"
fi
skip "Tenant admin web UI for location/slot setup — using seeded Configuration data"

# ── step 5: tenant object storage and branding ───────────────────────────────

header "Step 5 — Tenant object storage and branding"
deferred "Object storage provisioning (OPS008C) — document uploads, report exports, audit exports, and branding uploads are not available"
deferred "Organization branding (CUST010) — FairSpot defaults used; no custom logo or color tokens"

# ── step 6: employee and profile bootstrap ───────────────────────────────────

header "Step 6 — Employee and profile bootstrap"
TOKEN=$(get_token gl-employee1)
if [[ -n "$TOKEN" ]]; then
  PROFILE=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/profile/snapshot" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$PROFILE" != "UNREACHABLE" && "$PROFILE" != "" ]]; then
    ELIGIBLE=$(echo "$PROFILE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('parkingEligible','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "GET /profile/snapshot for gl-employee1 (parkingEligible: $ELIGIBLE)"
  else
    fail "GET /profile/snapshot unreachable for gl-employee1 — run ./tools/dev-seed.sh first"
  fi
else
  fail "Could not obtain gl-employee1 token"
fi

if ./tools/validate-hr-import.sh tools/templates/demo-employees.csv tools/templates/demo-vehicles.csv > /dev/null 2>&1; then
  pass "HR import templates validate cleanly"
else
  fail "HR import template validation errors — run ./tools/validate-hr-import.sh for details"
fi
skip "Web HR import upload (DATA002) — using dev-seed.sh and validate-hr-import.sh"

# ── step 7: readiness check ──────────────────────────────────────────────────

header "Step 7 — Readiness check"
TOKEN=$(get_token gl-tenant-admin)
if [[ -n "$TOKEN" ]]; then
  READINESS=$(curl -sf -H "Authorization: Bearer $TOKEN" "$CUSTOMER_SVC/tenants/$GL_TENANT/readiness" 2>/dev/null || echo "UNREACHABLE")
  if [[ "$READINESS" != "UNREACHABLE" && "$READINESS" != "" ]]; then
    IS_READY=$(echo "$READINESS" | python3 -c "import sys,json; print(json.load(sys.stdin).get('isReady','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    FAILED_CHECKS=$(echo "$READINESS" | python3 -c "
import sys,json
d=json.load(sys.stdin)
names=[c['name'] for c in d.get('checks',[]) if c['status']=='Failed']
print(','.join(names) if names else 'none')
" 2>/dev/null || echo "UNKNOWN")
    DEFERRED_CHECKS=$(echo "$READINESS" | python3 -c "
import sys,json
d=json.load(sys.stdin)
names=[c['name'] for c in d.get('checks',[]) if c['status']=='Deferred']
print(','.join(names) if names else 'none')
" 2>/dev/null || echo "UNKNOWN")
    if [[ "$IS_READY" == "True" ]]; then
      pass "Readiness check: isReady=True (failed: $FAILED_CHECKS)"
    else
      fail "Readiness check: isReady=$IS_READY (failed: $FAILED_CHECKS)"
    fi
    if [[ "$DEFERRED_CHECKS" != "none" && "$DEFERRED_CHECKS" != "UNKNOWN" ]]; then
      deferred_note "Pilot-deferred checks reported by readiness: $DEFERRED_CHECKS"
    fi
  else
    fail "Readiness check endpoint unreachable"
  fi
else
  fail "Could not obtain gl-tenant-admin token"
fi

# ── step 8: first booking smoke ──────────────────────────────────────────────

header "Step 8 — First booking smoke"
TOKEN=$(get_token gl-employee1)
if [[ -n "$TOKEN" ]]; then
  # Next workday at least +2 days out, to stay clear of the draw cutoff.
  BOOK_DATE=$(python3 - << 'PYEOF'
from datetime import date, timedelta
d = date.today() + timedelta(days=2)
while d.weekday() >= 5:
    d += timedelta(days=1)
print(d.isoformat())
PYEOF
)
  BOOKING=$(curl -sf -X POST "$GATEWAY/bookings" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"facilityId\": \"00000000-0000-0000-0000-000000000002\",
      \"locationId\": \"GL-HQ\",
      \"licensePlate\": \"1AB 2345\",
      \"vehicleType\": \"Sedan\",
      \"isElectric\": false,
      \"requiresAccessibleSpot\": false,
      \"isCompanyCar\": true,
      \"plannedArrivalTime\": \"${BOOK_DATE}T08:00:00\",
      \"plannedDepartureTime\": \"${BOOK_DATE}T18:00:00\"
    }" \
    2>/dev/null || echo "UNREACHABLE")
  if [[ "$BOOKING" != "UNREACHABLE" && "$BOOKING" != "" ]]; then
    B_STATUS=$(echo "$BOOKING" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
    pass "POST /bookings for gl-employee1 (status: $B_STATUS)"
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
  fail "Could not obtain gl-employee1 token"
fi

# ── step 9: audit evidence ───────────────────────────────────────────────────

header "Step 9 — Audit evidence"
TOKEN=$(get_token gl-auditor)
if [[ -n "$TOKEN" ]]; then
  AUDIT=$(curl -sf -H "Authorization: Bearer $TOKEN" "$GATEWAY/audit" 2>/dev/null || echo "UNREACHABLE")
  COUNT=$(echo "$AUDIT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d) if isinstance(d,list) else d.get('total',0))" 2>/dev/null || echo "0")
  if [[ "$AUDIT" != "UNREACHABLE" && "$COUNT" -ge 1 ]]; then
    pass "GET /audit returns $COUNT record(s)"
  else
    fail "GET /audit returned 0 records or is unreachable"
  fi
else
  fail "Could not obtain gl-auditor token"
fi

# ── summary ──────────────────────────────────────────────────────────────────

echo
echo "=== Onboarding Smoke Summary ==="
if [[ $FAILURES -eq 0 ]]; then
  echo -e "${GREEN}All automated checks passed.${NC}"
  if [[ $DEFERRED_COUNT -gt 0 ]]; then
    echo -e "${YELLOW}$DEFERRED_COUNT pilot-deferred item(s) reported.${NC} These are non-blocking for the pilot but must be resolved before production."
  fi
  echo "SKIP items require manual or evaluation-grade steps — see docs/production/tenant-onboarding-smoke.md."
  echo "Full classification: docs/production/cust008-onboarding-e2e-evidence.md"
else
  echo -e "${RED}$FAILURES check(s) failed.${NC} Review output above and consult docs/production/tenant-onboarding-smoke.md for blockers."
  exit 1
fi
