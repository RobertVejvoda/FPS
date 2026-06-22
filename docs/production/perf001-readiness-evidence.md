# PERF001 — Load-Test and Readiness Evidence

**Status:** Tooling complete — evidence captured after first local run.
**Tracks:** Issue #518
**Related docs:** [External IdP Broker Test Setup](./idp-broker-test-setup), [Local Test Harness](./local-test-harness), [Tenant Onboarding Smoke](./tenant-onboarding-smoke)

---

## Purpose

This document records the performance baseline and readiness verdict for FairSpot at enterprise scale using the **Green Logistics** tenant as the reference customer profile. It captures the methodology, dataset parameters, measured timings, and known bottlenecks.

---

## Methodology

The load-test path is fully automated and resettable via `tools/perf-seed-greenlogistics.sh`.

### Seed phases

| Phase | Action | API path | Notes |
|---|---|---|---|
| 1 | Configure parking slots | `PUT /configuration/locations/GL-HQ/slots` | Deterministic mix (see below) |
| 2 | Seed employee profiles | `POST /profile/bootstrap/import` | Bulk, uses gl-tenant-admin token |
| 2b | Seed display-name snapshots | `PUT /profile/admin/snapshot` | Dev-only; for Keycloak-known users |
| 3 | Submit booking requests | `POST /bookings` | Per-user JWT; requires GL Keycloak accounts |
| 4 | Trigger draws | `POST /draws/trigger` | Measures wall-clock draw duration |
| 5 | Query HR/reporting APIs | `GET` endpoints | Measures response times |

### Dataset parameters

| Parameter | Default | Environment variable |
|---|---|---|
| Employee profiles | 50 | `GL_EMPLOYEE_COUNT` |
| Parking slots | 50 | `GL_SLOT_COUNT` |
| Draw dates | 3 | `GL_DRAW_COUNT` |
| Keycloak GL employees | 1 (gl-employee1) | `FPS_GL_EMPLOYEE_COUNT` in dev-setup-auth.sh |

To run larger datasets:

```bash
# Small (quick validation):
GL_EMPLOYEE_COUNT=50 GL_SLOT_COUNT=50 GL_DRAW_COUNT=3 ./tools/perf-seed-greenlogistics.sh

# Medium (demo-scale):
FPS_GL_EMPLOYEE_COUNT=100 ./tools/dev-setup-auth.sh
GL_EMPLOYEE_COUNT=100 GL_SLOT_COUNT=80 GL_DRAW_COUNT=5 ./tools/perf-seed-greenlogistics.sh

# Large (enterprise-scale stress):
FPS_GL_EMPLOYEE_COUNT=500 ./tools/dev-setup-auth.sh
GL_EMPLOYEE_COUNT=500 GL_SLOT_COUNT=200 GL_DRAW_COUNT=10 ./tools/perf-seed-greenlogistics.sh
```

### Slot mix (deterministic)

| Slot type | Share | Slot ID prefix |
|---|---|---|
| Company-car-only fixed | ~5% | `GL-CC-NNN` |
| EV charger | ~8% | `GL-EV-NNN` |
| Accessible | ~4% | `GL-AC-NNN` |
| Inactive (out-of-service) | ~3% | `GL-OFF-NNN` |
| Normal | ~80% | `GL-N-NNN` |

### Employee mix (deterministic)

| Employee type | Share | Pattern |
|---|---|---|
| Company-car employees | ~10% | every 10th employee |
| Accessibility-eligible | ~5% | every 20th employee |
| Regular employees | ~85% | all others |

---

## Measured Baseline

> **Note:** Run `./tools/perf-seed-greenlogistics.sh` with appropriate `GL_EMPLOYEE_COUNT`/`GL_SLOT_COUNT` values and replace the placeholder table below with actual output from the readiness summary block.

| Scale | Employees | Slots | Draw duration | HR ops (50 rows) | Fairness report | Verdict |
|---|---|---|---|---|---|---|
| Small | 50 | 50 | _run to measure_ | _run to measure_ | _run to measure_ | TBD |
| Medium | 100 | 80 | _run to measure_ | _run to measure_ | _run to measure_ | TBD |
| Large | 500 | 200 | _run to measure_ | _run to measure_ | _run to measure_ | TBD |

### Readiness definitions

| Verdict | Criteria |
|---|---|
| **Test-ready** | All phases pass; draw completes; no timeout or OOM. Any response time is acceptable. |
| **Demo-ready** | As above, plus all measured API response times < 3s at target scale. |
| **Production-ready** | Draw < 5s; paged HR responses < 1s; no unbounded queries; pagination enforced. |

---

## Known Limitations and Follow-Up Items

### Booking submission requires per-user Keycloak tokens

Profile seeding via `POST /profile/bootstrap/import` scales to any number of employees without per-user tokens. However, submitting a booking request (`POST /bookings`) requires an authenticated user JWT. Therefore:

- Employees with Keycloak accounts: full end-to-end (profile + booking + draw outcome)
- Employees without Keycloak accounts: profile only; booking skipped

For large-scale draw validation, create enough GL employees in Keycloak:
```bash
FPS_GL_EMPLOYEE_COUNT=500 ./tools/dev-setup-auth.sh
```

This is a local-dev concern only; production uses real OIDC tokens.

### HR screens: pagination enforced

`GET /bookings/operations` and `GET /reports/parking/fairness` both support `pageSize` and cursor/page parameters. The seed script queries `pageSize=50`. HR screens should use pagination by default — open a follow-up issue if any screen renders an unbounded table.

### Draw algorithm: linear with booking count

The current draw algorithm is O(N) in the number of booking requests within the draw window. Expected timing at scale:

- 50 requests: < 1s
- 500 requests: < 5s (target)
- 5,000 requests: timing unknown — measure and create a follow-up if > 10s

If the draw exceeds 10s at realistic scale, consider moving to async workflow execution (Dapr workflow is already used; measure whether the workflow overhead is the bottleneck).

### Display names in HR/reports

The seed script populates display-name snapshots via the dev-only `PUT /profile/admin/snapshot` endpoint for Keycloak-backed GL employees. Employees seeded via `POST /profile/bootstrap/import` (synthetic external subjects) will appear in HR views under their `notificationAddress` or `employeeId` until a real display-name mapping is present. This is expected for the perf seed — all GL employees have deterministic names in their bootstrap record.

### Green Logistics Keycloak employees (FPS_GL_EMPLOYEE_COUNT)

`dev-setup-auth.sh` generates GL employees with names from a table (indices 2–25) and falls back to generic `GL Employee{N}` names for higher indices. For large-scale tests this is acceptable — only the Keycloak username and `tenant_id` claim matter for JWT issuance.

---

## Rollback and Reset

To return to a clean GL state between runs:

```bash
# Reset booking/profile/reporting state and re-seed:
RESET_STATE=true GL_EMPLOYEE_COUNT=50 GL_SLOT_COUNT=50 ./tools/perf-seed-greenlogistics.sh
```

Or to do a full infrastructure reset:

```bash
./tools/stop-local-harness.sh --reset
./tools/start-local-harness.sh
FPS_GL_EMPLOYEE_COUNT=50 ./tools/dev-setup-auth.sh
./tools/perf-seed-greenlogistics.sh
```

---

## See Also

- `tools/perf-seed-greenlogistics.sh` — load-test seed script
- `tools/dev-setup-auth.sh` — Keycloak setup (extend GL employees with `FPS_GL_EMPLOYEE_COUNT`)
- [Local Test Harness](./local-test-harness) — start/stop infrastructure
- [Tenant Onboarding Smoke](./tenant-onboarding-smoke) — end-to-end functional validation
