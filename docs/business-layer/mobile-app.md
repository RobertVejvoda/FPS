---
title: Mobile App
---

## User Interface
- **Responsive Design**: Optimized for mobile devices
- **Intuitive Navigation**: Simplified interface for booking flow
- **Accessible Defaults**: Readable text, touch-friendly controls, and clear loading/error states

## Booking Features
- **Current Status**: See whether the employee has an active or upcoming parking allocation
- **My Bookings**: View upcoming and recent booking requests
- **New Booking Entry Point**: Start the request flow once the booking UI slice is implemented
- **Notifications Entry Point**: Reach booking-related alerts once notification APIs are exposed to mobile

## User Profile
- **Current User**: Show authenticated user and tenant context from `GET /me`
- **Vehicle Snapshot**: Read vehicle/company-car/accessibility information once Profile APIs are available
- **Settings Entry Point**: Hold later preferences, token/session controls, and support links

## MOB001 Boundary

The first mobile slice is an app shell only. It establishes Expo, TypeScript, navigation, API configuration, generated API-client type consumption, and a development-only bearer-token handoff.

MOB001 does not implement production login, booking submission, booking cancellation, push/SSE notifications, payments, maps, feedback, profile editing, or app-store packaging.

## MOB002 Boundary

The second mobile slice implements the read-only My Bookings screen. It shows the authenticated employee's own booking requests from `GET /bookings`, including dates, time slots, locations, statuses, employee-visible reasons, and safe allocation labels when returned by the API.

MOB002 does not implement booking submission, cancellation, usage confirmation, no-show handling, Draw status, real login, push/SSE notifications, maps, payments, feedback, profile editing, or backend behavior changes.

## MOB003 Boundary

The third mobile slice replaces the development bearer-token handoff with a real employee login flow. The app should authenticate through the configured OIDC provider, obtain a bearer token through an Expo-compatible Authorization Code + PKCE flow, call `GET /me`, and enter the existing authenticated shell when the session is valid.

MOB003 must preserve the existing security boundary: the mobile app never supplies tenant ID, requestor ID, or user ID for employee scoping. Identity and Booking continue to resolve those values from authenticated claims on the backend.

MOB003 includes:

- login start, callback handling, authenticated session restoration, and logout;
- session validation through `GET /me`;
- clear states for unauthenticated, login cancelled, login failed, invalid/expired token, and unreachable backend;
- secure local token/session handling using Expo-compatible storage and browser-auth primitives;
- development configuration for API base URL, issuer/authorization endpoint, client ID, scopes, and redirect URI.

MOB003 does not implement booking submission, cancellation, usage confirmation, push/SSE notifications, profile editing, native app-store packaging, tenant/user selection, Keycloak provisioning, or backend business behavior changes.

## Notifications
- **Booking Alerts**: Receive booking confirmations
- **Reminder Settings**: Configure notification preferences
