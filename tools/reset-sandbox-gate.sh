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
# Why the scheduler route: the manual POST /platform/tenants/{id}/reset-sandbox needs a
# platform_operator token, and the platform plane is dormant in the local realm. The internal
# scheduler route is [DaprInternalOnly]; in Development with APP_API_TOKEN unset the guard allows
# the call, so it is the reproducible local trigger. In a hosted profile the operator uses the
# platform endpoint (and the GET evidence endpoint) instead.
set -euo pipefail

TENANT="${TENANT:-greenlogistics}"
CUSTOMER_URL="${CUSTOMER_URL:-http://localhost:5181}"
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

# 2. Record a log offset so we only read lines produced by THIS trigger.
SINCE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# 3. Trigger the reset (internal scheduler route; Development bypass, no auth).
echo "Triggering reset: POST $CUSTOMER_URL/sandbox-reset-scheduler"
curl -fsS -X POST -o /dev/null "$CUSTOMER_URL/sandbox-reset-scheduler"

# 4. Poll the customer logs for the outcome for this tenant.
printf "Waiting for reset outcome"
outcome=""
for _ in $(seq 1 30); do
  outcome="$("${COMPOSE[@]}" logs --since "$SINCE" fps-customer 2>/dev/null \
    | grep -E "Scheduled sandbox reset: tenant=${TENANT} status=" | tail -1 || true)"
  [[ -n "$outcome" ]] && break
  printf "."
  sleep 2
done
printf "\n"

if [[ -z "$outcome" ]]; then
  echo "FAIL: no reset outcome logged for tenant=${TENANT} within the timeout."
  echo "      Confirm the stack was started with FPS_SANDBOX_RESET_ENABLED=true FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true."
  exit 1
fi

echo "Reset log: ${outcome##*fps-customer  | }"
case "$outcome" in
  *"status=Succeeded"*)
    echo "PASS: seed -> reset -> reseed completed for ${TENANT} (status=Succeeded)."
    exit 0 ;;
  *"status=Skipped"*)
    echo "INFO: reset was skipped — the per-window lease was already claimed for today's UTC window"
    echo "      (a reset already ran this window; the machinery is armed). Restart the stack to see a"
    echo "      fresh Succeeded, or re-run on the next UTC day."
    exit 0 ;;
  *"status=Unavailable"*)
    echo "FAIL: reset is Unavailable — SandboxReset:Enabled is off or no tenant-store purgers are registered."
    exit 1 ;;
  *"status=Refused"*)
    echo "FAIL: reset Refused — ${TENANT} is not a stored resettable sandbox (guard working; unexpected for greenlogistics)."
    exit 1 ;;
  *)
    echo "FAIL: unexpected reset status in: ${outcome}"
    exit 1 ;;
esac
