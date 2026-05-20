# Implementation Tracker

This page tracks the delivery plan as slices. It is the first place to update when a new slice is created, assigned, implemented, reviewed, or merged.

The tracker complements the [Roadmap](./roadmap), [Delivery Board](./delivery-board), [Development Plan](./development-plan), and [Requirements Traceability](./requirements-traceability): the Roadmap explains phases and ordering, the Delivery Board explains milestone/priority/status rules, the Development Plan explains scope and acceptance criteria, Requirements Traceability maps slices to business/NFR coverage, and this page records progress, PRs, implementer attribution, and current ownership.

Phase visibility is tracked in the [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2). The board gives a high-level view of Backlog, Ready, In progress, In review, and Done work; the [Roadmap](./roadmap) explains what each phase means; this page remains the detailed source of truth for slice evidence and PR links.

## Tracking Rules

- Every implementation slice should have a stable slice ID.
- Every implementation slice should have a GitHub issue and a card in the [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2). Historical slices that predate issue-first tracking may remain PR-only in this page, but new work should not.
- Every implementation PR should link back to the issue that carries its work.
- Project board `Status` is the operational state. `Ready` means an issue has enough context for Codex, Claude, or Copilot to act; `Backlog` means the slice exists but still needs preparation or a predecessor.
- Every implementation slice should name the business and non-functional requirements it implements or supports.
- Future PRs should use `initiated-by:*` and `implemented-by:*` labels where possible so agent/user attribution is clear.
- Historical rows before attribution labels became routine use the GitHub PR author or known PR labels. Treat those as repository metadata, not a perfect authorship record.
- When a new slice is added, update this page in the same PR that adds or approves the slice spec.
- When a slice changes requirement coverage, update [Requirements Traceability](./requirements-traceability) in the same PR or in the approving follow-up PR.

## Current Status

| Area | Status | Notes |
| --- | --- | --- |
| Booking core | Done | B001-B010 implemented and merged. |
| Platform integration foundation | Done | ID001, BK011, P001, N001, A001, CFG001, API001, CI001 are merged. |
| Mobile foundation | Done | MOB001-MOB005 are merged for the current employee mobile flow. |
| Mobile product completion | Done | MOB006-MOB009 are merged for notifications, profile/vehicle display, employee-safe allocation detail, and demo/pilot polish. |
| Web app | Backlog | Web employee self-service starts after mobile completion; HR/admin dashboards can now build on Configuration and Reporting APIs. |
| Notification v1 completion | Done | N002-N005 are implemented, including API/stream, email channel, observability, and user preferences. |
| Audit v1 completion | In progress | A001 and A002 are implemented. Retention, integrity, and export evidence remain planned. |
| Production operations | In progress | OPS000-OPS002 and OPS006A-OPS006D are merged. Next work is coordinated local harness/AppHost, client-owned deployment hardening, observability, and operational evidence. |
| Configuration management | In progress | CFG001 and CFG002 are implemented. Publication, version history, and audit integration remain planned. |
| Reporting foundation | In progress | REPORT001 read models are implemented. Dashboards, exports, and web views remain planned. |

## Slice Tracker

### Foundation And Planning

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Foundation / monorepo | Done | - | [#1](https://github.com/RobertVejvoda/FPS/pull/1), [#2](https://github.com/RobertVejvoda/FPS/pull/2), [#3](https://github.com/RobertVejvoda/FPS/pull/3), [#4](https://github.com/RobertVejvoda/FPS/pull/4), [#5](https://github.com/RobertVejvoda/FPS/pull/5) | PR author: RobertVejvoda | 2026-05-09 | Monorepo, .NET 10 baseline, tooling docs, naming and housekeeping. |
| Booking policy docs | Done | - | [#6](https://github.com/RobertVejvoda/FPS/pull/6), [#7](https://github.com/RobertVejvoda/FPS/pull/7), [#8](https://github.com/RobertVejvoda/FPS/pull/8) | PR author: RobertVejvoda | 2026-05-09 to 2026-05-10 | Allocation, executable Draw, and policy requirements. |
| Global architecture refresh | Done | - | [#40](https://github.com/RobertVejvoda/FPS/pull/40) | PR author: RobertVejvoda | 2026-05-11 | Refresh of global architecture overview. |
| API001 OpenAPI Client Contract | Done | - | [#43](https://github.com/RobertVejvoda/FPS/pull/43), [#44](https://github.com/RobertVejvoda/FPS/pull/44) | PR author: RobertVejvoda | 2026-05-11 | OpenAPI and generated TypeScript client. |
| CI001 Build Status and CI Visibility | Done | - | [#46](https://github.com/RobertVejvoda/FPS/pull/46), [#47](https://github.com/RobertVejvoda/FPS/pull/47) | PR author: RobertVejvoda | 2026-05-11 | Badges, CI trigger expansion, manual/weekly runs, stale client check. |
| Agent routing and cost hygiene | Done | - | [#56](https://github.com/RobertVejvoda/FPS/pull/56), [#57](https://github.com/RobertVejvoda/FPS/pull/57), [#59](https://github.com/RobertVejvoda/FPS/pull/59), [#60](https://github.com/RobertVejvoda/FPS/pull/60), [#62](https://github.com/RobertVejvoda/FPS/pull/62)-[#72](https://github.com/RobertVejvoda/FPS/pull/72) | PR author: RobertVejvoda | 2026-05-13 | Copilot assignment path and Claude handoff-only routing. |
| Plan/status and stack docs | Done | - | [#73](https://github.com/RobertVejvoda/FPS/pull/73), [#77](https://github.com/RobertVejvoda/FPS/pull/77) | PR author: RobertVejvoda | 2026-05-13 to 2026-05-14 | Plan tracking, stack versions, collection-per-tenant decision. |

### Booking

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| B001 Submit Future Booking Request | Done | - | [#9](https://github.com/RobertVejvoda/FPS/pull/9) | PR author: RobertVejvoda | 2026-05-10 | First Booking command slice. |
| B002 Submit Same-Day Booking Request | Done | - | [#15](https://github.com/RobertVejvoda/FPS/pull/15) | PR author: RobertVejvoda | 2026-05-10 | Same-day immediate allocation path. |
| B003 Cancel Pending Request | Done | - | [#10](https://github.com/RobertVejvoda/FPS/pull/10) | PR author: RobertVejvoda | 2026-05-10 | Pending cancellation. |
| B004 Run Scheduled Draw | Done | - | [#13](https://github.com/RobertVejvoda/FPS/pull/13) | PR author: RobertVejvoda | 2026-05-10 | Draw execution. |
| B005 Cancel Allocated Reservation And Reallocate | Done | - | [#14](https://github.com/RobertVejvoda/FPS/pull/14) | PR author: RobertVejvoda | 2026-05-10 | Allocated cancellation and reallocation. |
| B006 Confirm Usage | Done | - | [#16](https://github.com/RobertVejvoda/FPS/pull/16) | PR author: RobertVejvoda | 2026-05-10 | Usage confirmation. |
| B007 Mark No-Show | Done | - | [#17](https://github.com/RobertVejvoda/FPS/pull/17) | PR author: RobertVejvoda | 2026-05-10 | No-show evaluation. |
| B008 View My Bookings | Done | - | [#11](https://github.com/RobertVejvoda/FPS/pull/11) | PR author: RobertVejvoda | 2026-05-10 | Backend query for employee booking history. |
| B009 View Draw Status | Done | - | [#18](https://github.com/RobertVejvoda/FPS/pull/18) | PR author: RobertVejvoda | 2026-05-10 | Draw status query. |
| B010 Manual Correction | Done | - | [#19](https://github.com/RobertVejvoda/FPS/pull/19) | PR author: RobertVejvoda | 2026-05-10 | Admin/manual correction path. |
| Booking hardening and cleanup | Done | - | [#20](https://github.com/RobertVejvoda/FPS/pull/20), [#21](https://github.com/RobertVejvoda/FPS/pull/21), [#22](https://github.com/RobertVejvoda/FPS/pull/22), [#24](https://github.com/RobertVejvoda/FPS/pull/24) | PR author: RobertVejvoda | 2026-05-10 | Dead-code cleanup, handoff docs, reconciled status, package hardening. |
| BK011 Booking Uses Auth Context | Done | - | [#27](https://github.com/RobertVejvoda/FPS/pull/27), [#33](https://github.com/RobertVejvoda/FPS/pull/33) | PR author: RobertVejvoda | 2026-05-10 to 2026-05-11 | Spec and implementation for authenticated Booking API scoping. |

### Identity And Profile

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| ID001 Authenticated User Context | Done | - | [#26](https://github.com/RobertVejvoda/FPS/pull/26) | PR author: RobertVejvoda | 2026-05-10 | Current user abstraction and `GET /me`. |
| P001 Profile Vehicle Snapshot | Done | - | [#30](https://github.com/RobertVejvoda/FPS/pull/30), [#34](https://github.com/RobertVejvoda/FPS/pull/34) | PR author: RobertVejvoda | 2026-05-11 | Profile-owned eligibility/vehicle facts consumed by Booking. |
| P002 Profile Mapping And Minimal Facts | Planned | [#146](https://github.com/RobertVejvoda/FPS/issues/146) | - | Unassigned | After `CUST002` | Implement SSO-derived profile mapping and minimal policy facts with tenant/user mapping, validation summary, and Confidential data controls. |
| ID002 User Provisioning Integration | Planned | [#143](https://github.com/RobertVejvoda/FPS/issues/143) | - | Unassigned | After `CUST002` | Map IdP subjects, claims/groups, role assignment, local-account fallback, and deactivation behavior. |

### Notification And Audit

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| N001 Booking Notification Consumer | Done | - | [#35](https://github.com/RobertVejvoda/FPS/pull/35), [#36](https://github.com/RobertVejvoda/FPS/pull/36) | PR author: RobertVejvoda | 2026-05-11 | Idempotent in-app notification records. |
| A001 Booking Audit Consumer | Done | - | [#37](https://github.com/RobertVejvoda/FPS/pull/37), [#39](https://github.com/RobertVejvoda/FPS/pull/39) | PR author: RobertVejvoda | 2026-05-11 | Append-only pseudonymised audit records. |
| N002 Notification API And Stream | Done | [#88](https://github.com/RobertVejvoda/FPS/issues/88) | [#93](https://github.com/RobertVejvoda/FPS/pull/93), [#94](https://github.com/RobertVejvoda/FPS/pull/94) | `implemented-by: claude` plus Codex SSE casing fix | 2026-05-14 | Notification history API, unread counts, mark-read API, and SSE stream. |
| N003 Notification Email Delivery | Done | [#103](https://github.com/RobertVejvoda/FPS/issues/103) | [#111](https://github.com/RobertVejvoda/FPS/pull/111) | `implemented-by: claude` | 2026-05-15 | Email channel for v1 critical operational notifications, with a Dapr-ready provider boundary and local no-cost validation path. |
| N004 Email Observability And Staging Validation | Done | [#122](https://github.com/RobertVejvoda/FPS/issues/122) | [#123](https://github.com/RobertVejvoda/FPS/pull/123) | `implemented-by: claude` | 2026-05-15 | Safe email failure logging, categories, and staging validation checklist. |
| N005 Notification Preferences | Done | [#144](https://github.com/RobertVejvoda/FPS/issues/144) | [#177](https://github.com/RobertVejvoda/FPS/pull/177) | `implemented-by: claude` | 2026-05-20 | User notification preferences for optional channels and reminders; mandatory operational notifications stay non-disableable. |
| A002 Audit Query And Erasure Support | Done | [#105](https://github.com/RobertVejvoda/FPS/issues/105) | [#112](https://github.com/RobertVejvoda/FPS/pull/112) | `implemented-by: claude` | 2026-05-15 | Auditor query API and GDPR PII mapping erasure support; retention and integrity jobs remain out of scope. |
| A003 Audit Retention And Integrity | Planned | [#145](https://github.com/RobertVejvoda/FPS/issues/145) | - | Unassigned | After A002 | Retention jobs, integrity verification, export evidence, and operational audit hardening. |

### Configuration, Customer, Reporting, And Billing

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| CFG001 Parking Policy/Slot Source | Done | - | [#42](https://github.com/RobertVejvoda/FPS/pull/42) | PR author: RobertVejvoda | 2026-05-11 | Configuration-owned policy shape. |
| CFG002 Admin Policy/Slot Management | Done | [#107](https://github.com/RobertVejvoda/FPS/issues/107) | [#125](https://github.com/RobertVejvoda/FPS/pull/125) | `implemented-by: claude` | 2026-05-15 | Admin-facing management for tenant policy, location overrides, and slot/capacity configuration. |
| CFG003 Configuration Publication And Audit | Planned | [#149](https://github.com/RobertVejvoda/FPS/issues/149) | - | Unassigned | After CFG002 | Publish policy/slot changes safely to Booking consumers, preserve version history, and audit policy-sensitive changes. |
| CUST001 Tenant Onboarding | Planned | [#148](https://github.com/RobertVejvoda/FPS/issues/148) | - | Unassigned | After production provisioning model | Tenant creation and initial admin/user setup. |
| CUST002 SSO-First Customer Integration Contract | Done | [#141](https://github.com/RobertVejvoda/FPS/issues/141) | direct doc update | Codex/spec | 2026-05-17 | Defines SSO/OIDC issuer and tenant mapping, minimal employee/profile data, local-account credential handling, import constraints, source-of-truth rules, audit/GDPR requirements, and downstream acceptance criteria for `P002`, `ID002`, `OPS005`, and `CUST001`. |
| REPORT001 Reporting Read Models | Done | [#109](https://github.com/RobertVejvoda/FPS/issues/109) | [#124](https://github.com/RobertVejvoda/FPS/pull/124) | `implemented-by: claude` | 2026-05-15 | Tenant-scoped operational reporting read models and summary/fairness APIs; exports and dashboards remain out of scope. |
| REPORT002 Reporting Dashboards And Exports | Planned | [#147](https://github.com/RobertVejvoda/FPS/issues/147) | - | Unassigned | After REPORT001 | Dashboard-facing aggregates, CSV/PDF export path, and manager-safe report views. |
| BILL000 Commercialisation Impact Review | Planned | [#150](https://github.com/RobertVejvoda/FPS/issues/150) | - | Unassigned | Before Billing implementation | Decide free/open core boundaries, paid add-on candidates, support subscription shape, and future dual-license posture before implementing product Billing behavior. |
| BILL001 Billing Stub To Workflow | Planned | [#153](https://github.com/RobertVejvoda/FPS/issues/153) | - | Unassigned | After BILL000 | Subscription, invoice generation, and payment-provider integration only after the commercial model is approved. |

### Mobile

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| MOB001 React Native App Shell | Done | - | [#48](https://github.com/RobertVejvoda/FPS/pull/48), [#51](https://github.com/RobertVejvoda/FPS/pull/51) | PR author: RobertVejvoda | 2026-05-11 to 2026-05-12 | Expo app shell and development session gate. |
| MOB002 Mobile My Bookings | Done | - | [#49](https://github.com/RobertVejvoda/FPS/pull/49), [#55](https://github.com/RobertVejvoda/FPS/pull/55), [#76](https://github.com/RobertVejvoda/FPS/pull/76) | `implemented-by: claude` on #55; other PRs authored by RobertVejvoda | 2026-05-11 to 2026-05-13 | Read-only My Bookings screen and follow-up rendering refactor. |
| MOB003 Mobile Real Login | Done | [#75](https://github.com/RobertVejvoda/FPS/issues/75) | [#78](https://github.com/RobertVejvoda/FPS/pull/78) | `implemented-by: claude` plus Codex repair commit | 2026-05-14 | Real OIDC Authorization Code + PKCE login in Expo mobile app. |
| MOB004 Mobile Booking Submission | Done | [#85](https://github.com/RobertVejvoda/FPS/issues/85) | [#87](https://github.com/RobertVejvoda/FPS/pull/87) | `implemented-by: claude` plus Codex review fix | 2026-05-14 | Employee request submission from mobile. |
| MOB005 Mobile Booking Actions | Done | [#91](https://github.com/RobertVejvoda/FPS/issues/91) | [#95](https://github.com/RobertVejvoda/FPS/pull/95) | PR author: RobertVejvoda; Codex reviewed | 2026-05-14 | Cancel and confirm-usage actions from mobile. |
| MOB006 Mobile Notifications | Done | [#137](https://github.com/RobertVejvoda/FPS/issues/137) | [#166](https://github.com/RobertVejvoda/FPS/pull/166) | `implemented-by: claude` plus Codex filter-refresh fix | 2026-05-17 | Notification list, unread count, mark-read action, and polling fallback using N002 APIs. |
| MOB007 Mobile Profile And Vehicle Details | Done | [#135](https://github.com/RobertVejvoda/FPS/issues/135) | direct implementation | Codex | 2026-05-17 | Mobile profile tab now consumes `GET /profile/snapshot` and shows employee-safe profile status, parking eligibility, company-car/accessibility/reserved-space facts, snapshot version, and active vehicles. Editing remains out of scope. |
| MOB008 Mobile Draw Status And Allocation Detail | Done | [#136](https://github.com/RobertVejvoda/FPS/issues/136) | [#171](https://github.com/RobertVejvoda/FPS/pull/171) | `implemented-by: claude` | 2026-05-20 | Employee-safe draw/allocation visibility without exposing hidden lottery internals. |
| MOB009 Mobile Production Polish | Done | [#138](https://github.com/RobertVejvoda/FPS/issues/138) | [#176](https://github.com/RobertVejvoda/FPS/pull/176) | `implemented-by: claude` | 2026-05-20 | Session expiry, refresh recovery, environment config, error/empty/loading states, accessibility, and production QA. |

### Web

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| WEB001 Web Employee Self-Service | Planned | [#151](https://github.com/RobertVejvoda/FPS/issues/151) | - | Unassigned | After mobile proves core UX/API path | React web employee self-service. |
| WEB002 HR/Admin Dashboard | Planned | [#154](https://github.com/RobertVejvoda/FPS/issues/154) | - | Unassigned | After reporting/configuration APIs mature | HR/facilities/admin operational UI. |
| WEB003 Tenant Admin Console | Planned | [#152](https://github.com/RobertVejvoda/FPS/issues/152) | - | Unassigned | After CUST001/CFG002 | Tenant users, roles, locations, policies, and slot administration. |
| WEB004 Reporting Views | Planned | [#155](https://github.com/RobertVejvoda/FPS/issues/155) | - | Unassigned | After REPORT001/REPORT002 | Parking summary, fairness metrics, utilization views, and exports. |

### Operations And Cloud

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| OPS000 Hosting and Deployment Strategy Options | Done | [#100](https://github.com/RobertVejvoda/FPS/issues/100) | [#102](https://github.com/RobertVejvoda/FPS/pull/102) | `implemented-by: claude` | 2026-05-15 | Deployment profile baseline merged; local, demo, and client-owned production profiles replace the earlier single-provider production assumption. |
| OPS001 Pluggable Dapr Component Baseline | Done | [#139](https://github.com/RobertVejvoda/FPS/issues/139) | [#167](https://github.com/RobertVejvoda/FPS/pull/167) | `implemented-by: claude` plus Codex scope/doc fixes | 2026-05-17 | Local/demo/client-owned Dapr component profiles, pub/sub name alignment, state/secret/store naming, and local setup docs. |
| OPS002 Demo Environment Baseline | Done | [#140](https://github.com/RobertVejvoda/FPS/issues/140) | direct doc update | Codex/spec | 2026-05-17 | Defines low-cost hosted demo scope, runtime components, Dapr boundaries, synthetic data handling, smoke checks, reset/teardown path, cost evidence, and handoff to `DOCS001`, `OPS003`, `OPS004`, and `OPS005`. |
| OPS006A Local Demo Seed And Dev Token | Done | [#173](https://github.com/RobertVejvoda/FPS/issues/173) | [#175](https://github.com/RobertVejvoda/FPS/pull/175) | `implemented-by: claude` | 2026-05-20 | Local Keycloak realm import, demo users, password setup, dev issuer env, and bearer-token helper. |
| OPS006B Local Mobile API Gateway | Done | [#180](https://github.com/RobertVejvoda/FPS/issues/180) | [#181](https://github.com/RobertVejvoda/FPS/pull/181) | `implemented-by: claude` | 2026-05-20 | Envoy gateway routes mobile employee endpoints under one local API base URL. |
| OPS006C Local Dapr Sidecars For FPS Services | Done | [#182](https://github.com/RobertVejvoda/FPS/issues/182) | [#184](https://github.com/RobertVejvoda/FPS/pull/184) | `implemented-by: claude` | 2026-05-20 | Dapr multi-app sidecar run path for local FPS services. |
| OPS006D Local Demo Seed And Reset | Done | [#183](https://github.com/RobertVejvoda/FPS/issues/183) | [#185](https://github.com/RobertVejvoda/FPS/pull/185) | `implemented-by: claude` | 2026-05-20 | Synthetic profile/configuration demo seed and repeatable reset path for local smoke testing. |
| OPS006 Local Test Harness | Planned | [#172](https://github.com/RobertVejvoda/FPS/issues/172) | - | Unassigned | Next operations slice | Coordinated AppHost or equivalent harness that starts dependencies/services together and exposes health/log evidence for local mobile/API testing. |
| OPS003 Client-Owned Production Integration | Planned | [#156](https://github.com/RobertVejvoda/FPS/issues/156) | - | Unassigned | After OPS001/OPS002 | Document and implement the handoff model for client-owned production, including deployment assumptions, Dapr component replacement, and operational responsibilities. |
| OPS004 Observability And Performance Evidence | Planned | [#158](https://github.com/RobertVejvoda/FPS/issues/158) | - | Unassigned | Before client pilot | Expose usage, performance, logs, metrics, and traces so Prometheus/Grafana locally can be replaced by Dynatrace, Azure Monitor, OpenTelemetry Collector, or client tooling. |
| OPS005 Integration Secrets And Observability | Planned | [#157](https://github.com/RobertVejvoda/FPS/issues/157) | - | Unassigned | After `CUST002` | Define secret handling, audit, logs, metrics, retries, and error evidence for customer-system integration actors. |
| DOCS001 Client Evaluation Pack | Done | [#142](https://github.com/RobertVejvoda/FPS/issues/142) | direct doc update | Codex/spec | 2026-05-17 | Adds a shareable client evaluation pack with product summary, evaluator paths, role demo script, architecture summary, deployment/operations summary, security/GDPR summary, cost assumptions, FAQ, and Demo v0 evidence. |

## Slice Order Rationale

| Order | Slice | Goal | Why This Order | Links |
| --- | --- | --- | --- | --- |
| 1 | Status and traceability cleanup | Make docs truthful after recent merges. | Stale tracker and traceability pages create wrong handoffs and wrong client expectations. | [Tracker](./implementation-tracker), [Traceability](./requirements-traceability) |
| 2 | Business story cleanup | Explain value, roles, and parking-first scope. | Business readers need the product story before architecture detail. | [Business Layer](./business-layer), [Demo and Evaluation](./demo-and-evaluation) |
| 3 | ArchiMate view hierarchy | Prepare business/application/technology/security/production view structure. | Architects need a stable hierarchy before richer diagrams are added. | [Architecture Views](./architecture-views) |
| 4 | Demo and client evaluation plan | Define how each role can try FPS. | Demo readiness exposes missing product and operational slices. | [Demo and Evaluation](./demo-and-evaluation) |
| 5 | Pluggable operations plan | Define local, demo, and client-owned production profiles. | Dapr only helps if each component boundary is explicit and replaceable. | [Production](./production), [Hosting Strategy](./production/hosting-deployment-strategy) |
| 6 | Observability and performance evidence | Make usage, metrics, logs, and traces consumable by client tooling. | Client production will run outside our environment, so evidence must be portable. | [Monitoring](./production/monitoring), `OPS004` |
| 7 | Client evaluation pack | Create shareable materials for sponsors, architects, security, and operators. | Materials should be created after the demo and architecture story stabilize. | `DOCS001` |

## Maintenance Items

These are tracker maintenance tasks, not ordered delivery slices.

| Item | Status | Source | Owner |
| --- | --- | --- | --- |
| GitHub Actions Node runtime refresh | Deferred | [#96](https://github.com/RobertVejvoda/FPS/issues/96), [#97](https://github.com/RobertVejvoda/FPS/pull/97) | Superseded until hosting/deployment strategy is agreed. |
| TestContainers-backed integration tests | Planned | Current validation skips Dapr/MongoDB integration tests | Unassigned |
| Collection-per-tenant implementation hardening | Planned | Decision recorded in [Versions and Decisions](./versions-and-decisions) | Unassigned |
| GitHub project / Kanban links | Done | [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2) links phase status and slice issue cards to tracker details | Codex |

## Future Product Extension Notes

| Idea | Status | Note |
| --- | --- | --- |
| Seat/desk booking | Future | Once the parking product reaches a stable hosted v1, evaluate whether the same platform approach can support company seat or desk booking. Reuse tenant-scoped resources, policy-driven allocation, notifications, audit, reporting, and admin configuration where practical, but define separate seat-specific rules before implementation. |
