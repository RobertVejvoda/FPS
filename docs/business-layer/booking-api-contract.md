---
title: Booking API Contract
---

## Purpose

This document standardizes Booking API responses and errors across vertical slices. It should be read with [Booking Vertical Slices](./booking-vertical-slices), [Booking Reason Codes](./booking-reason-codes), and [Booking Authorization](./booking-authorization).

## Identity Rules

Booking APIs must resolve tenant, actor, roles, and request ownership from the authenticated context. Request bodies and query strings must not be trusted for:

- tenant ID;
- authenticated actor ID;
- actor role;
- ownership of a booking request.

## Success Envelope

Single-resource command/query responses should use this shape:

```json
{
  "data": {},
  "meta": {
    "correlationId": "..."
  }
}
```

`data` contains the resource-specific response. `meta.correlationId` should match the request/workflow correlation ID when available.

## List Envelope

List responses such as `GET /bookings` should use this shape:

```json
{
  "items": [],
  "page": {
    "nextCursor": null,
    "pageSize": 50
  },
  "meta": {
    "correlationId": "..."
  }
}
```

Rules:

- Cursors are opaque to clients.
- Do not expose total counts when the count could reveal another user's data.
- `pageSize` defaults and maximums are defined by the slice contract.

## Error Envelope

Booking APIs should use a consistent problem-style error response:

```json
{
  "error": {
    "code": "duplicate_overlap",
    "message": "You already have a request for an overlapping time slot.",
    "target": "timeSlot",
    "details": []
  },
  "meta": {
    "correlationId": "..."
  }
}
```

Fields:

| Field | Meaning |
| --- | --- |
| `error.code` | Stable reason code from [Booking Reason Codes](./booking-reason-codes). |
| `error.message` | Employee-safe or actor-safe message. Must not expose internals. |
| `error.target` | Optional field/resource that caused the error. |
| `error.details` | Optional list of additional safe validation details. |
| `meta.correlationId` | Correlation ID for support and audit. |

## HTTP Status Guidance

| Scenario | HTTP status | Reason-code examples |
| --- | ---: | --- |
| Successful command accepted for async processing | `202 Accepted` | n/a |
| Successful synchronous command/query | `200 OK` | n/a |
| Resource created synchronously | `201 Created` | n/a |
| Malformed request shape or invalid parameter format | `400 Bad Request` | `policy_validation_failed` when policy input is invalid |
| Unauthenticated request | `401 Unauthorized` | `unauthorized` |
| Authenticated but not allowed | `403 Forbidden` | `unauthorized` |
| Resource missing or hidden by ownership/tenant scope | `404 Not Found` | `not_found_or_not_allowed` |
| Optimistic concurrency conflict or stale `oldValue` | `409 Conflict` | `concurrency_conflict` |
| Domain validation failure | `422 Unprocessable Entity` | `duplicate_overlap`, `after_cutoff`, `daily_cap_exceeded`, `invalid_status_transition` |

For employee self-service access to another employee's resource, prefer `404 Not Found` with `not_found_or_not_allowed` so the API does not reveal whether the resource exists.

## Booking Status Response Fields

Booking responses that expose a request outcome should include:

- `bookingRequestId`;
- `status`;
- `reasonCode` when the current status needs explanation;
- `reasonText` when the actor may see the explanation;
- `requestedDate`;
- `timeSlot`;
- `locationId` or employee-visible location label;
- timestamps relevant to the response.

Responses must not expose:

- other employees' private data;
- lottery seed;
- candidate order;
- internal weights;
- stack traces;
- audit-only diagnostics;
- hidden slot metadata.

## Command Idempotency

Commands that can be retried must be idempotent for the same business outcome.

Rules:

- Retrying a completed cancellation must not duplicate penalties, reallocations, notifications, or audit records.
- Retrying usage confirmation must not duplicate confirmation notifications or audit records.
- Retrying Draw trigger for an already running/completed Draw key returns the existing attempt reference unless a documented manual correction flow starts a new audited attempt.
- Idempotency keys or source event IDs should be reflected in events and audit records where applicable.

## Validation Details

Validation errors may include `error.details` when they are safe and useful:

```json
{
  "field": "pageSize",
  "code": "invalid_range",
  "message": "pageSize must be between 1 and 100."
}
```

Validation details must not include private data from other employees or internal exception messages.

## Localization

`reasonCode` is stable and not localized. `reasonText` may be localized by API, frontend, or notification service. Localized text must preserve the business meaning of the reason code.
