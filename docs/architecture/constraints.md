# Constraints

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Cross-cutting |
| **ADM Phase** | Preliminary / Phase A |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-06-02 |
| **Next Review** | Before customer architecture review |

Constraints record limits that shape FairSpot architecture options. They are not requirements by themselves; requirements state what must be true, while constraints narrow which solutions are acceptable.

## Constraint Register

| Constraint ID | Type | Constraint | Impacted Artifacts | Rationale | Owner | Review Trigger | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CON-001 | Schedule | Architecture must be useful before it is complete. Keep the repository lightweight and avoid heavy upfront process. | [Architecture Vision](/architecture/architecture-vision), [ADM Checklist](/architecture/adm-checklist), [Artifact Register](/architecture/artifact-register) | FairSpot needs customer-ready evidence without blocking implementation momentum. | Codex/Product Owner | Before moving artifacts to In Review. | Draft |
| CON-002 | Technology | Dapr-first runtime is the default for workflow, state, pub/sub, service invocation, secrets, resiliency, and outbox where suitable. | [Requirements](/architecture/requirements), [Technology Standards](/architecture/technology/standards), [Runtime Platform](/architecture/technology/runtime-platform) | Dapr is part of the production-grade proof point and provider-neutral deployment story. | Codex/Product Owner | Direct provider SDK or custom infrastructure is proposed. | Draft |
| CON-003 | Technology | Target stack is .NET 10, React, React Native + Expo, PostgreSQL where relational read models are needed, Dapr, and OpenTelemetry. | [Technology Standards](/architecture/technology/standards), [Deployment Profiles](/architecture/technology/deployment-profiles) | The stack keeps web, mobile, server, runtime, and observability decisions coherent for a small team. | Robert / Codex | Runtime or framework replacement is proposed. | Draft |
| CON-004 | Hosting | Pilot deployment must fit a small hosted footprint using the NAS/Cloudflare profile before enterprise scaling is considered. | [Deployment Profiles](/architecture/technology/deployment-profiles), [Risk Register](/architecture/architecture-states/risk-register), [Readiness](/architecture/implementation-migration/readiness) | Customer-first proof should be affordable, inspectable, and operationally realistic. | Robert | Hosted profile or public domain exposure changes. | Draft |
| CON-005 | Compliance | GDPR applies to employee and tenant data, including minimisation, retention, access, erasure, and breach-notification expectations. | [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls), [Requirements](/architecture/requirements) | FairSpot processes employee workplace data and audit evidence. | Robert / Security reviewer | New personal-data processing or retention change. | Draft |
| CON-006 | Team | Governance must work for a single architect/product-owner/implementer operating model and must not require synchronous multi-party approval by default. | [Governance](/architecture/governance/), [Architecture Contract](/architecture/governance/architecture-contract), [Change Control](/architecture/governance/change-control) | Lightweight review keeps the project moving while preserving traceability. | Robert | External customer governance requires stricter approval. | Draft |

## Constraint Handling

| Situation | Expected Handling |
| --- | --- |
| Constraint affects an approved artifact | Record the change through [Change Control](/architecture/governance/change-control) or a durable decision in [Versions and Decisions](/versions-and-decisions). |
| Constraint blocks a target state | Add or update [Gap Analysis](/architecture/architecture-states/gap-analysis) and [Risk Register](/architecture/architecture-states/risk-register). |
| Constraint needs temporary exception | Record a waiver in [Waivers](/architecture/governance/waivers). |
