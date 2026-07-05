# fps-mobile

Expo managed React Native + TypeScript app shell for FairSpot.

This package contains the Expo managed React Native employee app. It started as
the MOB001 app shell and now includes the current employee mobile flow for login,
bookings, notifications, and read-only profile/vehicle facts. Native packaging,
push delivery, profile editing, and pilot polish remain later scope.

## Prerequisites

- Node.js 20.x (matches CI)
- npm 10+ (or another package manager that respects `package-lock.json`)
- An Expo Go install on a phone, an iOS simulator, or an Android emulator. The
  web target also works for shell smoke-testing.

## Install

```sh
cd code/mobile/fps-mobile
npm install
```

The package depends on `@robertvejvoda/fairspot-api-client` via a local `file:../../clients/typescript`
reference. The generated types are consumed type-only — no DTOs are copied by
hand.

## Run

```sh
npm run start          # Expo Dev Server (choose target interactively)
npm run ios            # iOS simulator (macOS only)
npm run android        # Android emulator
npm run web            # Web target
```

If the QR code does not scan from a physical phone, start Expo with an explicit
host mode:

```sh
npm run start -- --lan --clear
npm run start -- --tunnel --clear
```

Use `--lan` when the phone and development machine are on the same reachable
network. Use `--tunnel` when Wi-Fi isolation, VPN, firewall rules, or multiple
network adapters prevent the phone from reaching the LAN URL. In Expo Go, use
the manual URL entry if the QR itself is hard to scan.

The app can use real OIDC login when the environment is configured, or a
developer session for smoke testing. For developer sessions, paste an API base
URL and a bearer token issued by a development Identity service. Values are
stored in `AsyncStorage` on the device only — they are never bundled, committed,
or sent anywhere off-device.

For local OIDC smoke testing from a physical phone, prefer the repository helper
from the repo root:

```sh
sh ./tools/start-smoke-mobile.sh
```

The script configures Expo with the local `fps-mobile-dev` client, a
device-reachable Keycloak issuer, and a device-reachable API base URL. If
Tailscale is installed, it prefers the development machine's Tailscale IPv4
address. Override with `FPS_MOBILE_HOST`, `FPS_MOBILE_KEYCLOAK_URL`, or
`FPS_MOBILE_API_BASE_URL` when needed.

For repeatable local overrides, copy `mobile-env.sample` to `.env.local` in
this directory. `.env.local` is ignored by Git and loaded by
`start-smoke-mobile.sh`. Issuer URL, client ID, scopes, and API base URL are
public runtime settings; do not put secrets in Expo config or local mobile env
files.

The mobile app expects one API base URL that exposes the employee endpoints for
Identity, Booking, Notification, and Profile. A physical device cannot use the
developer machine's `localhost`; use a LAN-reachable gateway, tunnel, or hosted
demo URL. The full device test runbook is in
[`docs/production/mobile-device-testing.md`](../../../docs/production/mobile-device-testing.md).

To clear stored credentials, open **Profile → Clear developer session** or call
the corresponding option on the debug-session screen.

## Typecheck

```sh
npm run typecheck      # tsc --noEmit
```

CI runs the same script against this directory on every PR that touches
`code/**`, `tools/**`, or `.github/workflows/**`.

## What is in scope here

| Concern | Status |
| --- | --- |
| Expo Router file-based navigation | Yes |
| Five-state shell (loading / empty / error / unauthenticated / unreachable) | Yes |
| `GET /me` session verification | Yes |
| Dev-only paste-token + API base URL screen | Yes |
| Real login / SSO using OIDC Authorization Code + PKCE | Yes, when runtime config is provided |
| Tabs: Home, My Bookings, New, Notifications, Profile | Yes |
| My Bookings list | Yes |
| Booking submission | Yes |
| Booking cancellation / usage confirmation | Yes |
| Notification list, unread count, mark-read, and polling fallback | Yes |
| Read-only profile and vehicle facts | Yes |
| Type-only imports from generated client | Yes |
| Token refresh/session polish | Partial — `MOB009` |
| Draw/allocation detail | No — `MOB008` |
| Profile editing / notification preferences | No — later slice |
| Push / native projects / EAS packaging | No — later slice |
