# OPS000 Deployment Profile Strategy

> **Private-later (#670):** hosted-platform-operator runbook — planned to move to the private `fairspot-platform` repository. This slice only classifies it; the [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary.md) tracks the public summary/replacement that will accompany the move. Nothing is moved or deleted here.

**Status:** Baseline merged; Release 1 uses NAS/Cloudflare and the FairSpot-operated cloud-hosted follow-up target is DigitalOcean.

**Prepared by:** Claude (FairSpot Implementer), 2026-05-14
**Updated by:** Codex (FairSpot Product Owner), 2026-05-15
**Supersedes:** `azure-setup.md` and `aws-setup.md` as active FairSpot target-cloud plans. Those files remain legacy stubs only; the active cloud target note is [DigitalOcean Setup](./digitalocean-setup).

---

## Executive Recommendation

FairSpot should keep the runtime **pluggable by deployment profile** while being explicit about the FairSpot-operated hosted path.

The product needs three practical targets:

| Profile | Recommendation | Why |
|---|---|---|
| **Local** | Docker Compose or local containers with self-hosted Dapr components. | Lowest cost, fast feedback, and close enough to production contracts for development. |
| **FairSpot-operated hosted evaluation** | Release 1 uses NAS/Cloudflare. The cloud-hosted follow-up target is DigitalOcean, starting with a Droplet/Docker Compose profile that mirrors the current container stack. DOKS is deferred until Kubernetes evidence is needed. | Lets business and technical evaluators try a real system and lets FairSpot collect usage/performance evidence without reopening AWS/Azure target-cloud planning. |
| **Client-owned production** | Client-selected cloud or platform, constrained by FairSpot component contracts, Dapr building blocks, OpenTelemetry telemetry, and documented backup/restore/security requirements. | Production operation belongs to the client. FairSpot should deliver deployable artifacts, configuration guidance, runbooks, and evidence rather than operate the client's environment. |

Dapr remains the boundary for pub/sub, state, bindings, service invocation, and secrets. OpenTelemetry remains the boundary for logs, metrics, and traces. Provider-specific services are allowed only behind those boundaries or in clearly isolated deployment scripts.

---

## 1. Deployment Profile Comparison

| Criterion | **Local** | **FairSpot hosted evaluation** | **Client-owned production** | **Kubernetes / enterprise option** |
|---|---|---|---|---|
| **Dapr support** | Self-hosted sidecars and local component YAML. | Self-hosted Dapr sidecars for NAS/Cloudflare and initial DigitalOcean Droplet profile; DOKS later if cluster evidence is needed. | Must support Dapr components or a documented equivalent adapter path. | Strong fit when client requires Kubernetes control. |
| **Identity integration** | Local or mocked OIDC. | Demo OIDC realm with seeded users and roles. | Client IdP through OIDC/OAuth 2.0, tenant and role claims mapped explicitly. | Enterprise OIDC, workload identity, private networking. |
| **Cost role** | Minimal developer cost. | Low monthly spend; enough to run credible demos and measurements. Static provider prices are not committed in public docs. | Client-owned cost model; FairSpot supplies sizing assumptions and measurement method. | Higher baseline cost, justified only by client controls or steady load. |
| **Operational complexity** | Low. | Medium; enough automation to redeploy repeatably. | Depends on client platform and controls. | High; useful when enterprise deployment standards require it. |
| **Multi-tenancy** | Tenant-scoped storage boundaries inside service-owned stores. | Same, with seeded demo tenants and visible admin flows. | Same, with client-approved naming, backup, retention, and access controls. | Same. |
| **CI/CD shape** | Local scripts and validation. | Build immutable images, deploy selected tags, seed, smoke test, collect evidence. | Deliverable pipeline template or client-integrated release process. | Profile-specific manifests or client platform equivalent. |
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

### 2.2 FairSpot-Operated Hosted Environment

The hosted evaluation environment is not client production. Its job is to prove the product story and collect evidence:

- seeded tenants, roles, employees, parking slots, policies, and booking history;
- working mobile/web/API flows for the target evaluator roles;
- repeatable deployment from source artifacts;
- basic backup/restore rehearsal;
- observable usage, latency, error rate, event processing, and notification delivery;
- clear teardown and cost control.

Release 1 uses NAS/Cloudflare because that path is already aligned with the containerized stack and public-domain evaluation plan. The cloud-hosted follow-up target is DigitalOcean:

- start with a small Droplet running the same containerized stack and self-hosted Dapr sidecars;
- keep Cloudflare in front where approved, or use a DigitalOcean Load Balancer only when the profile needs it;
- evaluate DigitalOcean Managed Databases, Spaces, and Container Registry only when they reduce operational risk or provide needed evidence;
- move to DOKS only if Kubernetes behavior, autoscaling, or client-facing cluster evidence becomes a real requirement.

### 2.3 Client-Owned Production

Client production is operated by the client or the client's hosting partner. FairSpot should provide:

- container images or build instructions;
- Dapr component contracts for pub/sub, state, bindings, secrets, and service invocation;
- OpenTelemetry instrumentation and exporter configuration guidance;
- identity claim mapping requirements;
- tenant storage provisioning and index guidance;
- backup, restore, incident, retention, and access-control runbooks;
- sizing assumptions and performance/usage evidence from demo or staging.

The exact provider choice is a client architecture decision. FairSpot should remain compatible with client-selected cloud, Kubernetes, and on-premises platforms by keeping provider-specific code outside the application services.

### 2.4 DigitalOcean Hosted Target

DigitalOcean is the active FairSpot-operated cloud-hosted follow-up target after the Release 1 NAS/Cloudflare evaluation path.

Initial profile:

- DigitalOcean Droplet running Docker Compose and the same containerized services used by NAS/Cloudflare.
- Self-hosted Dapr sidecars/runtime using the existing profile component names.
- Keycloak remains the first identity provider unless a pilot explicitly requires an external IdP.
- RabbitMQ, MongoDB/PostgreSQL, Vault, MinIO, and observability can stay self-hosted initially if backups, restore, and secrets are handled correctly.
- GHCR remains acceptable for images; DigitalOcean Container Registry is optional if it simplifies deployment.
- Cloudflare can remain the public edge/WAF. DigitalOcean Load Balancer is a profile decision, not a default requirement.

Managed-service evaluation path:

- DigitalOcean Managed Databases for PostgreSQL, MongoDB-compatible alternatives where approved, Valkey/Redis, OpenSearch, or other state services when they provide better durability than self-hosting.
- DigitalOcean Spaces for object storage where hosted reports, exports, backups, or attachments need provider-managed storage.
- DigitalOcean Monitoring for host/resource visibility; application telemetry still flows through OpenTelemetry-compatible collectors/backends.
- DOKS only after the Droplet profile proves insufficient or Kubernetes evidence is required.

### 2.5 Kubernetes / DOKS Candidate

Kubernetes gives full workload control: custom networking, Helm releases, Dapr operator/sidecar injection, full observability stack, and clearer client-enterprise mapping.

Operational overhead is higher than the Droplet profile and should be accepted only when the evidence is worth it:

- cluster and node upgrades must be managed;
- Dapr runtime/operator versions must be governed;
- ingress, certificates, secrets, backups, and observability become platform responsibilities;
- minimum cluster cost is higher than a single-host profile even at idle.

Recommended only if the Droplet profile proves insufficient or full Kubernetes-native deployment is required for client compliance/evaluation.

### 2.6 Hybrid / Minimal-Cost Stepping Stone

Keep local Docker Compose and repository-owned harness scripts for development. Deploy only the externally useful API surface first. Defer non-essential services until the demo needs them.

This is the lowest-risk path to a first live endpoint. The main downside is deferred integration testing between services in a real cloud environment.

### 2.7 Portable Dapr Baseline

Dapr components define the portability boundary. Provider changes should be a component/profile swap rather than an application-code rewrite.

**Portable (Dapr component swap only):**
- Pub/sub: local/self-hosted broker to selected Dapr-compatible broker.
- State store: local or self-hosted store to managed operational/document store supported by the selected profile.
- Secrets: local secret store to Vault, platform secret injection, or client-approved equivalent.
- Bindings: cron, object storage, email, SMS, and HTTP bindings to approved provider equivalents.

Provider-specific (must stay in profile/runbook code, not application services):

- container registry and image-pull credentials;
- workload identity or secret-injection mechanism;
- ingress, WAF, private networking, DNS, and certificates;
- CI/CD deployment step;
- platform monitoring/resource telemetry.

The Dapr abstraction is real but not free. Each component swap requires testing, component YAML updates, secret-store wiring, and smoke evidence.

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

The selected hosted region must satisfy the evaluation or client data-residency requirement. For FairSpot-operated evaluation profiles, use synthetic data unless a DPA-approved pilot explicitly changes that rule. Cross-region replication must not move personal data (booking requests, profiles, notifications) outside the approved jurisdiction without explicit DPA coverage.

### 7.2 Workload Identity

Where the runtime supports workload identity or platform-managed secret injection, Dapr component connections should use it instead of plaintext connection strings. The exact mechanism is provider-specific. When workload identity is unavailable, credentials must come from the configured secret store and must not be committed to source control or embedded in container images.

### 7.3 Secrets

- Use the selected profile's secret-management service through the Dapr secret-store boundary. Vault or profile-approved secret injection is the default FairSpot-operated path until a managed alternative is explicitly selected.
- No secrets in source control, container images, or Dapr component YAML (use secretstore reference pattern).
- Use Dapr secret scopes and component scopes so services can access only the secrets/components they require.
- GitHub Actions secrets for container registry credentials and deployment tokens are the CI boundary.

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

1. **DigitalOcean profile cutover**: Should the cloud-hosted follow-up start as Droplet/Docker Compose only, or should DOKS be included in the first cloud evidence pass?

2. **Persistence hosting choice for demo**: Which option gives credible backup/restore and reporting evidence without creating unnecessary monthly cost?

3. **Identity hosting for demo**: Self-hosted OIDC provider vs managed/shared demo OIDC? For client production, confirm that FairSpot supports client OIDC as long as claims are mapped.

4. **Observability evidence target**: What dashboards and measurements must exist before a client demo: usage counts, latency, error rate, event backlog, notification delivery, draw duration, and audit query performance?

5. **Client telemetry integrations**: Which client observability integration examples should FairSpot document first, and should the generic OpenTelemetry Collector be the default handoff pattern?

6. **Secrets target**: Which DigitalOcean-compatible secret-management approach should the cloud profile use while keeping the Dapr secret-store boundary stable?

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

The following documents contain outdated stack assumptions and are legacy references only:

- `docs/production/azure-setup.md` — legacy stub only; not an active FairSpot target-cloud plan.
- `docs/production/aws-setup.md` — legacy stub only; not an active FairSpot target-cloud plan.
- `docs/production/digitalocean-setup.md` — active FairSpot-operated cloud-hosted follow-up target note.

**Recommendation:** Keep the Azure/AWS paths as short compatibility stubs so old links do not imply active target-cloud support.

---

*Sources:*
- [DigitalOcean App Platform](https://docs.digitalocean.com/products/app-platform/)
- [DigitalOcean Kubernetes](https://docs.digitalocean.com/products/kubernetes/)
- [DigitalOcean Managed Databases](https://docs.digitalocean.com/products/databases/)
- [DigitalOcean Spaces](https://docs.digitalocean.com/products/spaces/)
- [Dapr components concept](https://docs.dapr.io/concepts/components-concept/)
- [Dapr bindings overview](https://docs.dapr.io/developing-applications/building-blocks/bindings/bindings-overview/)
