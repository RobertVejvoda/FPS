# TOGAF ADM Map

This page maps FairSpot documentation to TOGAF ADM areas. The mapping is intentionally lightweight: it makes coverage and validation explicit without forcing every TOGAF artifact.

## ADM Coverage

| ADM Phase | FairSpot Pages | Status | Notes |
| --- | --- | --- | --- |
| Preliminary | [Architecture Repository](/architecture/), [Artifact Register](/architecture/artifact-register), [Versions and Decisions](/versions-and-decisions) | Draft | Defines repository rules, artifact status/versioning, decision log, and governance expectations. |
| A. Architecture Vision | [Strategy](/strategy), [Architecture Views](/architecture-views), [Client Evaluation Pack](/client-evaluation-pack) | Draft | Explains FairSpot purpose, scope, stakeholders, and customer evaluation story. |
| B. Business Architecture | [Business Layer](/business-layer), [Functional Architecture](/business-layer/functional-architecture), [Business Process Flows](/business-layer/business-process-flows), [Allocation Rules](/business-layer/allocation-rules) | Draft | Covers capabilities, actors, processes, policies, and parking allocation rules. |
| C. Information Systems Architecture | [Software Architecture](/technology-layer/software-architecture), [DataHub](/application-layer/datahub), [Booking Event Contracts](/business-layer/booking-event-contracts) | Draft | Covers applications, bounded contexts, data/read-model direction, APIs, and events. |
| D. Technology Architecture | [Technology Layer](/technology-layer), [Production](/production), [Dapr-First Standards](/production/dapr-first-production-standards) | Draft | Covers runtime, Dapr, deployment profiles, observability, and operations. |
| E. Opportunities And Solutions | [Architecture States](/architecture/architecture-states/), [Gap Analysis](/architecture/architecture-states/gap-analysis), [Roadmap](/roadmap) | Draft | Compares current-state evidence to target architecture and identifies gaps. |
| F. Migration Planning | [Implementation Tracker](/implementation-tracker), [Delivery Board](/delivery-board), [Work Packages](/architecture/architecture-states/transition-architectures) | Draft | Uses existing slice evidence and future transition states to plan change. |
| G. Implementation Governance | [Delivery Board](/delivery-board), [Implementation Tracker](/implementation-tracker), [Readiness](/architecture/architecture-states/gap-analysis) | Draft | PR/slice reviews validate whether implementation conforms to target architecture. |
| H. Architecture Change Management | [Versions and Decisions](/versions-and-decisions), [Artifact Register](/architecture/artifact-register), [Architecture Version Register](/architecture/architecture-states/architecture-version-register) | Draft | Records durable decisions, artifact state changes, baseline changes, and supersession. |
| Requirements Management | [Requirements](/business-layer/requirements), [Requirement Traceability](/requirements-traceability), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Draft | Maintains architecture-significant requirements and traces gaps to work. |

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
