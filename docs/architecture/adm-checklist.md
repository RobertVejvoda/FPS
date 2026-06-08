# TOGAF ADM Checklist

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Cross-cutting |
| **ADM Phase** | Cross-ADM |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-06-02 |
| **Next Review** | Before customer architecture review |

This checklist is the lightweight readiness tool for the FairSpot architecture repository. It is not a heavy stage-gate process; it helps make gaps, risks, and review evidence visible.

## Preliminary

- Enterprise Continuum distinguishes reusable architecture assets, reusable solution assets, FairSpot product architecture, and FairSpot Operator enterprise architecture.
- Governance, RACI, artifact lifecycle, review rules, and waiver handling are visible.
- Principles, constraints, architecture decisions, and glossary terms are linked.
- Artifact status is tracked in [Artifact Register](/architecture/artifact-register).

## Phase A - Architecture Vision

- Architecture vision states customer-ready target, non-goals, and success measures.
- Stakeholders and concerns are visible.
- Constraints and architecture-significant requirements are separated and linked.

## Phase B - Business Architecture

- Capabilities, value streams, actors, processes, and policies describe the parking-first operating model.
- Role-centered HR, employee, administrator, auditor, and sponsor concerns are visible.
- Deferred Billing and future resource types are explicit.

## Phase C - Information Systems Architecture

- Application boundaries, data ownership, API contracts, event contracts, and DataHub direction are visible.
- Generated/source-of-truth contracts are linked where available.
- Tenant isolation is traced through APIs, events, data stores, and projections.

## Phase D - Technology Architecture

- Runtime platform, deployment profiles, observability, and technology standards are documented.
- Dapr-first expectations and allowed deviations are clear.
- Hosted profile evidence gaps are linked to readiness and risk.

## Phases E/F - Opportunities, Solutions, And Migration Planning

- Baseline evidence, customer-ready target, transition states, gaps, risks, and work packages are linked.
- Roadmap and implementation-migration pages explain sequence and exit criteria.
- Customer-ready hosted pilot is the active transition.

## Phase G - Implementation Governance

- Architecture review uses PR/issue evidence, generated contracts, smoke results, and runbooks where relevant.
- Architecture contract states conformance expectations for principles, standards, tenant isolation, and residual risks.
- Deviations are handled through change control, waivers, or durable decisions.

## Phase H - Change Management

- Changes to approved target architecture update affected artifacts.
- Residual risks and gaps have owners and review dates.
- Architecture versions are updated when target/baseline meaning changes.
