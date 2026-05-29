# Demo Seed Data Reference

This document describes the demo data seeded by `./tools/dev-seed.sh` and which endpoints each demo user can exercise.

## Quick start

```bash
./tools/start-local-harness.sh   # start all services + Dapr sidecars
./tools/dev-seed.sh              # seed profiles, vehicles, bookings, and run the demo Draw
```

Profile re-seeding is safe. Booking request seeding is not idempotent; restart the local harness before re-running when you need a clean demo.

---

## Demo users

| Username | Roles | Parking | Vehicles | Demo purpose |
|----------|-------|---------|----------|-------------|
| `employee1` | Jan Novak | employee | ✅ eligible | Daily Driver (`1AA 2345`), EV Commuter (`2AB 3456`) | Standard employee — booking, vehicle selection, notifications |
| `employee2` | Petra Svobodova | employee | ✅ eligible | Company Fleet (`3AC 4567`) | Company-car priority policy demonstration |
| `employee3` | Tomas Dvorak | employee | ✅ eligible + accessible | Accessible (`4AD 5678`) | Accessibility-eligible booking path |
| `hr-admin` | Lucie Prochazkova | employee, hr_manager | ❌ | — | Reports, configuration, HR import |
| `tenant-admin` | Karel Urban | admin | ❌ | — | Tenant admin console, readiness, guided setup |
| `report-viewer` | Eva Kralova | report_viewer | ❌ | — | Read-only reports access |
| `auditor` | Martin Cerny | auditor | ❌ | — | Audit log review |

Password for all demo users: `Dev1234!` (local Keycloak only).

---

## Seeded bookings

After running `dev-seed.sh`, 7 booking requests exist for the `Headquarters` facility in `Prague`. The script also runs the Draw for the +2 date from 08:00-18:00.

| Employee | Vehicle | Days ahead | Expected after seed |
|----------|---------|-----------|-------|
| Jan Novak | 1AA 2345 (Daily Driver) | +2 | Demo Draw participant |
| Petra Svobodova | 3AC 4567 (Company Fleet) | +2 | Demo Draw participant |
| Tomas Dvorak | 4AD 5678 (Accessible) | +2 | Demo Draw participant |
| Jan Novak | 2AB 3456 (EV Commuter) | +4 | Pending EV request |
| Jan Novak | 1AA 2345 (Daily Driver) | +6 | Additional booking |
| Petra Svobodova | 3AC 4567 (Company Fleet) | +5 | Company car priority |
| Tomas Dvorak | 4AD 5678 (Accessible) | +4 | Accessible spot request |

All bookings use dates ≥+2 days to stay clear of the draw cutoff that applies to same-day/+1 requests, ensuring they land as `Pending` regardless of time of day.

For the local development demo, Booking has two configured `AvailableSlots` for `demo` / `Prague`. The +2 Draw therefore has three requests competing for two slots, so the demo immediately shows allocated and waitlisted outcomes. The exact winning employee can vary with the deterministic Draw key seed when the date changes.

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

Configuration (policy + slots at `Prague`) is re-seeded automatically by the Configuration service on startup.

---

## Distinction: demo seed vs pilot import

| Path | Use case |
|------|---------|
| `./tools/dev-seed.sh` | Local development and demo smoke testing — uses internal admin endpoints, deterministic, dev-only |
| `tools/validate-hr-import.sh` + `POST /profile/bootstrap` | Pilot/production bootstrap — uses the HR CSV import contract, respects tenant scoping and auth |

Never run `dev-seed.sh` against a production or pilot environment.
