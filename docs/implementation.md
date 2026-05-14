The implementation section tracks how FPS is built, validated, and handed off between agents. Business documents explain what the product must do; implementation documents explain how work is sliced, assigned, reviewed, and merged.

## Core Documents

- [Development Plan](./development-plan): roadmap, scope, acceptance criteria, and open risks.
- [Implementation Tracker](./implementation-tracker): completed and planned slices, PRs, implementer attribution, and dates.
- [Booking Implementation Slices](./implementation/booking-vertical-slices): Booking implementation order and acceptance criteria.
- [Tooling](./tooling): validation scripts, hooks, router behavior, and local docs preview.

## Boundary

Implementation documents may reference business rules, service contracts, and architecture decisions, but they should not replace them. If a slice exposes a missing or contradictory business rule, update the relevant business or decision document first, then update the implementation plan.
