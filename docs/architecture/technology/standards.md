# Technology Standards

Technology standards define approved architecture choices. Implementation may deviate only through a recorded decision, review, or waiver.

## Approved Standards

| Standard ID | Area | Approved Standard | Version / Profile | Applies To | Deviation Rule | Owner | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| STD-001 | API | HTTP APIs must have generated or source-of-truth contracts and compatibility expectations. | Current generated clients / OpenAPI where available | Public and internal service APIs | Breaking behavior needs architecture review and contract update. | Codex/Product Owner | Draft |
| STD-002 | Events | Events must have stable names, versioning, tenant context, owner, replay expectations, and idempotency rules. | Dapr pub/sub profile | Booking, Audit, Notification, DataHub, future projections | Missing ownership or tenant context requires a gap or waiver. | Codex / Claude | Draft |
| STD-003 | Runtime | Dapr building blocks are preferred for workflow, pub/sub, service invocation, state integration, secrets, resiliency, and outbox where suitable. | Dapr-first production standard | Service runtime and background workflows | Direct integration needs decision, rationale, and review. | Codex/Product Owner | Draft |
| STD-004 | Ingress | Public endpoints must use the approved ingress and WAF profile. | NAS/Cloudflare hosted profile | Web, API, auth callback, public demo domain | Public bypass requires a time-boxed waiver. | Robert / Security reviewer | Draft |
| STD-005 | Observability | Services expose health, structured logs, metrics, and trace correlation according to deployment profile. | OpenTelemetry profile | Services, DataHub, workflows, scheduled work | Missing hosted evidence belongs in readiness or risk register. | Codex / Platform owner | Draft |
| STD-006 | Persistence | Authoritative state is service-owned and tenant-scoped; in-memory stores are not production-ready authoritative storage. | Dapr state store / service-owned storage profile | Customer, Booking, Configuration, Profile, DataHub, Audit, Notification | Any production in-memory authoritative state remains a gap. | Codex/Product Owner | Draft |
| STD-007 | Security | Secrets are injected through approved runtime mechanisms and never committed to source. | Dapr secret store / deployment profile | Services, CI, deployment components | Secret exposure requires rotation and incident handling. | Robert / Security reviewer | Draft |

## Source Evidence

- [Dapr-First Production Standards](/production/dapr-first-production-standards)
- [NAS Cloudflare Deployment Profile](/production/nas-cloudflare-deployment-profile)
- [Runtime Platform](/architecture/technology/runtime-platform)
- [Observability](/architecture/technology/observability)
