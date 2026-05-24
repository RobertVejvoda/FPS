#!/usr/bin/env bash
# provision-tenant.sh — Idempotent tenant provisioning from a definition file.
#
# Usage:
#   ./tools/provision-tenant.sh tools/templates/tenants/demo.json
#   ./tools/provision-tenant.sh tools/templates/tenants/acme-corp.json
#   ADMIN_USER=tenant-admin ./tools/provision-tenant.sh ...
#
# Prerequisites:
#   - Local harness running: ./tools/start-local-harness.sh
#   - Keycloak configured:   ./tools/dev-setup-auth.sh
#   - ADMIN_USER must have the admin role in their JWT.
#
# What this script provisions (idempotent):
#   1. Tenant workspace (Customer service) — create if absent, skip if present.
#   2. Identity config (Customer service) — always applies (PUT is idempotent).
#   3. Lifecycle transition to Configured (Customer service) — skipped if already beyond.
#   4. Parking policy + slots (Configuration service) — applied only when the
#      ADMIN_USER token belongs to the target tenant (same tenantId in JWT).
#      For cross-tenant provisioning, Configuration is a documented gap — see notes.
#   5. Readiness check + evidence summary.
#
# Secrets: no secrets in definition files. Dev password from FPS_DEV_PASSWORD (env var).
# No real PII is introduced by this script.

set -euo pipefail

DEFINITION_FILE="${1:-}"
if [ -z "$DEFINITION_FILE" ]; then
  echo "Usage: $0 DEFINITION_FILE"
  echo "  e.g. $0 tools/templates/tenants/demo.json"
  exit 1
fi

if [ ! -f "$DEFINITION_FILE" ]; then
  echo "ERROR: Definition file not found: $DEFINITION_FILE"
  exit 1
fi

CUSTOMER_URL="${CUSTOMER_URL:-http://localhost:5181}"
CONFIG_URL="${CONFIG_URL:-http://localhost:5141}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
ADMIN_USER="${ADMIN_USER:-tenant-admin}"
ADMIN_PASS="${FPS_DEV_PASSWORD:-Dev1234!}"

GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m'
ok()   { echo -e "  ${GREEN}OK${NC}    $1"; }
skip() { echo -e "  ${YELLOW}SKIP${NC}  $1"; }
warn() { echo -e "  ${YELLOW}WARN${NC}  $1"; }
err()  { echo -e "  ${RED}ERR${NC}   $1"; ERRORS=$((ERRORS+1)); }

ERRORS=0

# ── Parse definition ──────────────────────────────────────────────────────────

eval "$(python3 - "$DEFINITION_FILE" << 'PYEOF'
import json, sys, os

with open(sys.argv[1]) as f:
    d = json.load(f)

def sh(v):
    return "'" + str(v).replace("'", "'\\''") + "'"

print(f"TENANT_ID={sh(d['tenantId'])}")
print(f"TENANT_SLUG={sh(d.get('slug', d['tenantId']))}")
print(f"TENANT_DISPLAY={sh(d['displayName'])}")
print(f"TENANT_REGION={sh(d['region'])}")
print(f"TENANT_TZ={sh(d['timezone'])}")

contacts = d.get('supportContacts', [])
contacts_json = json.dumps(contacts)
print(f"TENANT_CONTACTS_JSON={sh(contacts_json)}")

identity = d.get('identity', {})
print(f"IDENTITY_ISSUER={sh(identity.get('trustedIssuer',''))}")
print(f"IDENTITY_AUDIENCE={sh(identity.get('audience','fps-api'))}")
print(f"IDENTITY_TENANT_CLAIM={sh(identity.get('tenantClaimName','tenant_id'))}")
print(f"IDENTITY_SUBJECT_CLAIM={sh(identity.get('subjectClaimName','sub'))}")
role_claims_json = json.dumps(identity.get('roleClaimNames', ['roles']))
print(f"IDENTITY_ROLE_CLAIMS_JSON={sh(role_claims_json)}")
role_mapping_json = json.dumps(identity.get('roleMapping', {}))
print(f"IDENTITY_ROLE_MAPPING_JSON={sh(role_mapping_json)}")

policy = d.get('parkingPolicy', {})
print(f"POLICY_CAP={sh(policy.get('dailyRequestCap',100))}")
print(f"POLICY_CUTOFF_H={sh(policy.get('drawCutOffHour',18))}")
print(f"POLICY_CUTOFF_M={sh(policy.get('drawCutOffMinute',0))}")
print(f"POLICY_LOOKBACK={sh(policy.get('allocationLookbackDays',10))}")
print(f"POLICY_LATE_PENALTY={sh(policy.get('lateCancellationPenalty',1))}")
print(f"POLICY_NOSHOW_PENALTY={sh(policy.get('noShowPenalty',2))}")
print(f"POLICY_SAMEDAY={sh(str(policy.get('sameDayBookingEnabled',True)).lower())}")
print(f"POLICY_COMPANY_CAR={sh(str(policy.get('companyCarTier1Enabled',True)).lower())}")
print(f"POLICY_OVERFLOW={sh(policy.get('companyCarOverflowBehavior','reject'))}")
print(f"POLICY_REALLOC={sh(str(policy.get('automaticReallocationEnabled',True)).lower())}")

locations_json = json.dumps(d.get('locations', []))
print(f"LOCATIONS_JSON={sh(locations_json)}")

smoke_users = d.get('smokeUsers', [])
print(f"SMOKE_USERS={sh(' '.join(smoke_users))}")
PYEOF
)"

echo "== Provision tenant: $TENANT_ID =="
echo "   Display: $TENANT_DISPLAY"
echo "   Region:  $TENANT_REGION / $TENANT_TZ"
echo "   Admin:   $ADMIN_USER"
echo ""

# ── Helpers ───────────────────────────────────────────────────────────────────

get_token() {
  local user="$1"
  curl -sf \
    -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password&client_id=$CLIENT_ID&username=$user&password=$ADMIN_PASS" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])" 2>/dev/null || echo ""
}

jwt_tenant() {
  python3 - "$1" << 'PYEOF'
import base64, json, sys
tok = sys.argv[1]
p = tok.split('.')[1]
p += '=' * (-len(p) % 4)
print(json.loads(base64.urlsafe_b64decode(p)).get('tenant_id',''))
PYEOF
}

ADMIN_TOKEN=$(get_token "$ADMIN_USER")
if [ -z "$ADMIN_TOKEN" ]; then
  echo "ERROR: Could not get token for $ADMIN_USER. Run ./tools/dev-setup-auth.sh first."
  exit 1
fi
ADMIN_TENANT=$(jwt_tenant "$ADMIN_TOKEN")
export ADMIN_TOKEN CONFIG_URL  # needed by Python subprocess for slot provisioning

# ── Step 1: Tenant workspace ──────────────────────────────────────────────────

echo "-- Step 1: Tenant workspace"
EXISTING=$(curl -sf -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$CUSTOMER_URL/tenants/$TENANT_ID" || echo "000")

if [ "$EXISTING" = "200" ]; then
  skip "Tenant $TENANT_ID already exists"
else
  CREATE_BODY=$(python3 -c "
import json
print(json.dumps({
  'tenantId': '$TENANT_ID',
  'slug': '$TENANT_SLUG',
  'displayName': '$TENANT_DISPLAY',
  'region': '$TENANT_REGION',
  'timeZone': '$TENANT_TZ',
  'supportContacts': $TENANT_CONTACTS_JSON
}))
")
  HTTP=$(curl -sf -o /dev/null -w "%{http_code}" \
    -X POST "$CUSTOMER_URL/tenants" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$CREATE_BODY" || echo "000")
  if [ "$HTTP" = "201" ]; then
    ok "Tenant created: $TENANT_ID"
  else
    err "Tenant create HTTP $HTTP"
  fi
fi

# ── Step 2: Identity config ───────────────────────────────────────────────────

echo "-- Step 2: Identity config"
ID_BODY=$(python3 -c "
import json
print(json.dumps({
  'trustedIssuer': '$IDENTITY_ISSUER',
  'audience': '$IDENTITY_AUDIENCE',
  'tenantClaimName': '$IDENTITY_TENANT_CLAIM',
  'subjectClaimName': '$IDENTITY_SUBJECT_CLAIM',
  'roleClaimNames': $IDENTITY_ROLE_CLAIMS_JSON,
  'roleMapping': $IDENTITY_ROLE_MAPPING_JSON,
  'localAccountPolicyEnabled': False
}))
")
HTTP=$(curl -sf -o /dev/null -w "%{http_code}" \
  -X PUT "$CUSTOMER_URL/tenants/$TENANT_ID/identity-config" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$ID_BODY" || echo "000")
if [ "$HTTP" = "204" ]; then
  ok "Identity config applied"
else
  err "Identity config HTTP $HTTP"
fi

# ── Step 3: Lifecycle transition → Configured ─────────────────────────────────

echo "-- Step 3: Lifecycle transition"
TRANSITION_BODY='{"to":"Configured","reason":"Provisioned via provision-tenant.sh","evidence":"local-provisioning"}'
HTTP=$(curl -sf -o /dev/null -w "%{http_code}" \
  -X POST "$CUSTOMER_URL/tenants/$TENANT_ID/transitions" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$TRANSITION_BODY" || echo "000")
if [ "$HTTP" = "200" ] || [ "$HTTP" = "204" ]; then
  ok "Transition → Configured"
elif [ "$HTTP" = "400" ]; then
  skip "Transition skipped (already Configured or beyond)"
else
  warn "Transition HTTP $HTTP (may already be in target state)"
fi

# ── Step 4: Parking policy + slots ────────────────────────────────────────────

echo "-- Step 4: Configuration (policy + slots)"
if [ "$ADMIN_TENANT" != "$TENANT_ID" ]; then
  warn "Admin token tenant=$ADMIN_TENANT ≠ target=$TENANT_ID — Configuration requires a token for the target tenant."
  warn "Policy and slots not seeded. To provision: get a token for a $TENANT_ID admin and re-run with ADMIN_USER set to that user."
  warn "For the demo tenant, Configuration is seeded automatically on service startup."
else
  POLICY_BODY=$(python3 -c "
import json
print(json.dumps({
  'timeZone': '$TENANT_TZ',
  'dailyRequestCap': $POLICY_CAP,
  'drawCutOffTime': '${POLICY_CUTOFF_H}:${POLICY_CUTOFF_M}:00',
  'allocationLookbackDays': $POLICY_LOOKBACK,
  'lateCancellationPenalty': $POLICY_LATE_PENALTY,
  'noShowPenalty': $POLICY_NOSHOW_PENALTY,
  'sameDayBookingEnabled': $POLICY_SAMEDAY == 'true',
  'sameDayUsesRequestCap': True,
  'companyCarTier1Enabled': $POLICY_COMPANY_CAR == 'true',
  'companyCarOverflowBehavior': '$POLICY_OVERFLOW',
  'automaticReallocationEnabled': $POLICY_REALLOC == 'true',
  'manualAdjustmentEnabled': True,
  'usageConfirmationRequired': False,
  'usageConfirmationWindowMinutes': 60,
  'usageConfirmationMethods': ['manual'],
  'noShowDetectionEnabled': True,
  'publicationReason': 'Provisioned via provision-tenant.sh'
}))
")
  HTTP=$(curl -sf -o /dev/null -w "%{http_code}" \
    -X PUT "$CONFIG_URL/configuration/parking-policy" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$POLICY_BODY" || echo "000")
  [ "$HTTP" = "204" ] && ok "Policy applied" || err "Policy HTTP $HTTP"

  python3 - "$LOCATIONS_JSON" << 'PYEOF'
import json, sys, subprocess, os

locations = json.loads(sys.argv[1])
config_url = os.environ.get('CONFIG_URL', 'http://localhost:5141')
token = os.environ.get('ADMIN_TOKEN', '')

for loc in locations:
  loc_id = loc['locationId']
  count = loc.get('slotCount', 10)
  charger_slots = loc.get('chargerSlots', 0)
  accessible_slots = loc.get('accessibleSlots', 0)

  slots = []
  for i in range(1, count + 1):
    slot = {
      'slotId': f'{loc_id}-{i:02d}',
      'isActive': True,
      'hasCharger': i <= charger_slots,
      'isAccessible': i <= accessible_slots,
      'isCompanyCarOnly': False,
      'isMotorcycleCapacity': False
    }
    slots.append(slot)

  body = json.dumps({'slots': slots, 'changeReason': f'Provisioned via provision-tenant.sh ({loc_id})'})
  result = subprocess.run([
    'curl', '-sf', '-o', '/dev/null', '-w', '%{http_code}',
    '-X', 'PUT',
    f'{config_url}/configuration/locations/{loc_id}/slots',
    '-H', f'Authorization: Bearer {token}',
    '-H', 'Content-Type: application/json',
    '-d', body
  ], capture_output=True, text=True)
  code = result.stdout.strip()
  if code == '204':
    print(f"  \033[0;32mOK\033[0m    Slots for {loc_id}: {count} slots ({charger_slots} chargers, {accessible_slots} accessible)")
  else:
    print(f"  \033[0;31mERR\033[0m   Slots for {loc_id}: HTTP {code}")
PYEOF
fi

# ── Step 5: Readiness check ───────────────────────────────────────────────────

echo "-- Step 5: Readiness evidence"
READINESS=$(curl -sf \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$CUSTOMER_URL/tenants/$TENANT_ID/readiness" 2>/dev/null || echo "")
if [ -n "$READINESS" ]; then
  STATUS=$(echo "$READINESS" | python3 -c "import json,sys; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNKNOWN")
  ok "Readiness: $STATUS"
  echo ""
  echo "$READINESS" | python3 -m json.tool 2>/dev/null || echo "$READINESS"
else
  err "Readiness endpoint unreachable"
fi

# ── Step 6: Smoke (if smokeUsers defined) ─────────────────────────────────────

if [ -n "$SMOKE_USERS" ]; then
  echo "-- Step 6: Smoke checks"
  GATEWAY="${GATEWAY_URL:-http://localhost:10000}"
  for SMOKE_USER in $SMOKE_USERS; do
    SMOKE_TOKEN=$(get_token "$SMOKE_USER" 2>/dev/null || echo "")
    if [ -z "$SMOKE_TOKEN" ]; then
      warn "No token for $SMOKE_USER (user may not exist in Keycloak)"
      continue
    fi
    SMOKE_TENANT=$(jwt_tenant "$SMOKE_TOKEN")
    ME=$(curl -sf -H "Authorization: Bearer $SMOKE_TOKEN" "$GATEWAY/me" 2>/dev/null || echo "")
    if [ -n "$ME" ]; then
      ROLES=$(echo "$ME" | python3 -c "import json,sys; print(','.join(json.load(sys.stdin).get('roles',[])))" 2>/dev/null || echo "?")
      ok "/me: $SMOKE_USER → tenantId=$SMOKE_TENANT roles=$ROLES"
    else
      warn "/me unreachable for $SMOKE_USER"
    fi
  done
fi

# ── Summary ───────────────────────────────────────────────────────────────────

echo ""
echo "== Provision complete: $TENANT_ID =="
if [ "$ERRORS" -gt 0 ]; then
  echo -e "  ${RED}$ERRORS error(s) — review output above.${NC}"
  exit 1
else
  echo -e "  ${GREEN}All steps passed.${NC}"
fi
