# API Contracts

API contracts should be generated or documented close to the owning service. This page records architecture expectations and missing contract surfaces.

| API Area | Owner | Contract Location | Status | Compatibility Notes |
| --- | --- | --- | --- | --- |
| Booking employee APIs | Booking | OpenAPI/generated TypeScript client; [Booking API Contract](/business-layer/booking-api-contract) | Partial | Must derive tenant/user from authenticated context. Own-bookings reads must not expose other employees. |
| Draw operations APIs | Booking | OpenAPI/generated TypeScript client; [Booking API Contract](/business-layer/booking-api-contract) | Partial | Admin/controlled operations only; idempotency required; employees cannot trigger Draw. |
| HR operations APIs | Booking / DataHub | Booking API contract and future DataHub query contracts. | Placeholder | Tenant/location-scoped request queues, safe request lookup, lifecycle explanation, next Draw status, controlled Draw, and cancellation with reason. |
| Profile APIs | Profile | OpenAPI/generated TypeScript client | Partial | Employee-safe profile/default vehicle facts and HR/admin facts must stay separated. |
| Notification APIs | Notification | OpenAPI/generated TypeScript client | Partial | Notification history, unread counts, mark-read, SSE stream, delivery summaries. |
| Audit APIs | Audit | OpenAPI/generated TypeScript client | Partial | Auditor/admin authorization required; PII mapping lookups require reason and audit trail. |
| Configuration APIs | Configuration | OpenAPI/generated TypeScript client | Partial | Policy/location/slot/resource-map admin surfaces, publication validation, closures, capabilities, and effective version history. |
| Customer APIs | Customer | Service API docs/OpenAPI where available | Placeholder | Durable tenant state and readiness API gap remains. |
| DataHub APIs | DataHub | Future query contracts | Placeholder | Projection ownership, privacy shape, query filters, pagination, impact-preview inputs, sponsor summaries, and export boundaries must be explicit. |
| Reporting APIs | Reporting, if retained | Future report catalog/configuration contract | Deferred | Should expose report metadata and approved DataHub-backed report surfaces only. |

## Contract Rules

- Authenticated commands must derive tenant, actor, roles, and ownership from the authenticated context.
- Request bodies and query strings must not be trusted for tenant ID, authenticated actor ID, actor role, or ownership.
- Employee-safe APIs must not expose lottery seed, candidate order, hidden weights, stack traces, audit-only diagnostics, or unrelated employee-private data.
- Privileged APIs must still be tenant-scoped and auditable.
- Retried commands must be idempotent for the same business outcome.
- List APIs must use safe pagination and avoid counts that reveal other users' data unless the caller is authorized for aggregate/tenant-wide views.
- API changes that affect web/mobile generated clients must update generated TypeScript clients and pass the stale-client check.

## Required Contract Placeholders

| Contract | Need |
| --- | --- |
| HR request queue query | Tenant/location/date/time-slot filters, status filters, safe employee reference, current status, safe reason, next HR action. |
| HR request lifecycle lookup | Safe request reference or authorized employee display search, lifecycle status, policy snapshot summary, employee-safe explanation, audit reference links where authorized, and no raw Draw seed/order/weights. |
| Draw status query | Configured cut-off, next scheduled Draw run time, policy timezone, request-window status, schedule source, lifecycle status, counts where authorized, timestamps. |
| HR cancellation command | Request/allocation ID from route, authenticated tenant/actor, required human-readable reason, notification/audit side effects. |
| Tenant readiness query | Customer, Identity, Configuration, Profile, Booking, Notification, Audit, DataHub readiness checks and blocking reasons. |
| Resource-map publication command/query | Draft/published versions, validation result, closures, capability counts, effective time, publication actor, reason where required, and audit reference. |
| Policy/capacity impact preview query | Draft or proposed policy/capacity input, projected shortage/utilization/capability impact where reliable, warning that preview is advisory, and no allocation side effects. |
| DataHub projection health query | Projection name, tenant scope, lag, last event, last processed timestamp, failed/poison events, rebuild state. |
| Sponsor management summary query | Tenant/location/date-range filters, demand, allocation rate, unmet demand, utilization, fairness trend, no-show/cancellation trend, capacity pressure, and aggregate-only default shape. |
| Report catalog query | Report identifiers, display names, allowed filters, visibility rules, export formats, availability flags. |

## Source Evidence

- [API client stale check](/tooling)
- [Booking API Contract](/business-layer/booking-api-contract)
