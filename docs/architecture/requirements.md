# Architecture Requirements

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.2 |
| **Architecture State** | Target |
| **ADM Phase** | Requirements Management |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before customer architecture review |

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

## Robert TODOs

- Robert TODO: prioritize AR-011 through AR-014 into delivery slices before customer-facing review.
- Robert TODO: confirm which AR items are mandatory for the first hosted demo versus mandatory for a paid pilot.
- Robert TODO: confirm whether Feedback is required as a small evaluator-feedback slice before the first customer demo.

## Source Evidence

- [Business Requirements](/business-layer/requirements)
- [Requirements Traceability](/requirements-traceability)
- [Gap Analysis](/architecture/architecture-states/gap-analysis)
- [Role Intent Roadmap](/business-layer/role-intent-roadmap)
- [Application Architecture 1 Validation](/application-arch-1-validation)
- [Function Map Validation](/function-map-validation)
