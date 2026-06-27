# Demo Seed Data Reference

This document describes the demo data seeded by `./tools/dev-seed.sh` and which endpoints each demo user can exercise.

## Quick start

```bash
./tools/start-local-harness.sh   # start all services + Dapr sidecars
./tools/dev-seed.sh              # seed profiles, vehicles, bookings, and run the demo Draw
```

Profile re-seeding is safe (idempotent). Bookings, notifications, and audit records are now stored in durable Dapr state (MongoDB), so re-running the seed **accumulates** data rather than replacing it. When you need a clean demo, do a full reset — see [Resetting seed data](#resetting-seed-data).

---

## Demo users

| Username | Name | Roles | Parking | Vehicles | Demo purpose |
|----------|------|-------|---------|----------|-------------|
| `employee1` | Jan Novak | employee | ✅ eligible | Daily Driver (`1AA 2345`), EV Commuter (`2AB 3456`) | Standard employee — booking, vehicle selection, notifications |
| `employee2` | Petra Svobodova | employee | ✅ eligible + company car | Company Fleet (`3AC 4567`) | Company-car fixed-slot policy demonstration |
| `employee3` | Tomas Dvorak | employee | ✅ eligible + accessible | Accessible (`4AD 5678`) | Accessibility-eligible booking path |
| `hr-admin` | Lucie Prochazkova | employee, hr_manager | ❌ | — | Reports, configuration, HR import |
| `tenant-admin` | Karel Urban | admin | ❌ | — | Tenant admin console, readiness, guided setup |
| `report-viewer` | Eva Kralova | report_viewer | ❌ | — | Read-only reports access |
| `auditor` | Martin Cerny | auditor | ❌ | — | Audit log review |

Password for all demo users: `Dev1234!` (local Keycloak realm `fps-local` only).

The two login paths an evaluator sees on the sign-in screen — **Company SSO** (work-email tenant discovery) and **FairSpot account** (local/demo accounts) — are explained in [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes). Demo users above use the FairSpot-account path.

---

## Green Logistics tenant (second demo tenant)

FairSpot provisions a second tenant, **Green Logistics** (`tenant_id=greenlogistics`), used to demonstrate **company-SSO / work-email tenant discovery** (the `greenlogistics.example` domain) and **multi-tenant isolation** (a `greenlogistics` user must never see `demo` tenant data). Three seed paths are distinct:

- **`./tools/dev-setup-auth.sh`** — provisions Green Logistics **identity users** (`gl-*`) in the `fps-local` realm.
- **`./tools/dev-seed.sh`** — seeds the **`demo`** tenant's profile/vehicle/booking dataset. It does **not** touch Green Logistics.
- **Green Logistics demo dataset (`gl-v1`)** — a canonical dataset (employees, vehicles, ~20 parking slots including EV-charger, accessible, company-car/reserved, and motorcycle-capacity, plus policy) seeded through the tenant-admin demo-seed endpoint `POST /tenants/{tenantId}/demo-seed` (Customer service). It does **not** auto-create historical bookings/draws — run a Draw manually after seeding.

| Username | Name | Role intent | Tenant |
|----------|------|-------------|--------|
| `gl-employee1` | Alice Green | Employee | greenlogistics |
| `gl-tenant-admin` | — | Tenant admin | greenlogistics |
| `gl-hr-admin` | — | HR / reports | greenlogistics |
| `gl-auditor` | — | Auditor | greenlogistics |
| `gl-report-viewer` | — | Report viewer | greenlogistics |

Roles mirror the demo-tenant equivalents. All Green Logistics users share the password `Dev1234!`. Mint a token with `./tools/dev-auth.sh gl-employee1`. Add more Green Logistics employees for load testing with `FPS_GL_EMPLOYEE_COUNT=N ./tools/dev-setup-auth.sh` (`gl-employee1` is always present).

**Which tenant to use:**

| Use the `demo` tenant when… | Use `greenlogistics` when… |
|---|---|
| Demonstrating the full employee booking / Draw / notification / audit flow out of the box — `dev-seed.sh` populates its profiles, vehicles, and bookings below. | Demonstrating company-SSO / work-email tenant discovery or tenant isolation, or seeding the canonical `gl-v1` dataset via the tenant-admin demo-seed endpoint and running a Draw. |

---

## Seeded bookings

After running `dev-seed.sh`, 7 booking requests exist for the `Headquarters` facility in `Prague`. The script also runs the Draw for the +2 date from 08:00-18:00.

| Employee | Vehicle | Days ahead | Expected after seed |
|----------|---------|-----------|-------|
| Jan Novak | 1AA 2345 (Daily Driver) | +2 | Demo Draw participant |
| Petra Svobodova | 3AC 4567 (Company Fleet) | +2 | Fixed company-car allocation when the assigned slot is configured |
| Tomas Dvorak | 4AD 5678 (Accessible) | +2 | Demo Draw participant |
| Jan Novak | 2AB 3456 (EV Commuter) | +4 | Pending EV request |
| Jan Novak | 1AA 2345 (Daily Driver) | +6 | Additional booking |
| Petra Svobodova | 3AC 4567 (Company Fleet) | +5 | Company car priority |
| Tomas Dvorak | 4AD 5678 (Accessible) | +4 | Accessible spot request |

All bookings use dates ≥+2 days to stay clear of the draw cutoff that applies to same-day/+1 requests, ensuring they land as `Pending` regardless of time of day.

For the local development demo, the seed must include both company-car and non-company-car employees. Company-car requests demonstrate the fixed HR-assigned slot rule and do not participate in the Tier 2 fairness lottery. Non-company-car requests demonstrate scarce-capacity fairness and may be allocated, pending, waitlisted, or rejected according to the configured slots and Draw key.

When a company-car employee is shown in the demo, explain the business rule explicitly: the employee cannot mark their own vehicle as a company car, and their fixed company-car slot is assigned by HR/facilities. If they submit the request on time and the assigned slot is active and compatible, the space is ready for them before the fairness Draw runs for remaining spaces.

These bookings trigger Dapr pub/sub events (when sidecars are running), which populate:
- **Notifications**: each booking creates an in-app notification for the requestor
- **Audit records**: booking events are appended to the audit log
- **Reporting**: booking data feeds `GET /reports/parking/summary` and related endpoints

---

## Vehicle selection

`employee1` / Jan Novak has two registered vehicles. In the booking flow, clients can pick between:
- `1AA 2345` — Daily Driver, standard car, non-electric
- `2AB 3456` — EV Commuter, electric car eligible for EV space allocation

The vehicle list is available via `GET /profile/snapshot` → `vehicles[]`.

---

## Verifying seed data

```bash
# Employee experience
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count | python3 -m json.tool

# Demo Draw status
TOKEN=$(./tools/dev-auth.sh tenant-admin)
DATE=$(date -v+2d +%Y-%m-%d 2>/dev/null || date -d '+2 days' +%Y-%m-%d)
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:10000/draws/$DATE/status?locationId=Prague&timeSlotStart=${DATE}T08:00:00&timeSlotEnd=${DATE}T18:00:00" \
  | python3 -m json.tool

# Admin/reporting experience
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/tenants/demo/readiness | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh report-viewer)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/summary | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh auditor)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/audit | python3 -m json.tool
```

---

## Resetting seed data

> **After PERSIST001–006, several stores are durable.** Configuration, Profile, Audit, Notification, Booking fairness metrics, and the DataHub read models are backed by MongoDB/PostgreSQL (Dapr state stores and EF Core), so they **survive a service restart**. Restarting services alone no longer gives a clean demo — re-running `dev-seed.sh` accumulates duplicate bookings, notifications, and audit records.

| Action | Effect |
|---|---|
| Re-run `dev-seed.sh` | Profile snapshots are overwritten (idempotent); new bookings, notifications, and audit records are **added** on top of existing data. |
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

`start-container-stack.sh --seed` starts the local container services in `Development` mode so the dev-only seed endpoints and OpenAPI probes are available. The default container stack and the NAS profile remain Production-like.

Configuration (policy + slots at `Prague`) is re-seeded automatically by the Configuration service on startup when its store is empty after a full reset.

> **Evidence:** durable-store list per [OPS008 Persistence Profile](./production/ops008-persistence-profile) and the merged PERSIST001–006 slices; `bookingstore` is a MongoDB-backed Dapr component. This guidance is from a static review of the persistence docs/components — confirm exact per-store behavior by running the stack.

---

## Distinction: demo seed vs pilot import

| Path | Use case |
|------|---------|
| `./tools/dev-seed.sh` | Local development and demo smoke testing — uses internal admin endpoints, deterministic, dev-only |
| `tools/validate-hr-import.sh` + `POST /profile/bootstrap` | Pilot/production bootstrap — uses the HR CSV import contract, respects tenant scoping and auth |
| Tenant-admin demo seed action | Future controlled self-test path for a synthetic sandbox tenant only; must require tenant-admin authorization and must not expose public anonymous seeding |

Never run `dev-seed.sh` against a production or pilot environment.
