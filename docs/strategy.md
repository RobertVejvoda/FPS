# Strategy

FairSpot starts with parking because parking is a concrete, high-friction reservation problem: demand often exceeds supply, allocation decisions affect employees directly, and manual coordination creates poor evidence. The strategic goal is to turn that process into a fair, auditable, tenant-isolated allocation platform for limited workplace resources.

Parking remains the v1 product focus. Future resource domains such as desks, chairs, or company seats should reuse the same platform ideas only after parking reaches a stable demo and hosted baseline.

This page is the product-level strategy entry. Architecture scope, constraints, stakeholders, target state, and gaps are maintained in the [Architecture Repository](./architecture/).

## Strategic Questions

| Question | Where To Read |
| --- | --- |
| What values guide the product and architecture? | [Core Values](./strategy-layer/core-values) |
| What is the adoption approach? | [Goals and Approach](./strategy-layer/approach) |
| How is licensing handled? | [Licensing Policy](./strategy-layer/licensing) |
| How will clients evaluate the product? | [Demo and Evaluation](./demo-and-evaluation) |
| What is the target architecture? | [Architecture Vision](./architecture/architecture-vision), [Target Architecture](./architecture/architecture-states/target-architecture) |
| Where are known gaps tracked? | [Gap Analysis](./architecture/architecture-states/gap-analysis), [Migration Tracker](./architecture/migration-tracker) |

## Strategic Direction

- Keep the product understandable to business readers: problem, actors, policy, outcomes, and evidence first.
- Start with small, realistic parking pilots before larger enterprise rollout.
- Keep the free/open core useful enough to prove fairness, tenant operation, and employee trust.
- Recover cost through setup, support, production readiness, and client-specific integration before product Billing is approved.
- Keep future workplace-resource booking as a product extension, not a distraction from parking v1.

Architecture-level direction such as Dapr-first runtime boundaries, DataHub read models, client-owned deployment profiles, and security/privacy controls belongs in the architecture repository so customer reviewers can assess it consistently.
