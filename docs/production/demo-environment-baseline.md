# Demo Environment Baseline

OPS002 defines the first hosted demo baseline for FPS. The demo environment is not client production. Its job is to prove the product story, exercise the pluggable runtime boundaries from OPS001, and collect enough operational evidence for Demo v0 and later client evaluation.

## Goals

| Goal | Requirement |
| --- | --- |
| Prove product behavior | A reviewer can log in as seeded demo users, submit and view bookings, receive notifications, and inspect audit/reporting evidence where available. |
| Prove deployment repeatability | The environment can be rebuilt from repository artifacts, container images, Dapr component manifests, and documented secrets. |
| Keep cost bounded | The selected profile uses small instances, scale-to-zero where safe, and explicit cost assumptions. Numeric provider pricing must be validated outside this static doc before sharing externally. |
| Keep production portable | Demo provider choices stay behind Dapr, OpenTelemetry, OIDC, and documented deployment boundaries. |
| Avoid real customer risk | Demo uses synthetic tenants, users, vehicles, bookings, notifications, and audit data unless a customer-approved pilot explicitly changes that rule. |

## Selected Baseline Profile

The first OPS002 baseline should target a low-cost managed container runtime with Dapr support or a small Dapr-capable container environment. Azure Container Apps remains the reference candidate because the existing hosting strategy already evaluates it and it supports managed Dapr, but the baseline must stay portable enough to move to another runtime.

The baseline is acceptable if it can provide:

- HTTPS ingress for the employee/API entry point;
- container deployment from built images;
- Dapr sidecars or equivalent Dapr runtime support;
- Dapr pub/sub, state store, and secret-store components;
- MongoDB-compatible persistence with backup/restore evidence;
- an OIDC provider or demo Keycloak instance;
- OpenTelemetry-compatible metrics, logs, and traces;
- documented cost assumptions and teardown/scale-down instructions.

## Runtime Shape

| Runtime part | Demo requirement | Notes |
| --- | --- | --- |
| API ingress | Public HTTPS endpoint with platform-managed or documented TLS. | Demo can use a platform hostname. Custom domains are optional. |
| Identity | Demo OIDC provider or Keycloak with seeded users and roles. | Identity may need to stay always on; do not assume scale-to-zero for active sessions. |
| Booking | Hosted service with Dapr enabled. | Must prove booking submission, draw/allocation status paths already implemented, and employee-safe reads. |
| Profile | Hosted service with Dapr enabled. | Seed only minimum profile and vehicle facts needed for demo scenarios. |
| Notification | Hosted service with Dapr enabled. | Must prove notification records and mobile/API consumption; external email can use a safe demo provider or stub if clearly marked. |
| Audit | Hosted service with Dapr enabled. | Must prove audit records are created and queryable for seeded data. |
| Configuration | Hosted service with Dapr enabled. | Must seed tenant policy and slot/location data required by booking scenarios. |
| Reporting | Hosted service when reporting is in the demo script. | Can be deferred from the first demo if the story does not include reporting views yet. |
| Mobile app | Points to the demo API/OIDC configuration. | App-store packaging remains out of OPS002 scope. |
| Web app | Optional until web/admin slices are implemented. | Do not block OPS002 on future web surfaces. |

Billing remains out of scope for the demo baseline.

## Dapr Component Contract

OPS002 must use the component names and profile boundaries established by OPS001.

| Component | Demo baseline | Evidence required |
| --- | --- | --- |
| Pub/sub | Dapr-compatible broker such as RabbitMQ, managed broker, or provider-native broker behind Dapr. | A booking event reaches Notification, Audit, and Reporting consumers where implemented. |
| State store | MongoDB-compatible store. | Tenant-specific collections and indexes are provisioned or verified repeatably. |
| Secret store | Vault, provider key vault, or another real secret-management service behind Dapr. | No secret values appear in manifests, logs, screenshots, or docs. |
| Bindings | Cron/scheduler and optional input bindings as required by implemented slices. | Draw or scheduled behavior can be triggered or explicitly marked out of the demo script. |
| Service invocation | Dapr service invocation or platform routing for internal service calls. | Internal endpoints are not exposed publicly unless required. |
| Observability | OpenTelemetry collector/exporter path. | Logs, traces, and basic metrics are visible for a demo run. |

## Secrets And Data Handling

Demo secrets are still Secret data. They must be created through repository environment secrets, runtime managed identity, provider key vault, Vault, or another selected secret store. They must not be committed to git or pasted into GitHub issues.

Demo data rules:

- use synthetic tenant names, users, emails, vehicle identifiers, and bookings by default;
- keep any email delivery clearly marked as demo/staging;
- do not use real license plates, employee numbers, company emails, customer IdP exports, or production-like secrets unless a customer-approved pilot explicitly requires it;
- make the seed/reset path repeatable;
- document any retained demo data and how to delete it.

## Seeded Demo Scenarios

The first hosted demo should support these scenarios:

| Scenario | Minimum proof |
| --- | --- |
| Employee login | Seeded employee authenticates through demo OIDC and `GET /me` resolves tenant/user context. |
| Submit booking | Employee submits a request using existing Booking APIs/mobile flow. |
| View bookings | Employee sees current and historical booking status. |
| Notification | Employee sees booking-related notification records and unread count behavior. |
| Audit evidence | Operator can show audit records for booking and policy-sensitive actions without exposing unnecessary PII. |
| Configuration evidence | Operator can show the seeded tenant policy/slot setup that drives the scenario. |
| Event flow | At least one Booking event is consumed by downstream services through the configured Dapr pub/sub component. |

Optional for first demo:

- reporting dashboard/export evidence;
- email delivery through a real staging provider;
- hosted mobile install/distribution;
- rollback demonstration.

## Smoke Test Checklist

Before the environment is used for a demo, record the result of:

| Check | Expected result |
| --- | --- |
| Deployment | Latest intended image versions are deployed and identifiable. |
| HTTPS | External endpoint uses HTTPS and the expected hostname. |
| Auth | Seeded employee and admin/operator users can authenticate. |
| Tenant context | APIs derive tenant/user from authenticated context, not request body values. |
| Dapr health | Required sidecars/components report healthy or equivalent runtime status. |
| Persistence | Tenant collections/indexes exist for services used in the demo. |
| Pub/sub | Booking event reaches at least Notification and Audit consumers. |
| Notification API | Notification history and unread count respond for the seeded employee. |
| Audit API | Audit query returns expected seeded/demo events for authorized access only. |
| Logs/traces | A demo request can be found in logs/traces without leaking Secret data. |
| Reset | Seed/reset instructions restore the demo to a known state. |
| Teardown/scale-down | Cost-control steps are documented and tested where safe. |

## Cost Evidence

The demo cost model should be recorded as assumptions, not promises:

| Cost driver | Evidence to collect |
| --- | --- |
| Container runtime | Instance size, replica minimums, scale-to-zero behavior, and idle cost assumptions. |
| Identity | Whether the IdP is always on, managed, or reused from a shared demo tenant. |
| Persistence | Storage tier, backup setting, estimated data volume, and restore evidence. |
| Broker | Broker type, message volume assumptions, and metric visibility. |
| Secrets | Secret-store option and whether it has fixed monthly cost. |
| Observability | Log/trace retention, sampling, dashboard cost, and exporter target. |
| Network/ingress | Public endpoint, TLS, egress assumptions, and custom domain cost if used. |

Provider prices change often. Any external evaluator-facing number must be checked against the provider pricing page or client platform estimate at the time of sharing.

## Rollback, Reset, And Teardown

OPS002 does not require full production-grade deployment automation, but the demo must be recoverable:

- keep the previous known-good image tags visible;
- document how to redeploy or roll back each service;
- keep Dapr component manifests versioned and environment-specific secret values outside git;
- provide a seed/reset procedure for synthetic tenants and users;
- provide teardown or scale-down steps for idle periods;
- record which state is safe to delete and which artifacts are needed for evidence.

## Out Of Scope

- client-owned production deployment;
- production SLO/SLA commitment;
- real customer data migration;
- app-store packaging;
- billing/payment integration;
- full Kubernetes platform unless selected explicitly for the demo provider;
- enterprise observability integration beyond proving an OpenTelemetry-compatible export path.

## Handoff To Later Slices

| Slice | How OPS002 feeds it |
| --- | --- |
| `DOCS001` | Provides the deployment summary, demo script evidence, and cost assumptions for the client evaluation pack. |
| `OPS003` | Separates demo ownership from client-owned production responsibilities. |
| `OPS004` | Identifies the first dashboards, metrics, traces, and operational evidence to harden. |
| `OPS005` | Surfaces integration secret and telemetry requirements for customer-system integration actors. |
