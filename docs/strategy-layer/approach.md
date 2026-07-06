# Goals And Approach

FairSpot should prove the scarce shared-capacity product story through one concrete first module: a B2B organization with scarce parking can replace manual coordination with a transparent request and Draw process that users, operators, administrators, and client IT can understand.

This page is product-facing. Architecture constraints and target-state detail are maintained in [Architecture Vision](../architecture/architecture-vision), [Target Architecture](../architecture/architecture-states/target-architecture), and [Transition Architectures](../architecture/architecture-states/transition-architectures).

## Adoption Path

| Step | Product Goal | Evidence Needed |
| --- | --- | --- |
| Confirm pilot fit | Find B2B organizations where parking demand exceeds supply and operators still coordinate requests manually. | Number of users needing parking, parking capacity, company-car/accessibility constraints, and current coordination pain. |
| Run a small-organization pilot | Start with a realistic small customer tenant, initially below about 150 active users, so the fairness story can be tested without enterprise rollout complexity. | User request flow, operator workflows, next Draw visibility, tenant setup, notifications, audit evidence, and hosted smoke result. |
| Validate paid-service fit | Learn whether customers value setup, support, production readiness review, and client-specific integration before implementing product Billing. | Pilot setup effort, support questions, operational gaps, willingness to pay for services, and implementation blockers. |
| Expand carefully | Move to medium-sized organizations only after the parking workflow, deployment profile, and support story are repeatable. | Repeatable onboarding, backup/restore evidence, role-specific views, DataHub/read-model evidence, and security review readiness. |
| Reassess enterprise scope | Treat large enterprise rollout as future scope. | Legal, privacy, support, identity, deployment, observability, security, and licensing obligations accepted explicitly. |

## Product Delivery Rules

- Parking stays the first product module until demo and hosted-pilot evidence are credible.
- Employee trust, HR/facility usefulness, tenant administration, and client IT reviewability are all part of product readiness.
- Seats, sport courts, desks, lockers, chargers, and similar resources belong to the same product model; parking remains the first proof path and resource-specific rules are added when each module is implemented.
- Billing is not a prerequisite for customer evaluation.
- Commercial services should reduce adoption effort without hiding fairness, audit, tenant operation, or privacy behind paid-only features.

## Architecture Link

The architecture repository translates this approach into target-state artifacts:

| Product Approach | Architecture Artifact |
| --- | --- |
| Scarce shared-capacity scope, parking first | [Architecture Vision](../architecture/architecture-vision), [Target Architecture](../architecture/architecture-states/target-architecture) |
| Small-organization pilot first | [Stakeholders and Concerns](../architecture/stakeholders-and-concerns), [Transition Architectures](../architecture/architecture-states/transition-architectures) |
| Repeatable deployment and evidence | [Deployment Profiles](../architecture/technology/deployment-profiles), [Observability](../architecture/technology/observability) |
| Open, inspectable fairness story | [Principles](../architecture/principles), [Policies](../architecture/business/policies), [Business Processes](../architecture/business/business-processes) |
| Deferred Billing | [Architecture Vision](../architecture/architecture-vision), [Migration Tracker](../architecture/migration-tracker) |
