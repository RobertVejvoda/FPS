#!/bin/sh
# Shared backend harness helpers for web/mobile smoke scripts.

STARTED_SMOKE_HARNESS=false

smoke_log() {
  printf '[smoke] %s\n' "$*"
}

current_smoke_revision() {
  if command -v git > /dev/null 2>&1; then
    git -C "$REPO_ROOT" rev-parse --verify HEAD 2>/dev/null || printf 'unknown\n'
  else
    printf 'unknown\n'
  fi
}

smoke_harness_revision_matches() {
  revision_file="$REPO_ROOT/logs/local-harness/revision"
  [ -f "$revision_file" ] || return 1

  started_revision="$(cat "$revision_file" 2>/dev/null || true)"
  current_revision="$(current_smoke_revision)"
  [ -n "$started_revision" ] && [ "$started_revision" = "$current_revision" ]
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
  stopped_stale_harness=false

  if smoke_harness_ready; then
    if smoke_harness_revision_matches; then
      smoke_log "Reusing running backend harness."
      STARTED_SMOKE_HARNESS=false
      return 0
    fi

    smoke_log "Backend harness was started from a different revision; restarting app services."
    "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
    stopped_stale_harness=true
  fi

  smoke_log "Starting backend harness for smoke run."
  if [ "$stopped_stale_harness" = false ]; then
    "$REPO_ROOT/tools/stop-local-harness.sh" --services-only
  fi
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
