# Integrations And Events

FairSpot integrations use synchronous calls only where command decisions need current facts. Business outcomes are published as events for Notification, Audit, and DataHub projections.

## Integration Catalog

| Integration / Event Flow | Producer | Consumer | Purpose | Status | Reliability Notes |
| --- | --- | --- | --- | --- | --- |
| Identity claims | IdP / Identity | All authenticated services | Establish tenant, user, and role context. | Partial | Missing tenant/user identity must fail closed. |
| Profile facts lookup | Booking | Profile | Validate employee, vehicle, default vehicle, company-car, accessibility, and eligibility facts during commands. | Partial | Booking must not trust client-submitted profile facts alone. |
| Configuration policy/capacity lookup | Configuration | Booking, Web/Admin | Resolve policy, Draw schedule, location, time slot, and capacity/resource map state. | Partial | Versioning/history matter for auditability. Stub/default paths are not customer-ready. |
| Booking lifecycle events | Booking | Notification, Audit, DataHub | Notify users, preserve evidence, update booking outcome/read-model projections. | Partial | Idempotent consumers and stable source event IDs required. |
| Draw workflow events/status | Booking | DataHub, HR/Admin UI, Audit | Expose Draw lifecycle, next/completed Draw status, counts, and safe explanations. | Placeholder | Seeds/candidate ordering stay audit-controlled, not employee-facing. |
| Customer readiness events | Customer | DataHub, Admin UI, Audit | Build tenant readiness projection and go-live evidence. | Placeholder | Depends on durable Customer storage. |
| Configuration change events | Configuration | Booking, DataHub, Audit | Publish policy/capacity/resource-map changes that affect readiness, Draw, impact preview, and reporting. | Placeholder | Effective dates, version IDs, publication status, and changed capability/closure summaries required. |
| Profile change events | Profile | Booking where needed, DataHub | Update eligibility/default vehicle summaries and readiness. | Placeholder | Avoid copying raw profile history unless needed. |
| Notification delivery events | Notification | DataHub, Audit where required | Summarize delivery health and support diagnostics. | Placeholder | Store status and failure group, not message body. |
| Audit reference events | Audit | DataHub | Index safe evidence references. | Placeholder | DataHub stores references, not raw audit payloads or PII mapping. |
| Audit PII mapping lookup | Audit | Authorized auditor/admin flow | Resolve pseudonymised actors under controlled conditions. | Partial | Lookup reason and audit record are required. |

## Event Envelope

Every DataHub-consumed event should use a stable envelope.

| Field | Requirement |
| --- | --- |
| `eventId` | Globally unique source event ID and DataHub inbox idempotency key. |
| `eventType` | Stable event name from the catalog, such as `booking.requestSubmitted`. |
| `eventVersion` | Integer version. Start at `1`; never change meaning in place. |
| `sourceService` | Owning service that emitted the event. |
| `tenantId` | Required for tenant-scoped projections. |
| `aggregateId` | Owning aggregate/entity ID, such as request ID, tenant ID, policy ID, profile ID, notification ID, or audit record ID. |
| `occurredAt` | Timestamp from the source service when the business fact happened. |
| `publishedAt` | Timestamp when the event left the source outbox/publisher, when available. |
| `correlationId` | Request/workflow correlation ID where available. |
| `actorRef` | Optional pseudonymised actor reference or system actor. Never raw name or email. |
| `payload` | Minimal event-specific fields needed by approved consumers. |

## Initial Event Families

| Event Family | Source | Consumers | Status |
| --- | --- | --- | --- |
| `booking.requestSubmitted`, `booking.requestRejected`, `booking.requestAllocated`, `booking.requestCancelled`, `booking.requestUsed`, `booking.requestNoShow`, `booking.requestExpired` | Booking | Notification, Audit, DataHub | Partial |
| `booking.drawStarted`, `booking.drawCompleted`, `booking.drawFailed` | Booking | DataHub, Audit, HR/Admin UI | Partial |
| `booking.penaltyApplied`, `booking.manualCorrectionApplied` | Booking | Audit, DataHub where approved | Placeholder |
| `customer.tenantCreated`, `customer.tenantReadinessChanged` | Customer | DataHub, Audit, Admin UI | Placeholder |
| `configuration.policyChanged`, `configuration.capacityChanged`, `configuration.resourceMapPublished` | Configuration | Booking, DataHub, Audit | Placeholder |
| `profile.employeeChanged` | Profile | Booking where needed, DataHub | Placeholder |
| `notification.deliveryChanged` | Notification | DataHub, Audit where required | Placeholder |
| `audit.recordCreated` | Audit | DataHub | Placeholder |

## Outbox And Inbox Rules

- Owning services should use an outbox pattern for business events that must survive process crashes.
- Prefer Dapr transactional outbox when the selected Dapr state store supports transactions and outbox behavior.
- Where Dapr transactional outbox is not available, the service must persist pending publication state with deterministic event IDs and retry behavior.
- Direct Dapr pub/sub publish is not enough for critical business events that must survive a crash after state persistence.
- DataHub must record received events in an inbox before applying projections.
- Duplicate Dapr pub/sub delivery is normal; the DataHub inbox idempotency key is `eventId`.
- Projection handlers must be deterministic, tenant-scoped, retryable, and visible through projection health.

## Visible Event Gaps

- Full source-of-truth event catalog needs implementation traceability to code.
- Customer, Configuration, Profile, Notification, and Audit event producers are placeholders.
- Resource-map publication and policy-impact preview event/read contracts need implementation traceability.
- DataHub inbox schema and projection health endpoints are target architecture but not complete.
- Poison event handling and replay/backfill process need operational design.

## Source Evidence

- [Booking Event Contracts](/business-layer/booking-event-contracts)
- [Dapr-First Standards](/production/dapr-first-production-standards)
- [Software Architecture](/technology-layer/software-architecture)
