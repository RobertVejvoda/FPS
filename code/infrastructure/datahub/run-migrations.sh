#!/bin/sh
# Run DataHub schema migrations as a finite Compose job.
#
# Current images run in Production, honor DataHub__ApplyMigrationsAndExit=true,
# preserve EF pending-model validation, and exit 0 after applying migrations.
# Images published before that switch reach listening state instead. Only then
# does this launcher restart the legacy image in Development, where its existing
# startup path applies compiled migrations. Both probes bind to container
# loopback; the legacy Development process is stopped after it reaches listening
# state, which occurs only after startup migration completes.

set -eu

if [ -z "${FPS_ASSEMBLY:-}" ]; then
  echo "ERROR: FPS_ASSEMBLY is not set in the DataHub image." >&2
  exit 1
fi

timeout_seconds="${FPS_DATAHUB_MIGRATION_TIMEOUT_SECONDS:-180}"
case "$timeout_seconds" in
  ''|*[!0-9]*|0)
    echo "ERROR: FPS_DATAHUB_MIGRATION_TIMEOUT_SECONDS must be a positive integer." >&2
    exit 1
    ;;
esac

log_file="${TMPDIR:-/tmp}/fairspot-datahub-migrate.log"
: > "$log_file"
migration_pid=""

# Invoked indirectly by the traps below.
# shellcheck disable=SC2329
cleanup() {
  if [ -n "$migration_pid" ] && kill -0 "$migration_pid" 2>/dev/null; then
    kill "$migration_pid" 2>/dev/null || true
    wait "$migration_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT HUP INT TERM

ASPNETCORE_ENVIRONMENT=Production \
DataHub__ApplyMigrationsAndExit=true \
  dotnet "$FPS_ASSEMBLY" >"$log_file" 2>&1 &
migration_pid=$!
elapsed=0

while [ "$elapsed" -lt "$timeout_seconds" ]; do
  if ! kill -0 "$migration_pid" 2>/dev/null; then
    set +e
    wait "$migration_pid"
    exit_code=$?
    set -e
    cat "$log_file"
    migration_pid=""
    if [ "$exit_code" -eq 0 ]; then
      echo "DataHub migration-mode process exited successfully."
      exit 0
    fi
    echo "ERROR: DataHub migration-mode process exited with code $exit_code." >&2
    exit "$exit_code"
  fi

  if grep -q 'Now listening on:' "$log_file"; then
    cat "$log_file"
    echo "DataHub image did not honor explicit migration mode; restarting the legacy fallback in Development."
    kill "$migration_pid" 2>/dev/null || true
    wait "$migration_pid" 2>/dev/null || true
    migration_pid=""
    break
  fi

  sleep 1
  elapsed=$((elapsed + 1))
done

if [ "$elapsed" -ge "$timeout_seconds" ]; then
  cat "$log_file" >&2
  echo "ERROR: DataHub migration-mode detection did not complete within ${timeout_seconds}s." >&2
  exit 1
fi

: > "$log_file"
ASPNETCORE_ENVIRONMENT=Development \
DataHub__ApplyMigrationsAndExit=false \
  dotnet "$FPS_ASSEMBLY" >"$log_file" 2>&1 &
migration_pid=$!

while [ "$elapsed" -lt "$timeout_seconds" ]; do
  if ! kill -0 "$migration_pid" 2>/dev/null; then
    set +e
    wait "$migration_pid"
    exit_code=$?
    set -e
    cat "$log_file"
    migration_pid=""
    echo "ERROR: legacy DataHub migration fallback exited before reaching listening state (code $exit_code)." >&2
    if [ "$exit_code" -eq 0 ]; then
      exit 1
    fi
    exit "$exit_code"
  fi

  if grep -q 'Now listening on:' "$log_file"; then
    cat "$log_file"
    echo "Legacy DataHub image reached listening state after Development startup migrations; stopping the loopback-only process."
    kill "$migration_pid" 2>/dev/null || true
    wait "$migration_pid" 2>/dev/null || true
    migration_pid=""
    exit 0
  fi

  sleep 1
  elapsed=$((elapsed + 1))
done

cat "$log_file" >&2
echo "ERROR: legacy DataHub migration fallback did not complete within ${timeout_seconds}s." >&2
exit 1
