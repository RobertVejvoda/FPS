# Runtime Platform

| Technology Area | Target Direction | Rationale | Source Evidence |
| --- | --- | --- | --- |
| Runtime | .NET services, React web, Expo/React Native mobile. | Existing stack and client portability. | [Technology Layer](/technology-layer) |
| Distributed application building blocks | Dapr-first for workflow, pub/sub, state, secrets, resiliency, service identity, and outbox where supported. | Production-grade proof point and portability. | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Persistence | Service-owned persistence; DataHub PostgreSQL projections for cross-service reads. | Clear ownership and CQRS read model separation. | [DataHub](/application-layer/datahub) |
| Ingress | Gateway/WAF profile appropriate to deployment environment. | Public surfaces must be controlled and observable. | [Cloudflare WAF Profile](/security/cloudflare-waf-profile) |
| Secrets | Dapr secret stores and profile-specific secret providers. | Avoid committed or logged secrets. | [Security Model](/security/security-model) |
