# Client Evaluation Pack

This pack gives a new evaluator enough context to decide whether FairSpot is worth a pilot without reading the full repository. It is intentionally business-first, with links into architecture, security, operations, and roadmap detail where needed.

## One-Page Product Summary

| Topic | Summary |
| --- | --- |
| Problem | Shared company parking often has more demand than capacity. Manual email/spreadsheet coordination is opaque, slow, hard to audit, and can feel unfair to employees. |
| Product | FairSpot is an open-source, multi-tenant fair allocation platform for limited workplace resources. Parking is the first module, with documented fairness rules, booking workflows, notifications, audit records, reporting, and tenant policy configuration. |
| Primary user value | Employees can request, view, cancel, and confirm parking from mobile without seeing hidden allocation internals or other employees' data. |
| Business value | HR/facilities teams can configure policy and capacity, reduce manual coordination, and explain allocation outcomes using audit and reporting evidence. |
| Trust value | Allocation rules, notifications, audit, GDPR erasure behavior, and tenant isolation are explicit rather than implicit operational habits. |
| Deployment posture | FairSpot is designed for local development, a low-cost hosted demo, and later client-owned production. Dapr is the component portability boundary; OpenTelemetry is the observability boundary. |
| Current status | Core backend flow, mobile employee foundation, notification/audit/reporting/configuration services, Dapr component baseline, SSO-first integration contract, and demo environment baseline are documented or implemented. |
| Near-term gaps | Mobile profile/draw visibility/polish, web/admin surfaces, hosted demo evidence, client-owned production handoff, observability hardening, and pilot-specific evidence. |

## Evaluator Paths

| Evaluator | Read first | What to check |
| --- | --- | --- |
| Sponsor / business owner | [Strategy](./strategy), [Demo and Evaluation](./demo-and-evaluation), [Roadmap](./roadmap) | Product value, demo scope, pilot readiness, and roadmap credibility. |
| HR / facilities | [Business](./business-layer), [Allocation Rules](./business-layer/allocation-rules), [Booking](./business-layer/booking) | Policy fit, fairness rules, employee-visible reasons, and operational workflow. |
| Client IT / architect | [Architecture Summary](./architecture-views), [Production](./production), [Hosting Strategy](./production/hosting-deployment-strategy) | Service boundaries, Dapr/OpenTelemetry portability, identity integration, and deployment ownership. |
| Security / DPO | [Security Review Pack](./security/security-review-pack), [Gap Register](./security/gap-register), [Security Model](./security/security-model), [Data Privacy](./security/data-privacy) | Security posture overview, BYOC boundaries, GDPR alignment, known gaps, and client review checklist. |
| Operator | [Demo Environment Baseline](./production/demo-environment-baseline), [Monitoring](./production/monitoring), [Backup And Restore](./production/backup-restore) | Runtime shape, smoke tests, telemetry, backup/restore, reset, rollback, and cost evidence. |

## Role-Based Demo Script

Use synthetic demo data only unless a customer-approved pilot explicitly changes that rule.

| Step | Role | Demo action | Evidence to show |
| --- | --- | --- | --- |
| 1 | Employee | Log in through demo OIDC and open the mobile shell. | `GET /me` resolves tenant/user context; no tenant/user is entered manually. |
| 2 | Employee | Submit a parking request for a constrained date/location. | Booking request appears with safe status and employee-visible reason where applicable. |
| 3 | Company-car employee | Submit a request that demonstrates priority policy. | Company-car priority is explainable through policy, not manual favoritism. |
| 4 | HR / facilities | Show tenant policy, location override, slots, and capacity. | The allocation behavior maps to configured policy and capacity. |
| 5 | System / operator | Run or show the Draw/allocation result for seeded requests. | Scarce spaces are allocated by documented rules; hidden lottery internals stay out of employee views. |
| 6 | Employee | View booking result and notification state. | Notification history/unread behavior reflects the booking event. |
| 7 | Auditor | Query audit records for booking and policy-sensitive actions. | Audit uses stable/pseudonymised identifiers and avoids unnecessary PII. |
| 8 | Operator | Show logs/traces/metrics for the demo request path. | OpenTelemetry-compatible evidence exists without exposing Secret data. |
| 9 | Sponsor | Review roadmap and deployment ownership model. | Remaining work is explicit and separated into demo, pilot, client evaluation, and production handoff slices. |

## Architecture Overview

FairSpot is organized around bounded services and explicit integration boundaries:

| Area | Current shape |
| --- | --- |
| Booking | Core domain for requests, Draw, allocation, cancellation, reallocation, usage confirmation, no-show, and manual correction. |
| Identity/Profile | Authenticated context, tenant/user/role claims, profile and vehicle facts needed by policy. |
| Notification | In-app records/API/SSE and email delivery for booking operational notifications. |
| Audit | Append-only pseudonymised records, auditor query, and GDPR PII mapping erasure support. |
| Configuration | Tenant policy, location overrides, and slot/capacity setup. |
| Reporting | Tenant-scoped operational reporting read models and fairness summaries. |
| Mobile | Expo React Native employee self-service foundation. |
| Runtime | Dapr for pub/sub, state, secrets, bindings, and service invocation; OpenTelemetry for metrics, logs, and traces. |

Key architecture links:

- [Architecture Views](./architecture-views)
- [Software Architecture](./technology-layer/software-architecture)
- [Production Model](./production)
- [Hosting Strategy](./production/hosting-deployment-strategy)

## Deployment And Operations Summary

| Profile | Owner | Purpose | Current expectation |
| --- | --- | --- | --- |
| Local | FPS delivery team | Development and validation. | Local containers with local Dapr components and local equivalents for identity, storage, broker, cache, secrets, and observability. |
| Demo | FPS delivery team | Evaluation environment and operational evidence. | Low-cost hosted profile with synthetic data, HTTPS ingress, Dapr components, OIDC, smoke tests, reset/teardown, and cost evidence. |
| Client production | Client IT / operations | Real operation under client controls. | Client-owned platform, IdP, persistence, secrets, observability, backups, incident process, and release controls. |

The demo environment baseline is documented in [Demo Environment Baseline](./production/demo-environment-baseline). Client production handoff remains a later slice; FairSpot should not promise managed production operation until that model is explicitly approved.

## Security And GDPR Summary

| Topic | FairSpot position |
| --- | --- |
| Tenant isolation | Tenant context comes from authenticated or trusted service context. Employee APIs must not accept arbitrary tenant/user values from request bodies. |
| Data minimisation | FairSpot stores the minimum employee/profile facts needed for booking, notification, audit, reporting, and support. |
| SSO-first integration | Company employees authenticate through the customer's IdP by default. FairSpot does not store company passwords. |
| Local accounts | FairSpot-local accounts are fallback/break-glass/demo accounts. Credential verifiers are Secret data owned by Identity. |
| Audit | Audit records use stable/pseudonymised identifiers where possible and preserve allocation and policy-sensitive evidence. |
| GDPR erasure | PII mapping erasure can remove identity mapping while preserving anonymous audit history. |
| Secrets | Tokens, keys, client secrets, connection strings, credential verifiers, and integration credentials are Secret data and must not appear in docs, logs, issues, or manifests. |
| Demo data | Demo uses synthetic data unless a customer-approved pilot explicitly allows otherwise. |

## Cost And Hosting Assumptions

Cost discussion should stay profile-based until a provider and region are selected:

| Cost area | Assumption to validate |
| --- | --- |
| Container runtime | Idle cost, minimum replicas, scale-to-zero support, and always-on identity requirements. |
| Persistence | Selected storage tier, backups, tenant storage volume, provisioning model, and restore evidence. |
| Broker | Dapr-compatible broker choice and message visibility. |
| Secrets | Secret-management platform cost, access model, and integration effort. |
| Observability | Log/trace retention, sampling, dashboard cost, and exporter target. |
| Network/ingress | HTTPS endpoint, custom domain, certificates, egress, and private networking needs. |

Provider prices change frequently. Do not present numeric cost commitments without checking current provider pricing or a client platform estimate at the time of sharing.

## FAQ

| Question | Answer |
| --- | --- |
| Is FairSpot only for parking? | Parking is the first concrete v1 domain. The same tenant, policy, notification, audit, and reporting pattern may later support other scarce workplace resources after parking is stable. |
| Does FairSpot replace the customer's identity provider? | No. FairSpot is SSO-first and expects company users to authenticate through the customer IdP where possible. |
| Does FairSpot store company passwords? | No. Company passwords must stay with the IdP. FairSpot-local credential verifiers are fallback Secret data only. |
| Can employees see other employees or lottery internals? | Employee views must remain safe: own bookings, own notifications, and understandable reasons without exposing other employees or hidden allocation internals. |
| Who operates production? | The current direction is client-owned production. FairSpot provides architecture, deployment guidance, component boundaries, and evidence; managed operation is not promised. |
| Can a client use Azure, AWS, Kubernetes, or another platform? | Yes. The design keeps provider choices behind Dapr, OIDC, service-owned persistence, secret-management, object-storage, and OpenTelemetry boundaries. Each provider still needs tested component manifests and runbooks. |
| Is billing implemented? | No. Commercialisation and billing are later decisions. The current AGPL project can still support paid implementation, support, and client-specific integration services. |
| What is needed before a client pilot? | Hosted demo evidence, mobile polish/profile/draw visibility, web/admin surfaces where needed, observability and backup/restore evidence, and a client-owned production handoff model. |

## Current Demo v0 Evidence

| Slice | Status | Evidence |
| --- | --- | --- |
| `MOB006` Mobile Notifications | Done | Mobile notification consumption, unread count, mark-read action, and polling fallback. |
| `OPS001` Pluggable Dapr Component Baseline | Done | Local/demo/client component profile direction, state/pubsub/secrets naming, and local setup docs. |
| `OPS002` Demo Environment Baseline | Done | Hosted demo scope, components, synthetic data rules, smoke checks, reset/teardown, and cost-evidence model. |
| `CUST002` SSO-First Customer Integration Contract | Done | Trusted issuer/tenant mapping, minimal profile facts, local-account fallback, import constraints, audit/GDPR requirements. |
| `DOCS001` Client Evaluation Pack | Done | This page. |

The pack is a starting point for conversation, not a substitute for a live pilot plan. Before sharing externally, update any environment-specific evidence, screenshots, URLs, and cost assumptions to match the actual demo environment.
