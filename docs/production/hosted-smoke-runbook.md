# OPS013 Hosted Customer Smoke and Reset Evidence

**Status:** Ready for operator use — public-domain execution marked pending until domain is configured.
**Prepared by:** Claude (FPS Implementer), 2026-05-29
**Tracks:** Issue #314
**Priority:** P0 customer-first deployment

---

## Purpose

This runbook and the accompanying script (`tools/smoke-hosted.sh`) verify that the FairSpot NAS pilot is working end-to-end at the public domain before customer access is allowed, and after each reset.

**Evidence from this runbook satisfies the pre-customer-traffic gate in [OPS011](./nas-cloudflare-deployment-profile.md).**

---

## Scope

The smoke covers:

| Area | Checks |
|---|---|
| Auth / identity | Login token acquisition; OIDC discovery |
| Tenant context | `/me` resolves tenant, user, and roles from JWT |
| Profile snapshot | Employee profile is accessible |
| Booking request | Submit a booking request; verify it appears in the list |
| Draw status | Draw status endpoint returns for the configured location/date |
| Notifications | Unread count and notification list accessible after booking |
| Audit | Audit record created and accessible after booking |
| Reporting | Summary report accessible for admin/report-viewer |
| HR operations | HR-scoped request list accessible to hr-admin |
| Administrator | Tenant readiness check accessible to admin |
| Logs | Service log evidence (manual check) |
| Reset | Demo data can be restored to a known state |

---

## Prerequisites

Before running the smoke:

1. The Cloudflare Tunnel is connected (verified per [OPS011 Step 5](./nas-cloudflare-deployment-profile.md)).
2. `app.<domain>` and `auth.<domain>` are reachable.
3. OIDC realm has been configured for the public domain (per [OPS012](./nas-cloudflare-auth-profile.md)).
4. Demo seed data has been loaded: `./tools/dev-seed.sh`.
5. All services are running and healthy (checked by the script).

---

## Running the smoke script

```bash
# Minimal: point at public domain
APP_URL=https://app.<domain> \
AUTH_URL=https://auth.<domain> \
OIDC_REALM=fps-pilot \
  ./tools/smoke-hosted.sh
```

The script outputs `PASS`, `FAIL`, `PENDING`, `SKIP`, or `DEFERRED` for each check, prints a summary at the end, and exits non-zero if any required check fails.

**If the public domain is not yet configured**, run in local mode to verify the same checks against the local harness:

```bash
# 1. Start all services with Dapr (Customer service requires its Dapr sidecar):
#    dapr run -f dapr.yaml   OR   ./tools/start-with-dapr.sh
# 2. Seed demo data:
#    ./tools/dev-seed.sh
# 3. Source auth environment (sets Auth__Authority for services):
source ./tools/dev-env.sh

APP_URL=http://localhost:10000 \
AUTH_URL=http://localhost:8180 \
OIDC_REALM=fps-local \
  ./tools/smoke-hosted.sh
```

**Important local harness notes:**
- The local Keycloak container maps internal port 8080 to host port **8180**. Use `AUTH_URL=http://localhost:8180`, not `http://localhost:8080`.
- The Customer service (port 5181) requires a Dapr sidecar to access its state store. Running it with bare `dotnet run` without the sidecar will cause 500 errors on readiness checks.
- The smoke uses +3 days from today for the test booking to avoid `CutOffPassed` rejection in the evening.

The script marks any public-domain-only checks (Cloudflare reachability, TLS, WAF) as `PENDING` when running against localhost.

**Pilot-deferred items:** A `DEFERRED` status in the readiness section indicates pilot limitations (ObjectStorageReadiness, BrandingReadiness) that are non-blocking. See `docs/production/cust008-onboarding-e2e-evidence.md` for the full classification.

---

## Evidence output format

The script writes a structured evidence file to `smoke-evidence-<timestamp>.txt` in the current directory. This file is safe to attach to a PR or release note — it redacts all tokens and passwords before writing.

Evidence file structure:

```
=== FairSpot Hosted Smoke Evidence ===
Run at: 2026-05-29T14:30:00Z
Environment: https://app.<domain>
Realm: fps-pilot

[PASS]  OIDC discovery reachable
[PASS]  Login: employee1
[PASS]  /me → tenantId=demo roles=[employee]
...
[PENDING]  Cloudflare TLS — run against public domain to verify
...
[FAIL]   Audit record not found after booking

Summary: 18 PASS / 1 PENDING / 1 FAIL
```

Tokens are replaced with `[REDACTED]` and bearer headers are not written to the evidence file.

---

## Mandatory checks before customer access

These checks must PASS (not PENDING) before any customer data is allowed:

| # | Check | Mandatory |
|---|---|---|
| 1 | OIDC discovery accessible at `auth.<domain>` | Yes |
| 2 | Employee login returns a valid token | Yes |
| 3 | `/me` returns `tenantId`, `userId`, `roles` from JWT | Yes |
| 4 | Booking request created successfully | Yes |
| 5 | Booking appears in employee list | Yes |
| 6 | Notification record exists after booking event | Yes |
| 7 | Audit record exists after booking | Yes |
| 8 | Tenant readiness check passes | Yes |
| 9 | Cloudflare TLS active (https only) | Yes |
| 10 | WAF blocks `/metrics` and Keycloak admin paths | Yes |

---

## Log review (manual)

After running the script, an operator must manually verify:

1. No 5xx errors in Envoy access log during the smoke run.
2. No token or credential values appear in service logs.
3. At least one trace/span is visible in Grafana/Jaeger for the booking submission path.

Check Grafana at `http://localhost:3000` (local) or `https://ops.<domain>` (if published via Cloudflare Access).

---

## Reset procedure

### Quick reset (demo data only — does not restart services)

```bash
./tools/demo-reset.sh
```

Re-seeds tenant, users, parking data, and booking history. Tunnel and OIDC sessions are unaffected.

### Full reset (stop all services, clean volumes, start clean)

```bash
# Stop all services and remove all data volumes
docker compose -f code/infrastructure/docker-compose.yaml down -v
docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml down

# Restart infrastructure
docker compose -f code/infrastructure/docker-compose.yaml up -d

# Re-import Keycloak realm
./tools/dev-setup-auth.sh

# Seed demo data
./tools/dev-seed.sh

# Restart application services
./tools/start-with-dapr.sh

# Start tunnel
docker compose -f code/infrastructure/cloudflared/docker-compose.cloudflared.yml \
  --env-file code/infrastructure/cloudflared/.env.nas up -d
```

After a full reset, wait for all service health checks to pass before running smoke:

```bash
for port in 5192 5131 5197 5157 5161 5171 5141 5181; do
  status=$(curl -sf http://localhost:$port/health | python3 -c "import sys,json; print(json.load(sys.stdin).get('status','UNKNOWN'))" 2>/dev/null || echo "UNREACHABLE")
  echo "  :$port → $status"
done
```

---

## Before-customer-access checklist

Complete this checklist and attach the smoke evidence file to the release or PR before allowing customer access:

- [ ] `tools/smoke-hosted.sh` run against `https://app.<domain>` — all mandatory checks PASS
- [ ] Evidence file attached to PR or release note (tokens redacted)
- [ ] Log review: no 5xx errors, no credential values in logs
- [ ] WAF active: `/metrics` returns 403 from public internet
- [ ] Keycloak admin console not reachable at `https://auth.<domain>/admin`
- [ ] TLS grade verified (check via browser or `openssl s_client`)
- [ ] Backup/restore: at least one restore drill evidenced (per [OPS009](./rto-rpo-requirements.md))
- [ ] Reset runbook tested at least once

---

## Document change log

| Date | Author | Change |
|---|---|---|
| 2026-05-29 | Claude | Initial runbook for issue #314 |
| 2026-06-25 | Claude | OPS007: fix local AUTH_URL to 8180, add Dapr startup note, add DEFERRED status, add +3d booking note |
