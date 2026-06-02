# Architecture Artifact Register

This register tracks the status, version, ownership, and review state of FairSpot architecture artifacts.

## Artifact Metadata Standard

Top-level pages inside the Architecture Repository should use this header when they become governed artifacts. Detailed child pages, including Architecture State detail pages, are tracked from this register instead of repeating the metadata block on every page.

|  |  |
| --- | --- |
| **Status** | Draft / In Review / Approved / Baselined / Deprecated / Superseded |
| **Version** | 0.1 |
| **Architecture State** | Baseline / Target / Transition / Gap Analysis / Cross-cutting |
| **Baseline Version** | Current State v0.1 |
| **Target Version** | Customer-Ready Target v0.1 |
| **ADM Phase** | Preliminary / A / B / C / D / E / F / G / H / Requirements Management |
| **Responsible** | Architecture Owner |
| **Accountable** | Robert / Architecture Board |
| **Last Reviewed** | YYYY-MM-DD |
| **Next Review** | YYYY-MM-DD or event-triggered |

## Register

| Artifact | Path | ADM Phase | Architecture State | Status | Version | Responsible | Accountable | Last Reviewed | Next Review |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TOGAF ADM Map | [TOGAF ADM Map](/architecture/togaf-adm-map) | Preliminary | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | On structure change |
| ADM Checklist | [ADM Checklist](/architecture/adm-checklist) | Cross-ADM | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | On ADM validation rule change |
| Architecture Migration Tracker | [Architecture Migration Tracker](/architecture/migration-tracker) | Cross-ADM | Cross-cutting | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | During each migration slice |
| Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Phase A | Target | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Stakeholders and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Phase A | Target | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Architecture Requirements | [Architecture Requirements](/architecture/requirements) | Requirements Management | Target | Draft | 0.5 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Constraints | [Constraints](/architecture/constraints) | Preliminary / Phase A | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | Before client architecture review |
| Architecture Decisions | [Architecture Decisions](/architecture/decisions) | Preliminary / Phase H | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | On durable architecture decision change |
| Architecture Glossary | [Architecture Glossary](/architecture/glossary) | Preliminary / Requirements Management | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | On terminology change |
| Business Architecture | [Business Architecture](/architecture/business/) | Phase B | Target | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Phase C | Target | Draft | 0.4 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Technology Architecture | [Technology Architecture](/architecture/technology/) | Phase D | Target | Draft | 0.4 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Technology Standards | [Technology Standards](/architecture/technology/standards) | Phase D | Target | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | On runtime or integration standard change |
| Security Architecture | [Security Architecture](/architecture/security/) | Cross-cutting | Target | Draft | 0.4 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Architecture Principles | [Architecture Principles](/architecture/principles) | Preliminary | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | On architecture principle change |
| Governance | [Governance](/architecture/governance/) | Preliminary + G/H | Cross-cutting | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | On governance change |
| Architecture Contract | [Architecture Contract](/architecture/governance/architecture-contract) | Phase G | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | Before implementation conformance review |
| Implementation And Migration | [Implementation And Migration](/architecture/implementation-migration/) | Phases E/F/G | Transition / Migration | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | During customer-ready pilot transition |
| Views and Diagrams | [Views and Diagrams](/architecture/views/) | Cross-ADM | Target | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | On diagram/model change |
| Architecture States | [Architecture States](/architecture/architecture-states/) | Cross-ADM | Baseline / Target / Gap Analysis | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | On milestone change |
| Risk Register | [Risk Register](/architecture/architecture-states/risk-register) | Cross-ADM | Risk | Draft | 0.1 | Codex/Product Owner | Robert | 2026-06-02 | Before hosted pilot |

## Layer Completeness

The architecture repository must show all expected layers even when content is incomplete.

| Layer / Area | Required Artifact | Completeness Status |
| --- | --- | --- |
| Preliminary and governance | [Governance](/architecture/governance/), [ADM Checklist](/architecture/adm-checklist) | Partial |
| Phase A - Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Partial |
| Stakeholder Map and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Partial |
| Requirements Management | [Requirements](/architecture/requirements) | Partial |
| Constraints | [Constraints](/architecture/constraints) | Draft |
| Phase B - Business Architecture | [Business Architecture](/architecture/business/) | Partial |
| Phase C - Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Partial |
| Phase D - Technology Architecture | [Technology Architecture](/architecture/technology/) | Partial |
| Cross-cutting Security Architecture | [Security Architecture](/architecture/security/) | Partial |
| Architecture states and gaps | [Architecture States](/architecture/architecture-states/) | Partial |
| Implementation and migration | [Implementation And Migration](/architecture/implementation-migration/) | Draft |
| Views and diagrams | [Views and Diagrams](/architecture/views/) | Partial |
| Deferred billing scope | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |
| Obsolete reporting direction | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |

## TOGAF Deliverable Coverage

FairSpot does not create a separate page for every formal TOGAF deliverable when the mandatory content is covered naturally elsewhere. This matrix records where that content lives and makes missing coverage explicit.

| TOGAF Deliverable / Content Area | FairSpot Coverage | Coverage Status | Notes |
| --- | --- | --- | --- |
| Architecture Repository | [Architecture Repository](/architecture/), [Artifact Register](/architecture/artifact-register), [Architecture Migration Tracker](/architecture/migration-tracker) | Partial | Repository structure exists. Content migration and diagram refresh continue. |
| Architecture Principles | [Principles](/architecture/principles), [Constraints](/architecture/constraints), [Architecture Decisions](/architecture/decisions) | Partial | Customer-ready principles have been restated from decision log, Dapr-first direction, tenant isolation, privacy/security, provider-neutral deployment, and delivery governance. Robert approval remains open. |
| Architecture Governance Framework | [Governance](/architecture/governance/), [Architecture Board](/architecture/governance/architecture-board), [RACI](/architecture/governance/raci), [Artifact Lifecycle](/architecture/governance/artifact-lifecycle), [Architecture Contract](/architecture/governance/architecture-contract) | Partial | Lightweight governance, board, RACI, artifact lifecycle, review levels, conformance expectations, change control, and waiver rules are restated. Customer-facing sign-off expectations remain open. |
| Request for Architecture Work | [Architecture Vision](/architecture/architecture-vision), [Requirements](/architecture/requirements), [Constraints](/architecture/constraints), [Architecture Migration Tracker](/architecture/migration-tracker) | Partial | Intent, scope, constraints, strategy translation, and migration slices are covered; no standalone document needed unless an external customer asks for it. |
| Statement of Architecture Work | [Architecture Vision](/architecture/architecture-vision), [Governance](/architecture/governance/), [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Risk Register](/architecture/architecture-states/risk-register) | Partial | Scope, constraints, approach, non-goals, risks, deliverable coverage, and governance links are covered; approval evidence remains open. |
| Stakeholder Map and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns), [Actors and Roles](/architecture/business/actors-roles) | Partial | Stakeholder concerns are restated for customer-ready review; customer-service/support scope remains open. |
| Communications Plan | [Governance](/architecture/governance/), [Architecture Review](/architecture/governance/architecture-review), [Change Control](/architecture/governance/change-control) | Placeholder | Current collaboration rules exist in repo guidance; architecture-specific stakeholder communication remains light. |
| Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Partial | Target statement, scope boundaries, strategy translation, success measures, non-goals, and customer-first deployability framing are stated; Robert approval remains open. |
| Requirements Impact / Requirements Repository | [Requirements](/architecture/requirements), [Architecture Glossary](/architecture/glossary), [Requirements Traceability](/requirements-traceability), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Partial | Requirements Management is continuous across all ADM phases. Central architecture-significant requirements are restated; issue/work-package traceability still needs tightening. |
| Business Architecture Definition | [Business Architecture](/architecture/business/), [Capabilities](/architecture/business/capabilities), [Value Streams](/architecture/business/value-streams), [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies) | Partial | Core business content, legacy evidence categorization, capability disposition, value-stream trace, process classification, and policy gaps are migrated; diagrams and customer validation remain. |
| Data Architecture Definition | [Data Architecture](/architecture/information-systems/data-architecture), [DataHub](/application-layer/datahub) | Partial | Target DataHub/read-model direction, ownership, projection families, and gaps are clear; implementation contracts and privacy-shaped projection evidence remain gaps. |
| Application Architecture Definition | [Application Architecture](/architecture/information-systems/application-architecture), [Service Catalog](/architecture/information-systems/service-catalog), [API Contracts](/architecture/information-systems/api-contracts) | Partial | Service boundaries and legacy evidence disposition are migrated; generated contract evidence and application cooperation diagram remain gaps. |
| Technology Architecture Definition | [Technology Architecture](/architecture/technology/), [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles), [Technology Standards](/architecture/technology/standards), [Observability](/architecture/technology/observability) | Partial | Runtime/deployment direction, Dapr-first interpretation, approved standards, and hosted-profile gaps are migrated; hosted evidence and hardening validation remain gaps. |
| Security Architecture Definition | [Security Architecture](/architecture/security/), [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls), [Security Gap Register](/architecture/security/gap-register) | Partial | Core controls, requirement interpretation, and security evidence disposition are migrated; trust-boundary diagram, retention evidence, and hosted validation remain gaps. |
| Architecture Requirements Specification | [Requirements](/architecture/requirements), [Controls](/architecture/security/controls), [Business Policies](/architecture/business/policies), [API Contracts](/architecture/information-systems/api-contracts) | Partial | Central requirements are maintained in Requirements Management; phase artifacts refine how each requirement is satisfied. |
| Architecture Definition Document | [Business Architecture](/architecture/business/), [Information Systems](/architecture/information-systems/), [Technology Architecture](/architecture/technology/), [Security Architecture](/architecture/security/), [Views and Diagrams](/architecture/views/) | Partial | Covered by layer pages rather than one monolithic document. Diagram set remains incomplete. |
| Architecture Roadmap | [Architecture Roadmap](/architecture/implementation-migration/roadmap), [Transition Architectures](/architecture/architecture-states/transition-architectures), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Risk Register](/architecture/architecture-states/risk-register), [Roadmap](/roadmap) | Partial | Transition architecture groups are stated; delivery issue traceability and acceptance evidence remain. |
| Opportunities and Solutions | [Gap Analysis](/architecture/architecture-states/gap-analysis), [Work Packages](/architecture/implementation-migration/work-packages), [Migration Tracker](/architecture/migration-tracker), [Delivery Board](/delivery-board) | Partial | Gaps are grouped into candidate work package groups; detailed issue linkage remains. |
| Migration Plan | [Implementation And Migration](/architecture/implementation-migration/), [Transition Architectures](/architecture/implementation-migration/transition-architectures), [Work Packages](/architecture/implementation-migration/work-packages), [Readiness](/architecture/implementation-migration/readiness), [Delivery Board](/delivery-board) | Partial | Migration plan remains lightweight and issue-backed; work package groups are now visible. |
| Implementation and Migration Plan | [Architecture Roadmap](/architecture/implementation-migration/roadmap), [Work Packages](/architecture/implementation-migration/work-packages), [Readiness](/architecture/implementation-migration/readiness), [Roadmap](/roadmap) | Partial | Customer-ready increments, sequencing, and major acceptance gates are visible; delivery issue linkage remains. |
| Architecture Contract | [Architecture Contract](/architecture/governance/architecture-contract), [Architecture Review](/architecture/governance/architecture-review), [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers) | Draft | Conformance expectations are captured for principles, standards, contracts, tenant isolation, risks, and gaps. |
| Compliance Assessment | [Architecture Review](/architecture/governance/architecture-review), [Architecture Contract](/architecture/governance/architecture-contract), [Readiness](/architecture/implementation-migration/readiness), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Security Gap Register](/architecture/security/gap-register) | Partial | Repeatable review evidence is emerging through readiness, contract, security, and gap artifacts. |
| Architecture Change Request | [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers), [Architecture Decisions](/architecture/decisions), [Versions and Decisions](/versions-and-decisions) | Partial | Durable decisions exist; formal change requests can stay lightweight unless external governance requires more. |
| Architecture Version Register | [Architecture Version Register](/architecture/architecture-states/architecture-version-register) | Partial | Register exists; needs use during target/baseline validation. |

## Status Definitions

| Status | Meaning |
| --- | --- |
| Draft | Content is being prepared and should not be treated as accepted. |
| In Review | Content is ready for stakeholder or architecture review. |
| Approved | Accountable owner accepts the artifact for its stated scope. |
| Baselined | Artifact is part of a named architecture baseline or target version. |
| Deprecated | Artifact is retained for history but should not guide new work. |
| Superseded | Artifact has been replaced by another artifact or version. |
