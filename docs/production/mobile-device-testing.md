# Mobile Device Testing Plan

This plan defines when and how to test the FPS mobile app on a real device, simulator, or emulator. It separates the current developer smoke path from the pilot-grade test path needed after the remaining mobile polish slices.

## When To Test

| Moment | Purpose | Expected depth |
| --- | --- | --- |
| After each mobile slice | Catch navigation, auth, API-contract, and rendering regressions early. | Developer smoke on Expo Go, simulator, emulator, or web. |
| After `MOB008` | Validate that the employee can understand booking, notification, profile, vehicle, and allocation status without hidden lottery details. | Full device scenario pass against a reachable API profile. |
| During `MOB009` | Validate session recovery, environment configuration, loading/empty/error states, accessibility, and production polish. | Pilot-grade device pass with evidence. |
| Before external demo or pilot | Prove the employee mobile journey works with seeded data and no local-only assumptions. | Hosted/demo environment pass with recorded evidence. |

Do not wait until the whole product is finished before testing on a device. Use device smoke testing after every mobile UI slice, then reserve pilot acceptance for `MOB009`.

## Run Prerequisites

- Node.js 20.x and npm 10+.
- Expo Go on a phone, or an iOS simulator / Android emulator.
- Mobile dependencies installed from `code/mobile/fps-mobile`.
- A single API base URL reachable from the device.
- A valid OIDC login configuration or a development bearer token from `./tools/dev-auth.sh`.
- Synthetic tenant, user, vehicle, booking, and notification data.

Secret values, bearer tokens, and real user data must not be committed, pasted into issues, or captured in screenshots.

## How To Run The App

From the repository root:

```sh
cd code/mobile/fps-mobile
npm install
npm run typecheck
npm run start
```

Use the Expo prompt to open the app:

```sh
npm run ios
npm run android
npm run web
```

For a physical phone, scan the Expo QR code from Expo Go and keep the phone on a network that can reach the development machine. If the QR code does not scan or the phone cannot open it, restart Expo with an explicit host mode:

```sh
npm run start -- --lan --clear
npm run start -- --tunnel --clear
```

Use `--lan` when the phone and development machine are on the same reachable network. Use `--tunnel` when Wi-Fi isolation, VPN, firewall rules, or multiple network adapters prevent the phone from reaching the LAN URL. If scanning still fails, use Expo Go's manual URL entry with the `exp://...` URL printed by Expo.

A phone cannot use the developer machine's `localhost`; use a LAN address such as `http://<dev-machine-ip>:<gateway-port>`, a tunnel, or a hosted demo URL for backend API access.

## API Run Profile

The mobile app expects one API base URL for employee endpoints such as:

- `GET /me`
- `GET /bookings`
- booking action endpoints used by the app
- notification endpoints
- `GET /profile/snapshot`

The preferred test profile is a demo or local gateway URL that exposes those endpoints under one origin and validates the same bearer token or OIDC session.

The local Envoy gateway (OPS006B) routes all mobile employee endpoints under one origin at `http://localhost:10000` (simulator) or `http://<dev-machine-ip>:10000` (physical device). This closes the routing gap — the mobile app can now be configured with a single API base URL. However, full E2E mobile testing still requires Dapr sidecars for Booking (plain `dotnet run` returns 500) and seeded profile data for `/profile/snapshot`. See the [Local Test Harness](./local-test-harness) gateway section for the current smoke commands, known gaps, and Linux notes.

The local run path and preferred Aspire/AppHost direction are documented in the [Local Test Harness](./local-test-harness) page.

Local infrastructure and demo auth can be started for service-level verification:

```sh
docker compose -f code/infrastructure/docker-compose.yaml up -d
./tools/dev-setup-auth.sh
source ./tools/dev-env.sh
./tools/dev-auth.sh employee1
```

Individual services can then be run from their projects when needed:

```sh
source ./tools/dev-env.sh
dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
dotnet run --project code/server/Booking/FPS.Booking.API/FPS.Booking.API.csproj
dotnet run --project code/server/Configuration/FPS.Configuration/FPS.Configuration.csproj
dotnet run --project code/server/Audit/FPS.Audit/FPS.Audit.csproj
dotnet run --project code/server/Reporting/FPS.Reporting/FPS.Reporting.csproj
dotnet run --project code/server/Profile/FPS.Profile/FPS.Profile.csproj
dotnet run --project code/server/Notification/FPS.Notification/FPS.Notification.csproj
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

These service URLs are useful for API verification, but the mobile app still needs a single base URL for a full-flow device pass.

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

## Authentication

Use real OIDC login when the environment has a configured issuer, client ID, scopes, redirect URI, and seeded users. This is the preferred path for demo and pilot evidence.

For developer smoke, use the Developer Session screen to paste:

- API base URL;
- development bearer token from `./tools/dev-auth.sh employee1`.

Available local users are `employee1`, `employee2`, `employee3`, and `hr-admin`. Source `./tools/dev-env.sh` in each service shell so backend services validate tokens issued by the local `fps-local` Keycloak realm.

Clear the session after testing from the Profile screen or debug-session screen. Development token generation and seeded OIDC demo users should be documented before `MOB009` is accepted.

## Test Data

Use synthetic data only. The minimum useful data set is:

| Data | Why it matters |
| --- | --- |
| Normal employee | Baseline login, booking, notification, and profile path. |
| Company-car employee | Priority and employee-visible profile facts. |
| Employee with no active vehicle | Empty vehicle state. |
| Employee with active vehicle | Vehicle rendering and eligibility facts. |
| Employee with accessibility/reserved-space eligibility | Eligibility display without exposing internal policy details. |
| Pending booking | Booking list and cancellation path. |
| Allocated booking | Allocation display and confirm-usage path. |
| Rejected or expired booking | Employee-visible reason display. |
| Unread and read notifications | Notification count, filtering, and mark-read behavior. |

## Scenario Checklist

| Scenario | Expected result |
| --- | --- |
| App starts from clean install | App opens without crash and shows login or developer session state. |
| Login or developer session | Valid credentials enter the authenticated shell; invalid credentials show recoverable errors. |
| Session restore | Closing and reopening the app restores a valid session or asks for login if expired. |
| My Bookings | Upcoming and recent bookings render with employee-safe status, dates, locations, and actions. |
| Empty bookings | Empty state is clear and does not look broken. |
| Submit booking | Employee can submit a valid request and sees validation feedback for invalid input. |
| Cancel booking | Eligible pending/allocated booking can be cancelled with clear result feedback. |
| Confirm usage | Eligible booking can be confirmed without exposing backend-only details. |
| Notifications | List, unread count, filters, refresh, and mark-read behavior work with seeded records. |
| Profile snapshot | Identity, profile status, eligibility facts, snapshot version, and active vehicles render correctly. |
| Missing profile or no vehicles | The app shows a clear non-crashing state. |
| Backend unreachable | App shows retryable error states and does not discard session silently. |
| Invalid or expired token | App returns to login or shows a clear authentication recovery path. |
| Small screen and large text | Main screens remain usable without clipped controls or hidden actions. |
| Reset session | Clearing the developer session removes stored base URL and token. |
| Secret hygiene | Screenshots, logs, and issue comments contain no tokens or real personal data. |

## Evidence To Record

For every meaningful device pass, record:

- date and tester;
- git commit SHA;
- device model, OS version, and Expo Go or build version;
- API profile used, without secrets;
- seeded user role and scenario set;
- pass/fail result with blocker links;
- screenshots only when they contain no tokens or real personal data;
- relevant app/backend logs with secrets redacted.

## Exit Criteria

Developer smoke is good enough when:

- `npm run typecheck` passes;
- the app opens on at least one Expo target;
- authentication or developer session can enter the shell;
- main tabs render without crashes;
- API errors are visible and recoverable.

Pilot-grade mobile acceptance requires:

- one reachable API base URL for Identity, Booking, Notification, and Profile employee endpoints;
- real OIDC login or documented seeded demo users;
- repeatable test data and reset path;
- session expiry and refresh behavior validated;
- main happy-path and failure scenarios completed on at least one physical iOS or Android device;
- accessibility and small-screen checks completed;
- evidence recorded for the hosted/demo environment.

## Follow-Up Gaps

- Provide a local or hosted API gateway URL that routes all mobile employee endpoints under one origin.
- Add an Aspire AppHost or equivalent local harness for one-command full-stack smoke testing.
- Add a repeatable seed/reset command for the mobile demo data set.
- Decide whether `MOB009` should include automated mobile component or end-to-end tests beyond TypeScript validation.
