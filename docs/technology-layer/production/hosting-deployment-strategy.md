# OPS000 Hosting and Deployment Strategy

**Status:** Proposal — not yet approved as a durable architecture decision.
Approval by Robert Vejvoda and/or Codex is required before OPS001 begins.

**Prepared by:** Claude (FPS Implementer), 2026-05-14
**Supersedes:** `azure-setup.md` and `aws-setup.md` cost tables (those files remain for reference but their stack assumptions are stale).

---

## Executive Recommendation

**Adopt Azure Container Apps (Consumption plan) as the first production target.**

Azure Container Apps (ACA) is the only option in this comparison that provides native, managed Dapr integration — no control-plane installation, no version-pinning scripts, no sidecar-injector management. The Consumption plan scales to zero, has a generous free tier, and maps cleanly to GitHub Actions → Azure Container Registry → Container Apps. At FPS's current scale (early SaaS, single maintainer), the operational overhead of running AKS is unjustified.

This is a proposal. The open questions in [section 8](#8-open-questions-requiring-approval) must be answered before OPS001 begins.

---

## 1. Comparison Table

| Criterion | **Azure Container Apps** (Consumption) | **Azure Kubernetes Service** (Dapr ext.) | **Hybrid stepping stone** | **Non-Azure / portable** |
|---|---|---|---|---|
| **Dapr support** | Native — all building blocks: service invocation, pub/sub, state, bindings, secrets, workflows | Managed extension — same Dapr building blocks; requires CRD/control-plane maintenance | ACA or Fargate for APIs; local Dapr for dev | Full Dapr compatibility; cloud-provider pieces must be replaced per target |
| **Managed identity** | Yes — Azure MI for Key Vault, Service Bus, ACR, etc. | Yes — but requires Workload Identity setup on AKS | Partial — depends on chosen managed container service | Not applicable; external IdP/OIDC required per cloud |
| **Cost at small scale** | ~$0–30/month compute (free tier covers light traffic); dominated by data service costs | ~$35–50/month base (1 node) + data services; no scale-to-zero on node | Lowest ACA + deferred services; $0 for idle dev/staging | Cloud-specific; comparable to ACA if self-managed Redis/RabbitMQ |
| **Cost growth** | Linear with vCPU-seconds consumed; Dedicated profiles available when steady-load justifies | More predictable at high steady load; per-node billing | Scales with chosen services | Scales with hosting choice |
| **Operational complexity** | Low — Microsoft manages Dapr control plane, TLS, scaling, and health | High — CRD lifecycle, Dapr extension versions, manual upgrade strategy | Low initially; deferred complexity | Medium — need Kubernetes or equivalent for orchestration |
| **Multi-tenancy** | Collection-per-tenant MongoDB fits; no ACA constraints | Same | Same | Same |
| **CI/CD shape** | GitHub Actions → ACR → `az containerapp update` | GitHub Actions → ACR → `kubectl apply` or Helm | Mixed | Cloud-specific |
| **Observability** | Azure Monitor + Container Apps built-in; can export to self-hosted Grafana/Loki | Full Prometheus/Grafana/Loki/Jaeger stack on cluster | Mix | Full self-hosted stack; no vendor lock-in |
| **GDPR / data residency** | Azure region selection; EU regions available | Azure region selection | Mixed | Cloud/region-specific |
| **Vendor lock-in** | Medium — compute abstracted by Dapr; Azure Service Bus, Key Vault, ACR are Azure-specific | Medium — same Azure services; more portable compute layer | Low initially | Low — Dapr component swap is the portability mechanism |
| **Rollback safety** | Revision-based traffic splitting; instant rollback to prior revision | Helm/kubectl rollback; more manual | Mixed | Depends on orchestrator |
| **Time to first deploy** | Low — ACA environments spin up quickly | High — AKS cluster, Dapr extension, networking setup | Low for ACA portion | Medium |

---

## 2. Option Detail

### 2.1 Azure Container Apps — Consumption Plan

**How Dapr works:** ACA has first-class Dapr support. Enable Dapr per app via CLI, Bicep, or portal. Sidecar is injected automatically. Changes do not create new revisions; existing replicas restart. API logging available for debugging.

**Plan types:**
- **Consumption** — serverless, scale-to-zero, charged per vCPU-second and GiB-second. Free tier: 180,000 vCPU-seconds, 360,000 GiB-seconds, 2M HTTP requests/month per subscription. No charge at zero replicas.
- **Dedicated (Workload Profiles)** — fixed management fee per profile instance; better for sustained, predictable load. Migrate to this tier when tenant count grows.

**Managed identity:** User-assigned or system-assigned MI recommended for all Azure service connections (Key Vault, Service Bus, ACR). Eliminates secrets in Dapr component definitions. `azureClientId` metadata field required for user-assigned MI.

**Limitations:**
- Keycloak requires a persistent container with a volume or external DB — not scale-to-zero friendly. Use a dedicated Consumption instance with minimum replicas = 1, or move Keycloak to a small VM/App Service.
- Self-hosted observability stack (Prometheus/Grafana/Loki/Jaeger) needs separate hosting. Azure Monitor/Log Analytics can substitute for early production; full stack can be deferred.
- RabbitMQ (current dev pub/sub) should be replaced by Azure Service Bus in production. Dapr pub/sub component swap is the mechanism — no application code changes.

### 2.2 Azure Kubernetes Service — Dapr Extension

AKS gives full Kubernetes control: custom networking, Helm releases, full observability stack. The Dapr extension handles operator/sidecar-injector/placement/sentry installation.

**Operational overhead is high for a single maintainer:**
- AKS control plane auto-upgraded by Microsoft, but node pool and Dapr extension versions require manual management.
- Dapr extension: rolling window support (current + previous version only). Auto-upgrade available but not recommended for production.
- CRDs remain after extension deletion and must be manually cleaned.
- Baseline node cost: ~$35–50/month for a Standard_B2s node (1 vCPU, 2 GB) even at zero traffic.

**Recommended only if:** workload profiles in ACA prove insufficient, or full Kubernetes-native Helm deployment path is required for enterprise customer compliance.

### 2.3 Hybrid / Minimal-Cost Stepping Stone

Keep local Docker Compose + .NET Aspire for development. Deploy only the externally useful API surface (Identity + Booking) to ACA Consumption first. Defer Notification, Audit, Reporting, and Configuration to a later deploy cycle.

This is the lowest-risk path to a first live endpoint. The main downside is deferred integration testing between services in a real cloud environment.

### 2.4 Non-Azure / Portable Dapr Baseline

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

## 3. Recommended First Production Target

**Phase 1: Azure Container Apps, Consumption plan, single environment.**

Deploy:
- Identity service (Keycloak + FPS.Identity) — minimum 1 replica
- Booking service
- Profile service
- Notification service
- Audit service
- Configuration service

Backed by:
- Azure Container Registry (Basic tier, ~$5/month)
- Azure Service Bus Standard (~$10/month) — replaces dev RabbitMQ as Dapr pub/sub component
- MongoDB Atlas M10 (~$57/month) or Azure Cosmos DB for MongoDB API (~$25/month at 400 RU/s) — see open question #1
- Azure Cache for Redis C0 (~$16/month) — replaces dev Redis
- Azure Key Vault Standard (~$4/month)

**Defer to later:**
- Reporting service (no consumer yet)
- Billing service (stub only)
- Full Prometheus/Grafana/Loki/Jaeger stack (use Azure Monitor / Log Analytics initially)
- Traefik (ACA has built-in ingress with HTTPS)
- MinIO (no production object storage use yet)

**Estimated monthly cost at small scale:**
| Component | Estimated cost |
|---|---|
| Azure Container Apps (Consumption) | $0–20 (free tier covers light traffic) |
| Azure Service Bus Standard | ~$10 |
| MongoDB (Cosmos DB for MongoDB, 400 RU/s, 10 GB) | ~$30 |
| Azure Cache for Redis C0 | ~$16 |
| Azure Key Vault | ~$4 |
| Azure Container Registry Basic | ~$5 |
| Log Analytics / Azure Monitor | ~$5–15 |
| **Total estimate** | **~$70–100/month** |

This replaces the stale estimate of ~$162.76/month from `azure-setup.md`. That estimate used AKS, Cosmos DB Postgres, App Gateway, Load Balancer, SignalR, and APIM — none of which are in the current stack.

> **Note:** Exact current pricing must be verified at [azure.microsoft.com/pricing/details/container-apps](https://azure.microsoft.com/en-us/pricing/details/container-apps/) and the respective service pages before committing to a budget.

---

## 4. Minimum Viable Deployment Architecture

```
GitHub Actions
    │
    ├─ Build + test (.NET 10, npm)
    ├─ docker build + push → Azure Container Registry
    └─ az containerapp update (per service)

Azure Container Apps Environment
    ├─ [fps-identity]     Dapr enabled, min 1 replica, ingress external
    ├─ [fps-booking]      Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-profile]      Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-notification] Dapr enabled, scale-to-zero, ingress internal
    ├─ [fps-audit]        Dapr enabled, scale-to-zero, ingress internal
    └─ [fps-configuration]Dapr enabled, scale-to-zero, ingress internal

External ingress (ACA built-in, HTTPS/TLS managed)
    └─ fps-identity (public) — employee OIDC + API auth entry point

Dapr components (scoped per app)
    ├─ pubsub: Azure Service Bus
    ├─ statestore: MongoDB (Cosmos DB for MongoDB API or Atlas)
    ├─ secretstore: Azure Key Vault
    └─ (future) bindings: Azure Storage / Service Bus input bindings

Managed identity
    └─ User-assigned MI → ACR pull, Service Bus, Key Vault, MongoDB (if Cosmos DB)

Data
    ├─ MongoDB: collection-per-tenant per service
    └─ Azure Cache for Redis (Dapr state store or direct session cache)

Observability (Phase 1, minimal)
    └─ Azure Log Analytics (ACA native integration)
        (Phase 2: add self-hosted Prometheus + Grafana in ACA or VM)
```

Keycloak deployment note: Keycloak cannot scale to zero (it holds session/realm state). Options:
- Run Keycloak in ACA Consumption with `minReplicas: 1` (incurs idle-rate charges, ~$5–10/month at minimum size).
- Use Azure App Service Basic B1 (~$13/month) for Keycloak — simpler lifecycle management.
- Use a managed OIDC provider (Entra External ID, Auth0) — eliminates Keycloak operational burden but requires evaluating cost and GDPR fit. See open question #2.

---

## 5. Dapr Component Mapping

| Building block | Local (dev) | Staging (ACA) | Production (ACA) |
|---|---|---|---|
| **pub/sub** | RabbitMQ (`localhost:5672`) | Azure Service Bus Standard | Azure Service Bus Standard |
| **state store** | MongoDB (`localhost:27017`) | MongoDB Atlas (free cluster) or Cosmos DB for MongoDB | MongoDB Atlas M10 or Cosmos DB provisioned |
| **secrets** | Local file / `.env` | Azure Key Vault (dev vault, lower access policy) | Azure Key Vault (prod vault, MI, restricted policy) |
| **bindings (cron)** | Dapr local scheduler | Dapr cron binding on ACA | Dapr cron binding on ACA |
| **bindings (input)** | File / local HTTP | Azure Service Bus input binding | Azure Service Bus input binding |
| **service invocation** | Dapr sidecar (`localhost:3500`) | ACA Dapr sidecar | ACA Dapr sidecar |
| **mTLS / Sentry** | Local Dapr self-hosted | ACA-managed (built-in) | ACA-managed (built-in) |
| **Dapr Workflows** | Local Dapr (1.14+) | ACA Dapr (1.14+) | ACA Dapr (1.14+) |

Component YAML files live in `code/infrastructure/dapr/components/`. Local files use `scopes: []` (all apps). Production files add `scopes: [fps-booking]` per building block and use `secretStoreComponent: fps-keyvault` instead of inline values.

---

## 6. Cost-Control Notes

- **Scale-to-zero is the primary cost lever** at this scale. All internal services (booking, profile, audit, notification, configuration) should use `minReplicas: 0`. Only Identity/Keycloak needs `minReplicas: 1` due to session state.
- **Azure Service Bus Basic tier** (~$0.05/million operations) is sufficient for dev/test; Standard tier required for Topics (Dapr pub/sub uses topics). Standard adds ~$10/month base.
- **MongoDB cost is the dominant variable.** Cosmos DB for MongoDB serverless is an option for very low traffic (~$0.25/million RU); provisioned throughput makes more sense as tenants grow. MongoDB Atlas M0 (free, 512 MB) is sufficient for staging but not production. Atlas M10 (~$57/month) is the lowest paid tier with dedicated resources.
- **Avoid** deploying the full observability stack (Prometheus, Grafana, Loki, Jaeger) in ACA for production Phase 1. Azure Monitor + Log Analytics provides sufficient production visibility at ~$5–15/month. Migrate to self-hosted stack when operational maturity justifies it.
- **Azure Container Registry Basic** (~$5/month) is sufficient; move to Standard only if geo-replication is needed.
- **Defer AKS** until monthly tenant count or sustained traffic justifies dedicated node costs and full Kubernetes control.

---

## 7. Security and GDPR Implications

### 7.1 Data Residency

ACA supports EU regions (West Europe, North Europe, Sweden Central, Germany West Central). All data services (MongoDB/Cosmos DB, Service Bus, Key Vault, Redis) must be provisioned in the **same region** to satisfy GDPR data residency. Cross-region replication must not move personal data (booking requests, profiles, notifications) to non-EU regions without explicit DPA coverage.

### 7.2 Managed Identity

ACA Consumption supports user-assigned managed identity. All Dapr component connections to Azure services (Key Vault, Service Bus, ACR) **must** use managed identity — no connection strings or API keys in component YAML or environment variables. `azureClientId` metadata field required for user-assigned MI.

### 7.3 Secrets

- Vault (HashiCorp) is the documented secret store target. ACA integration with Vault requires network reach and a Vault Dapr component. For Phase 1, Azure Key Vault via Dapr secretstore component with managed identity is simpler and consistent with Azure-native deployment.
- No secrets in source control, container images, or Dapr component YAML (use secretstore reference pattern).
- GitHub Actions secrets for ACR credentials and ACA deployment tokens are the CI boundary.

### 7.4 Private Networking

ACA Consumption environments support VNet integration (requires Consumption-only environment in custom VNet). Phase 1 can use the default shared environment — internal ingress services (booking, profile, etc.) are not publicly reachable. Phase 2 should add VNet integration and private MongoDB endpoint if Cosmos DB is chosen.

### 7.5 TLS

ACA manages TLS certificates for all ingress — no manual cert management for HTTPS. Custom domain + managed cert available. Internal service-to-service traffic over Dapr sidecar uses mTLS managed by ACA's built-in Sentry.

### 7.6 GDPR Audit Trail

Pseudonymised audit records (actor_hash) as per the existing architecture decision are stored in MongoDB. The `PiiMapping` collection (hash→identity) must be in an EU region with restricted access. On GDPR erasure: delete PiiMapping row only; audit log remains immutable and anonymous.

---

## 8. Open Questions Requiring Approval

The following questions need answers from Robert and/or Codex before OPS001 begins:

1. **MongoDB hosting choice**: Cosmos DB for MongoDB API (stays in Azure, MI supported, may need provisioned RU tuning) vs MongoDB Atlas (multi-cloud, no MI, requires Atlas credentials in Key Vault, better for non-Azure portability)? The Dapr statestore component changes slightly between them.

2. **Keycloak hosting**: Self-hosted on ACA (operational burden, min-replica cost) vs managed OIDC (Azure Entra External ID, Auth0)? If self-hosted, which ACA plan and persistence backend (Azure DB for PostgreSQL vs embedded H2/dev)?

3. **Observability Phase 1**: Accept Azure Monitor / Log Analytics for Phase 1, or deploy self-hosted Prometheus + Grafana from the start? Azure Monitor reduces initial operational burden but diverges from the documented architecture.

4. **Region selection**: Which Azure EU region? Recommendation: West Europe (Amsterdam) or Sweden Central (Stockholm) for GDPR fit, but confirm with Robert.

5. **Vault vs Azure Key Vault**: The documented stack uses HashiCorp Vault. For Phase 1 ACA, Azure Key Vault is operationally simpler and MI-compatible. Is it acceptable to use Azure Key Vault for Phase 1 and re-evaluate Vault for production hardening in OPS001?

6. **Environment topology**: Single environment (dev/staging/production all in one subscription with separate ACA environments) vs multi-subscription? Recommendation: single subscription, three ACA environments (`fps-dev`, `fps-staging`, `fps-prod`) for Phase 1.

7. **OPS000 approval**: Should the Azure Container Apps recommendation be recorded as a durable decision in `versions-and-decisions.md`? Holding until Robert approves.

---

## 9. Follow-up Implementation Slices

| Slice | Scope | Depends on |
|---|---|---|
| **OPS001** Local/Production Dapr Hardening | Align local Docker Compose Dapr components with production ACA component YAML. Add tenant collection/index provisioning scripts. Configure Key Vault (or Vault) secretstore. Write first operational runbook. | OPS000 approved (hosting target confirmed) |
| **OPS002** CI/CD Pipeline to ACA | GitHub Actions workflow: build → ACR push → `az containerapp update` per service. Staging environment deploy on PR merge. Production deploy on tag. | OPS001 (components configured) |
| **OPS003** Observability Stack | Deploy Prometheus + Grafana + Loki + Jaeger (self-hosted on ACA or dedicated VM). Wire Dapr metrics and service traces. Connect to existing Grafana dashboard placeholders. | OPS002 (deployed services to instrument) |
| **OPS004** Keycloak Production Setup | Production Keycloak realm, tenant claim mapper, PKCE client for mobile app, user provisioning. | OPS001 (secrets, database) |

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
