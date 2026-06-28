# OPS007 Hosted Demo Environment Evidence

**Status:** Localhost smoke complete — public-domain run pending tunnel and domain configuration.
**Prepared by:** Claude (FPS Implementer), 2026-06-25
**Tracks:** Issue #226
**Source-of-truth docs:** [OPS011 NAS/Cloudflare deployment profile](./nas-cloudflare-deployment-profile.md), [SEC010 WAF profile](../security/cloudflare-waf-profile.md), [OPS013 hosted smoke runbook](./hosted-smoke-runbook.md)
**Onboarding evidence:** [CUST008](./cust008-onboarding-e2e-evidence.md)

---

## Purpose

This document records the hosted demo environment evidence for the NAS/Cloudflare deployment profile. It confirms profile assumptions, classifies each check as locally verified or pending public-domain execution, and lists gaps that must be resolved before customer traffic is allowed.

---

## Classification Legend

| Symbol | Meaning |
|--------|---------|
| ✅ **Verified locally** | Confirmed working in the local harness (localhost mode smoke). |
| 🟡 **Config-dependent** | Correct behavior documented; requires operator-supplied values to activate. |
| 🟠 **Pilot-deferred** | Non-blocking pilot limitation; also reported by tenant readiness as `Deferred`. |
| ⏳ **Pending public domain** | Cannot be verified without a live Cloudflare tunnel and public domain. |
| ❌ **Gap** | Not implemented or not evidenced; blocks customer-traffic gate. |

---

## NAS/Cloudflare Profile Assumption Review

### Architecture assumptions

| Assumption | Status | Notes |
|---|---|---|
| Cloudflare Tunnel replaces inbound port exposure | 🟡 Config-dependent | Requires operator Cloudflare account and tunnel token in `.env.nas` |
| `app.<domain>` → Envoy proxy on NAS port 10000 | 🟡 Config-dependent | Envoy routes verified locally; public hostname requires tunnel |
| `auth.<domain>` → Keycloak on NAS port 8080 (internal) | 🟡 Config-dependent | Keycloak verified locally on 8180 (Docker maps 8180→8080) |
| All other ports (MongoDB, RabbitMQ, Vault, MinIO, Dapr, FPS services) are internal-only | 🟡 Config-dependent | NAS firewall rules documented in OPS011 Step 1; not verified externally |
| No NAS ports exposed to Internet | ⏳ Pending public domain | `docker ps --format "table {{.Names}}\t{{.Ports}}" | grep "0.0.0.0"` check must be performed on NAS host |

### Operator-supplied values (never in source control)

| Value | Where stored | Verification |
|---|---|---|
| Cloudflare tunnel token | `code/infrastructure/cloudflared/.env.nas` (gitignored) | `git check-ignore -v code/infrastructure/cloudflared/.env.nas` |
| Keycloak admin password | `code/infrastructure/nas.env` or NAS secrets manager | Must not be the default `admin` password |
| Grafana admin password | `code/infrastructure/nas.env` or NAS secrets manager | Must not use `admin/admin` |
| MongoDB passwords | Dapr secretstore (Vault) plus `code/infrastructure/nas.env` for startup seeding | Must not use default `admin/admin` credentials |
| Vault root token | NAS secrets manager | Must **not** use `dev-only-token` (dev mode value) |
| MinIO root credentials | `code/infrastructure/nas.env` or NAS secrets manager | Must **not** use default `minioadmin/minioadmin` |

**Vault dev mode warning:** The local Dapr secret store uses HashiCorp Vault in dev mode (`dev-only-token`). Dev mode does not persist secrets across restarts. Before any customer data, Vault must be switched to server mode with a persistent volume. This is a prerequisite listed in OPS011 "Before customer traffic" table.

---

## What Is Verified Locally vs What Requires Public Domain

| Check area | Locally verified? | Requires public domain |
|---|---|---|
| All 8 FPS service health checks | ✅ | No |
| OIDC discovery at `auth.<domain>` | ✅ (localhost:8180) | Must re-verify at `https://auth.<domain>` |
| Employee login and token acquisition | ✅ | Must re-verify against pilot realm |
| `/me` tenant context resolution | ✅ | No (same API behavior) |
| Profile snapshot (`parkingEligible`) | ✅ | No (same API behavior) |
| Booking request submission (POST /bookings) | ✅ | No (same API behavior) |
| Booking list visibility (GET /bookings) | ✅ | No (same API behavior) |
| Draw status endpoint | ✅ | No (same API behavior) |
| Notification records after booking | ✅ | No (same API behavior) |
| Audit records after booking | ✅ | No (same API behavior) |
| Reporting summary accessible | ✅ | No (same API behavior) |
| HR operations access | ✅ | No (same API behavior) |
| Tenant readiness check (isReady=True) | ✅ | No (same API behavior) |
| **TLS active (HTTPS only)** | ❌ | **Yes — requires live Cloudflare tunnel** |
| **WAF blocks `/metrics`** | ❌ | **Yes — requires Cloudflare WAF rules active** |
| **WAF blocks Keycloak admin** | ❌ | **Yes — requires Cloudflare WAF rules active** |
| NAS ports not exposed to Internet | ❌ | **Yes — requires NAS host firewall check** |
| OIDC realm configured for public domain | 🟡 | Yes — Keycloak `Frontend URL` must be updated to `https://auth.<domain>` |

---

## Localhost Smoke Run — 2026-06-25

Run using `smoke-hosted.sh` in localhost mode (all services with Dapr, demo data seeded):

```
APP_URL=http://localhost:10000
AUTH_URL=http://localhost:8180
OIDC_REALM=fps-local
```

**Prerequisites used:**
- `dapr run -f dapr.yaml` (all 8 services with Dapr sidecars)
- `./tools/dev-seed.sh` (25 bookings, 3 employees, 1 draw)
- `source ./tools/dev-env.sh` (Auth__Authority=http://localhost:8180/realms/fps-local)

**Result (re-run after blocker fixes):**

```
=== FairSpot Hosted Smoke Evidence ===
Run at:      2026-06-25T19:44:42Z
Environment: http://localhost:10000
Auth:        http://localhost:8180
Realm:       fps-local
Mode:        localhost (TLS/WAF checks PENDING)

[PASS]    Identity :5192 → Healthy
[PASS]    Booking :5131 → Healthy
[PASS]    Notification :5157 → Healthy
[PASS]    Profile :5197 → Healthy
[PASS]    Audit :5161 → Healthy
[PASS]    Reporting :5171 → Healthy
[PASS]    Configuration :5141 → Healthy
[PASS]    Customer :5181 → Healthy

[PASS]    OIDC discovery reachable (issuer: http://localhost:8180/realms/fps-local)

[PENDING] Cloudflare TLS — run against public domain to verify
[PENDING] WAF active — localhost mode; public-domain WAF not testable here

[PASS]    Login: employee1 → token acquired [REDACTED]
[PASS]    Login: tenant-admin → token acquired [REDACTED]
[PASS]    Login: hr-admin → token acquired [REDACTED]

[PASS]    /me → tenantId=demo userId=5bc1bef8… roles=employee

[PASS]    GET /profile/snapshot → parkingEligible=True

[PASS]    POST /bookings → status=Pending requestId=6c549540…
[PASS]    GET /bookings → 6 record(s) visible

[PASS]    GET /draws/2026-06-25/status → status=NotScheduled

[PASS]    GET /notifications → 9 record(s) — Booking event reached Notification

[PASS]    GET /audit → 49 record(s) after booking

[PASS]    GET /reports/parking/summary → accessible to admin

[PASS]    GET /bookings (hr-admin) → accessible

[PASS]    GET /tenants/demo/readiness → isReady=True (failed: none)
[DEFERRED] Pilot-deferred readiness checks: ObjectStorageReadiness,BrandingReadiness

[PENDING] WAF /metrics block — localhost mode
[PENDING] WAF Keycloak admin block — localhost mode

Summary: 22 PASS / 0 FAIL / 4 PENDING / 0 SKIP  (26 total)
DEFERRED: 1 pilot limitation(s) — non-blocking
```

**Verdict:** All locally verifiable mandatory checks pass, including notification delivery (9 records). 4 checks remain PENDING until the Cloudflare tunnel and public domain are configured. No mandatory failures in localhost mode.

---

## WAF and Security Checks

These checks cannot be verified locally and must be completed before the "Before customer traffic" gate (OPS011) can be cleared.

| Check | How to verify | Status |
|---|---|---|
| `GET https://app.<domain>/metrics` returns 403 | From an external IP — not from NAS LAN | ⏳ Pending public domain |
| `GET https://app.<domain>/dapr/v1.0/invoke/fps-booking/method/health` returns 403 | From external IP | ⏳ Pending public domain |
| `GET https://app.<domain>/swagger` returns 403 | From external IP | ⏳ Pending public domain |
| `GET https://auth.<domain>/admin` returns 403 | From external IP | ⏳ Pending public domain |
| NAS internal ports not reachable from Internet | `curl -v https://<NAS-IP>:27017` etc. (from external) | ⏳ Pending NAS deployment |
| TLS grade: Full or Full (strict) | Cloudflare dashboard SSL/TLS mode | 🟡 Config-dependent |
| Always Use HTTPS enabled | Cloudflare dashboard → Edge Certificates | 🟡 Config-dependent |
| Bot Fight Mode enabled | Cloudflare dashboard → Security → Bots | 🟡 Config-dependent |

For the complete WAF rule expressions and configuration steps, see [SEC010 cloudflare-waf-profile.md](../security/cloudflare-waf-profile.md).

---

## Pilot-Deferred Items

Two readiness checks are always `Deferred` in the pilot (non-blocking for `isReady`):

| Check | Status | Follow-up |
|---|---|---|
| `ObjectStorageReadiness` | 🟠 Pilot-deferred | OPS008C tenant storage provisioning |
| `BrandingReadiness` | 🟠 Pilot-deferred | CUST010 branding asset catalog and client-config |

These are explicitly shown in the readiness response and do not block day-to-day parking operations. No bucket names, object keys, or storage paths are exposed in the check output. See [CUST008 evidence](./cust008-onboarding-e2e-evidence.md) for full classification.

---

## Gaps Before Customer Traffic

The following gaps must be resolved before the first external customer is allowed access. This list is aligned with the "Before customer traffic" table in [OPS011](./nas-cloudflare-deployment-profile.md#before-customer-traffic).

| # | Gap | Issue | Status |
|---|---|---|---|
| 1 | WAF custom rules active and verified from external IP | SEC010 #315 | ⏳ Not started |
| 2 | Public-domain OIDC realm configuration (Frontend URL, redirect URIs, CORS) | OPS012 #316 | ⏳ Not started |
| 3 | Persistent tenant-scoped storage for in-memory services | DATA010 #317 | ⏳ Not started |
| 4 | Vault in server mode (not dev mode) with persistent volume | — | ⏳ Not started |
| 5 | Public-domain smoke evidence (TLS, WAF, OIDC at `https://app.<domain>`) | OPS013 #314 | ⏳ Pending tunnel/domain |
| 6 | Hosted Dapr mTLS/service-identity evidence | — | ⏳ Not started |
| 7 | NAS/store/backup encryption-at-rest evidence | — | ⏳ Not started |

Items 1–4, 6, and 7 are prerequisites for item 5. The localhost smoke in this document satisfies the API-level evidence only.

---

## How to Complete the Public-Domain Smoke

When the Cloudflare Tunnel and domain are configured:

1. Complete [OPS011](./nas-cloudflare-deployment-profile.md) Steps 1–7 on the NAS.
2. Apply [SEC010](../security/cloudflare-waf-profile.md) WAF rules.
3. Run the hosted smoke against the public domain:

```bash
APP_URL=https://app.<domain> \
AUTH_URL=https://auth.<domain> \
OIDC_REALM=fps-pilot \
  ./tools/smoke-hosted.sh
```

4. Attach the generated `smoke-evidence-<timestamp>.txt` to the release PR.
5. Complete the [OPS013](./hosted-smoke-runbook.md) before-customer-access checklist.

---

## Document change log

| Date | Author | Change |
|---|---|---|
| 2026-06-25 | Claude | Initial OPS007 evidence document for issue #226 |
| 2026-06-25 | Claude | Fix AUTH_URL default to 8180, fix json_list_len for totalReturned, require ≥1 notification; rerun smoke |
