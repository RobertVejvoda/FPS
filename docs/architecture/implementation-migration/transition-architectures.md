# Migration Transition Architectures

Architecture State [Transition Architectures](/architecture/architecture-states/transition-architectures) define the named target states. This page describes the migration planning view: what moves FairSpot from one state to the next.

| Migration Transition | From | To | Capabilities Added | Risks | Exit Criteria |
| --- | --- | --- | --- | --- | --- |
| T1 Architecture repository alignment | Current scattered source evidence | Customer-ready architecture repository structure | ADM checklist, constraints, decisions, glossary, risk register, standards, architecture contract, implementation/migration section. | Documentation can look complete before evidence is validated. | Artifact Register, sidebar, and gap/risk/work-package traceability are consistent. |
| T2 Hosted pilot readiness | Customer-ready target draft | Hosted Pilot Target | Durable tenant state, DataHub projections, role-centered UX, hosted smoke, WAF/auth evidence, contract evidence. | Runtime/security/data gaps can block public customer evaluation. | Hosted smoke path passes or residual risk is accepted by Robert. |
| T3 Customer-owned production handoff | Hosted Pilot Target | Customer-Owned Production Target | Restore drills, operational runbooks, observability evidence, support boundaries, deployment profile hardening. | Customer environment differences may need profile-specific work. | Handoff checklist and recovery evidence are accepted. |

## Cross-Links

| Planning View | Architecture State View |
| --- | --- |
| This page | [Architecture State Transition Architectures](/architecture/architecture-states/transition-architectures) |
| [Work Packages](/architecture/implementation-migration/work-packages) | [Gap Analysis](/architecture/architecture-states/gap-analysis) |
| [Readiness](/architecture/implementation-migration/readiness) | [Risk Register](/architecture/architecture-states/risk-register) |
