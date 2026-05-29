#!/usr/bin/env bash
# tools/smoke-hosted.sh — Hosted E2E smoke for FairSpot NAS pilot.
#
# Writes a structured evidence file (smoke-evidence-<timestamp>.txt) with all
# tokens redacted.  Exits non-zero if any mandatory check fails.
#
# Usage (public domain):
#   APP_URL=https://app.<domain> AUTH_URL=https://auth.<domain> \
#   OIDC_REALM=fps-pilot ./tools/smoke-hosted.sh
#
# Usage (localhost — TLS/WAF checks become PENDING):
#   APP_URL=http://localhost:10000 AUTH_URL=http://localhost:8080 \
#   OIDC_REALM=fps-local ./tools/smoke-hosted.sh
#
# See docs/production/hosted-smoke-runbook.md for full context and the
# mandatory-checks table.
set -euo pipefail

APP_URL="${APP_URL:-http://localhost:10000}"
AUTH_URL="${AUTH_URL:-http://localhost:8080}"
OIDC_REALM="${OIDC_REALM:-fps-local}"
OIDC_CLIENT_ID="${OIDC_CLIENT_ID:-fps-mobile-dev}"
SMOKE_PASSWORD="${SMOKE_PASSWORD:-Dev1234!}"
SMOKE_EMPLOYEE="${SMOKE_EMPLOYEE:-employee1}"
SMOKE_ADMIN="${SMOKE_ADMIN:-tenant-admin}"
SMOKE_HR_ADMIN="${SMOKE_HR_ADMIN:-hr-admin}"
SMOKE_TENANT="${SMOKE_TENANT:-demo}"
# Vehicle and facility defaults match dev-seed.sh demo data; override if your pilot seed differs
SMOKE_FACILITY_ID="${SMOKE_FACILITY_ID:-00000000-0000-0000-0000-000000000001}"
SMOKE_LOCATION_ID="${SMOKE_LOCATION_ID:-Prague}"
SMOKE_LICENSE_PLATE="${SMOKE_LICENSE_PLATE:-1AA 2345}"
SMOKE_VEHICLE_TYPE="${SMOKE_VEHICLE_TYPE:-Sedan}"

RUN_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EVIDENCE_FILE="smoke-evidence-$(date -u +%Y%m%dT%H%M%SZ).txt"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
CYAN='\033[0;36m'
NC='\033[0m'

PASS_COUNT=0
FAIL_COUNT=0
PENDING_COUNT=0
SKIP_COUNT=0
REQUIRED_FAILURES=0

# localhost mode: Cloudflare-only checks become PENDING rather than FAIL
IS_LOCALHOST=false
if [[ "$APP_URL" == "http://localhost"* || "$APP_URL" == "http://127.0.0.1"* ]]; then
  IS_LOCALHOST=true
fi

# ── evidence file ─────────────────────────────────────────────────────────────

{
  printf '=== FairSpot Hosted Smoke Evidence ===\n'
  printf 'Run at:      %s\n' "$RUN_AT"
  printf 'Environment: %s\n' "$APP_URL"
  printf 'Auth:        %s\n' "$AUTH_URL"
  printf 'Realm:       %s\n' "$OIDC_REALM"
  printf 'Mode:        %s\n' "$( $IS_LOCALHOST && echo "localhost (TLS/WAF checks PENDING)" || echo "public-domain" )"
  printf '\n'
} > "$EVIDENCE_FILE"

_ev() { printf '%s\n' "$1" >> "$EVIDENCE_FILE"; }

# ── output helpers ────────────────────────────────────────────────────────────

pass() {
  echo -e "  ${GREEN}PASS${NC}    $1"
  _ev "[PASS]    $1"
  PASS_COUNT=$((PASS_COUNT+1))
}

fail() {
  local mandatory="${2:-false}"
  echo -e "  ${RED}FAIL${NC}    $1"
  if [[ "$mandatory" == "true" ]]; then
    _ev "[FAIL]    $1  *** MANDATORY ***"
    REQUIRED_FAILURES=$((REQUIRED_FAILURES+1))
  else
    _ev "[FAIL]    $1"
  fi
  FAIL_COUNT=$((FAIL_COUNT+1))
}

pending() {
  echo -e "  ${YELLOW}PENDING${NC} $1"
  _ev "[PENDING] $1"
  PENDING_COUNT=$((PENDING_COUNT+1))
}

skip() {
  echo -e "  ${CYAN}SKIP${NC}    $1"
  _ev "[SKIP]    $1"
  SKIP_COUNT=$((SKIP_COUNT+1))
}

header() {
  echo
  echo "=== $1 ==="
  _ev ""
  _ev "--- $1 ---"
}

# ── auth helpers ──────────────────────────────────────────────────────────────

acquire_token() {
  local user="$1"
  local resp
  resp=$(curl -sf \
    -X POST "$AUTH_URL/realms/$OIDC_REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=$OIDC_CLIENT_ID&username=$user&password=$SMOKE_PASSWORD" \
    2>/dev/null || echo "")
  if [[ -z "$resp" ]]; then
    echo ""; return
  fi
  python3 -c "import sys,json; print(json.load(sys.stdin).get('access_token',''))" <<< "$resp" 2>/dev/null || echo ""
}

json_field() {
  # json_field <json_string> <field>
  python3 -c "import sys,json; print(json.load(sys.stdin).get('$2',''))" <<< "$1" 2>/dev/null || echo ""
}

json_list_len() {
  python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d) if isinstance(d,list) else d.get('total',d.get('count',0)))" <<< "$1" 2>/dev/null || echo "0"
}

http_get() {
  curl -sf -H "Authorization: Bearer $1" "$2" 2>/dev/null || echo ""
}

http_post() {
  curl -sf -X POST \
    -H "Authorization: Bearer $1" \
    -H "Content-Type: application/json" \
    -d "$3" \
    "$2" 2>/dev/null || echo ""
}

http_status() {
  curl -o /dev/null -sw "%{http_code}" "$1" 2>/dev/null || echo "000"
}

# ── service health ─────────────────────────────────────────────────────────────

header "Service health"
ALL_SERVICES_HEALTHY=true
for spec in "5192:Identity" "5131:Booking" "5157:Notification" "5197:Profile" \
            "5161:Audit" "5171:Reporting" "5141:Configuration" "5181:Customer"; do
  IFS=: read -r port svc <<< "$spec"
  if [[ "$IS_LOCALHOST" == "true" ]]; then
    status=$(curl -sf "http://localhost:$port/health" \
      | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null \
      || echo "UNREACHABLE")
    if [[ "$status" == "Healthy" ]]; then
      pass "$svc :$port → $status"
    else
      fail "$svc :$port → $status"
      ALL_SERVICES_HEALTHY=false
    fi
  else
    skip "$svc health (direct port check — verify via Grafana or service logs)"
  fi
done

if [[ "$ALL_SERVICES_HEALTHY" == "false" ]]; then
  echo
  echo "One or more services are not healthy. Resolve service health before running the smoke."
  echo "Check logs: docker compose -f code/infrastructure/docker-compose.yaml logs"
  exit 1
fi

# ── OIDC discovery ─────────────────────────────────────────────────────────────

header "Auth / OIDC discovery  [mandatory #1]"
OIDC_DISC=$(curl -sf "$AUTH_URL/realms/$OIDC_REALM/.well-known/openid-configuration" 2>/dev/null || echo "")
if [[ -n "$OIDC_DISC" && "$OIDC_DISC" != "UNREACHABLE" ]]; then
  ISS=$(json_field "$OIDC_DISC" "issuer")
  pass "OIDC discovery reachable (issuer: $ISS)"
else
  fail "OIDC discovery unreachable at $AUTH_URL/realms/$OIDC_REALM/.well-known/openid-configuration" "true"
fi

# ── TLS / Cloudflare  ─────────────────────────────────────────────────────────

header "TLS / Cloudflare  [mandatory #9]"
if [[ "$IS_LOCALHOST" == "true" ]]; then
  pending "Cloudflare TLS — run against public domain to verify (APP_URL=$APP_URL)"
  pending "WAF active — localhost mode; public-domain WAF not testable here"
else
  if [[ "$APP_URL" == "https://"* ]]; then
    pass "APP_URL uses HTTPS: $APP_URL"
  else
    fail "APP_URL does not use HTTPS — TLS not active" "true"
  fi
  if [[ "$AUTH_URL" == "https://"* ]]; then
    pass "AUTH_URL uses HTTPS: $AUTH_URL"
  else
    fail "AUTH_URL does not use HTTPS — TLS not active" "true"
  fi
fi

# ── Login ─────────────────────────────────────────────────────────────────────

header "Employee login  [mandatory #2]"
EMP_TOKEN=$(acquire_token "$SMOKE_EMPLOYEE")
if [[ -n "$EMP_TOKEN" ]]; then
  pass "Login: $SMOKE_EMPLOYEE → token acquired [REDACTED]"
else
  fail "Login: $SMOKE_EMPLOYEE → no token returned (check OIDC_REALM=$OIDC_REALM and SMOKE_PASSWORD)" "true"
fi

header "Admin login"
ADMIN_TOKEN=$(acquire_token "$SMOKE_ADMIN")
if [[ -n "$ADMIN_TOKEN" ]]; then
  pass "Login: $SMOKE_ADMIN → token acquired [REDACTED]"
else
  fail "Login: $SMOKE_ADMIN → no token returned"
fi

header "HR admin login"
HR_TOKEN=$(acquire_token "$SMOKE_HR_ADMIN")
if [[ -n "$HR_TOKEN" ]]; then
  pass "Login: $SMOKE_HR_ADMIN → token acquired [REDACTED]"
else
  fail "Login: $SMOKE_HR_ADMIN → no token returned"
fi

# ── Tenant context (/me)  ─────────────────────────────────────────────────────

header "Tenant context — /me  [mandatory #3]"
if [[ -n "$EMP_TOKEN" ]]; then
  ME=$(http_get "$EMP_TOKEN" "$APP_URL/me")
  if [[ -n "$ME" ]]; then
    TENANT_ID=$(json_field "$ME" "tenantId")
    USER_ID=$(json_field "$ME" "userId")
    ROLES=$(python3 -c "import sys,json; print(','.join(json.load(sys.stdin).get('roles',[])))" <<< "$ME" 2>/dev/null || echo "")
    if [[ -n "$TENANT_ID" && -n "$USER_ID" && "$ROLES" == *"employee"* ]]; then
      pass "/me → tenantId=$TENANT_ID userId=${USER_ID:0:8}… roles=$ROLES"
    else
      fail "/me response missing expected fields: tenantId=$TENANT_ID userId=$USER_ID roles=$ROLES" "true"
    fi
  else
    fail "GET $APP_URL/me → unreachable or empty" "true"
  fi
else
  skip "/me check — no employee token available"
fi

# ── Profile snapshot ──────────────────────────────────────────────────────────

header "Profile snapshot"
if [[ -n "$EMP_TOKEN" ]]; then
  PROFILE=$(http_get "$EMP_TOKEN" "$APP_URL/profile/snapshot")
  if [[ -n "$PROFILE" ]]; then
    ELIGIBLE=$(json_field "$PROFILE" "parkingEligible")
    pass "GET /profile/snapshot → parkingEligible=$ELIGIBLE"
  else
    fail "GET /profile/snapshot → unreachable or empty (check dev-seed.sh was run)"
  fi
else
  skip "Profile snapshot — no employee token"
fi

# ── Booking request ───────────────────────────────────────────────────────────

header "Booking request  [mandatory #4 #5]"
BOOKING_ID=""
if [[ -n "$EMP_TOKEN" ]]; then
  TOMORROW=$(date -v+1d +%Y-%m-%d 2>/dev/null || date -d tomorrow +%Y-%m-%d 2>/dev/null || echo "2099-01-01")
  BOOKING_RESP=$(http_post "$EMP_TOKEN" "$APP_URL/bookings" \
    "{\"facilityId\":\"$SMOKE_FACILITY_ID\",\"locationId\":\"$SMOKE_LOCATION_ID\",\"licensePlate\":\"$SMOKE_LICENSE_PLATE\",\"vehicleType\":\"$SMOKE_VEHICLE_TYPE\",\"isElectric\":false,\"requiresAccessibleSpot\":false,\"isCompanyCar\":false,\"plannedArrivalTime\":\"${TOMORROW}T09:00:00Z\",\"plannedDepartureTime\":\"${TOMORROW}T17:00:00Z\"}")
  if [[ -n "$BOOKING_RESP" ]]; then
    BOOKING_STATUS=$(json_field "$BOOKING_RESP" "status")
    BOOKING_ID=$(json_field "$BOOKING_RESP" "requestId")
    if [[ -n "$BOOKING_STATUS" && "$BOOKING_STATUS" != "Rejected" ]]; then
      pass "POST /bookings → status=$BOOKING_STATUS requestId=${BOOKING_ID:0:8}…  [mandatory #4]"
    elif [[ "$BOOKING_STATUS" == "Rejected" ]]; then
      fail "POST /bookings → status=Rejected (check SMOKE_LICENSE_PLATE='$SMOKE_LICENSE_PLATE' matches $SMOKE_EMPLOYEE profile; run dev-seed.sh first)" "true"
    else
      fail "POST /bookings → unexpected response (no status field)" "true"
    fi
  else
    fail "POST /bookings → unreachable or error" "true"
  fi

  BOOKINGS=$(http_get "$EMP_TOKEN" "$APP_URL/bookings")
  COUNT=$(json_list_len "$BOOKINGS")
  if [[ "$COUNT" -ge 1 ]]; then
    pass "GET /bookings → $COUNT record(s) visible  [mandatory #5]"
  else
    fail "GET /bookings → 0 records after submit (booking may not have persisted)" "true"
  fi
else
  skip "Booking request — no employee token"
fi

# ── Draw status ───────────────────────────────────────────────────────────────

header "Draw status"
if [[ -n "$ADMIN_TOKEN" ]]; then
  TODAY=$(date -u +%Y-%m-%d)
  DRAW=$(http_get "$ADMIN_TOKEN" "$APP_URL/booking/draw/status?locationId=Prague&date=$TODAY")
  if [[ -n "$DRAW" ]]; then
    DRAW_STATUS=$(json_field "$DRAW" "status")
    pass "GET /booking/draw/status → status=${DRAW_STATUS:-present}"
  else
    skip "GET /booking/draw/status → no response (draw may not exist for today — acceptable)"
  fi
else
  skip "Draw status — no admin token"
fi

# ── Notifications  ────────────────────────────────────────────────────────────

header "Notifications  [mandatory #6]"
if [[ -n "$EMP_TOKEN" ]]; then
  NOTIFS=$(http_get "$EMP_TOKEN" "$APP_URL/notifications")
  if [[ -n "$NOTIFS" ]]; then
    N_COUNT=$(json_list_len "$NOTIFS")
    pass "GET /notifications → $N_COUNT record(s)  [mandatory #6]"
  else
    fail "GET /notifications → unreachable or empty after booking event" "true"
  fi
else
  skip "Notifications — no employee token"
fi

# ── Audit ─────────────────────────────────────────────────────────────────────

header "Audit  [mandatory #7]"
if [[ -n "$ADMIN_TOKEN" ]]; then
  AUDIT=$(http_get "$ADMIN_TOKEN" "$APP_URL/audit")
  if [[ -n "$AUDIT" ]]; then
    A_COUNT=$(json_list_len "$AUDIT")
    if [[ "$A_COUNT" -ge 1 ]]; then
      pass "GET /audit → $A_COUNT record(s) after booking  [mandatory #7]"
    else
      fail "GET /audit → 0 records after booking event" "true"
    fi
  else
    fail "GET /audit → unreachable" "true"
  fi
else
  skip "Audit — no admin token"
fi

# ── Reporting ─────────────────────────────────────────────────────────────────

header "Reporting"
if [[ -n "$ADMIN_TOKEN" ]]; then
  REPORT=$(http_get "$ADMIN_TOKEN" "$APP_URL/reporting/summary")
  if [[ -n "$REPORT" ]]; then
    pass "GET /reporting/summary → accessible to admin"
  else
    fail "GET /reporting/summary → unreachable"
  fi
else
  skip "Reporting — no admin token"
fi

# ── HR operations ─────────────────────────────────────────────────────────────

header "HR operations"
if [[ -n "$HR_TOKEN" ]]; then
  HR_RESP=$(http_get "$HR_TOKEN" "$APP_URL/bookings/hr")
  if [[ -n "$HR_RESP" ]]; then
    pass "GET /bookings/hr → accessible to hr-admin"
  else
    fail "GET /bookings/hr → unreachable"
  fi
else
  skip "HR operations — no hr-admin token"
fi

# ── Tenant readiness (admin)  [mandatory #8] ──────────────────────────────────

header "Tenant readiness  [mandatory #8]"
if [[ -n "$ADMIN_TOKEN" ]]; then
  READINESS=$(http_get "$ADMIN_TOKEN" "$APP_URL/customer/tenants/$SMOKE_TENANT/readiness")
  if [[ -n "$READINESS" ]]; then
    R_STATUS=$(json_field "$READINESS" "status")
    pass "GET /customer/tenants/$SMOKE_TENANT/readiness → status=$R_STATUS  [mandatory #8]"
  else
    fail "Tenant readiness check unreachable" "true"
  fi
else
  skip "Tenant readiness — no admin token"
fi

# ── WAF / path blocking  [mandatory #10] ─────────────────────────────────────

header "WAF — path blocking  [mandatory #10]"
if [[ "$IS_LOCALHOST" == "true" ]]; then
  pending "WAF /metrics block — localhost mode (run against public domain to verify)"
  pending "WAF Keycloak admin block — localhost mode (run against public domain to verify)"
else
  METRICS_STATUS=$(http_status "$APP_URL/metrics")
  if [[ "$METRICS_STATUS" == "403" || "$METRICS_STATUS" == "404" ]]; then
    pass "GET /metrics → HTTP $METRICS_STATUS (blocked from public internet)  [mandatory #10]"
  else
    fail "GET /metrics → HTTP $METRICS_STATUS (expected 403/404 — WAF rule may not be active)" "true"
  fi

  KC_ADMIN_STATUS=$(http_status "$AUTH_URL/admin")
  if [[ "$KC_ADMIN_STATUS" == "403" || "$KC_ADMIN_STATUS" == "404" ]]; then
    pass "GET $AUTH_URL/admin → HTTP $KC_ADMIN_STATUS (Keycloak admin blocked)  [mandatory #10]"
  else
    fail "GET $AUTH_URL/admin → HTTP $KC_ADMIN_STATUS (expected 403/404 — WAF or Cloudflare Access rule needed)" "true"
  fi
fi

# ── evidence file summary ─────────────────────────────────────────────────────

TOTAL=$((PASS_COUNT + FAIL_COUNT + PENDING_COUNT + SKIP_COUNT))
{
  printf '\n'
  printf 'Summary: %d PASS / %d FAIL / %d PENDING / %d SKIP  (%d total)\n' \
    "$PASS_COUNT" "$FAIL_COUNT" "$PENDING_COUNT" "$SKIP_COUNT" "$TOTAL"
  if [[ "$REQUIRED_FAILURES" -gt 0 ]]; then
    printf 'MANDATORY FAILURES: %d — customer access MUST NOT be enabled until resolved.\n' "$REQUIRED_FAILURES"
  fi
  printf '\nNote: tokens and bearer headers are not written to this file.\n'
} >> "$EVIDENCE_FILE"

# ── terminal summary ──────────────────────────────────────────────────────────

echo
echo "=== Smoke Summary ==="
echo "  PASS:    $PASS_COUNT"
echo "  FAIL:    $FAIL_COUNT"
echo "  PENDING: $PENDING_COUNT (public-domain checks — run against https to verify)"
echo "  SKIP:    $SKIP_COUNT"
echo
echo "Evidence written to: $EVIDENCE_FILE"
echo "Attach this file to the PR or release note before enabling customer access."

if [[ "$REQUIRED_FAILURES" -gt 0 ]]; then
  echo
  echo -e "${RED}$REQUIRED_FAILURES mandatory check(s) FAILED.${NC}"
  echo "Customer access must not be enabled until all mandatory checks pass."
  echo "See docs/production/hosted-smoke-runbook.md for the mandatory-checks table."
  exit 1
fi

if [[ "$FAIL_COUNT" -gt 0 ]]; then
  echo
  echo -e "${YELLOW}$FAIL_COUNT non-mandatory check(s) failed.${NC} Review output above."
  exit 1
fi

echo
if $IS_LOCALHOST; then
  echo -e "${YELLOW}Localhost mode:${NC} all checks passed. Re-run against the public domain to resolve PENDING items."
else
  echo -e "${GREEN}All checks passed.${NC}"
fi
