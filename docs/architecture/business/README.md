# Business Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Target |
| **ADM Phase** | Phase B - Business Architecture |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | - |
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
