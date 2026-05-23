# Local Test Harness

This page defines the local run path for FPS testing. The immediate goal is to make backend, mobile, and demo smoke testing repeatable. The longer-term goal is a one-command local harness, preferably through .NET Aspire or an equivalent AppHost, without replacing the production deployment model.

## Current Baseline

Use Docker Compose for shared infrastructure and run the .NET services from source.

From the repository root:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

`docker-compose.yaml` creates the required Docker network when it starts. If a local Docker network already exists from an older setup, Compose can reuse it.

Local-only infrastructure defaults:

- Keycloak admin credentials are the Docker Compose/dev-script defaults used by `tools/dev-setup-auth.sh`; override them with `KEYCLOAK_ADMIN` and `KEYCLOAK_ADMIN_PASSWORD` if needed.
- Vault runs in dev mode for local Docker Compose. Use the local Vault token documented in `code/infrastructure/readme.md`, or set `VAULT_ADDR` and `VAULT_TOKEN` for your shell before using Vault CLI commands.

These values are disposable development defaults for the local Docker Compose profile only. Do not reuse them for demo, pilot, or client-owned environments.

If Docker infrastructure is already running and you want a one-shot smoke run for an app surface, use:

```sh
# Web smoke: starts backend services, seeds data, then starts Vite on :5200.
sh ./tools/start-smoke-web.sh

# Mobile smoke: starts backend services, seeds data, then starts Expo.
sh ./tools/start-smoke-mobile.sh

# If LAN QR scanning fails for a physical phone:
EXPO_MODE=tunnel sh ./tools/start-smoke-mobile.sh
```

Both scripts leave Docker infrastructure running when stopped with Ctrl-C. They stop only the app services and Dapr sidecars they started.

The scripts also check frontend dependencies before starting Vite or Expo. They prefer user-installed Node/npm from Homebrew or `/usr/local/bin` over embedded tool runtimes. If `node_modules` is missing or a native optional package probe fails, they run `npm ci` from the app lockfile to repair the local dependency tree. On macOS they also ad-hoc sign local `.node` binaries after install to avoid native optional dependency code-signature failures from packages such as Rollup.

## Devcontainer

Use the devcontainer for repeatable backend and web smoke development when local host tooling is noisy or missing. It provides:

- .NET 10 SDK;
- Node LTS and npm;
- Dapr CLI plus `dapr init --slim`;
- Docker CLI access to the host Docker engine;
- VS Code tasks for validation, infrastructure startup, smoke scripts, and web/mobile typechecks.

Open the repository in VS Code or Cursor and choose **Reopen in Container**. After the post-create setup completes:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
./tools/start-smoke-web.sh
```

The devcontainer uses the host Docker engine. If Docker Compose bind mounts do not resolve correctly from inside the container on your machine, start Docker infrastructure from a host terminal and then run the smoke script inside the devcontainer. Keep the API/service ports forwarded by the editor.

When running inside the devcontainer, the harness uses `FPS_INFRA_HOST=host.docker.internal` and `KEYCLOAK_URL=http://host.docker.internal:8180` so backend processes in the container can reach infrastructure published by the host Docker engine. On the host machine these default to `localhost`.

For focused backend debugging, start the smoke harness and use the .NET debugger's attach-to-process flow against the service process you need to inspect. This keeps Dapr sidecars and local auth behavior aligned with the same path used by smoke testing.

Physical mobile QR/LAN testing is still host-first. The devcontainer is useful for mobile typechecking and API/backend behavior, but Expo device networking is simpler from the host machine unless tunnel mode is intentionally configured.

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

Available local users:

| Username | FPS roles | Main demo interest |
| --- | --- | --- |
| `employee1` | `employee` | Normal employee booking, notifications, profile, mobile/web self-service. |
| `employee2` | `employee` | Company-car style seeded profile path. |
| `employee3` | `employee` | Missing/no-vehicle profile edge cases. |
| `hr-admin` | `employee`, `hr_manager` | HR/facilities operations: policy/slot management, employee bootstrap, operational reports. |
| `tenant-admin` | `admin` | Tenant setup, identity setup, readiness checks, privileged configuration, audit administration. |
| `report-viewer` | `report_viewer` | Read-only reporting review. |
| `auditor` | `auditor` | Audit query/evidence review. |

Treat generated bearer tokens as secrets: do not commit them, paste them into issues, or include them in screenshots.

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

## Admin Reports And Operator Views

After running `sh ./tools/start-smoke-web.sh`, the web app starts at `http://localhost:5200` and the API gateway is `http://localhost:10000`.

For business reports:

1. Sign in with `hr-admin` or `report-viewer` after WEB009 lands, or use the current development session handoff with:

   ```sh
   ./tools/dev-auth.sh hr-admin
   ```

2. In the web app, use API base URL `http://localhost:10000`.
3. Open **Reports**. The page shows total demand, allocations, allocation rate, rejections, cancellations, no-shows, daily trend, utilization by location, reason-code counts, fairness rows using pseudonymised requestor hashes, and CSV downloads.

Direct API checks use an `hr-admin` or `report-viewer` token:

```sh
TOKEN=$(./tools/dev-auth.sh hr-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/dashboard
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/summary
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/fairness
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/utilization
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/reason-codes
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/summary.csv
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/reports/parking/allocation-outcomes.csv
```

Reporting data is populated from Booking events consumed by the Reporting service. Empty reports usually mean no seeded or smoke booking events have reached Reporting yet, not that the report surface is broken.

For operational traces and infrastructure stats:

| Tool | URL | What it shows locally |
| --- | --- | --- |
| Grafana | `http://localhost:3000` | Local dashboard shell. Login with the local Docker Compose defaults from `code/infrastructure/readme.md`. Dashboard provisioning is still a follow-up gap. |
| Prometheus | `http://localhost:9090` | Local scrape targets from `code/infrastructure/prometheus/prometheus.yaml`. Current coverage is infrastructure-oriented, not full application metrics. |
| Zipkin | `http://localhost:19411` | Traces only when Dapr tracing config is enabled. The default smoke `dapr.yaml` intentionally omits tracing config to avoid the Docker-network-only Zipkin endpoint. |
| Jaeger | `http://localhost:16686` | Local tracing UI container. FPS services do not yet export OpenTelemetry traces to it by default. |
| RabbitMQ | `http://localhost:15672` | Pub/sub broker health when using durable local Dapr components. |
| Vault | `http://localhost:8200` | Local dev-mode secret store status. |

Current observability limit: FPS services expose `GET /health` and structured stdout logs. Full application OpenTelemetry metrics/traces and prebuilt admin dashboards are tracked as follow-up production-readiness work; see [Monitoring](./monitoring).

Role interests are intentionally separated:

| Actor | Uses app auth? | Primary views |
| --- | --- | --- |
| Employee | Yes | Own bookings, new request, notifications, profile. |
| HR/facilities | Yes | Operational reports, configuration, employee/profile bootstrap. |
| Tenant admin | Yes | Tenant lifecycle, identity setup, policy/slot readiness, privileged setup checks. |
| Report viewer | Yes | Parking reports and CSV exports only. |
| Auditor | Yes | Audit query, evidence, erasure/integrity support where implemented. |
| Operator/SRE | Usually no app role | Grafana, Prometheus, Zipkin/Jaeger, logs, Dapr/RabbitMQ/Vault/container health. |

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

`GET /profile/snapshot` returns `200` for seeded demo users after running the OPS006D seed script. Run `./tools/dev-seed.sh` after the services are started.

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
- OPS006D resolves the profile seed gap. `GET /profile/snapshot` returns `200` for `employee1`, `employee2`, and `employee3` after `./tools/dev-seed.sh`.

Full mobile E2E testing — where all four endpoints return valid data — requires the OPS006B gateway, the OPS006C Dapr sidecar run path, and the OPS006D seed/reset step. The gateway closes the routing gap; sidecars close the Dapr state/pubsub gap; seed data closes the Profile and demo-domain gap. The remaining OPS006 parent work is coordinated startup and health/log visibility through an AppHost or equivalent harness.

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

## Local Harness

`tools/start-local-harness.sh` is the one-command entry point for local full-stack smoke testing. It starts Docker Compose infrastructure, configures Keycloak, launches Identity and the six Dapr-paired services in the background, waits for each service port to bind, then seeds demo data.

Prerequisites (install once):

- Docker Desktop running.
- Dapr CLI >= 1.12 installed and initialised: `curl -fsSL https://raw.githubusercontent.com/dapr/cli/master/install/install.sh | /bin/bash && dapr init`
- .NET 10.0.203 SDK from `$HOME/.dotnet/dotnet` first on `PATH`.

Start:

```sh
./tools/start-local-harness.sh
```

The script waits for each service before seeding data and prints a smoke command set when all services are ready. Service logs go to `logs/local-harness/`.

Stop (keeps Docker volumes — data survives restart):

```sh
./tools/stop-local-harness.sh
```

Full reset (removes Docker volumes — returns environment to a clean state):

```sh
./tools/stop-local-harness.sh --reset
```

After a full reset, re-run `./tools/start-local-harness.sh` to rebuild the Keycloak realm and reseed demo data.

### Smoke commands

Run in a separate shell after the harness is ready:

```sh
TOKEN=$(./tools/dev-auth.sh employee1)
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/me
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot
```

All four should return `200`.

### Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Script exits with "Wrong .NET SDK" | System dotnet resolves before `$HOME/.dotnet` | Prepend `$HOME/.dotnet` to `PATH` and retry |
| Script exits non-zero with service port error | Dapr sidecar or service startup slow or crashed | Check `logs/local-harness/dapr-run.log`; run `./tools/stop-local-harness.sh` then retry |
| Seed step fails (script exits non-zero) | Profile service not yet ready, or Keycloak realm missing | Services are still running — fix the cause and re-run `./tools/dev-seed.sh`, or run `./tools/stop-local-harness.sh` and restart |
| `/bookings` returns 500 | Dapr sidecar not connected | Check `logs/local-harness/dapr-run.log` for sidecar startup errors |
| Keycloak timeout | Keycloak container slow to initialise | Wait 30 s and retry; check `docker compose logs keycloak` |

## Testing Split

Use the right tool for each test level:

| Test level | Recommended tool | Purpose |
| --- | --- | --- |
| Unit and slice tests | `dotnet test` / existing test projects | Fast behavioral validation in CI. |
| Repository and component integration | Testcontainers where deterministic CI coverage is needed | MongoDB, Dapr, and broker behavior around one service. |
| Local full-stack smoke | `./tools/start-local-harness.sh` | Verify services, dependencies, gateway, logs, and mobile API base URL together. |
| Manual device smoke | Expo Go, simulator, or emulator | Verify real mobile navigation, auth, rendering, and error states. |
| Hosted demo evidence | Demo environment runbook | Prove external evaluator path with HTTPS, seeded users, and operational evidence. |
