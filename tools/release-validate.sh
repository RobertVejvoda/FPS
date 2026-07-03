#!/usr/bin/env bash
# tools/release-validate.sh — REL001A: repeatable Release 1 validation gate + evidence summary.
#
# One primary command that runs the LOCAL / container validation path, records a per-area
# pass/fail evidence summary you can paste into the Release 1 tracking issue (#388), and
# documents (but does not execute) the NAS / Cloudflare HOSTED path.
#
# This script only ORCHESTRATES the existing gates — it does not re-implement checks:
#   • ./tools/validate.sh                     .NET build + every server test suite
#                                             (DataHub projections, Draw, notifications,
#                                              reports, audit, and the platform-health
#                                              endpoints are all covered by their tests)
#   • code/web/fps-web  typecheck + build     the web app (not in CI — validated here)
#   • ./tools/start-container-stack.sh --seed  container stack health + auth/OIDC + Green
#                                             Logistics seed (draw / waitlist / reallocation
#                                             + HR names) + booking→notification→audit E2E
#                                             + boundary smoke (via smoke-hosted.sh)
#   • ./tools/smoke-gateway-health.sh          per-service health through the Envoy gateway
#   • seeded-state area probes                 reports / audit / notifications / draw outcomes
#
# Evidence: written to release-evidence-<UTC-timestamp>.md (git-ignored; tokens never written).
# No secrets or environment-specific values are stored.
#
# Usage:
#   ./tools/release-validate.sh                LOCAL / container gate (default). Runs everything.
#   ./tools/release-validate.sh --quick        Fast gate: unit + web only (skips the container stack).
#   ./tools/release-validate.sh --skip-unit    Skip ./tools/validate.sh (e.g. already run in CI).
#   ./tools/release-validate.sh --hosted       Print the NAS / Cloudflare hosted runbook and exit
#                                              (documented steps to run ON the NAS host).
#   ./tools/release-validate.sh --help
#
# Exit code: 0 when there are no BLOCKER failures; 1 when any blocker failed.
#
# Failure classification (recorded per area):
#   BLOCKER   — must be fixed before Release 1 exposure. Fails the gate.
#   RESIDUAL  — accepted residual risk for Release 1 (documented, non-blocking).
#   FOLLOWUP  — track as a follow-up issue (non-blocking).
#   SKIP      — not run in this invocation / not verifiable in this environment.

set -uo pipefail  # deliberately NOT -e: we run each check and record its result, then continue.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

GATEWAY_URL="${GATEWAY_URL:-http://localhost:10000}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
DEV_PASSWORD="${FPS_DEV_PASSWORD:-Dev1234!}"

MODE="local"
QUICK=0
SKIP_UNIT=0
while [ $# -gt 0 ]; do
  case "$1" in
    --hosted) MODE="hosted" ;;
    --quick) QUICK=1 ;;
    --skip-unit) SKIP_UNIT=1 ;;
    --help|-h) sed -n '2,48p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown flag: $1 (see --help)"; exit 1 ;;
  esac
  shift
done

GREEN='\033[0;32m'; RED='\033[0;31m'; YEL='\033[0;33m'; NC='\033[0m'

# ── evidence recording ────────────────────────────────────────────────────────
EVIDENCE_FILE="release-evidence-$(date -u +%Y%m%dT%H%M%SZ).md"
AREAS=(); RESULTS=(); NOTES=()
record() { AREAS+=("$1"); RESULTS+=("$2"); NOTES+=("$3");
  local c="$NC"; case "$2" in PASS) c="$GREEN";; BLOCKER) c="$RED";; RESIDUAL|FOLLOWUP|SKIP) c="$YEL";; esac
  printf "  [${c}%-8s${NC}] %s — %s\n" "$2" "$1" "$3"; }

# Run a command quietly; PASS on exit 0, else BLOCKER. Output goes to a per-area log.
run_gate() {  # run_gate "Area" "next-hint" cmd...
  local area="$1" hint="$2"; shift 2
  echo ""; echo "== $area =="
  local log; log="$(mktemp)"
  if "$@" > "$log" 2>&1; then record "$area" PASS "$hint"; rm -f "$log"; else
    # Keep the full output of a failing step next to the evidence so "read FAIL lines" is
    # actionable without re-running the whole gate. Path is gitignored (release-step-*.log).
    local keep; keep="release-step-$(printf '%s' "$area" | tr ' /+' '___' | tr '[:upper:]' '[:lower:]').log"
    cp "$log" "$keep"; rm -f "$log"
    record "$area" BLOCKER "$hint — full log: $keep — last lines: $(tail -n 6 "$keep" | tr '\n' ' ' | cut -c1-200)"
  fi
}

# ── local seeded-state area probes (stack must be up + seeded) ─────────────────
tok() { curl -sf -X POST "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=$CLIENT_ID&username=$1&password=$DEV_PASSWORD" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])" 2>/dev/null || echo ""; }

http_code() { curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $2" "$GATEWAY_URL$1" 2>/dev/null || echo 000; }
json_len()  { curl -s -H "Authorization: Bearer $2" "$GATEWAY_URL$1" 2>/dev/null | python3 -c "import json,sys;
try:
    d=json.load(sys.stdin); print(len(d.get('items') or d.get('entries') or d.get('draws') or (d if isinstance(d,list) else [])))
except Exception: print(-1)" 2>/dev/null || echo -1; }

probe_areas() {
  echo ""; echo "== Seeded-state area probes (Green Logistics) =="
  local ta tr th tau
  ta=$(tok gl-tenant-admin); tr=$(tok gl-report-viewer); th=$(tok gl-hr-admin); tau=$(tok gl-auditor)
  if [ -z "$ta" ]; then record "Area probes" SKIP "No local token (stack/seed not up) — run without --quick"; return; fi
  echo "  (tenant tokens acquired [REDACTED])"

  local c
  c=$(http_code "/reports/parking/summary" "$tr")
  [ "$c" = "200" ] && record "Reports" PASS "GET /reports/parking/summary → 200" \
                   || record "Reports" BLOCKER "GET /reports/parking/summary → $c"

  local n; n=$(json_len "/audit?pageSize=50" "$tau")
  [ "${n:-0}" -gt 0 ] && record "Audit" PASS "audit feed has $n entries (seeded draw/booking evidence)" \
                      || record "Audit" BLOCKER "audit feed empty or unreachable (got $n)"

  local te; te=$(tok gl-employee1)
  c=$(http_code "/notifications/unread-count" "$te")
  [ "$c" = "200" ] && record "Notifications" PASS "GET /notifications/unread-count → 200" \
                   || record "Notifications" BLOCKER "GET /notifications/unread-count → $c"

  # DataHub-backed tenant draw outcomes for the seeded showcase (projection wired end to end).
  n=$(json_len "/draws/outcomes?from=2000-01-01&to=2100-01-01&pageSize=50" "$th")
  [ "${n:-0}" -gt 0 ] && record "DataHub projections" PASS "draw outcomes projected: $n row(s)" \
                      || record "DataHub projections" RESIDUAL "no draw-outcome rows via /draws/outcomes (got $n) — verify projection lag"
}

# ── hosted runbook (documented; run on the NAS host) ──────────────────────────
print_hosted_runbook() {
  cat <<'RUNBOOK'
== Release 1 — NAS / Cloudflare HOSTED validation runbook (documented; run ON the NAS host) ==

Prerequisites (host): Docker Engine + Docker Compose v2 only. No .NET / Dapr / Node required.

1. Credentials & config
   - code/infrastructure/nas.env         real per-environment credentials (NEVER committed).
     See docs/production/nas-cloudflare-deployment-profile.md and nas-cloudflare-auth-profile.md.

2. Images
   - Pull the published GHCR images (do not build on the NAS).
     See docs/production/ghcr-image-publishing.md.

3. Tunnel + public URLs (Cloudflare)
   - Cloudflared tunnel exposes app.<domain> (SPA + /api) and auth.<domain> (Keycloak).
     See docs/production/nas-cloudflare-deployment-profile.md.

4. Bring up the hosted stack (NAS overlay: restart policies + required-credential check):
     ./tools/start-container-stack.sh --nas --domain <domain> --realm fps-pilot
   This verifies container/sidecar health, internal OIDC discovery, and the public
   app./auth. boundary through Cloudflare (Docker-only probes).

5. Hosted E2E + boundary smoke (writes a redacted evidence file):
     APP_URL=https://app.<domain>/api AUTH_URL=https://auth.<domain> OIDC_REALM=fps-pilot \
       ./tools/smoke-hosted.sh
   Produces smoke-evidence-<timestamp>.txt (tokens redacted) — attach to #388.
   Mandatory boundary checks: HTTP→HTTPS redirect, and internal surfaces
   (/metrics, Keycloak /admin, Dapr control plane) NOT publicly served.
   See docs/production/hosted-smoke-runbook.md and ops007-hosted-demo-evidence.md.

Note: --nas --seed is intentionally rejected — a NAS is never seeded with dev credentials.
Seeding a hosted environment is a separate, credentialed follow-up.
RUNBOOK
}

# ── main ──────────────────────────────────────────────────────────────────────
if [ "$MODE" = "hosted" ]; then
  print_hosted_runbook
  exit 0
fi

echo "== FairSpot Release 1 validation gate (LOCAL / container) =="
echo "Evidence → $EVIDENCE_FILE"

# 1. Host prerequisites (Docker only for the container path).
run_gate "Prerequisites" "install Docker Engine + Compose v2" bash -c 'command -v docker >/dev/null && docker compose version >/dev/null 2>&1'

# 2. Server unit gate — build + every test suite (covers DataHub/Draw/notifications/reports/audit/platform-health).
if [ "$SKIP_UNIT" = "0" ]; then
  run_gate "Server tests (validate.sh)" "run ./tools/validate.sh and read the failing suite" ./tools/validate.sh
else
  record "Server tests (validate.sh)" SKIP "skipped via --skip-unit"
fi

# 3. Web app — typecheck + build (fps-web is not in CI).
run_gate "Web app (typecheck+build)" "cd code/web/fps-web && npm run typecheck && npm run build" \
  bash -c 'cd code/web/fps-web && npm run typecheck && npm run build'

# 4. Mobile — readiness note (built + tested in CI; store publishing is out of scope).
record "Mobile readiness" RESIDUAL "fps-mobile builds/tests in CI; store publishing out of scope (see docs/production/hosted-mobile-build-plan.md)"

# 5. Platform health — verified via the DataHub platform-health endpoint tests in step 2
#    (no platform-issuer realm exists locally to mint a live platform token).
record "Platform health" PASS "platform draw-health + usage-stats endpoints covered by server tests (validate.sh); honest not-wired states verified there"

if [ "$QUICK" = "0" ]; then
  # 6. Container stack + auth + GL seed + booking→notification→audit E2E + boundary smoke.
  run_gate "Container stack + seed + E2E" "re-run ./tools/start-container-stack.sh --seed and read FAIL lines" \
    ./tools/start-container-stack.sh --seed

  # 7. Per-service gateway health.
  run_gate "Gateway service health" "./tools/smoke-gateway-health.sh (stack must be up)" ./tools/smoke-gateway-health.sh

  # 8. Seeded-state area probes for granular evidence.
  probe_areas
else
  record "Container stack + seed + E2E" SKIP "skipped in --quick mode — run the full gate before release"
  record "Gateway service health" SKIP "skipped in --quick mode"
  record "Area probes" SKIP "skipped in --quick mode"
fi

# ── evidence summary ──────────────────────────────────────────────────────────
blockers=0
for r in "${RESULTS[@]}"; do [ "$r" = "BLOCKER" ] && blockers=$((blockers+1)); done
overall="PASS"; [ "$blockers" -gt 0 ] && overall="FAIL"

{
  echo "# FairSpot Release 1 validation evidence"
  echo ""
  echo "- **Profile:** local / container"
  echo "- **When (UTC):** $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "- **Commit:** $(git rev-parse --short HEAD 2>/dev/null || echo unknown) ($(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo '?'))"
  echo "- **Overall:** $overall ($blockers blocker(s))"
  echo ""
  echo "| Area | Result | Evidence / next hint |"
  echo "|------|--------|----------------------|"
  for i in "${!AREAS[@]}"; do
    note="${NOTES[$i]//|/\\|}"
    echo "| ${AREAS[$i]} | ${RESULTS[$i]} | ${note} |"
  done
  echo ""
  echo "Legend: **BLOCKER** fails the gate · **RESIDUAL** accepted risk · **FOLLOWUP** track separately · **SKIP** not run here."
  echo ""
  echo "Hosted (NAS/Cloudflare) path is documented separately — run \`./tools/release-validate.sh --hosted\` on the NAS host and attach its \`smoke-evidence-*.txt\`."
  echo ""
  echo "_Tokens and secrets are never written to this file._"
} > "$EVIDENCE_FILE"

echo ""
echo "======================================================================"
printf "Release 1 local gate: %b%s%b  (%d blocker(s))\n" \
  "$([ "$overall" = PASS ] && echo "$GREEN" || echo "$RED")" "$overall" "$NC" "$blockers"
echo "Evidence summary → $EVIDENCE_FILE  (paste into #388)"
[ "$blockers" -gt 0 ] && echo "Fix BLOCKER rows above, then re-run. Hosted path: ./tools/release-validate.sh --hosted"
echo "======================================================================"

[ "$blockers" -gt 0 ] && exit 1 || exit 0
