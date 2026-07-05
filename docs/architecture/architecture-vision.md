# Architecture Vision

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.4 |
| **Architecture State** | Target |
| **Target Version** | Customer-Ready Target v0.1 |
| **ADM Phase** | Phase A - Architecture Vision |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-07-05 |
| **Next Review** | Before customer architecture review |

FairSpot is an open-source fair allocation and booking platform for companies where demand for shared workplace resources exceeds supply. Parking is the first launch module and proof vertical, not the product boundary.

## Problem

Companies with limited workplace resources often coordinate requests through email, spreadsheets, or informal priority rules. Parking is the first visible problem, but the same pattern appears with seats, sport courts, desks, lockers, chargers, and other shared resources. Manual coordination creates avoidable HR/facilities work, low transparency, weak auditability, and low employee trust.

## Target Outcome

FairSpot provides a transparent request and Draw process with role-appropriate user experiences, auditable allocation rules, tenant-scoped data, and a deployment model that can run locally, in a hosted pilot, or in a customer-owned environment.

The customer-ready target is not a full enterprise architecture baseline. It is the minimum coherent target architecture needed to let a customer, HR/facilities stakeholder, client IT reviewer, and security reviewer understand whether FairSpot is credible for a hosted demo or pilot.

## Product Strategy Translation

FairSpot's strategy is expressed through the product overview, roadmap, commercialisation notes, durable decisions, and architecture repository. The architecture vision translates that strategy into reviewable target-state constraints.

| Product Direction | Architecture Translation |
| --- | --- |
| Shared-resource platform, parking first | Business, application, data, technology, and security architecture prove the model through the parking launch module while preserving the target direction for seats, sport courts, desks, lockers, chargers, and similar resource types. |
| Small-company pilot first | The customer-ready target optimizes for a realistic tenant, initially below about 150 employees, with visible shared-resource friction and manageable support complexity. Parking remains the first proof path. |
| Trust before scale | Employee-visible explanations, HR/facility auditability, tenant isolation, privacy, and hosted smoke evidence are required before larger rollout. |
| Open core remains useful | Fairness rules, normal tenant operation, basic reporting/read-model evidence, audit, and privacy controls must remain usable without paid unlocks. |
| Paid services before product Billing | Setup, support, production readiness, and client-specific integration may be commercial paths; in-product Billing and payment remain deferred. |
| Client-owned production is credible | Dapr, OpenTelemetry, documented deployment profiles, and runbooks must support client-owned operation without binding the product to one provider. |

## Architecture Scope

| Scope Item | Target Position | Status |
| --- | --- | --- |
| Product domain | Fair allocation and booking for scarce workplace resources. Parking is the first launch module; other resource types reuse the same tenant, policy, booking, Draw, notification, audit, reporting, and usage-evidence model. | Draft |
| Customer readiness | Demonstrable employee, HR/facilities, tenant admin, audit, and operator journeys with explicit gaps. | Draft |
| Pilot fit | First external evaluation should target a small or medium company with visible shared-resource scarcity, starting with parking where the current implementation is strongest. | Draft |
| Deployment posture | Local development, hosted demo, and client-owned production profiles with Dapr/OpenTelemetry portability boundaries. | Draft |
| Data architecture | Service-owned writes and event-fed DataHub read models for cross-service reads. | Draft |
| Security posture | Tenant isolation, SSO-first identity, auditability, privacy, WAF/ingress hardening, and secrets separation. | Draft |
| Commercial scope | Billing and payment are visible future capabilities, not customer-first dependencies. | Deferred |

## Goals

- Make the employee booking and allocation outcome understandable.
- Make HR/facility operation repeatable and auditable.
- Keep tenant identity and data boundaries explicit.
- Use Dapr-first provider-neutral runtime contracts where they fit.
- Keep the free/open core strong enough for a normal tenant to evaluate fairness, auditability, and operations.
- Prepare enough architecture evidence for customer and client IT evaluation.
- Keep architecture content separated by target, current evidence, gaps, and transition work.

## Success Measures

| Measure | Customer-Ready Expectation |
| --- | --- |
| Employee trust | Employee can request, see status, understand outcome, cancel where allowed, and receive notifications without seeing hidden Draw internals. |
| HR/facilities usability | HR can see what needs attention, understand next Draw timing, trigger an authorized Draw where allowed, cancel any tenant-scoped request with reason, and rely on audit/notification evidence. |
| Tenant administration | Administrator can see readiness, identity/profile/policy/location/storage gaps, and avoid exposing technical tenant concepts to employee workflows. |
| Operational confidence | Operator can see deployment profile, Dapr component health, observability, backup/restore expectations, WAF boundary, and hosted smoke evidence. |
| Architecture governance | Draft, approved, deferred, and missing architecture artifacts are visible in the repository and trace to delivery gaps. |
| Commercial restraint | Customer evaluation does not depend on Billing, paid-only fairness, or hidden operational features. |

## Non-Goals

- Billing and commercial enforcement are not part of the customer-ready target.
- Large-enterprise rollout is not part of the first customer-ready target.
- A full enterprise baseline model is not required for FairSpot v1.
- Provider-specific deployment products are examples or profiles, not core architecture.
- Reporting-as-a-service-owned PostgreSQL direction is not the target. Cross-service read models belong in DataHub; Reporting may remain only as catalog/configuration/presentation if needed.

## Statement Of Architecture Work Coverage

FairSpot does not maintain a separate Statement of Architecture Work document for this phase. The mandatory content is covered here and in linked repository pages.

| Content Area | Covered By | Status |
| --- | --- | --- |
| Scope and objective | This page, [Architecture States](/architecture/architecture-states/), [Target Architecture](/architecture/architecture-states/target-architecture) | Draft |
| Constraints | This page, [Principles](/architecture/principles), [Security Architecture](/architecture/security/) | Draft |
| Stakeholders and concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns), [Actors and Roles](/architecture/business/actors-roles) | Draft |
| Work packages and gaps | [Gap Analysis](/architecture/architecture-states/gap-analysis), [Transition Architectures](/architecture/architecture-states/transition-architectures), delivery board issues | Partial |
| Governance and approval | [Governance](/architecture/governance/), [Artifact Lifecycle](/architecture/governance/artifact-lifecycle), [Architecture Review](/architecture/governance/architecture-review) | Draft |

## Robert TODOs

- Robert TODO: confirm the first customer-ready pilot scope and what must be shown live versus documented as a known gap.
- Robert TODO: confirm whether the hosted public-domain profile on NAS plus Cloudflare is acceptable for the first customer-facing demo.
- Robert TODO: provide or approve the refreshed application cooperation diagram that includes Booking, Configuration, Customer, Notification, Audit, DataHub, Reporting, and Dapr integration boundaries.
- Robert TODO: confirm which DataHub projections are mandatory before the first customer evaluation.
- Robert TODO: confirm which architecture artifacts require formal approval before sharing externally.

## Source Evidence

- [Strategy](/strategy)
- [Client Evaluation Pack](/client-evaluation-pack)
- [Versions and Decisions](/versions-and-decisions)
- [Legacy Architecture Evidence](/architecture-views)
- [Application Architecture 1 Validation](/application-arch-1-validation)
