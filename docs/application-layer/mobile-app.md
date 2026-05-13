---
title: Mobile App
---

The mobile application is the employee self-service client for FPS. It uses React Native with Expo so the team can build and test the app without native iOS or Android project maintenance in early slices.

## MOB001 Application Shape

The first implementation slice creates only the app shell:

- Expo managed TypeScript app under `code/mobile/fps-mobile`.
- Shared generated API types imported from `code/clients/typescript`.
- Central API configuration for base URL and bearer token.
- Development-only token handoff screen.
- Authenticated navigation placeholders for Home, My Bookings, New Booking, Notifications, and Profile.
- Shell-level loading, empty, error, unauthenticated, and unreachable-backend states.

## Later Slices

Later mobile slices will add real login, booking request flows, cancellation, usage confirmation, notification streaming/push, profile editing, and production app packaging.

## MOB002 My Bookings

MOB002 is the first read-only employee feature after the app shell:

- My Bookings calls `GET /bookings` with authenticated backend scoping.
- The app does not send tenant ID or requestor ID.
- Booking cards show date, time slot, location, status, employee-visible reason, safe allocation label, and next-action label when returned by the API.
- The screen supports refresh, empty state, API error, invalid token, unreachable backend, and cursor pagination.
- Booking actions remain disabled/placeholders until later mobile slices implement their workflows.

## MOB003 Real Login

MOB003 replaces the development-only token handoff with a real mobile login flow while keeping the current app shell and generated API contract.

Application responsibilities:

- Start an Expo-compatible OIDC Authorization Code + PKCE login flow from the unauthenticated state.
- Complete the browser callback and store only the session data needed for API calls and session restoration.
- Validate the session by calling `GET /me` before entering the authenticated shell.
- Attach the bearer token through the existing mobile API access layer.
- Clear local session state on logout.
- Handle login cancelled, login failed, expired/invalid token, and unreachable backend states without crashing.

Application constraints:

- Do not send tenant ID, requestor ID, user ID, or role data from the mobile app for employee API scoping.
- Do not hardcode secrets, tokens, tenant IDs, user IDs, or developer-machine URLs.
- Do not introduce booking actions or backend behavior changes in MOB003.
- Prefer Expo managed workflow packages and avoid generated `ios/` or `android/` projects unless a later native-build slice explicitly requires them.
