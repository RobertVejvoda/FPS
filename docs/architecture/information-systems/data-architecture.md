# Data Architecture

FairSpot uses service-owned writes with event-fed read models. DataHub is the target durable store for cross-service reads; it is not the command-side source of truth.

## Data Ownership

| Data Area | Write Owner | Classification | Target Direction | Status |
| --- | --- | --- | --- | --- |
| Booking requests, allocations, Draw attempts, penalties, usage/no-show state | Booking | Confidential | Service-owned command state; events published for projections and audit. | Partial |
| Tenant lifecycle, readiness, identity setup metadata, support contacts | Customer | Confidential / Internal | Durable tenant state required before customer-ready hosted pilot. | Placeholder |
| Employee profile and vehicle facts | Profile | Confidential | Tenant-scoped minimal facts; default vehicle and eligibility visible through approved APIs/events. | Partial |
| Policy, locations, time slots, capacity/resource maps, zones | Configuration | Internal / Confidential | Versioned publication with history for Draw decisions and audit explanation. | Partial |
| Notifications, preferences, delivery status | Notification | Confidential | Operational notifications remain mandatory where required; message bodies should not be copied into DataHub. | Partial |
| Audit records, evidence references, PII mapping | Audit | Confidential / Restricted evidence | Append-only records; PII mapping restricted to authorized audit/admin flows. | Partial |
| Cross-service read models, projection checkpoints, event inbox | DataHub | Depends on projection | PostgreSQL-backed event-fed projections with tenant-scoped indexes and idempotent inbox. | Placeholder |
| Report catalog/configuration metadata | Reporting, if retained | Internal / Confidential | Stable report identifiers, filters, visibility rules, export formats, and availability flags only. | Deferred |
| Technical telemetry | Observability stack | Internal | Logs, metrics, and traces are operator evidence, not business audit. | Partial |

## Target Rules

- Writes stay with the owning business service.
- Cross-service operational reads are projected into DataHub.
- Tenant data must be isolated by authenticated/service context.
- Audit evidence must remain privacy-aware and append-only where required.
- DataHub may rebuild projections from events and must expose projection health, lag, and rebuild state.
- DataHub consumers must be idempotent by source event ID.
- Reporting must not store booking outcome projections, event inbox rows, projection checkpoints, rebuild state, or cross-service query models.

## Initial DataHub Projections

| Projection | Source Events | Read Uses | Boundary |
| --- | --- | --- | --- |
| Tenant readiness summary | Customer, Configuration, Profile, Notification, Audit reference events. | Tenant admin readiness workspace, go-live checklist, hosted smoke readiness. | Does not mutate Customer, Configuration, or Profile. |
| Booking outcome summary | Booking lifecycle events. | My Spots summaries where approved, HR queue counts, demand and lifecycle summaries. | Does not replace Booking-owned commands or expose raw lottery internals. |
| Draw status snapshot | Booking Draw events and lifecycle events. | HR Draw status, next/completed Draw dashboard, allocation explanation summaries. | Does not trigger or retry Draw. |
| Parking operational metrics | Booking lifecycle plus Configuration capacity/policy events. | Demand, utilization, allocation, rejection, cancellation, no-show, expiry dashboards and exports. | Reporting may use approved views only. |
| Notification delivery summary | Notification delivery events. | Tenant readiness and support diagnostics. | Stores status/counts only, not message body. |
| Audit reference index | Audit record-created references. | Links from HR/admin/report views to authorized audit evidence. | Audit remains source of truth for evidence and PII mapping. |

## Visible Data Gaps

- Customer durable tenant storage is required before customer-ready deployment.
- DataHub event inbox, projection checkpoints, and projection health are target architecture but not complete.
- Reporting/PostgreSQL projection work is obsolete unless re-scoped into DataHub or report catalog/configuration.
- Data retention and anonymisation rules need to be aligned with security/privacy architecture.

## Source Evidence

- [DataHub](/application-layer/datahub)
- [Security Model](/security/security-model)
- [Tenant Storage Contract](/production/tenant-storage-contract)
