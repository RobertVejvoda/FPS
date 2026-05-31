# Architecture Principles

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Target |
| **ADM Phase** | Preliminary |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | - |
| **Next Review** | On architecture principle change |

| Principle | Statement | Rationale | Implications |
| --- | --- | --- | --- |
| Parking first, extensible later | Parking is the v1 product domain; other scarce resources remain future options. | Focus keeps the product explainable and testable. | Avoid generic resource abstractions unless they reduce real complexity. |
| Tenant context is authoritative | Tenant/user identity comes from authenticated context and service context, not caller-supplied body fields. | Tenant isolation is core to trust and security. | APIs, storage keys, events, and read models must derive tenant safely. |
| Dapr first where it fits | Prefer production-grade Dapr building blocks for workflow, pub/sub, state, secrets, resiliency, and mTLS where appropriate. | FairSpot is also a proof point for production-grade Dapr usage. | Custom infrastructure is a fallback, not the default. |
| Service-owned writes | Owning services remain sources of truth for commands and business state. | Clear ownership reduces cross-service coupling. | Cross-service reads belong in DataHub projections, not direct writes into another service store. |
| Business evidence over raw telemetry | Audit/business timelines are built from business audit records, not raw logs. | Business users need explainable evidence, not operational traces. | Technical telemetry may correlate but does not replace audit records. |
| Provider-neutral core | Core architecture defines contracts; Azure/AWS/NAS/local profiles are deployment examples. | Customer-owned deployment must stay possible. | Provider-specific details live in deployment profiles. |
