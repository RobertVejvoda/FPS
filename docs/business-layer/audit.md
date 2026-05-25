# Audit Business

[Audit module](../application-layer/audit) is designed to ensure the integrity, availability, and confidentiality of audit logs within the system. It provides mechanisms for logging, retaining, protecting, and reviewing audit logs to comply with regulatory requirements and enhance security monitoring.

### Audit Services

#### Log Management Service
- **Functions:**
    - Log Capture and Storage
    - Log Backup Management
    - Retention Management
- **Processes:**
    - Log Collection Process
    - Archive Process
    - Cleanup Process
- **Events:**
    - Log Entry Created
    - Backup Completed
    - Retention Period Exceeded

#### Security Control Service
- **Functions:**
    - Access Control Management
    - Log Protection
    - Integrity Verification
- **Processes:**
    - Access Validation Process
    - Integrity Check Process
- **Events:**
    - Unauthorized Access Attempt
    - Integrity Breach Detected

#### Monitoring Service
- **Functions:**
    - Log Analysis
    - Threat Detection
    - Review Management
- **Processes:**
    - Automated Analysis Process
    - Security Review Process
- **Events:**
    - Suspicious Activity Detected
    - Review Completed

#### Compliance Service
- **Functions:**
    - Regulatory Compliance
    - Audit Support
    - Trail Management
- **Processes:**
    - Compliance Verification Process
    - Report Generation Process
- **Events:**
    - Compliance Check Initiated
    - Audit Report Generated

#### Access Control Service
- **Functions:**
    - User Authorization
    - Access Level Management
    - Access Monitoring
- **Processes:**
    - Access Request Process
    - Authorization Process
- **Events:**
    - Access Granted
    - Access Denied
    - Authorization Changed

## Slice A001: Booking Audit Consumer

A001 is the first Audit implementation slice. It consumes Booking events and stores append-only audit records with pseudonymised actors.

A001 must:

- subscribe to the Booking event topic;
- accept the Booking event envelope defined in [Booking Event Contracts](./booking-event-contracts);
- store one audit record per unique `eventId`;
- deduplicate duplicate deliveries by `eventId`;
- record tenant, event type, event version, business timestamp, ingestion timestamp, correlation ID, causation ID, source, actor type, actor hash, entity reference, and captured payload;
- pseudonymise user references before writing audit records;
- tolerate additive Booking event payload fields;
- keep the repository interface append-only for this slice.

A001 must not:

- expose `GET /audit` or any audit query API;
- implement GDPR erasure or `DELETE /pii-mapping/{userId}`;
- persist the full `PiiMapping` store beyond a documented shape if needed;
- add audit UI, reporting dashboards, retention jobs, backup jobs, or integrity verification jobs;
- change Booking event publication or Booking state transitions;
- store raw names, emails, actor IDs, requestor IDs, or profile private data in Audit records.

Minimum audit record fields:

| Field | Meaning |
| --- | --- |
| `auditRecordId` | Internal Audit record ID. |
| `sourceEventId` | Booking event ID and idempotency key. |
| `eventType` | Booking event type. |
| `eventVersion` | Booking event schema version. |
| `occurredAt` | Timestamp from the Booking event. |
| `recordedAt` | Audit ingestion timestamp. |
| `tenantId` | Tenant that owns the event. |
| `correlationId` | Request/workflow correlation ID. |
| `causationId` | Command, workflow activity, or source event. |
| `actorType` | Actor category from the Booking event. |
| `actorHash` | SHA-256 hash of the actor ID when present. |
| `source` | Producing service, normally `booking`. |
| `entityType` | Stable category such as `bookingRequest`, `drawAttempt`, or `penalty`. |
| `entityId` | Primary entity ID when known. |
| `payload` | Captured payload with raw user identifiers removed or replaced by hashes. |

Pseudonymisation rules:

- Store `actorHash`, not raw `actorId`.
- Store requestor and affected-user hashes, not raw user IDs.
- Use a deterministic SHA-256 implementation so repeated events for the same source ID can be correlated without exposing identity.
- The `PiiMapping` shape is `{ actor_hash, user_id, name, email }`, but A001 does not persist or erase this mapping.
- On a future GDPR erasure request, the mapping row is deleted while append-only audit records remain anonymous.

Acceptance criteria:

- Given a valid Booking event, Audit stores one append-only audit record.
- Given the same event is delivered twice, Audit stores one record.
- Given a Booking event has an actor ID, the audit record contains `actorHash` and no raw actor ID.
- Given a payload has requestor or affected recipient IDs, the stored payload contains hashed references and no raw user IDs.
- Given the event has no actor ID, Audit still records the event with actor type/source and a null actor hash.
- A001 tests prove the repository abstraction has no update or delete path for audit records.

## Business Activity Timeline

The Audit service is the system of record for business activity that HR, tenant admins, auditors, and security reviewers are allowed to see. This is different from technical logs in Grafana/Loki.

Business activity records should be created from command outcomes or domain events, not by scraping application log text. The record is a structured product fact: who acted, what business action occurred, which entity was affected, what result was produced, and which technical trace can help an operator diagnose the same flow.

### Business Activity Versus Technical Logs

| Concern | Business activity record | Technical log |
| --- | --- | --- |
| Primary audience | Auditor, HR/facility manager, tenant admin, security reviewer. | Operator, developer, support engineer. |
| System of record | Audit service. | Observability backend such as Loki. |
| Retention | Audit retention policy, often years. | Operational telemetry retention, often days or weeks. |
| Identity | Pseudonymised actor, optionally resolved through approved PII mapping access. | No raw user IDs; pseudonymised values only when needed. |
| Content | Stable business action, entity, result, reason, timestamp. | Failure category, dependency, latency, status, trace/span metadata. |
| Access path | Tenant-scoped Audit API and Audit UI. | Grafana/Jaeger with operator access. |

Business-facing screens must read Audit records. They must not expose raw Loki logs as an HR/admin/auditor timeline.

### Minimum Business Activity Fields

| Field | Meaning |
| --- | --- |
| `auditRecordId` | Internal Audit record ID. |
| `sourceEventId` | Source command/event ID and idempotency key. |
| `tenantId` | Tenant that owns the activity. |
| `action` | Stable business action name, for example `booking.requestSubmitted` or `configuration.policyPublished`. |
| `entityType` | Business object category such as `bookingRequest`, `drawAttempt`, `policy`, `profile`, `notification`, or `auditMapping`. |
| `entityId` | Primary entity ID when known. |
| `actorType` | `employee`, `hr`, `admin`, `auditor`, `system`, or `integration`. |
| `actorHash` | SHA-256 hash of the actor ID when present. |
| `occurredAt` | Business timestamp from the source command/event. |
| `recordedAt` | Audit ingestion timestamp. |
| `result` | Outcome such as `accepted`, `rejected`, `allocated`, `cancelled`, `updated`, `failed`, or `suppressed`. |
| `reasonCode` | Safe reason code when one exists. |
| `correlationId` | Request/workflow correlation ID when present. |
| `traceId` | Origin OpenTelemetry trace ID when present. |
| `spanId` | Origin span ID when useful. |
| `processingTraceId` | Optional consumer-side trace ID for async processing. |
| `summary` | Short business-readable text derived from safe fields. |

The `traceId` fields are correlation metadata only. They help an operator find matching technical logs/traces when there is an approved support or incident reason. They must not replace actor, tenant, action, entity, result, reason, or idempotency fields.

### Actor Resolution

`actorHash` is one-way. It cannot be mathematically reversed to the original actor ID.

Actor identity can be resolved only through the separate PII mapping store:

| Mapping field | Purpose |
| --- | --- |
| `actor_hash` | Hash stored in audit records. |
| `user_id` | Original FairSpot/IdP subject or local account ID. |
| `display_name` | Optional display value for approved audit views. |
| `email` | Optional contact value when the client policy allows it. |
| `tenant_id` | Tenant boundary for the mapping. |
| `created_at` / `updated_at` | Mapping lifecycle evidence. |
| `erased_at` | Present when GDPR erasure has anonymised the actor. |

Rules:

- Normal audit query responses should show `actorHash`, `actorType`, and a safe display label such as "employee actor" or "system actor".
- Resolving `actorHash` to a person requires a separate permission path, reason, and audit record of the lookup.
- GDPR erasure deletes or anonymises the PII mapping row. Historical audit records remain immutable but no longer resolve to a person.
- The Audit service must still support correlation by `actorHash` after erasure without exposing identity.

### Business Activity Examples

| Action | Actor | Entity | Result | Notes |
| --- | --- | --- | --- | --- |
| `booking.requestSubmitted` | Employee | Booking request | `accepted` | Stores request ID, date, time slot, safe vehicle requirement flags, and trace ID. |
| `booking.requestRejected` | Employee or system | Booking request | `rejected` | Stores safe rejection code, not hidden allocation diagnostics. |
| `booking.slotAllocated` | System | Booking request | `allocated` | Stores slot/allocation reference and draw attempt ID where safe. |
| `booking.requestCancelled` | Employee, HR, or system | Booking request | `cancelled` | Manual or HR cancellation requires a reason. |
| `configuration.policyPublished` | Admin | Policy version | `updated` | Stores policy version and safe scope, not the full policy blob unless approved. |
| `profile.vehicleUpdated` | Employee, HR, or admin | Vehicle/profile | `updated` | Must not store raw license plate in audit summary. |
| `audit.piiMappingErased` | Admin, auditor, or privacy contact | PII mapping | `erased` | Stores target actor hash and reason, not the erased identity. |
| `privacy.erasureRequested` | Admin, auditor, or privacy contact | Erasure request | `accepted` | Starts the Dapr Workflow and stores target actor hash, not raw identity. |
| `privacy.erasureStepRecorded` | System | Erasure service step | service treatment | Records each service-owned erasure result without raw identity. |
| `privacy.erasureCompleted` | System | Erasure request | `completed` | Stores per-service outcome summary and trace ID when present. |

### Product Usage

Audit APIs and UI should support:

- tenant-scoped filtering by date range, actor hash, actor type, action, entity type, result, reason code, and trace ID;
- role-specific views for auditor, HR/facility manager, and tenant admin;
- safe CSV/JSON export for auditors;
- optional "resolve actor" action for authorized users with reason capture;
- optional "copy trace ID" action for support escalation, without embedding raw technical logs in the business UI.

## Erasure Workflow Audit

Employee data erasure is implemented as a Dapr Workflow coordinated by a privacy/GDPR API. The workflow calls service-owned activities for Profile, Booking, Notification, Reporting, and Audit. Each service decides whether matching records are deleted, anonymised, pseudonymised, retained, blocked, or failed according to its own invariants and retention rules. The production gap is durable-store completion for Profile erasure and Booking active-check/anonymisation.

Audit records for the workflow must show:

- request creation and requester actor hash;
- target actor hash;
- reason/legal basis category;
- blocking dependency checks;
- each service step result;
- completion, partial completion, rejection, or failure;
- optional `traceId` / `processingTraceId` for support correlation.

The erasure workflow must not store the erased user's raw identity in the business activity summary. After the Audit PII mapping is deleted, historical audit records remain queryable by actor hash but no longer resolve to a person.

## AUD007: Draw Lifecycle Audit Evidence

AUD007 provides comprehensive Draw lifecycle tracking for auditors and authorized administrators to verify FairSpot's fairness claims.

### Scope

- Capture step-level lifecycle events during Draw execution
- Expose auditor/admin API to query complete Draw lifecycle
- Include per-booking decisions with outcomes and business reasons
- Preserve deterministic evidence: algorithm version, seed, ordered candidate sequence
- Link lifecycle steps with correlation metadata for tracing
- Support both completed and failed/partial Draw lifecycle paths
- Keep employee-facing views separate from audit views

### Lifecycle Steps

Each Draw captures these business-readable action steps:

| Step | Description | Evidence captured |
| --- | --- | --- |
| `PolicyResolved` | Retrieved and validated tenant allocation policy | Policy lookup confirmation |
| `RequestsLoaded` | Loaded pending booking requests for the time slot | Request count |
| `CapacityLoaded` | Loaded available parking slots | Slot count |
| `MetricsLoaded` | Loaded fairness metrics for requestors | Unique requestor count |
| `WeightedAllocationCompleted` | Ran weighted lottery allocation | Algorithm version |
| `DecisionsPersisted` | Persisted decisions and updated booking statuses | Decision count |
| `EventsPublished` | Published draw completion and decision events | Event publication confirmation |

Each step records:

- `stepName`: Business-readable step name
- `status`: `Pending`, `InProgress`, `Completed`, `Skipped`, or `Failed`
- `startedAt`: Step start timestamp
- `completedAt`: Step completion timestamp
- `reasonCode`: Safe reason code when step fails or is skipped
- `summary`: Business-readable text summary
- `correlationId`: Draw correlation ID
- `traceId`: OpenTelemetry trace ID where available

### Draw Lifecycle Model

The complete Draw lifecycle includes:

| Field | Meaning | Audience |
| --- | --- | --- |
| `drawKey` | Business-safe draw reference | Auditor, admin |
| `tenantId`, `locationId`, `date` | Draw scope | Auditor, admin |
| `status` | `Pending`, `InProgress`, `Completed`, `Failed`, `Partial` | Auditor, admin |
| `seed` | Deterministic seed for reproducibility | Auditor only |
| `algorithmVersion` | Algorithm version used | Auditor, admin |
| `startedAt`, `completedAt` | Lifecycle timestamps | Auditor, admin |
| `steps` | Step-level lifecycle events | Auditor, admin |
| `allocatedCount`, `rejectedCount`, `waitlistedCount` | Summary counts | Auditor, admin |
| `decisions` | Per-booking decisions with outcomes and reasons | Auditor, admin |
| `tier2CandidateSequence` | Ordered candidate sequence for fairness verification | Auditor only |
| `correlationId`, `traceId` | Correlation metadata for trace linking | Auditor, admin |

### API

**Endpoint**: `GET /draws/{date}/lifecycle`

**Authorization**: `admin` or `auditor` roles only

**Query Parameters**:
- `locationId`: Location identifier
- `timeSlotStart`: Time slot start (ISO 8601 datetime)
- `timeSlotEnd`: Time slot end (ISO 8601 datetime)

**Response**: Full Draw lifecycle with steps, decisions, and deterministic evidence

**Employee Protection**: Employees see only plain outcomes and safe reasons through separate endpoints. They do not see draw seed, candidate order, raw IDs, or hidden fairness internals.

### Acceptance Criteria

- [x] Auditor/admin can inspect one Draw and see lifecycle status from start to completion/failure
- [x] Lifecycle includes key action steps and each step's result
- [x] Per-booking decisions are linked to the Draw lifecycle and show outcome plus business reason
- [x] Audit records, traces, and logs can be correlated by draw attempt/reference, correlationId, and traceId
- [x] Failed or partial Draw runs are visible (not only successful completions)
- [x] Employee-facing views continue to hide draw internals
- [x] Tests cover a completed Draw lifecycle and step tracking

### Implementation

- **Files**:
  - `FPS.Booking.Application/Models/DrawLifecycleModels.cs`: Lifecycle DTOs
  - `FPS.Booking.Application/Queries/GetDrawLifecycleQuery.cs`: Lifecycle query
  - `FPS.Booking.Application/Queries/GetDrawLifecycleHandler.cs`: Lifecycle handler
  - `FPS.Booking.Application/Commands/TriggerDrawHandler.cs`: Step capture during draw execution
  - `FPS.Booking.API/Controllers/DrawsController.cs`: Lifecycle endpoint
  - Tests: `TriggerDrawHandlerTests.cs`, `GetDrawLifecycleHandlerTests.cs`

- **Deployment**: AUD007 is fully backward compatible. Existing draw attempts have no lifecycle steps; new draw attempts capture all steps.

- **Validation**: All 122 Booking application tests pass, including 9 new lifecycle tests covering step tracking, correlation metadata, and decision linking.
