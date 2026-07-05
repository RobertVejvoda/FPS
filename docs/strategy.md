# Product Strategy Source Evidence

This page is retained as source evidence. Product strategy is no longer a separate public navigation section.

Current strategy content is governed through:

- [Product Overview](./Home) for the concise public product entry.
- [Architecture Vision](./architecture/architecture-vision) for target scope, outcomes, constraints, non-goals, and Statement of Architecture Work coverage.
- [Principles](./architecture/principles) for durable architecture/product constraints.
- [Roadmap](./roadmap) and [Transition Architectures](./architecture/architecture-states/transition-architectures) for sequencing and migration planning.
- [Commercialisation](./strategy-layer/commercialisation) for paid services, licensing implications, and deferred Billing.
- [Evaluation and Onboarding](./strategy-layer/evaluation-and-onboarding) for the guided pilot funnel, self-onboarding tiers, and the open-runtime / paid-platform (open-core) split.
- [Versions and Decisions](./versions-and-decisions) for durable decisions.

FairSpot is a fair shared-resource booking and allocation platform. Parking is the first launch module because it is a concrete, high-friction reservation problem: demand often exceeds supply, allocation decisions affect employees directly, and manual coordination creates poor evidence. The strategic goal is to make scarce workplace resources fair, auditable, tenant-isolated, and explainable across parking, seats, sport courts, desks, lockers, chargers, and similar bookable resources.

Parking remains the first implementation proof, not the product boundary. New resource domains should reuse the same tenant, policy, booking, Draw, notification, audit, reporting, and usage-evidence model rather than becoming separate one-off products.

Architecture scope, constraints, stakeholders, target state, and gaps are maintained in the [Architecture Repository](./architecture/).

## Strategic Questions

| Question | Where To Read |
| --- | --- |
| What values guide the product and architecture? | [Core Values](./strategy-layer/core-values) |
| What is the adoption approach? | [Goals and Approach](./strategy-layer/approach) |
| How is licensing handled? | [Licensing Policy](./strategy-layer/licensing) |
| How will clients evaluate the product? | [Demo and Evaluation](./demo-and-evaluation) |
| How do prospects evaluate and onboard (pilot funnel, open-core split)? | [Evaluation and Onboarding](./strategy-layer/evaluation-and-onboarding) |
| What is the target architecture? | [Architecture Vision](./architecture/architecture-vision), [Target Architecture](./architecture/architecture-states/target-architecture) |
| Where are known gaps tracked? | [Gap Analysis](./architecture/architecture-states/gap-analysis), [Migration Tracker](./architecture/migration-tracker) |

## Strategic Direction

- Keep the product understandable to business readers: problem, actors, policy, outcomes, and evidence first.
- Start with small, realistic parking-led pilots before larger rollout, while presenting FairSpot as a shared-resource platform.
- Keep the free/open core useful enough to prove fairness, tenant operation, and employee trust.
- Recover cost through setup, support, production readiness, and client-specific integration before product Billing is approved.
- Treat additional resource types as first-class product scope, sequenced after the shared booking/allocation model is stable enough to avoid fragmenting the platform.

Architecture-level direction such as Dapr-first runtime boundaries, DataHub read models, client-owned deployment profiles, and security/privacy controls belongs in the architecture repository so customer reviewers can assess it consistently.
