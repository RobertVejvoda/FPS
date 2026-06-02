# TOGAF ADM Map

This page maps FairSpot documentation to TOGAF ADM areas. The mapping is intentionally lightweight: it makes coverage and validation explicit without forcing every TOGAF artifact.

Formal TOGAF deliverables are mapped in [TOGAF Deliverable Coverage](/architecture/artifact-register?id=togaf-deliverable-coverage). FairSpot creates standalone deliverable pages only when the content does not have a natural home elsewhere.

## ADM Coverage

| ADM Phase | FairSpot Pages | Status | Notes |
| --- | --- | --- | --- |
| Preliminary | [Architecture Repository](/architecture/), [ADM Checklist](/architecture/adm-checklist), [Principles](/architecture/principles), [Constraints](/architecture/constraints), [Governance](/architecture/governance/), [Artifact Register](/architecture/artifact-register), [Architecture Glossary](/architecture/glossary) | Draft | Defines repository rules, ADM validation gates, principles, constraints, artifact status/versioning, architecture board, RACI, lifecycle, shared language, and review process. |
| A. Architecture Vision | [Architecture Vision](/architecture/architecture-vision), [Stakeholders and Concerns](/architecture/stakeholders-and-concerns), [Constraints](/architecture/constraints) | Draft | Explains FairSpot purpose, scope, stakeholders, concerns, constraints, and customer evaluation story. |
| B. Business Architecture | [Business Architecture](/architecture/business/), [Capabilities](/architecture/business/capabilities), [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies) | Draft | Covers capabilities, actors, processes, policies, and parking allocation business rules. |
| C. Information Systems Architecture | [Information Systems](/architecture/information-systems/), [Application Architecture](/architecture/information-systems/application-architecture), [Data Architecture](/architecture/information-systems/data-architecture), [Integrations and Events](/architecture/information-systems/integrations-events) | Draft | Covers applications, bounded contexts, data/read-model direction, APIs, and events. |
| D. Technology Architecture | [Technology Architecture](/architecture/technology/), [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles), [Technology Standards](/architecture/technology/standards), [Observability](/architecture/technology/observability) | Draft | Covers runtime, Dapr, approved technology standards, deployment profiles, observability, and operations. |
| E. Opportunities And Solutions | [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Risk Register](/architecture/architecture-states/risk-register), [Implementation And Migration](/architecture/implementation-migration/), [Work Packages](/architecture/implementation-migration/work-packages), [Roadmap](/architecture/implementation-migration/roadmap) | Draft | Compares current-state evidence to target architecture, records risks, groups work packages, and identifies solution increments. |
| F. Migration Planning | [Transition Architectures](/architecture/implementation-migration/transition-architectures), [Work Packages](/architecture/implementation-migration/work-packages), [Readiness](/architecture/implementation-migration/readiness), [Roadmap](/roadmap), [Delivery Board](/delivery-board) | Draft | Converts work package groups into issue-backed migration increments, release sequencing, and readiness checks. |
| G. Implementation Governance | [Architecture Contract](/architecture/governance/architecture-contract), [Architecture Review](/architecture/governance/architecture-review), [Readiness](/architecture/implementation-migration/readiness), [Operations Runbooks](/production), [Delivery Board](/delivery-board), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Draft | PR/slice reviews, contract checks, readiness evidence, and runbook evidence validate whether implementation conforms to target architecture. |
| H. Architecture Change Management | [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers), [Architecture Decisions](/architecture/decisions), [Versions and Decisions](/versions-and-decisions), [Architecture Version Register](/architecture/architecture-states/architecture-version-register) | Draft | Records durable decisions, artifact state changes, baseline changes, waivers, and supersession. |
| Requirements Management | [Requirements](/architecture/requirements), [Architecture Glossary](/architecture/glossary), [Requirement Traceability](/requirements-traceability), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Draft | Maintains architecture-significant requirements, shared terminology, and traces gaps to work. |

## Validation Gate Pattern

An artifact can move from `Draft` to `Approved` or `Baselined` when:

- stakeholder concerns are addressed;
- assumptions and open questions are listed;
- security, privacy, and operational impact are considered;
- affected decisions are recorded;
- baseline-to-target gaps are linked where relevant;
- Robert or the accountable architecture owner accepts the artifact.

## FairSpot Tailoring

FairSpot is a greenfield product, not a classic enterprise estate transformation. The target architecture is the main model. Baseline architecture is represented by current-state evidence and implemented-state snapshots rather than a full baseline architecture document.

Legacy layer pages remain source evidence until their content is migrated or superseded by pages under `docs/architecture/`.

## Roadmap And Operations Placement

| Artifact | TOGAF Placement | FairSpot Rule |
| --- | --- | --- |
| [Roadmap](/roadmap) | Phases E/F | Business-readable sequencing remains product-level, while architecture work packages and target states are controlled in [Implementation And Migration](/architecture/implementation-migration/) and [Transition Architectures](/architecture/implementation-migration/transition-architectures). |
| [Operations Runbooks](/production) | Phases D/G | Runbooks are not target architecture definitions by themselves. They provide implementation-governance evidence for deployment, smoke, backup/restore, observability, incidents, and Dapr hardening. |
| [Product Strategy Source Evidence](/strategy) | Phase A source evidence | Strategy is not a separate public architecture layer. It feeds Architecture Vision, Principles, Roadmap, Commercialisation, and durable decisions. |
