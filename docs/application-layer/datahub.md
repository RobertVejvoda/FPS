# DataHub Application

DataHub is the proposed FairSpot application component for cross-service read models and query data. It is not a command-side service and must not own operational business state.

The working name is **DataHub**. It may be renamed before implementation if a better product-neutral name is approved.

## Purpose

FairSpot change ownership stays with dedicated business services. In this document, an owning business service means the service that accepts changes and is the source of truth for one business area:

- Booking owns booking requests, allocation, cancellation, usage, and no-show state.
- Customer owns tenant workspace and readiness state.
- Profile owns employee profile and vehicle facts.
- Configuration owns policy, location, and capacity configuration.
- Audit owns append-only business evidence.
- Notification owns notification records and preferences.

Those services publish domain events after authoritative state changes. DataHub subscribes to the event stream and maintains read-optimized projections for application queries, dashboards, reporting, exports, and cross-domain views.

```text
User action or API command -> owning business service -> service-owned state -> domain events
Domain events -> DataHub projections -> read APIs / dashboards / reports
```

## Naming

Candidate names:

| Name | Fit | Concern |
| --- | --- | --- |
| DataHub | Broad, understandable, works for cross-service read models. | Can sound like a generic data lake if not scoped. |
| Read Model Hub | Architecturally precise. | Too technical for product-facing docs. |
| Insights Hub | Product-friendly for dashboards. | Too narrow if it also serves operational lookup/read APIs. |
| Projection Service | Accurate implementation term. | Too internal and not user-facing. |
| Query Service | CQRS-friendly. | Too generic and can be confused with every service's own query handlers. |

Recommendation: use **DataHub** as the service/project name and **Read Model Store** as the database responsibility.

## Responsibilities

DataHub should:

- consume domain events from service-owned event streams;
- store tenant-scoped read models;
- expose query APIs for cross-service read needs;
- serve operational dashboards and report data;
- support deterministic exports from approved read models;
- provide projection health, lag, and rebuild status;
- process events idempotently by source event ID;
- preserve tenant isolation in every projection and query.

## Immediate Priority

The first customer-test-ready DataHub priority is Draw and booking outcome visibility.

After a Draw runs, testers must be able to see:

- HR Draw History with completed Draws, counts, and safe per-request outcomes;
- employee Past Draw Outcomes with only the authenticated employee's result;
- reporting/dashboard summaries populated from completed booking outcomes;
- projection health/freshness so empty reports are explainable.

Booking remains the write-side owner. DataHub must not trigger, retry, or correct Draws. It consumes Booking events and projects read models that make the completed Draw understandable to HR, employees, administrators, reports, and later customer service/support roles.

This makes DataHub part of test readiness, not a later analytics nicety. A completed Draw that does not appear in history/reports should be treated as an implementation gap until the DataHub projection path is working.

DataHub should not:

- accept commands that mutate Booking, Customer, Profile, Configuration, Audit, or Notification state;
- become the source of truth for operational decisions;
- publish corrective business events on behalf of owning services;
- replace Audit as the evidence source;
- expose raw events, secrets, hidden lottery internals, or unrelated employee-private data.

## Relationship To Reporting

Reporting is a business/report surface, not the durable CQRS store. Existing Reporting endpoints can stay as compatibility or presentation APIs while DataHub is introduced, but new PostgreSQL-backed projections should be implemented in DataHub.

If Reporting keeps persistence at all, it should store only report catalog/configuration metadata:

- stable report identifiers and display names;
- allowed filter definitions;
- export formats and column policies;
- role-specific visibility rules;
- report availability flags per tenant or deployment profile.

It should not store booking outcome projections, operational metrics, event inbox rows, projection checkpoints, rebuild state, or cross-service query models. Those belong to DataHub.

## Optional BI Tooling

Apache Superset is a candidate free self-hosted BI tool for analytical dashboards over DataHub PostgreSQL views. Superset should be optional and admin/analyst-facing: FairSpot web and mobile screens still provide the core employee, HR, and administrator workflows.

Superset should connect only to approved DataHub views or tables, never to private databases owned by Booking, Customer, Profile, Configuration, Audit, or Notification.

## Storage Direction

Use PostgreSQL for the DataHub Read Model Store.

Rationale:

- read models need filtering, grouping, ordering, aggregation, pagination, and exports;
- PostgreSQL indexes and relational constraints fit operational dashboards better than a key-value state-store API;
- read models can be rebuilt from events, so they are projections rather than command-side source of truth;
- one store can serve multiple safe read surfaces while preserving service ownership for writes.

Use Entity Framework Core with Npgsql for:

- schema migrations;
- typed projection tables;
- ordinary report/dashboard queries;
- tenant-scoped indexes;
- integration tests with PostgreSQL-compatible infrastructure when feasible.

Use raw SQL through EF/Npgsql only where EF would obscure intent:

- idempotent event inbox insertion;
- atomic upserts;
- projection rebuild or backfill batches;
- high-volume aggregation maintenance if needed.

## Event Streaming Plan

FairSpot should implement event streaming incrementally, using Dapr pub/sub first and keeping event contracts explicit. Kafka or another broker can be introduced later through the same Dapr pub/sub abstraction if customer deployment requires it.

### Phase 1: Event Catalog

Create a source-of-truth event catalog before building DataHub projections.

For each event, document:

- event name;
- source service;
- event version;
- source event ID;
- tenant ID;
- aggregate or entity ID;
- occurred timestamp;
- publishing command or business trigger;
- payload fields;
- privacy classification;
- consumers;
- idempotency key;
- retention expectation.

Initial event families:

- `booking.requestSubmitted`
- `booking.requestRejected`
- `booking.requestAllocated`
- `booking.requestCancelled`
- `booking.requestUsed`
- `booking.requestNoShow`
- `booking.requestExpired`
- `booking.drawStarted`
- `booking.drawCompleted`
- `booking.drawFailed` (DRAW009: published when a Draw workflow fails; carries `LifecycleSteps` and `SafeFailureReason`)
- `customer.tenantCreated`
- `customer.tenantReadinessChanged`
- `configuration.policyChanged`
- `configuration.capacityChanged`
- `profile.employeeChanged`
- `notification.deliveryChanged`
- `audit.recordCreated` for evidence references only, not raw audit payload replication.

### Event Envelope

Every DataHub-consumed event should use a stable envelope. Payload shape is event-specific, but envelope fields are common.

| Field | Requirement |
| --- | --- |
| `eventId` | Globally unique source event ID. Prefer deterministic IDs for retryable state changes, for example `{sourceService}:{aggregateId}:{version}:{eventName}` or a persisted outbox ID. |
| `eventType` | Stable event name from the catalog, such as `booking.requestSubmitted`. |
| `eventVersion` | Integer version. Start at `1`; never change meaning in place. |
| `sourceService` | Owning service that emitted the event. |
| `tenantId` | Required for tenant-scoped projections. System events without tenant scope are out of scope for first DataHub slices. |
| `aggregateId` | Owning aggregate/entity ID, such as booking request ID, tenant ID, policy ID, profile ID, notification ID, or audit record ID. |
| `occurredAt` | Timestamp from the source service when the business fact happened. |
| `publishedAt` | Timestamp when the event left the source outbox/publisher, when available. |
| `correlationId` | Request/workflow correlation ID where available. |
| `actorRef` | Optional pseudonymised actor reference or system actor. Never raw name or email. |
| `payload` | Event-specific fields listed in the catalog. Payloads must be minimal and projection-oriented. |

DataHub inbox idempotency key is `eventId`. If a legacy producer cannot provide a stable `eventId`, DataHub may derive a temporary key from `sourceService`, `eventType`, `eventVersion`, `tenantId`, `aggregateId`, and `occurredAt`, but that is transitional and must be visible in projection health.

### Initial Event Catalog

Privacy classifications:

- **Internal**: operational data without direct employee/customer personal data.
- **Confidential**: tenant, employee, booking, audit, notification, or operational data requiring tenant-scoped authorization.
- **Restricted evidence**: evidence references that may point to sensitive audit records. DataHub stores references, not raw evidence payloads.

All catalog rows use the common envelope above. Source event ID is `eventId`, tenant ID is `tenantId`, occurred timestamp is `occurredAt`, and the DataHub inbox idempotency key is `eventId` unless a future event version explicitly documents a different key.

| Event | Source | Version | Aggregate/entity ID | Trigger | Payload fields | Privacy | Consumers | Retention |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `customer.tenantCreated` | Customer | 1 | `tenantId` | Tenant workspace created or imported. | `tenantId`, `displayName`, `status`, `createdAt`, `region`, `readinessState`. | Confidential | Tenant readiness projection, admin dashboard, report availability. | Retain while tenant exists; tombstone/anonymise according to tenant offboarding policy. |
| `customer.tenantReadinessChanged` | Customer | 1 | `tenantId` | Tenant onboarding/readiness check changes. | `tenantId`, `previousState`, `newState`, `changedChecks`, `blockingChecks`, `changedAt`. | Confidential | Tenant readiness projection, production readiness dashboard. | Retain current projection plus event history required for setup evidence. |
| `configuration.policyChanged` | Configuration | 1 | `policyId` or `tenantId:locationId` | Parking policy created/updated. | `tenantId`, `locationId`, `policyId`, `effectiveFrom`, `drawCutOffTime`, `timeZone`, `requestCap`, `sameDayEnabled`, `changedFields`. | Confidential | Tenant readiness, operational reporting, Draw schedule/readiness views. | Retain latest policy projection; keep event history needed for audit/evidence. |
| `configuration.capacityChanged` | Configuration | 1 | `locationId` or `capacityVersionId` | Location/slot/capacity configuration changes. | `tenantId`, `locationId`, `capacityVersionId`, `effectiveFrom`, `slotCount`, `capabilityCounts`, `reservedCounts`, `changedFields`. | Confidential | Operational metrics, utilization, Draw status/readiness. | Retain latest capacity projection and historic summary needed for reports. |
| `profile.employeeChanged` | Profile | 1 | `profileId` or `employeeRef` | Employee profile/vehicle/eligibility facts changed. | `tenantId`, `employeeRef`, `profileStatus`, `roleRefs`, `vehicleCount`, `hasDefaultVehicle`, `eligibilityFlags`, `changedFields`. | Confidential | Tenant readiness, HR support views, operational aggregates. | Retain current projection; avoid keeping raw profile history unless required by a report. |
| `booking.requestSubmitted` | Booking | 1 | `requestId` | Employee submits a future Draw or same-day booking request. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `vehicleClass`, `capabilityFlags`, `requestSource`, `submittedAt`. | Confidential | Booking outcome, HR queue, demand, operational reporting. | Retain while request lifecycle is reportable; aggregate older data where possible. |
| `booking.requestRejected` | Booking | 1 | `requestId` | Request rejected by validation, same-day allocation, Draw, expiry, or manual correction. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `status`, `reasonCode`, `safeReasonGroup`, `source`, `decidedAt`. | Confidential | Booking outcome, HR queue, reason-code reports, employee-safe summaries. | Retain lifecycle projection and aggregated reason-code metrics. |
| `booking.requestAllocated` | Booking | 1 | `requestId` | Request receives a parking allocation. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `allocationId`, `slotCapabilityGroup`, `allocationSource`, `drawAttemptId`, `allocatedAt`. | Confidential | Booking outcome, HR queue, utilization, fairness and allocation summaries. | Retain lifecycle projection and aggregated utilization/fairness metrics. |
| `booking.requestCancelled` | Booking | 1 | `requestId` | Employee or HR cancels a pending/allocated request. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `previousStatus`, `cancelledByRole`, `reasonCode`, `cancelledAt`, `reallocationTriggered`. | Confidential | Booking outcome, HR queue, cancellation reports, notification summary. | Retain lifecycle projection and cancellation aggregates. |
| `booking.requestUsed` | Booking | 1 | `requestId` | Employee confirms usage or usage is observed. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `allocationId`, `confirmedAt`, `confirmationSource`. | Confidential | Usage/utilization reports, employee history, HR operations. | Retain lifecycle projection and aggregated usage metrics. |
| `booking.requestNoShow` | Booking | 1 | `requestId` | Scheduled/manual no-show evaluation marks request no-show. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `allocationId`, `penaltyApplied`, `reasonCode`, `evaluatedAt`. | Confidential | No-show reports, fairness context, HR support views. | Retain lifecycle projection and aggregated no-show metrics. |
| `booking.requestExpired` | Booking | 1 | `requestId` | Pending waitlist request is no longer actionable. | `tenantId`, `requestId`, `requestorRef`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `previousStatus`, `expiredAt`, `reasonCode`. | Confidential | Booking outcome, HR queue, pending/waitlist reports. | Retain lifecycle projection and expiry aggregates. |
| `booking.drawStarted` | Booking | 1 | `drawAttemptId` | Manual/scheduled Draw workflow starts for a Draw key. | `tenantId`, `drawAttemptId`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `triggerSource`, `startedAt`, `algorithmVersion`, `seedRef`. | Confidential | Draw status projection, HR operations, audit evidence links. | Retain Draw attempt projection and summary history. Do not expose raw seed except to authorized audit roles. |
| `booking.drawCompleted` | Booking | 1 | `drawAttemptId` | Draw workflow completes successfully. | `tenantId`, `drawAttemptId`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `status`, `allocatedCount`, `rejectedCount`, `waitlistedCount`, `completedAt`, `lifecycleSteps`. | Confidential | Draw status, operational reporting, HR explanations, readiness evidence. DRAW009: `lifecycleSteps` carries ordered safe lifecycle steps for the progress API. | Retain Draw attempt projection and aggregated Draw metrics. |
| `booking.drawFailed` | Booking | 1 | `drawAttemptId` | Draw workflow fails with a safe error reason. (DRAW009) | `tenantId`, `drawAttemptId`, `locationId`, `requestedDate`, `timeSlotStart`, `timeSlotEnd`, `safeFailureReason`, `completedAt`, `lifecycleSteps`. | Confidential | Draw status projection (Failed), HR failure visibility. Prevents stale Running rows. | Retain Draw attempt projection. No stack traces or internal algorithm details. |
| `notification.deliveryChanged` | Notification | 1 | `notificationId` | Notification created, sent, failed, read, or suppressed. | `tenantId`, `notificationId`, `recipientRef`, `channel`, `templateKey`, `deliveryStatus`, `changedAt`, `failureGroup`. | Confidential | Notification summary, tenant readiness, support diagnostics. | Retain summary/status projection; avoid copying message body. |
| `audit.recordCreated` | Audit | 1 | `auditRecordId` | Append-only audit evidence record created. | `tenantId`, `auditRecordId`, `entityType`, `entityId`, `action`, `actorHash`, `occurredAt`, `evidenceClass`, `traceId`. | Restricted evidence | Audit reference projection, dashboards linking to evidence. | Retain reference while audit record is retained. DataHub must not duplicate raw audit payload. |

Event IDs must remain stable across retries. Dapr pub/sub delivery is at-least-once; DataHub must treat duplicates as normal.

### First Projection And Query Ownership

| Projection | Owner | Source events | Query/read uses | Boundaries |
| --- | --- | --- | --- | --- |
| `tenant_readiness_summary` | DataHub | `customer.tenantCreated`, `customer.tenantReadinessChanged`, `configuration.policyChanged`, `configuration.capacityChanged`, `profile.employeeChanged`, `notification.deliveryChanged`, `audit.recordCreated` references. | Tenant administrator readiness workspace, hosted smoke readiness, go-live checklist. | Does not mutate Customer/Configuration/Profile. Stores readiness facts and evidence references only. |
| `booking_outcome_summary` | DataHub | `booking.requestSubmitted`, `booking.requestRejected`, `booking.requestAllocated`, `booking.requestCancelled`, `booking.requestUsed`, `booking.requestNoShow`, `booking.requestExpired`. | My Spots summaries, HR queue counts, operational demand and lifecycle summaries. | Does not replace Booking `GET /bookings` authoritative own-booking queries until explicitly migrated. No raw lottery internals. |
| `draw_status_snapshot` | DataHub | `booking.drawStarted`, `booking.drawCompleted`, `booking.drawFailed`, Booking request lifecycle events. | HR Draw status, allocation explanation summaries, next/completed Draw dashboards, DRAW009 progress API (`GET /datahub/draw-history/{id}/progress`). | Does not trigger or retry Draw. Seed visibility remains audit-role controlled. Lifecycle steps stored as JSON; available after Draw completes or fails. |
| `parking_operational_metrics` | DataHub | Booking lifecycle events plus Configuration capacity/policy events. | Reporting dashboards and exports: demand, utilization, allocation, rejection, cancellation, no-show, expiry. | Reporting may query approved DataHub views but must not own projection storage. |
| `notification_delivery_summary` | DataHub | `notification.deliveryChanged`. | Tenant readiness, support diagnostics, delivery health summaries. | Stores status/counts only, not notification message bodies. |
| `audit_reference_index` | DataHub | `audit.recordCreated`. | Links from HR/admin/report views to authorized audit evidence. | DataHub stores references and safe metadata only; Audit remains source of truth for evidence and PII mapping. |

Query ownership rules:

- employee self-service reads stay in owning services unless a DataHub-backed read API is explicitly approved and privacy-shaped;
- HR/admin dashboards may use DataHub projections for cross-service summaries;
- Reporting may expose report definitions and exports over approved DataHub views;
- Audit evidence reads remain served by Audit; DataHub only stores evidence references;
- DataHub never writes corrective events back to owning services.

### Phase 2: Outbox Per Owning Service

Each owning business service that publishes business events should use an outbox pattern.

The outbox is a small service-owned pending-event store. When a business change is saved, the service also records the event it must publish. A background publisher or retry loop then sends unsent events to Dapr pub/sub and marks them as published.

This prevents the classic failure where the service saves the business change but crashes before publishing the event. After restart, the pending outbox record is still present and can be published safely.

Dapr remains the runtime boundary for state and event transport. Prefer Dapr transactional outbox where the selected Dapr state store supports transactions and outbox behavior. In that model, the service saves state through Dapr state transactions and Dapr coordinates reliable pub/sub publication from the state-side outbox record.

When the selected Dapr component does not support transactional outbox, the service must implement an explicit pending-event/outbox state record with deterministic event IDs and retry behavior. A direct Dapr pub/sub publish is not enough for business events that must survive process crashes.

Minimum behavior:

1. Persist the authoritative state change.
2. Persist the event in a service-owned outbox or pending-publication record.
3. Publish pending outbox events to Dapr pub/sub.
4. Mark events as published after broker acknowledgement.
5. Retry unpublished events idempotently.

When the selected Dapr state-store component supports transactions, Dapr outbox, or ETag concurrency, use those features to keep the aggregate update and pending event record consistent. When it does not, the service must still make publication recoverable:

- use deterministic event IDs;
- store pending publication state beside the aggregate or in a service-owned outbox key;
- use ETags or compare-and-set where available to avoid lost updates;
- make republishing safe;
- let DataHub treat duplicate delivery as normal through inbox idempotency;
- document the failure mode and recovery path.

### Phase 3: DataHub Event Inbox

DataHub consumes events through Dapr pub/sub and records every received event in an inbox table before applying projections.

Minimum `event_inbox` fields:

- `event_id` primary key;
- `event_type`;
- `event_version`;
- `source_service`;
- `tenant_id`;
- `aggregate_id`;
- `occurred_at`;
- `received_at`;
- `processed_at`;
- `processing_status`;
- `retry_count`;
- `payload_hash`;
- `error_summary`.

Duplicate event delivery must not duplicate projection rows or counts. The inbox insert should be the idempotency gate.

### Phase 4: Projection Handlers

Projection handlers update PostgreSQL read models from inbox events.

Rules:

- one handler owns each projection table family;
- handler updates are tenant-scoped;
- handler code is deterministic;
- handler failures leave the inbox row retryable;
- poison events move to a failed state with operational visibility;
- projection code never calls private write-service databases.

Initial projections:

| Projection | Source events | Read use |
| --- | --- | --- |
| Booking outcome projection | Booking request and lifecycle events | My Spots summaries, HR queues, demand widgets. |
| Draw status projection | Draw lifecycle events plus Booking outcomes | Draw status, allocation explanation, HR operations. |
| Operational metrics projection | Booking and Configuration events | Parking summary, utilization, reason-code reports. |
| Tenant readiness projection | Customer, Identity, Configuration, Profile events | Administrator readiness workspace. |

### Phase 5: Backfill And Rebuild

DataHub projections must be rebuildable.

Options in order of preference:

1. Replay retained events from the broker/event archive.
2. Use service-owned backfill endpoints that emit canonical events or snapshots.
3. Use controlled one-time migration scripts for legacy data only.

Backfill must record:

- source;
- time range;
- tenant scope;
- event/projection counts;
- operator;
- start/end time;
- errors.

### Phase 6: Observability

Expose projection health:

- latest processed event timestamp by event type;
- lag from event occurrence to projection;
- failed inbox count;
- retry count;
- projection rebuild status;
- per-tenant projection freshness where useful.

DataHub health must not expose raw event payloads, secrets, employee-private details, or hidden Draw internals.

## Initial Projection Areas

| Projection area | Source events | Example reads |
| --- | --- | --- |
| Booking outcomes | Booking | My Spots summaries, HR queue summaries, demand, allocation, rejection, cancellation, no-show counts. |
| Draw status snapshots | Booking | Next/completed Draw status, request counts, capacity counts, allocation outcomes. |
| Operational reporting | Booking, Configuration | Daily parking summary, utilization, reason-code reports, manager-safe exports. |
| Tenant readiness summary | Customer, Identity, Configuration, Profile, Notification, Audit | Administrator readiness dashboard and setup gaps. |
| Notification summary | Notification | User notification inbox counts and delivery status summaries where appropriate. |
| Audit references | Audit | Links from dashboards to evidence IDs, not raw audit payload duplication. |

## Migration From Reporting

The current Reporting service is a legacy transitional component. It may continue serving existing report endpoints while DataHub is designed and implemented.

Target direction:

```text
Existing Reporting API -> eventually served by DataHub read models
New cross-service reads -> DataHub first
Reporting projections -> deprecated once equivalent DataHub projections exist
```

Do not build new durable PostgreSQL persistence inside Reporting. New read-model persistence should be designed under DataHub unless Robert/Codex explicitly approves an exception.

## Event Storming Needed

Before implementation, run a lightweight event-storming pass for DataHub.

Minimum output:

1. Commands per owning business service.
2. Domain events emitted by each service.
3. Read models required by each role and screen.
4. Event-to-projection mapping.
5. Missing event fields.
6. Privacy and tenant-isolation rules per read model.
7. Projection rebuild/backfill strategy.
8. Ownership boundary for every query.

Start with the customer-first flows:

- tenant setup/readiness;
- employee request and allocation;
- HR operations;
- reporting/export summaries;
- audit evidence links.

Billing remains out of scope for the first DataHub event-storming pass.

## Implementation Slices

| Slice | Purpose |
| --- | --- |
| `DATAHUB001` DataHub Architecture And Event Catalog | Finalize naming, boundaries, event catalog, projection ownership, and first read models. |
| `DATAHUB002` Project Skeleton And PostgreSQL Store | Create DataHub service/project, EF Core/Npgsql setup, migrations, health checks, and local Postgres profile. |
| `DATAHUB003` Event Inbox And Projection Runtime | Consume Dapr pub/sub events idempotently and record processing status. |
| `DATAHUB004` Booking Outcome Projections | Project Booking events into tenant-scoped operational read models. |
| `DATAHUB005` Reporting Compatibility Reads | Serve existing operational report needs from DataHub projections, then deprecate equivalent Reporting internals. |
| `DATAHUB006` Draw And Booking Outcome Projections For Test-Ready History | Immediate test-readiness slice: project completed Draws, employee-safe outcomes, HR history, and report-ready metrics from Booking events. |
