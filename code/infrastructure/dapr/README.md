# Dapr Component Baseline

This directory defines FPS's pluggable Dapr component strategy across local development, demo, and client-owned production deployment profiles.

## Design Principles

1. **Pluggability by deployment profile** — FPS does not lock into a single production provider. Component files change per environment; application code does not.
2. **Dapr abstraction boundary** — Pub/sub, state, secrets, bindings, service invocation, and observability remain behind Dapr APIs. Provider-specific SDKs stay out of domain and application layers.
3. **Cost-aware choices** — Local is minimal-cost. Demo uses low-cost managed services. Client production cost belongs to the client.
4. **No vendor lock-in in domain/application code** — Swapping components requires YAML changes and validation, not code rewrites.
5. **Collection-per-tenant isolation** — State store components define database-level connections. Services derive tenant-specific collection names from authenticated context. Never accept collection names from API callers.

## Directory Structure

```
dapr/
├── components/
│   ├── local/                    # Local development components (Docker Compose)
│   ├── demo/                     # Demo environment components (managed services)
│   ├── production-examples/      # Client-owned production examples (Azure/AWS/GCP/K8s)
│   └── [legacy files remain at root for now, will be removed after migration]
├── configuration/
│   └── fps-config.yaml           # Dapr configuration (tracing, mTLS, etc.)
└── README.md                     # This file
```

## Component Boundaries

### Pub/Sub (Event-Driven Communication)

**Purpose:** Asynchronous message delivery between services for domain events (booking allocated, notification created, audit recorded, etc.)

**Contract:** Services publish and subscribe to topics via Dapr pub/sub API. Message payload is JSON. Delivery is at-least-once; consumers must be idempotent.

**Component profiles:**

| Profile | Component | Notes |
|---------|-----------|-------|
| **Local** | RabbitMQ (`pubsub.rabbitmq`) | Docker Compose, dev-mode credentials from Vault |
| **Demo** | Azure Service Bus Topics (`pubsub.azure.servicebus.topics`) | Low-cost tier, managed identity preferred |
| **Client production** | Azure Service Bus, AWS SNS/SQS, GCP Pub/Sub, RabbitMQ, Kafka, or equivalent | Client-approved broker with Dapr component support |

**Portability:** Swapping pub/sub components requires YAML update and validation. No application code changes.

### State Store (Command-Side Persistence)

**Purpose:** Command-side write model for aggregates and entities. Used via Dapr state API for transactional writes.

**Contract:** MongoDB-compatible state store. Collection-per-tenant model: services resolve `{database}.{tenant_key}_{service_collection}` from authenticated tenant context. Index creation is service responsibility. Never accept collection names from API input.

**Component profiles:**

| Profile | Component | Notes |
|---------|-----------|-------|
| **Local** | MongoDB (`state.mongodb`) | Docker Compose, single instance, dev-mode credentials |
| **Demo** | MongoDB Atlas (`state.mongodb`) | M0 free tier or M2/M5, EU region for GDPR |
| **Client production** | MongoDB Atlas, Azure Cosmos DB (MongoDB API), self-hosted MongoDB replica set, or equivalent | Client-approved MongoDB-compatible store with collection-per-tenant provisioning |

**Collection naming convention:**
```
Database: fps (local) / fps-demo / fps-production
Collections: {tenant_key}_bookings, {tenant_key}_profiles, {tenant_key}_notifications, etc.
tenant_key: Sanitized tenant identifier from ICurrentUser context (alphanumeric + underscore only)
```

**Indexes:** Each service is responsible for creating tenant-specific indexes. Index names should include tenant key to avoid conflicts.

**Portability:** Swapping state stores requires YAML update, connection validation, and backup/restore verification. Application code using Dapr state API remains unchanged.

### Secrets (Credential Management)

**Purpose:** Secure storage and retrieval of connection strings, API keys, certificates, and other Secret data.

**Contract:** Dapr secret store API. Secrets never committed to source control, container images, or logs. Secret references in component YAML use `secretKeyRef` pattern pointing to configured secret store.

**Component profiles:**

| Profile | Component | Notes |
|---------|-----------|-------|
| **Local** | HashiCorp Vault (`secretstores.hashicorp.vault`) | Docker Compose, dev-mode token, NOT production-safe |
| **Demo** | Azure Key Vault (`secretstores.azure.keyvault`) | Managed identity authentication, EU region |
| **Client production** | Azure Key Vault, AWS Secrets Manager, GCP Secret Manager, production Vault, or equivalent | Client-approved secret management with audit trail |

**Secret store pattern:**
```yaml
metadata:
  - name: connectionString
    secretKeyRef:
      name: mongodb-credentials  # Secret name in secret store
      key: connectionString      # Key within secret
auth:
  secretStore: vault              # Reference to secret store component
```

**Security notes:**
- Secret store components themselves may need manual review (review hooks block automatic edits)
- Vault dev-mode token is for local development only
- Production secret stores must use workload identity (Azure MI, AWS IRSA, GCP Workload Identity) where available
- Fallback to access keys/service principals only when workload identity unavailable
- Secret access must be audited in client production environments

**Portability:** Swapping secret stores requires YAML update and credential migration. Secret store component name remains stable; authentication mechanism changes.

### Bindings (Input/Output)

**Purpose:** Integration with external systems: object storage for reports/exports, cron scheduling for background jobs, HTTP webhooks, etc.

**Contract:** Dapr bindings API. Input bindings trigger service operations. Output bindings write to external systems.

**Component profiles:**

| Profile | Component | Use Case | Notes |
|---------|-----------|----------|-------|
| **Local** | MinIO (`bindings.aws.s3`) | S3-compatible object storage | Docker Compose, dev credentials |
| **Demo** | Azure Blob Storage (`bindings.azure.blobstorage`) | Reports, exports, attachments | Managed identity preferred |
| **Client production** | Azure Blob, AWS S3, GCP Cloud Storage, or equivalent | Reports, exports, backups | Client-approved object storage |

**Cron bindings:** Dapr cron binding or platform scheduler (e.g., Azure Container Apps scheduled jobs, Kubernetes CronJobs). Draw execution uses configurable tenant-specific cut-off time (default 18:00 local).

**Portability:** Swapping bindings requires YAML update and validation. Application code using Dapr bindings API remains unchanged.

### Service Invocation

**Purpose:** Synchronous service-to-service calls via Dapr sidecar with mTLS, retries, and observability.

**Contract:** Dapr service invocation API. Services call each other by app-id, not by direct URL.

**Component profiles:** Not a component per se, but configuration-driven via `dapr.yaml` or platform-specific sidecar injection.

| Profile | Implementation | Notes |
|---------|----------------|-------|
| **Local** | Dapr self-hosted sidecars | Started via `dapr run` or Docker Compose sidecar pattern |
| **Demo** | Managed or self-hosted Dapr sidecars | Azure Container Apps has native Dapr support |
| **Client production** | Managed or self-hosted Dapr sidecars | Client platform must support Dapr or equivalent service mesh |

**mTLS:** Local uses `mtls.enabled: false` for simplicity. Demo and production should enable mTLS where platform supports it.

### Identity and Observability

**Identity integration:**
- **Local:** Keycloak with dev realm, or mocked OIDC for rapid iteration
- **Demo:** Demo OIDC realm with seeded users and roles
- **Client production:** Client IdP through OIDC/OAuth 2.0. Tenant and role claims mapped explicitly. FPS stores only mapped subject, tenant, role, and minimal policy facts — never company passwords.

**Observability:**
- **Local:** Prometheus, Grafana, Jaeger/Zipkin for traces. Fast developer feedback.
- **Demo:** Low-cost dashboards sufficient for evaluation. OpenTelemetry export to managed service acceptable.
- **Client production:** OpenTelemetry metrics, logs, and traces exported to client platform (Dynatrace, Azure Monitor, Grafana, Splunk, CloudWatch, etc.). No vendor-specific SDK in application code.

Observability configuration in `dapr/configuration/fps-config.yaml`:
```yaml
spec:
  tracing:
    samplingRate: "1"
    zipkin:
      endpointAddress: "http://zipkin:9411/api/v2/spans"
```

For production, replace with OpenTelemetry Collector endpoint or platform-native exporter.

## Deployment Profile Guidance

### Local Development

**Goal:** Minimal cost, fast feedback, close to production contracts.

**Components:**
- Pub/sub: RabbitMQ (Docker Compose)
- State: MongoDB (Docker Compose)
- Secrets: Vault (dev mode, Docker Compose)
- Bindings: MinIO S3-compatible storage
- Identity: Keycloak dev realm
- Observability: Prometheus/Grafana/Jaeger

**Setup:** See `code/infrastructure/readme.md` for Docker Compose setup instructions.

**Component path:** `code/infrastructure/dapr/components/local/`

### Demo Hosted Environment

**Goal:** Low-cost hosted environment for product evaluation. Prove the product story with real usage, latency, error rate, and operational evidence.

**Candidate platform:** Azure Container Apps (native Dapr support) or equivalent managed container runtime.

**Components:**
- Pub/sub: Azure Service Bus (low-cost tier) or equivalent
- State: MongoDB Atlas (M0/M2 free tier) or equivalent
- Secrets: Azure Key Vault with managed identity
- Bindings: Azure Blob Storage for reports/exports
- Identity: Demo Keycloak instance or managed OIDC provider
- Observability: Low-cost metrics/logs/traces sufficient for demo evidence

**Region:** EU region (West Europe, North Europe) for GDPR data residency.

**Cost control:** Scale-to-zero where possible (internal services). Identity service may need min 1 replica.

**Component path:** `code/infrastructure/dapr/components/demo/`

**Next implementation:** OPS002 will validate and deploy demo environment baseline.

### Client-Owned Production

**Goal:** Production operation belongs to the client. FPS delivers deployable artifacts, configuration guidance, runbooks, and evidence. Client selects platform, region, backup, retention, and access controls.

**Components:** Client-approved equivalents for each boundary:
- Pub/sub: Azure Service Bus, AWS SNS/SQS, GCP Pub/Sub, RabbitMQ, Kafka, or equivalent
- State: MongoDB Atlas, Azure Cosmos DB (MongoDB API), self-hosted MongoDB, or equivalent
- Secrets: Azure Key Vault, AWS Secrets Manager, GCP Secret Manager, production Vault, or equivalent
- Bindings: Azure Blob, AWS S3, GCP Cloud Storage, or equivalent
- Identity: Client IdP through OIDC/OAuth 2.0 with mapped tenant and role claims
- Observability: OpenTelemetry export to Dynatrace, Azure Monitor, Grafana, Splunk, CloudWatch, or equivalent

**Authentication:** Use workload identity (Azure Managed Identity, AWS IRSA, GCP Workload Identity) where available. Fallback to secret-store-backed credentials only when workload identity unavailable.

**Data residency:** All data services (database, broker, secrets, storage) must be in client-approved region(s) to satisfy GDPR requirements.

**Component path:** `code/infrastructure/dapr/components/production-examples/`

**Next implementation:** OPS003 will document client-owned production integration and handoff responsibilities.

## Tenant Collection Provisioning Guidance

FPS uses **collection-per-tenant** isolation. Each service owns its MongoDB database and isolates tenant data through tenant-specific collections.

### Collection Naming

**Pattern:** `{tenant_key}_{collection_suffix}`

Example:
- `acmecorp_bookings` (Booking service, tenant "acmecorp")
- `acmecorp_profiles` (Profile service, tenant "acmecorp")
- `betainc_bookings` (Booking service, tenant "betainc")

**tenant_key derivation:**
1. Resolve `tenantId` from authenticated `ICurrentUser` context (JWT claim `tenant_id`)
2. Sanitize: alphanumeric + underscore only, lowercase, max 64 characters
3. Never accept tenant key from API input — always derive from authenticated context

### Index Creation

Each service is responsible for creating tenant-specific indexes when a new tenant is provisioned.

**Index naming pattern:** `idx_{tenant_key}_{field(s)}`

Example:
```csharp
var collectionName = $"{tenantKey}_bookings";
var collection = database.GetCollection<BookingDocument>(collectionName);

// Create indexes
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<BookingDocument>(
        Builders<BookingDocument>.IndexKeys.Ascending(x => x.RequestDate),
        new CreateIndexOptions { Name = $"idx_{tenantKey}_requestdate" }
    )
);
```

### Provisioning Flow

1. **Tenant onboarding** (CUST001, planned):
   - Admin creates tenant record with unique tenant ID and sanitized tenant key
   - Tenant metadata stored in Configuration service

2. **First authenticated request per service:**
   - Service derives collection name from authenticated tenant context
   - Service checks if collection exists; creates if missing
   - Service creates required indexes for that tenant's collection
   - Service records provisioning completion in internal state

3. **Ongoing operations:**
   - All queries and writes use tenant-scoped collection name
   - Never cross tenant boundaries in a single query
   - Audit all tenant provisioning events

### Backup and Retention

Client production environments must configure:
- Database-level backup (covers all tenant collections)
- Point-in-time recovery window per client requirements
- Retention policy for audit logs (default: 7 years, configurable per jurisdiction)
- GDPR erasure: Delete PiiMapping row only; audit log remains immutable and pseudonymised

## Component Replacement Operational Runbook

### When to Replace a Component

- **Demo environment setup:** Swap local components for managed equivalents (e.g., RabbitMQ → Azure Service Bus)
- **Client onboarding:** Match client's approved provider and region
- **Cost optimization:** Move from managed service to self-hosted or vice versa
- **Compliance requirements:** Change region or provider to meet GDPR, data residency, or audit requirements
- **Platform migration:** Move demo from Azure to AWS, GCP, Kubernetes, etc.

### Replacement Procedure

1. **Identify component to replace:**
   - Current: `local/rabbitmq-pubsub.yaml`
   - Target: `demo/azure-servicebus-pubsub.yaml`

2. **Provision new infrastructure:**
   - Create target resource (e.g., Azure Service Bus namespace)
   - Configure region, tier, and access controls
   - Create managed identity or service principal if needed
   - Store credentials in target secret store

3. **Update component YAML:**
   - Copy target component file to active components directory
   - Replace placeholders: `{your-servicebus-namespace}`, `{managed-identity-client-id}`, etc.
   - Verify secret store references match target secret store component name

4. **Validate component:**
   - Deploy updated component to target environment
   - Run Dapr component validation: `dapr components -k` (Kubernetes) or check Dapr logs (self-hosted)
   - Verify connectivity from Dapr sidecar to target service
   - Check Dapr component metadata API: `http://localhost:3500/v1.0/metadata`

5. **Test with canary traffic:**
   - Deploy single service instance with new component
   - Send test messages/state operations
   - Verify success in target system (e.g., Service Bus explorer, MongoDB Atlas console)
   - Check application logs and Dapr sidecar logs for errors

6. **Roll out to all services:**
   - Update deployment manifests or CI/CD pipeline
   - Deploy services with new component configuration
   - Monitor metrics, logs, and traces during rollout

7. **Verify end-to-end:**
   - Run smoke tests covering booking submission, draw execution, notification delivery, audit records
   - Verify no message loss, state corruption, or secret access failures
   - Check observability dashboards for latency and error rate

8. **Decommission old component:**
   - Wait for message backlog to drain (pub/sub)
   - Verify no active connections to old component
   - Archive old component YAML for rollback safety
   - Decommission old infrastructure (after backup/export if needed)

9. **Update documentation:**
   - Record component change in `docs/versions-and-decisions.md` if durable
   - Update deployment runbooks with new component details
   - Update cost assumptions if provider or tier changed

### Rollback Procedure

If new component fails validation or causes production issues:

1. Revert component YAML to previous version
2. Redeploy services with old component
3. Verify old component still accessible (retain old infrastructure until rollback proven)
4. Investigate root cause (connectivity, credentials, API version mismatch, etc.)
5. Fix issue and retry replacement procedure

### Testing Checklist

Before considering a component swap complete:

- [ ] Component validates via Dapr metadata API
- [ ] Application logs show successful Dapr component initialization
- [ ] Pub/sub: Messages published and consumed successfully
- [ ] State store: Keys written and retrieved successfully
- [ ] Secrets: Credentials loaded without errors
- [ ] Bindings: Input/output operations succeed
- [ ] Observability: Metrics, logs, and traces exported correctly
- [ ] Performance: Latency within acceptable range
- [ ] Cost: New component cost matches expectations
- [ ] Security: Workload identity or secret-store-backed credentials working
- [ ] Compliance: Region and data residency requirements met

## Next Implementation Slices

| Slice | Goal | Status |
|-------|------|--------|
| **OPS001** (this slice) | Define component baseline, profiles, and replacement guidance | In progress |
| **OPS002** | Deploy demo environment with managed components | Planned |
| **OPS003** | Document client-owned production integration and handoff | Planned |
| **OPS004** | Wire OpenTelemetry observability and performance evidence | Planned |
| **OPS005** | Define integration secret handling and observability for customer-system actors | Planned |

## References

- [Dapr Components Concept](https://docs.dapr.io/concepts/components-concept/)
- [Dapr Pub/Sub Overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/)
- [Dapr State Store](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/)
- [Dapr Secret Stores](https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-overview/)
- [Dapr Bindings](https://docs.dapr.io/developing-applications/building-blocks/bindings/bindings-overview/)
- [Azure Container Apps — Enable Dapr](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview)
- [OPS000 Hosting and Deployment Strategy](../../docs/production/hosting-deployment-strategy.md)
- [Monitoring](../../docs/production/monitoring.md)
- [Versions and Decisions](../../docs/versions-and-decisions.md)
