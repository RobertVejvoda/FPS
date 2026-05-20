# Local Test Harness

This page defines the local run path for FPS testing. The immediate goal is to make backend, mobile, and demo smoke testing repeatable. The longer-term goal is a one-command local harness, preferably through .NET Aspire or an equivalent AppHost, without replacing the production deployment model.

## Current Baseline

Use Docker Compose for shared infrastructure and run the .NET services from source.

From the repository root:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

`docker-compose.yaml` creates the required Docker network when it starts. If a local Docker network already exists from an older setup, Compose can reuse it.

Set up local identity once after Keycloak is running:

```sh
./tools/dev-setup-auth.sh
```

This imports the `fps-local` realm, creates the local demo users, and sets local-only demo passwords. Re-run it whenever the local Keycloak realm needs to be reset.

Before each backend service shell, source the local issuer settings:

```sh
source ./tools/dev-env.sh
```

Get a bearer token for a demo user when the mobile app or API smoke test needs one:

```sh
./tools/dev-auth.sh employee1
```

Available local users are `employee1`, `employee2`, `employee3`, and `hr-admin`. Treat generated bearer tokens as secrets: do not commit them, paste them into issues, or include them in screenshots.

Before starting backend services, confirm the repository-local .NET 10 SDK is first on `PATH`:

```sh
which dotnet
dotnet --info
```

Expected SDK: `10.0.203` from `$HOME/.dotnet/dotnet`. If a shell resolves `/usr/local/share/dotnet/dotnet`, run from the repository root or prepend `$HOME/.dotnet` to `PATH`.

Run services as needed:

```sh
source ./tools/dev-env.sh
dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
dotnet run --project code/server/Booking/FPS.Booking.API/FPS.Booking.API.csproj
dotnet run --project code/server/Profile/FPS.Profile/FPS.Profile.csproj
dotnet run --project code/server/Notification/FPS.Notification/FPS.Notification.csproj
dotnet run --project code/server/Audit/FPS.Audit/FPS.Audit.csproj
dotnet run --project code/server/Reporting/FPS.Reporting/FPS.Reporting.csproj
dotnet run --project code/server/Configuration/FPS.Configuration/FPS.Configuration.csproj
```

Current local service URLs:

| Service | Local URL |
| --- | --- |
| Identity | `http://localhost:5192` |
| Booking | `http://localhost:5131` |
| Configuration | `http://localhost:5141` |
| Audit | `http://localhost:5161` |
| Reporting | `http://localhost:5171` |
| Profile | `http://localhost:5197` |
| Notification | `http://localhost:5157` |

Each runnable service has an `http` launch profile so plain `dotnet run --project ...` resolves to a stable port instead of the implicit Kestrel fallback. Avoid relying on port `5000`; on macOS this port may already be owned by Control Center, and multiple services would collide there.

Use these URLs for local service smoke checks:

| Service | Smoke URL | Expected result without token |
| --- | --- | --- |
| Identity | `http://localhost:5192/openapi/v1.json` | `200` |
| Booking | `http://localhost:5131/openapi/v1.json` | `200` |
| Profile | `http://localhost:5197/openapi/v1.json` | `200` |
| Notification | `http://localhost:5157/openapi/v1.json` | `200` |
| Configuration | `http://localhost:5141/configuration/parking-policy` | `401` |
| Audit | `http://localhost:5161/audit` | `401` |
| Reporting | `http://localhost:5171/reports/parking/summary` | `401` |

Configuration, Audit, and Reporting do not currently expose `/openapi/v1.json`. Use the protected endpoint `401` check for those services until an approved API-documentation approach is adopted for them.

Current smoke result from `2026-05-20`: Docker infrastructure is healthy, including Vault, RabbitMQ, and `whoami-dapr`. The service port collision has been narrowed to missing launch profiles on Configuration, Audit, and Reporting; those services now have stable local HTTP ports.

Stop shared infrastructure:

```sh
docker compose -f code/infrastructure/docker-compose.yaml down
```

## Running Services With Dapr Sidecars (OPS006C)

Plain `dotnet run` starts a service without a Dapr sidecar. Endpoints that use `DaprClient` — such as Booking's state and pub/sub calls — return `500` because the sidecar gRPC port is not listening.

Use the Dapr CLI multi-app run to start six FPS services each paired with a sidecar:

```sh
# 1. Infrastructure and auth (once per session)
docker compose -f code/infrastructure/docker-compose.yaml up -d
./tools/dev-setup-auth.sh
source ./tools/dev-env.sh

# 2. Identity — no Dapr sidecar needed, plain dotnet run
dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj &

# 3. All other services with Dapr sidecars
./tools/start-with-dapr.sh
# or directly: dapr run -f dapr.yaml
```

`./tools/start-with-dapr.sh` requires the Dapr CLI (>= 1.12). Install once:

```sh
# macOS / Linux
curl -fsSL https://raw.githubusercontent.com/dapr/cli/master/install/install.sh | /bin/bash
dapr init
```

The run file is `dapr.yaml` at the repository root. It starts these services:

| App ID | Service | App port | Dapr HTTP | Dapr gRPC |
| --- | --- | --- | --- | --- |
| `fps-booking` | Booking | 5131 | 3601 | 50001 |
| `fps-notification` | Notification | 5157 | 3607 | 50007 |
| `fps-profile` | Profile | 5197 | 3617 | 50017 |
| `fps-audit` | Audit | 5161 | 3611 | 50011 |
| `fps-reporting` | Reporting | 5171 | 3621 | 50021 |
| `fps-configuration` | Configuration | 5141 | 3631 | 50031 |

### In-memory vs local components

`dapr.yaml` loads `code/infrastructure/dapr/components/smoke` — in-memory state and pub/sub components that need no Vault or MongoDB credentials. State is lost on restart but Dapr sidecar connections work immediately.

To use durable local state (MongoDB + RabbitMQ + Vault), change `resourcesPath` in `dapr.yaml` to `code/infrastructure/dapr/components/local` and ensure Vault is initialised with the required secrets.

### Smoke commands with Dapr sidecars

After starting Identity and `dapr run -f dapr.yaml`:

```sh
TOKEN=$(./tools/dev-auth.sh employee1)

# Gateway auth passthrough — expects 401 / 200
curl -s -o /dev/null -w "%{http_code}" http://localhost:10000/me
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://localhost:10000/me

# Booking with Dapr sidecar — expects 200 (empty list)
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings

# Notification — expects 200
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count
```

`GET /profile/snapshot` still returns `404` for `employee1` until profile domain data is seeded (OPS006D).

## Mobile Testing Implication

The mobile app expects one API base URL. The Envoy gateway added in OPS006B provides that URL at `http://localhost:10000`, routing all four mobile employee endpoints under one origin:

| Mobile path | Target service |
| --- | --- |
| `/me` | Identity |
| `/bookings` and booking actions | Booking |
| `/notifications` and notification actions | Notification |
| `/profile/snapshot` | Profile |

## Local Mobile API Gateway (OPS006B)

The Envoy proxy in Docker Compose now routes all mobile employee endpoints under one origin.
**Gateway URL (simulator/browser):** `http://localhost:10000`
**Gateway URL (physical phone on same LAN):** `http://<dev-machine-ip>:10000`

Start the gateway by starting Docker Compose — Envoy is already in `docker-compose.yaml`:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

Gateway route table:

| Mobile path | Target service |
| --- | --- |
| `GET /me` | Identity `localhost:5192` |
| `/bookings` and booking actions | Booking `localhost:5131` |
| `/notifications` and notification actions | Notification `localhost:5157` |
| `/profile/snapshot` | Profile `localhost:5197` |

Authorization headers pass through unchanged. The gateway does not mint or verify tokens.

**Linux note:** `host.docker.internal` is not available by default. Add the following to the `envoy-proxy` service in `docker-compose.yaml`:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

Or replace `host.docker.internal` with `172.17.0.1` in `code/infrastructure/envoy/envoy.yaml`.

### Gateway smoke commands

Run after `docker compose up`, `dev-setup-auth.sh`, `dev-env.sh`, and all four services.

**What passes today (gateway routing + auth passthrough):**

```sh
TOKEN=$(./tools/dev-auth.sh employee1)

# Should return 401 without token
curl -s -o /dev/null -w "%{http_code}" http://localhost:10000/me
# Should return 200 — gateway routes and bearer token is accepted
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://localhost:10000/me
# Should return 200 — Notification service uses in-memory storage
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count
```

**Full mobile E2E sequence:**

- OPS006C (this page) resolves the Booking sidecar gap. `GET /bookings` returns `200` when Booking is started through `dapr run -f dapr.yaml` instead of plain `dotnet run`.
- `GET /profile/snapshot` returns `404` for `employee1` until profile domain data is seeded by OPS006D.

Full mobile E2E testing — where all four endpoints return valid data — requires the OPS006B gateway, the OPS006C Dapr sidecar run path, and the OPS006D seed/reset step. The gateway closes the routing gap; sidecars close the Dapr state/pubsub gap; seed data closes the Profile and demo-domain gap.

### Mobile session configuration

On a physical phone, use a LAN-reachable gateway URL such as `http://<dev-machine-ip>:10000`, not `localhost`.

Use Expo LAN mode when the phone and development machine are on the same network:

```sh
cd code/mobile/fps-mobile
npm run start -- --lan --clear
```

If the QR code cannot be opened from the phone, retry with Expo tunnel mode:

```sh
cd code/mobile/fps-mobile
npm run start -- --tunnel --clear
```

The developer session screen still requires both values:

- API base URL: `http://localhost:10000` (simulator) or `http://<dev-machine-ip>:10000` (phone);
- bearer token: a local development token from `./tools/dev-auth.sh`.

## Seeding Local Demo Data (OPS006D)

After starting services (Identity + `dapr run -f dapr.yaml`), run the seed script once:

```sh
./tools/dev-seed.sh
```

This seeds Profile snapshots for `employee1`, `employee2`, and `employee3` by:
1. Getting a ROPC token per user from local Keycloak.
2. Decoding the JWT `sub` claim to get the Dapr/service user ID.
3. Calling `PUT /profile/admin/snapshot` (Development-only endpoint) with synthetic profile facts.

**Configuration** (tenant policy + 10 parking slots at `LOC-MAIN`) is seeded automatically by the Configuration service when it starts in `Development` mode.

**Bookings** — empty list (`GET /bookings` → `200 []`) is the documented local baseline. Submit a booking via the mobile app or Booking API to create entries.

**Notifications** — unread count `0` is the documented baseline. Events are published in-memory (smoke components) so booking submissions create notification records.

### Seed demo data table

| User | ParkingEligible | CompanyCar | Vehicle | Accessibility |
| --- | --- | --- | --- | --- |
| `employee1` | ✓ | — | Sedan ABC001 | — |
| `employee2` | ✓ | ✓ | — | — |
| `employee3` | ✓ | — | — | ✓ |

### Reset / re-seed

The seed script is idempotent — run it again after a service restart:

```sh
./tools/dev-seed.sh
```

### Post-seed smoke

```sh
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count
```

All three should return `200`.

## Preferred Next Harness

The local identity and bearer-token path now exists through `OPS006A`. The next harness work should close the remaining full-stack gaps: seeded FPS domain data, one mobile API gateway/base URL, coordinated startup, and reset instructions.

Implemented `OPS006A` local auth sequence:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
./tools/dev-setup-auth.sh
source ./tools/dev-env.sh
./tools/dev-auth.sh employee1
dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
```

After `OPS006A`, add a local AppHost slice when implementation work is scheduled. .NET Aspire is the preferred candidate because the server stack is already .NET and the system needs coordinated local startup, logs, health, and dependency visibility. Treat Aspire as a developer/test harness, not as the client production deployment decision.

The first useful AppHost should:

- start or reference MongoDB, RabbitMQ, Vault, MinIO, Keycloak, and observability dependencies;
- start Identity, Booking, Profile, Notification, Audit, Reporting, and Configuration from source;
- load the local Dapr component path from `code/infrastructure/dapr/components/local`;
- expose a single local mobile API gateway URL;
- show service health, logs, and traces in one dashboard;
- document seeded demo users and data reset;
- avoid committing or printing real secrets.

Suggested slice name: `OPS006 Local Test Harness`.

## AppHost Acceptance Criteria

The AppHost or equivalent harness is acceptable when:

- one documented command starts the local test stack;
- the mobile app can use one API base URL for the employee flow;
- health checks identify which dependency or service is down;
- seeded synthetic data supports login, bookings, notifications, and profile scenarios;
- stop/reset instructions return the environment to a known state;
- existing Docker Compose and Dapr component docs remain valid;
- `./tools/validate.sh` still passes for code changes.

## Testing Split

Use the right tool for each test level:

| Test level | Recommended tool | Purpose |
| --- | --- | --- |
| Unit and slice tests | `dotnet test` / existing test projects | Fast behavioral validation in CI. |
| Repository and component integration | Testcontainers where deterministic CI coverage is needed | MongoDB, Dapr, and broker behavior around one service. |
| Local full-stack smoke | Aspire AppHost or equivalent local harness | Verify services, dependencies, gateway, logs, and mobile API base URL together. |
| Manual device smoke | Expo Go, simulator, or emulator | Verify real mobile navigation, auth, rendering, and error states. |
| Hosted demo evidence | Demo environment runbook | Prove external evaluator path with HTTPS, seeded users, and operational evidence. |

## Until AppHost Exists

Use this minimum workflow:

1. Start shared infrastructure with Docker Compose.
2. Run the backend smoke checks. Do not continue to mobile device testing if a service cannot bind its configured HTTP port.
3. Run `./tools/dev-setup-auth.sh`, source `./tools/dev-env.sh`, and generate a development bearer token with `./tools/dev-auth.sh employee1`.
4. Start a local gateway or equivalent single API base URL for mobile.
5. Run Expo in LAN mode, falling back to tunnel mode when QR discovery fails.
6. Record that full mobile end-to-end testing is blocked until domain seed data and the gateway URL both pass.
