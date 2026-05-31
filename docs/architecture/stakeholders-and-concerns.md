# Stakeholders And Concerns

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.3 |
| **Architecture State** | Target |
| **ADM Phase** | Phase A - Architecture Vision |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before customer architecture review |

| Stakeholder | Concerns | Views That Answer Them |
| --- | --- | --- |
| Employee | Booking is simple, outcome is clear, personal data is protected, default vehicle/profile facts reduce repeated input, and technical tenant terms are hidden. | [Business Architecture](/architecture/business/), [Actors and Roles](/architecture/business/actors-roles), [Security Architecture](/architecture/security/) |
| HR / Facilities | Requests, Draw, cancellation, support actions, next Draw timing, capacity mismatches, and policy exceptions are manageable and auditable. | [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies), [Application Architecture](/architecture/information-systems/application-architecture) |
| Tenant Administrator | Tenant setup, identity, roles, locations, policy, customer storage, readiness, and bootstrap data survive restarts and are visible. | [Business Architecture](/architecture/business/), [Information Systems](/architecture/information-systems/), [Service Catalog](/architecture/information-systems/service-catalog) |
| System Administrator / Operator | Deployment, secrets, observability, backup, WAF, Dapr component health, and hosted smoke evidence are explicit without bypassing tenant authorization. | [Technology Architecture](/architecture/technology/), [Deployment Profiles](/architecture/technology/deployment-profiles), [Observability](/architecture/technology/observability) |
| Auditor / Compliance Reviewer | Business decisions are explainable and supported by audit evidence without exposing unrelated personal data or technical logs. | [Security Architecture](/architecture/security/), [Controls](/architecture/security/controls), [Privacy Architecture](/architecture/security/privacy-architecture) |
| Client IT / Security Reviewer | Identity, tenancy, ingress, secrets, data stores, Dapr, encryption, audit, retention, backup, and operational responsibilities are reviewable. | [Security Architecture](/architecture/security/), [Technology Architecture](/architecture/technology/), [Architecture States](/architecture/architecture-states/) |
| Product Sponsor | Product value, open-source posture, customer readiness, deferred billing scope, and visible implementation gaps are clear. | [Architecture Vision](/architecture/architecture-vision), [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis) |
| Pilot Customer Sponsor | The first pilot is small enough to support, valuable enough to prove parking fairness, and clear about what is live versus a known gap. | [Architecture Vision](/architecture/architecture-vision), [Target Architecture](/architecture/architecture-states/target-architecture), [Transition Architectures](/architecture/architecture-states/transition-architectures) |
| Product Owner / Architecture Owner | Architecture decisions, delivery priorities, acceptance criteria, versioning, and implementation handoffs stay consistent. | [Governance](/architecture/governance/), [Artifact Register](/architecture/artifact-register), [Migration Tracker](/architecture/migration-tracker) |

## Concern To View Traceability

| Concern | Required View / Artifact | Current Coverage |
| --- | --- | --- |
| Employee-safe allocation explanation | Business process, policy, application API, and mobile/web UX views. | Partial |
| HR daily operations and request cancellation | Business process, actor/role, application cooperation, audit, and notification views. | Partial |
| Next Draw schedule visibility | Business process, application API, DataHub/read-model, and UI views. | Placeholder |
| Customer tenant durability | Application service catalog, data architecture, transition architecture, and gap analysis. | Placeholder |
| DataHub read models | Data architecture, integration/events, API contracts, and transition architecture. | Placeholder |
| Hosted public-domain readiness | Deployment profile, WAF/security, observability, backup/restore, and hosted smoke evidence. | Partial |
| Role-specific default views | Actor/role, application architecture, web/mobile UX, and authorization views. | Partial |
| Audit and privacy evidence | Security architecture, controls, privacy architecture, audit service, and retention evidence. | Partial |
| Billing exclusion | Architecture vision, migration tracker, and gap analysis. | Draft |
| Pilot size and supportability | Architecture vision, target architecture, transition architecture, deployment profile, and client evaluation pack. | Draft |

## Open Concerns

- Robert TODO: confirm which hosted pilot profile is accepted for the first public-domain demo.
- Robert TODO: confirm which DataHub projections are required before customer evaluation.
- Robert TODO: confirm which architecture artifacts must be approved before a customer architecture review.
- Robert TODO: confirm whether a separate customer-service/support stakeholder is required now or whether evaluator feedback is enough for the first pilot.
