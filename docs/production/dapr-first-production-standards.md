# Dapr-First Production Standards

FairSpot should use Dapr as a production-grade application runtime boundary, not only as local development glue. When Dapr provides a building block that matches a FairSpot requirement, implementation slices should use that building block first and document any exception.

This standard applies to new production-facing slices and to reviews of cross-service changes.

## Design Rule

Use Dapr-native capabilities before custom application infrastructure when they fit the requirement:

| Requirement | Preferred FairSpot direction | Fallback only when needed |
| --- | --- | --- |
| Long-running orchestration | Dapr Workflow with deterministic instance IDs, idempotent activities, persisted progress, and explicit recovery states. | Direct command handler only for narrow operations that complete inside one request and do not need progress/retry evidence. |
| State plus integration events | Dapr transactional outbox with transactional state store support. | Service-owned pending-event/outbox records with deterministic event IDs and retry publisher. |
| Cross-service events | Dapr pub/sub over the selected broker. | Provider SDK only behind an adapter when Dapr cannot satisfy the deployment profile. |
| Event consumers | Idempotent handlers with inbox/checkpoint state. | None. At-least-once delivery must always be expected. |
| Service-to-service calls | Dapr service invocation with mTLS/Sentry in production. | HTTPS/gRPC only when a service is outside the Dapr runtime boundary. |
| Scheduled work | Dapr Workflow plus Dapr cron binding or Dapr Jobs where suitable, guarded by deterministic keys. | Platform scheduler calling the same workflow starter. |
| Secrets | Dapr secret store references and component secret scoping. | Environment variables only for local/dev bootstrap values and CI boundaries. |
| Resiliency | Dapr resiliency policies for retries, timeouts, circuit breakers, and dependency health. | Application-level policy only when Dapr target policy is not expressive enough. |
| State encryption | Dapr state encryption where the selected state store/runtime supports it, plus infrastructure encryption at rest. | Store-managed encryption only when Dapr encryption is not available for that component. |
| Access to Dapr components | Dapr component scopes and sidecar/API hardening. | Network-only restrictions are not enough for production. |

Business authorization, tenant isolation, input validation, and privacy filtering still belong in application code. Dapr secures and standardises runtime boundaries; it does not replace business security checks.

## Production Acceptance Criteria

Production-facing Dapr slices should explicitly answer:

- Which Dapr building block is used.
- Which Dapr component name and app IDs are involved.
- Whether the selected component supports transactions, outbox, encryption, resiliency targets, and component scoping.
- What happens when Dapr redelivers an event or retries an activity.
- How duplicate workflow starts or scheduler ticks are handled.
- Which state keys, event IDs, workflow IDs, and inbox IDs are deterministic.
- Which fields are safe for logs, metrics, traces, and UI progress.
- How local, demo, and client-owned production profiles validate the same contract.

## Implementation Plan

| Priority | Slice direction | Outcome |
| --- | --- | --- |
| P0 | Apply this Dapr-first standard to Draw Workflow review and follow-up fixes. | DRAW002 does not merge with fire-and-forget events, unstable seeds, or unclear failed retry behavior. |
| P0 | Define event publication reliability for Booking. | Booking state changes publish through Dapr transactional outbox where supported, or through a service-owned pending-event outbox with deterministic IDs. |
| P0 | Finish DataHub event catalog and inbox contract. | DataHub treats Dapr pub/sub delivery as at-least-once and prevents duplicate projections through inbox idempotency. |
| P1 | Add Dapr production component hardening checklist. | Demo/client profiles define component scopes, secret references, mTLS, resiliency policies, state encryption, backup, and health evidence. |
| P1 | Add local/NAS smoke proof for Dapr runtime capabilities. | Smoke evidence covers sidecars, service invocation, pub/sub, state, workflow, secret store, resiliency behavior, and scheduled trigger safety. |
| P2 | Add optional hosted-profile validation. | A low-cost hosted demo proves the same Dapr contracts outside the developer machine. |

## Draw Workflow Requirements

Draw workflow implementation must:

- use deterministic workflow instance IDs from tenant, location, date, and time slot;
- use stable deterministic seeds, never runtime-randomised `GetHashCode`;
- make mutating activities idempotent or guarded by deterministic state keys;
- persist progress before exposing it to UI;
- publish integration events through Dapr transactional outbox or an approved durable fallback;
- keep manual HR/admin trigger and scheduled trigger on the same workflow starter;
- treat duplicate workflow starts and duplicate scheduled ticks as normal.

## Event Reliability Requirements

Dapr pub/sub gives FairSpot a portable event transport, but consumers must still assume at-least-once delivery.

Producer services should:

- update authoritative state and record integration events in one recoverable operation;
- prefer Dapr transactional outbox where the state store supports Dapr transactions;
- use deterministic source event IDs so retries are safe;
- publish only employee/customer-safe payload fields required by consumers;
- avoid fire-and-forget event publishing in workflows and command handlers.

DataHub and other consumers should:

- record received events in an inbox before projection;
- make inbox insertion idempotent by event ID;
- update projections deterministically;
- expose lag, failure, retry, and poison-event status.

## Security Requirements

Production profiles should use:

- Dapr mTLS/Sentry for service-to-service identity and encrypted sidecar traffic;
- Dapr secret stores with component secret references, not inline secrets;
- Dapr secret and component scopes so services can access only their required runtime dependencies;
- Dapr `WorkflowAccessPolicy` for any target app that accepts cross-app workflow/activity scheduling; use deny-by-default scopes and explicit allow-lists;
- Only `schedule` is enforced cross-app today; `get`, `terminate`, `purge`, `pause`, `resume`, and `rerun` still route as self-calls and need application-layer authorization if they are exposed.
- Dapr workflow history signing only when the deployment profile has mTLS enabled and a documented CA/root-key lifecycle; keep it off in local smoke;
- Dapr API token and app API token hardening where the deployment profile exposes sidecar/app endpoints;
- Dapr resiliency policies for state stores, pub/sub, service invocation, and workflow dependencies;
- Dapr state encryption where supported, in addition to infrastructure encryption at rest;
- private networking and WAF/ingress controls that never expose Dapr sidecar APIs publicly.

## Service-to-Service Security Mode (OPS017)

Dapr mTLS gives each sidecar a SPIFFE workload identity and encrypts and mutually
authenticates all sidecar-to-sidecar traffic. Which profiles run it, and why, is fixed:

| Profile | mTLS | Why |
|---|---|---|
| Local Docker Compose | **Disabled** | Keep local development simple. Traffic stays on one host's loopback/private bridge. |
| NAS / self-hosted Docker Compose (Release 1) | **Disabled (documented exception)** | Dapr mTLS needs the **Sentry** control plane to issue and rotate workload certificates. The self-hosted Compose stack runs only Placement + Scheduler — no Sentry — so mTLS cannot be enabled here safely. On a single NAS host all sidecars share one private Docker bridge, so plaintext intra-service traffic is low risk: sniffing it requires host access, at which point the attacker already has more than the traffic. |
| Kubernetes / DigitalOcean DOKS (target) | **Enabled** | Sentry is standard on the Kubernetes Dapr install and managed runtimes. mTLS is the default there, so the defense-in-depth that matters once services span hosts arrives naturally with the K8s move. |

**Configuration split.** The Compose stack mounts `dapr/configuration/fps-config.yaml`
(`mtls.enabled: false`). The mTLS-enabled target for the hosted K8s profile is a separate,
not-wired-in artifact, `dapr/configuration/fps-config.k8s-hosted.yaml` (`mtls.enabled: true`),
which also carries the `workloadCertTTL` / `allowedClockSkew` and workflow-history-signing
posture for that profile.

**Certificates, trust anchors, rotation.** No key material is committed. On Kubernetes,
Sentry holds the trust-bundle root CA and issues short-lived leaf certificates bound to each
app's SPIFFE ID (`spiffe://<trust-domain>/ns/<namespace>/<app-id>`); it auto-rotates leaves
before `workloadCertTTL` expiry. Root rotation is an operator action — roll the Sentry
trust-bundle secret and restart sidecars — with evidence recorded in the private operator
runbook. `allowedClockSkew` absorbs node clock drift during verification.

**Failure mode.** With mTLS enabled, a sidecar that cannot obtain or validate a certificate
(Sentry unreachable, expired/again-untrusted root, clock skew beyond tolerance) fails its
service-to-service calls closed rather than falling back to plaintext. Operators must treat
Sentry availability and trust-bundle rotation as release-gating for that profile.

**How this is reported.** No application code depends on certificate material — services
reference only logical Dapr component and app names. The startup path
(`tools/start-container-stack.sh`) reads the active Dapr Configuration and prints the
service-to-service security mode under **"Dapr service-to-service security (OPS017)"**:
`DISABLED` with the documented-exception note on the self-hosted profiles, `ENABLED` where a
profile mounts the mTLS-enabled configuration.

## Dynamic Database Secrets (SEC012A)

DataHub is the one service backed by PostgreSQL (EF Core / Npgsql) rather than a Dapr state store, so its database credential is a connection string the app reads at startup, not a Dapr component secret. SEC012A (#742) is Phase 2 of the #628 secret-hardening path.

**Fail-closed hardening (done).** The base `appsettings.json` no longer ships a Postgres connection string; `ConnectionStrings:DataHub` is supplied per profile:

- **Local development** — `appsettings.Development.json` (dev-only `localhost` credentials, matching the local Postgres container).
- **Production-like profiles** — injected as the `ConnectionStrings__DataHub` environment value. The container compose files (`docker-compose.services.yml`, `docker-compose.services.images.yml`) require `POSTGRES_PASSWORD` explicitly (`${POSTGRES_PASSWORD:?…}`) with **no committed `fps` fallback**, so an unset password fails compose interpolation rather than silently substituting the dev default. NAS sources the password from the operator `nas.env`; the local dev default is confined to the local-only `start-container-stack.sh` path.
- If no connection string is supplied, DataHub startup **fails closed** (`ConnectionStrings:DataHub is required`) instead of falling back to a committed default password.

**Why Dapr Vault KV is not enough for database leases.** The Dapr `secretstores.hashicorp.vault` component reads **static** key/value secrets: it fetches a stored string and returns it unchanged — no lease, no TTL, no renewal or revocation. A database credential that never rotates and outlives any single process is exactly what dynamic secrets exist to avoid. Vault's **database secrets engine** instead *generates* a short-lived Postgres user on demand, bound to a lease with a TTL; the credential must be **renewed** before expiry and is **revoked** when the lease ends. Dapr KV has no lease lifecycle to drive that renewal/revocation, so it cannot safely carry a dynamic database credential — hence Vault Agent (or app-side lease renewal) is required.

**NAS dynamic-secret target (follow-up implementation).** The intended flow keeps the credential short-lived and out of application config:

| Step | Component | Behavior |
|---|---|---|
| Enable engine | Vault **database secrets engine** | Configured against Postgres using an admin/rotation connection. |
| Least-privilege role | Vault role `datahub` | Issues a Postgres user granted only the privileges DataHub needs (read/write its projection tables — not superuser). |
| Lease / TTL | Vault role default-TTL + max-TTL | Short default TTL (e.g. hours) with renewal; max-TTL bounds total lifetime before a fresh credential is issued. |
| Render | **Vault Agent** template | The Agent authenticates with the least-privilege `fairspot-dapr` token, requests a `datahub` credential, and writes the rendered `ConnectionStrings__DataHub` to a file/env source DataHub reads. |
| Renew / rotate | **Vault Agent** | Renews the lease before expiry and re-renders on rotation; DataHub picks up the refreshed connection string (Agent restart-on-change or app reload). |
| Consume | DataHub | Reads `ConnectionStrings__DataHub` from the Agent-rendered source exactly as it reads any injected connection string — no application code change and no Dapr dependency on this path. |

Because Npgsql pools connections, rotation must recycle the pool (or rely on new connections picking up the refreshed string); app-side lease renewal is an acceptable alternative where a Vault Agent sidecar is not available.

**Remaining follow-ups (#628).** The other datastores still use static secrets and should move to Vault dynamic secrets under the same path, tracked separately — not widened into this slice:

- **MongoDB** (Booking/Profile/Configuration/Audit/Reporting/Notification/Customer state stores) — Vault database secrets engine (MongoDB plugin).
- **RabbitMQ** (`fps-pubsub`) — Vault RabbitMQ secrets engine or rotated static credentials.
- **MinIO / object storage** (`s3store`) — rotated access/secret keys or a provider-managed identity.

## References

- Dapr transactional outbox: <https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/>
- Dapr security: <https://docs.dapr.io/concepts/security-concept/>
- Dapr resiliency: <https://docs.dapr.io/operations/resiliency/resiliency-overview/>
- Dapr state encryption: <https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-encrypt-state/>
- Dapr secret scoping: <https://docs.dapr.io/operations/configuration/secret-scope/>
