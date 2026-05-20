# Local Test Harness

This page defines the local run path for FPS testing. The immediate goal is to make backend, mobile, and demo smoke testing repeatable. The longer-term goal is a one-command local harness, preferably through .NET Aspire or an equivalent AppHost, without replacing the production deployment model.

## Current Baseline

Use Docker Compose for shared infrastructure and run the .NET services from source.

From the repository root:

```sh
docker network create fps_network
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

If `fps_network` already exists, Docker will report that and the command can be ignored.

Run services as needed:

```sh
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
| Profile | `http://localhost:5197` |
| Notification | `http://localhost:5157` |

Configuration, Audit, and Reporting should be checked from their launch profiles before they are added to a scripted smoke path.

Stop shared infrastructure:

```sh
docker compose -f code/infrastructure/docker-compose.yaml down
```

## Mobile Testing Implication

The mobile app expects one API base URL. The current baseline is enough for service-level checks, but it is not enough for a full mobile device pass because Identity, Booking, Notification, and Profile run on separate ports.

For a full mobile pass, provide one gateway URL that routes:

| Mobile path | Target service |
| --- | --- |
| `/me` | Identity |
| `/bookings` and booking actions | Booking |
| `/notifications` and notification actions | Notification |
| `/profile/snapshot` | Profile |

On a physical phone, use a LAN-reachable gateway URL such as `http://<dev-machine-ip>:<gateway-port>`, not `localhost`.

## Preferred Next Harness

Create the local seed and token path before the AppHost/gateway work. Without stable identity, seeded data, and a repeatable bearer-token command, mobile and full-stack testing stays manual and inconsistent.

First implementation slice: `OPS006A Local Demo Seed And Dev Token`.

`OPS006A` should provide:

- local Keycloak realm, client, users, tenant claims, and roles;
- seeded tenant, location, policy, slot, profile, booking, and notification data;
- a local-only token helper such as `./tools/dev-token.sh employee1`;
- a reset/reseed path that can be rerun without manual database edits;
- documentation for the exact run order from clean local infrastructure to a mobile bearer token.

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
2. Run only the backend services needed for the scenario.
3. Verify service endpoints individually.
4. Run mobile Expo smoke for UI/device behavior.
5. Record that full mobile end-to-end testing is blocked until a gateway or hosted demo URL exists.
