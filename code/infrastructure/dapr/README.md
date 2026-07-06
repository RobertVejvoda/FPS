# Dapr Component Baseline

OPS001 establishes the pluggable Dapr component structure for local development, demo, and client-owned production.
The component **name** is the contract. The underlying provider is swapped per profile without touching application code.

---

## Directory Layout

```
dapr/
  components/
    local/    ← loaded by docker-compose (./dapr/components/local)
    demo/     ← template files for demo-hosted environment (OPS002)
    client/   ← template files for client-owned production (OPS003)
  configuration/
    fairspot-config.yaml   ← Compose (local + NAS): tracing, actors, mTLS disabled (OPS017)
    fairspot-smoke-config.yaml ← Host-local smoke config without tracing export
    fairspot-config.k8s-hosted.yaml ← mTLS-enabled target for the K8s/DOKS profile (not wired into Compose)
```

Dapr loads components from the directory mounted at `/components` in the sidecar.
For local development this is `components/local/`. Demo and client directories are
template/documentation — copy and adapt them for each deployment target.

---

## Component Contract

| Logical name       | Building block | Local provider     | Demo candidate              | Client-owned             |
|--------------------|----------------|--------------------|-----------------------------|--------------------------|
| `fairspot-pubsub`       | pub/sub        | RabbitMQ           | Azure Service Bus / RabbitMQ managed | Client-approved broker  |
| `bookingstore`     | state          | MongoDB            | MongoDB Atlas / managed     | Client-approved MongoDB-compatible store |
| `notificationstore`| state          | MongoDB            | Same                        | Same                     |
| `auditstore`       | state          | MongoDB            | Same                        | Same                     |
| `profilestore`     | state          | MongoDB            | Same                        | Same                     |
| `configstore`      | state          | MongoDB            | Same                        | Same                     |
| `reportingstore`   | state          | MongoDB            | Same                        | Same                     |
| `workflowstore`    | actor state    | MongoDB            | Same                        | Same                     |
| `s3store`          | output binding | MinIO (S3-compat.) | Cloud object storage        | Client-approved S3-compatible store |
| `notification-email` | output binding | Retained/superseded — real sends go through a direct SendGrid v3 HTTP transport (key from `secretstore`), not this binding; local uses the in-memory sender | Twilio SendGrid (component retained) | Twilio SendGrid or approved equivalent (component retained) |
| `secretstore`      | secret store   | HashiCorp Vault    | Azure Key Vault / Vault managed | Client secret-management platform |

**Rule:** Application code references only the logical name. Never hardcode a broker URL,
connection string, or provider SDK in domain or application-layer code.

---

## Topic Contract

| Topic name       | Publisher      | Subscribers                              |
|------------------|----------------|------------------------------------------|
| `booking-events` | `fairspot-booking`  | `fairspot-notification`, `fairspot-audit`, `fairspot-reporting` |

The topic name is fixed. The pub/sub component backing it is swapped per profile.

---

## Secret Store Pattern

All component credentials use `secretKeyRef` pointing to named secrets in the active `secretstore`.
No credentials are embedded directly in component YAML.

Local Vault secret paths (prefix: `dapr/`):
- `dapr/rabbitmq-credentials` → `{ username, password }`
- `dapr/mongodb-credentials` → `{ username, password }`
- `dapr/minio-credentials` → `{ accessKey, secretKey }`
- `dapr/sendgrid-credentials` → `{ apiKey }` read by the Notification SendGrid HTTP transport at send time (and referenced by the retained `notification-email` binding)

Demo and client profiles: replace Vault with a deployment-approved managed secret store.
Use workload/managed identity where the platform supports it — no committed tokens.

---

## MongoDB Database Naming

Each service owns its own MongoDB database. Collections are named per entity type.

| Service            | Database          | Collection(s)           |
|--------------------|-------------------|-------------------------|
| fairspot-booking        | `fairspot-booking`     | `bookings`              |
| fairspot-notification   | `fairspot-notification`| `notifications`         |
| fairspot-audit          | `fairspot-audit`       | `auditlog`              |
| fairspot-profile        | `fairspot-profile`     | `profiles`              |
| fairspot-configuration  | `fairspot-configuration`| `policies`, `slots`    |
| fairspot-reporting      | `fairspot-reporting`   | `projections`           |
| Dapr workflow actor runtime | `fairspot-workflow` | `workflows` |

For multi-tenant sharding, prefix the collection name with `{tenantId}_` (e.g. `acme_bookings`).
Dapr state store keys embed the tenant-scoped key; the collection name is the partition boundary.

Indexes to create per collection:
- Booking: `tenantId + requestedDate`, `tenantId + status`, `tenantId + requestorId`
- Audit: `tenantId + createdAt`, `tenantId + entityId`
- Notification: `tenantId + recipientId + isRead`, `tenantId + createdAt`

---

## App Scoping

State-store components are scoped to the owning service app ID in every profile. `workflowstore`
is shared because it backs the Dapr actor runtime used by Dapr Workflow. Pub/sub, binding,
and secret-store components may also be shared when multiple apps need them. App IDs follow
the pattern `fairspot-{service}` (e.g. `fairspot-booking`, `fairspot-notification`).

## Workflow Access Policies

Workflow access policies are resource files, not components. Keep them beside the
profile's other Dapr YAML so the same resources path loads them with the matching
components.

- Smoke, local, demo, and client profiles can load `WorkflowAccessPolicy` resources
  from their profile-specific `components/<profile>/` directory.
- FairSpot uses a deny-by-default policy for workflow-hosting app IDs (`fairspot-booking`
  and `fairspot-audit`) so unrelated app IDs cannot schedule their workflows.
- Same-app workflow calls remain allowed; the policy is a Dapr runtime perimeter
  and does not replace application authorization.
- Workflow history signing is intentionally not enabled in local smoke because the
  smoke config keeps mTLS off. Enable signing only in profiles that can preserve a
  stable CA/root-key lifecycle.

---

## Observability

The `fairspot-config.yaml` Dapr configuration selects `workflowstore` as the actor state store
and enables tracing at 100% sampling rate (local). `fairspot-smoke-config.yaml` selects the same
actor state store but omits tracing so host-local `dapr run -f dapr.yaml` does not try to
export to the Docker-network Zipkin endpoint.
- **Local**: Zipkin at `http://zipkin:9411/api/v2/spans`
- **Local host UI**: Docker Compose maps Zipkin to `http://localhost:19411` to avoid colliding with Dapr's default local Zipkin on host port `9411`.
- **Demo / client**: Uncomment the `otel:` block and point at an OpenTelemetry Collector.
  The collector then exports to Azure Monitor, Grafana, Dynatrace, Splunk, or equivalent.

### Service-to-service security mode (OPS017)

mTLS is **disabled** on the self-hosted Docker Compose stack (local and NAS): `fairspot-config.yaml`
sets `mtls.enabled: false` because Dapr mTLS needs the Sentry control plane to issue/rotate
workload certificates, and this stack runs only Placement + Scheduler (no Sentry). The
mTLS-**enabled** target for the Kubernetes/DigitalOcean DOKS profile is a separate,
not-wired-in artifact: `configuration/fairspot-config.k8s-hosted.yaml`. `tools/start-container-stack.sh`
reads the active configuration and reports the mode ("Dapr service-to-service security").
See [Dapr-First Production Standards](../../../docs/production/dapr-first-production-standards.md).

---

## Swapping a Component for Demo or Client

1. Copy the relevant file from `demo/` or `client/` into the deployment target's component path.
2. Change only `spec.type` and the `metadata` values — keep `metadata.name` unchanged.
3. Populate the referenced secrets in the target secret store.
4. Run `dapr components list` after deployment to confirm the component is loaded.
5. Smoke-test the specific building block (publish a test event, read/write state).

No application code changes are needed when the component name is preserved.
