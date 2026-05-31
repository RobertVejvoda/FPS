# Data Architecture

| Data Area | Owner | Classification | Target Direction |
| --- | --- | --- | --- |
| Booking requests and allocations | Booking | Confidential | Service-owned writes; events published for projections. |
| Tenant lifecycle and readiness | Customer | Confidential / Internal | Durable state required before hosted pilot. |
| Profile and vehicle facts | Profile | Confidential | Minimal facts, tenant-scoped, SSO-first alignment. |
| Policy, locations, and capacity | Configuration | Internal / Confidential | Versioned policy and slot publication. |
| Notifications and preferences | Notification | Confidential | Operational notifications remain mandatory where required. |
| Audit records and PII mapping | Audit | Confidential / Secret-adjacent controls | Pseudonymised audit records with restricted PII mapping. |
| Cross-service read models | DataHub | Depends on projection | PostgreSQL-backed event-fed projections. |
| Technical telemetry | Observability stack | Internal | Logs/metrics/traces are operator evidence, not business audit. |

## Target Rules

- Writes stay with the owning business service.
- Cross-service operational reads are projected into DataHub.
- Tenant data must be isolated by authenticated/service context.
- Audit evidence must remain privacy-aware and append-only where required.

## Source Evidence

- [DataHub](/application-layer/datahub)
- [Security Model](/security/security-model)
- [Tenant Storage Contract](/production/tenant-storage-contract)
