# Availability Model

FPS availability is based on graceful degradation. Booking state changes are authoritative; observer services such as Notification, Audit, and Reporting should catch up from events or retry paths without rolling back Booking outcomes.

## Availability Goals

| Capability | Availability expectation | Degraded behavior |
| --- | --- | --- |
| Employee login | Required for normal use. | If identity is unavailable, users cannot start new sessions; existing sessions continue until token expiry if validation allows it. |
| Booking commands | Core product path. | If required policy/profile data is unavailable, commands fail safely instead of accepting invalid requests. |
| Booking reads | Core employee trust path. | If read model is stale, show last known state with clear refresh/error behavior where UI supports it. |
| Draw execution | Critical scheduled operation. | Missed or failed Draw requires alert, retry, and operator-visible state. |
| Notification | Important but not authoritative. | Notification failures must not roll back Booking; failed delivery is retryable and traceable. |
| Audit | Required compliance evidence. | Auditable mutations must have reliable audit persistence or fail before mutation where reliable audit cannot be guaranteed. |
| Reporting | Operational insight. | Reporting can lag and rebuild from events/read models. |
| Admin configuration | Required for policy changes. | Existing published policy remains active if admin UI/API is unavailable. |

## Failure Boundaries

- A single service failure should not take down unrelated services.
- A broker outage should queue or retry event publication where supported; services must tolerate duplicate events.
- A database outage for a service affects that service's reads/writes but should not corrupt other service-owned data.
- Identity outage blocks new authentication but must not permit unauthenticated fallback.
- Observability outage reduces visibility but must not alter product state.
- Secret-store outage may block startup or rotation; running services should not log or expose cached secrets.

## Availability Dependencies

| Dependency | Used by | Production control |
| --- | --- | --- |
| Identity provider | Mobile, web, APIs | Health checks, backup config, documented recovery, token/key rotation plan. |
| MongoDB | Service persistence/read models | Backups, indexes, tenant collection provisioning, restore drills. |
| RabbitMQ via Dapr pub/sub | Notification, Audit, Reporting | Durable queues where needed, dead-letter/retry strategy, duplicate handling. |
| Dapr sidecars/components | Service invocation, state, pub/sub, secrets | Version pinning, component validation, health probes. |
| Ingress/TLS | External access | Certificate renewal, routing tests, WAF/rate limits where selected. |
| Secret store | Runtime credentials | Access audit, backup/recovery, rotation runbook. |

## Design Principles

- Fail closed for missing identity, tenant, role, policy, or security context.
- Prefer idempotent commands and consumers.
- Keep event consumers independently retryable.
- Keep tenant isolation intact during failover and restore.
- Treat manual recovery as an auditable operational action.
