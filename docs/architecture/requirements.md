# Architecture Requirements

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.3 |
| **Architecture State** | Target |
| **ADM Phase** | Requirements Management |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before customer architecture review |

Requirements Management is continuous across the ADM. Requirements are not owned by only Phase A or Business Architecture; they are captured centrally here and traced to the affected business, information systems, technology, security, governance, and transition architecture views.

When a phase introduces or changes an architecture-significant requirement, update this page or the linked traceability evidence, then reflect the impact in the affected phase artifact.

| ID | Requirement | Source | Affected Views | Status |
| --- | --- | --- | --- | --- |
| AR-001 | FairSpot must preserve tenant isolation across API, persistence, events, audit, and read models. | Security and customer integration decisions | Security, data, application | Draft |
| AR-002 | Booking writes remain owned by Booking; cross-service read models are projected through DataHub. | DataHub direction decision | Information systems, data | Draft |
| AR-003 | Hosted pilot must expose only intended public surfaces through the selected ingress/WAF profile. | Customer-first deployability | Technology, security | Draft |
| AR-004 | Employees must see safe, understandable booking and allocation information without hidden lottery internals. | My Spots / UX decisions | Business, application | Draft |
| AR-005 | Architecture artifacts must separate target state from current-state evidence and known gaps. | TOGAF repository decision | Governance, architecture states | Draft |
| AR-006 | Employee, HR/facilities, tenant administrator, system administrator, auditor, and sponsor views must have role-specific default entry points. | Role intent roadmap | Business, application, security | Draft |
| AR-007 | The next scheduled Draw time and the ability to run an authorized Draw must be visible to the appropriate operational roles. | Draw workflow discussion | Business, application, technology | Draft |
| AR-008 | HR/facilities must be able to cancel any tenant-scoped request only with authorization, reason capture, audit evidence, and employee notification when affected. | HR operational requirement | Business, application, security | Draft |
| AR-009 | Dapr should be the preferred runtime boundary for pub/sub, state, secrets, service invocation, workflows, and security features where the building block fits the requirement. | Dapr-first production direction | Technology, security | Draft |
| AR-010 | Scheduled Draw execution must be safe in multiple container instances and must be idempotent for the same tenant/location/date/time-slot key. | Draw scheduling direction | Business, technology | Draft |
| AR-011 | Customer tenant registry, tenant identity setup, first admins, readiness, and parking bootstrap state must be durably stored. | Customer service gap | Information systems, technology | Placeholder |
| AR-012 | Reporting-as-PostgreSQL is obsolete as the primary target; DataHub owns durable event-fed read models and Reporting may only keep report catalog/configuration/presentation responsibilities. | DataHub direction decision | Information systems, data | Draft |
| AR-013 | DataHub projections must be tenant-scoped, event-idempotent, privacy-shaped, restart-safe, and suitable for approved operational reads and report views. | DataHub direction decision | Information systems, security | Placeholder |
| AR-014 | Hosted demo evidence must include smoke checks for authentication, booking, Draw, notification, audit, DataHub/read models when present, observability, backup/restore expectations, and WAF boundary. | Client evaluation pack | Technology, security, architecture states | Placeholder |
| AR-015 | Billing and payment capabilities must remain visible as future/deferred architecture scope but must not block the customer-first deployable target. | Billing priority decision | Business, information systems | Draft |
| AR-016 | Architecture pages that are governed artifacts must show metadata, status, version, owner, accountable owner, and review trigger. | TOGAF repository decision | Governance | Draft |
| AR-017 | Facilities must be able to maintain and publish tenant-scoped resource maps, zones, capacity pools, capabilities, and temporary closures with validation and audit before changes affect allocation. | Business requirements and facilities persona | Business, information systems, security | Placeholder |
| AR-018 | HR/facilities support views must allow safe request lookup and lifecycle explanation without exposing hidden Draw internals, unrelated employee data, or raw technical diagnostics. | Role intent roadmap | Business, application, security | Placeholder |
| AR-019 | Customer sponsors must be able to review aggregated business-value evidence for demand, allocation, utilization, fairness, cancellations, no-shows, unmet demand, and capacity pressure without employee-level detail by default. | Personas and reporting direction | Business, data, security | Placeholder |
| AR-020 | Customer-visible policy or capacity changes should support advisory impact preview when reliable DataHub projections exist; preview must not allocate spaces or override policy. | Role intent roadmap and DataHub direction | Business, data, application | Placeholder |

## ADM Phase Impact

| ADM Area | Requirement Use |
| --- | --- |
| Preliminary | Defines governance, principles, artifact lifecycle, review gates, and how requirement changes are controlled. |
| Phase A - Architecture Vision | Sets target scope, stakeholder concerns, success measures, and initial architecture-significant requirements. |
| Phase B - Business Architecture | Refines requirements into capabilities, value streams, processes, roles, and policies. |
| Phase C - Information Systems | Converts requirements into application, data, API, event, service, and read-model contracts. |
| Phase D - Technology Architecture | Converts requirements into runtime, deployment, Dapr, observability, backup, and operational platform needs. |
| Phases E/F - Opportunities, Solutions, Migration | Groups gaps into transition architectures, work packages, priorities, and delivery sequencing. |
| Phase G - Implementation Governance | Checks implementation evidence and PRs against accepted requirements and architecture constraints. |
| Phase H - Architecture Change Management | Reopens or revises requirements when customer feedback, risk, implementation evidence, or operating assumptions change. |
| Security / Privacy | Cross-cutting requirements can affect every phase and may require explicit risk acceptance or controls. |

## Robert TODOs

- Robert TODO: prioritize AR-011 through AR-014 and AR-017 through AR-020 into delivery slices before customer-facing review.
- Robert TODO: confirm which AR items are mandatory for the first hosted demo versus mandatory for a paid pilot.
- Robert TODO: confirm whether Feedback is required as a small evaluator-feedback slice before the first customer demo.
- Robert TODO: confirm whether requirements approval should happen per phase artifact, centrally on this page, or both for customer-facing reviews.

## Source Evidence

- [Business Requirements](/business-layer/requirements)
- [Requirements Traceability](/requirements-traceability)
- [Gap Analysis](/architecture/architecture-states/gap-analysis)
- [Role Intent Roadmap](/business-layer/role-intent-roadmap)
- [Application Architecture 1 Validation](/application-arch-1-validation)
- [Function Map Validation](/function-map-validation)
