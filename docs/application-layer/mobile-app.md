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
