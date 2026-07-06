# OPS010A Deployment Profile Template

**Status:** Template established. Release 1 hosted evaluation is NAS/Cloudflare; the FairSpot-operated cloud-hosted follow-up target is DigitalOcean. AWS/Azure are legacy/client-selected examples only.
**Parent:** #229 · **Strategy:** [Hosting and Deployment Strategy](./hosting-deployment-strategy)

FairSpot is a **bring-your-own-cloud** platform: the core architecture defines provider-neutral *contracts*, and each concrete environment is a *deployment profile* that binds those contracts to real technology. This page is the reusable template that keeps the two separated — so a new profile (a client cloud, a Kubernetes cluster, an on-prem install) can be described by filling in a known shape without changing application code or core architecture.

- **Section 1** is the provider-neutral contract every profile must satisfy.
- **Section 2** is the blank profile skeleton to copy.
- **Section 3** fills the skeleton for the Local, NAS/Cloudflare, DigitalOcean, and Client-owned/BYOC profiles.

It does not restate the [deployment-profile strategy](./hosting-deployment-strategy) (comparison matrix, Dapr component mapping, cost/security depth) or duplicate per-profile runbooks; it links to them.

---

## 1. Provider-neutral contracts

Every deployment profile must satisfy these contracts. Concrete technology is a profile choice; the contract is not. "Portable" means the change is a Dapr component / configuration swap with no application-service code change.

| Section | Contract (holds for all profiles) | Portable |
| --- | --- | --- |
| **Runtime** | .NET 10 services + web app run as containers (or from source locally), each optionally paired with a Dapr sidecar. Provider movement must not require application-service code changes. | — |
| **Ingress** | Public entry is HTTPS with documented hostnames; direct service/container ports are never Internet-exposed. TLS, routing, and WAF/rate-limiting where required. | Profile-specific (edge/WAF/DNS/certs stay in profile code) |
| **Identity** | OIDC/OAuth issues a stable subject, `tenant_id`, and role claims. Tenant/user/role come only from validated claims — never from request bodies, query, headers, or UI state. Company SSO and FairSpot-local fallback both supported; MFA per [authentication policy](../security/authentication). | Profile/client IdP selectable |
| **Dapr components** | Application code references only **logical component names**; the same names are reused across profiles. Components are scoped per service (least privilege) outside local. | Yes (component swap) |
| **Persistence / read models** | Each service owns its operational store and read models with tenant-safe collections/partitions/keys derived from a sanitised tenant key ([tenant storage contract](./tenant-storage-contract)); storage names are never caller-supplied. Backup, restore, encryption, and repeatable tenant provisioning required. | Yes (state component swap) |
| **Broker / pub-sub** | Booking events reach Notification/Audit/Reporting through the logical `fairspot-pubsub` component bound to an approved broker or provider-native event service. | Yes (pub/sub swap) |
| **Secrets** | Credentials, certificates, API keys, and tunnel/registry tokens come from a secret store through the Dapr secret-store boundary. No secrets in Git, container images, or component YAML (use `secretKeyRef`). | Yes (secret store swap) |
| **Object storage** | Reports, exports, backup artifacts, and future attachments use tenant-scoped paths (prefer one bucket/container per tenant) with encryption; clients never load direct storage paths. | Yes (binding swap) |
| **Observability** | Services emit OpenTelemetry-compatible metrics, logs, and traces (span `tenant_id`); the profile selects where they land. Application telemetry stays FairSpot-owned; provider monitoring is host/network only. | Yes (OTel exporter config) |
| **Backup / restore** | Backup and restore must be tested at least once before real customer data; RTO/RPO per [availability model](./availability-model). See [Backup and Restore](./backup-restore). | Profile-specific |
| **Data classification / encryption** | Local uses synthetic data only and may use plain HTTP on a developer machine. NAS/hosted/pilot/client profiles classify tenant and employee data **Confidential by default**: HTTPS, encrypted stores/backups, real secret management, and smoke evidence before real data. | — |
| **Cost assumptions** | Docs record the cost *model and assumptions*, not committed provider prices. Demo/evaluation cost is separated from client-production cost. | — |
| **Operational ownership** | Who deploys, patches, and responds. FairSpot owns delivery/evaluation profiles; the client owns client production. | — |
| **Support boundary** | FairSpot delivers artifacts, configuration guidance, runbooks, and evidence; the client operates client-owned production. | — |
| **Validation / evidence** | Repeatable deploy (no manual server edits), HTTPS + documented hostnames, claim-based identity, provisioned tenant scopes/indexes, validated Dapr pub/sub, secret injection, backup/restore rehearsal, telemetry + alerts, and `./tools/validate.sh` green. See [production readiness](../production#testing-and-readiness). | — |

Sources: provider-neutral architecture in `docs/technology-layer/software-architecture.md`; the *bring-your-own-cloud* and *tenant-scoped storage boundary* decisions in [Versions and Decisions](../versions-and-decisions); portable/provider-specific split in [Hosting Strategy](./hosting-deployment-strategy); [Dapr-First Production Standards](./dapr-first-production-standards).

---

## 2. Profile skeleton (copy this)

To document a new profile, copy the table and fill each cell with that profile's concrete choice. Leave contract wording to Section 1; record only what this profile binds.

| Section | This profile's choice |
| --- | --- |
| Runtime | |
| Ingress | |
| Identity | |
| Dapr components | |
| Persistence / read models | |
| Broker / pub-sub | |
| Secrets | |
| Object storage | |
| Observability | |
| Backup / restore | |
| Data classification / encryption | |
| Cost assumptions | |
| Operational ownership | |
| Support boundary | |
| Validation / evidence | |

---

## 3. Filled profiles

### 3.1 Local

The developer/CI profile. Filled from the current [Local Test Harness](./local-test-harness) and the Dapr component files under `code/infrastructure/dapr/components/local/`.

| Section | Local |
| --- | --- |
| Runtime | .NET 10 services run from source, each (except Identity) paired with a Dapr sidecar; started by `tools/start-local-harness.sh` (stop/reset via `tools/stop-local-harness.sh`). Docker Compose (`code/infrastructure/docker-compose.yaml`) runs the infrastructure. |
| Ingress | Envoy gateway at `http://localhost:10000` fronts all services under one origin and passes `Authorization` through unchanged (it neither mints nor verifies tokens). |
| Identity | Local Keycloak (`http://localhost:8180`), realm `fps-local`, clients `fps-web-dev` / `fps-mobile-dev`; seeded synthetic users via `tools/dev-setup-auth.sh`. |
| Dapr components | Logical names backed locally: state stores (`bookingstore`, `profilestore`, `auditstore`, `configstore`, `reportingstore`, `customerstore`, `notificationstore`, `workflowstore`, …) → MongoDB; `fairspot-pubsub` → RabbitMQ; `secretstore` → HashiCorp Vault (dev); `s3store` → MinIO; cron bindings `draw-scheduler` / `sandbox-reset-scheduler`. A `components/smoke/` variant swaps state/pub-sub to in-memory. |
| Persistence / read models | MongoDB per service (collection-per-tenant), tenant-scoped keys via `TenantStorageKey`; in-memory smoke variant for fast runs. |
| Broker / pub-sub | RabbitMQ (`3-management`) behind `fairspot-pubsub`. |
| Secrets | HashiCorp Vault in dev mode behind `secretstore`; local dev credentials only. |
| Object storage | MinIO (`fairspot-bucket`) behind `s3store`. |
| Observability | OpenTelemetry traces export to local Jaeger via OTLP (OBS001, [Local Observability](../local-observability)); Grafana/Prometheus/Loki/Alertmanager dashboards in Compose; logs carry `TraceId` and `tenant_id`. |
| Backup / restore | Not applicable — named volumes only; `stop-local-harness.sh --reset` discards them. |
| Data classification / encryption | Synthetic data only; plain HTTP permitted on the developer machine. |
| Cost assumptions | Developer machine only. |
| Operational ownership | FairSpot delivery team / developer. |
| Support boundary | None — not an operated environment. |
| Validation / evidence | `start-local-harness.sh` seeds Green Logistics (`tools/dev-seed.sh`) and runs post-seed smoke `curl`s (`/me`, `/bookings`, `/notifications/unread-count`, `/profile/snapshot`) through the gateway; `./tools/validate.sh`. |

### 3.2 NAS / Cloudflare (Release 1 hosted evaluation)

Public contract only — detailed operator steps live in the private `fairspot-platform` runbook (#684). See [NAS Cloudflare Deployment Contract](./nas-cloudflare-deployment-profile) and [OIDC/Auth Contract](./nas-cloudflare-auth-profile).

| Section | NAS / Cloudflare |
| --- | --- |
| Runtime | FairSpot services + Dapr sidecars as containers on the NAS via Docker Compose. |
| Ingress | HTTPS only via Cloudflare Tunnel/WAF; public hostnames `app.fairspot.net` (application) and `auth.fairspot.net` (authentication); no direct container ports exposed. |
| Identity | One Keycloak realm `fairspot` for demo + Green Logistics; tenant separation by application tenant claims, not separate realms. Company SSO + FairSpot-local (MFA per policy). |
| Dapr components | Same logical names as Local; **Dapr mTLS is a documented exception (disabled)** on this single-host Compose profile (no Sentry control plane) — startup reports the active security mode. |
| Persistence / read models | Tenant-safe, encrypted stores/backups per the hosted encryption boundary. |
| Broker / pub-sub | RabbitMQ behind `fairspot-pubsub`. |
| Secrets | Injected from a secret store (Vault via the Dapr secret-store boundary); tunnel tokens, realm signing material, admin passwords, and recovery keys never in Git. |
| Object storage | Tenant-scoped, encrypted; profile-selected backing. |
| Observability | FairSpot-owned OpenTelemetry app telemetry; Cloudflare/provider monitoring covers host/network only; business activity stays in the Audit service. |
| Backup / restore | Backup/restore + encryption evidence required before real customer data ([Encryption and Backup Evidence](./nas-encryption-backup-evidence)); detailed procedure private. |
| Data classification / encryption | Confidential-by-default; HTTPS, encrypted stores/backups, real secret management. |
| Cost assumptions | Low monthly evaluation cost; no committed prices in public docs. |
| Operational ownership | FairSpot delivery team (evaluation, not client production). |
| Support boundary | Evaluation environment; not a client-operated production system. |
| Validation / evidence | Public-boundary smoke evidence ([Hosted Readiness Expectations](./hosted-smoke-runbook)) before real data. |

### 3.3 DigitalOcean (cloud-hosted follow-up target)

Target shape only — see [DigitalOcean Setup](./digitalocean-setup). Reuses the same logical Dapr component names; managed services are evaluated only when a concrete evidence/durability need exists.

| Section | DigitalOcean |
| --- | --- |
| Runtime | Start with a DO Droplet running the same containerized stack via Docker Compose; DOKS only if Kubernetes evidence is required. |
| Ingress | Cloudflare in front where approved, or a DO Load Balancer when the profile needs it; public endpoints HTTPS. |
| Identity | Keycloak first; managed/client OIDC only when a pilot requires it. |
| Dapr components | Same logical names; self-hosted sidecars/runtime initially. |
| Persistence / read models | Self-hosted stores first; DO Managed Databases (PostgreSQL, MongoDB-compatible, Valkey/Redis, OpenSearch) evaluated only when durability/evidence improves. |
| Broker / pub-sub | RabbitMQ first (or another Dapr-compatible broker later) behind `fairspot-pubsub`. |
| Secrets | Vault or profile-approved injection through the secret-store boundary; no secrets in manifests/docs. |
| Object storage | MinIO initially; DO Spaces when hosted storage is needed. |
| Observability | Grafana/Prometheus/Loki/Jaeger + OpenTelemetry export first; DO Monitoring adds host visibility only. |
| Backup / restore | Droplet snapshots + service backups first; managed-DB backups if state moves; restore evidence required before customer data. |
| Data classification / encryption | Confidential-by-default when real data is present. |
| Cost assumptions | Cost model only; GHCR acceptable for images, DO Container Registry optional. |
| Operational ownership | FairSpot delivery team (follow-up evaluation target). |
| Support boundary | FairSpot-operated evaluation; not a client-operated environment. |
| Validation / evidence | Same readiness bar as NAS before any real data; no application-service code changes for provider movement. |

### 3.4 Client-owned / BYOC

Operated by the client or the client's hosting partner. FairSpot supplies deployable artifacts, configuration guidance, runbooks, and evidence; the client selects the provider and runs the environment. See [Client Production Handoff](./client-production-handoff).

| Section | Client-owned / BYOC |
| --- | --- |
| Runtime | Client-selected cloud, Kubernetes, or on-premises; container images or build instructions provided by FairSpot. |
| Ingress | Client-managed HTTPS ingress/WAF/DNS/certificates. |
| Identity | Client IdP via OIDC/OAuth 2.0 with explicitly mapped tenant and role claims. |
| Dapr components | Must support Dapr components or a documented equivalent adapter path, keeping the logical component names. |
| Persistence / read models | Client-approved stores with tenant-scoped provisioning, backup, retention, and access controls. |
| Broker / pub-sub | Client-approved broker/provider behind `fairspot-pubsub`. |
| Secrets | Client secret-management platform / workload identity through the Dapr secret-store boundary. |
| Object storage | Client-approved, tenant-scoped, encrypted storage. |
| Observability | OpenTelemetry export to the client's observability platform (Collector as the default handoff). |
| Backup / restore | Client-owned backup/restore, tested and evidenced per the handoff. |
| Data classification / encryption | Client region, retention, DPA, encryption at rest and in transit. |
| Cost assumptions | Client-owned cost model; FairSpot supplies sizing assumptions and measurement method. |
| Operational ownership | Client IT / operations. |
| Support boundary | FairSpot delivers artifacts + guidance + evidence; the client operates production. |
| Validation / evidence | Client runs the readiness checklist against their environment before production use. |

### 3.5 Legacy / client-selected (AWS, Azure)

AWS and Azure are **not** active FairSpot-operated target clouds — they are legacy compatibility stubs ([AWS Setup](./aws-setup), [Azure Setup](./azure-setup)). An AWS or Azure deployment is possible only when a client explicitly selects it and provides tested Dapr component manifests, secrets, storage, monitoring, and backup/restore evidence for that client-owned environment. Treat them as instances of the Client-owned/BYOC profile, not as FairSpot targets. This is recorded in [Versions and Decisions](../versions-and-decisions) (bring-your-own-cloud architecture; DigitalOcean follow-up target).

---

## Validation evidence

- Markdown review for internal consistency; `git diff --check` clean.
- Facts sourced from current public docs: [Local Test Harness](./local-test-harness), [Hosting Strategy](./hosting-deployment-strategy), [NAS Cloudflare Deployment Contract](./nas-cloudflare-deployment-profile), [OIDC/Auth Contract](./nas-cloudflare-auth-profile), [DigitalOcean Setup](./digitalocean-setup), [Dapr-First Production Standards](./dapr-first-production-standards), [Tenant Storage Contract](./tenant-storage-contract), and [Versions and Decisions](../versions-and-decisions).
- No cloud manifests, provider deployment code, secrets, pricing commitments, or private operator steps added.
