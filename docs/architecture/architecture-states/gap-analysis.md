# Gap Analysis

This page compares current-state evidence with the customer-ready target architecture.

| Gap ID | Area | Baseline Version | Target Version | Baseline State | Target State | Gap | Impact | Work Package | Owner | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GAP-001 | Customer | Current State v0.1 | Customer-Ready Target v0.1 | Customer service has in-memory repositories for some tenant state. | Customer tenant state needed for onboarding/readiness is durable. | Customer primary state is Dapr-backed (`DaprCustomerTenantRepository`, `DaprCustomerIdentityRepository`, `DaprCustomerParkingBootstrapRepository`); in-memory stores are restart-safe caches hydrated at startup. Notification, Audit, Reporting, Profile, and Configuration remain in-memory. | Notification/Audit/Reporting/Profile/Configuration in-memory stores are not customer-ready for production traffic. | DATA010 — Customer persistence evidence (done); remaining services persistence slices. | Claude/Codex | In Progress |
| GAP-002 | DataHub | Current State v0.1 | Customer-Ready Target v0.1 | DataHub project skeleton exists. | Event-fed read models support customer-facing reports/dashboards. | Projection catalog and first projections need implementation. | Reporting and management views may remain partial. | DataHub projection slices. | Claude/Codex | Open |
| GAP-003 | Hosted pilot security | Current State v0.1 | Customer-Ready Target v0.1 | Cloudflare/WAF and NAS deployment docs exist. | Public domain deployment is smoke-tested with WAF, auth, secrets, and no internal exposure. | End-to-end hosted validation remains needed. | Public demo risk until validated. | Hosted smoke and WAF validation. | Codex/Robert | Open |
| GAP-004 | Role-centered UI | Current State v0.1 | Customer-Ready Target v0.1 | Employee, HR, and admin views exist or are emerging. | Each role lands on a clear role-specific workspace with safe terminology and expected actions. | Role-specific polish and validation remain ongoing. | Customer evaluation can be confusing. | UX/customer evaluation slices. | Claude/Codex | Open |
| GAP-005 | Architecture validation | Current State v0.1 | Customer-Ready Target v0.1 | Architecture docs are distributed across existing layer pages. | TOGAF map, artifact status, and gap register are reviewed and baselined. | Formal architecture validation is not complete. | Harder to prove architecture readiness to client IT. | Architecture review pass. | Codex/Robert | Open |
| GAP-006 | Diagrams | Current State v0.1 | Customer-Ready Target v0.1 | Existing diagrams are source evidence and do not clearly show target status or gaps. | Authoritative target diagrams exist for capability/value stream, application cooperation, DataHub, deployment, workflow, trust boundary, and transition roadmap. | Diagram refresh is incomplete. | Architecture may be hard to validate visually. | Diagram refresh work package. | Robert/Codex | Open |
| GAP-007 | API/event contract evidence | Current State v0.1 | Customer-Ready Target v0.1 | Contract pages and implementation exist in several places. | Generated or source-of-truth API/event contracts are linked from Information Systems Architecture. | Generated contracts for Booking, Identity, Notification, and Profile are now linked from API Contracts page. HR, Customer, DataHub, Reporting remain placeholders. | Implementers and reviewers can miss cross-service contract drift. | API/event contract consolidation (API007). | Codex/Claude | In Progress |
| GAP-008 | Dapr hardening evidence | Current State v0.1 | Customer-Ready Target v0.1 | Dapr-first direction is documented. | Hosted profile proves component scopes, secret scopes, resiliency, mTLS/Sentry where supported, and outbox behavior. | Component scopes and secret references are evidenced. mTLS, resiliency policies, state encryption, and transactional outbox remain explicit gaps documented in Runtime Platform. | Hosted pilot security/operability risk. | Dapr hosted hardening slice (OPS014). | Codex/Claude | In Progress |

## Gap Status Values

| Status | Meaning |
| --- | --- |
| Open | Gap is known and unresolved. |
| Planned | Gap has an accepted resolution path. |
| In Progress | Work is actively closing the gap. |
| Closed | Evidence shows the gap is closed. |
| Accepted | Gap remains by explicit risk or scope decision. |
