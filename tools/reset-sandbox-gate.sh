#!/usr/bin/env bash
# tools/reset-sandbox-gate.sh — PLAT003C live gate.
#
# Proves the Green Logistics sandbox reset works end to end in the local container stack:
# seed -> purge/reset -> reseed, with the outcome recorded as reset evidence.
#
# This script does NOT start the stack. Preconditions:
#   1. Bring the stack up and seed Green Logistics, with the reset activated:
#        FPS_SANDBOX_RESET_ENABLED=true FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true \
#          ./tools/start-container-stack.sh --seed
#      (--seed runs in ASPNETCORE_ENVIRONMENT=Development.)
#   2. Run this gate.
#
# Determinism: the reset is dedup-leased per UTC day. This gate CLEARS the day's lease before it
# triggers, so every run drives a real reset (never a no-op "Skipped"). If a Skipped is still
# observed it fails by default with instructions; pass ALLOW_SKIPPED=1 to accept a skip.
#
# Why the scheduler route: the manual POST /platform/tenants/{id}/reset-sandbox needs a
# platform_operator token, and the platform plane is dormant in the local realm. The internal
# scheduler route is [DaprInternalOnly]; in Development with APP_API_TOKEN unset the guard allows
# the call, so it is the reproducible local trigger. In a hosted profile the operator uses the
# platform endpoint (and the GET evidence endpoint) instead.
set -euo pipefail

TENANT="${TENANT:-greenlogistics}"
CUSTOMER_URL="${CUSTOMER_URL:-http://localhost:5181}"
ALLOW_SKIPPED="${ALLOW_SKIPPED:-0}"
CURL_IMAGE="${CURL_IMAGE:-curlimages/curl:8.11.1}"
INFRA_DIR="$(cd "$(dirname "$0")/../code/infrastructure" && pwd)"
COMPOSE=(docker compose --project-directory "$INFRA_DIR"
  -f "$INFRA_DIR/docker-compose.yaml"
  -f "$INFRA_DIR/docker-compose.services.yml"
  -f "$INFRA_DIR/docker-compose.dapr.yml")

echo "== PLAT003C sandbox-reset gate (tenant=$TENANT) =="

# 1. Customer service reachable?
if ! curl -fsS -o /dev/null "$CUSTOMER_URL/health"; then
  echo "FAIL: fps-customer not reachable at $CUSTOMER_URL."
  echo "      Start the stack first: FPS_SANDBOX_RESET_ENABLED=true FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true ./tools/start-container-stack.sh --seed"
  exit 1
fi

# 2. Clear the per-UTC-day lease so THIS run drives a real reset (not a Skipped dedupe). The lease
#    lives in customerstore; reach the customer Dapr sidecar (:3500) via a curl container that shares
#    the customer container's network namespace. Best-effort.
CID="$("${COMPOSE[@]}" ps -q fps-customer 2>/dev/null | head -1 || true)"
if [[ -n "$CID" ]]; then
  docker run --rm --network "container:$CID" "$CURL_IMAGE" -s -o /dev/null \
    -X DELETE "http://localhost:3500/v1.0/state/customerstore/sandbox-reset:lease" 2>/dev/null || true
else
  echo "  (note: could not resolve the fps-customer container to clear the reset lease; a Skipped run is possible)"
fi

# 3. Trigger the reset (internal scheduler route; Development bypass, no auth).
SINCE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "Triggering reset: POST $CUSTOMER_URL/sandbox-reset-scheduler"
curl -fsS -X POST -o /dev/null "$CUSTOMER_URL/sandbox-reset-scheduler"

# 4. Poll the customer logs for the per-tenant outcome OR the window-skip line.
printf "Waiting for reset outcome"
outcome=""; skipped=""
for _ in $(seq 1 30); do
  logs="$("${COMPOSE[@]}" logs --since "$SINCE" fps-customer 2>/dev/null || true)"
  outcome="$(printf '%s\n' "$logs" | grep -E "Scheduled sandbox reset: tenant=${TENANT} status=" | tail -1 || true)"
  skipped="$(printf '%s\n' "$logs" | grep -E "Scheduled sandbox reset window .* already claimed" | tail -1 || true)"
  { [[ -n "$outcome" ]] || [[ -n "$skipped" ]]; } && break
  printf "."
  sleep 2
done
printf "\n"

# 5. Classify — Succeeded is the only pass; Failed (mid-flow), Refused (guard), Unavailable (inert)
#    and Skipped (lease) are all distinct failures.
if [[ -n "$outcome" ]]; then
  echo "Reset log: ${outcome#*Scheduled sandbox reset: }"
  case "$outcome" in
    *"status=Succeeded"*)
      echo "PASS: seed -> reset -> reseed completed for ${TENANT} (status=Succeeded)."
      exit 0 ;;
    *"status=Failed"*)
      echo "FAIL: the reset ran but FAILED mid-flow (a purge or reseed step threw). See 'detail=' in the"
      echo "      log line above and the sandbox-reset evidence record (FailureReason)."
      exit 1 ;;
    *"status=Refused"*)
      echo "FAIL: reset Refused by the guard — ${TENANT} is not a stored resettable sandbox (unexpected)."
      exit 1 ;;
    *"status=Unavailable"*)
      echo "FAIL: reset Unavailable — SandboxReset:Enabled is off or no tenant-store purgers are registered."
      exit 1 ;;
    *)
      echo "FAIL: unexpected reset status in: ${outcome}"
      exit 1 ;;
  esac
fi

if [[ -n "$skipped" ]]; then
  if [[ "$ALLOW_SKIPPED" == "1" ]]; then
    echo "INFO (ALLOW_SKIPPED=1): reset was skipped — the per-UTC-day lease was already claimed; the"
    echo "      machinery is armed but no reset ran this invocation."
    exit 0
  fi
  echo "FAIL: reset was SKIPPED — the per-UTC-day lease is already claimed, so no reset ran this window."
  echo "      This gate normally clears the lease automatically; if you still see this, clear it and retry:"
  echo "        CID=\$(${COMPOSE[*]} ps -q fps-customer)"
  echo "        docker run --rm --network container:\$CID $CURL_IMAGE -X DELETE http://localhost:3500/v1.0/state/customerstore/sandbox-reset:lease"
  echo "      Or accept a skip explicitly with ALLOW_SKIPPED=1."
  exit 1
fi

echo "FAIL: no reset outcome or skip logged for tenant=${TENANT} within the timeout."
echo "      Confirm the stack was started with FPS_SANDBOX_RESET_ENABLED=true FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true."
exit 1
