# OPS001 Component Replacement Runbook

This runbook provides step-by-step procedures for replacing Dapr components when moving between deployment profiles (local → demo → production) or when migrating providers within a profile.

## Quick Reference

**When you need this runbook:**
- Setting up demo environment from local components
- Onboarding a client with their approved cloud provider
- Migrating between cloud providers (Azure ↔ AWS ↔ GCP)
- Moving from managed service to self-hosted (or vice versa)
- Changing regions for GDPR or data residency compliance

**Component boundaries that can be swapped:**
- Pub/Sub: RabbitMQ ↔ Azure Service Bus ↔ AWS SNS/SQS ↔ GCP Pub/Sub ↔ Kafka
- State Store: Local MongoDB ↔ MongoDB Atlas ↔ Cosmos DB ↔ self-hosted replica set
- Secret Store: Local Vault ↔ Azure Key Vault ↔ AWS Secrets Manager ↔ GCP Secret Manager
- Bindings: MinIO ↔ Azure Blob ↔ AWS S3 ↔ GCP Cloud Storage

**What stays the same:** Application code using Dapr APIs.

**What changes:** Component YAML files, credentials in secret store, infrastructure provisioning.

---

## Pre-Requisites

Before starting component replacement:

1. **Backup current state:**
   - Export current database (if replacing state store)
   - Archive current component YAML files
   - Document current connection strings and credentials
   - Capture current metrics baseline (latency, error rate, throughput)

2. **Provision target infrastructure:**
   - Create new managed service or deploy self-hosted infrastructure
   - Configure region, tier, replication, backup policies
   - Set up workload identity (Azure MI, AWS IRSA, GCP Workload Identity) if available
   - Create service principal or access keys as fallback
   - Test connectivity from target environment

3. **Prepare secret store:**
   - Store new credentials in target secret store
   - Verify secret store component is deployed and accessible
   - Test secret retrieval via Dapr secret store API

4. **Verify Dapr version compatibility:**
   - Check Dapr component reference for target component type
   - Ensure Dapr runtime version supports target component
   - Review breaking changes in Dapr release notes

5. **Schedule maintenance window (production only):**
   - Coordinate with client stakeholders
   - Plan for potential message backlog during cutover
   - Prepare rollback procedure

---

## Procedure: Replacing Pub/Sub Component

**Example:** RabbitMQ (local) → Azure Service Bus (demo/production)

### Step 1: Provision Azure Service Bus

```bash
# Azure CLI example
az servicebus namespace create \
  --name fps-demo-servicebus \
  --resource-group fps-demo-rg \
  --location westeurope \
  --sku Standard

# Create managed identity for workload authentication
az identity create \
  --name fps-demo-mi \
  --resource-group fps-demo-rg

# Grant Service Bus permissions to managed identity
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Azure Service Bus Data Owner" \
  --scope /subscriptions/<sub-id>/resourceGroups/fps-demo-rg/providers/Microsoft.ServiceBus/namespaces/fps-demo-servicebus
```

### Step 2: Update Component YAML

Create `demo/azure-servicebus-pubsub.yaml`:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: rabbitmq-pubsub  # Keep same name for app compatibility
spec:
  type: pubsub.azure.servicebus.topics
  version: v1
  metadata:
  - name: namespaceName
    value: "fps-demo-servicebus.servicebus.windows.net"
  - name: azureClientId
    value: "<managed-identity-client-id>"
  - name: consumerID
    value: "fps-{service-name}"
auth:
  secretStore: azurekeyvault
```

**Important:** Keep component `metadata.name` the same as the old component so application code does not need to change.

### Step 3: Deploy and Validate

**For Azure Container Apps:**
```bash
# Update container app with new component
az containerapp dapr enable \
  --name fps-booking \
  --resource-group fps-demo-rg \
  --dapr-app-id fps-booking \
  --dapr-app-port 8080

# Verify component loaded
az containerapp logs show \
  --name fps-booking \
  --resource-group fps-demo-rg \
  --follow
```

**For Kubernetes:**
```bash
kubectl apply -f demo/azure-servicebus-pubsub.yaml

# Check Dapr component status
kubectl get components -n fps

# Check sidecar logs
kubectl logs <pod-name> -c daprd -n fps
```

### Step 4: Test Pub/Sub Flow

```bash
# Publish test message
curl -X POST http://localhost:3500/v1.0/publish/rabbitmq-pubsub/booking.requested \
  -H "Content-Type: application/json" \
  -d '{"bookingId":"test-123","tenantId":"demo"}'

# Check Azure Service Bus explorer for message delivery
# Verify consumer received and processed message
# Check application logs for successful handler execution
```

### Step 5: Monitor and Validate

- Check Service Bus metrics in Azure Portal (incoming/outgoing messages, dead letters)
- Verify application logs show successful pub/sub operations
- Monitor latency and error rate in observability dashboard
- Run end-to-end smoke tests (booking submission → notification delivery)

### Step 6: Decommission RabbitMQ

**Wait for message backlog to drain:**
```bash
# Check RabbitMQ queue depth
curl -u admin:admin http://localhost:15672/api/queues
```

Once queues empty and traffic confirmed on new broker:
```bash
# Stop RabbitMQ container
docker stop rabbitmq

# Or delete Azure Container Apps component reference
# (Keep infrastructure for rollback period)
```

---

## Procedure: Replacing State Store Component

**Example:** Local MongoDB → MongoDB Atlas (demo/production)

### Step 1: Provision MongoDB Atlas Cluster

1. Create MongoDB Atlas account and organization
2. Create cluster:
   - Region: EU (West Europe) for GDPR compliance
   - Tier: M0 (free) for demo, M2/M5 for light production
   - Version: MongoDB 6.0 or later
3. Configure network access:
   - Whitelist IP ranges for demo environment
   - Or use Private Link/VPC peering for production
4. Create database user with read/write permissions
5. Get connection string

### Step 2: Store Credentials in Secret Store

**For Azure Key Vault:**
```bash
az keyvault secret set \
  --vault-name fps-demo-kv \
  --name mongodb-atlas-connectionstring \
  --value "mongodb+srv://<user>:<password>@<cluster>.mongodb.net/?retryWrites=true&w=majority"
```

### Step 3: Update Component YAML

Create `demo/mongodb-atlas-statestore.yaml`:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: bookingstore  # Keep same name
spec:
  type: state.mongodb
  version: v1
  metadata:
  - name: host
    value: "<cluster-name>.mongodb.net"
  - name: databaseName
    value: fps-demo
  - name: collectionName
    value: default
  - name: username
    secretKeyRef:
      name: mongodb-atlas-credentials
      key: username
  - name: password
    secretKeyRef:
      name: mongodb-atlas-credentials
      key: password
  - name: params
    value: "?retryWrites=true&w=majority&ssl=true"
auth:
  secretStore: azurekeyvault
```

### Step 4: Migrate Existing Data (Optional)

**If preserving local development data:**
```bash
# Export from local MongoDB
mongodump --host localhost:27017 --username admin --password admin --db fps --out ./backup

# Import to MongoDB Atlas
mongorestore --uri "mongodb+srv://<user>:<password>@<cluster>.mongodb.net" --db fps-demo ./backup/fps
```

**Production note:** Client production data migration requires documented backup/restore procedure, validation, and rollback plan.

### Step 5: Deploy and Validate

```bash
# Deploy updated component
kubectl apply -f demo/mongodb-atlas-statestore.yaml

# Test state operations
curl -X POST http://localhost:3500/v1.0/state/bookingstore \
  -H "Content-Type: application/json" \
  -d '[{"key":"test","value":"hello"}]'

curl http://localhost:3500/v1.0/state/bookingstore/test
```

### Step 6: Verify Collection-Per-Tenant Pattern

```bash
# Connect to MongoDB Atlas
mongosh "mongodb+srv://<cluster>.mongodb.net/fps-demo" --username <user>

# Verify tenant collections exist
show collections
# Should see: acmecorp_bookings, betainc_bookings, etc.

# Verify indexes
db.acmecorp_bookings.getIndexes()
```

---

## Procedure: Replacing Secret Store Component

**Example:** Local Vault (dev mode) → Azure Key Vault (demo/production)

### Step 1: Provision Azure Key Vault

```bash
az keyvault create \
  --name fps-demo-kv \
  --resource-group fps-demo-rg \
  --location westeurope \
  --enable-rbac-authorization true

# Grant Key Vault Secrets Officer to managed identity
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Key Vault Secrets Officer" \
  --scope /subscriptions/<sub-id>/resourceGroups/fps-demo-rg/providers/Microsoft.KeyVault/vaults/fps-demo-kv
```

### Step 2: Migrate Secrets

```bash
# Export secrets from local Vault
vault kv get -format=json secret/mongodb-credentials > mongodb-creds.json
vault kv get -format=json secret/rabbitmq-credentials > rabbitmq-creds.json

# Import to Azure Key Vault
az keyvault secret set --vault-name fps-demo-kv --name mongodb-credentials \
  --value "$(cat mongodb-creds.json | jq -r '.data.data | to_entries | map("\(.key)=\(.value)") | join(";")')"

# Or set individual secrets
az keyvault secret set --vault-name fps-demo-kv --name mongodb-username --value admin
az keyvault secret set --vault-name fps-demo-kv --name mongodb-password --value <password>
```

### Step 3: Update Secret Store Component

**Note:** Secret store component updates may require manual review due to security hooks.

Create `demo/azure-keyvault-secretstore.yaml` with managed identity authentication.

### Step 4: Update Dependent Components

All components with `secretKeyRef` must now point to the new secret store:

```yaml
auth:
  secretStore: azurekeyvault  # Changed from 'vault'
```

### Step 5: Validate Secret Access

```bash
# Test secret retrieval via Dapr
curl http://localhost:3500/v1.0/secrets/azurekeyvault/mongodb-username

# Check Dapr sidecar logs for secret store connection
kubectl logs <pod-name> -c daprd | grep secretstore
```

---

## Procedure: Replacing Bindings Component

**Example:** MinIO (local) → Azure Blob Storage (demo/production)

### Step 1: Provision Azure Blob Storage

```bash
# Create storage account
az storage account create \
  --name fpsdemostorage \
  --resource-group fps-demo-rg \
  --location westeurope \
  --sku Standard_LRS \
  --kind StorageV2

# Create container
az storage container create \
  --name fps-demo-files \
  --account-name fpsdemostorage

# Grant Storage Blob Data Contributor to managed identity
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/fps-demo-rg/providers/Microsoft.Storage/storageAccounts/fpsdemostorage
```

### Step 2: Update Component YAML

Create `demo/azure-blob-binding.yaml`:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: s3store  # Keep same name for app compatibility
spec:
  type: bindings.azure.blobstorage
  version: v1
  metadata:
  - name: accountName
    value: "fpsdemostorage"
  - name: containerName
    value: "fps-demo-files"
  - name: azureClientId
    value: "<managed-identity-client-id>"
auth:
  secretStore: azurekeyvault
```

### Step 3: Test Binding Operations

```bash
# Test output binding (write blob)
curl -X POST http://localhost:3500/v1.0/bindings/s3store \
  -H "Content-Type: application/json" \
  -d '{
    "operation": "create",
    "data": "Hello Azure Blob Storage",
    "metadata": {
      "blobName": "test.txt"
    }
  }'

# Verify in Azure Portal Storage Browser
# Or via Azure CLI
az storage blob list --container-name fps-demo-files --account-name fpsdemostorage
```

---

## Cross-Platform Migration Examples

### Azure → AWS

**Pub/Sub:** Azure Service Bus → AWS SNS/SQS
- Update component type to `pubsub.aws.snssqs`
- Configure IAM role or access keys
- Update region metadata
- Validate message delivery end-to-end

**State Store:** Cosmos DB (MongoDB API) → MongoDB Atlas or self-hosted
- Export data from Cosmos DB
- Import to target MongoDB
- Update connection string
- Verify collection-per-tenant pattern

**Secrets:** Azure Key Vault → AWS Secrets Manager
- Export secrets (manually or via script)
- Import to AWS Secrets Manager
- Update component type to `secretstores.aws.secretmanager`
- Use IRSA for workload identity

### AWS → GCP

**Pub/Sub:** AWS SNS/SQS → GCP Pub/Sub
- Update component type to `pubsub.gcp.pubsub`
- Configure Workload Identity or service account keys
- Create topics/subscriptions in GCP
- Validate message delivery

**Secrets:** AWS Secrets Manager → GCP Secret Manager
- Export and import secrets
- Update component type to `secretstores.gcp.secretmanager`
- Use Workload Identity for authentication

### Kubernetes Self-Hosted

For clients requiring full Kubernetes control:
- Deploy RabbitMQ, MongoDB, Vault as StatefulSets or via Helm charts
- Use Kubernetes secrets for initial credentials
- Configure Dapr components to reference in-cluster services
- Set up backup/restore procedures for StatefulSet volumes

---

## Troubleshooting

### Component Not Loading

**Symptom:** Dapr sidecar logs show component initialization errors.

**Check:**
1. Component YAML syntax valid (run through YAML validator)
2. Component type and version supported by Dapr runtime version
3. Secret store accessible and credentials correct
4. Network connectivity to target service (firewall, NSG, security group rules)
5. Managed identity or service principal permissions granted

**Debug:**
```bash
# Check Dapr metadata API
curl http://localhost:3500/v1.0/metadata

# Check sidecar logs
kubectl logs <pod-name> -c daprd -n fps --tail=100

# Test connectivity from pod
kubectl exec <pod-name> -n fps -- curl -v https://<target-service>
```

### Authentication Failures

**Symptom:** "Unauthorized" or "Access Denied" errors in Dapr logs.

**Check:**
1. Managed identity assigned to workload (Azure MI, AWS IRSA, GCP Workload Identity)
2. RBAC roles granted to managed identity (Key Vault Secrets User, Storage Blob Data Contributor, etc.)
3. Secret store contains correct credentials
4. Credentials not expired or rotated
5. Secret key names match `secretKeyRef` in component YAML

**Fix:**
```bash
# Verify managed identity assignment
az containerapp identity show --name fps-booking --resource-group fps-demo-rg

# Verify role assignments
az role assignment list --assignee <managed-identity-principal-id>

# Test secret access manually
az keyvault secret show --vault-name fps-demo-kv --name mongodb-username
```

### Message Loss or Duplication

**Symptom:** Messages not delivered, delivered multiple times, or stuck in dead-letter queue.

**Check:**
1. Consumer subscription/queue exists and correctly configured
2. Consumer ack/nack behavior correct (at-least-once delivery requires idempotency)
3. Message TTL and retention policies appropriate
4. Dead-letter queue monitored and processed
5. Network partition or sidecar restart during processing

**Fix:**
- Implement idempotency keys in message handlers
- Configure retry policies in Dapr pub/sub metadata
- Monitor dead-letter queue and replay manually if needed
- Verify message ordering requirements (if any)

### State Inconsistencies

**Symptom:** State operations fail, data missing, or wrong tenant data returned.

**Check:**
1. Collection names derived correctly from tenant context (never from API input)
2. Indexes created for all tenant collections
3. Connection string points to correct database
4. Write concern and read concern set appropriately
5. Tenant key sanitization applied (alphanumeric + underscore only)

**Fix:**
```bash
# Verify collections exist
mongosh "<connection-string>" --eval "db.getCollectionNames()"

# Verify indexes
mongosh "<connection-string>" --eval "db.<tenant>_bookings.getIndexes()"

# Check Dapr state store logs
kubectl logs <pod-name> -c daprd | grep statestore
```

### Performance Degradation

**Symptom:** Higher latency or error rate after component swap.

**Check:**
1. New component tier/SKU sufficient for workload (demo M0 vs production M5)
2. Region latency (cross-region calls add roundtrip time)
3. Connection pool settings appropriate
4. Retry policies too aggressive or too lenient
5. Observability overhead (sampling rate too high)

**Fix:**
- Upgrade tier/SKU if resource-constrained
- Co-locate components in same region
- Tune connection pool and timeout settings
- Review Dapr telemetry sampling rate

---

## Rollback Checklist

If component replacement fails or causes production issues:

- [ ] Revert component YAML to previous version
- [ ] Verify old infrastructure still accessible
- [ ] Redeploy services with reverted component configuration
- [ ] Verify services reconnect to old component successfully
- [ ] Check message backlog drained from old component
- [ ] Monitor metrics and logs for stability
- [ ] Document root cause and lessons learned
- [ ] Plan fix and retry component replacement later

---

## Post-Replacement Validation

After successful component replacement:

- [ ] Smoke tests pass (booking submission, draw execution, notification delivery)
- [ ] Metrics within expected range (latency, error rate, throughput)
- [ ] Logs show no authentication or connectivity errors
- [ ] Observability dashboards updated with new component metrics
- [ ] Documentation updated (deployment guide, runbooks, cost assumptions)
- [ ] Old infrastructure decommissioned or archived for rollback period
- [ ] Team trained on new component operational procedures
- [ ] Client notified (if production change)

---

## References

- [Dapr Component Baseline README](../dapr/README.md)
- [OPS000 Hosting and Deployment Strategy](../../docs/production/hosting-deployment-strategy.md)
- [Monitoring](../../docs/production/monitoring.md)
- [Versions and Decisions](../../docs/versions-and-decisions.md)
- [Dapr Components Reference](https://docs.dapr.io/reference/components-reference/)
