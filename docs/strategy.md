# Strategy

FPS starts with parking because parking is a concrete, high-friction reservation problem: demand often exceeds supply, allocation decisions affect employees directly, and manual coordination creates poor evidence. The strategic goal is to turn that process into a fair, auditable, tenant-isolated reservation platform.

Parking remains the v1 product focus. Future resource domains such as desks, chairs, or company seats should reuse the same platform ideas only after parking reaches a stable demo and hosted baseline.

## Strategic Questions

| Question | Where To Read |
| --- | --- |
| What values guide the product and architecture? | [Core Values](./strategy-layer/core-values) |
| What is the implementation approach? | [Goals and Approach](./strategy-layer/approach) |
| How is licensing handled? | [Licensing Policy](./strategy-layer/licensing) |
| How will clients evaluate the product? | [Demo and Evaluation](./demo-and-evaluation) |
| How does the architecture story fit together? | [Architecture Views](./architecture-views) |

## Strategic Direction

- Keep the product understandable to business readers: problem, actors, policy, outcomes, and evidence first.
- Keep implementation sliced vertically so each PR proves a visible capability or operational foundation.
- Keep production pluggable: Dapr for component boundaries and OpenTelemetry for telemetry boundaries.
- Keep client production client-owned: FPS supplies deployable artifacts, runbooks, operational evidence, and integration guidance.
- Keep future workplace-resource booking as a product extension, not a distraction from parking v1.
