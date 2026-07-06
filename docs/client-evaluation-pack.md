# Client Evaluation Pack

This pack gives a new evaluator enough context to decide whether FairSpot is worth a pilot without reading the full repository. It is intentionally business-first, with links into architecture, security, operations, and roadmap detail where needed.

## One-Page Product Summary

| Topic | Summary |
| --- | --- |
| Problem | Scarce shared capacity often has more demand than supply. Parking is the first visible pain point, but the same coordination challenge appears with seats, sport courts, desks, lockers, chargers, and other limited resources. Manual email/spreadsheet coordination is opaque, slow, hard to audit, and can feel unfair to users. |
| Product | FairSpot is an open-source, multi-tenant fair allocation and booking platform for B2B organizations with limited shared capacity. Parking is the first launch module, with documented fairness rules, booking workflows, notifications, audit records, reporting, and tenant policy configuration. |
| Primary user value | Users can request, view, cancel, and confirm resource bookings without seeing hidden allocation internals or other users' data. The current mobile flow proves this first for employees in the parking module. |
| Business value | Operators can configure policy and capacity, reduce manual coordination, and explain allocation outcomes using audit and reporting evidence. |
| Trust value | Allocation rules, notifications, audit, GDPR erasure behavior, and tenant isolation are explicit rather than implicit operational habits. |
| Deployment posture | FairSpot is designed for local development, NAS/Cloudflare Release 1 evaluation, a DigitalOcean cloud-hosted follow-up profile, and later client-owned production. Dapr is the component portability boundary; OpenTelemetry is the observability boundary. |
| Current status | Release 1 evaluation baseline: employee mobile journey, web employee + HR/admin/reporting/audit surfaces, booking/Draw lifecycle, notifications, audit, reporting, durable Dapr persistence (PERSIST001–006), and the containerized NAS/Cloudflare hosting path are implemented. The [Roadmap → Release 1 Scope](./roadmap#release-1-scope) is the authoritative status. |
| Near-term gaps | Mobile vehicle management and UX polish, broader tenant administration, client-owned production handoff, and pilot-specific evidence. See the [Roadmap](./roadmap) for the live gap list. |

## Evaluator Paths

| Evaluator | Read first | What to check |
| --- | --- | --- |
| Sponsor / business owner | [Strategy](./strategy), [Demo and Evaluation](./demo-and-evaluation), [Roadmap](./roadmap) | Product value, demo scope, pilot readiness, and roadmap credibility. |
| HR / facilities | [Business Architecture](./architecture/business/), [Business Processes](./architecture/business/business-processes), [Policies](./architecture/business/policies) | Policy fit, fairness rules, employee-visible reasons, and operational workflow. |
| Client IT / architect | [Architecture Repository](./architecture/), [Information Systems](./architecture/information-systems/), [Deployment Profiles](./architecture/technology/deployment-profiles) | Service boundaries, Dapr/OpenTelemetry portability, identity integration, and deployment ownership. |
| Security / DPO | [Security Architecture](./architecture/security/), [Security Gap Register](./architecture/security/gap-register), [Privacy Architecture](./architecture/security/privacy-architecture), [Controls](./architecture/security/controls) | Security posture overview, deployment boundaries, GDPR alignment, known gaps, and client review checklist. |
| Operator | [Demo Environment Baseline](./production/demo-environment-baseline), [Monitoring](./production/monitoring), [Backup And Restore](./production/backup-restore) | Runtime shape, smoke tests, telemetry, backup/restore, reset, rollback, and cost evidence. |

## Role-Based Demo Script

Use synthetic demo data only unless a customer-approved pilot explicitly changes that rule.

**Before you start (local container demo).** Bring the stack up with `./tools/start-container-stack.sh --seed`, then reach it at:

| What | Where |
|---|---|
| API gateway | `http://localhost:10000` |
| Web app | `./tools/start-smoke-web.sh` → `http://localhost:5200` |
| Mobile (Expo) | `./tools/start-smoke-mobile.sh` |
| Keycloak sign-in | `http://localhost:8180` (realm `fps-local`) |

Demo users live in the **Green Logistics** tenant (`tenant_id=greenlogistics`) with password `Dev1234!`: `gl-employee1` (company car), `gl-employee2` (EV), `gl-employee5` (accessible), `gl-hr-admin`, `gl-tenant-admin`, `gl-report-viewer`, `gl-auditor`. Green Logistics also demonstrates company-SSO / work-email tenant discovery (the `greenlogistics.example` domain). A second tenant, **demo**, remains a bare scaffold (no seeded booking data) used to demonstrate multi-tenant isolation. Full user list, roles, and seeded data: [Demo Seed Data](./demo-seed-data). The two sign-in paths (company SSO vs FairSpot account) are explained in [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes).

| Step | Role | Demo action | Evidence to show |
| --- | --- | --- | --- |
| 1 | Employee | Log in through demo OIDC and open the mobile shell. | `GET /me` resolves tenant/user context; no tenant/user is entered manually. |
| 2 | Employee | Submit a parking request for a constrained date/location. | Booking request appears with safe status and employee-visible reason where applicable. |
| 3 | Company-car employee | Submit an on-time request for an HR-assigned company car. | The fixed company-car slot is allocated before the fairness Draw; the employee cannot self-assign company-car status. |
| 4 | HR / facilities | Show tenant policy, location override, slots, and capacity. | The allocation behavior maps to configured policy and capacity. |
| 5 | Tenant admin / operator | Run the admin-only Demo Draw for seeded requests, or show an already completed Draw result. | Scarce spaces are allocated by documented rules; the same Draw key is idempotent; hidden lottery internals stay out of employee views. |
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

- [Architecture Repository](./architecture/)
- [Views and Diagrams](./architecture/views/)
- [Information Systems](./architecture/information-systems/)
- [Technology Architecture](./architecture/technology/)
- [Security Architecture](./architecture/security/)
- [Deployment Profiles](./architecture/technology/deployment-profiles)
- [Operations Runbooks](./production)

## Deployment And Operations Summary

| Profile | Owner | Purpose | Current expectation |
| --- | --- | --- | --- |
| Local | FairSpot delivery team | Development and validation. | Local containers with local Dapr components and local equivalents for identity, storage, broker, cache, secrets, and observability. |
| NAS / Cloudflare (Release 1 evaluation) | FairSpot delivery team / evaluator operator | Self-hosted, reviewable demo at a public HTTPS domain. | Fully containerized Docker Compose stack (services, Dapr sidecars, gateway, identity, stores) reached through a Cloudflare Tunnel. Needs only Docker + Compose on the host — no host .NET SDK or Dapr CLI. Started with `./tools/start-container-stack.sh`. |
| DigitalOcean demo | FairSpot delivery team | Cloud-hosted evaluation environment and operational evidence after the NAS/Cloudflare Release 1 path. | Low-cost hosted profile with synthetic data, HTTPS ingress, Dapr components, OIDC, smoke tests, reset/teardown, and cost evidence. |
| Client production | Client IT / operations | Real operation under client controls. | Client-owned platform, IdP, persistence, secrets, observability, backups, incident process, and release controls. |

The NAS/Cloudflare evaluation profile — the Release 1 hosting target — is documented in the [NAS / Cloudflare Deployment Profile](./production/nas-cloudflare-deployment-profile). The demo environment baseline is documented in [Demo Environment Baseline](./production/demo-environment-baseline). Client production handoff remains a later slice; FairSpot should not promise managed production operation until that model is explicitly approved.

**Release 1 evaluation boundary:** Release 1 is for synthetic/demo evaluation only and is not approved for real customer data unless explicitly agreed. The [Roadmap → Release 1 Scope](./roadmap#release-1-scope) records what is ready, demo-only, and deferred.

## Security And GDPR Summary

| Topic | FairSpot position |
| --- | --- |
| Tenant isolation | Tenant context comes from authenticated or trusted service context. Employee APIs must not accept arbitrary tenant/user values from request bodies. |
| Data minimisation | FairSpot stores the minimum user/profile facts needed for booking, notification, audit, reporting, and support. |
| SSO-first integration | Customer organization users authenticate through the customer's IdP by default. FairSpot does not store customer passwords. |
| Local accounts | FairSpot-local accounts are fallback/break-glass/demo accounts. Credential verifiers are Secret data owned by Identity. |
| Audit | Audit records use stable/pseudonymised identifiers where possible and preserve allocation and policy-sensitive evidence. |
| GDPR erasure | PII mapping erasure can remove identity mapping while preserving anonymous audit history. |
| Secrets | Tokens, keys, client secrets, connection strings, credential verifiers, and integration credentials are Secret data and must not appear in docs, logs, issues, or manifests. |
| Demo data | Demo uses synthetic data unless a customer-approved pilot explicitly allows otherwise. |
| Demo credentials | Demo credentials and seed/reset actions must be available only to approved evaluators or authenticated tenant admins, never as anonymous public functionality. |

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
| Is FairSpot only for parking? | No. Parking is the first launch module and proof vertical. The product scope is fair booking and allocation of scarce shared capacity for B2B organizations, including seats, sport courts, desks, lockers, chargers, and similar bookable resources. |
| Are users always employees? | No. Employee is the current workplace parking proof path. Other tenants may have members, guests, external participants, or public users who authenticate with their own Google, Apple, Microsoft, or customer-approved identity provider and are still scoped to one managing organization. |
| Does FairSpot replace the customer's identity provider? | No. FairSpot is SSO-first and expects customer organization users to authenticate through the customer IdP where possible. Public-participant domains may broker user-owned IdPs, but tenant membership and booking eligibility remain FairSpot/customer-controlled. |
| Does FairSpot store customer passwords? | No. Customer and external IdP passwords must stay with the IdP. FairSpot-local credential verifiers are fallback Secret data only. |
| Can users see other users or lottery internals? | User views must remain safe: own bookings, own notifications, and understandable reasons without exposing other users or hidden allocation internals. |
| Who operates production? | The current direction is client-owned production. FairSpot provides architecture, deployment guidance, component boundaries, and evidence; managed operation is not promised. |
| Can a client use its own cloud, Kubernetes, or on-premises platform? | Yes. The design keeps client provider choices behind Dapr, OIDC, service-owned persistence, secret-management, object-storage, and OpenTelemetry boundaries. FairSpot's own cloud-hosted follow-up target is DigitalOcean, not AWS or Azure. |
| Is billing implemented? | No. Commercialisation and billing are later decisions. The current AGPL project can still support paid implementation, support, and client-specific integration services. |
| What is needed before a client pilot? | Hosted demo evidence, mobile polish/profile/draw visibility, web/admin surfaces where needed, observability and backup/restore evidence, and a client-owned production handoff model. |

## Delivery Status And Evidence

This pack does not maintain its own slice-by-slice status table — it goes stale quickly. The authoritative, current status lives in:

- [Roadmap](./roadmap) — phases, milestones, and the [Release 1 Scope](./roadmap#release-1-scope) (what is ready, demo-only, and deferred).
- [Implementation Tracker](./implementation-tracker) and [Delivery Board](./delivery-board) — per-slice delivery state and evidence.

The pack is a starting point for conversation, not a substitute for a live pilot plan. Before sharing externally, update any environment-specific evidence, screenshots, URLs, and cost assumptions to match the actual demo environment.
