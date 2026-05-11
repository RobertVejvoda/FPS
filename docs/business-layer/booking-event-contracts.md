---
title: Booking Event Contracts
---

## Purpose

Booking events are the integration contract between Booking, Notification, Audit, Reporting, and future read-model consumers. They must be stable enough for vertical slice implementation while still allowing payloads to evolve.

These contracts apply to the Booking slices defined in [Booking Vertical Slices](./booking-vertical-slices).

## Event Envelope

Every Booking event must include this envelope.

| Field | Meaning |
| --- | --- |
| `eventId` | Globally unique event ID. Used for idempotency. |
| `eventType` | Stable event type name. |
| `eventVersion` | Integer event schema version. Starts at `1`. |
| `occurredAt` | Server timestamp when the business event occurred. |
| `tenantId` | Tenant that owns the event. |
| `correlationId` | Request/workflow correlation ID. |
| `causationId` | Command, workflow activity, or source event that caused this event. |
| `actorType` | `employee`, `hr`, `admin`, `system`, or `integration`. |
| `actorId` | Actor ID when available. System events may use a stable system actor. |
| `source` | Producing service, normally `booking`. |
| `payload` | Event-specific payload. |

Events must not include secrets, stack traces, or private details about unrelated employees.

## Idempotency

Consumers must deduplicate by `eventId`. When an event causes channel-specific notifications, the notification deduplication key is:

```text
eventId + recipientId + notificationType + channel
```

Commands and workflows that retry must reuse the same source event ID for the same business outcome. They must not create new event IDs for duplicate retries of an already persisted outcome.

## Core Event Types

| Event type | Slice | Purpose |
| --- | --- | --- |
| `booking.requestSubmitted` | B001 | Request accepted into the queue. |
| `booking.requestRejected` | B001, B002, B004 | Request rejected with an employee-visible reason. |
| `booking.requestCancelled` | B003, B005 | Request or allocated reservation cancelled. |
| `booking.slotAllocated` | B002, B004, B005 | Slot assigned to a request. |
| `booking.drawStarted` | B004 | Draw attempt started. |
| `booking.drawCompleted` | B004 | Draw attempt completed. |
| `booking.drawFailed` | B004 | Draw attempt failed or requires manual intervention. |
| `booking.penaltyApplied` | B005, B007, B010 | Penalty score applied or adjusted. |
| `booking.usageConfirmed` | B006 | Allocated request confirmed as used. |
| `booking.noShowRecorded` | B007 | Allocated request marked no-show. |
| `booking.requestExpired` | B004, B007, expiry process | Request/allocation is no longer actionable. |
| `booking.manualCorrectionApplied` | B010 | Authorized correction applied. |

Implementation may use internal class names that differ from `eventType`, but published `eventType` values must remain stable.

## Common Booking Payload Fields

Most Booking events should include these payload fields when relevant:

| Field | Meaning |
| --- | --- |
| `bookingRequestId` | Booking request ID. |
| `requestorId` | Affected employee/requestor. |
| `locationId` | Requested or allocated location. |
| `date` | Requested parking date. |
| `timeSlot` | Requested time slot. |
| `previousStatus` | Status before the event, when applicable. |
| `newStatus` | Status after the event, when applicable. |
| `reasonCode` | Stable machine-readable reason. |
| `reasonText` | Employee-visible reason when the event affects the employee. |
| `affectedRecipientIds` | Optional list of additional affected employee IDs that must receive employee-facing notifications for the same business outcome. Use only IDs, not names, emails, or private profile data. |

Reason code values are defined in [Booking Reason Codes](./booking-reason-codes).

## Allocation Payload Fields

`booking.slotAllocated` must include:

- `allocationId`;
- `bookingRequestId`;
- `requestorId`;
- `slotId` or `poolId`;
- `allocationSource`: `draw`, `sameDay`, `reallocation`, or `manualCorrection`;
- `drawAttemptId` when allocation came from a Draw or reallocation based on a Draw;
- `reallocatedFromBookingRequestId` when allocation came from cancellation reallocation;
- `employeeVisibleSlotLabel` when the slot may be shown to the employee.

The event must not expose lottery seed, candidate order, or other employees to employee-facing consumers.

When an allocation comes from reallocation, Booking should publish enough recipient data for Notification to notify both the newly allocated requestor and the original requestor whose cancellation released the slot. If a single event represents both effects, include the additional employee ID in `affectedRecipientIds`; otherwise publish separate employee-facing events with each affected employee as `requestorId`.

## Draw Payload Fields

`booking.drawStarted`, `booking.drawCompleted`, and `booking.drawFailed` must include:

- `drawAttemptId`;
- `drawKey`: tenant, location, date, and time slot;
- `algorithmVersion`;
- `startedAt`;
- `completedAt` when available;
- `status`;
- request, allocation, rejection, pending waitlist, and company-car overflow counts when available;
- `failureReasonCode` and `failureReasonText` for failed attempts.

Seed, candidate order, weights, and detailed per-request diagnostics belong in the Draw attempt/audit record. They may be referenced by `drawAttemptId` but must not be included in employee-facing notifications.

## Penalty Payload Fields

`booking.penaltyApplied` must include:

- `penaltyId`;
- `bookingRequestId`;
- `requestorId`;
- `penaltyType`: `lateCancellation`, `noShow`, or `manualAdjustment`;
- `score`;
- `effectiveAt`;
- `expiresAt`;
- `sourceEventId`;
- `reasonCode`;
- `reasonText`.

Penalty events must be idempotent by `sourceEventId` and `penaltyType`.

## Manual Correction Payload Fields

`booking.manualCorrectionApplied` must include:

- `correctionId`;
- `bookingRequestId`;
- `correctionType`;
- `oldValue`;
- `newValue`;
- `reason`;
- `effectiveAt`;
- related allocation or penalty IDs when applicable.

Manual correction events never replace or delete previous events.

## Consumer Expectations

Notification consumes Booking events to create mandatory in-app and email notifications for critical operational outcomes.

Audit consumes all Booking events and stores append-only audit records. Audit may store diagnostic references that are not visible to employees.

Reporting/read models may consume Booking events to materialize booking history, utilization, rejection summaries, and fairness metrics.

Consumers must tolerate additive payload fields and ignore unknown fields.
