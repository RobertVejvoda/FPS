# OPS007A Demo Profile Decision And Runtime Inventory

**Status:** Decision required — see [Open Decisions](#open-decisions) below.
**Prepared by:** Claude (FPS Implementer), 2026-05-22
**Tracks:** Issue #234 → OPS007 (#226)

---

## 1. Local Runtime Inventory

What the local harness already provides, from `local-test-harness.md` and the repository:

### 1.1 Application Services

**In the current harness** — started by `tools/start-with-dapr.sh` (or `dapr run -f dapr.yaml`) plus a plain `dotnet run` for Identity:

| Service | Port | Dapr sidecar | Notes |
|---|---|---|---|
| Identity (FPS.Identity) | 5192 | No | OIDC issuer, `/me`, token exchange |
| Booking (FPS.Booking) | 5131 | Yes | Booking requests, draw, allocation workflow |
| Profile (FPS.Profile) | 5197 | Yes | Profile snapshots, employee bootstrap |
| Notification (FPS.Notification) | 5157 | Yes | Email/in-app notification consumer |
| Audit (FPS.Audit) | 5161 | Yes | Audit record consumer |
| Reporting (FPS.Reporting) | 5171 | Yes | Read-model consumer |
| Configuration (FPS.Configuration) | 5141 | Yes | Parking policy and slot management |

**Exists in code but not in the harness:**

| Service | Status | Notes |
|---|---|---|
| Customer (FPS.Customer) | No launch profile, not started by harness | Tenant onboarding and readiness; needed for CUST-slice API flows but not the employee demo path |

### 1.2 Infrastructure Components (Docker Compose)

| Component | Image | Role |
|---|---|---|
| Keycloak | `quay.io/keycloak/keycloak:latest` | OIDC identity provider, `fps-local` realm |
| RabbitMQ | `rabbitmq:3-management` | Dapr pub/sub broker (`fps-pubsub`) |
| MongoDB | `mongo:latest` | Dapr state stores (booking, profile, audit, reporting, config, notification) |
| HashiCorp Vault | `hashicorp/vault:1.18` | Dapr secret store |
| MinIO | `minio/minio` | Dapr S3-compatible output binding (`s3store`) |
| Envoy | `envoyproxy/envoy:v1.33.1` | Local API gateway / ingress |
| Jaeger | `jaegertracing/all-in-one` | Distributed traces |
| Zipkin | `openzipkin/zipkin` | Trace export (alternative) |
| Prometheus | `prom/prometheus` | Metrics scrape |
| Alertmanager | `prom/alertmanager` | Alert routing |
| Loki | `grafana/loki` | Log aggregation |
| Grafana | `grafana/grafana` | Dashboards |
| Dapr runtime / sidecar | `daprio/dapr:1.18.0` for placement/scheduler, `daprio/daprd:1.18.0` for sidecars | Placement, scheduler, sidecar runtime (`daprd`), pub/sub, state, secrets |

### 1.3 Dapr Component Contracts

Demo component templates in `code/infrastructure/dapr/components/demo/` are **partial**. OPS007B must add the missing state-store components.

| Logical name | Building block | Local provider | Demo template exists? | Demo candidate |
|---|---|---|---|---|
| `fps-pubsub` | pub/sub | RabbitMQ | **Yes** (`fps-pubsub.yaml`) | Azure Service Bus or managed RabbitMQ |
| `bookingstore` | state | MongoDB | **Yes** (`bookingstore.yaml`) | MongoDB Atlas or managed MongoDB |
| `secretstore` | secret store | HashiCorp Vault | **Yes** (`vault-demo.yaml`) | Azure Key Vault or Vault managed |
| `profilestore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas (same pattern as bookingstore) |
| `notificationstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `auditstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `configstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `reportingstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `s3store` | output binding | MinIO | **No — OPS007B must add** | Cloud S3-compatible store |

### 1.4 Identity

- Keycloak with `fps-local` realm, imported by `tools/dev-setup-auth.sh`
- Seeded users: `employee1`, `employee2`, `employee3`, `hr-admin`
- Local demo passwords set at seed time; never committed

### 1.5 Seed and Reset

- `tools/dev-seed.sh` — loads demo tenant, users, parking data
- `tools/demo-reset.sh` — drops and re-creates demo data without stopping infrastructure
- `tools/dev-auth.sh <user>` — prints a bearer token for the named demo user

### 1.6 Smoke Checks

| Endpoint | Expected |
|---|---|
| `GET /openapi/v1.json` on Identity, Booking, Profile | 200 |
| `GET /notifications/unread-count` on Notification | 401 |
| `GET /configuration/parking-policy` | 401 (authenticated endpoint alive) |
| `GET /audit` | 401 |
| `GET /reports/parking/summary` | 401 |

---

## 2. Minimum Hosted Demo Shape

For one credible employee demo, the hosted environment needs:

| Capability | Minimum requirement |
|---|---|
| HTTPS ingress | Public URL with TLS; routing to services |
| Identity / OIDC | Working login for seeded demo users |
| Booking + Profile + Configuration | Core employee flow: submit, view, policy enforcement |
| Dapr pub/sub | Booking events reach at least Notification and Audit |
| Notification | Booking outcomes visible (email delivery or in-app stub) |
| Audit | Booking events recorded and queryable |
| Reporting | At least summary report available |
| Seed data | Demo tenant, 3+ employees, slots, policy, some booking history |
| Reset | Repeatable reset to known state between demo sessions |
| Observability | Enough to show latency, event flow, and error rate during a demo |
| Secret management | No secrets in manifests or screenshots |

The Customer service is not required for the Demo v0 employee flow. App-store packaging is out of scope, but the hosted backend, OIDC issuer, and ingress must support mobile app configuration and mobile employee smoke — the mobile app must be able to point at the demo URL and log in with seeded users.

---

## 3. Demo Profile Comparison

### Profile A — Docker Compose on a VPS (minimal delta from local)

Run the existing Docker Compose stack on a small cloud VM with a public IP, TLS termination via Traefik or Nginx, and a domain.

| Criterion | Assessment |
|---|---|
| **Cost** | $6–20/month (Hetzner CX22/CAX21, DigitalOcean Basic, Linode Nanode). TLS free via Let's Encrypt. |
| **Complexity** | Very low. Stack is identical to local; only add TLS ingress and firewall rules. |
| **Dapr fit** | Identical to local — `dapr run -f dapr.yaml` or `start-with-dapr.sh` on the VM. No component changes. |
| **Identity fit** | Same Keycloak; runs on Docker with a persistent volume. |
| **Persistence fit** | Same MongoDB on Docker. Backup = VM snapshot or `mongodump` cron. |
| **Teardown** | `docker compose down -v` then destroy VM. |
| **Limitation** | Not a proof of cloud Dapr component swap. All services on one VM is a single point of failure. Not representative of client production topology. |
| **Time to working URL** | 1–2 days. |

### Profile B — Azure Container Apps (ACA) with Azure-managed components

Deploy FairSpot services as ACA apps using ACA's native Dapr sidecar, Azure Service Bus for pub/sub, MongoDB Atlas for state, Azure Key Vault for secrets, and Microsoft Entra External ID or a small Keycloak ACA app for identity.

| Criterion | Assessment |
|---|---|
| **Cost** | Consumption plan free tier covers light demo traffic (180k vCPU-sec/month free). MongoDB Atlas M0 free tier covers demo data volume. Azure Service Bus Basic ≈ $0.05/million operations. Key Vault ≈ $0/month at demo volume. Main variable: identity — Entra External ID has a free tier (50k MAU); Keycloak on a dedicated ACA app adds ≈$5–15/month. Total estimated: $0–30/month at zero or light traffic. |
| **Complexity** | Medium. ACA Dapr is injected per-app via CLI or Bicep. Requires ACA environment, managed identity, component YAML. Existing `demo/` component templates are a starting point. |
| **Dapr fit** | Excellent. ACA is the reference demo candidate in `hosting-deployment-strategy.md`. Native sidecar, managed identity for component auth. |
| **Identity fit** | Entra External ID: managed, no hosted VM, free tier sufficient. Alternative: Keycloak on a single always-on ACA dedicated plan app (avoids Entra dependency). |
| **Persistence fit** | MongoDB Atlas free tier (M0) for state stores. Backup via Atlas snapshots. Dapr component swap from local MongoDB is one YAML change. |
| **Teardown** | Delete resource group. Scale-to-zero during idle periods. |
| **Limitation** | Requires Azure subscription and service principal. Bicep or CLI scripts needed. More setup than Profile A. Entra External ID tenant is a new dependency unless Keycloak is kept. |
| **Time to working URL** | 3–5 days including ACA environment, component wiring, and identity. |

### Profile C — Fly.io with self-hosted components

Deploy FairSpot services and supporting infrastructure as Fly.io apps. No managed Dapr; Dapr CLI installed in containers.

| Criterion | Assessment |
|---|---|
| **Cost** | Per-VM billing. 7–9 Fly Machines at 256–512MB RAM ≈ $35–70/month. No free managed Dapr, pub/sub, or database. |
| **Complexity** | High. No native Dapr support; requires running Dapr CLI inside each container or as a co-located process. Complex networking between Fly apps. |
| **Dapr fit** | Fair. Works but no first-class support; sidecar injection is manual. |
| **Identity fit** | Self-hosted Keycloak on a Fly Machine with persistent volume. |
| **Persistence fit** | Self-hosted MongoDB on Fly Machine, or MongoDB Atlas. |
| **Teardown** | `fly apps destroy`. |
| **Limitation** | Highest complexity for no clear benefit over Profile A or B. Not recommended. |
| **Time to working URL** | 4–7 days. |

---

## 4. Recommendation

**Start with Profile A (VPS + Docker Compose). Plan a Profile B (ACA) migration for Demo v0 release.**

### Rationale

Profile A is the fastest path to a stable public demo URL with zero new technology risk:

- The stack is identical to local — same Compose file, same Dapr version, same components
- Dapr component contracts are unchanged; this is not a proof of swap but a proof of the product
- A working demo at `https://fps-demo.example.com` can exist within 1–2 working days
- Cost is $6–20/month with no subscription, service principal, or cloud account required to start
- Keycloak, RabbitMQ, MongoDB, and Vault all have Docker volumes for persistence; VM snapshot covers backup at demo scale

Profile B (ACA) is the right next step *after* a successful Demo v0 walkthrough because:

- It validates the Dapr component swap (the core portability claim)
- It demonstrates cloud-native deployment to evaluators who ask about production topology
- Three demo Dapr component templates already exist (`fps-pubsub`, `bookingstore`, `vault-demo`); the remaining six are derived from the same MongoDB/Vault pattern and OPS007B adds them
- It sets up the evidence base for client production conversations

Profile C (Fly.io) adds complexity without a compensating benefit and is not recommended.

### Profile A Implementation Prerequisites

**Business decisions (Robert/Codex must answer — see [Open Decisions](#open-decisions)):**

1. Domain or subdomain for the demo URL (e.g. `fps-demo.yourdomain.com`) — TLS and OIDC redirect URIs depend on it
2. VPS provider choice (Hetzner, DigitalOcean, or other)
3. SSH key for VM access

**Implementation risks and known checks (OPS007B must address):**

- **Linux Dapr multi-app run** — `dapr.yaml` is developed and tested on macOS; verify the multi-app run file and `start-with-dapr.sh` work unchanged on the target Linux distribution
- **TLS and OIDC redirect URLs** — Let's Encrypt cert provisioning, Traefik/Nginx TLS termination config, and Keycloak redirect URIs must all reference the final domain before any smoke test
- **Firewall rules** — only HTTPS (443) and SSH (22) should be publicly exposed; Dapr gRPC, MongoDB, Vault, and RabbitMQ ports must be internal-only
- **Persistent volumes** — MongoDB, Keycloak, Vault, and MinIO data must survive VM reboots via named Docker volumes; no container uses ephemeral storage for demo data
- **Service restart strategy** — Docker restart policies (`unless-stopped`) for infrastructure and application containers; define expected boot order and startup health checks
- **Docker network availability** — the Compose-managed network must already exist or be created on VM start; multi-app Dapr run must resolve service hostnames correctly on the VM
- **Missing demo Dapr component templates** — `profilestore`, `notificationstore`, `auditstore`, `configstore`, `reportingstore`, and `s3store` templates do not exist yet; Profile A uses the local components unchanged, so this is not a blocker for Profile A but is a blocker for Profile B
- **Smoke vs durable components** — Profile A reuses local components (MongoDB on Docker, RabbitMQ on Docker); these are durable between restarts if volumes are configured correctly, but a VM failure loses data since there is no off-VM backup by default

---

## 5. Open Decisions

These are concrete yes/no or option choices that require input from Robert or Codex before OPS007B (hosted deployment) can start.

| # | Decision | Options | Blocking |
|---|---|---|---|
| D1 | **VPS provider** | Hetzner CX22 (~€4/month), DigitalOcean Basic Droplet (~$6/month), other | Yes — needed to provision the VM |
| D2 | **Domain for demo URL** | Use existing domain, register `fairspot.demo` / `fps-demo.example.com`, or use IP-only for first demo | Yes — TLS cert and OIDC redirect URIs depend on it |
| D3 | **Profile A → B migration timing** | Before Demo v0 launch (validate Dapr swap claim) vs after Demo v0 launch (keep velocity) | Yes — determines OPS007B scope |
| D4 | **Identity for Profile B** | Keep Keycloak on ACA dedicated app vs Entra External ID free tier | No — can decide during OPS007B |
| D5 | **Observability on Profile A** | Keep Grafana/Prometheus/Loki on the same VM vs skip dashboards for first demo and add in OPS007B | No — can decide during OPS007B |
| D6 | **Demo reset downtime** | Acceptable to stop and restart Docker Compose for reset (≈30s) vs require hot-reset without downtime | No — can decide during OPS007B |

Robert/Codex must answer **D1**, **D2**, and **D3** before OPS007B (deployment) work begins. D4–D6 can be decided in parallel with OPS007B.

---

## 6. Assumptions

The following assumptions are recorded. If any are wrong, the recommendation above may change.

- The demo is for technical/business evaluation, not a load test or security audit. A single VM is acceptable.
- Demo data is synthetic only. No real customer data in the hosted environment.
- App-store packaging is out of scope, but the backend, OIDC issuer, and ingress must support mobile app configuration and mobile employee smoke for Demo v0.
- A 30-second restart window for demo reset is acceptable.
- Cost visibility requires recording actual provider pricing before sharing externally; the estimates above are indicative only.
- Linux compatibility of `dapr.yaml` and `start-with-dapr.sh` must be verified during OPS007B before assuming the multi-app run works unchanged.
