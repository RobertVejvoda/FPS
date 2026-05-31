# Business Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.2 |
| **Architecture State** | Target |
| **ADM Phase** | Phase B - Business Architecture |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before customer architecture review |

FairSpot business architecture describes the parking-first fair allocation operating model. The target business state is a transparent, auditable booking and Draw process that reduces HR/facilities coordination work while keeping employees informed.

## Migration Status

Core business architecture has been restated from legacy business-layer evidence into this repository. It is still `Draft` because diagram refresh, customer validation, and transition-state gap closure are not complete.

| Area | Status | Notes |
| --- | --- | --- |
| Capabilities | Partial | Customer-ready parking capabilities are stated. Billing remains deferred. |
| Value streams | Partial | Core tenant, employee, HR, audit, and deployment value streams are stated. Detailed ArchiMate diagrams are still placeholders. |
| Actors and roles | Partial | Primary operating roles are stated. RACI remains governed separately. |
| Business processes | Partial | Customer-first parking flows are migrated. Some implementation gaps remain in Customer persistence, HR/admin operations, and durable DataHub projections. |
| Policies | Partial | Allocation, lifecycle, notification, privacy, and deferred-scope policies are stated. Tenant-specific policy configuration still needs implementation validation. |

## Legacy Business Evidence Categorization

| Legacy Source | New Target Location | Migration Result |
| --- | --- | --- |
| `business-layer/requirements.md` | [Architecture Requirements](/architecture/requirements), [Capabilities](/architecture/business/capabilities), [Value Streams](/architecture/business/value-streams) | Business goals and customer-ready requirements are restated. Detailed implementation acceptance criteria remain source evidence until linked to issues. |
| `business-layer/personas.md`, `business-layer/roles.md`, `business-layer/role-intent-roadmap.md` | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns), [Actors and Roles](/architecture/business/actors-roles) | Primary actors, role-specific default views, and concerns are restated. |
| `business-layer/business-process-flows.md` | [Business Processes](/architecture/business/business-processes), [Value Streams](/architecture/business/value-streams) | Operational sequence is restated at target business level. Detailed legacy flow remains source evidence until diagrams are refreshed. |
| `business-layer/allocation-rules.md`, `booking-request-lifecycle.md`, `booking-reason-codes.md`, `parking-policy-configuration.md` | [Business Processes](/architecture/business/business-processes), [Policies](/architecture/business/policies), later Information Systems contracts | Business rules are summarized here. Executable rule details remain implementation contracts until migrated into API/event/domain contract artifacts. |
| `business-layer/customer.md`, `tenant-onboarding.md`, `customer-data-import.md` | [Capabilities](/architecture/business/capabilities), [Business Processes](/architecture/business/business-processes), [Information Systems](/architecture/information-systems/) | Customer/tenant onboarding is partially migrated. Durable tenant storage remains a visible architecture gap. |
| `business-layer/reporting.md` | [Capabilities](/architecture/business/capabilities), [Data Architecture](/architecture/information-systems/data-architecture) | Reporting is reframed as DataHub-backed operational insight. Reporting-as-durable-projection-store is obsolete. |
| `business-layer/billing.md` | Deferred scope in this page and [Migration Tracker](/architecture/migration-tracker) | Billing remains visible but excluded from customer-ready target. |
| `business-layer/feedback.md` | Pilot feedback capability and future support process | Narrow authenticated pilot feedback remains optional P2; broad support portal remains deferred. |

## Contents

- [Capabilities](/architecture/business/capabilities)
- [Value Streams](/architecture/business/value-streams)
- [Actors and Roles](/architecture/business/actors-roles)
- [Business Processes](/architecture/business/business-processes)
- [Policies](/architecture/business/policies)

## Source Evidence

- [Legacy Business Layer](/business-layer)
- [Functional Architecture](/business-layer/functional-architecture)
- [Business Process Flows](/business-layer/business-process-flows)
- [Booking Request Lifecycle](/business-layer/booking-request-lifecycle)
- [Allocation Rules](/business-layer/allocation-rules)

## Robert TODOs

- Robert TODO: confirm which business process diagrams must be redrawn before external customer review.
- Robert TODO: confirm whether broad customer support remains deferred or whether a narrow customer-service process is needed before pilot.
- Robert TODO: confirm whether motorcycle-specific capacity remains future scope for v1 customer demos.
