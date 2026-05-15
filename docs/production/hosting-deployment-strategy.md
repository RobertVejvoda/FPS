# OPS000 Deployment Profile Strategy

**Status:** Baseline merged; provider-specific production choice remains client/environment dependent.

**Prepared by:** Claude (FPS Implementer), 2026-05-14
**Updated by:** Codex (FPS Product Owner), 2026-05-15
**Supersedes:** `azure-setup.md` and `aws-setup.md` cost tables for planning purposes. Those files remain reference material, but their stack assumptions are stale.

---

## Executive Recommendation

FPS should keep the runtime **pluggable by deployment profile** rather than choosing a single production provider owned by FPS.

The product needs three practical targets:

| Profile | Recommendation | Why |
|---|---|---|
| **Local** | Docker Compose or local containers with self-hosted Dapr components. | Lowest cost, fast feedback, and close enough to production contracts for development. |
| **Demo** | A low-cost hosted environment with managed container runtime and replaceable Dapr components. Azure Container Apps remains a strong candidate because of native Dapr support, but the demo provider is not yet a durable architecture decision. | Lets business and technical evaluators try a real system and lets FPS collect usage/performance evidence. |
| **Client-owned production** | Client-selected cloud or platform, constrained by FPS component contracts, Dapr building blocks, OpenTelemetry telemetry, and documented backup/restore/security requirements. | Production operation belongs to the client. FPS should deliver deployable artifacts, configuration guidance, runbooks, and evidence rather than operate the client's environment. |

Dapr remains the boundary for pub/sub, state, bindings, service invocation, and secrets. OpenTelemetry remains the boundary for logs, metrics, and traces. Provider-specific services are allowed only behind those boundaries or in clearly isolated deployment scripts.

---

## 1. Deployment Profile Comparison

| Criterion | **Local** | **Demo hosted environment** | **Client-owned production** | **Kubernetes / enterprise option** |
|---|---|---|---|---|
| **Dapr support** | Self-hosted sidecars and local component YAML. | Prefer managed Dapr where available; otherwise self-hosted Dapr sidecars. | Must support Dapr components or a documented equivalent adapter path. | Strong fit when client requires Kubernetes control. |
| **Identity integration** | Local Keycloak or mocked OIDC. | Demo OIDC realm with seeded users and roles. | Client IdP through OIDC/OAuth 2.0, tenant and role claims mapped explicitly. | Enterprise OIDC, workload identity, private networking. |
| **Cost role** | Minimal developer cost. | Low monthly spend; enough to run credible demos and measurements. | Client-owned cost model; FPS supplies sizing assumptions and measurement method. | Higher baseline cost, justified only by client controls or steady load. |
| **Operational complexity** | Low. | Medium; enough automation to redeploy repeatably. | Depends on client platform and controls. | High; useful when enterprise deployment standards require it. |
| **Multi-tenancy** | Collection-per-tenant MongoDB in service-owned databases. | Same, with seeded demo tenants and visible admin flows. | Same, with client-approved naming, backup, retention, and access controls. | Same. |
| **CI/CD shape** | Local scripts and validation. | Build, deploy, seed, smoke test, collect evidence. | Deliverable pipeline template or client-integrated release process. | Helm/Kubernetes manifests or client platform equivalent. |
| **Observability** | Prometheus/Grafana plus local traces. | Dashboards for usage, latency, errors, background processing, and demo evidence. | OpenTelemetry export to client tooling such as Dynatrace, Azure Monitor, Grafana, Splunk, or equivalent. | Full platform-native telemetry stack. |
| **GDPR / data residency** | Synthetic or local developer data. | Demo data only unless a DPA-approved pilot exists. | Client-owned region, retention, backup, DPA, and access model. | Client-specific. |
| **Vendor lock-in** | Low. | Controlled; provider choices are replaceable behind Dapr. | Controlled by client environment. | Depends on client platform. |
| **Time to first useful environment** | Immediate. | Short after OPS001/OPS002. | Depends on client onboarding and security review. | Longer. |

---

## 2. Profile Detail

### 2.1 Local Development

Local development should remain cheap, repeatable, and close to production contracts:

- .NET services run locally or in containers.
- Dapr sidecars use local component YAML.
- MongoDB, RabbitMQ, Redis, Vault-compatible secrets, and object storage run through local infrastructure where feasible.
- Prometheus/Grafana provide local metrics dashboards.
- Local tracing should use OpenTelemetry export to Jaeger or an equivalent collector.
- Demo and production component files must not require code changes in application services.

### 2.2 Demo Hosted Environment

The demo environment is not production. Its job is to prove the product story and collect evidence:

- seeded tenants, roles, employees, parking slots, policies, and booking history;
- working mobile/web/API flows for the target evaluator roles;
- repeatable deployment from source artifacts;
- basic backup/restore rehearsal;
- observable usage, latency, error rate, event processing, and notification delivery;
- clear teardown and cost control.

Azure Container Apps remains a good candidate because it has native Dapr support and can be low-cost at light traffic, but OPS002 should validate alternatives before locking in a demo provider. Alternatives can include a small Kubernetes distribution, a client-provided sandbox, or another managed container platform if it supports the Dapr and OpenTelemetry boundaries.

### 2.3 Client-Owned Production

Client production is operated by the client or the client's hosting partner. FPS should provide:

- container images or build instructions;
- Dapr component contracts for pub/sub, state, bindings, secrets, and service invocation;
- OpenTelemetry instrumentation and exporter configuration guidance;
- identity claim mapping requirements;
- collection-per-tenant provisioning and index guidance;
- backup, restore, incident, retention, and access-control runbooks;
- sizing assumptions and performance/usage evidence from demo or staging.

The exact provider choice is a client architecture decision. FPS should remain compatible with Azure, AWS, Kubernetes, and equivalent platforms by keeping provider-specific code outside the application services.

### 2.4 Azure Container Apps — Demo Candidate

**How Dapr works:** ACA has first-class Dapr support. Enable Dapr per app via CLI, Bicep, or portal. Sidecar is injected automatically. API logging is available for debugging.

**Plan types:**
- **Consumption** — serverless, scale-to-zero, charged per vCPU-second and GiB-second. Free tier: 180,000 vCPU-seconds, 360,000 GiB-seconds, 2M HTTP requests/month per subscription. No charge at zero replicas.
- **Dedicated (Workload Profiles)** — fixed management fee per profile instance; better for sustained, predictable load. Migrate to this tier when tenant count grows.

**Managed identity:** User-assigned or system-assigned MI recommended for all Azure service connections (Key Vault, Service Bus, ACR). Eliminates secrets in Dapr component definitions. `azureClientId` metadata field required for user-assigned MI.

**Limitations:**
- Keycloak requires a persistent container with a volume or external DB — not scale-to-zero friendly. Use a dedicated Consumption instance with minimum replicas = 1, or move Keycloak to a small VM/App Service.
- Self-hosted observability stack (Prometheus/Grafana/Loki/Jaeger) needs separate hosting. Azure Monitor/Log Analytics can substitute for early production; full stack can be deferred.
- RabbitMQ (current dev pub/sub) should be replaced by Azure Service Bus in production. Dapr pub/sub component swap is the mechanism — no application code changes.

### 2.5 Azure Kubernetes Service — Enterprise Candidate

AKS gives full Kubernetes control: custom networking, Helm releases, full observability stack. The Dapr extension handles operator/sidecar-injector/placement/sentry installation.

**Operational overhead is high for a single maintainer:**
- AKS control plane auto-upgraded by Microsoft, but node pool and Dapr extension versions require manual management.
- Dapr extension: rolling window support (current + previous version only). Auto-upgrade available but not recommended for production.
- CRDs remain after extension deletion and must be manually cleaned.
- Baseline node cost: ~$35–50/month for a Standard_B2s node (1 vCPU, 2 GB) even at zero traffic.

**Recommended only if:** workload profiles in a managed container runtime prove insufficient, or full Kubernetes-native deployment is required for client compliance.

### 2.6 Hybrid / Minimal-Cost Stepping Stone

Keep local Docker Compose or .NET Aspire-style orchestration for development. Deploy only the externally useful API surface first. Defer non-essential services until the demo needs them.

This is the lowest-risk path to a first live endpoint. The main downside is deferred integration testing between services in a real cloud environment.

### 2.7 Non-Azure / Portable Dapr Baseline

Dapr components define the portability boundary. If FPS ever needs to move off Azure:

**Portable (Dapr component swap only):**
- Pub/sub: RabbitMQ → Azure Service Bus → AWS SNS/SQS → GCP Pub/Sub
- State store: MongoDB → Cosmos DB → DynamoDB → Redis (any Dapr-supported store)
- Secrets: Vault → Azure Key Vault → AWS Secrets Manager
- Bindings: Cron, HTTP → provider equivalents

**Azure-specific (must be re-implemented per cloud):**
- Container registry: ACR → ECR / GAR
- Managed identity: Azure MI → IRSA (AWS) / Workload Identity (GCP)
- Private networking / DNS: Azure Private DNS / VNet
- CI/CD deployment step: `az containerapp update` → cloud-specific equivalent
- Observability: Azure Monitor → CloudWatch / GCP Cloud Monitoring (or keep self-hosted)

The Dapr abstraction is real but not free — each component swap requires testing and a component YAML update. Azure-specific infra pieces require deeper changes.

---

## 3. Recommended Next Deployment Target

**Next target: demo environment baseline, not FPS-operated production.**

The next operational slice should produce a working demo environment with enough evidence for client evaluation. It should deploy:

- Identity service (Keycloak + FPS.Identity) — minimum 1 replica
- Booking service
- Profile service
- Notification service
- Audit service
- Configuration service
- Reporting service when dashboards or exports are part of the demo

Backed by:
- container registry;
- Dapr pub/sub component such as RabbitMQ, Azure Service Bus, AWS SNS/SQS, or equivalent;
- MongoDB-compatible storage with collection-per-tenant provisioning;
- Redis-compatible cache where needed;
- Dapr secret store backed by Vault, cloud key vault, or client-approved equivalent;
- OpenTelemetry collector/exporter path.

**Defer until there is a clear consumer or client requirement:**
- Billing service.
- Full enterprise-grade observability hosting if the demo can export telemetry to a managed service.
- Object storage unless reports/exports require it.
- Kubernetes unless a client or demo constraint requires it.

**Cost planning model:**
| Component | Demo cost expectation | Production cost ownership |
|---|---|
| Container hosting | Keep small and scale down outside demos where possible. | Client-owned platform and sizing. |
| Persistence | Use the lowest credible managed or self-hosted option that supports backup/restore evidence. | Client data platform, region, backup, and retention policy. |
| Pub/sub | Use a Dapr-compatible broker with visible message metrics. | Client-approved broker behind Dapr. |
| Secrets | Use a real secret store, not repository files. | Client secret-management platform. |
| Observability | Capture demo metrics/traces/logs at low cost. | Client observability platform, exported through OpenTelemetry. |

Exact provider pricing changes frequently. Treat any numeric cloud estimates as planning placeholders until OPS002 validates current prices against the selected demo provider and expected demo traffic.

---

## 4. Minimum Viable Deployment Architecture

```
GitHub Actions
    │
    ├─ Build + test (.NET 10, npm)
    ├─ docker build + push → selected container registry
    └─ deploy to selected runtime profile

Container Runtime
    ├─ [fps-identity]     Dapr enabled, min 1 replica, ingress external
    ├─ [fps-booking]      Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-profile]      Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-notification] Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-audit]        Dapr enabled, scale-to-zero, ingress internal
    └─ [fps-configuration]Dapr enabled, scale-to-zero, ingress internal

External ingress
    └─ HTTPS/TLS managed by platform or ingress gateway

Dapr components (scoped per app)
    ├─ pubsub: local broker, managed broker, or client broker
    ├─ statestore: MongoDB-compatible store
    ├─ secretstore: Vault, cloud key vault, or client secret store
    └─ bindings: cron, object storage, broker input, or provider equivalent

Workload identity / credentials
    └─ Runtime identity or secret-store references for registry, broker, database, and secret access

Data
    ├─ MongoDB: collection-per-tenant per service
    └─ Redis-compatible cache where required

Observability
    ├─ OpenTelemetry metrics, logs, and traces from services
    ├─ Local: Prometheus/Grafana and local tracing
    ├─ Demo: low-cost dashboard and evidence collection
    └─ Client production: export to client platform such as Dynatrace, Azure Monitor, Grafana, Splunk, or equivalent
```

Keycloak deployment note: Keycloak cannot scale to zero safely when it owns live session and realm state. Demo may use a small always-on Keycloak instance. Client production may use Keycloak or a client-managed OIDC provider, provided FPS receives the required tenant, user, and role claims.

---

## 5. Dapr Component Mapping

| Building block | Local | Demo | Client-owned production |
|---|---|---|---|
| **pub/sub** | RabbitMQ | Dapr-compatible low-cost broker | Client-approved broker such as RabbitMQ, Azure Service Bus, SNS/SQS, Kafka, or equivalent |
| **state store** | MongoDB | MongoDB-compatible managed or hosted option | Client-approved MongoDB-compatible store with collection-per-tenant provisioning |
| **secrets** | Local secret store; no committed secrets | Vault or cloud key vault | Client secret-management platform |
| **bindings (cron)** | Dapr local scheduler | Dapr cron binding or platform scheduler | Client-approved scheduler behind Dapr or equivalent |
| **bindings (input)** | File/local HTTP where useful | Broker or object-storage binding | Client-approved broker/object-storage binding |
| **service invocation** | Dapr sidecar | Managed or self-hosted Dapr sidecar | Managed or self-hosted Dapr sidecar |
| **mTLS / Sentry** | Local Dapr self-hosted | Runtime-managed or self-hosted Dapr mTLS | Client platform Dapr mTLS policy |
| **Dapr Workflows** | Local Dapr 1.14+ where needed | Dapr workflow support if selected slice needs it | Client-supported Dapr workflow runtime or an approved alternative |

Component YAML files live in `code/infrastructure/dapr/components/`. Local files may be broad for developer convenience. Demo and production files should scope components per app and use secret-store references instead of inline credentials.

---

## 6. Cost-Control Notes

- **Separate demo cost from production cost.** FPS should estimate and control the demo bill. Client production cost belongs to the client's hosting and operations model.
- **Scale-to-zero is useful for demo.** Internal services can scale down when idle if the selected runtime supports it. Identity may need an always-on instance.
- **MongoDB-compatible storage is usually the dominant variable.** OPS002 should validate cost against expected tenant count, data volume, backups, and query/reporting load.
- **Use managed services only where they reduce delivery risk.** A demo can use managed broker/secrets/monitoring to save time, but application code must stay behind Dapr and OpenTelemetry boundaries.
- **Avoid Kubernetes by default for demo.** Use it only when the client target or technical validation requires Kubernetes behavior.
- **Verify current prices before sharing numbers externally.** Cloud pricing changes frequently; docs should show the cost model and assumptions, not pretend one estimate is final.

---

## 7. Security and GDPR Implications

### 7.1 Data Residency

ACA supports EU regions (West Europe, North Europe, Sweden Central, Germany West Central). All data services (MongoDB/Cosmos DB, Service Bus, Key Vault, Redis) must be provisioned in the **same region** to satisfy GDPR data residency. Cross-region replication must not move personal data (booking requests, profiles, notifications) to non-EU regions without explicit DPA coverage.

### 7.2 Workload Identity

Where the runtime supports workload identity, Dapr component connections should use it instead of connection strings. The exact mechanism is provider-specific: Azure managed identity, AWS IAM Roles for Service Accounts, Kubernetes workload identity, or a client-approved equivalent. When workload identity is unavailable, credentials must come from the configured secret store and must not be committed to source control or embedded in container images.

### 7.3 Secrets

- Vault (HashiCorp) is the documented secret store target. ACA integration with Vault requires network reach and a Vault Dapr component. For Phase 1, Azure Key Vault via Dapr secretstore component with managed identity is simpler and consistent with Azure-native deployment.
- No secrets in source control, container images, or Dapr component YAML (use secretstore reference pattern).
- GitHub Actions secrets for ACR credentials and ACA deployment tokens are the CI boundary.

### 7.4 Private Networking

Demo can use a simpler network boundary if no real personal data is present. Client production should place internal services, persistence, broker, cache, and secret store behind private networking according to the client's platform standards.

### 7.5 TLS

External ingress must use HTTPS. Certificate ownership depends on the deployment profile: demo can use platform-managed certificates; client production should use client-approved certificate management. Internal service-to-service traffic should use Dapr mTLS where the runtime supports it.

### 7.6 GDPR Audit Trail

Pseudonymised audit records (actor_hash) as per the existing architecture decision are stored in MongoDB. The `PiiMapping` collection (hash→identity) must be in an EU region with restricted access. On GDPR erasure: delete PiiMapping row only; audit log remains immutable and anonymous.

---

## 8. Open Questions Requiring Approval

The following questions need answers from Robert and/or Codex before OPS001 begins:

1. **Demo provider choice**: Which environment should OPS002 target first: Azure Container Apps, another low-cost managed container runtime, a lightweight Kubernetes environment, or a client-provided sandbox?

2. **MongoDB hosting choice for demo**: Which option gives credible backup/restore and reporting evidence without creating unnecessary monthly cost?

3. **Keycloak hosting for demo**: Self-hosted Keycloak vs managed OIDC? For client production, confirm that FPS supports client OIDC as long as claims are mapped.

4. **Observability evidence target**: What dashboards and measurements must exist before a client demo: usage counts, latency, error rate, event backlog, notification delivery, draw duration, and audit query performance?

5. **Client telemetry integrations**: Which examples should FPS document first: Dynatrace, Azure Monitor, Grafana/Prometheus, Splunk, or generic OpenTelemetry Collector?

6. **Secrets target**: Should demo use Vault, a provider key vault, or another low-cost secret store while keeping the Dapr secret-store boundary stable?

7. **External client material**: Which package should be prepared first: one-page business summary, demo script, architecture pack, production operations pack, security/GDPR pack, or cost assumptions sheet?

---

## 9. Follow-up Implementation Slices

| Slice | Scope | Depends on |
|---|---|---|
| **OPS001** Pluggable Dapr Component Baseline | Align local component files with demo/client component contracts. Add tenant collection/index provisioning guidance. Configure secret-store pattern. Write first operational runbook. | OPS000 baseline |
| **OPS002** Demo Environment Baseline | Select and deploy a low-cost hosted demo profile. Build, deploy, seed, smoke test, and collect cost/usage evidence. | OPS001 |
| **OPS003** Client-Owned Production Integration | Document client deployment responsibilities, identity integration, network/security assumptions, backup/restore handoff, and release process. | OPS001, OPS002 evidence |
| **OPS004** Observability And Performance Evidence | Wire OpenTelemetry metrics/logs/traces, local Prometheus/Grafana, demo dashboards, and client exporter examples such as Dynatrace. | OPS002 |
| **DOCS001** Client Evaluation Pack | Prepare business summary, demo script, architecture overview, production operations summary, security/GDPR summary, cost assumptions, and FAQ. | Demo plan and current architecture docs |

These slices have clear boundaries: each has no product behavior changes and each can be reviewed and validated independently.

---

## 10. Stale Documents

The following documents contain outdated stack assumptions and should be reviewed after OPS000 is approved:

- `docs/technology-layer/production/azure-setup.md` — references AKS, Cosmos DB Postgres, SignalR, App Gateway, Load Balancer, APIM, Azure DevOps. The current stack uses none of these.
- `docs/technology-layer/production/aws-setup.md` — references EKS/Fargate, DynamoDB, RDS, SNS. AWS is not the primary hosting target and this doc has no recent decisions behind it.

**Recommendation:** Keep both files as historical references. Do not delete until OPS000 is approved and `hosting-deployment-strategy.md` is accepted as the authoritative document. Robert/Codex should explicitly approve removal.

---

*Sources:*
- [Azure Container Apps — Enable Dapr](https://learn.microsoft.com/en-us/azure/container-apps/enable-dapr)
- [Azure Container Apps — Plan types](https://learn.microsoft.com/en-us/azure/container-apps/plans)
- [Azure Container Apps — Billing](https://learn.microsoft.com/en-us/azure/container-apps/billing)
- [Azure Container Apps — Dapr component connect services](https://learn.microsoft.com/en-us/azure/container-apps/dapr-component-connect-services)
- [AKS Dapr extension](https://learn.microsoft.com/en-us/azure/aks/dapr)
- [Azure Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)
- [Dapr components concept](https://docs.dapr.io/concepts/components-concept/)
- [Dapr bindings overview](https://docs.dapr.io/developing-applications/building-blocks/bindings/bindings-overview/)
