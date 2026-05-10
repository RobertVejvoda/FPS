---
title: Notification Requirements
---

## Purpose

FPS notifications keep employees, HR, and administrators informed about allocation outcomes and policy-sensitive events. For v1, notification delivery is part of the core parking workflow, not an optional enhancement.

## Channel Decision

v1 requires both:

- in-app notifications;
- email notifications.

Push notifications may be added later, but they are not required for v1.

## Notification Classes

| Class | Meaning | User preference allowed |
| --- | --- | --- |
| Critical operational | Required to understand booking, allocation, penalty, or manual decision outcomes. | No |
| Reminder | Helps users avoid missed usage or cancellation windows. | Yes |
| Informational | Product, maintenance, or non-critical updates. | Yes |

Critical operational notifications are mandatory and must be sent through both in-app and email channels.

## Mandatory V1 Events

FPS must notify affected employees for these events:

| Event | Recipient | Required channels |
| --- | --- | --- |
| Request submitted | Requestor | In-app, email |
| Request rejected | Requestor | In-app, email |
| Slot allocated | Requestor | In-app, email |
| Slot allocated by reallocation | Requestor | In-app, email |
| Request cancelled before allocation | Requestor | In-app, email |
| Allocated reservation cancelled | Requestor | In-app, email |
| Slot released and reallocated | Original requestor and new requestor | In-app, email |
| Late-cancellation penalty applied | Requestor | In-app, email |
| No-show recorded | Requestor | In-app, email |
| No-show penalty applied | Requestor | In-app, email |
| Manual correction or override | Affected requestor | In-app, email |
| Draw completed | Requestors included in the Draw | In-app, email |

When a request remains `Pending` after the Draw because it was eligible but no matching capacity was available, the Draw completed notification must explain that the request is still waiting for a released slot until the requested time slot expires.

FPS must notify HR or configured administrators for these events:

| Event | Recipient |
| --- | --- |
| Company-car overflow rejection occurs | HR/facility manager |
| Draw fails or requires manual intervention | HR/facility manager |
| Policy publication fails validation | Administrator/configuration manager |
| Manual correction is applied | HR/facility manager and auditor where configured |

## Message Content

Employee-facing notifications must be clear and avoid implementation details.

Each notification must include:

- notification type;
- request date and time slot;
- location where relevant;
- current request status;
- short human-readable reason;
- next action when one exists;
- timestamp.

Notifications must not expose:

- random seed;
- internal algorithm details;
- stack traces;
- private details about other employees;
- audit-only diagnostic fields.

## Employee-Facing Message Examples

| Event | Example message |
| --- | --- |
| Request submitted | Your parking request was submitted and is waiting for allocation. |
| Request rejected | Your parking request could not be allocated because no matching slot is available. |
| Slot allocated | A parking slot was allocated to your request. |
| Slot allocated by reallocation | A parking slot became available and was allocated to your request. |
| Request cancelled before allocation | Your parking request was cancelled. No penalty was applied. |
| Allocated reservation cancelled | Your allocated parking reservation was cancelled. |
| Late-cancellation penalty applied | A late-cancellation penalty was applied because the reservation was cancelled after allocation. |
| No-show recorded | Your allocated parking slot was not confirmed as used. |
| No-show penalty applied | A no-show penalty was applied according to parking policy. |
| Manual correction | Your parking request was updated by an authorized administrator. |
| Draw completed | Parking allocation for your requested time slot is complete. |

## Preferences

User preferences may control:

- reminder notifications;
- informational notifications;
- preferred reminder timing;
- future optional channels such as push notifications.

User preferences must not disable critical operational notifications for booking, allocation, cancellation, reallocation, no-show, penalty, or manual correction outcomes.

## Delivery and Idempotency

Notification delivery must be idempotent.

Rules:

- Booking workflows publish notification events asynchronously after the authoritative booking state change is persisted.
- the same source event must not create duplicate in-app notifications;
- the same source event must not send duplicate emails;
- each notification should have a stable deduplication key based on event ID, recipient, notification type, and channel;
- retries must use the same deduplication key;
- in-app notification creation and email sending may complete independently;
- failure in one channel must not silently suppress the other channel.
- notification delivery failure must not roll back a completed booking cancellation, allocation, reallocation, penalty, or Draw outcome.

## Delivery Failure Behavior

If in-app notification creation fails:

- FPS must log the failure;
- FPS must retry according to infrastructure policy;
- email delivery should still be attempted.

If email delivery fails:

- FPS must log the failure;
- FPS must retain the in-app notification;
- FPS should retry according to infrastructure policy;
- persistent email failure should be visible to support or administrators.

FPS must not roll back a completed booking or allocation solely because email delivery failed.

## Notification History

Employees must be able to view their in-app notification history.

Notification history should support:

- unread/read status;
- timestamp;
- notification type;
- related request ID;
- related date/time slot;
- message text;
- basic filtering by unread, booking, penalty, or system notification.

## Audit and Reporting

Notification delivery is not the source of truth for booking outcomes. Booking, allocation, penalty, and audit records remain authoritative.

Notification service must record:

- source event ID;
- recipient;
- channel;
- notification type;
- deduplication key;
- delivery status;
- failure reason when delivery fails;
- timestamps for creation, send attempt, success, and failure.

## Acceptance Criteria For Implementation

- Given a request is submitted, when FPS accepts it into the queue, then the requestor receives both in-app and email notifications.
- Given a request is allocated, when the allocation is persisted, then the requestor receives both in-app and email notifications.
- Given a request is rejected, when the rejection is persisted, then the requestor receives both in-app and email notifications with a clear reason.
- Given an allocated reservation is cancelled, when the cancellation is persisted, then the original requestor receives both in-app and email notifications.
- Given a released slot is reallocated, when the new allocation is persisted, then both original and new affected requestors receive both in-app and email notifications.
- Given a penalty is applied, when the penalty is persisted, then the affected requestor receives both in-app and email notifications.
- Given a no-show is recorded, when the no-show status is persisted, then the affected requestor receives both in-app and email notifications.
- Given the same source event is processed twice, when notifications are generated, then FPS does not create duplicate in-app notifications or duplicate emails.
- Given email delivery fails, when the in-app notification succeeds, then booking state remains unchanged and email retry/failure is recorded.
- Given a user disables reminders, when a critical operational event occurs, then FPS still sends both in-app and email notifications.
