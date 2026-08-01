#!/bin/sh
# Run DataHub schema migrations as a finite Compose job.
#
# Current images honor DataHub__ApplyMigrationsAndExit=true and exit 0 after
# applying migrations. Images published before that switch still apply their
# compiled EF migrations during Development startup, then continue serving.
# For those legacy images this launcher binds the process to container loopback,
# waits until ASP.NET reports that it is listening (which occurs only after the
# startup migration completes), terminates it, and exits 0.

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
echo "ERROR: DataHub migration job did not complete within ${timeout_seconds}s." >&2
exit 1
