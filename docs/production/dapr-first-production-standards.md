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

## References

- Dapr transactional outbox: <https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/>
- Dapr security: <https://docs.dapr.io/concepts/security-concept/>
- Dapr resiliency: <https://docs.dapr.io/operations/resiliency/resiliency-overview/>
- Dapr state encryption: <https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-encrypt-state/>
- Dapr secret scoping: <https://docs.dapr.io/operations/configuration/secret-scope/>
