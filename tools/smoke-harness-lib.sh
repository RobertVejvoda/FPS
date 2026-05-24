#!/bin/sh
# Shared backend harness helpers for web/mobile smoke scripts.

STARTED_SMOKE_HARNESS=false

smoke_log() {
  printf '[smoke] %s\n' "$*"
}

smoke_harness_ready() {
  for port_label in \
    "10000 Gateway" \
    "5192 Identity" \
    "5131 Booking" \
    "5157 Notification" \
    "5197 Profile" \
    "5161 Audit" \
    "5171 Reporting" \
    "5141 Configuration" \
    "5181 Customer"
  do
    if ! nc -z localhost "${port_label%% *}" 2>/dev/null; then
      return 1
    fi
  done

  return 0
}

ensure_smoke_harness() {
  if smoke_harness_ready; then
    smoke_log "Reusing running backend harness."
    STARTED_SMOKE_HARNESS=false
    return 0
  fi

  smoke_log "Starting backend harness for smoke run."
  "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
  "$REPO_ROOT/tools/start-local-harness.sh" --skip-infra
  STARTED_SMOKE_HARNESS=true
}

cleanup_smoke_harness() {
  if [ "${SMOKE_STOP_HARNESS_ON_EXIT:-false}" = "true" ]; then
    "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
    return
  fi

  smoke_log "Leaving backend harness running. Stop it with: ./tools/stop-local-harness.sh --services-only"
}
