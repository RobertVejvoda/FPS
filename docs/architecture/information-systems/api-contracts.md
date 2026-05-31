# API Contracts

API contracts should be generated or documented close to the owning service. This page records architecture expectations, generated contract evidence, and missing contract surfaces.

## Generated Contract Evidence

The following services expose OpenAPI endpoints and have generated TypeScript clients maintained by `tools/generate-api-client.sh`. The stale-client CI check fails if generated files diverge from the running service.

| Service | OpenAPI spec (source of truth) | Generated TypeScript client |
| --- | --- | --- |
| Booking | `code/clients/typescript/openapi/booking.json` | `code/clients/typescript/src/booking.d.ts` |
| Identity | `code/clients/typescript/openapi/identity.json` | `code/clients/typescript/src/identity.d.ts` |
| Notification | `code/clients/typescript/openapi/notification.json` | `code/clients/typescript/src/notification.d.ts` |
| Profile | `code/clients/typescript/openapi/profile.json` | `code/clients/typescript/src/profile.d.ts` |

Audit, Configuration, and Customer expose OpenAPI but do not yet have generated TypeScript clients in `code/clients/typescript/`. HR, DataHub, and Reporting have no generated contract yet.

## Contract Index

| API Area | Owner | Contract Location | Status | Compatibility Notes |
| --- | --- | --- | --- | --- |
| Booking employee APIs | Booking | `code/clients/typescript/openapi/booking.json`; [Booking API Contract](/business-layer/booking-api-contract) (narrative) | Generated | Must derive tenant/user from authenticated context. Own-bookings reads must not expose other employees. |
| Draw operations APIs | Booking | `code/clients/typescript/openapi/booking.json`; [Booking API Contract](/business-layer/booking-api-contract) (narrative) | Generated | Admin/controlled operations only; idempotency required; employees cannot trigger Draw. |
| Profile APIs | Profile | `code/clients/typescript/openapi/profile.json` | Generated | Employee-safe profile/default vehicle facts and HR/admin facts must stay separated. |
| Notification APIs | Notification | `code/clients/typescript/openapi/notification.json` | Generated | Notification history, unread counts, mark-read, SSE stream, delivery summaries. |
| Identity APIs | Identity | `code/clients/typescript/openapi/identity.json` | Generated | Auth context establishment only; services must not trust client-supplied identity claims. |
| Audit APIs | Audit | Service OpenAPI (no generated client yet) | Partial | Auditor/admin authorization required; PII mapping lookups require reason and audit trail. |
| Configuration APIs | Configuration | Service OpenAPI (no generated client yet) | Partial | Policy/location/slot/resource-map admin surfaces, publication validation, closures, capabilities, and effective version history. |
| HR operations APIs | Booking / DataHub | Booking OpenAPI (partial); DataHub query contracts not yet defined | Placeholder | Tenant/location-scoped request queues, safe request lookup, lifecycle explanation, next Draw status, controlled Draw, and cancellation with reason. |
| Customer APIs | Customer | Service OpenAPI where available; durable state gap remains | Placeholder | Durable tenant state and readiness API gap remains. |
| DataHub APIs | DataHub | Not yet defined | Placeholder | Projection ownership, privacy shape, query filters, pagination, impact-preview inputs, sponsor summaries, and export boundaries must be explicit. |
| Reporting APIs | Reporting, if retained | Not yet defined | Deferred | Should expose report metadata and approved DataHub-backed report surfaces only. |

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

- Generated OpenAPI specs: `code/clients/typescript/openapi/` (Booking, Identity, Notification, Profile)
- Generated TypeScript clients: `code/clients/typescript/src/` (Booking, Identity, Notification, Profile)
- [Booking API Contract](/business-layer/booking-api-contract) — narrative contract; generated spec is authoritative
- [API client stale check](/tooling)
