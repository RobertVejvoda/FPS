## About

**FairSpot** is the product name for the fair allocation platform currently implemented in the FPS repository. Parking is the first module because it is a concrete workplace resource where demand often exceeds supply. The product replaces first-come, first-served email/spreadsheet coordination with auditable allocation rules and employee-visible outcomes.

The repository, service namespaces, and some internal tooling may continue to use `FPS` as an implementation shorthand until a later rename is explicitly approved.

### Objectives

- Ensure fair distribution of parking slots.
- Automate the parking slot allocation process.
- Reduce the administrative burden on HR.
- Improve employee satisfaction with the parking system.

### Features

- Automated allocation of parking slots based on predefined rules.
- User-friendly interface for employees to request parking.
- Real-time notifications for parking slot status.
- Reporting and analytics for HR to monitor usage and trends.

### Benefits

- Fair and transparent allocation process.
- Reduced manual intervention and errors.
- Enhanced employee experience.
- Data-driven decision-making for parking management.

### Future Enhancements

- Integration with company calendar for seamless booking.
- Mobile app for on-the-go access.
- Advanced analytics for predictive parking demand.



## Versions


| Version | Date      | Author         | Role      | Comments
| ------- | --------- | -------------- | --------- | --------
| 0.1     | 1.10.2024 | Robert Vejvoda | Architect | First draft
| 0.2     | 23.11.2024 | Robert Vejvoda | Architect | Scope clarification
| 0.3     | 1.12.2024 | Robert Vejvoda | Architect | React, API gateways & domain model
| 0.4     | 9.5.2026  | Robert Vejvoda | Architect | Persistence, multi-tenancy, CQRS, stack versions
| 0.5     | 13.5.2026 | Codex          | Product Owner | Plan/status validation after MOB002 merge
| 0.6     | 22.5.2026 | Codex          | Product Owner | Product story aligned around FairSpot naming
| 0.7     | 22.5.2026 | Codex          | Product Owner | Core architecture clarified as provider-neutral bring-your-own-cloud contracts



## Document reviews


| Name | Role | Date
| ---- | ---- | ----



## Document validations


| Name | Role | Date
| ---- | ---- | ----
| Codex | Product Owner | 13.5.2026


## Decision Log


| Purpose | Rational | By | Date
| ------- | -------- | -- | ----
| ~~Public cloud hosting: MS Azure~~ *(revised 22.5.2026)* | Replaced by provider-neutral bring-your-own-cloud deployment profiles. Azure remains an implementation profile/candidate, not the core architecture. | Architect | 1.10.2024
| ASP.NET Core, .NET 9 | Modern powerful language with great community support, cross platform | Architect | 1.10.2024
| React | Web UI for both web and mobile progressive apps | Architect | 1.11.2024
| Dapr | Dapr helps with consistency, bindings and abstractions, important software component build block. | Architect | 1.10.2024
| ~~.NET MAUI~~ *(reversed 9.5.2026)* | Replaced by React Native + Expo. | Architect | 13.10.2024
| Development tools | Use Visual Studio Code for development. It does not require any licences and is effective with full language support | Architect | 13.10.2024
| ~~Xcode~~ *(reversed 9.5.2026)* | No longer required — Expo managed workflow removes need for native build tooling. | Architect | 13.10.2024
| **React Native + Expo** | Mobile platform. Expo managed workflow, no native build tooling required. Larger community, better AI support than MAUI. TypeScript consistent with React web frontend. OTA updates without App Store review. | Architect | 9.5.2026
| **No EF Core in the current implementation baseline** | Service persistence is handled through Dapr state-store/persistence adapters and service-owned read model stores. The durable architecture is the storage contract and tenant boundary, not a specific database product. | Architect | 9.5.2026
| **CQRS persistence split** | Commands use a state/persistence boundary. Queries use service-owned read models. The selected local/demo/client storage implementation must preserve tenant-safe collections, partitions, or keys. | Architect | 9.5.2026
| ~~Database-per-tenant (MongoDB)~~ *(reversed 14.5.2026)* | Replaced by collection-per-tenant. Database-per-tenant gave stronger physical isolation but created more provisioning and operational overhead than needed for the current FairSpot scale. | Architect | 9.5.2026
| **Tenant-scoped storage boundary** | Each service owns its data store and isolates tenant data through tenant-specific collections, partitions, or keys resolved from authenticated/service context. The current local implementation may use collection-per-tenant where supported, but the core architecture is provider-neutral. Repository/query code must derive storage identifiers from a sanitised tenant key, create tenant-specific indexes/metadata, and never accept storage names from callers. | Architect | 14.5.2026
| **Tenant object storage and organization branding** | Tenant onboarding should provision tenant-scoped object storage for controlled documents, reports, audit evidence, GDPR bundles, and branding assets. Prefer one bucket/container per tenant; tenant prefixes are acceptable only when documented by the deployment profile. Uploaded documents enter through FairSpot APIs with metadata, authorization, retention category, checksum, and audit records. Organization branding is v1 scope; full white-label/custom-domain support remains future scope. Web and mobile clients load branding through FairSpot client configuration, never direct MinIO/S3 paths. | Codex/Product Owner | 26.5.2026
| **Dapr 1.14+** | Minimum Dapr version updated from 1.4.0. Dapr Workflows require 1.10+. | Architect | 9.5.2026
| **.NET 10** | Upgraded from .NET 9 (LTS). Released Nov 2025. | Architect | 9.5.2026
| **Draw volume cap: 500** | Maximum 500 booking requests per tenant per Draw. Single sequential Dapr Workflow — no fan-out needed. Booking service enforces the cap at submission time. | Architect | 9.5.2026
| **Same-day booking supported** | Employees can request a slot for the current day outside the Draw (e.g. during commute). System allocates immediately if a slot is available. Same 500-cap per date applies. Consistent with process.md flow already documented. | Architect | 9.5.2026
| **Company car: auto-scheduled, guaranteed allocation** | Company car employees use the same auto-scheduler as regular employees. In the Draw, their requests are allocated first (Tier 1) before the weighted lottery runs for remaining slots. No penalty ever applies. `HasCompanyCar` flag on `UserProfile` drives this. No separate admin workflow needed. | Architect | 9.5.2026
| **Tier 2 lottery weight** | Tier 2 uses `1 / (1 + RecentAllocationCount + ActivePenaltyScore)`, calculated from a draw-time snapshot of user metrics. `RecentAllocationCount` counts successful non-company-car allocations in the tenant lookback window, including same-day allocations. The lookback window is tenant-configurable and defaults to `10` days. `ActivePenaltyScore` covers active penalties from late cancellations, no-shows, policy violations, or manual HR adjustments. Rejected requests are not in the denominator; any future reward for repeated rejected requests must be a separate positive factor. | Architect | 9.5.2026
| **Company-car capacity overflow** | If company-car requests exceed available matching capacity, FairSpot rejects the overflow requests for now. This keeps the first implementation simple and treats the case as a tenant configuration issue that should be rare. | Architect | 9.5.2026
| **Cancellation reallocation** | When an allocated reservation is cancelled and another eligible requestor exists, FairSpot automatically reallocates the released space instead of only notifying a waitlist. The action must be auditable and notification events are sent to affected requestors. | Architect | 9.5.2026
| **Executable Draw rules** | Draw implementation follows `docs/business-layer/allocation-rules.md`. The rules define duplicate detection, allocation precedence, seeded lottery reproducibility, slot matching, same-day metric updates, automatic cancellation reallocation, default penalties, audit payload, and idempotency expectations. | Architect | 9.5.2026
| **Booking request lifecycle** | Request lifecycle follows `docs/business-layer/booking-request-lifecycle.md`. Late cancellation starts after a slot has been allocated. Cancellation before allocation does not create a penalty. Usage confirmation, no-show handling, terminal statuses, employee-visible reasons, and audit requirements are defined there. | Architect | 9.5.2026
| **Parking policy configuration** | Parking policy uses tenant-level defaults with optional per-location overrides. Location overrides win for that location; missing fields fall back to tenant defaults. Required fields, defaults, slot capability settings, penalty policy, usage confirmation, and policy publication behavior are defined in `docs/business-layer/parking-policy-configuration.md`. | Architect | 9.5.2026
| **V1 notification channels** | V1 requires both in-app and email notifications for critical operational events. Preferences may control reminders and informational notifications, but cannot disable booking, allocation, rejection, cancellation, reallocation, no-show, penalty, or manual-correction notifications. Details are defined in `docs/business-layer/notification.md`. | Architect | 9.5.2026
| **Booking implementation model** | Booking will be implemented story-by-story using vertical slices. Each story cuts through domain, application, API, persistence, notification, audit, and tests where needed. The implementation order and acceptance criteria are defined in `docs/implementation/booking-vertical-slices.md`. | Architect | 10.5.2026
| **Booking Phase 1 slice completion** | Booking vertical slices B001-B010 are implemented and merged. Remaining Booking-adjacent work is integration and hardening: authenticated tenant/user/role context, Profile-provided eligibility and vehicle snapshots, Notification and Audit consumers, and production infrastructure concerns. | Architect | 10.5.2026
| **Draw cut-off: configurable, default 18:00** | Request submission cut-off is configurable per tenant, stored in Configuration service. Default is 18:00 local time. Draw workflow triggered by Dapr cron binding at the configured time. Requests after cut-off rejected; 500-cap enforced before cut-off. Docs implied the lock mechanism but did not specify timing or configurability. | Architect | 9.5.2026
| **On-demand Draw trigger guardrails** | Scheduled Draw remains the normal production path. `POST /draws/trigger` is allowed for scheduled automation, local demo, controlled operations, recovery, or support/admin action only. It is admin-only, derives tenant from authenticated context, requires explicit location/date/time slot plus reason, and is idempotent for a completed Draw key so repeat demo clicks do not reallocate. | Codex/Product Owner | 24.5.2026
| **GDPR audit: pseudonymisation** | Audit records store `actor_hash` (SHA-256 of `user_id`) — never name or email. Separate `PiiMapping` collection holds hash→identity. On GDPR erasure: delete mapping row only. Audit log remains immutable and anonymous. Chosen over field redaction to preserve append-only invariant. GDPR Article 25 explicitly names pseudonymisation as a privacy-enhancing technique. | Architect | 9.5.2026
| ~~Payment provider: stub~~ *(superseded 21.5.2026)* | Billing is deferred until the commercial model is approved. Do not add provider-specific Billing implementation based on this older placeholder. | Architect | 9.5.2026
| **Real-time frontend: SSE** | Server-Sent Events via `GET /notifications/stream` on Notification service. Dapr pub/sub events bridged to connected SSE clients. No Azure dependency, no extra infrastructure, no per-connection cost. Azure SignalR rejected (Azure-specific, cost); MQTT rejected (poor browser support). ASP.NET Core native SSE, React Native via EventSource. | Architect | 9.5.2026
| **ID001 JWT claim mapping** | `userId` resolved from `ClaimTypes.NameIdentifier` with `sub` fallback. `tenantId` from `tenant_id` custom claim. `roles` from `ClaimTypes.Role` array (empty list when absent). Missing `userId` or `tenantId` in an authenticated token returns 401 — both are required for any FPS operation. Stable names chosen for cross-service use via `ICurrentUser` in `FPS.SharedKernel`. The selected OIDC provider must emit `tenant_id` when production identity is wired. | Architect | 10.5.2026
| **Project license: AGPL-3.0-or-later** | FairSpot/FPS is licensed under the GNU Affero General Public License v3.0 or later. The project remains open source while requiring network-service distributors to provide source code for modified versions, reducing the risk of closed SaaS forks. | Architect | 11.5.2026
| **Product name: FairSpot** | FairSpot is the business-facing product name. It positions the product as fair access to limited workplace resources, starting with parking. `FPS` remains the repository and internal implementation shorthand for now to avoid broad namespace and automation churn. | Codex/Product Owner | 22.5.2026
| **FairSpot brand mark direction** | FairSpot uses a calm green abstract allocation mark rather than a literal car or parking sign. The mark must work for web, mobile, docs, and future non-parking resource allocation modules. Primary colors are charcoal `#17212B`, green `#2F7D3F`, fresh green `#43B75A`, and warm surface `#F7F4EE`. | Codex/Product Owner | 24.5.2026
| **Project copyright and contribution notice** | Repository copyright notice is recorded in `NOTICE`. Contributions, including AI-assisted changes prepared under Robert Vejvoda's direction, are accepted under AGPL-3.0-or-later unless a separate written agreement says otherwise. `CONTRIBUTING.md` records this contribution rule for future collaborators. | Architect | 11.5.2026
| **N001 notification channel split** | `N001` implements the Booking-event consumer and idempotent in-app notification records only. Email remains a v1 requirement but is implemented in a later Notification slice after the in-app record contract, recipient mapping, and deduplication behavior are stable. | Architect | 11.5.2026
| **A001 audit scope split** | `A001` implements the Booking-event Audit consumer and append-only pseudonymised audit records only. Audit query APIs, retention/backup/integrity jobs, and GDPR erasure/PiiMapping persistence are later slices after the audit record contract and idempotency behavior are stable. | Architect | 11.5.2026
| **CI visibility** | Repository entry points should show GitHub Actions red/green status for CI and documentation deployment. CI should validate code, tooling, workflow, and generated-client changes, support manual runs, and run weekly to detect environment drift. | Architect | 11.5.2026
| **MOB001 app shell scope** | The first mobile slice is an Expo managed TypeScript app shell only. It consumes generated API client types and supports development bearer-token handoff, but real login, booking workflows, push/SSE notifications, native packaging, and app-store delivery are later slices. | Architect | 11.5.2026
| **MOB002 read-only booking scope** | The second mobile slice implements My Bookings as a read-only employee screen backed by `GET /bookings`. It may show statuses, employee-visible reasons, safe allocation labels, refresh, and cursor pagination, but booking actions, real login, push/SSE, and backend behavior changes remain later slices. | Architect | 11.5.2026
| **Claude routing: handoff-only automation** | The GitHub router prepares Claude issue/PR handoff comments and updates labels, but does not invoke Anthropic automatically. This keeps agent context consistent while avoiding repeated paid runs from label events. Manual Claude invocation remains available when the task is worth the token cost. | Architect | 13.5.2026
| **Delivery workflow state machine** | Delivery status is governed by GitHub Project fields, not assignment labels. `Status` records lifecycle, `Owner` records who acts next, and `Implementer` records who implements or repairs the slice. Labels are limited to slice taxonomy, durable attribution, and temporary compatibility triggers while automation is migrated. This avoids contradictory label states across Codex, Claude, Copilot, and human work. | Codex/Product Owner | 20.5.2026
| **MOB003 mobile login scope** | The third mobile slice replaces development bearer-token handoff with OIDC Authorization Code + PKCE in the Expo app. The app validates sessions with `GET /me`, stores no client secret, and never supplies tenant/user/role identity for employee API scoping. OIDC provider provisioning, booking actions, notification streaming, native packaging, and backend business changes remain out of scope. | Architect | 13.5.2026
| **WEB009 web login scope** | The web app should replace the manual development token handoff with browser OIDC Authorization Code + PKCE. The app validates sessions with `GET /me`, stores no client secret, and never supplies tenant/user/role identity for API scoping. Manual token entry remains a local smoke-testing fallback only when explicitly enabled. Identity-provider provisioning, tenant onboarding, MFA policy design, role administration, and backend business changes remain out of scope. | Codex/Product Owner | 23.5.2026
| **Security data classification** | FairSpot classifies data as Public, Internal, Confidential, or Secret. Confidential tenant/employee data requires authenticated tenant-scoped authorization, encryption in transit and at rest, and audit for sensitive administrative access. Secret data is limited to credentials, keys, tokens, certificates, connection strings, and recovery material; it must be stored in secret-management systems, never committed or logged, and human access must be justified, time-bound, audited, and followed by rotation when exposed. | Architect | 14.5.2026
| **SSO-first customer integration** | Company users authenticate through the customer's identity provider by default. FairSpot stores only the minimum mapped subject, tenant, role, and policy facts required for booking, notification, audit, reporting, and support. FairSpot does not store company passwords. FairSpot-local accounts are fallback accounts; their credential verifiers are Secret data and must use hardened Identity storage. CSV/file import is bootstrap or exception only, never a password or broad HR-data import path. | Architect | 17.5.2026
| **Server project structure** | Default service shape is one main project plus one test project while the service remains simple. Booking remains an intentional exception with Domain, Application, Infrastructure, API, and separate test projects because it is the core domain and highest-complexity bounded context. Do not collapse Booking for symmetry; revisit only if the layering demonstrably slows delivery more than it protects domain, persistence, and API boundaries. | Codex/Product Owner | 17.5.2026
| **Commercialisation before Billing** | FairSpot stays open-source first and parking-first. The free/open core must remain useful for tenant setup, employee booking, fair allocation, audit, privacy, basic reporting, and client-owned operation. First cost-recovery candidates are support, implementation, pilot setup, production readiness, client-specific integration, enhanced reporting packs, and possibly a hosted demo or dual license after approval. Product Billing, invoice handling, subscription enforcement, and employee-level commercial metering remain deferred until a real commercial offer is approved. | Codex/Product Owner | 21.5.2026
| **Resource map and zone preference direction** | FairSpot should support tenant-maintained resource maps with zones, first for parking spaces and later for desks, chairs, seats, lockers, chargers, or similar limited resources. Employee preferred zones and team default zones are soft placement preferences by default: allocation should try them first, then fall back to another compatible resource when policy allows. Hard constraints such as accessibility, capability, reserved-only rules, and time availability still win over preference. | Codex/Product Owner | 22.5.2026
| **Provider-neutral bring-your-own-cloud architecture** | Core architecture defines contracts for identity, ingress, Dapr service integration, pub/sub, state/persistence, secret management, object storage, telemetry, backup/restore, tenant isolation, and operations. Concrete technologies such as local gateways, brokers, document stores, secret stores, and observability tools belong in local/demo/Azure/AWS/client deployment profiles, not in core architecture requirements. | Codex/Product Owner | 22.5.2026
| **Technical telemetry versus business activity** | Technical logs, metrics, and traces are operator-facing observability data in Grafana/Loki/Prometheus/Jaeger or a client equivalent. Business-facing activity timelines for auditors, HR, and tenant admins must be built from append-only Audit service records, not raw technical logs. The two streams may be linked by optional `traceId`, `spanId`, `correlationId`, and `sourceEventId` metadata. | Codex/Product Owner | 24.5.2026
| **Actor resolution through PII mapping** | Audit records store one-way `actorHash` values. The original actor can be resolved only through a separate restricted PII mapping store, with reason capture and an audit record of the lookup. GDPR erasure deletes or anonymises the mapping row while preserving immutable pseudonymised audit evidence. | Codex/Product Owner | 24.5.2026
| **Employee data erasure workflow** | User data deletion is a governed rights-request workflow, not a blind database delete. Dapr Workflow is the preferred orchestration model: it coordinates service-owned erasure activities, idempotent retries, status, and completion evidence. Each service classifies affected data as delete, anonymise, pseudonymise, or retain based on active operations, legal basis, retention policy, and audit requirements. The erasure request and service outcomes are themselves audited using pseudonymised actors and an erasure request ID. | Codex/Product Owner | 24.5.2026
| **V1 persona scope** | FairSpot v1 focuses on the core parking flow: car and company-car booking, configurable slot capabilities such as EV and accessibility, fair allocation, notifications, audit, reporting, and tenant setup. Motorcycle-specific capacity, recurring reserved-space release, sustainability incentives, and proactive optimization are future optional extensions only when customer demand justifies the extra policy and capacity complexity. | Codex/Product Owner | 24.5.2026
| **FairSpot app domain** | `fairspot.net` is reserved for the hosted FairSpot application site, starting with the demo tenant. Repository documentation remains on GitHub Pages under the existing documentation URL; do not use `docs/CNAME` for `fairspot.net`. The app domain must route through the selected demo hosting profile, with backend tenant identity still derived from authenticated claims rather than the host name alone. | Codex/Product Owner | 24.5.2026
| **Role-centered UX roadmap** | FairSpot user experiences are organized around actor intent rather than service modules. Employees start in My Spots; HR/facilities start from attention and support; auditors start from evidence timelines; customer/IT admins start from readiness and setup; executive sponsors receive management summaries. Technical tenant concepts stay out of employee/HR flows unless the role is explicitly technical. | Codex/Product Owner | 26.5.2026
| **FairSpot brand attribution boundary** | AGPL remains the software license. The web and mobile apps keep visible Legal/About notices with copyright, license, source, and brand attribution. The FairSpot name and logo are treated as project brand assets: forks and commercial deployments may truthfully say they are based on FairSpot, but must not imply official FairSpot status or Robert Vejvoda endorsement without separate written agreement. | Codex/Product Owner | 25.5.2026
