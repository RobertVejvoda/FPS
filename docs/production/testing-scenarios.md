# Production Testing Scenarios

These scenarios prove that the hosted environment can run FPS safely. They should be executed for the first cloud baseline and repeated after major infrastructure changes.

## Deployment Scenarios

| Scenario | Expected evidence |
| --- | --- |
| Deploy clean environment from CI/CD | Services, Dapr components, ingress, identity config, databases, broker, cache, and secrets are provisioned without manual server edits. |
| Deploy application update | New image/version rolls out, health checks pass, and rollback path is known. |
| Roll back failed deployment | Previous known-good version is restored and validated. |
| Rotate a non-production secret | Secret is updated through the secret store and services continue or restart cleanly. |

## Functional Smoke Scenarios

| Scenario | Expected evidence |
| --- | --- |
| Login through hosted identity provider | JWT contains required tenant, user, and role claims. |
| Submit booking request | Booking writes to the correct tenant collection and returns employee-safe response. |
| Run or simulate Draw | Allocation rules execute and produce auditable events. |
| Cancel booking | Cancellation state change persists and notification/audit consumers receive events. |
| Confirm usage | Usage confirmation persists and appears in employee history. |
| Read notifications | Notification history, unread count, and mark-read actions work. |
| Query audit as authorized role | Auditor can query permitted records; unauthorized roles fail closed. |

## Failure Scenarios

| Scenario | Expected evidence |
| --- | --- |
| Broker temporarily unavailable | Booking state remains consistent; consumers catch up or expose retry/failure evidence. |
| Notification provider unavailable | Booking is not rolled back; failed delivery is traceable. |
| Identity provider unavailable | New login fails safely; no unauthenticated fallback exists. |
| MongoDB unavailable | Affected service fails safely and recovers after database availability returns. |
| Dapr sidecar/component failure | Health checks expose failure and service does not silently bypass required integration. |
| Secret missing or invalid | Service fails startup or operation safely without logging secret values. |

## Recovery Scenarios

| Scenario | Expected evidence |
| --- | --- |
| Restore full environment backup | Core smoke tests pass after restore. |
| Restore one tenant's collections to temporary database | Tenant data can be inspected and selectively restored without cross-tenant overwrite. |
| Rebuild reporting projections | Reporting catches up from source data/events. |
| Recreate indexes | Tenant collections regain required indexes and uniqueness constraints. |

## Security And Operations Scenarios

| Scenario | Expected evidence |
| --- | --- |
| Access denied without role | API returns forbidden/unauthorized and records appropriate security/audit evidence. |
| Tenant isolation test | User from one tenant cannot access another tenant's data by changing request input. |
| Logs reviewed for sensitive content | Logs do not contain tokens, secrets, raw confidential payloads, or hidden allocation internals. |
| Alert route test | A synthetic alert reaches the configured operator channel. |
| Dashboard review | Core service, database, broker, Dapr, and ingress health are visible. |
