# API Contracts

| API Area | Owner | Contract Location | Compatibility Notes |
| --- | --- | --- | --- |
| Booking employee APIs | Booking | OpenAPI/generated TypeScript client | Must derive tenant/user from authenticated context. |
| Draw operations APIs | Booking | OpenAPI/generated TypeScript client | Admin/controlled operations only; idempotency required. |
| Profile APIs | Profile | OpenAPI/generated TypeScript client | Employee-safe profile facts and HR/admin facts must stay separated. |
| Notification APIs | Notification | OpenAPI/generated TypeScript client | Notification history, unread counts, mark-read, SSE stream. |
| Audit APIs | Audit | OpenAPI/generated TypeScript client | Auditor/admin authorization required. |
| Configuration APIs | Configuration | OpenAPI/generated TypeScript client | Policy/location/slot admin surfaces. |
| Customer APIs | Customer | Service API docs/OpenAPI where available | Durable tenant state gap remains. |
| DataHub APIs | DataHub | Future query contracts | Projection ownership and privacy shape must be explicit. |

## Source Evidence

- [API client stale check](/tooling)
- [Booking API Contract](/business-layer/booking-api-contract)
