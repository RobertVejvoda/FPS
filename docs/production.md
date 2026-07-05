# Production

Production describes how FairSpot is hosted, operated, recovered, and validated once it runs outside local development. It is a top-level architecture section because production concerns cut across the technology stack, security model, implementation slices, and business continuity expectations.

The goal for v1 is not to operate production for clients directly. FairSpot must prove that the platform can be run locally, demonstrated in a realistic hosted environment, and deployed into a client-owned production environment with clear operational evidence. Dapr is the component portability boundary; OpenTelemetry is the telemetry portability boundary.

FairSpot is therefore a **bring-your-own-cloud** platform. The core architecture defines contracts for identity, ingress, service integration, persistence, messaging, secrets, object storage, observability, backup, restore, and operations. Release 1 uses the NAS/Cloudflare hosted evaluation path, and the FairSpot-operated cloud-hosted follow-up target is DigitalOcean. Client-owned cloud, Kubernetes, or on-premises infrastructure can still satisfy the same contracts as long as tenant isolation, security controls, and operational evidence remain intact.

FairSpot is also a Dapr-first production reference for later systems. New production-facing slices should use Dapr building blocks first when they match the requirement: Workflow for orchestration, pub/sub for domain events, transactional outbox for state-plus-event reliability where supported, secret stores for runtime secrets, mTLS/Sentry for service identity, resiliency policies for dependency behavior, and state encryption where the selected component supports it.

## TOGAF Placement

Operations runbooks are implementation-governance evidence, not a separate architecture layer.

| TOGAF Area | How Operations Runbooks Are Used |
| --- | --- |
| Phase D - Technology Architecture | Deployment profiles, runtime platform, observability, Dapr component standards, backup/restore expectations, and security boundaries define the target technology architecture. |
| Phase E - Opportunities and Solutions | Hosted pilot, Dapr hardening, backup/restore, and observability gaps become work package groups in [Transition Architectures](./architecture/architecture-states/transition-architectures). |
| Phase F - Migration Planning | Runbooks provide the migration evidence needed to sequence hosted pilot and client-owned production work. |
| Phase G - Implementation Governance | Smoke tests, reset evidence, incident/maintenance runbooks, restore drills, and Dapr validation prove whether implementation conforms to the architecture. |
| Phase H - Architecture Change Management | Operational incidents, waivers, provider changes, and accepted risks feed [Change Control](./architecture/governance/change-control), [Waivers](./architecture/governance/waivers), and [Versions and Decisions](./versions-and-decisions). |

## Production Story

Read this section from high level to detail:

1. **Environment profiles**: separate local development, demo, and client-owned production responsibilities.
2. **Target runtime**: understand what must run and which cloud services are replaceable behind Dapr.
3. **Demo baseline**: prove a low-cost hosted environment before client production work.
4. **Availability and recovery**: define what can fail, how FairSpot keeps operating, and how much data loss/downtime is acceptable.
5. **Data protection**: define backups, restore drills, tenant-scoped recovery, and secret recovery.
6. **Operations**: define monitoring, alerts, incidents, maintenance, and runbooks.
7. **Provider setup**: keep deployment-profile choices explicit and avoid provider assumptions in application architecture.
8. **Testing and readiness**: prove the environment before calling it production.

## Environment Profiles

| Profile | Owner | Purpose | Expected shape |
| --- | --- | --- | --- |
| Local | FairSpot delivery team | Develop and validate behavior cheaply. | Docker Compose or local containers with local Dapr components and local equivalents for identity, storage, broker, cache, secrets, and observability. |
| Demo | FairSpot delivery team | Show a working system to evaluators and collect performance/usage evidence. | NAS/Cloudflare for Release 1 evaluation; DigitalOcean for the cloud-hosted follow-up profile using replaceable Dapr components. |
| Client production | Client IT / operations | Run FairSpot with the client's identity, hosting, monitoring, backup, and security controls. | Client-owned cloud or on-premise environment, Dapr-compatible components, OpenTelemetry export to the client's observability platform, documented backup/restore and support boundaries. |

## Target Runtime

FairSpot production runtime is expected to contain:

| Capability | Production role | Current direction |
| --- | --- | --- |
| Container hosting | Runs the .NET services, web app, and supporting workers. | Replaceable by profile: local containers, low-cost demo hosting, or client-owned platform. |
| Dapr sidecars | Service invocation, pub/sub, state-store integration, secret access, and future workflows. | Dapr remains the portability boundary. |
| API ingress | Public HTTPS entry point and routing to services. | Selected by deployment profile; must support TLS, routing, and rate limiting/WAF where required. |
| Identity provider | OIDC/OAuth login, JWT claims, roles, and tenant/user context. | Selected by deployment profile or client IdP standard. |
| Write/read persistence | Service-owned operational and read-model stores with tenant-safe collections, partitions, or keys. | Selected by deployment profile; must support backup, restore, encryption, and repeatable tenant provisioning. |
| Message broker | Booking events to Notification, Audit, Reporting, and future consumers. | Dapr pub/sub component bound to an approved broker or provider-native event service. |
| Cache/session support | Cache, rate limiting, and short-lived operational state. | Selected by deployment profile. |
| Secret management | Credentials, certificates, API keys, and deployment secrets. | Selected by deployment profile; no inline secrets in manifests or logs. |
| Object storage | Reports, exports, backup artifacts, and future attachments. | Selected by deployment profile; tenant-scoped paths and encryption required. |
| Observability | Metrics, logs, traces, dashboards, alerting, and usage evidence. | OpenTelemetry-compatible export to the selected local, demo, or client observability platform. |

## Operational Pages

Detailed hosted-operator runbooks for the FairSpot-operated pilot live in the private `fairspot-platform` repository after #684. The public pages below keep stable responsibility contracts, readiness expectations, and customer/self-hosting architecture references.

- [Availability Model](./production/availability-model): service, data, broker, identity, and deployment failure assumptions.
- [RTO/RPO Requirements](./production/rto-rpo-requirements): recovery time and recovery point targets by capability.
- [Backup And Restore](./production/backup-restore): public backup/restore responsibility contract; private operator procedure lives in `fairspot-platform`.
- [Monitoring](./production/monitoring): metrics, logs, traces, dashboards, alerts, and hosted-provider monitoring boundaries.
- [Incident Handling](./production/incident-handling): public incident classification and communication contract; private operator procedure lives in `fairspot-platform`.
- [Maintenance](./production/maintenance): public maintenance responsibility model; private operator procedure lives in `fairspot-platform`.
- [Operational Evidence Checklist](./production/operational-evidence-checklist): provider-neutral observability, backup, restore, and incident readiness checklist, with a restore-drill evidence record and post-restore smoke checks.

## Cloud And Environment Notes

- [Hosting and Deployment Strategy](./production/hosting-deployment-strategy): deployment profile strategy covering local, demo, and client-owned production with Dapr component portability and cost planning.
- [Deployment Profile Template](./production/deployment-profile-template): reusable template separating provider-neutral contracts from profile-specific examples, with the Local, NAS/Cloudflare, DigitalOcean, and Client-owned/BYOC profiles filled in.
- [DigitalOcean Setup](./production/digitalocean-setup): FairSpot-operated cloud-hosted follow-up target after Release 1 NAS/Cloudflare evaluation.
- [Dapr-First Production Standards](./production/dapr-first-production-standards): production-grade Dapr usage rules for workflows, outbox, pub/sub, state, secrets, mTLS, resiliency, and validation.
- [Demo Environment Baseline](./production/demo-environment-baseline): OPS002 baseline for low-cost hosted demo scope, components, seed data, smoke tests, cost evidence, reset, and teardown.
- [Client Production Handoff](./production/client-production-handoff): OPS003 responsibility split, Dapr component replacement boundaries, identity integration requirements, backup/restore handoff, release process, and client IT checklist.
- [Customer-First Deployment Gap Analysis](./production/customer-first-deployment-gap-analysis): gap analysis and prioritized slices for the NAS-hosted, Cloudflare-protected public-domain pilot path.
- [Integration Evidence](./production/integration-evidence): OPS005 safe credential handling and evidence boundaries for integration actors.
- [Local Test Harness](./production/local-test-harness): current local run instructions and one-command harness for full-stack smoke testing.
- [Draw Scheduling And Workflow](./production/draw-scheduling-and-workflow): Dapr Workflow direction, on-demand Draw behavior, multi-instance scheduler safety, and UI progress requirements.
- [Draw REST Client Scenarios](./production/draw-rest-client-scenarios.http): VS Code REST Client smoke scenarios for booking, Draw trigger, idempotency, status, and lifecycle checks.

Provider-specific setup notes and local development environment details should stay out of the public production overview unless they are needed for client evaluation. `OPS000` selected the need for a pluggable Dapr-first strategy. The current FairSpot-operated hosted path is NAS/Cloudflare for Release 1, with DigitalOcean approved as the cloud-hosted follow-up target.

## Testing And Readiness

- [Testing](./production/testing): test types used to build production confidence.
- [Testing Scenarios](./production/testing-scenarios): concrete production-readiness scenarios to validate before hosted pilot.
- [Mobile Device Testing](./production/mobile-device-testing): runbook and scenario plan for Expo/device testing of the employee mobile app.

Minimum readiness before a hosted pilot:

- deployment can be repeated without manual server edits;
- ingress uses HTTPS and documented hostnames;
- tenant and user context come from the identity provider;
- tenant storage scopes and indexes are provisioned repeatably;
- Dapr pub/sub is configured and validated with the selected broker/provider;
- Dapr production building blocks are configured where applicable: mTLS, secret store references, component scopes, resiliency policies, state transactions/outbox, and state encryption support;
- secrets are injected from a secret-management system, not committed files;
- backup and restore have been tested at least once;
- metrics, logs, traces, usage counters, and alert routing exist;
- telemetry can be exported through OpenTelemetry to a client monitoring platform;
- incident and rollback runbooks are documented;
- `./tools/validate.sh` and relevant frontend/mobile checks pass before deployment.
- mobile device smoke or pilot evidence exists for any environment used in an employee demo.

## Slice Mapping

Production work is tracked through these slices:

| Slice | Purpose |
| --- | --- |
| `OPS000` | Hosting and deployment strategy options; merged as the baseline for pluggable environments. |
| `OPS001` | Pluggable Dapr component baseline, tenant collection/index provisioning, secrets, and runbooks. |
| `OPS002` | Demo environment baseline, smoke checklist, reset/teardown path, and cost evidence for evaluation. |
| `OPS003` | Client-owned production integration guide. |
| `OPS004` | Observability, performance evidence, backup/restore verification, and production runbooks. |
| `OPS006` | Local test harness with one-command stack startup, mobile gateway URL, seeded data, and smoke-test instructions. |

Production pages should be updated whenever these slices change the target hosting model, deployment path, backup strategy, monitoring stack, or operational responsibilities.
