# Runtime Platform

FairSpot is Dapr-first by design. Dapr is a production-grade runtime boundary, not only local development glue.

| Technology Area | Target Direction | Status | Rationale | Source Evidence |
| --- | --- | --- | --- | --- |
| Service runtime | .NET services, React web, Expo/React Native mobile. | Partial | Existing stack and client portability. | [Technology Layer](/technology-layer) |
| Distributed application building blocks | Dapr-first for workflow, pub/sub, state, secrets, resiliency, service identity, component scoping, and outbox where supported. | Partial | Production-grade proof point and portability across NAS, demo, and client profiles. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Workflow/orchestration | Dapr Workflow for Draw and other long-running/retryable business processes that need progress evidence. | Partial | Manual and scheduled Draw must converge into one safe execution path. | [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Scheduled work | Dapr cron binding, Dapr Jobs, or platform scheduler may trigger workflows only through deterministic keys and idempotent acquisition. | Placeholder | Multiple container instances must not run the same Draw or job three times. | [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Event transport | Dapr pub/sub over selected broker/provider. | Partial | Application code remains provider-neutral; consumers assume at-least-once delivery. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| State and outbox | Service-owned state through Dapr state store or persistence adapter; Dapr transactional outbox where supported, otherwise service-owned pending-event outbox. | Placeholder | State changes and business events must be recoverable after process crash. | [DataHub](/application-layer/datahub) |
| Read-model persistence | DataHub PostgreSQL projections for cross-service reads. | Placeholder | Clear ownership and CQRS read model separation. | [Data Architecture](/architecture/information-systems/data-architecture) |
| Resource-map publication persistence | Configuration stores draft/published policy and capacity versions; Booking consumes only published compatible versions; DataHub projects publication summaries. | Placeholder | Resource changes must survive restart and not affect allocation until validated/published. | [Information Systems](/architecture/information-systems/), [Business Policies](/architecture/business/policies) |
| Ingress | Gateway/WAF profile appropriate to deployment environment, with Cloudflare for NAS hosted pilot. | Partial | Public surfaces must be controlled and observable. | [Cloudflare WAF Profile](/security/cloudflare-waf-profile), [NAS Cloudflare Deployment Profile](/production/nas-cloudflare-deployment-profile) |
| Secrets | Dapr secret stores and profile-specific secret providers. | Partial | Avoid committed, logged, or inline secrets. | [Security Model](/security/security-model) |
| Service-to-service security | Dapr mTLS/Sentry where supported, plus application-level tenant and role authorization. | Placeholder | Runtime identity does not replace business authorization. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Resiliency | Dapr resiliency policies for state stores, pub/sub, service invocation, and workflow dependencies. | Placeholder | Retry, timeout, and circuit-breaker behavior should be profile-visible. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Encryption | Dapr state encryption where supported, plus store-managed encryption at rest. | Placeholder | Use Dapr security capabilities when available. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Backup and restore | Service-owned stores, DataHub projections, Dapr component manifests, identity config, secret metadata, object storage, and observability config have documented backup/restore expectations. | Placeholder | Customer-ready operation requires tested restore evidence, not only scheduled backups. | [Backup And Restore](/production/backup-restore), [RTO/RPO Requirements](/production/rto-rpo-requirements) |

## Runtime Rules

- Use Dapr-native capabilities before custom infrastructure when they fit the requirement.
- Business authorization, tenant isolation, input validation, and privacy filtering remain in application code.
- Dapr sidecar APIs must never be exposed publicly.
- Component YAML/configuration must be profile-specific and scoped per service outside local convenience profiles.
- Duplicate event delivery, workflow starts, scheduler ticks, and activity retries are normal conditions.
- UI progress for Draw must come from persisted workflow/lifecycle state, not process memory.
- Backup/restore evidence must prove the selected profile can recover authoritative state before reopening writes.
- Derived state such as DataHub projections can be rebuilt, but projection rebuild health and lag must be visible.
- Runtime maintenance commands and provider-specific procedures belong in `production/` runbooks, not in core architecture pages.

## Visible Runtime Gaps

- Dapr outbox support must be validated for the selected state store or replaced by explicit service-owned outbox records.
- Component scopes, mTLS/Sentry, resiliency policies, and state encryption need hosted-profile evidence.
- DataHub PostgreSQL read-model store is target architecture but not customer-ready yet.
- Backup/restore drills and RTO/RPO evidence are not complete for the hosted profile.
- Resource-map publication persistence and DataHub impact-preview projections need implementation evidence.
- Scheduled jobs beyond Draw need their own deterministic key and duplicate-execution rules.
