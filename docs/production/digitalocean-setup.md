# DigitalOcean Droplet Setup

Credential-free operator profile for running FairSpot on a single, hardened
**DigitalOcean Droplet** using the containerized stack — the same durable
stores, secret-store, backup/restore, and Cloudflare protections as the NAS
profile, without Kubernetes or application-code changes (#766).

This page is a public, **credential-free** operator runbook: prepared-host
prerequisites, directory/env setup, deploy, public smoke, operator access,
restart/stop/upgrade/rollback, backup, restore drill, and troubleshooting. It
contains **no** account IDs, tokens, SSH material, public IPs, backup locations,
real evidence, or provider pricing. Real-account provisioning (the Droplet, DNS,
tunnel credentials, reserved IPs, backup destinations) and **live operator
evidence** live in the private companion issue
[`RobertVejvoda/fairspot-platform#38`](https://github.com/RobertVejvoda/fairspot-platform/issues/38).

## How this profile is built

The DigitalOcean profile reuses the hardened NAS baseline and shared
Tunnel-only port suppression, then adds one Droplet convenience delta:

| Layer | File |
| --- | --- |
| Base stack | `code/infrastructure/docker-compose.yaml` |
| Image-mode services (no local build) | `code/infrastructure/docker-compose.services.images.yml` |
| Dapr sidecars | `code/infrastructure/docker-compose.dapr.yml` |
| NAS hardening (restart policies, durable Keycloak Postgres, server-mode Vault, enforced secrets) | `code/infrastructure/docker-compose.nas.yml`, `docker-compose.services.nas.yml` |
| **Shared Tunnel-only boundary (no host ports)** | `code/infrastructure/docker-compose.no-host-ports.yml` |
| **DigitalOcean delta (loopback-only Grafana)** | `code/infrastructure/docker-compose.digitalocean.yml` |

`tools/deploy-digitalocean.sh` and `tools/start-container-stack.sh --digitalocean`
assemble exactly these files. Backups and restores target the same profile via
`tools/backup-stack.sh --digitalocean` and `tools/restore-drill.sh --digitalocean`.

### The public boundary

The shared Tunnel-only overlay removes **every** host-port publish. The
DigitalOcean delta then adds back one operator convenience endpoint (Grafana)
on loopback only.
The only ingress is the outbound **Cloudflare Tunnel**, which reaches
`fairspot-web:80` and `keycloak:8080` over the internal Docker network — no
published host port is required. Container-to-container traffic uses service
names on that network. `tools/validate-digitalocean-profile.sh` renders the
merged profile and asserts no public port survives (default allowlist empty).

## 1. Prepared-host prerequisites

The deploy script never provisions or mutates the host or the DigitalOcean
account — it does not touch users, SSH, the firewall, disks, DNS, or reserved
IPs. Prepare the Droplet first (details + evidence: companion issue #38):

- **OS**: a current Ubuntu LTS Droplet.
- **Docker Engine 24+** and the **Docker Compose v2** plugin. The overlay uses
  Compose merge tags (`!reset`/`!override`), so a current Compose v2 is required.
- A **non-root operator user in the `docker` group** (the supported execution
  path; running as root warns).
- Enough disk for images plus durable data (the deploy preflight prints a
  low-space warning).
- **DigitalOcean Cloud Firewall + host firewall**: inbound should be limited to
  SSH from known admin addresses. No application, API, database, broker, Vault,
  MinIO, or observability port is opened — ingress is the Cloudflare Tunnel only.
- A **Cloudflare Tunnel** created in the Cloudflare dashboard, with the public
  hostnames routed to the internal services:
  - `app.<domain>` → `http://fairspot-web:80`
  - `auth.<domain>` → `http://keycloak:8080`

## 2. Directory and environment setup

```sh
git clone https://github.com/RobertVejvoda/fairspot.git
cd fairspot

# Operator stack secrets — reuses the hosted variable contract. Gitignored.
cp code/infrastructure/nas.env.example code/infrastructure/do.env
$EDITOR code/infrastructure/do.env        # fill every value

# Cloudflare Tunnel token — gitignored.
printf 'CLOUDFLARED_TUNNEL_TOKEN=<tunnel-token>\n' > code/infrastructure/cloudflared/.env.do
```

`do.env` and `cloudflared/.env.do` are git-ignored and must **never** be
committed. `do.env` uses the same variable contract as `nas.env.example` — there
is no second secret schema to drift. Set at least the store credentials
(`MONGO_*`, `POSTGRES_*`, `RABBITMQ_*`, `MINIO_*`), Keycloak (`KC_*`,
`FPS_APP_ORIGIN`), Grafana (`GRAFANA_*`), the public auth/app values
(`FPS_AUTH_AUTHORITY` as an `https://` URL, `FPS_PUBLIC_DOMAIN`), and `VAULT_TOKEN`.

### Public web runtime contract (`FPS_WEB_*`)

`nas.env.example` includes the explicit hostname and web-runtime keys but leaves
their environment-specific values blank. A DigitalOcean operator must fill
`FPS_PUBLIC_DOMAIN`, `KC_HOSTNAME`, `FPS_APP_ORIGIN`, `FPS_AUTH_AUTHORITY`, and
every required `FPS_WEB_*` value for the selected Droplet domain. Leave
`FPS_PUBLIC_APP_HOST`/`FPS_PUBLIC_AUTH_HOST` blank when using the
DigitalOcean `--domain` shorthand.

The
`fairspot-web` container entrypoint (`code/web/fps-web/docker-entrypoint.sh`)
reads them at startup to render its runtime `/config.json`. If
`FPS_WEB_API_BASE_URL` is unset, the container does **not** fail — it
silently serves the image's **baked default** `config.json`
(`http://localhost:10000`, a local Keycloak issuer, local callback URLs), and
the hosted preflight and public smoke reject a missing or inconsistent exact
contract. A `do.env` copied straight from `nas.env.example` and never edited
therefore fails before deployment.

`tools/deploy-digitalocean.sh` closes this gap: for a normal public deploy (no
`--skip-public`), preflight fails if any of the following are blank, or
inconsistent with `--domain`/`FPS_AUTH_AUTHORITY`:

| Variable | Required value for domain `<domain>` |
| --- | --- |
| `FPS_WEB_API_BASE_URL` | `https://app.<domain>/api` — single-origin model, nginx proxies `/api/` to Envoy |
| `FPS_WEB_OIDC_AUTHORITY` | Must equal `FPS_AUTH_AUTHORITY` — the same public issuer the APIs validate |
| `FPS_WEB_OIDC_CLIENT_ID` | Non-empty (e.g. `fps-web`) |
| `FPS_WEB_OIDC_REDIRECT_URI` | Exactly `https://app.<domain>/auth/callback` — a same-origin path that isn't this exact value is still rejected |
| `FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI` | Exactly `https://app.<domain>/` |

Add these five lines to `do.env` after copying the template — substitute your
own domain, and match the realm path if `FPS_AUTH_AUTHORITY` differs:

```sh
FPS_WEB_API_BASE_URL=https://app.<domain>/api
FPS_WEB_OIDC_AUTHORITY=https://auth.<domain>/realms/fairspot
FPS_WEB_OIDC_CLIENT_ID=fps-web
FPS_WEB_OIDC_REDIRECT_URI=https://app.<domain>/auth/callback
FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI=https://app.<domain>/
```

Full variable reference: [GHCR image publishing](ghcr-image-publishing.md#environment-variables-image-mode).

## 3. Deploy

```sh
./tools/deploy-digitalocean.sh --domain <your-domain> --tag sha-<commit>
```

The entry point runs a **non-mutating, fail-closed preflight** before starting
anything, with DigitalOcean-specific errors: Docker/Compose present; `do.env`
and the tunnel env file exist; a public domain and `https://` auth authority are
set; the [public web runtime contract](#public-web-runtime-contract-fps_web_)
(`FPS_WEB_*`) is present and consistent with the domain/auth authority; an
**immutable** image tag is pinned (a missing or `latest` tag is rejected
unless you pass `--allow-latest`, which is not valid for evidence); disk-space
guidance; and a **public-boundary render check** that refuses to deploy if any
service would publish a public port. Only then does it check the daemon, start
the stack, start the Cloudflare Tunnel connector, and run the public smoke.

**First boot — Vault.** Server-mode Vault starts sealed/uninitialized on a clean
volume. The start script pauses with the one-time init/unseal instructions:

```sh
docker compose --project-directory code/infrastructure --env-file code/infrastructure/do.env \
  -f docker-compose.yaml -f docker-compose.services.images.yml -f docker-compose.dapr.yml \
  -f docker-compose.nas.yml -f docker-compose.services.nas.yml \
  -f docker-compose.no-host-ports.yml -f docker-compose.digitalocean.yml \
  exec vault vault operator init         # record unseal shares + root token OUT OF BAND
# ... unseal (3 shares), enable the kv-v2 secret path, provision a least-privilege
#     token, set VAULT_TOKEN in do.env, then re-run deploy. See the NAS runbook.
```

Store unseal shares and the root token out of band (never in Git or `do.env`
history). This mirrors the [NAS deployment profile](nas-cloudflare-deployment-profile.md).

## 4. Public smoke

The deploy runs `tools/start-container-stack.sh --digitalocean --domain <domain>`,
which probes (Docker-only, no host tooling): the web SPA entry point and runtime
`config.json`, API health through the web `/api` proxy → Envoy, Keycloak OIDC
discovery on `auth.<domain>`, that the Keycloak admin console and internal/
diagnostic paths are **not** publicly served, and edge rate limits (plan-
dependent). Re-run it any time — use `--smoke-only` so a routine recheck never
pulls or redeploys (it only checks/probes the already-running stack; without
this flag the same command pulls and re-`up`s, and without `--tag` that can
silently replace a previously pinned release with `latest`):

```sh
./tools/start-container-stack.sh --digitalocean --domain <your-domain> --smoke-only
```

## 5. Operator access (no public dashboards)

No dashboard is public. Grafana is bound to **loopback only**; reach it through
an SSH tunnel from your workstation:

```sh
ssh -L 3001:127.0.0.1:3001 <droplet>     # then open http://localhost:3001
```

Grafana's external root URL follows the loopback-published host port (default
`3001`). If `FPS_GRAFANA_HOST_PORT` is changed, Compose derives the matching
root URL; `FPS_GRAFANA_ROOT_URL` remains available for an explicit external URL.

Reach a store or Vault the same way (`docker compose ... exec <service> …` over
SSH). Prefer Cloudflare Access for any operator surface exposed through the edge.

## 6. Restart, stop, upgrade, rollback

- **Restart / reboot survival**: every service runs `restart: unless-stopped`,
  so the stack returns after a Droplet reboot. Server-mode Vault must be
  **unsealed** after each restart before secrets are readable.
- **Stop (preserve data)**: `./tools/deploy-digitalocean.sh --down` stops the
  stack and keeps all durable volumes. The tunnel connector is stopped
  separately (the command is printed).
- **Upgrade**: redeploy a newer immutable tag — images are pulled, never built
  on the Droplet, and durable volumes are preserved:
  ```sh
  ./tools/deploy-digitalocean.sh --domain <your-domain> --tag sha-<newer-commit>
  ```
- **Rollback**: redeploy a **previous** immutable tag the same way. Because the
  data volumes are preserved, rollback swaps images without touching state.
  Before executing it, compare
  `code/server/DataHub/FPS.DataHub/Infrastructure/Migrations/` between the
  previous and current commits. If migrations differ, confirm the previous
  image can read the current schema or select a verified pre-migration restore
  point; do not run the command while that decision is unresolved:
  ```sh
  ./tools/deploy-digitalocean.sh --domain <your-domain> --tag sha-<previous-commit>
  ```
  The finite DataHub migration launcher also supports images published before
  explicit migration-and-exit mode by running their existing Development
  startup migrations on container loopback and stopping them after they reach
  listening state. A schema-incompatible rollback may still need a restore —
  see the restore drill.

## 7. Backup

```sh
./tools/backup-stack.sh --digitalocean               # add --quiesce for a consistent snapshot
```

Backs up MongoDB, DataHub Postgres, Keycloak Postgres, MinIO, and a native Vault
raft snapshot, with a `SHA256SUMS` integrity file and `manifest.json`. In the
hosted profile a Vault it cannot snapshot is a **hard failure** (never a
misleading live-directory tar). Generated backups are git-ignored — schedules,
encrypted off-box copies, and storage locations are private-operator concerns
(companion #38). Full model: [Backup and Restore](backup-restore.md) and the
[encryption/backup readiness gate](nas-encryption-backup-evidence.md).

## 8. Restore drill

```sh
./tools/restore-drill.sh --from <backup-dir> --digitalocean --force-digitalocean --yes
```

**DESTRUCTIVE.** It wipes the stack + volumes, rebuilds the data/object/identity
stores from the backup, and asserts that data returned. The DigitalOcean profile
requires its **own** force flag — `--force-digitalocean` — and never falls
through to `--nas` or local behavior; `--yes` is also required.

The command deliberately **defers the full-stack smoke** at the Vault boundary:
after the volume wipe, server-mode Vault is sealed/uninitialized. Complete the
human-supervised Vault DR steps printed by the drill (init/unseal/snapshot
restore/re-unseal), start the stack, and run the hosted smoke manually. Do not
rerun the destructive restore drill after recovering Vault. Record both the
data assertions and the subsequent manual smoke in the private runbook
(companion #38).

## 9. Troubleshooting

| Symptom | Check |
| --- | --- |
| Preflight: "cannot talk to the Docker daemon" | Run as a user in the `docker` group; confirm the daemon is up. |
| Preflight: "requires an immutable image tag" | Pass `--tag sha-<commit>` (or a `v*` tag). `latest` needs `--allow-latest` and is not valid for evidence. |
| Preflight: "missing public web runtime setting" / "does not match" | Fill every `FPS_WEB_*` value in `do.env` and match it to `--domain`/`FPS_AUTH_AUTHORITY` — see [Public web runtime contract](#public-web-runtime-contract-fps_web_). |
| Preflight: "the merged Compose profile did not render" | A required value is missing in `do.env`, or Compose is too old for the overlay's merge tags — update Compose v2. |
| Preflight: "refusing to deploy — a service would publish a PUBLIC host port" | The overlay/base drifted; run `./tools/validate-digitalocean-profile.sh` and restore the suppression. |
| Vault "SEALED" / "UNINITIALIZED" on start | Unseal (or one-time init) as printed; re-run deploy. |
| `app.<domain>` unreachable | Confirm the Cloudflare Tunnel connector is running and the hostnames route to `fairspot-web:80` / `keycloak:8080`. |
| Keycloak admin reachable publicly | A Cloudflare hostname/WAF rule is missing — see [Cloudflare WAF and edge](nas-cloudflare-deployment-profile.md). |

## Beyond this profile (deferred, target shape)

The single-Droplet profile is the starting point. Managed services are evaluated
only when a concrete durability or evidence need exists — none are implemented
here:

| Area | Direction |
| --- | --- |
| Orchestration | DOKS/Kubernetes only if cluster-level operation or evidence is required. |
| Registry | GHCR remains acceptable; DigitalOcean Container Registry optional. |
| Ingress | Cloudflare Tunnel by default; a DigitalOcean Load Balancer only if a concrete need is found. |
| State stores | Self-hosted first; DigitalOcean Managed Databases / Spaces only when they improve durability or evidence. |
| Backups | Service-level backups + restore drill first; Droplet snapshots and managed-DB backups as state moves. |

## Non-goals

- No DOKS/Kubernetes, Terraform/`doctl` account provisioning, DigitalOcean API
  automation, managed databases/Spaces/Container Registry, or Load Balancer.
- No application-service changes or provider-specific application code.
- No secrets, account/zone/project IDs, SSH material, public IPs, backup
  locations, real evidence, or provider pricing in this repository.

## References

- [NAS/Cloudflare deployment profile](nas-cloudflare-deployment-profile.md) — the hardened baseline this profile reuses.
- [Backup and Restore](backup-restore.md) · [Encryption/backup readiness gate](nas-encryption-backup-evidence.md) · [Hosted smoke runbook](hosted-smoke-runbook.md) · [Release pipeline](release-pipeline.md).
- Private live provisioning + operator evidence: [`fairspot-platform#38`](https://github.com/RobertVejvoda/fairspot-platform/issues/38).
- [DigitalOcean Droplets](https://docs.digitalocean.com/products/droplets/) · [Cloud Firewalls](https://docs.digitalocean.com/products/networking/firewalls/) · [Kubernetes](https://docs.digitalocean.com/products/kubernetes/) · [Managed Databases](https://docs.digitalocean.com/products/databases/) · [Spaces](https://docs.digitalocean.com/products/spaces/).
