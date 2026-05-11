---
title: Mobile App
---

The mobile application uses React Native with Expo managed workflow. Early mobile work should avoid native project generation and app-store packaging until the product flows and API contracts are stable.

## Key Components

- [Identity](./identity)
- [Booking](./booking)
- [Profile](./profile)
- [Notification](./notification)
- [Feedback](./feedback)

## Implementation Baseline

| Concern | Decision |
| --- | --- |
| Framework | React Native + Expo managed workflow |
| Language | TypeScript |
| Repository path | `code/mobile/fps-mobile` |
| API contract | Generated types from `code/clients/typescript` |
| Auth in MOB001 | Development-only bearer token handoff; production login is later |
| Native projects | Do not commit generated `ios/` or `android/` directories in MOB001 |
| Validation | Mobile TypeScript typecheck, wired into CI when the mobile project is added |

## Packaging

![Packaging](../images/fps-software-pack-mobile.png)

| Software Component | Type | Purpose |
|------------------- | ---- | ------- |
| login-ui | GUI | User interface for authentication |
| login-svc | Service | Authentication service |
| booking-ui | GUI | User interface for bookings |
| profile-ui | GUI | User interface for profile management |
| notification-svc | Service | Handles push notifications |
| feedback-ui | GUI | User interface for feedback |
