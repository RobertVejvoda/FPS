# OPS011 NAS Cloudflare Deployment Profile

**Status:** Ready for operator use — follow-up gaps listed in [Before Customer Traffic](#before-customer-traffic).
**Prepared by:** Claude (FPS Implementer), 2026-05-29
**Tracks:** Issue #313
**Priority:** P0 customer-first deployment

---

## Purpose

This runbook describes how to deploy FairSpot on a NAS (Network-Attached Storage) device behind a Cloudflare Tunnel so it is reachable at a public HTTPS domain without opening any NAS ports directly to the Internet.

The result of following this runbook is:

- `app.<domain>` routed through Cloudflare to the Envoy API gateway on the NAS
- `auth.<domain>` routed through Cloudflare to the Keycloak identity provider on the NAS
- All internal service, Dapr, database, broker, and monitoring ports remaining private
- No committed secrets, tunnel tokens, or certificates in source control

This runbook is **not** a full client-owned production handoff. See [client-production-handoff.md](./client-production-handoff.md) for that. This profile exists to get the first customer pilot running from the current NAS-hosted stack.

---

## Architecture

```
Internet
    │
    ▼
Cloudflare Edge (DNS proxy, TLS, WAF, DDoS, rate limiting)
    │
    │   (encrypted outbound tunnel — no inbound port required)
    │
    ▼
cloudflared (Docker container on NAS)
    │
    ├─ app.<domain>  → http://envoy-proxy:10000  (Envoy API gateway)
    │
    └─ auth.<domain> → http://keycloak:8080      (Keycloak public login)
         (Keycloak admin console must NOT be published as a public hostname)

NAS Docker network (not reachable from Internet)
    ├─ envoy-proxy          :10000
    ├─ keycloak             :8080 (public login only)
    ├─ fps-booking          :5131
    ├─ fps-profile          :5197
    ├─ fps-notification     :5157
    ├─ fps-audit            :5161
    ├─ fps-reporting        :5171
    ├─ fps-configuration    :5141
    ├─ fps-identity         :5192
    ├─ mongodb              :27017  (internal only)
    ├─ rabbitmq             :5672 / :15672  (internal only)
    ├─ vault                :8200  (internal only)
    ├─ minio                :9000 / :9001  (internal only)
    ├─ prometheus           :9090  (internal only)
    ├─ grafana              :3000  (internal only; use Cloudflare Access if external access needed)
    └─ dapr sidecars        :3500 / :50001  (internal only)
```

Cloudflare Tunnel replaces the need for any inbound firewall hole or public IP. The `cloudflared` daemon opens an outbound encrypted connection from the NAS to the Cloudflare edge. Cloudflare terminates TLS for public visitors and routes traffic into the tunnel.

---

## Prerequisites

### NAS hardware

| Requirement | Minimum |
|---|---|
| OS | Linux-based NAS OS (Synology DSM 7+, QNAP QTS 5+, or Docker-capable Linux) |
| RAM | 8 GB or more available for the full stack |
| Docker | Docker Engine 24+ or equivalent NAS Docker package |
| Docker Compose | v2.20+ (or `docker compose` plugin) |
| Disk | 20 GB free for images, volumes, and logs |
| Outbound HTTPS | Outbound TCP 443 allowed from NAS to Cloudflare edge (Tunnel requirement) |

### Domain and Cloudflare account

- A domain managed through Cloudflare DNS (nameservers pointed to Cloudflare).
- A Cloudflare account with the domain active in it.
- Tunnel and WAF features require at minimum the Cloudflare Free plan. Rate limiting rules require Pro or above — see [SEC010](../security/cloudflare-waf-profile.md) (issue #315) for plan-dependent guidance.

### Local access

- SSH access to the NAS from a trusted operator machine.
- The FairSpot repository cloned on the NAS (or available via shared folder).

---

## Step 1 — Harden the NAS firewall

Before creating the tunnel, confirm no NAS service ports are exposed to the Internet.

**Required firewall state:**

| Port | Protocol | Rule |
|---|---|---|
| 22 | TCP | Allow inbound from trusted operator IPs only; block from 0.0.0.0/0 |
| 443 | TCP | Allow outbound to 0.0.0.0/0 (Tunnel needs outbound HTTPS) |
| 80 | TCP | Allow outbound only; block inbound unless operator explicitly needs it |
| 10000 | TCP | Block inbound; Envoy is accessed only through the Tunnel |
| 8080 | TCP | Block inbound; Keycloak is accessed only through the Tunnel |
| 27017 | TCP | Block inbound; MongoDB internal only |
| 5672 / 15672 | TCP | Block inbound; RabbitMQ internal only |
| 8200 | TCP | Block inbound; Vault internal only |
| 9000 / 9001 | TCP | Block inbound; MinIO internal only |
| 9090 | TCP | Block inbound; Prometheus internal only |
| 3000 | TCP | Block inbound; Grafana via Cloudflare Access if needed, not directly |
| 5131–5197 | TCP | Block inbound; FPS service ports internal only |
| 3500 / 50001 | TCP | Block inbound; Dapr ports internal only |

Apply these rules through the NAS firewall interface or Linux `iptables`/`ufw` before proceeding.

---

## Step 2 — Create the Cloudflare Tunnel

1. Log in to the [Cloudflare dashboard](https://dash.cloudflare.com).
2. Select the domain you are using for FairSpot.
3. Navigate to **Zero Trust** → **Networks** → **Tunnels** → **Create a tunnel**.
4. Choose **Cloudflared** as the connector type.
5. Name the tunnel `fps-nas` (or another operator-chosen name).
6. Copy the tunnel token shown on screen. This token is **never committed to source control**.
   Store it in a password manager or secret store immediately.
7. Keep the dashboard open — you will configure public hostnames in the next step.

---

## Step 3 — Configure public hostnames

In the Cloudflare tunnel configuration, add these public hostname entries. Replace `<domain>` with your actual domain.

| Public hostname | Type | URL | Notes |
|---|---|---|---|
| `app.<domain>` | HTTP | `http://envoy-proxy:10000` | FairSpot API gateway; public to authenticated users |
| `auth.<domain>` | HTTP | `http://keycloak:8080` | Keycloak public login only |

**Do not add a public hostname for:**
- Keycloak admin console (`/auth/admin`, `/admin`)
- Grafana, Prometheus, Alertmanager, Loki, Jaeger
- MongoDB, RabbitMQ, Vault, MinIO endpoints
- Dapr sidecar ports
- FPS service ports (5131–5197)

If operator access to Grafana is needed externally, add `ops.<domain>` behind a **Cloudflare Access** policy with email or OIDC authentication. Do not expose it publicly.

Ensure **Proxied** (orange cloud) is toggled on for all DNS records. This is required for Cloudflare TLS, WAF, and DDoS protection.

---

## Step 4 — Create the NAS environment file

The cloudflared service requires the tunnel token at runtime. Create a `.env.nas` file from the provided template. This file is listed in `.gitignore` and must never be committed.

```bash
cd /path/to/fps-repo/code/infrastructure
cp cloudflared/nas-env.template cloudflared/.env.nas
```

Edit `cloudflared/.env.nas` and set:

```
CLOUDFLARED_TUNNEL_TOKEN=<paste tunnel token here>
```

All other values in the template are placeholders that the operator must review and fill in before starting services. See the template comments for each value.

---

## Step 5 — Start cloudflared

The cloudflared service is defined as a Docker Compose override. Start it independently so it can be stopped or restarted without affecting the application stack.

```bash
cd /path/to/fps-repo/code/infrastructure
docker compose -f cloudflared/docker-compose.cloudflared.yml --env-file cloudflared/.env.nas up -d
```

Verify the tunnel is connected:

```bash
docker compose -f cloudflared/docker-compose.cloudflared.yml logs cloudflared
```

Look for a line like `Connection registered connIndex=0` or `Registered tunnel connection`. If the tunnel fails, check outbound HTTPS from the NAS and confirm the token is correct.

---

## Step 6 — Start the FairSpot stack

Start the application infrastructure (databases, broker, vault, keycloak, envoy, observability):

```bash
cd /path/to/fps-repo/code/infrastructure
docker compose up -d
```

Then start the .NET services with Dapr. On Linux the multi-app run file should be tested first:

```bash
cd /path/to/fps-repo
dapr run -f dapr.yaml
```

Or use the harness start script if available:

```bash
./tools/start-with-dapr.sh
```

For NAS hosting, apply the `docker-compose.nas.yml` overlay which adds `restart: unless-stopped` to every infrastructure service. The default `docker-compose.yaml` targets local development and has no restart policy so containers do not auto-start on developer machine reboots.

```bash
cd /path/to/fps-repo/code/infrastructure
docker compose -f docker-compose.yaml -f docker-compose.nas.yml up -d
```

Review and confirm that MongoDB, RabbitMQ, Vault, MinIO, Keycloak, and Envoy all have named volumes (not anonymous volumes) so data survives container restarts.

> **Note:** `cloudflared/docker-compose.cloudflared.yml` is a separate file that starts only the Cloudflare Tunnel connector. Run it independently with `--env-file cloudflared/.env.nas` as shown in Step 5.

---

## Step 7 — Configure OIDC for the public domain

> **Follow-up slice:** Full auth and gateway configuration is tracked in **OPS012** (issue #316). The steps below are the minimum required before smoke-testing the public domain.

After the stack is running, update the Keycloak realm to accept the new public domain:

1. Log in to the Keycloak admin console at `http://localhost:8080/admin` (local access only — not through the tunnel).
2. Select the `fps-local` realm (or the production realm).
3. Under **Realm settings** → **General**, update the **Frontend URL** to `https://auth.<domain>`.
4. Under each FairSpot client (e.g. `fps-web`, `fps-mobile`):
   - Add `https://app.<domain>/*` to **Valid redirect URIs**.
   - Add `https://app.<domain>` to **Web origins**.
   - Remove `http://localhost:*` redirect URIs that should not be active in the pilot (keep them for dev if using a separate realm).
5. Update `Auth:Authority` in each .NET service's environment or appsettings to `https://auth.<domain>/realms/<realm>`.
6. Confirm no CORS or cookie settings still reference `localhost` for the public pilot path.

See [OPS012](./nas-cloudflare-auth-profile.md) (issue #316) for the full auth and gateway profile once that slice is implemented.

---

## Step 8 — WAF and origin hardening

> **Follow-up slice:** Full WAF and rate-limiting policy is tracked in **SEC010** (issue #315).

Before customer traffic is allowed, complete the Cloudflare WAF configuration described in SEC010. At minimum before go-live:

- Enable the Cloudflare Free plan WAF rules (available on all plans) for the domain.
- Add a custom WAF rule to block requests to `/metrics`, `/dapr`, `/admin`, `/swagger`, `/openapi`, and Keycloak admin paths from the `app.<domain>` hostname.
- Set Cloudflare SSL/TLS mode to **Full** or **Full (strict)**.
- Enable **Bot Fight Mode** if available on your plan.
- Confirm that the Cloudflare **Always Use HTTPS** setting is enabled for both hostnames.

---

## Smoke check after deployment

After completing Steps 1–7, run the following checks before treating the NAS deployment as ready:

| Check | Command / URL | Expected result |
|---|---|---|
| Tunnel connected | `docker compose -f cloudflared/docker-compose.cloudflared.yml logs cloudflared` | `Registered tunnel connection` |
| App hostname reachable | `curl -I https://app.<domain>/openapi/v1.json` (from external machine) | HTTP 200 or 401 |
| Auth hostname reachable | `curl -I https://auth.<domain>/realms/<realm>/.well-known/openid-configuration` (from external machine) | HTTP 200 |
| Internal MongoDB not exposed | `curl -v https://<your-NAS-public-IP>:27017` | Connection refused or timeout |
| Internal Grafana not exposed | `curl -v https://app.<domain>:3000` | Connection refused or timeout |
| Local services healthy | `curl http://localhost:10000/openapi/v1.json` (on NAS) | HTTP 200 |

A complete hosted smoke script is tracked in **OPS013** (issue #314).

---

## Rollback

To stop public access without destroying data:

```bash
# Stop cloudflared — disconnects the tunnel; NAS services keep running
docker compose -f cloudflared/docker-compose.cloudflared.yml stop cloudflared
```

To fully tear down:

```bash
# Stop cloudflared
docker compose -f cloudflared/docker-compose.cloudflared.yml down

# Stop application stack
docker compose down

# (Keep volumes — data is preserved)
# docker compose down -v would remove all data
```

In the Cloudflare dashboard, a disabled or deleted tunnel removes DNS routing immediately.

---

## Reset (demo / pilot data)

To reset seeded data to a known state without stopping infrastructure:

```bash
./tools/demo-reset.sh
```

This drops and re-creates demo tenants, users, parking data, and booking history without restarting Docker containers. The tunnel and OIDC session state are unaffected by a data reset.

For a full environment reset (stop all services, clean volumes, restart clean):

```bash
docker compose down -v
docker compose up -d
# then re-seed:
./tools/dev-setup-auth.sh
./tools/dev-seed.sh
```

---

## Secrets and environment values

| Value | Where to store | Never do this |
|---|---|---|
| Cloudflare tunnel token | `.env.nas` on the NAS; operator's password manager | Commit to git; paste into any issue or PR |
| Keycloak admin password | NAS secrets manager or `.env.nas` | Hardcode in `docker-compose.yaml` |
| MongoDB passwords | Dapr secretstore (Vault) or `.env.nas` | Commit inline credentials |
| Vault root token | NAS secrets manager | Use the local dev token (`dev-only-token`) in any hosted environment |
| MinIO root credentials | `.env.nas` | Use default `minioadmin` credentials in pilot |
| JWT signing keys | Keycloak-managed; never exported | Commit key material |

The `.env.nas` file is in `.gitignore`. Verify this before your first commit on the NAS:

```bash
git check-ignore -v code/infrastructure/cloudflared/.env.nas
```

If the file is not ignored, do not proceed — add it to `.gitignore` first.

---

## Dapr component notes

The NAS pilot uses the local Dapr component set from `code/infrastructure/dapr/components/local/`. These components are unchanged from local development. For customer pilot use:

- MongoDB state-store components target the NAS-local MongoDB container. Named volumes ensure data persists across restarts.
- `workflowstore` is the shared Dapr actor state store required by Dapr Workflow.
- `fps-pubsub` uses RabbitMQ on the same Docker network.
- `secretstore` uses HashiCorp Vault in dev mode. **Dev mode Vault does not persist state across restarts.** Before customer traffic, either run Vault in server mode with a persistent volume or replace the secretstore component with a production-grade secret manager.

A Vault persistence upgrade and production-mode configuration are prerequisites for any customer data. Flag this as a blocker in the acceptance gate below.

---

## Before customer traffic

The following slices must be completed or explicitly deferred before allowing real customer access:

| # | Slice | Issue | Status | Required for customer traffic? |
|---|---|---|---|---|
| 1 | WAF custom rules, rate limiting, origin hardening | SEC010 #315 | Not started | **Yes** — block internal paths and protect login endpoints |
| 2 | Public-domain Keycloak/OIDC, Envoy CORS, redirect URIs | OPS012 #316 | Not started | **Yes** — auth cannot use localhost assumptions in production |
| 3 | Persistent tenant-scoped storage (Booking key gaps, in-memory repos) | DATA010 #317 | Not started | **Yes** — no customer data in evaluation-grade stores |
| 4 | Vault in production mode (not dev mode) | — | Not started | **Yes** — Vault dev mode loses secrets on restart |
| 5 | Hosted smoke/readiness evidence | OPS013 #314 | Not started | **Yes** — proof that the public domain works end-to-end |
| 6 | HR operations workspace | #310 | Not started | Recommended before HR users access the pilot |
| 7 | Administrator default workspace | #311 | Not started | Recommended before admin users access the pilot |
| 8 | Tenant onboarding hardening | CUST011 #319 | Not started | Required for production tenant creation |

**Do not allow external customer access** until items 1–5 are complete and evidenced.

---

## Document change log

| Date | Author | Change |
|---|---|---|
| 2026-05-29 | Claude | Initial runbook for issue #313 |
