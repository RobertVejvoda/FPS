# Data Privacy

Ensures that sensitive data is handled in compliance with privacy regulations and best practices, such as data minimization and anonymization.
## Purpose

Data privacy defines how FairSpot limits, protects, retains, and exposes personal and tenant data. It complements the [Security Model](./security-model) by translating the data classification and GDPR alignment into implementation and operational rules.

## Personal and Tenant Data

FairSpot commonly processes these Confidential data categories:

- employee profile identifiers, vehicle facts, accessibility or company-car eligibility, and parking preferences;
- booking requests, allocation outcomes, cancellations, no-show signals, penalties, and employee-visible reasons;
- notifications and delivery metadata;
- tenant locations, parking spaces, policy configuration, role assignments, and operational reports;
- audit records, support cases, and security investigation evidence;
- billing contacts, invoices, subscriptions, and usage records where billing is enabled.

Secret data, such as credentials, signing keys, tokens, certificates, connection strings, and recovery material, is not normal personal data access. It is governed by secret-management controls and separate access tracking.

## Privacy Rules

- Collect only data required for parking operations, auditability, notification, reporting, billing, or legally required administration.
- Resolve tenant and user context from authenticated claims or trusted service context, never from caller-supplied identity fields.
- Default employee views to own data only.
- Do not expose hidden lottery weights, seeds, internal diagnostic details, unrelated employee data, or raw audit internals to employees.
- Pseudonymise audit actors where possible so immutable evidence can remain useful after PII mappings are removed.
- Keep events minimal: no secrets, stack traces, raw names, emails, license plates, or unrelated employee details unless a documented consumer requires them.
- Define retention before production for bookings, notifications, audit records, reports, logs, backups, support cases, and PII mappings.

## GDPR Alignment

FairSpot supports GDPR-aligned operation through tenant scoping, least privilege, data minimisation, audit evidence, encryption in transit and at rest, and rights-request slices for access, rectification, erasure, and restriction. Product documentation does not certify GDPR compliance by itself; production use still requires controller/processor roles, privacy notices, subprocessors, data-processing agreements, retention schedules, and legal review.

## Rights Requests

Access, rectification, and erasure workflows must identify all service-owned data for the affected tenant/user. Immutable audit evidence should preserve accountability while removing or disconnecting direct PII mappings where legally allowed.

### Employee Data Erasure

Employee data deletion must be a governed erasure workflow, not an immediate blind delete across databases. The workflow must classify each record as one of:

| Treatment | Meaning | Examples |
| --- | --- | --- |
| Delete | Remove the record because no active operational or legal purpose remains. | Expired notifications, obsolete profile facts, local support notes after retention. |
| Anonymise | Remove direct identity while preserving aggregate or accountability value. | Reporting projections, historical fairness evidence. |
| Pseudonymise | Keep the immutable business record but disconnect it from direct identity. | Audit records after deleting the PII mapping. |
| Retain | Keep the record because another legal/operational retention basis applies. | Active booking, unresolved dispute, required audit/legal evidence, incident record. |

Preferred implementation shape: a Dapr Workflow coordinates the erasure request. The workflow owns orchestration, retry, status, and timeout behavior; each service owns its own data treatment; the Audit service records the business activity evidence.

Typical erasure workflow:

1. Create an erasure request with target tenant, target user, requester, reason/legal basis, and requested scope.
2. Resolve the target user's `actorHash` and affected service-owned records.
3. Block or stage the request if the user has active bookings, open support/security cases, or another dependency that must be resolved first.
4. Delete or anonymise Profile facts, saved vehicles, local account fallback data, notification records, and eligible support metadata.
5. Delete or anonymise Booking data according to status and retention policy. Active bookings should be cancelled or completed before erasure unless legal policy says otherwise.
6. Delete or anonymise Reporting projections where they contain user-level data; aggregate reports may remain if no person can be re-identified.
7. Delete the Audit PII mapping so historical audit records keep `actorHash` but no longer resolve to a person.
8. Record an append-only audit event for the erasure request and each service result.
9. Return a completion summary showing deleted, anonymised, retained, skipped, and failed categories without exposing unnecessary PII.

Workflow activities should be idempotent. A retry must not recreate deleted records, duplicate audit records, or report inconsistent completion status. Use `erasureRequestId` plus service/data-class keys as idempotency inputs.

Catch points:

- Deleting a user in the customer IdP does not automatically erase FairSpot data. FairSpot needs a rights-request workflow or integration event.
- Audit records should not be physically rewritten to remove `actorHash`; the privacy boundary is the separate PII mapping.
- Backups cannot usually be rewritten safely. The retention policy must define backup expiry and restore-time re-erasure controls.
- If the user has an active allocation or pending booking, the product needs a business decision: cancel first, transfer responsibility, or block erasure until final status.
- Tenant admins should not be able to use erasure to hide their own privileged actions. Pseudonymised audit evidence remains.

Erasure tracking fields:

| Field | Purpose |
| --- | --- |
| `erasureRequestId` | Stable request ID used across services. |
| `tenantId` | Tenant boundary. |
| `targetActorHash` | Target user pseudonym. |
| `requestedByActorHash` | Requester pseudonym, if different. |
| `requestedAt` / `completedAt` | Workflow timestamps. |
| `reason` / `legalBasis` | Client-provided justification. |
| `scope` | Services/data classes included. |
| `serviceResults` | Deleted/anonymised/retained/failed counts by service. |
| `traceId` | Optional technical trace for support investigation. |

The erasure request itself is a business activity and must be audited. The audit event should keep the erasure request ID, target actor hash, requester actor hash, result, reason category, and service outcome summary. It must not store the erased user's name, email, license plate, or raw user ID.

### Dapr Workflow Responsibilities

| Responsibility | Owner |
| --- | --- |
| Create erasure request and workflow instance | Privacy/GDPR API. |
| Check active bookings and blocking dependencies | Booking service activity. |
| Delete or anonymise profile and vehicle facts | Profile service activity. |
| Delete or anonymise booking data according to retention policy | Booking service activity. |
| Delete notifications and delivery metadata | Notification service activity. |
| Anonymise user-level reporting projections where needed | Reporting service activity. |
| Delete Audit PII mapping and preserve pseudonymised audit records | Audit service activity. |
| Record request, step result, completion, rejection, and failure events | Audit service. |
| Expose workflow status to authorized privacy/admin users | Privacy/GDPR API. |

The workflow should not directly edit another service's database. It should call service-owned APIs or activities so each bounded context enforces its own invariants and retention rules.
