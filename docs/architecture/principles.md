# Architecture Principles

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.2 |
| **Architecture State** | Target |
| **ADM Phase** | Preliminary |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | On architecture principle change |

| Principle | Statement | Rationale | Implications |
| --- | --- | --- | --- |
| Parking first, extensible later | Parking is the v1 product domain; other scarce resources remain future options. | Focus keeps the product explainable and testable. | Avoid generic resource abstractions unless they reduce real complexity. |
| Tenant context is authoritative | Tenant/user identity comes from authenticated context and service context, not caller-supplied body fields. | Tenant isolation is core to trust and security. | APIs, storage keys, events, and read models must derive tenant safely. |
| Dapr first where it fits | Prefer production-grade Dapr building blocks for workflow, pub/sub, state, secrets, resiliency, and mTLS where appropriate. | FairSpot is also a proof point for production-grade Dapr usage. | Custom infrastructure is a fallback, not the default. |
| Service-owned writes | Owning services remain sources of truth for commands and business state. | Clear ownership reduces cross-service coupling. | Cross-service reads belong in DataHub projections, not direct writes into another service store. |
| Business evidence over raw telemetry | Audit/business timelines are built from business audit records, not raw logs. | Business users need explainable evidence, not operational traces. | Technical telemetry may correlate but does not replace audit records. |
| Provider-neutral core | Core architecture defines contracts; local, NAS/Cloudflare, DigitalOcean, Kubernetes, and client-owned profiles are deployment examples. | Customer-owned deployment must stay possible. | Provider-specific details live in deployment profiles. |
| Target-state first, evidence-backed | FairSpot models the customer-ready target separately from current implementation evidence and known gaps. | There is no mature enterprise baseline yet, but customer reviewers still need clarity. | Baseline/current-state evidence must be labelled as evidence, not mixed into target architecture. |
| Reviewer independence | Implementers do not approve or mark complete their own implementation. | Architecture acceptance requires a separate product/architecture review. | Claude, Copilot, Codex, and humans may provide evidence, but acceptance must come from the accountable reviewer. |
| Issues are delivery source of truth | Work that affects implementation must be captured in GitHub issues or linked docs before routing to an implementer. | Chat context is not durable enough for customer-ready delivery. | Requirements, acceptance criteria, validation, and ownership must be visible from the issue or PR. |
| Security and privacy by default | Tenant isolation, least privilege, auditability, encryption, secrets handling, and GDPR obligations are default architecture concerns. | Security and privacy cannot be added after customer trust is at stake. | Architecture-significant changes must consider security/privacy impact and record accepted risk where relevant. |

## Principle Governance

- Principle changes require architecture review and Robert approval when they alter delivery direction.
- Exceptions to principles must be recorded as waivers or durable decisions.
- A principle should be retired only by marking it deprecated or superseded and linking to the replacement.
- Implementation slices may reference draft principles, but customer-facing claims should rely on approved or baselined architecture.

## Source Evidence

- [Versions and Decisions](/versions-and-decisions)
- [Delivery Board](/delivery-board)
- [Architecture Migration Tracker](/architecture/migration-tracker)
- [Dapr-first Production Standards](/production/dapr-first-production-standards)
