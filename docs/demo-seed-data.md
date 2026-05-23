# Demo Seed Data Reference

This document describes the demo data seeded by `./tools/dev-seed.sh` and which endpoints each demo user can exercise.

## Quick start

```bash
./tools/start-local-harness.sh   # start all services + Dapr sidecars
./tools/dev-seed.sh              # seed profiles, vehicles, and bookings
```

Re-seeding is safe — the script is idempotent.

---

## Demo users

| Username | Roles | Parking | Vehicles | Demo purpose |
|----------|-------|---------|----------|-------------|
| `employee1` | employee | ✅ eligible | Sedan (EMP1001), EV (EMP1002) | Standard employee — booking, vehicle selection, notifications |
| `employee2` | employee | ✅ eligible | Company car | Company-car priority policy demonstration |
| `employee3` | employee | ✅ eligible + accessible | Sedan (EMP3001) | Accessibility-eligible booking path |
| `hr-admin` | employee, hr_manager | ❌ | — | Reports, configuration, HR import |
| `tenant-admin` | admin | ❌ | — | Tenant admin console, readiness, guided setup |
| `report-viewer` | report_viewer | ❌ | — | Read-only reports access |
| `auditor` | auditor | ❌ | — | Audit log review |

Password for all demo users: `Dev1234!` (local Keycloak only).

---

## Seeded bookings

After running `dev-seed.sh`, 7 pending booking requests exist:

| Employee | Vehicle | Days ahead | Notes |
|----------|---------|-----------|-------|
| employee1 | EMP1001 (Sedan) | +2 | Standard booking |
| employee1 | EMP1002 (EV) | +4 | EV space preference |
| employee1 | EMP1001 (Sedan) | +6 | Additional booking |
| employee2 | COMPANY001 | +3 | Company car priority |
| employee2 | COMPANY001 | +5 | Company car priority |
| employee3 | EMP3001 | +2 | Accessible spot request |
| employee3 | EMP3001 | +4 | Accessible spot request |

All bookings use dates ≥+2 days to stay clear of the draw cutoff that applies to same-day/+1 requests, ensuring they land as `Pending` regardless of time of day.

These bookings trigger Dapr pub/sub events (when sidecars are running), which populate:
- **Notifications**: each booking creates an in-app notification for the requestor
- **Audit records**: booking events are appended to the audit log
- **Reporting**: booking data feeds `GET /reports/parking/summary` and related endpoints

---

## Vehicle selection

`employee1` has two registered vehicles. In the booking flow, clients can pick between:
- `EMP1001` — standard sedan, non-electric
- `EMP1002` — electric sedan (eligible for EV space allocation)

The vehicle list is available via `GET /profile/snapshot` → `vehicles[]`.

---

## Verifying seed data

```bash
# Employee experience
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count | python3 -m json.tool

# Admin/reporting experience
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/tenants/tenant-1/readiness | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh report-viewer)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/summary | python3 -m json.tool

TOKEN=$(./tools/dev-auth.sh auditor)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/audit | python3 -m json.tool
```

---

## Resetting seed data

Profile snapshots are overwritten on each run of `dev-seed.sh` (idempotent).

Booking requests are **not** idempotent — repeated `dev-seed.sh` runs create duplicate future requests because the booking service has no admin-delete endpoint. To reset booking data, restart the services (the in-memory store is cleared on shutdown):

```bash
./tools/stop-local-harness.sh
./tools/start-local-harness.sh
./tools/dev-seed.sh
```

Configuration (policy + slots at `LOC-MAIN`) is re-seeded automatically by the Configuration service on startup.

---

## Distinction: demo seed vs pilot import

| Path | Use case |
|------|---------|
| `./tools/dev-seed.sh` | Local development and demo smoke testing — uses internal admin endpoints, deterministic, dev-only |
| `tools/validate-hr-import.sh` + `POST /profile/bootstrap` | Pilot/production bootstrap — uses the HR CSV import contract, respects tenant scoping and auth |

Never run `dev-seed.sh` against a production or pilot environment.
