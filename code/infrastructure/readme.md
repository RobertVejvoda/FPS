# Infrastructure Setup - Local Docker Environment

This guide provides step-by-step instructions to set up the infrastructure for the FPS project using Docker Compose. The setup includes services like MongoDB, RabbitMQ, Vault, MinIO (S3), and others, with Dapr components configured for state store, pub/sub, and secret management.

---

## Prerequisites

1. **Docker**: Ensure Docker is installed and running on your system.
   - [Install Docker](https://docs.docker.com/get-docker/)

2. **Dapr CLI**: Install the Dapr CLI to manage Dapr components.
   - [Install Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)

3. **Vault CLI** (optional): Install the Vault CLI for managing secrets.
   - [Install Vault CLI](https://developer.hashicorp.com/vault/downloads)

---

## Step 1: Create a Docker Network

Create an external Docker network to allow containers to communicate with each other.

```bash
docker network create fps_network
```

---

## Step 2: Start the Infrastructure

Run the following command to start all services defined in the `docker-compose.yaml` file:

```bash
docker compose up -d
```

From the repository root, use:

```bash
docker compose -f code/infrastructure/docker-compose.yaml up -d
```

This will start the following services:
- **Envoy Proxy**: Acts as a reverse proxy.
- **Whoami**: A simple service to test Dapr integration.
- **Vault**: Secret management service.
- **MinIO**: S3-compatible object storage.
- **RabbitMQ**: Message broker for pub/sub.
- **MongoDB**: NoSQL database for state store.
- **PostgreSQL**: Relational database.
- **Jaeger**: Distributed tracing.
- **Prometheus**: Monitoring and alerting.
- **Grafana**: Visualization and monitoring dashboard.
- **Zipkin**: Distributed tracing.
- **Loki**: Log aggregation.

---

## Step 3: Configure Vault for Local Secrets

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

3. **Store local component secrets**:

```bash
vault kv put secret/vault-token token="dev-only-token"
vault kv put secret/mongodb-credentials username="admin" password="admin"
vault kv put secret/rabbitmq-credentials username="admin" password="admin"
vault kv put secret/minio-credentials accessKey="minioadmin" secretKey="minioadmin"
```

OPS001 local components use the `secretstore` component with `vaultKVPrefix: dapr`.
When using the profile-based components, store these secret names under the `secret/dapr/`
prefix or set equivalent values through your local Vault management flow.

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

2. **Test Dapr Components**:
   Use the Dapr CLI to test the components. For example, to test the state store:

   ```bash
   dapr run --app-id fps-booking --components-path ./dapr/components/local --dapr-http-port 3500
   curl -X POST http://localhost:3500/v1.0/state/bookingstore -H "Content-Type: application/json" -d '[{"key":"test-key","value":"test-value"}]'
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

## Step 6: Stop the Infrastructure

To stop all running containers, use the following command:

```bash
docker compose down
```

From the repository root, use:

```bash
docker compose -f code/infrastructure/docker-compose.yaml down
```

---

## Local Full-Stack Testing

This Docker Compose setup starts shared dependencies, not the whole FPS application stack. Run the .NET services from source for service-level checks, or use the local harness when it exists.

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

  The `whoami-dapr` sidecar is only a local Dapr smoke service. Application components such as `fps-pubsub` and `s3store` are scoped to the real FPS service app IDs so the sample sidecar does not require RabbitMQ or MinIO credentials.

- **RabbitMQ Connection Issues**:
  Verify that the RabbitMQ container is running and accessible at `http://localhost:15672`.

---

This guide ensures that your local infrastructure is set up securely and integrates seamlessly with Dapr components. Let me know if you need further assistance!
