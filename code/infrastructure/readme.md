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
docker-compose up -d
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

## Step 3: Configure Vault for Secrets Management

Vault is used to securely store credentials for Dapr components. Follow these steps to set up Vault:

1. **Access Vault**:
   - Open your browser and navigate to `http://localhost:8200`.
   - Use the token `dev-only-token` to log in.

2. **Enable KV Secrets Engine**:
   Run the following command to enable the KV secrets engine:

   ```bash
   vault secrets enable -path=secret kv
   ```

3. **Store Secrets**:
   Add the required secrets for Dapr components:


```bash
export VAULT_ADDR=http://127.0.0.1:8200
export VAULT_TOKEN=dev-only-token
```

or persist in file...

```bash
echo 'export VAULT_ADDR=http://127.0.0.1:8200' >> ~/.zshrc
echo 'export VAULT_TOKEN=dev-only-token' >> ~/.zshrc
source ~/.zshrc
```

```bash
vault kv put secret/vault-token token="dev-only-token"
vault kv put secret/mongodb-credentials username="admin" password="admin"
vault kv put secret/rabbitmq-credentials username="admin" password="admin"
vault kv put secret/minio-credentials accessKey="minioadmin" secretKey="minioadmin"
```

```bash
vault status
```

---

## Persisting Vault Data

To ensure that Vault data is not lost when the container restarts, the `vault` service is configured to use the `file` storage backend. The data is stored in the `./vault/data` directory on the host machine.

### Steps to Verify Persistence

1. **Store a Secret**:
   Use the Vault CLI to store a secret:

   ```bash
   export VAULT_ADDR=http://127.0.0.1:8200
   export VAULT_TOKEN=root

   vault kv put secret/test-key value="test-value"
   ```

---

## Step 4: Configure Dapr Components

Dapr components are configured in the `dapr/components` directory. Below are the key components:

### MongoDB State Store

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.mongodb
  version: v1
  metadata:
  - name: host
    value: mongodb:27017
  - name: databaseName
    value: fps
  - name: username
    secretKeyRef:
      name: mongodb-credentials
      key: username
  - name: password
    secretKeyRef:
      name: mongodb-credentials
      key: password
auth:
  secretStore: vault
```

### RabbitMQ Pub/Sub

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.rabbitmq
  version: v1
  metadata:
  - name: host
    value: amqp://rabbitmq:5672
  - name: username
    secretKeyRef:
      name: rabbitmq-credentials
      key: username
  - name: password
    secretKeyRef:
      name: rabbitmq-credentials
      key: password
auth:
  secretStore: vault
```

### MinIO (S3-Compatible Storage)

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: s3store
spec:
  type: bindings.aws.s3
  version: v1
  metadata:
  - name: accessKey
    secretKeyRef:
      name: minio-credentials
      key: accessKey
  - name: secretKey
    secretKeyRef:
      name: minio-credentials
      key: secretKey
  - name: bucket
    value: fps-bucket
  - name: endpoint
    value: http://minio:9000
auth:
  secretStore: vault
```

### Vault Secret Store

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: vault
spec:
  type: secretstores.hashicorp.vault
  version: v1
  metadata:
  - name: vaultAddr
    value: http://vault:8200
  - name: token
    secretKeyRef:
      name: vault-token
      key: token
auth:
  secretStore: vault
```

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
   dapr run --app-id test-app --components-path ./dapr/components --dapr-http-port 3500
   curl -X POST http://localhost:3500/v1.0/state/statestore -H "Content-Type: application/json" -d '[{"key":"test-key","value":"test-value"}]'
   ```

3. **Access RabbitMQ**:
   Open your browser and navigate to `http://localhost:15672`. Use the credentials `admin/admin` to log in.

4. **Access MinIO**:
   Open your browser and navigate to `http://localhost:9001`. Use the credentials `minioadmin/minioadmin` to log in.

5. **Access Grafana**:
   Open your browser and navigate to `http://localhost:3000`. Use the credentials `admin/admin` to log in.

---

## Step 6: Stop the Infrastructure

To stop all running containers, use the following command:

```bash
docker-compose down
```

---

## Troubleshooting

- **Vault Not Accessible**:
  Ensure the Vault container is running and accessible at `http://localhost:8200`.

- **Dapr Component Errors**:
  Check the logs of the Dapr sidecar for any errors:

  ```bash
  docker logs <container-id>
  ```

- **RabbitMQ Connection Issues**:
  Verify that the RabbitMQ container is running and accessible at `http://localhost:15672`.

---

This guide ensures that your local infrastructure is set up securely and integrates seamlessly with Dapr components. Let me know if you need further assistance!

