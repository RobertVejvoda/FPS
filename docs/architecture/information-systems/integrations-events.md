# Integrations And Events

| Integration / Event Flow | Producer | Consumer | Purpose | Reliability Notes |
| --- | --- | --- | --- | --- |
| Booking lifecycle events | Booking | Notification, Audit, Reporting/DataHub | Notify users, preserve audit evidence, update read models. | Idempotent consumers and source event IDs required. |
| Profile facts lookup | Booking | Profile | Validate employee/vehicle/eligibility facts. | Booking must not trust client-submitted profile facts alone. |
| Configuration policy/capacity | Configuration | Booking, Web/Admin | Publish tenant policy and slot/capacity state. | Versioning and history matter for auditability. |
| Identity claims | IdP / Identity | All authenticated services | Establish tenant, user, and role context. | Missing tenant/user identity must fail closed. |
| Audit PII mapping lookup | Audit | Authorized auditor/admin flow | Resolve pseudonymised actors under controlled conditions. | Lookup reason and audit record are required. |
| DataHub projection events | Owning services | DataHub | Build cross-service read models. | Projection checkpoints and replay strategy are required. |

## Source Evidence

- [Booking Event Contracts](/business-layer/booking-event-contracts)
- [Dapr-First Standards](/production/dapr-first-production-standards)
- [Software Architecture](/technology-layer/software-architecture)
