# Demo Seed Data Reference

This document describes the **Green Logistics** showcase data seeded by `./tools/dev-seed.sh` and which endpoints each demo user can exercise. Green Logistics (`tenant_id=greenlogistics`, location `GL-HQ`) is the default guided-evaluation showcase: small, synthetic, and narrative-driven. Load/performance data and per-prospect pilot data use separate paths.

## Quick start

```bash
./tools/start-local-harness.sh   # start all services + Dapr sidecars
./tools/dev-seed.sh              # seed the Green Logistics showcase: slots, profiles, vehicles, bookings, parking Draw, and Seats Draw
```

By default `dev-seed.sh` clears local persisted demo state before it seeds, then creates the current showcase story. Set `FPS_DEV_SEED_RESET_STATE=false` only when intentionally appending to existing local state. For a full volume reset, see [Resetting seed data](#resetting-seed-data).

---

## Demo users (Green Logistics)

The default seed creates 10 named Green Logistics employees (`gl-employee1..10`) plus four role users. The roster is deliberately small enough to understand in one screen while still exercising the main allocation paths.

| Username | Name | Roles | Parking | Vehicle | Demo purpose |
|----------|------|-------|---------|---------|-------------|
| `gl-employee1` | Jan Novak | employee | ✅ eligible + **company car** | Sedan `1AB 2345` | Company-car Tier-1 fixed-slot precedence (`VIP-01`) |
| `gl-employee2` | Petra Svobodova | employee | ✅ eligible | EV sedan `2SC 4417` | EV charger-slot preference (`EV-01`) |
| `gl-employee3` | Hana Vesela | employee | ✅ eligible + **accessible** | Sedan `5BL 6628` | Accessibility slot (`ACC-01`) |
| `gl-employee4` | Tomas Dvorak | employee | ✅ eligible | Motorcycle `3AH 8820` | Motorcycle capacity (`MOTO-01`) |
| `gl-employee5` | Pavel Cerny | employee | ✅ eligible | Sedan `4EK 1193` | General fair-lottery demand |
| `gl-employee6` | Martin Horak | employee | ✅ eligible | Sedan `1AP 3092` | General fair-lottery demand |
| `gl-employee7` | Jana Kucerova | employee | ✅ eligible | Sedan `6CT 7741` | Seeded recent-winner history lowers fair weight |
| `gl-employee8` | Petr Novotny | employee | ✅ eligible | Sedan `7AZ 2284` | Seeded late-cancellation penalty lowers fair weight |
| `gl-employee9` | Lenka Maresova | employee | ✅ eligible | Sedan `3BM 9087` | General fair-lottery demand |
| `gl-employee10` | Michal Prochazka | employee | ✅ eligible | Sedan `4EH 4451` | General fair-lottery demand |
| `gl-hr-admin` | Lucie Prochazkova | employee, hr_manager | ❌ | — | Reports, configuration, HR import |
| `gl-tenant-admin` | Karel Urban | admin | ❌ | — | Tenant admin console, readiness, guided setup |
| `gl-report-viewer` | Eva Kralova | report_viewer | ❌ | — | Read-only reports access |
| `gl-auditor` | Martin Cerny | auditor | ❌ | — | Audit log review |

If `FPS_GL_EMPLOYEE_COUNT` is set above 10, the extra `gl-employee*` users are local-only generic drivers for experiments. Use `tools/perf-seed-greenlogistics.sh` for real load/performance validation instead of expanding the default showcase.

Password for all demo users: `Dev1234!` (local Keycloak realm `fps-local` only).

The two login paths an evaluator sees on the sign-in screen — **Company SSO** (work-email tenant discovery, the `greenlogistics.example` domain) and **FairSpot account** (local accounts) — are explained in [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes).

---

## Parking facility (`GL-HQ`)

`dev-seed.sh` configures six human-labelled slots at `GL-HQ` (the `slotId` is what the parking map and HR views render — there is no separate label field):

| Slots | Count | Capability |
|-------|-------|-----------|
| `A-01`..`A-02` | 2 | General (fair Tier-2 Draw) |
| `EV-01` | 1 | EV charger |
| `ACC-01` | 1 | Accessible |
| `VIP-01` | 1 | Company-car only — reserved for the company-car employee at seed time |
| `MOTO-01` | 1 | Motorcycle area |

`VIP-01` is stamped with the resolved Keycloak `sub` of the company-car employee during the seed (this drives the HR config and parking-map views).

> **Slot source (#666):** Booking submission and the Draw read slot capacity from the Configuration-service slots seeded here, over Dapr — **not** the Booking service's static `appsettings.AvailableSlots`. So the curated six-slot layout and the company-car reservation drive Draw allocation: a live seed produces visible allocated/waitlisted outcomes, and the `verify_demo_draw` gate asserts them.

## Seats module showcase

Green Logistics runs Parking as its primary module and Seats as an enabled secondary module. `dev-seed.sh` configures eight team seats at `GL-TEAMS` (`HQ-TEAM-A-01`..`HQ-TEAM-A-08`) and submits seat requests for the 10 employees, producing allocated seats plus a visible waitlist. This proves the multi-resource direction without mixing seats into the parking location.

---

## The `demo` tenant (opt-in isolation fixture)

The default `./tools/dev-setup-auth.sh` provisions **only Green Logistics** — the legacy `demo` employee population is no longer part of the normal evaluator experience (#668). The `demo` tenant (`tenant_id=demo`) is an **opt-in cross-tenant isolation fixture**, enabled with:

```bash
FPS_INCLUDE_DEMO_TENANT=1 ./tools/dev-setup-auth.sh
```

When enabled it adds a single `demo` **`tenant-admin`** (no profile/booking/draw data) — enough to prove tenant isolation (a `greenlogistics` user must never see `demo` data, and vice-versa). It is never seeded with business data; the seeded booking/Draw evidence lives entirely in Green Logistics. If a test genuinely needs `demo` employees, add them explicitly with `FPS_DEMO_EMPLOYEE_COUNT=<n>` (which also enables the fixture).

A separate canonical provisioning sample (`gl-v1`) can be seeded into a freshly provisioned **Sandbox/Evaluation** tenant via the tenant-admin endpoint `POST /tenants/{tenantId}/demo-seed` (Customer service); it does not auto-create bookings/draws.

---

## Seeded bookings

After running `dev-seed.sh`, each of the 10 default Green Logistics employees has one parking request for the `GL-HQ` facility, dated the next workday at least +2 days out (08:00–18:00). The script then triggers the Draw for that date.

The allocation (realised in the live Draw — #666; asserted by the `verify_demo_draw` gate):

- **Company-car holder** (`gl-employee1`) takes `VIP-01` through Tier-1 fixed-slot allocation — not the Tier-2 fairness lottery.
- **Everyone else** competes in the Draw for the general / EV / accessible / motorcycle slots, producing a mix of allocated and waitlisted outcomes (demand > capacity).
- The **accessibility** request (`gl-employee3`) prefers `ACC-01`; the **EV** request (`gl-employee2`) prefers `EV-01`; the **motorcycle** request (`gl-employee4`) takes `MOTO-01`.
- `gl-employee7` has seeded recent-winner history and `gl-employee8` has an active late-cancellation penalty, so the fair outcome is explainable in HR, reports, and audit views.
- The seed cancels one allocated general request after the Draw and verifies that the next fair waitlisted employee is promoted.

All bookings use dates ≥+2 days to stay clear of the draw cutoff that applies to same-day/+1 requests, ensuring they enter the Draw regardless of time of day.

When a company-car employee is shown in the demo, explain the business rule explicitly: the employee cannot mark their own vehicle as a company car, and their fixed company-car slot is assigned by HR/facilities. If they submit the request on time and the assigned slot is active and compatible, the space is ready for them before the fairness Draw runs for the remaining spaces.

These bookings trigger Dapr pub/sub events (when sidecars are running), which populate:
- **Notifications**: each booking creates an in-app notification for the requestor
- **Audit records**: booking events are appended to the audit log
- **Reporting**: booking data feeds `GET /reports/parking/summary` and related endpoints

---

## Vehicle selection

Each default employee has one active default vehicle so the showcase stays legible. The vehicle list is available via `GET /profile/snapshot` → `vehicles[]`. `gl-employee2` carries the EV used for `EV-01`; `gl-employee4` carries the motorcycle used for `MOTO-01`.

---

## Verifying seed data

```bash
# Employee experience
TOKEN=$(./tools/dev-auth.sh gl-employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count | python3 -m json.tool

# Draw status
TOKEN=$(./tools/dev-auth.sh gl-tenant-admin)
DATE=$(date -v+2d +%Y-%m-%d 2>/dev/null || date -d '+2 days' +%Y-%m-%d)
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:10000/draws/$DATE/status?locationId=GL-HQ&timeSlotStart=${DATE}T08:00:00&timeSlotEnd=${DATE}T18:00:00" \
  | python3 -m json.tool

# Admin/reporting experience
TOKEN=$(./tools/dev-auth.sh gl-tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/tenants/greenlogistics/readiness | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh gl-report-viewer)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/summary | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh gl-auditor)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/audit | python3 -m json.tool
```

---

## Resetting seed data

> **After PERSIST001–006, several stores are durable.** Configuration, Profile, Audit, Notification, Booking fairness metrics, and the DataHub read models are backed by MongoDB/PostgreSQL (Dapr state stores and EF Core), so they **survive a service restart**. Restarting services alone no longer gives a clean demo. Re-running `dev-seed.sh` with its default reset recreates the local showcase; running it with `FPS_DEV_SEED_RESET_STATE=false` intentionally appends data and can accumulate duplicate bookings, notifications, and audit records.

| Action | Effect |
|---|---|
| Re-run `dev-seed.sh` | Default behavior clears local persisted demo state, then recreates the showcase. With `FPS_DEV_SEED_RESET_STATE=false`, profile snapshots and slots are overwritten but bookings, notifications, audit, and projections may accumulate. |
| Restart services only | Durable stores above are **kept**; only intentional in-memory stubs (e.g. the simulation clock) reset. |
| Full reset (clear data volumes) | Removes all durable state for a clean demo. |

**Clean reset — developer harness** (`stop-local-harness.sh --reset` runs `docker compose down -v`, removing the data volumes):

```bash
./tools/stop-local-harness.sh --reset
./tools/start-local-harness.sh
./tools/dev-seed.sh
```

**Clean reset — container stack:**

```bash
./tools/start-container-stack.sh --down
docker volume rm $(docker volume ls -q | grep fps)
./tools/start-container-stack.sh --seed
```

`start-container-stack.sh --seed` starts the local container services in `Development` mode so the dev-only seed endpoints and OpenAPI probes are available. The default container stack and the NAS profile remain Production-like. `dev-seed.sh` configures the `GL-HQ` slots itself, so a clean reset followed by the seed reproduces the full Green Logistics demo.

> **Evidence:** durable-store list per [OPS008 Persistence Profile](./production/ops008-persistence-profile) and the merged PERSIST001–006 slices; `bookingstore` is a MongoDB-backed Dapr component. This guidance is from a static review of the persistence docs/components — confirm exact per-store behavior by running the stack.

---

## Distinction: demo seed vs pilot import

| Path | Use case |
|------|---------|
| `./tools/dev-seed.sh` | Default Green Logistics showcase for local development, guided evaluation, and static product-tour evidence — small synthetic story, dev-only |
| `FPS_INCLUDE_DEMO_TENANT=1 ./tools/dev-setup-auth.sh` | Tiny `demo` tenant isolation fixture — one admin login, no seeded business data |
| `tools/perf-seed-greenlogistics.sh` | Load/performance validation — explicit synthetic bulk path, not the customer showcase |
| `tools/validate-hr-import.sh` + `POST /profile/bootstrap` | Pilot/production bootstrap — uses the HR CSV import contract, respects tenant scoping and auth |
| Tenant-admin demo seed action | Controlled self-test path for a synthetic sandbox tenant only; must require tenant-admin authorization and must not expose public anonymous seeding |

Never run `dev-seed.sh` against a production or pilot environment.
