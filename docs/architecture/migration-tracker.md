# Architecture Migration Tracker

This tracker controls how legacy FairSpot documentation is migrated into the TOGAF-style architecture repository.

The goal is consistency first. Every architecture layer must be visible up front, even when the content is incomplete. Missing or intentionally deferred layers are recorded as placeholders instead of being hidden in older documentation.

## Migration Rules

- `docs/architecture/` is the authoritative architecture repository structure.
- Legacy pages under `business-layer/`, `application-layer/`, `technology-layer/`, `security/`, `production/`, and delivery trackers remain source evidence until explicitly migrated or superseded.
- Migration is not a copy operation. Content should be restated into the target architecture viewpoint when needed.
- If content is missing, create or keep a placeholder and mark the status as `Placeholder`.
- If content exists but conflicts with the target architecture direction, record the gap in [Gap Analysis](/architecture/architecture-states/gap-analysis).
- Durable architecture decisions stay in [Versions and Decisions](/versions-and-decisions).
- Operational runbooks can remain in `production/` until the technology and operations repository structure is mature enough to absorb or reference them cleanly.

## Status Legend

| Status | Meaning |
| --- | --- |
| Migrated | Content has been restated in the architecture repository and legacy material is no longer the primary source. |
| Partial | Some content has been migrated, but legacy evidence is still needed. |
| Placeholder | The repository has an explicit page or section, but content is not yet complete. |
| Deferred | Known scope is intentionally postponed. Deferred scope must still be visible. |
| Source Evidence | Legacy content is useful input but not yet migrated. |

## Layer Coverage

| Layer / Area | Target Repository Location | Current Status | Source Evidence | Notes |
| --- | --- | --- | --- | --- |
| Preliminary and governance | [Governance](/architecture/governance/) | Partial | [Versions and Decisions](/versions-and-decisions), [Delivery Board](/delivery-board) | Governance structure exists. Architecture review cadence, board practice, and RACI need validation during customer-ready preparation. |
| Architecture vision | [Architecture Vision](/architecture/architecture-vision) | Placeholder | [Strategy](/strategy), [Client Evaluation Pack](/client-evaluation-pack), [Roadmap](/roadmap) | Needs customer-ready target statement, scope boundaries, and success measures. |
| Stakeholders and concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Placeholder | [Personas](/business-layer/personas), [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap) | HR, employee, administrator, facilities, tenant owner, operator, and security concerns should be explicit. |
| Requirements | [Requirements](/architecture/requirements) | Placeholder | [Requirements Traceability](/requirements-traceability), [Business Requirements](/business-layer/requirements) | Needs prioritized customer-ready requirements and traceability to slices. |
| Business architecture | [Business Architecture](/architecture/business/) | Partial | [Business Layer](/business-layer), [Functional Architecture](/business-layer/functional-architecture), [Business Process Flows](/business-layer/business-process-flows) | Core parking-first business content has been restated. Still draft because business diagrams, customer validation, HR/admin UI validation, Customer persistence, and DataHub/reporting gaps remain. |
| Information systems architecture | [Information Systems](/architecture/information-systems/) | Partial | [Application Layer](/application-layer), [DataHub](/application-layer/datahub), [Software Architecture](/technology-layer/software-architecture) | DataHub/CQRS direction must replace obsolete Reporting/PostgreSQL assumptions. |
| Technology architecture | [Technology Architecture](/architecture/technology/) | Partial | [Technology Layer](/technology-layer), [Production](/production), [Dapr-first Standards](/production/dapr-first-production-standards) | Dapr-first runtime, NAS/Cloudflare hosting, workflow, observability, and deployment profiles need consolidation. |
| Security architecture | [Security Architecture](/architecture/security/) | Partial | [Security](/security), [Security Model](/security/security-model), [Cloudflare WAF Profile](/security/cloudflare-waf-profile) | Needs a target trust-boundary view and explicit controls mapped to customer-ready deployment. |
| Architecture states | [Architecture States](/architecture/architecture-states/) | Partial | [Implementation Tracker](/implementation-tracker), [Roadmap](/roadmap), validation pages | Target-first modeling exists. Gap and transition architecture need stronger links to deployable slices. |
| Views and diagrams | [Views and Diagrams](/architecture/views/) | Partial | [Architecture Views](/architecture-views), `docs/images/`, `docs/archi/`, draw.io files | Existing diagrams are indexed as source evidence. Missing diagrams must be listed as placeholders. |
| Billing | Business / information systems / technology pages as needed | Deferred | [Billing Business](/business-layer/billing), [Billing Application](/application-layer/billing), [Billing Technology](/technology-layer/billing) | Billing is known non-priority scope. Keep visible but out of customer-first delivery. |
| Reporting | [Data Architecture](/architecture/information-systems/data-architecture) and future report catalog if retained | Deferred | [Reporting Business](/business-layer/reporting), [Reporting Application](/application-layer/reporting), [Reporting Technology](/technology-layer/reporting) | Reporting-as-PostgreSQL direction is obsolete. Reframe as DataHub/read-model/report catalog only if needed. |
| Customer service / tenant storage | [Service Catalog](/architecture/information-systems/service-catalog), [Data Architecture](/architecture/information-systems/data-architecture) | Placeholder | [Customer Application](/application-layer/customer), [Tenant Storage Contract](/production/tenant-storage-contract) | Physical tenant persistence remains a known gap and should be tracked in target and transition states. |

## Migration Slices

| Priority | Slice | Outcome |
| --- | --- | --- |
| 1 | Normalize layer coverage | Keep this tracker, artifact register, and diagram index aligned so missing areas are visible immediately. |
| 2 | Business architecture migration | Core content migrated. Remaining work is diagram refresh, customer validation, and closing visible placeholders. |
| 3 | Information systems migration | Restate service catalog, application architecture, DataHub/read model, APIs, and events. |
| 4 | Technology architecture migration | Consolidate runtime platform, Dapr usage, hosting profiles, workflow, deployment, and observability. |
| 5 | Security architecture migration | Consolidate identity, tenant isolation, privacy, WAF, encryption, audit, and operational controls. |
| 6 | Architecture states and roadmap | Turn known gaps into transition architecture and customer-ready roadmap slices. |
| 7 | Diagram refresh | Replace source-evidence diagrams with authoritative architecture views where needed. |

## Completion Checks

Before a migrated layer is treated as review-ready:

- The layer has an owner, status, version, and review trigger in [Artifact Register](/architecture/artifact-register).
- Each missing sub-area is marked `Placeholder` or `Deferred`.
- Source evidence is linked, but not treated as authoritative when it contradicts the target repository.
- Customer-first gaps are linked to [Gap Analysis](/architecture/architecture-states/gap-analysis) or delivery issues.
- Required diagrams are either linked or listed as placeholders in [Diagrams](/architecture/views/diagrams).
