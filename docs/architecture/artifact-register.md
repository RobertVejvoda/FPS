# Architecture Artifact Register

This register tracks the status, version, ownership, and review state of FairSpot architecture artifacts.

## Artifact Metadata Standard

Major architecture pages should use this header when they become governed artifacts.

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
| Architecture Migration Tracker | [Architecture Migration Tracker](/architecture/migration-tracker) | Cross-ADM | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | During each migration slice |
| Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Phase A | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Stakeholders and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Phase A | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Architecture Requirements | [Architecture Requirements](/architecture/requirements) | Requirements Management | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Business Architecture | [Business Architecture](/architecture/business/) | Phase B | Target | Draft | 0.3 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Phase C | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Technology Architecture | [Technology Architecture](/architecture/technology/) | Phase D | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Security Architecture | [Security Architecture](/architecture/security/) | Cross-cutting | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Architecture Principles | [Architecture Principles](/architecture/principles) | Preliminary | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | On architecture principle change |
| Governance | [Governance](/architecture/governance/) | Preliminary + G/H | Cross-cutting | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | On governance change |
| Views and Diagrams | [Views and Diagrams](/architecture/views/) | Cross-ADM | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | On diagram/model change |
| Architecture States | [Architecture States](/architecture/architecture-states/) | Cross-ADM | Baseline / Target / Gap Analysis | Draft | 0.1 | Codex/Product Owner | Robert | - | On milestone change |

## Layer Completeness

The architecture repository must show all expected layers even when content is incomplete.

| Layer / Area | Required Artifact | Completeness Status |
| --- | --- | --- |
| Preliminary and governance | [Governance](/architecture/governance/) | Partial |
| Phase A - Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Partial |
| Stakeholder Map and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Partial |
| Requirements Management | [Requirements](/architecture/requirements) | Partial |
| Phase B - Business Architecture | [Business Architecture](/architecture/business/) | Partial |
| Phase C - Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Partial |
| Phase D - Technology Architecture | [Technology Architecture](/architecture/technology/) | Partial |
| Cross-cutting Security Architecture | [Security Architecture](/architecture/security/) | Partial |
| Architecture states and gaps | [Architecture States](/architecture/architecture-states/) | Partial |
| Views and diagrams | [Views and Diagrams](/architecture/views/) | Partial |
| Deferred billing scope | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |
| Obsolete reporting direction | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |

## TOGAF Deliverable Coverage

FairSpot does not create a separate page for every formal TOGAF deliverable when the mandatory content is covered naturally elsewhere. This matrix records where that content lives and makes missing coverage explicit.

| TOGAF Deliverable / Content Area | FairSpot Coverage | Coverage Status | Notes |
| --- | --- | --- | --- |
| Architecture Repository | [Architecture Repository](/architecture/), [Artifact Register](/architecture/artifact-register), [Architecture Migration Tracker](/architecture/migration-tracker) | Partial | Repository structure exists. Content migration and diagram refresh continue. |
| Architecture Principles | [Principles](/architecture/principles) | Partial | Customer-ready principles have been restated from decision log, Dapr-first direction, tenant isolation, privacy/security, provider-neutral deployment, and delivery governance. Robert approval remains open. |
| Architecture Governance Framework | [Governance](/architecture/governance/), [Architecture Board](/architecture/governance/architecture-board), [RACI](/architecture/governance/raci), [Artifact Lifecycle](/architecture/governance/artifact-lifecycle) | Partial | Lightweight governance, board, RACI, artifact lifecycle, review levels, change control, and waiver rules are restated. Customer-facing sign-off expectations remain open. |
| Request for Architecture Work | [Architecture Vision](/architecture/architecture-vision), [Requirements](/architecture/requirements), [Architecture Migration Tracker](/architecture/migration-tracker) | Placeholder | Covered as intent/scope once Phase A is expanded; no standalone document needed unless an external customer asks for it. |
| Statement of Architecture Work | [Architecture Vision](/architecture/architecture-vision), [Governance](/architecture/governance/), [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Placeholder | Should state scope, constraints, approach, deliverables, acceptance gates, and responsibilities. |
| Stakeholder Map and Concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns), [Actors and Roles](/architecture/business/actors-roles) | Placeholder | Roles are restated in Business Architecture; stakeholder concerns need a dedicated customer-ready pass. |
| Communications Plan | [Governance](/architecture/governance/), [Architecture Review](/architecture/governance/architecture-review), [Change Control](/architecture/governance/change-control) | Placeholder | Current collaboration rules exist in repo guidance; architecture-specific stakeholder communication remains light. |
| Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Placeholder | Needs target statement, scope boundaries, success measures, risks, and customer-first deployability framing. |
| Requirements Impact / Requirements Repository | [Requirements](/architecture/requirements), [Requirements Traceability](/requirements-traceability), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Placeholder | Existing traceability exists outside the new repository; architecture-significant requirements need restatement. |
| Business Architecture Definition | [Business Architecture](/architecture/business/), [Capabilities](/architecture/business/capabilities), [Value Streams](/architecture/business/value-streams), [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies) | Partial | Core business content, legacy evidence categorization, capability disposition, value-stream trace, process classification, and policy gaps are migrated; diagrams and customer validation remain. |
| Data Architecture Definition | [Data Architecture](/architecture/information-systems/data-architecture), [DataHub](/application-layer/datahub) | Partial | Target DataHub/read-model direction is clear; implementation contracts and privacy-shaped projections remain gaps. |
| Application Architecture Definition | [Application Architecture](/architecture/information-systems/application-architecture), [Service Catalog](/architecture/information-systems/service-catalog), [API Contracts](/architecture/information-systems/api-contracts) | Partial | Service boundaries are migrated; generated contract evidence and application cooperation diagram remain gaps. |
| Technology Architecture Definition | [Technology Architecture](/architecture/technology/), [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles), [Observability](/architecture/technology/observability) | Partial | Runtime/deployment direction is migrated; hosted evidence and hardening validation remain gaps. |
| Security Architecture Definition | [Security Architecture](/architecture/security/), [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls), [Security Gap Register](/architecture/security/gap-register) | Partial | Core controls migrated; trust-boundary diagram, retention evidence, and hosted validation remain gaps. |
| Architecture Requirements Specification | [Requirements](/architecture/requirements), [Controls](/architecture/security/controls), [Business Policies](/architecture/business/policies), [API Contracts](/architecture/information-systems/api-contracts) | Placeholder | Requirements are distributed; needs a consolidated architecture-significant requirements pass. |
| Architecture Definition Document | [Business Architecture](/architecture/business/), [Information Systems](/architecture/information-systems/), [Technology Architecture](/architecture/technology/), [Security Architecture](/architecture/security/), [Views and Diagrams](/architecture/views/) | Partial | Covered by layer pages rather than one monolithic document. Diagram set remains incomplete. |
| Architecture Roadmap | [Transition Architectures](/architecture/architecture-states/transition-architectures), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Roadmap](/roadmap), [Implementation Tracker](/implementation-tracker) | Placeholder | Current roadmap evidence exists; transition roadmap needs architecture-state consolidation. |
| Opportunities and Solutions | [Gap Analysis](/architecture/architecture-states/gap-analysis), [Migration Tracker](/architecture/migration-tracker), [Delivery Board](/delivery-board) | Placeholder | Needs explicit grouping of gaps into work packages/capability increments. |
| Migration Plan | [Transition Architectures](/architecture/architecture-states/transition-architectures), [Implementation Tracker](/implementation-tracker), [Delivery Board](/delivery-board) | Placeholder | Delivery board exists; formal migration plan should remain lightweight and issue-backed. |
| Implementation and Migration Plan | [Transition Architectures](/architecture/architecture-states/transition-architectures), [Roadmap](/roadmap), [Delivery Board](/delivery-board) | Placeholder | Should show customer-ready increments, sequencing, acceptance gates, and deferred scope. |
| Architecture Contract | [Architecture Review](/architecture/governance/architecture-review), [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers) | Placeholder | No standalone contract. Conformance expectations should be captured through PR/slice acceptance gates. |
| Compliance Assessment | [Architecture Review](/architecture/governance/architecture-review), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Security Gap Register](/architecture/security/gap-register) | Placeholder | Needs a repeatable review checklist for customer-ready deployment. |
| Architecture Change Request | [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers), [Versions and Decisions](/versions-and-decisions) | Partial | Durable decisions exist; formal change requests can stay lightweight unless external governance requires more. |
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
