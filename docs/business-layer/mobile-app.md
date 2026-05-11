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

## Notifications
- **Booking Alerts**: Receive booking confirmations
- **Reminder Settings**: Configure notification preferences
