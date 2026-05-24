# Security Audit

Security audit defines how FairSpot preserves accountability for sensitive actions without exposing unnecessary personal or technical data.

## Audit Evidence Types

| Evidence type | Source of truth | Purpose |
| --- | --- | --- |
| Business activity records | Audit service. | Tenant-scoped evidence for booking, allocation, configuration, profile, notification, erasure, export, and privileged actions. |
| Security access records | Audit service or client security system, depending on deployment. | Evidence for privileged reads, PII mapping resolution, export/download, break-glass, and secret access. |
| Technical logs and traces | Observability backend. | Operational diagnosis and incident investigation. |

Business and security audit records are product evidence. Technical logs and traces are support evidence. They can be linked by `traceId`, `sourceEventId`, or `correlationId`, but they have different audiences, retention periods, and access rules.

## Actor Pseudonymisation

Audit records store `actorHash`, not raw actor ID, name, or email. The hash is deterministic so the same actor can be correlated across records without revealing identity.

Identity resolution requires the separate PII mapping store:

- normal audit queries return `actorHash` and safe actor type;
- resolving a hash to a person requires a dedicated permission path and reason;
- each resolution is itself audited;
- GDPR erasure deletes or anonymises the mapping row, leaving historical records immutable but anonymous.

## Trace Correlation

Audit records may store optional OpenTelemetry correlation metadata:

- `traceId` from the originating request or command;
- `spanId` where useful;
- `processingTraceId` when an async consumer processes the event in a separate trace;
- `sourceEventId` or command ID for idempotency and cross-service correlation.

These values are support links only. Audit records must remain useful when no trace exists and must still contain tenant, actor hash, action, entity, result, reason, and timestamp fields.

## Sensitive Audit Actions

The following actions require explicit audit records:

- booking submission, rejection, allocation, cancellation, no-show, usage confirmation, and manual correction;
- policy, slot, location, tenant, role, user, profile, and vehicle changes;
- HR/import preview and commit actions;
- report export, audit export, and sensitive read/download actions;
- PII mapping lookup, erasure request creation, erasure completion, and erasure failure;
- secret access, rotation, revocation, break-glass access, and incident response actions where FairSpot owns the control;
- integration credential changes and customer-system import failures where they affect tenant data.

Audit summaries must use safe business language and reason codes. They must not include secrets, raw user IDs, names, emails, license plates, complete request payloads, stack traces, or hidden allocation internals.

## Erasure Accountability

An erasure request must leave evidence that the request happened and what treatment was applied, while avoiding retention of the erased identity.

The preferred implementation is a Dapr Workflow that coordinates service-owned erasure activities. Audit does not orchestrate deletion, but it records the durable business evidence for the workflow and each material step.

Minimum erasure audit fields:

| Field | Purpose |
| --- | --- |
| `erasureRequestId` | Stable erasure workflow ID. |
| `targetActorHash` | Pseudonymised target user. |
| `requestedByActorHash` | Pseudonymised requester or operator. |
| `action` | `privacy.erasureRequested`, `privacy.erasureCompleted`, `privacy.erasurePartiallyCompleted`, or `privacy.erasureRejected`. |
| `result` | Outcome classification. |
| `reasonCode` | Safe reason or legal basis category. |
| `serviceResults` | Summary by service, without raw PII. |
| `traceId` | Optional technical correlation ID. |

If the PII mapping is deleted, future readers can still prove that a pseudonymised actor was erased, but they cannot resolve that actor to a person without an external retained legal record.

Recommended erasure audit actions:

| Action | Emitted when |
| --- | --- |
| `privacy.erasureRequested` | A user/admin/privacy contact creates the request. |
| `privacy.erasureBlocked` | Active bookings, legal hold, open incident, or another dependency prevents completion. |
| `privacy.erasureServiceStepCompleted` | A service finishes its delete/anonymise/pseudonymise/retain activity. |
| `privacy.erasureServiceStepFailed` | A service activity fails after retry policy or needs manual intervention. |
| `privacy.erasureCompleted` | All required services complete successfully. |
| `privacy.erasurePartiallyCompleted` | Some data was retained or a non-critical service could not complete under policy. |
| `privacy.erasureRejected` | The request is rejected with a safe reason code. |
