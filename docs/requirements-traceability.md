# Requirements Traceability

This page maps FPS requirements to implementation slices, PR evidence, and remaining gaps. It complements the [Implementation Tracker](./implementation-tracker): the tracker follows delivery ownership; this page follows requirement coverage.

## How To Use This Matrix

- Business requirements use IDs from [Business Requirements](./business-layer/requirements), such as `BR001`.
- Non-functional requirements use IDs from [Non-functional Requirements](./technology-layer/non-functional-requirements), such as `NFR1100`.
- Security and privacy requirements may also reference [Security](./security) when the NFR is a cross-cutting control rather than one specific feature.
- A requirement can map to multiple slices. Mark it `Partial` until all required slices and evidence are merged.
- A slice can map to multiple requirements. Every new implementation slice should name the business and NFR IDs it implements or supports.

## Status Values

| Status | Meaning |
| --- | --- |
| Planned | Requirement is accepted but no implementation evidence has merged. |
| Partial | At least one supporting slice has merged, but the requirement is not complete. |
| Done | Requirement has merged implementation evidence and validation evidence. |
| Deferred | Requirement is intentionally outside the current delivery phase. |

## Business Requirement Coverage

| Requirement | Source | Implemented by slice | Status | Evidence | Notes |
| --- | --- | --- | --- | --- | --- |
| `BR001` Automated Parking Requests | [Business Requirements](./business-layer/requirements) | `B001`, `B002`, `BK011`, `MOB004`, future `WEB001` | Partial | PR #9, #15, #27, #33, #87 | Backend and mobile request submission are implemented; web employee self-service remains planned. |
| `BR002` Fair Slot Allocation | [Business Requirements](./business-layer/requirements), [Allocation Rules](./business-layer/allocation-rules) | `B004`, `B005`, `B007`, `B010`, `REPORT001` | Partial | PR #13, #14, #17, #19, #124 | Core allocation and reporting read-model evidence exist; dashboards and manager-facing reporting remain planned. |
| `BR003` Configurable Parking Policies | [Business Requirements](./business-layer/requirements), [Parking Policy Configuration](./business-layer/parking-policy-configuration) | `CFG001`, `CFG002`, `CFG003` | Partial | PR #42, #125 | Policy shape and admin/HR policy/slot APIs exist; publication, version history, and audit integration remain planned. |
| `BR004` Real-Time Status and Notifications | [Business Requirements](./business-layer/requirements), [Notification](./business-layer/notification) | `N001`, `N002`, `N003`, `N004`, `MOB006`, future `WEB001` | Partial | PR #35, #36, #93, #94, #111, #123 | In-app records/API/SSE, email delivery, and email failure observability exist; mobile/web consumption and preferences remain. |
| `BR005` Cancellation and Reallocation | [Business Requirements](./business-layer/requirements), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle) | `B003`, `B005`, `MOB005`, future `WEB001` | Partial | PR #10, #14, #95 | Backend and mobile cancellation paths exist; web surface remains planned. |
| `BR006` Usage Confirmation | [Business Requirements](./business-layer/requirements) | `B006`, `MOB005`, future `WEB001` | Partial | PR #16, #95 | User confirmation exists in backend/mobile; physical integrations are future scope. |
| `BR007` Penalties and Adjustments | [Business Requirements](./business-layer/requirements), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle) | `B005`, `B007`, `B010`, future `WEB002` | Partial | PR #14, #17, #19 | Core penalty/manual correction behavior exists; admin UI remains planned. |
| `BR008` Reporting and Analytics | [Business Requirements](./business-layer/requirements) | `REPORT001`, `REPORT002`, `WEB004` | Partial | PR #124 | Tenant-scoped summary and fairness read models exist; dashboards, exports, and web views are later. |
| `BR009` Role-Based Access | [Business Requirements](./business-layer/requirements), [Booking Authorization](./business-layer/booking-authorization), [Security Model](./security/security-model) | `ID001`, `BK011`, `CFG002`, `A002`, `REPORT001`, future `WEB002`/`WEB003` | Partial | PR #26, #27, #33, #112, #124, #125 | Auth context, Booking scoping, auditor/admin APIs, reporting roles, and configuration admin/HR APIs exist; UI role surfaces remain. |
| `BR010` Auditability and Compliance | [Business Requirements](./business-layer/requirements), [Audit](./business-layer/audit), [Security](./security) | `A001`, `A002`, `A003`, `CFG003` | Partial | PR #37, #39, #112 | Append-only audit records, auditor query, and GDPR PII mapping erasure exist; retention, integrity, export evidence, and configuration audit remain. |
| `BR011` Multi-Tenant Customer Model | [Business Requirements](./business-layer/requirements), [Versions and Decisions](./versions-and-decisions) | `BK011`, `CFG001`, `OPS001`, `OPS002`, `CUST001` | Partial | PR #27, #33, #42 | Tenant-scoped implementation exists in core paths; provisioning and hosted tenant lifecycle remain. |

## Non-Functional Requirement Coverage

| Requirement | Source | Implemented by slice | Status | Evidence | Notes |
| --- | --- | --- | --- | --- | --- |
| `NFR1100` Service Isolation | [Non-functional Requirements](./technology-layer/non-functional-requirements) | Foundation, `N001`, `A001`, `CFG001`, `CFG002`, `REPORT001` | Partial | PR #1-#5, #35-#39, #42, #124, #125 | Services are separated in code; deployment independence remains part of OPS work. |
| `NFR1101` Service Communication | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `N001`, `A001`, `API001`, `OPS001` | Partial | PR #35-#39, #43, #44 | HTTP/OpenAPI and event consumers exist; production Dapr component hardening remains. |
| `NFR1300` Message Broker Implementation | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `N001`, `A001`, `N003`, `REPORT001`, `OPS001`, `OPS002` | Partial | PR #35-#39, #111, #124 | Event consumer pattern exists; hosted Dapr pub/sub baseline remains. |
| `NFR1400` Deployment Portability | [Non-functional Requirements](./technology-layer/non-functional-requirements), [Production](./production) | `OPS000`, `OPS001`, `OPS002`, `OPS003` | Partial | PR #102 | Deployment profile baseline exists; pluggable local/demo/client-owned production profiles remain. |
| Security tenant/user context from auth only | [Security Model](./security/security-model), [Booking Authorization](./business-layer/booking-authorization) | `ID001`, `BK011`, `MOB003` | Done | PR #26, #27, #33, #78 | Core employee/Booking path no longer trusts caller-supplied tenant/user identity. |
| Confidential and Secret data protection | [Security Model](./security/security-model), [Data Privacy](./security/data-privacy), [Encryption](./security/encryption) | `A001`, `A002`, `OPS001`, `OPS002` | Partial | PR #37, #39, #112 | Audit pseudonymisation and GDPR erasure path exist; secret store and hosted controls remain production work. |
| Audit and traceability | [Security Model](./security/security-model), [Traceability](./security/traceability) | `A001`, `A002`, `A003`, `CFG003`, docs traceability cleanup | Partial | PR #37, #39, #112 | Business audit exists for Booking events; query and erasure exist; retention, integrity, policy-change audit, and full requirement evidence remain. |
| Availability and operations | [Production](./production), [Security](./security) | `OPS000`, `OPS001`, `OPS002`, `OPS003`, `OPS004` | Partial | PR #102 | Deployment profile baseline exists; pluggable deployment, observability, backups, restore, and incident runbooks remain future slices. |
| Demo and client evaluation readiness | [Demo and Evaluation](./demo-and-evaluation) | `DOCS001`, `OPS002`, mobile/web/admin slices | Planned | - | Role-based demo path and client materials are planned; demo data, reset, scripts, and shareable materials remain. |
| Frontend accessibility and usability | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `MOB001`-`MOB009`, `WEB001`-`WEB004` | Partial | PR #48, #51, #55, #76, #78, #87, #95 | Mobile core flow exists; mobile polish and web app remain planned. |

## Slice Evidence Rules

When creating or approving a new implementation slice, update either this page or the slice issue with:

- business requirement IDs covered by the slice;
- NFR/security/privacy/operability IDs covered by the slice;
- explicit `Partial` vs `Done` expectations;
- issue and PR links;
- validation evidence, such as tests, typecheck, `./tools/validate.sh`, screenshots, deployment checks, or runbook verification;
- known gaps left to later slices.

If a PR implements user-visible behavior that maps to no requirement, add or update the requirement before merging. If a requirement reaches `Done`, the evidence column must link to the merged PR or deployed artifact that proves it.
