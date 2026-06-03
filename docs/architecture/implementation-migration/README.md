# Implementation And Migration

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Transition / Migration |
| **ADM Phase** | Phases E/F/G |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-06-02 |
| **Next Review** | During customer-ready pilot transition |

Implementation and Migration turns FairSpot architecture gaps into sequenced work packages, roadmap increments, and readiness evidence.

FairSpot is currently in the **Customer-Ready Pilot** transition: the product story is coherent, but hosted pilot readiness still depends on durable state, DataHub projections, hosted profile evidence, role-centered UX validation, contract evidence, and diagram refresh.

## Contents

- [Roadmap](/architecture/implementation-migration/roadmap)
- [Transition Architectures](/architecture/implementation-migration/transition-architectures)
- [Work Packages](/architecture/implementation-migration/work-packages)
- [Readiness](/architecture/implementation-migration/readiness)

## Related Views

| View | Purpose |
| --- | --- |
| [Transition Roadmap View](/architecture/views/diagrams?id=target-view-catalog) | Shows customer-ready work packages, transition states, delivery gates, and deferred scope. |
| [Deployment Diagram](/architecture/views/diagrams?id=target-view-catalog) | Supports hosted pilot readiness and release validation. |
| [Application Logical Architecture Diagram](/architecture/views/diagrams?id=target-view-catalog) | Supports migration planning for service boundaries, DataHub, Customer persistence, and Reporting cleanup. |
| [Data Ownership and Read-Model View](/architecture/views/diagrams?id=target-view-catalog) | Supports transition planning for DataHub projections and remaining data gaps. |

## Migration Principles

| Principle | Meaning |
| --- | --- |
| Customer-ready first | Work is sequenced around deployable customer evaluation, not broad enterprise completeness. |
| Evidence over assertion | Gaps close only when linked to implementation, runbook, contract, or review evidence. |
| Keep Billing deferred | Billing remains visible but outside customer-first deployability. |

## Requirement Coverage

Implementation and Migration interprets central requirements from [Requirements Management](/architecture/requirements) as transition states, work packages, readiness evidence, release checks, accepted risks, or deferred gaps.

| Requirement | Implementation / Migration Interpretation | Impacted Artifacts | Evidence / Gap |
| --- | --- | --- | --- |
| AR-011 / AR-013 | Customer durable state and DataHub projections are transition work packages before customer-ready hosted evaluation. | [Roadmap](/architecture/implementation-migration/roadmap), [Work Packages](/architecture/implementation-migration/work-packages), [Readiness](/architecture/implementation-migration/readiness) | Customer persistence evidence exists; DataHub projections and health remain active gaps. |
| AR-014 / AR-021 / AR-022 | Hosted smoke, backup/restore, Dapr hardening, and operational evidence must be validated or accepted as residual risk during Release 1. | [Readiness](/architecture/implementation-migration/readiness), [Risk Register](/architecture/architecture-states/risk-register), [Architecture Roadmap](/architecture/implementation-migration/roadmap) | Release 1 validation issue tracks pass/fail/residual risk. |
| AR-006 / AR-007 / AR-008 / AR-024 | Role-centered UX, Draw visibility/progress, HR cancellation, and role-safe outputs become release validation checks or follow-up issues. | [Roadmap](/architecture/implementation-migration/roadmap), [Work Packages](/architecture/implementation-migration/work-packages), [Readiness](/architecture/implementation-migration/readiness) | Draw schedule is merged; Draw progress, HR operations, and role-safe output review remain open. |
| AR-015 | Billing remains deferred and must not block Release 1 or customer-first deployability. | [Roadmap](/architecture/implementation-migration/roadmap), [Work Packages](/architecture/implementation-migration/work-packages) | No release work unless a commercial decision changes. |
