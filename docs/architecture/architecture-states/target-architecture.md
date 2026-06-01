# Target Architecture

The FairSpot target architecture is a customer-ready, parking-first fair allocation product that can be evaluated and deployed under a client-owned or low-cost hosted profile.

The target is intentionally pilot-sized. It must be coherent enough for a small or medium company with real parking scarcity to evaluate, but it does not need the operating model, sales model, or legal/compliance breadth of a large-enterprise rollout.

## Target Characteristics

| Area | Target State | Acceptance Criteria |
| --- | --- | --- |
| Business | HR, facilities, employees, admins, auditors, and customer IT have role-appropriate views and understandable rules. | Client evaluator can follow the booking, Draw, allocation, cancellation, audit, and reporting story without internal implementation context. |
| Applications | Service boundaries remain provider-neutral; writes stay in owning services; DataHub owns cross-service read models. | Core user workflows are usable through web/mobile/API, and service responsibilities match documented architecture. |
| Data | Tenant-scoped data ownership, audit evidence, notification history, and read models are durable enough for hosted pilot. | Customer and Booking state needed for pilot survives process restart and can be backed up/restored by profile. |
| Technology | Dapr-first runtime supports workflows, pub/sub, state, secrets, resiliency, and observability where appropriate. | Local and hosted profiles can be started, smoke-tested, observed, and recovered with documented runbooks. |
| Security | Authentication, tenant isolation, authorization, privacy, auditability, WAF, and secret handling are explicit. | Hosted pilot passes security checklist and exposes no admin/internal surfaces publicly. |
| Product adoption | Setup, support, production readiness, and client-specific integration can be demonstrated as service opportunities without in-product Billing. | Customer evaluation is not blocked by pricing, subscriptions, or payment workflows. |

## Target Architecture Sections

- [Business Architecture](/architecture/business/)
- [Information Systems Architecture](/architecture/information-systems/)
- [Technology Architecture](/architecture/technology/)
- [Security Architecture](/architecture/security/)
- [Governance](/architecture/governance/)
- [Views and Diagrams](/architecture/views/)

## Target Assumptions

- Billing remains deferred.
- First pilot evaluation is expected to fit a small or medium company before enterprise rollout is reconsidered.
- Fairness, normal tenant operation, audit/privacy evidence, and basic operational insight stay in the open core.
- `fairspot.net` is for the application domain, not the GitHub Pages documentation site.
- Current service namespaces may continue to use `FPS` until a later technical rename is approved.
