# TOGAF ADM Map

This page maps FairSpot documentation to TOGAF ADM areas. The mapping is intentionally lightweight: it makes coverage and validation explicit without forcing every TOGAF artifact.

## ADM Coverage

| ADM Phase | FairSpot Pages | Status | Notes |
| --- | --- | --- | --- |
| Preliminary | [Architecture Repository](/architecture/), [Principles](/architecture/principles), [Governance](/architecture/governance/), [Artifact Register](/architecture/artifact-register) | Draft | Defines repository rules, principles, artifact status/versioning, architecture board, RACI, lifecycle, and review process. |
| A. Architecture Vision | [Architecture Vision](/architecture/architecture-vision), [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) | Draft | Explains FairSpot purpose, scope, stakeholders, concerns, and customer evaluation story. |
| B. Business Architecture | [Business Architecture](/architecture/business/), [Capabilities](/architecture/business/capabilities), [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies) | Draft | Covers capabilities, actors, processes, policies, and parking allocation business rules. |
| C. Information Systems Architecture | [Information Systems](/architecture/information-systems/), [Application Architecture](/architecture/information-systems/application-architecture), [Data Architecture](/architecture/information-systems/data-architecture), [Integrations and Events](/architecture/information-systems/integrations-events) | Draft | Covers applications, bounded contexts, data/read-model direction, APIs, and events. |
| D. Technology Architecture | [Technology Architecture](/architecture/technology/), [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles), [Observability](/architecture/technology/observability) | Draft | Covers runtime, Dapr, deployment profiles, observability, and operations. |
| E. Opportunities And Solutions | [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Roadmap](/roadmap) | Draft | Compares current-state evidence to target architecture and identifies gaps. |
| F. Migration Planning | [Implementation Tracker](/implementation-tracker), [Delivery Board](/delivery-board), [Work Packages](/architecture/architecture-states/transition-architectures) | Draft | Uses existing slice evidence and future transition states to plan change. |
| G. Implementation Governance | [Architecture Review](/architecture/governance/architecture-review), [Delivery Board](/delivery-board), [Implementation Tracker](/implementation-tracker), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Draft | PR/slice reviews validate whether implementation conforms to target architecture. |
| H. Architecture Change Management | [Change Control](/architecture/governance/change-control), [Waivers](/architecture/governance/waivers), [Versions and Decisions](/versions-and-decisions), [Architecture Version Register](/architecture/architecture-states/architecture-version-register) | Draft | Records durable decisions, artifact state changes, baseline changes, waivers, and supersession. |
| Requirements Management | [Requirements](/architecture/requirements), [Requirement Traceability](/requirements-traceability), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Draft | Maintains architecture-significant requirements and traces gaps to work. |

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
