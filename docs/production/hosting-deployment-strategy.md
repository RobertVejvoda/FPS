# OPS000 Deployment Profile Strategy

> **Private-later (#670):** hosted-platform-operator runbook — planned to move to the private `fairspot-platform` repository. This slice only classifies it; the [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary.md) tracks the public summary/replacement that will accompany the move. Nothing is moved or deleted here.

**Status:** Baseline merged; provider-specific production choice remains client/environment dependent.

**Prepared by:** Claude (FairSpot Implementer), 2026-05-14
**Updated by:** Codex (FairSpot Product Owner), 2026-05-15
**Supersedes:** `azure-setup.md` and `aws-setup.md` cost tables for planning purposes. Those files remain reference material, but their stack assumptions are stale.

---

## Executive Recommendation

FairSpot should keep the runtime **pluggable by deployment profile** rather than choosing a single production provider owned by FairSpot.

The product needs three practical targets:

| Profile | Recommendation | Why |
|---|---|---|
| **Local** | Docker Compose or local containers with self-hosted Dapr components. | Lowest cost, fast feedback, and close enough to production contracts for development. |
| **Demo** | A low-cost hosted environment with managed container runtime and replaceable Dapr components. Azure Container Apps remains a strong candidate because of native Dapr support, but the demo provider is not yet a durable architecture decision. | Lets business and technical evaluators try a real system and lets FairSpot collect usage/performance evidence. |
| **Client-owned production** | Client-selected cloud or platform, constrained by FairSpot component contracts, Dapr building blocks, OpenTelemetry telemetry, and documented backup/restore/security requirements. | Production operation belongs to the client. FairSpot should deliver deployable artifacts, configuration guidance, runbooks, and evidence rather than operate the client's environment. |

Dapr remains the boundary for pub/sub, state, bindings, service invocation, and secrets. OpenTelemetry remains the boundary for logs, metrics, and traces. Provider-specific services are allowed only behind those boundaries or in clearly isolated deployment scripts.

---

## 1. Deployment Profile Comparison

| Criterion | **Local** | **Demo hosted environment** | **Client-owned production** | **Kubernetes / enterprise option** |
|---|---|---|---|---|
| **Dapr support** | Self-hosted sidecars and local component YAML. | Prefer managed Dapr where available; otherwise self-hosted Dapr sidecars. | Must support Dapr components or a documented equivalent adapter path. | Strong fit when client requires Kubernetes control. |
| **Identity integration** | Local or mocked OIDC. | Demo OIDC realm with seeded users and roles. | Client IdP through OIDC/OAuth 2.0, tenant and role claims mapped explicitly. | Enterprise OIDC, workload identity, private networking. |
| **Cost role** | Minimal developer cost. | Low monthly spend; enough to run credible demos and measurements. | Client-owned cost model; FairSpot supplies sizing assumptions and measurement method. | Higher baseline cost, justified only by client controls or steady load. |
| **Operational complexity** | Low. | Medium; enough automation to redeploy repeatably. | Depends on client platform and controls. | High; useful when enterprise deployment standards require it. |
| **Multi-tenancy** | Tenant-scoped storage boundaries inside service-owned stores. | Same, with seeded demo tenants and visible admin flows. | Same, with client-approved naming, backup, retention, and access controls. | Same. |
| **CI/CD shape** | Local scripts and validation. | Build, deploy, seed, smoke test, collect evidence. | Deliverable pipeline template or client-integrated release process. | Profile-specific manifests or client platform equivalent. |
| **Observability** | Local OpenTelemetry-compatible dashboards/traces. | Dashboards for usage, latency, errors, background processing, and demo evidence. | OpenTelemetry export to client-approved observability tooling. | Full platform-native telemetry stack. |
| **GDPR / data residency** | Synthetic or local developer data. | Demo data only unless a DPA-approved pilot exists. | Client-owned region, retention, backup, DPA, and access model. | Client-specific. |
| **Vendor lock-in** | Low. | Controlled; provider choices are replaceable behind Dapr. | Controlled by client environment. | Depends on client platform. |
| **Time to first useful environment** | Immediate. | Short after OPS001/OPS002. | Depends on client onboarding and security review. | Longer. |

---

## 2. Profile Detail

### 2.1 Local Development

Local development should remain cheap, repeatable, and close to production contracts:

- .NET services run locally or in containers.
- Dapr sidecars use local component YAML.
- Local equivalents for persistence, broker, cache, secrets, and object storage run through local infrastructure where feasible.
- Local dashboards provide metrics/logs/traces for service-level debugging.
- Local tracing should use OpenTelemetry export to a local collector/backend.
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

Client production is operated by the client or the client's hosting partner. FairSpot should provide:

- container images or build instructions;
- Dapr component contracts for pub/sub, state, bindings, secrets, and service invocation;
- OpenTelemetry instrumentation and exporter configuration guidance;
- identity claim mapping requirements;
- tenant storage provisioning and index guidance;
- backup, restore, incident, retention, and access-control runbooks;
- sizing assumptions and performance/usage evidence from demo or staging.

The exact provider choice is a client architecture decision. FairSpot should remain compatible with Azure, AWS, Kubernetes, and equivalent platforms by keeping provider-specific code outside the application services.

### 2.4 Azure Container Apps — Demo Candidate

**How Dapr works:** ACA has first-class Dapr support. Enable Dapr per app via CLI, Bicep, or portal. Sidecar is injected automatically. API logging is available for debugging.

**Plan types:**
- **Consumption** — serverless, scale-to-zero, charged per vCPU-second and GiB-second. Free tier: 180,000 vCPU-seconds, 360,000 GiB-seconds, 2M HTTP requests/month per subscription. No charge at zero replicas.
- **Dedicated (Workload Profiles)** — fixed management fee per profile instance; better for sustained, predictable load. Migrate to this tier when tenant count grows.

**Managed identity:** User-assigned or system-assigned MI recommended for all Azure service connections (Key Vault, Service Bus, ACR). Eliminates secrets in Dapr component definitions. `azureClientId` metadata field required for user-assigned MI.

**Limitations:**
- A self-hosted identity provider requires persistent runtime and storage, so it is not scale-to-zero friendly. Use an always-on identity component or a managed/client IdP.
- A self-hosted observability stack needs separate hosting. Azure-native monitoring can substitute for early Azure demo/production evidence where approved.
- The local pub/sub broker should be replaced by an Azure-approved event service in Azure production. Dapr pub/sub component swap is the mechanism — no application code changes.

### 2.5 Azure Kubernetes Service — Enterprise Candidate

AKS gives full Kubernetes control: custom networking, Helm releases, full observability stack. The Dapr extension handles operator/sidecar-injector/placement/sentry installation.

**Operational overhead is high for a single maintainer:**
- AKS control plane auto-upgraded by Microsoft, but node pool and Dapr extension versions require manual management.
- Dapr extension: rolling window support (current + previous version only). Auto-upgrade available but not recommended for production.
- CRDs remain after extension deletion and must be manually cleaned.
- Baseline node cost: ~$35–50/month for a Standard_B2s node (1 vCPU, 2 GB) even at zero traffic.

**Recommended only if:** workload profiles in a managed container runtime prove insufficient, or full Kubernetes-native deployment is required for client compliance.

### 2.6 Hybrid / Minimal-Cost Stepping Stone

Keep local Docker Compose and repository-owned harness scripts for development. Deploy only the externally useful API surface first. Defer non-essential services until the demo needs them.

This is the lowest-risk path to a first live endpoint. The main downside is deferred integration testing between services in a real cloud environment.

### 2.7 Non-Azure / Portable Dapr Baseline

Dapr components define the portability boundary. If FairSpot ever needs to move off Azure:

**Portable (Dapr component swap only):**
- Pub/sub: local broker → Azure Service Bus → AWS SNS/SQS → GCP Pub/Sub or approved equivalent
- State store: local document/operational store → managed document/operational store supported by the selected profile
- Secrets: local secret store → Azure Key Vault → AWS Secrets Manager or approved equivalent
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

**Next target: demo environment baseline, not FairSpot-operated production.**

The next operational slice should produce a working demo environment with enough evidence for client evaluation. It should deploy:

- Identity service / OIDC provider integration — minimum 1 replica where self-hosted
- Booking service
- Profile service
- Notification service
- Audit service
- Configuration service
- Reporting service when dashboards or exports are part of the demo

Backed by:
- container registry;
- Dapr pub/sub component backed by the selected broker/provider;
- service-owned persistence with tenant-scoped provisioning;
- cache/session store where needed;
- Dapr secret store backed by the selected secret-management platform;
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

OPS002 is now specified in [Demo Environment Baseline](./demo-environment-baseline). That page is the source of truth for demo scope, smoke checks, synthetic data, reset/teardown, and cost-evidence expectations.

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
    ├─ statestore: selected operational/document store
    ├─ secretstore: selected secret-management platform
    └─ bindings: cron, object storage, broker input, or provider equivalent

Workload identity / credentials
    └─ Runtime identity or secret-store references for registry, broker, database, and secret access

Data
    ├─ service-owned stores with tenant-scoped collections/partitions/keys
    └─ cache/session store where required

Observability
    ├─ OpenTelemetry metrics, logs, and traces from services
    ├─ Local: local dashboards and tracing
    ├─ Demo: low-cost dashboard and evidence collection
    └─ Client production: export to the client-approved observability platform
```

Identity deployment note: a self-hosted IdP usually cannot scale to zero safely when it owns live session and realm state. Demo may use a small always-on IdP instance or a managed/shared demo IdP. Client production may use any client-managed OIDC provider, provided FairSpot receives the required tenant, user, and role claims.

---

## 5. Dapr Component Mapping

| Building block | Local | Demo | Client-owned production |
|---|---|---|---|
| **pub/sub** | Local broker | Dapr-compatible low-cost broker/provider | Client-approved broker/provider behind Dapr |
| **state store** | Local document/operational store | Managed or hosted operational/document store | Client-approved store with tenant-scoped provisioning |
| **secrets** | Local secret store; no committed secrets | Demo secret-management platform | Client secret-management platform |
| **bindings (cron)** | Dapr local scheduler | Dapr cron binding or platform scheduler | Client-approved scheduler behind Dapr or equivalent |
| **bindings (input)** | File/local HTTP where useful | Broker or object-storage binding | Client-approved broker/object-storage binding |
| **service invocation** | Dapr sidecar | Managed or self-hosted Dapr sidecar | Managed or self-hosted Dapr sidecar |
| **mTLS / Sentry** | Local Dapr self-hosted | Runtime-managed or self-hosted Dapr mTLS | Client platform Dapr mTLS policy |
| **Dapr Workflows** | Local Dapr 1.14+ where needed | Dapr workflow support if selected slice needs it | Client-supported Dapr workflow runtime or an approved alternative |
| **Dapr outbox** | Enabled where the local state component supports transactions/outbox | Required for business state-plus-event flows where supported | Required or replaced by a documented service-owned pending-event outbox |
| **resiliency policies** | Local retry/timeout policies for smoke evidence | Demo retry, timeout, and circuit-breaker policies | Client-approved Dapr resiliency policies |
| **state encryption** | Optional local proof where supported | Enabled for confidential state where supported | Enabled where the selected component supports Dapr encryption, plus store-managed encryption at rest |
| **component scopes** | Broad only where needed for local convenience | Scoped per service | Scoped per service and least privilege |

Component YAML files live in `code/infrastructure/dapr/components/`. Local files may be broad for developer convenience. Demo and production files should scope components per app and use secret-store references instead of inline credentials.

---

## 6. Cost-Control Notes

- **Separate demo cost from production cost.** FairSpot should estimate and control the demo bill. Client production cost belongs to the client's hosting and operations model.
- **Scale-to-zero is useful for demo.** Internal services can scale down when idle if the selected runtime supports it. Identity may need an always-on instance.
- **Persistence is usually the dominant variable.** OPS002 should validate cost against expected tenant count, data volume, backups, restore needs, and query/reporting load.
- **Use managed services only where they reduce delivery risk.** A demo can use managed broker/secrets/monitoring to save time, but application code must stay behind Dapr and OpenTelemetry boundaries.
- **Avoid Kubernetes by default for demo.** Use it only when the client target or technical validation requires Kubernetes behavior.
- **Verify current prices before sharing numbers externally.** Cloud pricing changes frequently; docs should show the cost model and assumptions, not pretend one estimate is final.

---

## 7. Security and GDPR Implications

### 7.1 Data Residency

ACA supports EU regions (West Europe, North Europe, Sweden Central, Germany West Central). All selected Azure data services must be provisioned in the **same region** to satisfy GDPR data residency. Cross-region replication must not move personal data (booking requests, profiles, notifications) to non-EU regions without explicit DPA coverage.

### 7.2 Workload Identity

Where the runtime supports workload identity, Dapr component connections should use it instead of connection strings. The exact mechanism is provider-specific: Azure managed identity, AWS IAM Roles for Service Accounts, Kubernetes workload identity, or a client-approved equivalent. When workload identity is unavailable, credentials must come from the configured secret store and must not be committed to source control or embedded in container images.

### 7.3 Secrets

- Use the selected profile's secret-management service through the Dapr secret-store boundary. For an Azure-native demo, Azure Key Vault with managed identity is the simplest candidate.
- No secrets in source control, container images, or Dapr component YAML (use secretstore reference pattern).
- Use Dapr secret scopes and component scopes so services can access only the secrets/components they require.
- GitHub Actions secrets for ACR credentials and ACA deployment tokens are the CI boundary.

### 7.4 Private Networking

Demo can use a simpler network boundary if no real personal data is present. Client production should place internal services, persistence, broker, cache, and secret store behind private networking according to the client's platform standards.

### 7.5 TLS

External ingress must use HTTPS. Certificate ownership depends on the deployment profile: demo can use platform-managed certificates; client production should use client-approved certificate management. Internal service-to-service traffic should use Dapr mTLS where the runtime supports it.

### 7.6 Dapr Resiliency And Outbox

Business flows that persist state and publish integration events should use Dapr transactional outbox where the selected state store supports it. If not supported, the service must use a documented pending-event outbox with deterministic event IDs and retry behavior.

Dapr resiliency policies should define timeouts, retries, and circuit breakers for state stores, pub/sub, service invocation, and workflow dependencies. Production readiness evidence must show how the app behaves when these dependencies are slow, unavailable, or redeliver messages.

### 7.7 GDPR Audit Trail

Pseudonymised audit records (`actor_hash`) as per the existing architecture decision are stored in the Audit service data store. The PII mapping store (hash-to-identity) must be in an approved region with restricted access. On GDPR erasure: delete the mapping row only; audit log remains immutable and anonymous.

---

## 8. Open Questions Requiring Approval

The following questions should be resolved as OPS002 turns the demo baseline into a concrete hosted environment:

1. **Demo provider choice**: Which environment should OPS002 target first: Azure Container Apps, another low-cost managed container runtime, a lightweight Kubernetes environment, or a client-provided sandbox?

2. **Persistence hosting choice for demo**: Which option gives credible backup/restore and reporting evidence without creating unnecessary monthly cost?

3. **Identity hosting for demo**: Self-hosted OIDC provider vs managed/shared demo OIDC? For client production, confirm that FairSpot supports client OIDC as long as claims are mapped.

4. **Observability evidence target**: What dashboards and measurements must exist before a client demo: usage counts, latency, error rate, event backlog, notification delivery, draw duration, and audit query performance?

5. **Client telemetry integrations**: Which client observability integration examples should FairSpot document first, and should the generic OpenTelemetry Collector be the default handoff pattern?

6. **Secrets target**: Which low-cost secret-management service should demo use while keeping the Dapr secret-store boundary stable?

7. **External client material**: Which package should be prepared first: one-page business summary, demo script, architecture pack, production operations pack, security/GDPR pack, or cost assumptions sheet?

---

## 9. Follow-up Implementation Slices

| Slice | Scope | Depends on |
|---|---|---|
| **OPS001** Pluggable Dapr Component Baseline | Align local component files with demo/client component contracts. Add tenant storage/index provisioning guidance. Configure secret-store pattern. Write first operational runbook. | OPS000 baseline |
| **OPS002** Demo Environment Baseline | Define the low-cost hosted demo profile, required runtime components, synthetic data rules, smoke tests, reset/teardown path, and cost-evidence model. | OPS001 |
| **OPS003** Client-Owned Production Integration | Document client deployment responsibilities, identity integration, network/security assumptions, backup/restore handoff, and release process. | OPS001, OPS002 evidence |
| **OPS004** Observability And Performance Evidence | Wire OpenTelemetry metrics/logs/traces, local/demo dashboards, and client exporter examples through the selected observability backend. | OPS002 |
| **DOCS001** Client Evaluation Pack | Prepare business summary, demo script, architecture overview, production operations summary, security/GDPR summary, cost assumptions, and FAQ. | Demo plan and current architecture docs |

These slices have clear boundaries: each has no product behavior changes and each can be reviewed and validated independently.

---

## 10. Stale Documents

The following documents contain outdated stack assumptions and should be reviewed after OPS000 is approved:

- `docs/production/azure-setup.md` — contains Azure-specific cost/reference material that must not be treated as core architecture.
- `docs/production/aws-setup.md` — contains AWS-specific cost/reference material that must not be treated as core architecture.

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
