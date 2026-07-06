# Demo and Evaluation

This page defines how FairSpot should be demonstrated to business stakeholders, client IT, operators, and future users. The shareable evaluator-facing summary is available in the [Client Evaluation Pack](./client-evaluation-pack).

## Demo Goal

The demo should prove that FairSpot is more than a booking form. It should show fair access to scarce shared capacity, with parking as the first concrete proof path. Seats, sport courts, desks, lockers, chargers, and similar bookable resources are part of the same product model and should reuse the same tenant, policy, notification, audit, and reporting foundations with resource-specific rules where needed.

## Demo Roles

| Role | What They Should See | Success Signal |
| --- | --- | --- |
| Employee | Login, view own bookings, submit a parking request, cancel, confirm usage, receive status notification. | Employee understands request status and next action without seeing hidden lottery details. |
| Company-car employee | HR-assigned company-car handling with a fixed compatible parking slot. | The employee sees that the space is ready when the request is on time, and the priority is explainable as an HR/facilities-controlled obligation rather than lottery preference. |
| HR / facilities | Tenant policy, location override, slots/capacity, Draw outcome, operational reporting. | HR can manage parking rules without code changes and can explain outcomes. |
| Tenant admin | User roles, tenant/location setup, policy configuration, slot setup. | Admin can see which setup is required before go-live. |
| Auditor / compliance | Audit query, pseudonymised actor references, GDPR PII mapping erasure behavior. | Audit evidence exists without exposing unnecessary personal data. |
| Client IT / operator | Local stack, demo deployment option, Dapr components, metrics/logs/traces, backup/restore and incident notes. | IT can see how FairSpot plugs into their environment and observability stack. |
| Sponsor / procurement | Product value, deployment ownership model, cost path, license posture, implementation roadmap. | Sponsor can decide whether FairSpot is worth a pilot. |

## Demo Data Set

| Data Set | Purpose |
| --- | --- |
| Tenant with one office location | Keeps the story simple for first demo. |
| Employees with normal parking needs | Shows regular request and allocation behavior. |
| Company-car employees | Shows HR-assigned fixed-slot allocation outside the Tier 2 fairness lottery. |
| HR/admin users | Shows policy and slot configuration. |
| Auditor user | Shows audit query and erasure workflow. |
| Enough requests to exceed capacity | Shows why fairness and Draw are needed. |
| Notifications and reporting examples | Shows operational evidence after the allocation flow. |

## Green Logistics Sandbox Reset

Green Logistics is the primary hosted sandbox tenant. Its demo data should be predictable enough for repeated customer evaluation, but reset controls must never apply to real customer tenants.

| Topic | Release 1 behavior |
| --- | --- |
| Schedule | Nightly reset window is 02:00 UTC through the `sandbox-reset-scheduler` Dapr cron binding. |
| Activation | The scheduler and destructive reset are both fail-closed. `SandboxReset__Scheduler__Enabled=true` enables the scheduled tick. Actual purge/reseed requires `SandboxReset__Enabled=true` and registered tenant-store purgers. |
| Target | The default scheduled target is `greenlogistics`. The reset service must verify that the tenant is a resettable sandbox before any purge work starts. |
| Manual path | Platform operators may trigger `POST /platform/tenants/greenlogistics/reset-sandbox` when a manual reset is needed. |
| Evidence | Platform readers, including auditors, can inspect `GET /platform/tenants/greenlogistics/reset-sandbox` for the last status, timestamps, source, snapshot version, and aggregate purge counts. Evidence must not expose secrets or raw personal data. |
| Missed run | A missed nightly run is not caught up automatically. Operators verify the latest reset timestamp and run a manual reset if the demo environment must be refreshed before a customer session. |
| Credentials | Demo account passwords are not rotated automatically in Release 1. If a demo password must be changed, rotate it manually in the identity provider and do not commit or log generated credentials. |

The detailed hosted-operator procedure belongs in the private platform runbooks. This public page records the product and safety contract only.

### Activating and verifying the reset (PLAT003C)

**Where the flag lives.** Activation is env-gated and OFF by default (fail closed). The container/hosted compose files (`docker-compose.services.yml`, `docker-compose.services.images.yml`) read on `fairspot-customer`:

- `SandboxReset__Enabled=${FPS_SANDBOX_RESET_ENABLED:-false}` — arms the destructive purge/reseed.
- `SandboxReset__Scheduler__Enabled=${FPS_SANDBOX_RESET_SCHEDULER_ENABLED:-false}` — arms the nightly cron tick.

Set `FPS_SANDBOX_RESET_ENABLED=true` (and `FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true` for the scheduler) **only** in a demo / Green Logistics evaluation profile. Do **not** enable it in `appsettings.Development.json` or a generic developer profile — the reset is destructive. A non-sandbox tenant is always refused before any purge, from stored metadata, so real customer tenants can never be reset through this path.

**Local live gate (seed → reset → reseed).** From a machine with Docker + host `curl`/`python3`:

```
FPS_SANDBOX_RESET_ENABLED=true FPS_SANDBOX_RESET_SCHEDULER_ENABLED=true \
  ./tools/start-container-stack.sh --seed        # stack up + Green Logistics seeded (Development)
./tools/reset-sandbox-gate.sh                    # trigger a reset and assert the full cycle
```

The gate triggers the reset and asserts `status=Succeeded` for `greenlogistics` (seed → purge fan-out → reseed of the golden dataset). It uses the internal scheduler route (`POST /sandbox-reset-scheduler`), which is the reproducible local trigger: the manual platform endpoint needs a `platform_operator` token and the platform plane is dormant in the local realm, whereas the internal route is `[DaprInternalOnly]` and, in Development with `APP_API_TOKEN` unset, is reachable in-cluster for the gate.

**Verifying success.**

- Local: the gate prints `PASS` and the customer log line `Scheduled sandbox reset: tenant=greenlogistics status=Succeeded`.
- Hosted: platform readers (incl. auditors) inspect `GET /platform/tenants/greenlogistics/reset-sandbox` for status / source / started + completed timestamps / snapshot version / aggregate purge counts, and the Audit trail carries an immutable `platform.sandboxReset` record. Neither surface exposes secrets, tokens, raw user ids, or PII.

**Re-running.** The reset is idempotent and dedup-leased per UTC day. `reset-sandbox-gate.sh` **clears the day's lease before it triggers**, so every gate run drives a real reset. A *manual* re-trigger of `POST /sandbox-reset-scheduler` without clearing the lease is a no-op the scheduler logs as `already claimed … skipping` (the earlier reset stands); the gate treats that as a failure by default (pass `ALLOW_SKIPPED=1` to accept it). To force a fresh run manually, clear the lease (`DELETE /v1.0/state/customerstore/sandbox-reset:lease` on the customer Dapr sidecar) or wait for the next UTC window.

## Demo Tracks

| Track | Goal |
| --- | --- |
| Employee mobile demo | Show the employee self-service path: login, My Spots, request, cancel/confirm, notifications, profile, and Draw schedule visibility. |
| HR / facilities backend demo | Show policy, slots, reporting, and operational evidence. |
| Auditor demo | Show pseudonymised audit query and GDPR erasure behavior. |
| Client IT demo | Show the containerized local/NAS stack, Dapr component boundary, and observability approach. |
| Sponsor evaluation | Show value, roadmap, costs, and deployment ownership model. |

For the live, per-slice delivery state and remaining gaps per track, see the [Roadmap](./roadmap) and [Implementation Tracker](./implementation-tracker). (The previous "Gaps To Close" column listed slices such as `MOB006`–`MOB009`, web/admin UI, and `OPS001`–`OPS003` that are now delivered.)

## Client-Facing Materials

The first version of these materials is collected in the [Client Evaluation Pack](./client-evaluation-pack). Keep this table as the checklist for future improvements.

| Material | Audience | Purpose |
| --- | --- | --- |
| One-page product summary | Sponsor, business evaluator | Explain the customer need, product value, and parking-led proof scope. |
| Role-based demo script | Demo facilitator | Keep employee, HR, auditor, and operator demos consistent. |
| Architecture overview | Architect, client IT | Show ArchiMate-style layers, Dapr boundaries, services, and data/security controls. |
| Deployment and operations summary | Client IT, operator | Explain local/demo/client-owned production options and pluggable components. |
| Security and GDPR summary | Security reviewer, DPO | Summarize roles, data classes, audit, erasure, encryption, secrets, and traceability. |
| Cost and hosting assumptions | Sponsor, procurement, client IT | Explain demo cost path, production ownership, and usage/performance tracking. |
| Commercialisation options note | Sponsor, procurement | Explain that support subscription, dual licensing, dedicated tenant packaging, and enhanced reports are future options, not current product promises. |
| FAQ | All evaluators | Capture common product, security, deployment, and roadmap questions. |

## Demo Readiness Checklist

- Seeded tenant, users, roles, locations, policies, slots, and request history exist.
- Each demo role has a known login and a scripted path.
- Data is fake and safe to share.
- Demo can be reset without manual database editing.
- Any evaluator-facing seed/reset action is authenticated, rate-limited where practical, and limited to synthetic sandbox tenants.
- Demo credentials are shared only with approved evaluators or issued through a controlled request flow.
- Expected notifications, audit records, and reporting results are predictable.
- Local observability shows metrics/logs/traces for the demo flow.
- Client-facing materials are linked from this page and updated before external sharing.
- Employee mobile scenarios are checked with the [Mobile Device Testing Plan](./production/mobile-device-testing) before any external demo.
