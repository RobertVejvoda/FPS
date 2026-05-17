# fps-mobile

Expo managed React Native + TypeScript app shell for the Fair Parking System.

This package implements the mobile employee self-service experience. It started
as the MOB001 slice (app shell, navigation, API configuration, development-only
bearer-token handoff) and has been incrementally expanded through MOB002-MOB006
to include real OIDC login, My Bookings, booking submission/actions, and
notification consumption.

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

The package depends on `@fps/api-client` via a local `file:../../clients/typescript`
reference. The generated types are consumed type-only — no DTOs are copied by
hand.

## Run

```sh
npm run start          # Expo Dev Server (choose target interactively)
npm run ios            # iOS simulator (macOS only)
npm run android        # Android emulator
npm run web            # Web target
```

The app starts on a developer-credential gate. Paste an API base URL (for
example `http://localhost:5100`) and a bearer token issued by a development
Identity service. Values are stored in `AsyncStorage` on the device only — they
are never bundled, committed, or sent anywhere off-device.

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
| Real OIDC login with Authorization Code + PKCE | Yes (MOB003) |
| My Bookings list with filters and pagination | Yes (MOB002) |
| Booking submission | Yes (MOB004) |
| Booking cancellation and usage confirmation | Yes (MOB005) |
| Notification list with polling fallback | Yes (MOB006) |
| Unread count badge on Alerts tab | Yes (MOB006) |
| Mark notification as read | Yes (MOB006) |
| Type-only imports from generated client | Yes |
| Profile and vehicle detail display | No — MOB007 |
| Draw status and allocation detail | No — MOB008 |
| Push notifications / SSE with auth headers | No — later slice |
| Session expiry / refresh recovery | No — MOB009 |
| Native projects / EAS packaging | No — later slice |
