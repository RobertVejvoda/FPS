#!/usr/bin/env bash
# tools/smoke-hosted.sh — Hosted E2E smoke for FairSpot NAS pilot.
#
# Writes a structured evidence file (smoke-evidence-<timestamp>.txt) with all
# tokens redacted.  Exits non-zero if any mandatory check fails.
#
# Usage (public domain — single-origin: the API is proxied at app.<domain>/api):
#   APP_URL=https://app.<domain>/api AUTH_URL=https://auth.<domain> \
#   OIDC_REALM=fairspot-pilot ./tools/smoke-hosted.sh
#
# Usage (localhost — talks to the Envoy gateway directly, API served at root, so
# no /api; TLS/WAF checks become PENDING):
#   APP_URL=http://localhost:10000 AUTH_URL=http://localhost:8180 \
#   OIDC_REALM=fps-local ./tools/smoke-hosted.sh
#
# A root public APP_URL is auto-normalized to its /api base (see below), so the
# API probes never accidentally hit the SPA root.
# Note: local Keycloak Docker container maps internal :8080 to host :8180.
#
# See docs/production/hosted-smoke-runbook.md for public readiness context.
# The detailed mandatory-checks table is operator material in the private
# fairspot-platform docs/runbooks/hosted-smoke-runbook.md runbook.
set -euo pipefail

APP_URL="${APP_URL:-http://localhost:10000}"
AUTH_URL="${AUTH_URL:-http://localhost:8180}"
OIDC_REALM="${OIDC_REALM:-fps-local}"
OIDC_CLIENT_ID="${OIDC_CLIENT_ID:-fps-mobile-dev}"
SMOKE_PASSWORD="${SMOKE_PASSWORD:-Dev1234!}"
SMOKE_EMPLOYEE="${SMOKE_EMPLOYEE:-gl-employee1}"
SMOKE_ADMIN="${SMOKE_ADMIN:-gl-tenant-admin}"
SMOKE_HR_ADMIN="${SMOKE_HR_ADMIN:-gl-hr-admin}"
SMOKE_TENANT="${SMOKE_TENANT:-greenlogistics}"
# Vehicle and facility defaults match dev-seed.sh Green Logistics data; override if your pilot seed differs
SMOKE_FACILITY_ID="${SMOKE_FACILITY_ID:-00000000-0000-0000-0000-000000000002}"
SMOKE_LOCATION_ID="${SMOKE_LOCATION_ID:-GL-HQ}"
SMOKE_LICENSE_PLATE="${SMOKE_LICENSE_PLATE:-1AB 2345}"
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
DEFERRED_COUNT=0
REQUIRED_FAILURES=0

# localhost mode: Cloudflare-only checks become PENDING rather than FAIL
IS_LOCALHOST=false
if [[ "$APP_URL" == "http://localhost"* || "$APP_URL" == "http://127.0.0.1"* ]]; then
  IS_LOCALHOST=true
fi

# Single-origin public model: the API is proxied at app.<domain>/api, so the
# public APP_URL must target the /api base. Drop any trailing slash, then add
# /api if missing, so the API probes ($APP_URL/me, /bookings, /openapi/v1.json,
# ...) hit the gateway and not the SPA root — which would return 200 for every
# path and record misleading evidence. Localhost talks to the Envoy gateway
# directly (API served at root), so it is left unchanged.
APP_URL="${APP_URL%/}"
if [[ "$IS_LOCALHOST" == "false" && "$APP_URL" != */api ]]; then
  APP_URL="$APP_URL/api"
  echo "Note: using public API base APP_URL=$APP_URL (single-origin /api)."
fi

# Bare public origin, for root-path checks that must hit app.<domain> directly
# (not the /api base): the WAF /metrics block and the HTTP→HTTPS redirect. For
# localhost (no /api) this equals APP_URL.
APP_ORIGIN="${APP_URL%/api}"

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

deferred_note() {
  echo -e "  ${YELLOW}DEFERRED${NC} $1 (pilot limitation; already counted in onboarding evidence)"
  _ev "[DEFERRED] $1"
  DEFERRED_COUNT=$((DEFERRED_COUNT+1))
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
  python3 -c "
import sys,json
d=json.load(sys.stdin)
if isinstance(d,list):
    print(len(d))
else:
    # FPS services use different pagination shapes: totalCount, totalReturned, total, count
    # Fall back to len(items) if no count field is present
    items=d.get('items',[])
    print(d.get('totalCount',d.get('totalReturned',d.get('total',d.get('count',len(items))))))
" <<< "$1" 2>/dev/null || echo "0"
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

  # "Always Use HTTPS": the plain-HTTP app origin must redirect to https, not
  # serve content. Probe the bare origin (not the /api base), swap the scheme.
  HTTP_APP="${APP_ORIGIN/https:/http:}"
  HTTP_STATUS=$(http_status "$HTTP_APP/")
  if [[ "$HTTP_STATUS" == "301" || "$HTTP_STATUS" == "302" || "$HTTP_STATUS" == "308" ]]; then
    pass "GET $HTTP_APP/ → HTTP $HTTP_STATUS (redirects to HTTPS)  [mandatory #9]"
  elif [[ "$HTTP_STATUS" == "000" || -z "$HTTP_STATUS" ]]; then
    pass "GET $HTTP_APP/ → no plain-HTTP response (HTTP not served)  [mandatory #9]"
  elif [[ "$HTTP_STATUS" == "200" ]]; then
    fail "GET $HTTP_APP/ → HTTP 200 over plain HTTP — enable Cloudflare 'Always Use HTTPS'" "true"
  else
    pending "GET $HTTP_APP/ → HTTP $HTTP_STATUS (confirm 'Always Use HTTPS' on the live domain)"
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
  # Use +3 days to avoid CutOffPassed rejection for same/next-day slots when run in the evening
  SMOKE_DATE=$(date -v+3d +%Y-%m-%d 2>/dev/null || date -d "+3 days" +%Y-%m-%d 2>/dev/null || echo "2099-01-01")
  BOOKING_RESP=$(http_post "$EMP_TOKEN" "$APP_URL/bookings" \
    "{\"facilityId\":\"$SMOKE_FACILITY_ID\",\"locationId\":\"$SMOKE_LOCATION_ID\",\"licensePlate\":\"$SMOKE_LICENSE_PLATE\",\"vehicleType\":\"$SMOKE_VEHICLE_TYPE\",\"isElectric\":false,\"requiresAccessibleSpot\":false,\"isCompanyCar\":false,\"plannedArrivalTime\":\"${SMOKE_DATE}T09:00:00Z\",\"plannedDepartureTime\":\"${SMOKE_DATE}T17:00:00Z\"}")
  if [[ -n "$BOOKING_RESP" ]]; then
    BOOKING_STATUS=$(json_field "$BOOKING_RESP" "status")
    BOOKING_ID=$(json_field "$BOOKING_RESP" "requestId")
    if [[ -n "$BOOKING_STATUS" && "$BOOKING_STATUS" != "Rejected" ]]; then
      pass "POST /bookings → status=$BOOKING_STATUS requestId=${BOOKING_ID:0:8}…  [mandatory #4]"
    elif [[ "$BOOKING_STATUS" == "Rejected" ]]; then
      fail "POST /bookings → status=Rejected (rejectionCode=$(python3 -c \"import sys,json; print(json.load(sys.stdin).get('rejectionCode','?'))\" <<< \"$BOOKING_RESP\" 2>/dev/null); check $SMOKE_EMPLOYEE profile and run dev-seed.sh)" "true"
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
  DRAW=$(http_get "$ADMIN_TOKEN" \
    "$APP_URL/draws/$TODAY/status?locationId=$SMOKE_LOCATION_ID&timeSlotStart=${TODAY}T08:00:00Z&timeSlotEnd=${TODAY}T18:00:00Z")
  if [[ -n "$DRAW" ]]; then
    DRAW_STATUS=$(json_field "$DRAW" "status")
    pass "GET /draws/$TODAY/status → status=${DRAW_STATUS:-present}"
  else
    skip "GET /draws/$TODAY/status → no response (draw may not exist for today — acceptable)"
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
    if [[ "$N_COUNT" -ge 1 ]]; then
      pass "GET /notifications → $N_COUNT record(s) — Booking event reached Notification  [mandatory #6]"
    else
      fail "GET /notifications → 0 records (Booking event did not reach Notification service; check Dapr pub/sub and run dev-seed.sh)" "true"
    fi
  else
    fail "GET /notifications → unreachable after booking event" "true"
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
  REPORT=$(http_get "$ADMIN_TOKEN" "$APP_URL/reports/parking/summary")
  if [[ -n "$REPORT" ]]; then
    pass "GET /reports/parking/summary → accessible to admin"
  else
    fail "GET /reports/parking/summary → unreachable"
  fi
else
  skip "Reporting — no admin token"
fi

# ── HR operations ─────────────────────────────────────────────────────────────

header "HR operations — privileged role reaches a non-Customer service (PLAT001)"
if [[ -n "$HR_TOKEN" ]]; then
  # hr_manager hits Booking (a non-Customer service that uses the shared role mapper). A 403
  # here means the FairSpot realm's privileged roles were stripped — i.e. Auth:TrustedRealmRoles
  # is not wired in this deployment. This is the explicit assertion that the seeded allowlist
  # reaches non-Customer services.
  HR_STATUS=$(curl -o /dev/null -sw "%{http_code}" \
    -H "Authorization: Bearer $HR_TOKEN" "$APP_URL/bookings" 2>/dev/null || echo "000")
  if [[ "$HR_STATUS" == "403" ]]; then
    fail "GET /bookings (hr_manager) → 403 — privileged role stripped; set Auth:TrustedRealmRoles in this profile" "true"
  elif [[ "$HR_STATUS" == "200" ]]; then
    pass "GET /bookings (hr_manager) → 200 — hr_manager reaches Booking (TrustedRealmRoles active)"
  else
    pending "GET /bookings (hr_manager) → HTTP $HR_STATUS (confirm against the live domain)"
  fi
else
  skip "HR operations — no hr-admin token"
fi

# ── Tenant readiness (admin)  [mandatory #8] ──────────────────────────────────

header "Tenant readiness  [mandatory #8]"
if [[ -n "$ADMIN_TOKEN" ]]; then
  READINESS=$(http_get "$ADMIN_TOKEN" "$APP_URL/tenants/$SMOKE_TENANT/readiness")
  if [[ -n "$READINESS" ]]; then
    IS_READY=$(python3 -c "import sys,json; print(json.load(sys.stdin).get('isReady','UNKNOWN'))" <<< "$READINESS" 2>/dev/null || echo "UNKNOWN")
    FAILED_CHECKS=$(python3 -c "
import sys,json
d=json.load(sys.stdin)
names=[c['name'] for c in d.get('checks',[]) if c['status']=='Failed']
print(','.join(names) if names else 'none')
" <<< "$READINESS" 2>/dev/null || echo "UNKNOWN")
    DEFERRED_CHECKS=$(python3 -c "
import sys,json
d=json.load(sys.stdin)
names=[c['name'] for c in d.get('checks',[]) if c['status']=='Deferred']
print(','.join(names) if names else 'none')
" <<< "$READINESS" 2>/dev/null || echo "UNKNOWN")
    if [[ "$IS_READY" == "True" ]]; then
      pass "GET /tenants/$SMOKE_TENANT/readiness → isReady=True (failed: $FAILED_CHECKS)  [mandatory #8]"
    else
      fail "GET /tenants/$SMOKE_TENANT/readiness → isReady=$IS_READY (failed: $FAILED_CHECKS)" "true"
    fi
    if [[ "$DEFERRED_CHECKS" != "none" && "$DEFERRED_CHECKS" != "UNKNOWN" ]]; then
      deferred_note "Pilot-deferred readiness checks: $DEFERRED_CHECKS (see docs/production/cust008-onboarding-e2e-evidence.md)"
    fi
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
  # Root-origin path: the WAF/SEC010 contract blocks https://app.<domain>/metrics
  # (not /api/metrics), so probe the bare origin, not the API base.
  METRICS_STATUS=$(http_status "$APP_ORIGIN/metrics")
  if [[ "$METRICS_STATUS" == "403" || "$METRICS_STATUS" == "404" ]]; then
    pass "GET $APP_ORIGIN/metrics → HTTP $METRICS_STATUS (blocked from public internet)  [mandatory #10]"
  else
    fail "GET $APP_ORIGIN/metrics → HTTP $METRICS_STATUS (expected 403/404 — WAF rule may not be active)" "true"
  fi

  KC_ADMIN_STATUS=$(http_status "$AUTH_URL/admin")
  if [[ "$KC_ADMIN_STATUS" == "403" || "$KC_ADMIN_STATUS" == "404" ]]; then
    pass "GET $AUTH_URL/admin → HTTP $KC_ADMIN_STATUS (Keycloak admin blocked)  [mandatory #10]"
  else
    fail "GET $AUTH_URL/admin → HTTP $KC_ADMIN_STATUS (expected 403/404 — WAF or Cloudflare Access rule needed)" "true"
  fi

  # Additional internal/diagnostic surfaces that must not be publicly served via
  # the API. NOTE: in the single-origin model the SPA history-fallback returns
  # 200 for any unknown path at the app *root* by design (static SPA, no
  # sensitive data); the meaningful checks target the /api/* surfaces proxied to
  # the gateway, so APP_URL must include the /api prefix.
  # Diagnostic/observability surfaces and Dapr sidecar control-plane paths must not be
  # served through the public API. v1.0/invoke is the Dapr service-invocation entrypoint;
  # metrics is the Prometheus scrape endpoint.
  for ipath in "openapi/v1.json" "swagger" "swagger/index.html" "metrics" \
               "v1.0/healthz" "v1.0/metadata" "v1.0/invoke/fairspot-booking/method/health"; do
    ISTATUS=$(http_status "$APP_URL/$ipath")
    if [[ "$ISTATUS" == "401" || "$ISTATUS" == "403" || "$ISTATUS" == "404" ]]; then
      pass "GET /api/$ipath → HTTP $ISTATUS (internal surface not publicly served)  [mandatory #10]"
    elif [[ "$ISTATUS" == "200" ]]; then
      fail "GET /api/$ipath → HTTP 200 (internal surface exposed through the public API)" "true"
    else
      pending "GET /api/$ipath → HTTP $ISTATUS (confirm against the live domain/WAF)"
    fi
  done
fi

# ── Internal infrastructure not publicly exposed  [mandatory #10] ─────────────

header "Internal infrastructure exposure  [mandatory #10]"
if [[ "$IS_LOCALHOST" == "true" ]]; then
  pending "Infra hostnames (observability/stores/broker/object-storage) — run against the public domain, then operator-confirm in Cloudflare"
else
  # Only app.<domain> (SPA + /api) and auth.<domain> are meant to be tunneled publicly.
  # Stores, broker, object storage, and observability must never be reachable from the
  # internet. Probe the conventional infra subdomains: a served tool (HTTP 200) means an
  # accidental public tunnel; non-resolving/blocked hosts return non-200 (often 000) and
  # pass. The final word is operator confirmation of the Cloudflare tunnel ingress.
  BASE_DOMAIN="${APP_ORIGIN#https://}"; BASE_DOMAIN="${BASE_DOMAIN#http://}"; BASE_DOMAIN="${BASE_DOMAIN#app.}"
  for host in grafana prometheus jaeger minio mongo-express; do
    ISTATUS=$(http_status "https://$host.$BASE_DOMAIN/")
    if [[ "$ISTATUS" == "200" ]]; then
      fail "GET https://$host.$BASE_DOMAIN/ → HTTP 200 (internal infrastructure publicly reachable — remove its tunnel ingress)" "true"
    else
      pass "https://$host.$BASE_DOMAIN/ → HTTP $ISTATUS (not publicly served)  [mandatory #10]"
    fi
  done
  pending "Operator-confirm: only app.<domain> and auth.<domain> are tunneled; stores/broker/object-storage/observability stay LAN-only or behind Cloudflare Access"
fi

# ── evidence file summary ─────────────────────────────────────────────────────

TOTAL=$((PASS_COUNT + FAIL_COUNT + PENDING_COUNT + SKIP_COUNT))
{
  printf '\n'
  printf 'Summary: %d PASS / %d FAIL / %d PENDING / %d SKIP  (%d total)\n' \
    "$PASS_COUNT" "$FAIL_COUNT" "$PENDING_COUNT" "$SKIP_COUNT" "$TOTAL"
  if [[ "$DEFERRED_COUNT" -gt 0 ]]; then
    printf 'DEFERRED: %d pilot limitation(s) — non-blocking; resolve before production.\n' "$DEFERRED_COUNT"
    printf '  See docs/production/cust008-onboarding-e2e-evidence.md for deferred item details.\n'
  fi
  if [[ "$REQUIRED_FAILURES" -gt 0 ]]; then
    printf 'MANDATORY FAILURES: %d — customer access MUST NOT be enabled until resolved.\n' "$REQUIRED_FAILURES"
  fi
  printf '\nNote: tokens and bearer headers are not written to this file.\n'
} >> "$EVIDENCE_FILE"

# ── terminal summary ──────────────────────────────────────────────────────────

echo
echo "=== Smoke Summary ==="
echo "  PASS:     $PASS_COUNT"
echo "  FAIL:     $FAIL_COUNT"
echo "  PENDING:  $PENDING_COUNT (public-domain checks — run against https to verify)"
echo "  SKIP:     $SKIP_COUNT"
echo "  DEFERRED: $DEFERRED_COUNT (pilot limitations — non-blocking)"
echo
echo "Evidence written to: $EVIDENCE_FILE"
echo "Attach this file to the PR or release note before enabling customer access."

if [[ "$REQUIRED_FAILURES" -gt 0 ]]; then
  echo
  echo -e "${RED}$REQUIRED_FAILURES mandatory check(s) FAILED.${NC}"
  echo "Customer access must not be enabled until all mandatory checks pass."
  echo "See private fairspot-platform docs/runbooks/hosted-smoke-runbook.md for the mandatory-checks table."
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
