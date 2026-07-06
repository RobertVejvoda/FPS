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

## Dapr Hardening Evidence

The table below records what is in place and what remains a gap for the NAS hosted profile. Component YAML files are in `code/infrastructure/dapr/components/{profile}/`.

| Capability | NAS / Local profile status | Evidence | Gap / Next step |
| --- | --- | --- | --- |
| Component scopes | **Partial** — local/demo/client state, pub/sub, binding, and scheduler components carry `scopes:` lists for intended app IDs. Secret-store components and the smoke-test in-memory pub/sub remain unscoped. | `local/bookingstore.yaml`, `local/fairspot-pubsub.yaml` (scopes: fairspot-booking, fairspot-notification, fairspot-audit, fairspot-reporting), demo/client state and pub/sub components. | Decide whether secret-store component scoping is required for the hosted/NAS profile; keep smoke-test profile explicitly non-production. |
| Secret references | **Done** — all credential-sensitive metadata fields use `secretKeyRef`; no inline passwords or tokens in demo or client profiles. Local dev vault uses a dev-only token, documented as non-production. | `local/vault.yaml` (dev-only token comment), `demo/vault-demo.yaml` (env-injected token), `client/bookingstore.yaml`, `client/fairspot-pubsub.yaml`. | Replace local dev vault token with injected secret before customer traffic. |
| mTLS / Sentry | **Gap** — `code/infrastructure/dapr/configuration/fairspot-config.yaml` has `mtls.enabled: false`. Comments document that demo and client profiles should enable mTLS when Dapr sidecar injection is managed by the platform. | `code/infrastructure/dapr/configuration/fairspot-config.yaml` | Enable mTLS in the NAS profile once the deployment moves to a Dapr-managed runtime boundary (platform-hosted Sentry or self-hosted Sentry). Accepted limitation for local NAS pilot. |
| Resiliency policies | **Gap** — no Dapr `Resiliency` YAML files exist in any profile. Retry, timeout, and circuit-breaker behavior is not yet declared. | None. | Add a `resiliency.yaml` per profile covering state store, pub/sub, and service invocation targets before customer-facing load. |
| State encryption | **Gap** — no `encryption` block in any state component YAML. MongoDB Atlas and managed stores provide infrastructure encryption at rest. Dapr-layer encryption is not configured. | None. | Evaluate Dapr state encryption support for MongoDB provider; use infrastructure encryption at rest as accepted fallback where Dapr encryption is not supported. |
| Transactional outbox | **Gap** — Dapr pub/sub publish currently used directly in command handlers. Dapr transactional outbox with MongoDB requires Dapr state store transaction support; this has not been validated. | [Dapr-First Standards](/production/dapr-first-production-standards) — outbox direction documented. | Validate MongoDB state store transaction support or implement service-owned pending-event outbox with deterministic event IDs before customer traffic. |
| Secret scoping | **Partial** — `auth.secretStore: secretstore` is present on all components that use `secretKeyRef`. No per-component secret scope restriction is configured. | `local/bookingstore.yaml`, `local/fairspot-pubsub.yaml`, `demo/` equivalents. | Add Dapr secret scope configuration if fine-grained secret access restriction is required for the NAS profile. |

## Visible Runtime Gaps

- Secret-store component scoping is not yet proven for the hosted/NAS profile; smoke-test components remain non-production evidence only.
- mTLS/Sentry is disabled in the current NAS/local profile; accepted limitation until a managed runtime boundary is in place.
- Resiliency policies (retry, timeout, circuit-breaker) are not defined; add before customer-facing load.
- Dapr state encryption is not configured; infrastructure encryption at rest is the current accepted fallback.
- Transactional outbox is not validated; must be resolved or replaced by service-owned outbox before customer traffic.
- DataHub PostgreSQL read-model store is target architecture but not customer-ready yet.
- Backup/restore drills and RTO/RPO evidence are not complete for the hosted profile.
- Resource-map publication persistence and DataHub impact-preview projections need implementation evidence.
- Scheduled jobs beyond Draw need their own deterministic key and duplicate-execution rules.
