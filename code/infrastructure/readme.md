# Infrastructure Setup

This guide describes the lower-level Docker Compose infrastructure. For normal use, prefer the scenario scripts:

| Scenario | Command | Includes |
|---|---|---|
| Local full stack | `./tools/local-start.sh` | Local Docker backend, local seed, web dev server, Expo mobile dev server. No Cloudflare. |
| Local stop | `./tools/local-stop.sh` | Stops web/mobile and local Docker containers. |
| NAS hosted | `./tools/nas-start.sh --domain fairspot.net` | NAS Docker runtime, auth, gateway/web/API, observability, Dapr, state stores, Cloudflare Tunnel, HTTPS public checks. |
| NAS stop | `./tools/nas-stop.sh` | Stops Cloudflare Tunnel and NAS Docker runtime. |

Local is the only profile allowed to use plain HTTP for browser/mobile testing. NAS and later hosted/cloud profiles are external hosting profiles and must use encrypted public communication through HTTPS.

The setup includes services like MongoDB, RabbitMQ, Vault, MinIO (S3), and others, with Dapr components configured for state store, pub/sub, and secret management.

---

## Prerequisites

1. **Docker**: Ensure Docker is installed and running on your system.
   - [Install Docker](https://docs.docker.com/get-docker/)
2. **Docker Compose v2**: Required by the local and NAS scenario scripts.
3. **Node.js/npm**: Required only when running local web and Expo mobile developer servers through `./tools/local-start.sh`.

The containerized local and NAS profiles do not require host-installed .NET or Dapr CLI. Dapr runs as containers managed by Docker Compose.

---

## Step 1: Create a Docker Network

The scenario scripts create the external Docker network automatically. To create it manually for low-level troubleshooting:

```bash
docker network create fps_network
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

Local Vault runs in development mode. Secrets are local-only and may need to be re-seeded after the Vault container is recreated. The checked-in `vault/config/vault-config.json` is retained for future non-dev local experiments, but it is not mounted by the default Docker Compose profile because the official Vault entrypoint already loads `/vault/config` automatically.

If you switch away from dev mode, do not pass both `server` and an explicit `-config=/vault/config/...` argument while also mounting config under `/vault/config`; that loads the listener twice and fails with `bind: address already in use`.

---

## Step 4: Configure Dapr Components

Dapr components are configured in profile-specific directories:

- `dapr/components/local`: loaded by local Docker Compose and local Dapr runs.
- `dapr/components/demo`: templates for demo-hosted environments.
- `dapr/components/client`: templates for client-owned production.

The local logical component names are:

- `bookingstore`: MongoDB state store for Booking.
- `fps-pubsub`: RabbitMQ pub/sub for `booking-events`.
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
   Open your browser and navigate to `http://localhost:3000`. Use the credentials `admin/admin` to log in.

6. **Access Zipkin**:
   Open your browser and navigate to `http://localhost:19411`. The container still listens on `9411` inside the Docker network, but the host port is moved to `19411` so it does not collide with Dapr's default local Zipkin on `9411`.

---

## NAS / Hosted Deployment

For NAS or hosted deployments, use the scenario script. Cloudflare Tunnel is part of the NAS hosted profile:

```bash
./tools/nas-start.sh --domain fairspot.net
```

For internal troubleshooting only, the low-level stack can be started without public checks:

```bash
./tools/start-container-stack.sh --nas --env-file code/infrastructure/nas.env --skip-e2e
```

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
