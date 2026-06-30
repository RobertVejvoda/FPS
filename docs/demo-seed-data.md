# Demo Seed Data Reference

This document describes the **Green Logistics** demo data seeded by `./tools/dev-seed.sh` and which endpoints each demo user can exercise. Green Logistics (`tenant_id=greenlogistics`, location `GL-HQ`) is the single seeded guided-pilot demo.

## Quick start

```bash
./tools/start-local-harness.sh   # start all services + Dapr sidecars
./tools/dev-seed.sh              # seed the Green Logistics demo: slots, profiles, vehicles, bookings, and run the Draw
```

Profile re-seeding is safe (idempotent). Bookings, notifications, and audit records are stored in durable Dapr state (MongoDB), so re-running the seed **accumulates** data rather than replacing it. When you need a clean demo, do a full reset — see [Resetting seed data](#resetting-seed-data).

---

## Demo users (Green Logistics)

The seed creates 25 Green Logistics employees (`gl-employee1..25`) plus four role users. The roster deliberately mixes — and combines — the special cases so one Draw exercises every allocation path. Highlights:

| Username | Name | Roles | Parking | Vehicles | Demo purpose |
|----------|------|-------|---------|----------|-------------|
| `gl-employee1` | Jan Novak | employee | ✅ eligible + **company car** | Company car (`1AB 2345`, EV) | Company-car Tier-1 fixed-slot precedence |
| `gl-employee2` | Petra Svobodova | employee | ✅ eligible | EV (`2SC 4417`) | EV charger-slot preference |
| `gl-employee3` | Tomas Dvorak | employee | ✅ eligible | **Two vehicles**: car (`3AH 8820`) + motorcycle (`3AH 0143`) | Multi-vehicle selection; books the default car |
| `gl-employee5` | Hana Vesela | employee | ✅ eligible + **accessible** | EV (`5BL 6628`) | Accessibility **+ EV** combined case |
| `gl-employee8` | Petr Svoboda | employee | ✅ eligible + **company car** | Company car (`2SD 5510`, EV) | Company-car **+ EV** combined case |
| `gl-employee20` | David Vacek | employee | ✅ eligible | Motorcycle (`7AM 9921`) | Shared motorcycle area (`MOTO-01`) |
| `gl-hr-admin` | Lucie Prochazkova | employee, hr_manager | ❌ | — | Reports, configuration, HR import |
| `gl-tenant-admin` | Karel Urban | admin | ❌ | — | Tenant admin console, readiness, guided setup |
| `gl-report-viewer` | Eva Kralova | report_viewer | ❌ | — | Read-only reports access |
| `gl-auditor` | Martin Cerny | auditor | ❌ | — | Audit log review |

The remaining `gl-employee*` are regular-car employees who compete in the fair Tier-2 Draw. The full roster — 2 company-car (one EV), 2 accessibility (one EV), 5 EV, 1 motorcycle, 2 multi-vehicle, the rest regular — and the per-employee plates/attributes are defined in `tools/dev-seed.sh`.

Password for all demo users: `Dev1234!` (local Keycloak realm `fps-local` only).

The two login paths an evaluator sees on the sign-in screen — **Company SSO** (work-email tenant discovery, the `greenlogistics.example` domain) and **FairSpot account** (local accounts) — are explained in [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes).

---

## Parking facility (`GL-HQ`)

`dev-seed.sh` configures 20 human-labelled slots at `GL-HQ` (the `slotId` is what the parking map and HR views render — there is no separate label field):

| Slots | Count | Capability |
|-------|-------|-----------|
| `A-01`..`A-13` | 13 | General (fair Tier-2 Draw) |
| `EV-01`..`EV-03` | 3 | EV charger |
| `ACC-01` | 1 | Accessible |
| `VIP-01`..`VIP-02` | 2 | Company-car only — reserved per company-car employee at seed time |
| `MOTO-01` | 1 | Motorcycle area (holds 4) |

The two `VIP-*` slots are stamped with the resolved Keycloak `sub` of the company-car employees during the seed (this drives the HR config and parking-map views).

> **Known limitation (#665):** the Booking submission and Draw currently read slot capacity from the Booking service's own static `appsettings.AvailableSlots`, **not** the Configuration-service slots seeded here. So the curated 20-slot layout and the company-car reservations do **not** yet drive Draw allocation, and a live seed does not currently produce visible allocated/waitlisted Draw outcomes. Wiring the seeded slots/reservations into the Draw is tracked in #665.

---

## The `demo` tenant (isolation scaffold)

FairSpot still provisions a second tenant, **`demo`** (`tenant_id=demo`, users `employee1..25` / `hr-admin` / `tenant-admin` / `report-viewer` / `auditor`), but it is **not** seeded with profile/booking/draw data. It exists as a bare scaffold so tenant-isolation behaviour can be demonstrated (a `greenlogistics` user must never see `demo` data, and vice-versa). Use it only for isolation/SSO-discovery testing — the seeded booking/Draw evidence lives entirely in Green Logistics.

A separate canonical provisioning sample (`gl-v1`) can be seeded into a freshly provisioned **Sandbox/Evaluation** tenant via the tenant-admin endpoint `POST /tenants/{tenantId}/demo-seed` (Customer service); it does not auto-create bookings/draws.

---

## Seeded bookings

After running `dev-seed.sh`, each of the 25 Green Logistics employees has one booking request for the `GL-HQ` facility, dated the next workday at least +2 days out (08:00–18:00). The script then triggers the Draw for that date.

The **intended** allocation (subject to the #665 limitation above — not yet realised in the live Draw):

- **Company-car holders** (`gl-employee1`, `gl-employee8`) take their `VIP-*` Tier-1 fixed slot immediately on submission — not the Tier-2 fairness lottery.
- **Everyone else** competes in the Draw for the general / EV / accessible slots, producing a mix of allocated, waitlisted, and rejected outcomes (demand > capacity).
- The **accessibility** request (`gl-employee5`) prefers `ACC-01`; **EV** requests prefer the `EV-*` charger slots; the **motorcycle** request (`gl-employee20`) takes `MOTO-01`.

All bookings use dates ≥+2 days to stay clear of the draw cutoff that applies to same-day/+1 requests, ensuring they enter the Draw regardless of time of day.

When a company-car employee is shown in the demo, explain the business rule explicitly: the employee cannot mark their own vehicle as a company car, and their fixed company-car slot is assigned by HR/facilities. If they submit the request on time and the assigned slot is active and compatible, the space is ready for them before the fairness Draw runs for the remaining spaces.

These bookings trigger Dapr pub/sub events (when sidecars are running), which populate:
- **Notifications**: each booking creates an in-app notification for the requestor
- **Audit records**: booking events are appended to the audit log
- **Reporting**: booking data feeds `GET /reports/parking/summary` and related endpoints

---

## Vehicle selection

`gl-employee3` / Tomas Dvorak has two registered vehicles. In the booking flow, clients can pick between:
- `3AH 8820` — default car, non-electric
- `3AH 0143` — motorcycle

The vehicle list is available via `GET /profile/snapshot` → `vehicles[]`. EV employees (e.g. `gl-employee2`) and the company-car/EV employees (`gl-employee1`, `gl-employee8`) carry electric vehicles eligible for the `EV-*` charger slots.

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

> **After PERSIST001–006, several stores are durable.** Configuration, Profile, Audit, Notification, Booking fairness metrics, and the DataHub read models are backed by MongoDB/PostgreSQL (Dapr state stores and EF Core), so they **survive a service restart**. Restarting services alone no longer gives a clean demo — re-running `dev-seed.sh` accumulates duplicate bookings, notifications, and audit records.

| Action | Effect |
|---|---|
| Re-run `dev-seed.sh` | Profile snapshots and `GL-HQ` slots are overwritten (idempotent); new bookings, notifications, and audit records are **added** on top of existing data. |
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
| `./tools/dev-seed.sh` | Local development and demo smoke testing — uses internal admin endpoints, deterministic, dev-only |
| `tools/validate-hr-import.sh` + `POST /profile/bootstrap` | Pilot/production bootstrap — uses the HR CSV import contract, respects tenant scoping and auth |
| Tenant-admin demo seed action | Controlled self-test path for a synthetic sandbox tenant only; must require tenant-admin authorization and must not expose public anonymous seeding |

Never run `dev-seed.sh` against a production or pilot environment.
