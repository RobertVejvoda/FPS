# Requirements Traceability

This page maps FairSpot requirements to implementation slices, PR evidence, and remaining gaps. It complements the [Implementation Tracker](./implementation-tracker): the tracker follows delivery ownership; this page follows requirement coverage.

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
| `BR001` Automated Parking Requests | [Business Requirements](./business-layer/requirements) | `B001`, `B002`, `BK011`, `MOB004`, `WEB001` | Done | PR #9, #15, #27, #33, #87, #174 | Backend, mobile, and web request submission are implemented for the current employee self-service scope. |
| `BR002` Fair Slot Allocation | [Business Requirements](./business-layer/requirements), [Allocation Rules](./business-layer/allocation-rules) | `B004`, `B005`, `B007`, `B010`, `REPORT001`, `REPORT003`, `WEB006` | Done | PR #13, #14, #17, #19, #124, #199, #210 | Core allocation, reporting read-model evidence, dashboard views, utilization/reason/outcome reports, and export hardening exist for the current scope. |
| `BR003` Configurable Parking Policies | [Business Requirements](./business-layer/requirements), [Parking Policy Configuration](./business-layer/parking-policy-configuration) | `CFG001`, `CFG002`, `CFG003`, `WEB007` | Done | PR #42, #125, #178, #199 | Policy shape, admin/HR policy/slot APIs, publication/version history, audit integration, and web configuration surfaces exist. |
| `BR004` Real-Time Status and Notifications | [Business Requirements](./business-layer/requirements), [Notification](./business-layer/notification) | `N001`, `N002`, `N003`, `N004`, `N005`, `MOB006`, `WEB001`, `WEB005` | Done | PR #35, #36, #93, #94, #111, #123, #166, #174, #177, #199 | In-app records/API/SSE, email delivery, email failure observability, preferences, mobile consumption, and web notification surfaces exist. |
| `BR005` Cancellation and Reallocation | [Business Requirements](./business-layer/requirements), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle) | `B003`, `B005`, `MOB005`, `WEB001` | Done | PR #10, #14, #95, #174 | Backend, mobile, and web cancellation paths exist for the current self-service scope. |
| `BR006` Usage Confirmation | [Business Requirements](./business-layer/requirements) | `B006`, `MOB005`, `WEB001` | Done | PR #16, #95, #174 | User confirmation exists in backend, mobile, and web; physical access-control integrations remain outside current scope. |
| `BR007` Penalties and Adjustments | [Business Requirements](./business-layer/requirements), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle) | `B005`, `B007`, `B010`, `WEB002`, `WEB008` | Partial | PR #14, #17, #19, #199 | Core penalty/manual correction behavior and supporting admin/audit views exist; a dedicated penalty-adjustment UI remains a later product decision. |
| `BR008` Reporting and Analytics | [Business Requirements](./business-layer/requirements), [Reporting](./business-layer/reporting) | `REPORT001`, `REPORT002`, `REPORT003`, `WEB006` | Done | PR #124, #199, #210 | Tenant-scoped summary/fairness read models, web reporting dashboard, summary CSV export, fixed reports, and export hardening exist. |
| `BR009` Role-Based Access | [Business Requirements](./business-layer/requirements), [Booking Authorization](./business-layer/booking-authorization), [Security Model](./security/security-model) | `ID001`, `ID002`, `BK011`, `CFG002`, `A002`, `REPORT001`, `WEB002`, `WEB003`, `WEB009` | Partial | PR #26, #27, #33, #112, #124, #125, #189, #199 | Auth context, Booking scoping, provisioning integration, auditor/admin APIs, reporting roles, configuration admin/HR APIs, and first web role surfaces exist; web real login and broader tenant-admin role management remain. |
| `BR010` Auditability and Compliance | [Business Requirements](./business-layer/requirements), [Audit](./business-layer/audit), [Security](./security) | `A001`, `A002`, `A003`, `A004`, `A005`, `WEB008`, `CFG003` | Done | PR #37, #39, #112, #178, #198, #199 | Append-only audit records, auditor query, GDPR PII mapping erasure, retention, integrity verification, export evidence, web audit console, and configuration-change audit exist. |
| `BR011` Multi-Tenant Customer Model | [Business Requirements](./business-layer/requirements), [Tenant Onboarding](./business-layer/tenant-onboarding), [Versions and Decisions](./versions-and-decisions) | `BK011`, `CFG001`, `OPS001`, `OPS002`, `CUST001`, `CUST003`-`CUST007` | Partial | PR #27, #33, #42, #167, #200, #209, #211, #212, #213, #216 | Tenant-scoped implementation, onboarding contract, workspace lifecycle, identity setup, parking bootstrap, employee/profile bootstrap, and readiness checks exist or are in review; `WEB003` remains for tenant-admin operation. |

## Non-Functional Requirement Coverage

| Requirement | Source | Implemented by slice | Status | Evidence | Notes |
| --- | --- | --- | --- | --- | --- |
| `NFR1100` Service Isolation | [Non-functional Requirements](./technology-layer/non-functional-requirements) | Foundation, `N001`, `A001`, `CFG001`, `CFG002`, `REPORT001` | Partial | PR #1-#5, #35-#39, #42, #124, #125 | Services are separated in code; deployment independence remains part of OPS work. |
| `NFR1101` Service Communication | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `N001`, `A001`, `API001`, `OPS001` | Partial | PR #35-#39, #43, #44 | HTTP/OpenAPI and event consumers exist; production Dapr component hardening remains. |
| `NFR1300` Message Broker Implementation | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `N001`, `A001`, `N003`, `REPORT001`, `OPS001`, `OPS002` | Partial | PR #35-#39, #111, #124 | Event consumer pattern exists; hosted Dapr pub/sub baseline remains. |
| `NFR1400` Deployment Portability | [Non-functional Requirements](./technology-layer/non-functional-requirements), [Production](./production) | `OPS000`, `OPS001`, `OPS002`, `OPS003`, `OPS006` | Partial | PR #102, #167, #188, #191 | Deployment profile baseline, pluggable local/demo/client-owned profiles, and local harness evidence exist; hosted production proof remains future evaluation work. |
| Security tenant/user context from auth only | [Security Model](./security/security-model), [Booking Authorization](./business-layer/booking-authorization) | `ID001`, `BK011`, `MOB003` | Done | PR #26, #27, #33, #78 | Core employee/Booking path no longer trusts caller-supplied tenant/user identity. |
| Confidential and Secret data protection | [Security Model](./security/security-model), [Data Privacy](./security/data-privacy), [Encryption](./security/encryption) | `A001`, `A002`, `P002`, `ID002`, `OPS001`, `OPS002`, `OPS005` | Partial | PR #37, #39, #112, #167, #189, #190, #191 | Audit pseudonymisation, GDPR erasure, SSO/profile mapping, and integration secret handling exist; production environment controls remain deployment-specific. |
| Audit and traceability | [Security Model](./security/security-model), [Traceability](./security/traceability) | `A001`, `A002`, `A003`, `A004`, `A005`, `CFG003`, docs traceability cleanup | Done | PR #37, #39, #112, #178, #198 | Business audit, query/erasure, retention, integrity verification, export evidence, policy-change audit, and requirement evidence exist for the current scope. |
| Availability and operations | [Production](./production), [Security](./security) | `OPS000`, `OPS001`, `OPS002`, `OPS003`, `OPS004`, `OPS006` | Partial | PR #102, #167, #188, #191 | Deployment profiles, local harness, observability/performance evidence, backups, restore, and runbooks exist; live client environment proof remains future work. |
| Demo and client evaluation readiness | [Demo and Evaluation](./demo-and-evaluation), [Client Evaluation Pack](./client-evaluation-pack), [Demo Environment Baseline](./production/demo-environment-baseline) | `DOCS001`, `OPS002`, `OPS006`, mobile/web/admin/customer slices | Partial | `OPS002`, `DOCS001`, PR #166, #171, #174, #176, #188, #199, #209-#213, #216 | Demo environment baseline, client evaluation pack, mobile polish, web/admin surfaces, local harness, onboarding slices, and readiness checks exist or are in review; final pilot story and real-device test evidence remain. |
| Frontend accessibility and usability | [Non-functional Requirements](./technology-layer/non-functional-requirements) | `MOB001`-`MOB009`, `WEB001`-`WEB004` | Partial | PR #48, #51, #55, #76, #78, #87, #95, #166, #171, #174, #176, #199, `MOB007` | Mobile core flow, notifications, draw visibility, profile/vehicle details, production polish, employee web, reporting, configuration, and audit web surfaces exist; broader tenant admin UX remains in `WEB003`. |

## Slice Evidence Rules

When creating or approving a new implementation slice, update either this page or the slice issue with:

- business requirement IDs covered by the slice;
- NFR/security/privacy/operability IDs covered by the slice;
- explicit `Partial` vs `Done` expectations;
- issue and PR links;
- validation evidence, such as tests, typecheck, `./tools/validate.sh`, screenshots, deployment checks, or runbook verification;
- known gaps left to later slices.

If a PR implements user-visible behavior that maps to no requirement, add or update the requirement before merging. If a requirement reaches `Done`, the evidence column must link to the merged PR or deployed artifact that proves it.
