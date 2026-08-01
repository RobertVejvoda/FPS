# Infrastructure Setup

This guide describes the lower-level Docker Compose infrastructure. For normal use, prefer the scenario scripts:

| Scenario | Command | Includes |
|---|---|---|
| Local full stack | `./tools/local-start.sh` | Local Docker backend, local seed, web dev server, Expo mobile dev server. No Cloudflare. |
| Local stop | `./tools/local-stop.sh` | Stops web/mobile and local Docker containers. |
| NAS Development | `./tools/deploy-nas.sh --tag sha-<commit> --existing-tunnel-container fairspot-cloudflared` | Pulls immutable images; renders the no-host-port profile; starts stores, migrations, services, Dapr, and observability; attaches the existing Tunnel; runs internal and exact-host public checks. Exact hosts come from ignored `nas.env`. |
| NAS stop | `./tools/deploy-nas.sh --down --existing-tunnel-container fairspot-cloudflared` | Stops FairSpot while preserving volumes; leaves the independently managed Tunnel running. Omit the existing-container option for a Compose-managed Tunnel. |

Local is the only profile allowed to use plain HTTP for browser/mobile testing. NAS and later hosted/cloud profiles are external hosting profiles and must use encrypted public communication through HTTPS.

The setup includes services like MongoDB, RabbitMQ, Vault, MinIO (S3), and others, with Dapr components configured for state store, pub/sub, and secret management.

---

## Prerequisites

1. **Docker**: Ensure Docker is installed and running on your system.
   - [Install Docker](https://docs.docker.com/get-docker/)
2. **Docker Compose v2.24+**: Required by hosted profiles for `!reset`/`!override` merge tags.
3. **Node.js/npm**: Required only when running local web and Expo mobile developer servers through `./tools/local-start.sh`.

The containerized local and NAS profiles do not require host-installed .NET or Dapr CLI. Dapr runs as containers managed by Docker Compose.

---

## Step 1: Create a Docker Network

The scenario scripts create the external Docker network automatically. To create it manually for low-level troubleshooting:

```bash
docker network create fairspot_network
```

---

## Step 2: Start the Infrastructure

Prefer the scenario script:

```bash
./tools/local-start.sh
```

For low-level infrastructure-only troubleshooting, start the Compose stack directly:

```bash
./tools/start-container-stack.sh --skip-e2e
```

This will start the following services:
- **Envoy Proxy**: Acts as a reverse proxy.
- **Vault**: Secret management service.
- **MinIO**: S3-compatible object storage.
- **RabbitMQ**: Message broker for pub/sub.
- **MongoDB**: NoSQL database for state store.
- **Jaeger**: Distributed tracing.
- **Prometheus**: Monitoring and alerting.
- **Grafana**: Visualization and monitoring dashboard.
- **Zipkin**: Distributed tracing.
- **Loki**: Log aggregation.

---

## Step 3: Local Secrets

The local Docker Compose profile runs Vault in development mode for repeatable local testing. This is not a production Vault setup. Demo and client-owned environments must use a real secret-management setup and must not reuse the local token.

1. **Access Vault UI**:
   - Open your browser and navigate to `http://localhost:8200`.
   - Use the token `dev-only-token` to log in.

2. **Configure the local Vault CLI session**:

```bash
export VAULT_ADDR=http://127.0.0.1:8200
export VAULT_TOKEN=dev-only-token
```

For convenience on a local-only development machine:

```bash
echo 'export VAULT_ADDR=http://127.0.0.1:8200' >> ~/.zshrc
echo 'export VAULT_TOKEN=dev-only-token' >> ~/.zshrc
source ~/.zshrc
```

3. **Component secrets**:

The local container profile seeds the Dapr component secrets through `vault-init`. Manual Vault writes are only needed when troubleshooting the local secret store directly.

```bash
vault status
```

---

## Vault Persistence

Local Vault runs in development mode. Secrets are local-only and may need to be re-seeded after the Vault container is recreated. The NAS/hosted profile uses `vault/config/vault.hcl`; the default local Docker Compose profile does not mount it because the official Vault entrypoint already loads `/vault/config` automatically.

If you switch away from dev mode, do not pass both `server` and an explicit `-config=/vault/config/...` argument while also mounting config under `/vault/config`; that loads the listener twice and fails with `bind: address already in use`.

---

## Step 4: Configure Dapr Components

Dapr components are configured in profile-specific directories:

- `dapr/components/local`: loaded by local Docker Compose and local Dapr runs.
- `dapr/components/demo`: templates for demo-hosted environments.
- `dapr/components/client`: templates for client-owned production.

The local logical component names are:

- `bookingstore`: MongoDB state store for Booking.
- `fairspot-pubsub`: RabbitMQ pub/sub for `booking-events`.
- `s3store`: MinIO/S3-compatible output binding.
- `secretstore`: Vault-backed secret store.

See `dapr/README.md` for the full component contract, app scoping rules, and provider-swap guidance.

---

## Step 5: Verify the Setup

1. **Check Running Containers**:
   Run the following command to ensure all containers are running:

   ```bash
   docker ps
   ```

2. **Run gateway health smoke**:

   ```bash
   ./tools/smoke-gateway-health.sh
   ```

3. **Access RabbitMQ**:
   Open your browser and navigate to `http://localhost:15672`. Use the credentials `admin/admin` to log in.

4. **Access MinIO**:
   Open your browser and navigate to `http://localhost:9001`. Use the credentials `minioadmin/minioadmin` to log in.

5. **Access Grafana**:
   Open your browser and navigate to `http://localhost:3001`. Use the credentials `admin/admin` to log in.
   The local host port defaults to `3001` so Docsify can use `3000`; Grafana still listens on `3000` inside the Docker network.
   If port `3001` is already in use, set a local override before starting the stack:

   ```bash
   FPS_GRAFANA_HOST_PORT=3002 ./tools/start-container-stack.sh --seed
   ```

   For a repeatable local override, add `FPS_GRAFANA_HOST_PORT=3002` to the ignored `code/infrastructure/local-docker.env` file.

6. **Access Zipkin**:
   Open your browser and navigate to `http://localhost:19411`. The container still listens on `9411` inside the Docker network, but the host port is moved to `19411` so it does not collide with Dapr's default local Zipkin on `9411`.

---

## NAS / Hosted Deployment

For NAS Development, put the exact app/auth/ops hostnames and all real values in
the ignored `nas.env`, then use the scenario script with an immutable image tag:

```bash
./tools/deploy-nas.sh \
  --tag sha-<full-commit> \
  --existing-tunnel-container fairspot-cloudflared
```

The wrapper is idempotent and preserves durable volumes. It prepares required
ignored bind-mount directories, checks the exact public web/OIDC contract,
renders a profile with zero host-published ports, pulls images, gates durable
Vault, runs the one-shot Mongo and DataHub migration jobs, verifies every
service and Dapr sidecar, attaches or starts `cloudflared`, and finally runs the
public smoke. `mongodb-init`, `fairspot-datahub-migrate`, and `vault-init`
finishing as `Exited (0)` means successful completion.

The DataHub job uses a finite compatibility launcher. Current images apply
migrations and exit through their explicit migration mode. When rolling back to
an image published before that mode existed, the launcher lets the image run
its existing Development startup migrations on container loopback, then stops
it after ASP.NET reaches listening state. The long-running DataHub service
always starts separately in Production only after that job succeeds.

If the Tunnel is managed by this repo instead, omit
`--existing-tunnel-container` and provide the ignored tunnel env file. The
Production-compatible `--domain fairspot.net` shorthand still derives
`app.fairspot.net` and `auth.fairspot.net`; Development should use explicit
`FPS_PUBLIC_APP_HOST=app-dev...` and `FPS_PUBLIC_AUTH_HOST=auth-dev...` values.

For internal troubleshooting only, the low-level stack can be started without
public probes. Pass the exact hosts so it still validates the hosted runtime
contract before mutation:

```bash
./tools/start-container-stack.sh --nas \
  --env-file code/infrastructure/nas.env \
  --app-host <app-host> \
  --auth-host <auth-host> \
  --skip-public-smoke
```

If exact hosts are intentionally omitted, first stop or disconnect every
Cloudflare Tunnel connector from `fairspot_network`. The script rejects an
unchecked hosted mutation while active ingress remains attached.

### CI/CD boundary

- `.github/workflows/ci.yml` builds/tests the code and renders/validates the NAS
  and DigitalOcean Compose profiles with placeholder values.
- `.github/workflows/publish-images.yml` builds all service/web images and
  publishes immutable `sha-<full-commit>` tags to GHCR.
- The NAS is the deployment runner: an operator selects a green immutable tag
  and runs `deploy-nas.sh` locally. GitHub-hosted runners receive no NAS,
  Cloudflare, Vault, or application credentials and do not connect to the NAS.

See `docs/production/nas-cloudflare-deployment-profile.md` for the full NAS deployment runbook.

---

## Step 6: Stop the Infrastructure

Prefer the scenario script:

```bash
./tools/local-stop.sh
```

To reset local data:

```bash
./tools/local-stop.sh --reset
```

---

## Local Full-Stack Testing

Use the local scenario script for the full local developer experience:

```bash
./tools/local-start.sh
```

This starts the local Docker backend, seeds local demo data, starts the web dev server, and starts Expo mobile. It does not use Cloudflare.

See `../../docs/production/local-test-harness.md` for:

- current service run commands;
- local service URLs;
- mobile gateway requirements;
- local harness direction for one-command full-stack smoke testing.

---

## Troubleshooting

- **Vault Not Accessible**:
  Ensure the Vault container is running and accessible at `http://localhost:8200`.

- **Dapr Component Errors**:
  Check the logs of the Dapr sidecar for any errors:

  ```bash
  docker logs <container-id>
  ```

  The old `whoami-dapr` sample sidecar has been removed. Use `./tools/smoke-gateway-health.sh`
  from the repository root to verify the local Envoy gateway against real FairSpot
  service `/health` endpoints.

- **RabbitMQ Connection Issues**:
  Verify that the RabbitMQ container is running and accessible at `http://localhost:15672`.

---

This guide keeps the low-level Compose setup visible, but day-to-day use should go through the scenario scripts.
