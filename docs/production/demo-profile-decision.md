# OPS007A Demo Profile Decision And Runtime Inventory

**Status:** Provider choice superseded. Release 1 uses NAS/Cloudflare; the cloud-hosted follow-up target is DigitalOcean.
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
| RabbitMQ | `rabbitmq:3-management` | Dapr pub/sub broker (`fairspot-pubsub`) |
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
| `fairspot-pubsub` | pub/sub | RabbitMQ | **Yes** (`fairspot-pubsub.yaml`) | RabbitMQ first; Dapr-compatible hosted broker later if needed |
| `bookingstore` | state | MongoDB | **Yes** (`bookingstore.yaml`) | Self-hosted MongoDB first; managed database later if needed |
| `secretstore` | secret store | HashiCorp Vault | **Yes** (`vault-demo.yaml`) | Vault or profile-approved secret injection |
| `profilestore` | state | MongoDB | **No — OPS007B must add** | Same state-store pattern as bookingstore |
| `notificationstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `auditstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `configstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `reportingstore` | state | MongoDB | **No — OPS007B must add** | MongoDB Atlas |
| `s3store` | output binding | MinIO | **No — OPS007B must add** | MinIO first; DigitalOcean Spaces when hosted object storage is needed |

### 1.4 Identity

- Keycloak with `fps-local` realm, imported by `tools/dev-setup-auth.sh`
- Seeded users: `employee1`, `employee2`, `employee3`, `hr-admin`
- Local demo passwords set at seed time; never committed

### 1.5 Seed and Reset

- `tools/dev-seed.sh` — loads Green Logistics tenant, users, parking data
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
| **Cost** | Small VPS baseline; validate current provider prices before sharing externally. TLS can use Let's Encrypt or the selected edge provider. |
| **Complexity** | Very low. Stack is identical to local; only add TLS ingress and firewall rules. |
| **Dapr fit** | Identical to local — `dapr run -f dapr.yaml` or `start-with-dapr.sh` on the VM. No component changes. |
| **Identity fit** | Same Keycloak; runs on Docker with a persistent volume. |
| **Persistence fit** | Same MongoDB on Docker. Backup = VM snapshot or `mongodump` cron. |
| **Teardown** | `docker compose down -v` then destroy VM. |
| **Limitation** | Not a proof of cloud Dapr component swap. All services on one VM is a single point of failure. Not representative of client production topology. |
| **Time to working URL** | 1–2 days. |

### Profile B — DigitalOcean cloud profile

Deploy the FairSpot stack to DigitalOcean after the NAS/Cloudflare Release 1 profile is proven. Start with a Droplet/Docker Compose profile and self-hosted Dapr sidecars. Evaluate DigitalOcean Managed Databases, Spaces, Container Registry, Load Balancers, and DOKS only when they provide needed durability or evidence.

| Criterion | Assessment |
|---|---|
| **Cost** | Validate current DigitalOcean prices before sharing externally. Keep the first cloud profile small and avoid static public commitments. |
| **Complexity** | Low to medium for Droplet/Docker Compose; medium to high if DOKS or managed service swaps are included. |
| **Dapr fit** | Good with self-hosted sidecars and existing component contracts. DOKS can add Kubernetes-native Dapr operation later. |
| **Identity fit** | Keep Keycloak first unless a pilot requires a managed/client IdP. |
| **Persistence fit** | Self-hosted stores first with documented backup/restore; evaluate managed databases when durability or operations evidence requires it. |
| **Teardown** | Destroy Droplet and managed resources after backup/evidence retention decisions are complete. |
| **Limitation** | The Droplet path proves cloud hosting and public operation, not managed component swaps. DOKS increases operational overhead. |
| **Time to working URL** | Similar to Profile A for Droplet/Docker Compose after domain, secrets, and edge routing are prepared. |

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

**Start with the Release 1 NAS/Cloudflare profile. Use Profile B (DigitalOcean Droplet + Docker Compose) as the cloud-hosted follow-up.**

### Rationale

The NAS/Cloudflare profile is the fastest path to a stable public demo URL with zero new target-cloud risk:

- The stack is identical to local — same Compose file, same Dapr version, same components
- Dapr component contracts are unchanged; this is not a proof of swap but a proof of the product
- A working public-domain demo can use the existing containerized stack and Cloudflare edge profile
- Cost stays under operator control without committing public provider prices
- Keycloak, RabbitMQ, MongoDB, and Vault all have Docker volumes for persistence; VM snapshot covers backup at demo scale

Profile B (DigitalOcean) is the right next step *after* a successful Release 1 walkthrough because:

- It validates FairSpot-operated cloud hosting without reopening AWS/Azure planning
- It demonstrates a cloud deployment path to evaluators who ask about production topology
- Three demo Dapr component templates already exist (`fairspot-pubsub`, `bookingstore`, `vault-demo`); the remaining six are derived from the same MongoDB/Vault pattern and OPS007B adds them
- It sets up the evidence base for client production conversations

Profile C (Fly.io) adds complexity without a compensating benefit and is not recommended.

### Profile A Implementation Prerequisites

**Business decisions (Robert/Codex must answer — see [Open Decisions](#open-decisions)):**

1. Domain or subdomain for the demo URL (e.g. `fairspot-demo.yourdomain.com`) — TLS and OIDC redirect URIs depend on it
2. Target host choice for the cloud follow-up (DigitalOcean Droplet first unless Robert approves a different option)
3. SSH key for VM access

**Implementation risks and known checks (OPS007B must address):**

- **Linux Dapr multi-app run** — `dapr.yaml` is developed and tested on macOS; verify the multi-app run file and `start-with-dapr.sh` work unchanged on the target Linux distribution
- **TLS and OIDC redirect URLs** — Let's Encrypt cert provisioning, Traefik/Nginx TLS termination config, and Keycloak redirect URIs must all reference the final domain before any smoke test
- **Firewall rules** — only HTTPS (443) and SSH (22) should be publicly exposed; Dapr gRPC, MongoDB, Vault, and RabbitMQ ports must be internal-only
- **Persistent volumes** — MongoDB, Keycloak, Vault, and MinIO data must survive VM reboots via named Docker volumes; no container uses ephemeral storage for demo data
- **Service restart strategy** — Docker restart policies (`unless-stopped`) for infrastructure and application containers; define expected boot order and startup health checks
- **Docker network availability** — the Compose-managed network must already exist or be created on VM start; multi-app Dapr run must resolve service hostnames correctly on the VM
- **Missing demo Dapr component templates** — `profilestore`, `notificationstore`, `auditstore`, `configstore`, `reportingstore`, and `s3store` templates do not exist yet; the NAS/Droplet path can use local-style components first, but managed-service swaps require profile-specific templates
- **Smoke vs durable components** — Profile A reuses local components (MongoDB on Docker, RabbitMQ on Docker); these are durable between restarts if volumes are configured correctly, but a VM failure loses data since there is no off-VM backup by default

---

## 5. Open Decisions

These are concrete yes/no or option choices that require input from Robert or Codex before OPS007B (hosted deployment) can start.

| # | Decision | Options | Blocking |
|---|---|---|---|
| D1 | **Cloud follow-up host** | DigitalOcean Droplet first, DOKS later, or explicitly approved alternative | Yes — needed before cloud provisioning |
| D2 | **Domain for demo URL** | Use existing domain, register `fairspot.demo` / `fairspot-demo.example.com`, or use IP-only for first demo | Yes — TLS cert and OIDC redirect URIs depend on it |
| D3 | **NAS → DigitalOcean timing** | After Release 1 evaluation path is stable vs before first external walkthrough | Yes — determines OPS007B scope |
| D4 | **Identity for DigitalOcean profile** | Keep Keycloak first vs client/managed IdP for a specific pilot | No — can decide during OPS007B |
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
