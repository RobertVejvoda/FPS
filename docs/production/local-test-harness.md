# Local Test Harness

This page defines the local run path for FairSpot testing. The immediate goal is to make backend, mobile, and demo smoke testing repeatable. The longer-term goal is a repository-owned one-command local harness without replacing the production deployment model.

## Current Baseline

Use Docker Compose for shared infrastructure and run the .NET services from source.

From the repository root:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

`docker-compose.yaml` creates the required Docker network when it starts. If a local Docker network already exists from an older setup, Compose can reuse it.

Local-only infrastructure defaults:

- Keycloak admin credentials are the Docker Compose/dev-script defaults used by `tools/dev-setup-auth.sh`; override them with `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD` if needed. The legacy `KEYCLOAK_ADMIN` variables are still accepted by the script for older local shells.
- Vault runs in dev mode for local Docker Compose. Use the local Vault token documented in `code/infrastructure/readme.md`, or set `VAULT_ADDR` and `VAULT_TOKEN` for your shell before using Vault CLI commands.

These values are disposable development defaults for the local Docker Compose profile only. Do not reuse them for demo, pilot, or client-owned environments.

The local run path is split into four lifecycles:

- infrastructure: Docker Compose dependencies such as Keycloak, Envoy, RabbitMQ, Vault, observability, and data stores;
- backend: FairSpot .NET services plus Dapr sidecars, started by the local harness;
- web client: Vite on `http://localhost:5200`;
- mobile client: Expo in LAN, tunnel, or localhost mode.

If Docker infrastructure is already running and you want a one-shot smoke run for an app surface, use:

```sh
# Web smoke: starts or reuses backend services, seeds data if it started them,
# then starts Vite on :5200.
sh ./tools/start-smoke-web.sh

# Mobile smoke: starts or reuses backend services, seeds data if it started them,
# then starts Expo.
sh ./tools/start-smoke-mobile.sh

# If LAN QR scanning fails for a physical phone:
EXPO_MODE=tunnel sh ./tools/start-smoke-mobile.sh
```

Both smoke scripts can run at the same time. If the backend harness is already reachable, the second script reuses it instead of starting another copy. Stopping either frontend with Ctrl-C stops only that frontend by default; the backend harness and Docker infrastructure stay running so the other client is not broken. Stop the shared backend explicitly when finished:

```sh
./tools/stop-local-harness.sh --services-only
```

Set `SMOKE_STOP_HARNESS_ON_EXIT=true` only for isolated one-client smoke runs where the script should stop backend app services on exit.

The scripts also check frontend dependencies before starting Vite or Expo. They prefer user-installed Node/npm from Homebrew or `/usr/local/bin` over embedded tool runtimes. If `node_modules` is missing or a native optional package probe fails, they run `npm ci` from the app lockfile to repair the local dependency tree. On macOS they also ad-hoc sign local `.node` binaries after install to avoid native optional dependency code-signature failures from packages such as Rollup.

Web OIDC login is bound to `http://localhost:5200/auth/callback` in the local Keycloak `fps-web-dev` client and in `code/web/fps-web/public/config.json`. Web logout is bound to `http://localhost:5200/` through the same client. If port `5200` is already occupied, stop the other web process first; do not use a Vite fallback port such as `5201` unless you also update the runtime config and Keycloak redirect URI. The local Envoy gateway also allows browser CORS preflight only from `http://localhost:5200`.

`start-smoke-web.sh` binds Vite to `127.0.0.1` by default so it does not advertise LAN or Tailscale URLs that cannot complete SSO with the default local realm. To test web from another device, set a deliberate host with `FPS_WEB_HOST=0.0.0.0`, then update all of these values together before starting the smoke path:

- `fps-web-dev` Keycloak redirect URI and web origin for the chosen host;
- `code/web/fps-web/public/config.json` `apiBaseUrl`, OIDC authority, redirect URI, and logout redirect URI;
- Envoy CORS allowed origin for the chosen web origin.

For phone testing, prefer `sh ./tools/start-smoke-mobile.sh`; it is the supported network-device path.

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

Available local users — login username and fictional display name:

| Username | Display name | FairSpot roles | Main demo interest |
| --- | --- | --- | --- |
| `employee1` | Jan Novak | `employee` | Normal employee booking, EV and sedan vehicle selection, notifications, profile, mobile/web self-service. |
| `employee2` | Petra Svobodova | `employee` | Company-car booking path; fleet vehicle (3AC 4567). |
| `employee3` | Tomas Dvorak | `employee` | Accessibility-eligible booking; accessible spot priority. |
| `hr-admin` | Lucie Prochazkova | `employee`, `hr_manager` | HR/facilities operations: policy/slot management, employee bootstrap, operational reports. |
| `tenant-admin` | Karel Urban | `admin` | Tenant setup, identity setup, readiness checks, privileged configuration, audit administration. |
| `report-viewer` | Eva Kralova | `report_viewer` | Read-only reporting review. |
| `auditor` | Martin Cerny | `auditor` | Audit query/evidence review. |

All display names are synthetic and fictional — no real employees, emails, or identifiers. Script usernames (`employee1`, `hr-admin`, …) are stable and safe to use in smoke commands.

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
dotnet run --project code/server/Booking/FPS.Booking/FPS.Booking.csproj
dotnet run --project code/server/Profile/FPS.Profile/FPS.Profile.csproj
dotnet run --project code/server/Notification/FPS.Notification/FPS.Notification.csproj
dotnet run --project code/server/Audit/FPS.Audit/FPS.Audit.csproj
dotnet run --project code/server/Reporting/FPS.Reporting/FPS.Reporting.csproj
dotnet run --project code/server/Configuration/FPS.Configuration/FPS.Configuration.csproj
dotnet run --project code/server/Customer/FPS.Customer/FPS.Customer.csproj
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
| Customer | `http://localhost:5181` |

Each runnable service has an `http` launch profile so plain `dotnet run --project ...` resolves to a stable port instead of the implicit Kestrel fallback. Avoid relying on port `5000`; on macOS this port may already be owned by Control Center, and multiple services would collide there.

Use these URLs for local service smoke checks:

| Service | Smoke URL | Expected result without token |
| --- | --- | --- |
| Identity | `http://localhost:5192/openapi/v1.json` | `200` |
| Booking | `http://localhost:5131/openapi/v1.json` | `200` |
| Profile | `http://localhost:5197/openapi/v1.json` | `200` |
| Notification | `http://localhost:5157/notifications/unread-count` | `401` |
| Configuration | `http://localhost:5141/configuration/parking-policy` | `401` |
| Audit | `http://localhost:5161/audit` | `401` |
| Reporting | `http://localhost:5171/reports/parking/summary` | `401` |
| Customer | `http://localhost:5181/openapi/v1.json` | `200` |

Notification, Configuration, Audit, and Reporting do not currently expose `/openapi/v1.json`. Use the protected endpoint `401` check for those services until an approved API-documentation approach is adopted for them.

## Admin Reports And Operator Views

After running `sh ./tools/start-smoke-web.sh`, the web app starts at `http://localhost:5200` and the API gateway is `http://localhost:10000`.

For business reports:

1. Open `http://localhost:5200` and sign in with `hr-admin` or `report-viewer` using the OIDC login screen. Keycloak must be running (`docker compose up -d keycloak`) and the realm imported (`./tools/dev-setup-auth.sh`).

2. Open **Reports**.
3. The page shows total demand, allocations, allocation rate, rejections, cancellations, no-shows, daily trend, utilization by location, reason-code counts, fairness rows using pseudonymised requestor hashes, and CSV downloads.

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
| Jaeger | `http://localhost:16686` | Local tracing UI container. FairSpot services do not yet export OpenTelemetry traces to it by default. |
| RabbitMQ | `http://localhost:15672` | Pub/sub broker health when using durable local Dapr components. |
| Vault | `http://localhost:8200` | Local dev-mode secret store status. |

Current observability limit: FairSpot services expose `GET /health` and structured stdout logs. Full application OpenTelemetry metrics/traces and prebuilt admin dashboards are tracked as follow-up production-readiness work; see [Monitoring](./monitoring).

Role interests are intentionally separated:

| Actor | Uses app auth? | Primary views |
| --- | --- | --- |
| Employee | Yes | Own bookings, new request, notifications, profile. |
| HR/facilities | Yes | Operational reports, configuration, employee/profile bootstrap. |
| Tenant admin | Yes | Tenant lifecycle, identity setup, policy/slot readiness, privileged setup checks. |
| Report viewer | Yes | Parking reports and CSV exports only. |
| Auditor | Yes | Audit query, evidence, erasure/integrity support where implemented. |
| Operator/SRE | Usually no app role | Grafana, Prometheus, Zipkin/Jaeger, logs, Dapr/RabbitMQ/Vault/container health. |

Current local smoke direction: Docker infrastructure provides Vault, RabbitMQ, data stores, observability shells, and Envoy. The old `whoami-dapr` sample service has been removed; use `./tools/smoke-gateway-health.sh` to verify the local gateway against real FairSpot service health endpoints.

Stop shared infrastructure:

```sh
docker compose -f code/infrastructure/docker-compose.yaml down
```

## Running Services With Dapr Sidecars (OPS006C)

Plain `dotnet run` starts a service without a Dapr sidecar. Endpoints that use `DaprClient` — such as Booking's state and pub/sub calls — return `500` because the sidecar gRPC port is not listening.

Use the Dapr CLI multi-app run to start seven FairSpot services each paired with a sidecar:

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

`./tools/start-with-dapr.sh` requires the Dapr CLI (>= 1.14). Install once:

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
| `fps-customer` | Customer | 5181 | 3641 | 50041 |

### In-memory vs local components

`dapr.yaml` loads `code/infrastructure/dapr/components/smoke` and `code/infrastructure/dapr/configuration/fps-smoke-config.yaml` — in-memory state and pub/sub components that need no Vault or MongoDB credentials, plus the actor-state configuration required by Dapr Workflow. State is lost on restart but Dapr sidecar connections work immediately.

To use durable local state (MongoDB + RabbitMQ + Vault), change `resourcesPath` in `dapr.yaml` to `code/infrastructure/dapr/components/local`, change `configFilePath` to `code/infrastructure/dapr/configuration/fps-config.yaml`, and ensure Vault is initialised with the required secrets.

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

The Envoy proxy in Docker Compose now routes employee and operator API endpoints under one origin for mobile and browser web smoke testing. The local gateway allows browser CORS from `http://localhost:5200`, matching the web OIDC redirect origin.
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
| `/reports` | Reporting `localhost:5171` |
| `/audit` | Audit `localhost:5161` |
| `/configuration` | Configuration `localhost:5141` |
| `/tenants` | Customer `localhost:5181` |

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

Full mobile E2E testing — where all four endpoints return valid data — requires the OPS006B gateway, the OPS006C Dapr sidecar run path, and the OPS006D seed/reset step. The gateway closes the routing gap; sidecars close the Dapr state/pubsub gap; seed data closes the Profile and demo-domain gap. The remaining OPS006 parent work is coordinated startup and health/log visibility through the local harness.

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

For real mobile OIDC login, prefer `sh ./tools/start-smoke-mobile.sh`. The script exports Expo runtime config for the local `fps-mobile-dev` client and uses one device-reachable host for both Keycloak and the API gateway. When Tailscale is installed, it prefers the Mac's Tailscale IPv4 address; otherwise it uses the LAN address. Override detection with:

```sh
FPS_MOBILE_HOST=<host-or-ip> sh ./tools/start-smoke-mobile.sh
FPS_MOBILE_KEYCLOAK_URL=http://<host-or-ip>:8180 FPS_MOBILE_API_BASE_URL=http://<host-or-ip>:10000 sh ./tools/start-smoke-mobile.sh
```

For repeatable local overrides, copy `code/mobile/fps-mobile/mobile-env.sample` to `code/mobile/fps-mobile/.env.local`. The smoke script loads `.env.local` automatically. Treat these as public runtime settings only; never put secrets in mobile Expo config.

If mobile login reaches Keycloak but fails with `invalid parameter: redirect_uri`, re-run `./tools/dev-setup-auth.sh` so the local `fps-mobile-dev` client receives the current Expo/native redirect allow-list.

## Seeding Local Demo Data (OPS006D)

After starting services (Identity + `dapr run -f dapr.yaml`), run the seed script once:

```sh
./tools/dev-seed.sh
```

This seeds Profile snapshots for `employee1`, `employee2`, and `employee3` by:
1. Getting a ROPC token per user from local Keycloak.
2. Decoding the JWT `sub` claim to get the Dapr/service user ID.
3. Calling `PUT /profile/admin/snapshot` (Development-only endpoint) with synthetic profile facts.

**Configuration** (tenant policy + 10 parking slots at `Prague`) is seeded automatically by the Configuration service when it starts in `Development` mode.

**Bookings** — empty list (`GET /bookings` → `200 []`) is the documented local baseline. Submit a booking via the mobile app or Booking API to create entries.

**Notifications** — unread count `0` is the documented baseline. Events are published in-memory (smoke components) so booking submissions create notification records.

### Seed demo data table

| Username | Display name | ParkingEligible | CompanyCar | Vehicles | Accessibility |
| --- | --- | --- | --- | --- | --- |
| `employee1` | Jan Novak | ✓ | — | 1AA 2345 (sedan), 2AB 3456 (EV) | — |
| `employee2` | Petra Svobodova | ✓ | ✓ | 3AC 4567 (fleet) | — |
| `employee3` | Tomas Dvorak | ✓ | — | 4AD 5678 | ✓ |
| `hr-admin` | Lucie Prochazkova | — | — | — | — |
| `tenant-admin` | Karel Urban | — | — | — | — |
| `report-viewer` | Eva Kralova | — | — | — | — |
| `auditor` | Martin Cerny | — | — | — | — |

### Reset / re-seed

Profile seeding is idempotent — re-run after a service restart:

```sh
./tools/dev-seed.sh
```

For a full reset (clears all in-memory state including bookings):

```sh
./tools/stop-local-harness.sh --reset
./tools/start-local-harness.sh
```

`stop-local-harness.sh --reset` removes Docker volumes and returns the environment to a clean state. After restart, `start-local-harness.sh` re-imports the Keycloak realm and re-seeds demo data automatically.

### Post-seed smoke

```sh
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/notifications/unread-count
```

All three should return `200`. For a full scenario walkthrough including audit and reporting evidence, run:

```sh
./tools/smoke-onboarding.sh
```

All three should return `200`.

## Local Harness

`tools/start-local-harness.sh` is the one-command entry point for local full-stack smoke testing. It starts Docker Compose infrastructure, configures Keycloak, launches Identity and the seven Dapr-paired services in the background, waits for each service port to bind, then seeds demo data.

Prerequisites (install once):

- Docker Desktop running.
- Dapr CLI >= 1.14 installed and initialised: `curl -fsSL https://raw.githubusercontent.com/dapr/cli/master/install/install.sh | /bin/bash && dapr init`
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

### Tenant readiness

The Customer service readiness endpoint combines tenant-local configuration checks with connected service probes:

```sh
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/tenants/demo/readiness
```

The local harness wires evaluation-grade HTTP health probes for Profile, Booking, Notification, Audit, and Reporting. If those services are running and healthy, `ProfileFacts`, `BookingSmokeTest`, `NotificationReachable`, `AuditEvidence`, and `ReportingEvidence` pass. If a service is stopped or unhealthy, the readiness report fails that check with the service health URL and status.

These checks prove service connectivity for local/demo readiness. They do not yet prove deeper tenant-specific evidence such as exact profile fact counts, booking write/read smoke data, audit row content, or reporting aggregate correctness.

### Demo Draw

Seeded bookings are usually `Pending` because they target future dates and wait for the scheduled Draw. For a demo walkthrough, an administrator can run one Draw on demand.

For repeatable API testing, use [Draw REST Client Scenarios](./draw-rest-client-scenarios.http) in VS Code with the REST Client extension.

Web path:

1. Start the local web smoke path:

   ```sh
   sh ./tools/start-smoke-web.sh
   ```

2. Sign in as `tenant-admin`.
3. Open **Configuration**.
4. In **Demo Draw**, choose:
   - Location: `Prague`;
   - Parking date matching the pending seeded booking date;
   - Arrival/departure time, normally `08:00` to `18:00`;
   - Reason, for example `Demo on-demand Draw`.
5. Click **Run Draw now**.

The result shows allocated, rejected, and waitlisted counts. Running the same location, date, and time slot again returns the completed Draw result instead of reallocating.

Direct API smoke:

```sh
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -X POST http://localhost:10000/draws/trigger \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "locationId": "Prague",
    "date": "2026-05-26",
    "timeSlotStart": "2026-05-26T08:00:00",
    "timeSlotEnd": "2026-05-26T18:00:00",
    "reason": "Demo on-demand Draw"
  }'
```

Employee tokens must receive `403` for this endpoint. Employees see the next Draw time and final result through booking and notification surfaces; they must not be able to trigger allocation directly.

### Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| Script exits with "Wrong .NET SDK" | System dotnet resolves before `$HOME/.dotnet` | Prepend `$HOME/.dotnet` to `PATH` and retry |
| Script exits with "port ... is already in use" | Stale FairSpot service or Dapr sidecar process is still bound from a previous run, or a partial backend harness is running | Run `./tools/stop-local-harness.sh --services-only`, then retry |
| Script exits non-zero with service port error | Dapr sidecar or service startup slow or crashed | Check `logs/local-harness/dapr-run.log`; run `./tools/stop-local-harness.sh` then retry |
| Seed step fails with Booking `401` | Booking rejected the token; most often a stale service process was running with the wrong auth environment | Run `./tools/stop-local-harness.sh --services-only`, then start the harness again |
| Seed step fails (script exits non-zero) | Profile service not yet ready, Keycloak realm missing, or service validation rejected the seed payload | Services are still running — fix the cause and re-run `./tools/dev-seed.sh`, or run `./tools/stop-local-harness.sh` and restart |
| `/bookings` returns 500 | Dapr sidecar not connected | Check `logs/local-harness/dapr-run.log` for sidecar startup errors |
| Keycloak timeout | Keycloak container slow to initialise | Wait 30 s and retry; check `docker compose logs keycloak` |

## Tenant Provisioning (OPS008B)

Tenant workspaces, identity config, and parking policy/slots can be provisioned from a declarative definition file using `./tools/provision-tenant.sh`.

### Tenant definition files

Definitions live in `tools/templates/tenants/`. Two synthetic definitions are included:

| File | Tenant | Purpose |
| --- | --- | --- |
| `demo.json` | `demo` | Default local demo tenant (seeded automatically on service startup). |
| `acme-corp.json` | `acme-corp` | Second synthetic tenant proving provisioning is not hardcoded. |

### Running provisioning

```sh
# Provision (or re-provision) the demo tenant
./tools/provision-tenant.sh tools/templates/tenants/demo.json

# Provision the second synthetic tenant
./tools/provision-tenant.sh tools/templates/tenants/acme-corp.json

# Override tenant ID for local experiments
FPS_DEMO_TENANT_ID=my-test ./tools/provision-tenant.sh tools/templates/tenants/demo.json
```

Provisioning is idempotent: re-running it creates the tenant workspace if absent, updates identity config, and checks readiness. Profile and booking seed data are created by `./tools/dev-seed.sh` after provisioning.

### Adding a new tenant locally

1. Copy `tools/templates/tenants/demo.json` to `tools/templates/tenants/{tenantId}.json`.
2. Edit `tenantId`, `displayName`, `region`, `timezone`, `parkingPolicy`, and `locations`.
3. Run `./tools/provision-tenant.sh tools/templates/tenants/{tenantId}.json`.
4. Verify the readiness summary at the end.

### Mapping to client-owned production

The same definition format drives the provisioning contract defined in [Tenant Storage Contract](./tenant-storage-contract.md). In a client-owned environment, the provisioning steps map to:

| Step | Local (provision-tenant.sh) | Client-owned production |
| --- | --- | --- |
| Tenant workspace | `POST /tenants` via Customer service | Same API call, client-managed credentials |
| Identity config | `PUT /tenants/{id}/identity-config` | Same, pointing to client IdP |
| Parking policy | `PUT /configuration/parking-policy` | Same, tenant-scoped admin token |
| Slots | `PUT /configuration/locations/{locationId}/slots` | Same |
| Readiness | `GET /tenants/{id}/readiness` | Same |

### Known limitations

- **Configuration for non-default tenants**: `PUT /configuration/parking-policy` and `/locations/{id}/slots` use the tenant from the JWT claim. The provisioning script can only apply Configuration for the tenant that the `ADMIN_USER` token belongs to. For the `demo` tenant with `tenant-admin`, this works end-to-end. For `acme-corp` (no Keycloak users in local realm), Configuration is provisioned on the next step when an `acme-corp` admin token is available.
- **Keycloak user creation**: `provision-tenant.sh` does not create Keycloak users. Use `./tools/dev-setup-auth.sh` for the local realm import; cross-tenant user provisioning via Keycloak Admin API requires a separate step.

## Testing Split

Use the right tool for each test level:

| Test level | Recommended tool | Purpose |
| --- | --- | --- |
| Unit and slice tests | `dotnet test` / existing test projects | Fast behavioral validation in CI. |
| Repository and component integration | Testcontainers where deterministic CI coverage is needed | MongoDB, Dapr, and broker behavior around one service. |
| Local full-stack smoke | `./tools/start-local-harness.sh` | Verify services, dependencies, gateway, logs, and mobile API base URL together. |
| Manual device smoke | Expo Go, simulator, or emulator | Verify real mobile navigation, auth, rendering, and error states. |
| Hosted demo evidence | Demo environment runbook | Prove external evaluator path with HTTPS, seeded users, and operational evidence. |
