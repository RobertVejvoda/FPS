# Draw Scheduling And Workflow Direction

Current Draw execution is API-driven. `POST /draws/trigger` runs one explicit Draw key for a tenant, location, date, and time slot. The endpoint is restricted to HR/admin roles, takes tenant identity from the authenticated principal, and is idempotent for already completed Draw attempts.

FairSpot must keep an on-demand Draw action for HR/admin users. Scheduled execution and manual execution should use the same Draw key semantics and produce the same lifecycle/progress evidence.

## REST Client Scenarios

Use [Draw REST Client Scenarios](./draw-rest-client-scenarios.http) with the VS Code REST Client extension for repeatable local and hosted smoke testing.

The scenarios cover:

- future booking request submission;
- employee rejection from Draw trigger;
- pre-Draw status;
- HR/admin on-demand Draw;
- idempotent rerun of the same Draw key;
- post-Draw employee status;
- admin lifecycle view.

## Execution Options

Preferred customer-ready direction: implement Draw execution as a Dapr Workflow from the scheduling slice.

Acceptable fallback: keep direct application-handler computation only if the implementation provides the same externally visible guarantees:

- deterministic Draw key;
- single execution per Draw key across multiple Booking instances;
- persisted lifecycle/progress state;
- idempotent retry behavior;
- safe failure state and explicit recovery path;
- same HR/admin manual trigger behavior as the scheduled trigger.

Do not keep two independent execution paths. If both scheduled and manual triggers exist, they must converge into the same workflow starter or the same single-execution Draw service.

## Schedule Visibility Contract

The next scheduled Draw time is a customer-facing rule, not only an internal scheduler detail. Readers should be able to understand when requests close, when allocation is expected to run, and what happens if the schedule is not yet configured.

Minimum API-visible schedule metadata for a selected tenant, location, parking date, and time slot:

| Field | Meaning |
| --- | --- |
| `cutOffAt` | Exact local timestamp when future requests stop being accepted for this Draw key. |
| `nextDrawAt` | Exact local timestamp when the scheduled Draw is expected to run, when known. |
| `timeZone` | Policy timezone used to calculate cut-off and Draw time. |
| `requestWindowStatus` | `open`, `closed`, or `unknown`. |
| `scheduleStatus` | `known`, `notConfigured`, `disabled`, or `unknown`. |
| `scheduleSource` | `tenantPolicy`, `locationOverride`, `manualOnly`, or another documented source. |
| `lastCalculatedAt` | Timestamp when the schedule metadata was calculated. |
| `safeMessage` | Employee/customer-safe explanation suitable for UI display. |

The UI must expose this information where readers make or review parking decisions:

- employee **My Spots** should show next Draw time, request cut-off, timezone/context, and whether requests are still open for the selected date/time slot;
- HR operations should show the same schedule metadata plus current Draw lifecycle status and the authorized **Run Draw now** action;
- tenant/customer readiness views should make missing, disabled, or unknown Draw schedule configuration visible before go-live;
- mobile should show the employee-safe schedule summary wherever the employee submits or checks a request.

If the schedule cannot be calculated, the API must return an explicit schedule status and safe reason. It must not silently omit `nextDrawAt` in a way that makes the UI look complete. Employee/customer-safe views must not expose scheduler internals, workflow IDs, lottery seed, candidate order, raw penalties, stack traces, or other employees' outcomes.

## Dapr Workflow Target

The workflow should coordinate idempotent activities. Manual HR/admin trigger and scheduled trigger both call the same workflow starter with the same input shape.

Workflow input:

| Field | Source | Notes |
| --- | --- | --- |
| `tenantId` | Authenticated principal for manual trigger; trusted scheduler context for scheduled trigger. | Never from an unauthenticated request body. |
| `locationId` | Manual trigger body or configured schedule target. | Must match tenant location configuration. |
| `date` | Manual trigger body or scheduler-computed target parking date. | Usually next business day for scheduled Draw. |
| `timeSlotStart` / `timeSlotEnd` | Manual trigger body or configured slot schedule. | Must produce a valid Draw key. |
| `triggeredBy` | User hash or scheduler identity. | Used for audit/progress only. |
| `triggerSource` | `manual`, `scheduled`, or `recovery`. | Used for UI and audit. |
| `reason` | Required for manual/recovery; generated for scheduled. | Safe text, no secrets. |

The workflow ID must be deterministic from the Draw key, for example `draw:{tenantId}:{locationId}:{date}:{timeSlot}`. Starting the same workflow ID twice must not run allocation twice.

Exact workflow actions:

| Order | Activity | Responsibility | Idempotency and progress |
| --- | --- | --- | --- |
| 1 | `ResolveDrawInputActivity` | Validate tenant/location/date/time slot, resolve effective policy, compute the canonical Draw key, and calculate whether the request window should be closed. | Writes `PolicyResolved` progress. Re-running with same input returns the same Draw key. |
| 2 | `AcquireDrawAttemptActivity` | Create or acquire the Draw attempt for the Draw key. Prevent duplicate execution across replicas. | Uses ETag/transaction/compare-and-set. Existing `Completed` returns completed result; existing `Running` returns in-progress; existing `Failed` follows retry policy. Writes `Scheduled` or `Running`. |
| 3 | `CloseRequestWindowActivity` | Prevent new non-same-day requests for this Draw key after cutoff/manual start. | Must be repeatable. Writes `RequestWindowClosed`. If current code cannot lock the window yet, record the gap and enforce by submission-time checks. |
| 4 | `LoadPendingRequestsActivity` | Load pending requests matching tenant/location/date/time slot. | Read-only. Writes `RequestsLoaded` with count. |
| 5 | `LoadCapacityActivity` | Load available slots/capacity for tenant/location/date/time slot. | Read-only. Writes `CapacityLoaded` with available count. |
| 6 | `LoadMetricsActivity` | Load allocation lookback and penalty metrics for requestors. | Read-only. Writes `MetricsLoaded` with requestor count. |
| 7 | `RunAllocationActivity` | Run the pure domain Draw algorithm with deterministic seed and inputs. | Pure/replay-safe. Writes `AllocationCompleted` with allocated/rejected/waitlisted counts and algorithm version. |
| 8 | `PersistDecisionsActivity` | Persist request status changes, slot allocation records, rejection reasons, metric changes, and Draw decision records. | Must be idempotent by Draw key and request ID. Writes `DecisionsPersisted`. |
| 9 | `QueueIntegrationEventsActivity` | Store/publish Draw and booking outcome events using the service outbox/event publisher. | Events use deterministic source event IDs where possible. Duplicate publish is safe for consumers. Writes `EventsQueued`. |
| 10 | `CompleteDrawAttemptActivity` | Mark Draw attempt completed and persist final lifecycle summary. | Idempotent finalization. Writes `Completed`. |
| 11 | `FailDrawAttemptActivity` | Mark Draw attempt failed when an unrecoverable activity error occurs. | Writes `Failed` with safe error summary. Does not expose stack traces to employee views. |

High-level flow:

1. Resolve tenant/location policy and target Draw key.
2. Acquire the Draw key for execution.
3. Lock or close the request window for that Draw key.
4. Load pending requests.
5. Load available capacity.
6. Load employee metrics.
7. Run the domain allocation algorithm.
8. Persist booking decisions.
9. Persist Draw attempt lifecycle/progress.
10. Publish Booking events through the service outbox/event publisher.
11. Mark the workflow complete or failed with safe error detail.

Workflow activity retries should be conservative. Pure read activities can retry automatically. Activities that mutate state must be idempotent and should use deterministic ids, ETags, or explicit "already applied" checks before retry is enabled.

Manual trigger behavior:

- if no Draw attempt exists, start the workflow and return `202 Accepted` with Draw key and current progress;
- if a Draw attempt is `Running`, return `202 Accepted` with current progress;
- if a Draw attempt is `Completed`, return `200 OK` with final counts;
- if a Draw attempt is `Failed`, return failure state unless the caller uses an explicit recovery/retry action.

Scheduled trigger behavior:

- scheduler computes all due Draw keys for configured tenants/locations/slots;
- each due key is submitted to the same workflow starter;
- duplicate scheduler ticks are safe because workflow ID and Draw attempt acquisition are deterministic.

## Multi-Instance Scheduling Safety

A Dapr cron binding or platform scheduler is acceptable only if multiple Booking replicas cannot execute the same Draw key more than once.

Required safety rules:

- the scheduled trigger computes deterministic Draw keys;
- execution first acquires or creates a Draw attempt using concurrency control;
- an existing `Running` or `Completed` Draw attempt prevents duplicate execution;
- an existing `Failed` Draw attempt requires explicit retry/recovery policy;
- the workflow instance ID is deterministic from the Draw key;
- the Draw repository update uses ETags, Dapr state transactions, a distributed lock, or another documented compare-and-set mechanism supported by the selected state store;
- repeated scheduler ticks and duplicate Dapr deliveries are treated as normal and idempotent.

Do not rely on "only one container receives the cron event" unless the deployment profile proves that property. The application must be safe when three Booking replicas receive the same scheduled trigger.

## Progress In UI

The UI should show Draw progress from persisted lifecycle state, not from transient process memory.

Minimum states:

- scheduled;
- running;
- policy resolved;
- requests loaded;
- capacity loaded;
- metrics loaded;
- allocation completed;
- decisions persisted;
- events queued/published;
- completed;
- failed with safe reason.

Employee views should stay safe and simple: next Draw time, whether requests can still be submitted, running/completed status, and final employee-safe outcome. HR/admin views can show operational counts and step progress. Auditor views can show lifecycle details, seed, algorithm version, and evidence references where authorized.

## Diagrid Consideration

FairSpot should remain implementable with Dapr open source. The code should use Dapr Workflow APIs and Dapr building blocks directly so it can run self-hosted on the NAS/local profile.

Diagrid can be an optional deployment/operations profile if cost, licensing, and deployment model fit the customer environment. It must not be required for the open-source baseline.
