# Implementation Tracker

This page tracks the delivery plan as slices. It is the first place to update when a new slice is created, assigned, implemented, reviewed, or merged.

The tracker complements the [Development Plan](./development-plan) and [Requirements Traceability](./requirements-traceability): the Development Plan explains scope and acceptance criteria, Requirements Traceability maps slices to business/NFR coverage, and this page records progress, PRs, implementer attribution, and current ownership.

## Tracking Rules

- Every implementation slice should have a stable slice ID.
- Every slice should be linked to the issue or PR that carries its work.
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
| Mobile product completion | Planned | Next employee slices are notifications, profile/vehicle display, draw/allocation detail, and production polish. |
| Web app | Planned | Web employee self-service starts after mobile completion; HR/admin dashboards can now build on Configuration and Reporting APIs. |
| Notification v1 completion | In progress | N002, N003, and N004 are implemented. Preferences and client consumption remain planned. |
| Audit v1 completion | In progress | A001 and A002 are implemented. Retention, integrity, and export evidence remain planned. |
| Production operations | In progress | OPS000 deployment profile baseline is merged. Next work is pluggable local/demo/client-owned deployment hardening, observability, and operational evidence. |
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

### Notification And Audit

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| N001 Booking Notification Consumer | Done | - | [#35](https://github.com/RobertVejvoda/FPS/pull/35), [#36](https://github.com/RobertVejvoda/FPS/pull/36) | PR author: RobertVejvoda | 2026-05-11 | Idempotent in-app notification records. |
| A001 Booking Audit Consumer | Done | - | [#37](https://github.com/RobertVejvoda/FPS/pull/37), [#39](https://github.com/RobertVejvoda/FPS/pull/39) | PR author: RobertVejvoda | 2026-05-11 | Append-only pseudonymised audit records. |
| N002 Notification API And Stream | Done | [#88](https://github.com/RobertVejvoda/FPS/issues/88) | [#93](https://github.com/RobertVejvoda/FPS/pull/93), [#94](https://github.com/RobertVejvoda/FPS/pull/94) | `implemented-by: claude` plus Codex SSE casing fix | 2026-05-14 | Notification history API, unread counts, mark-read API, and SSE stream. |
| N003 Notification Email Delivery | Done | [#103](https://github.com/RobertVejvoda/FPS/issues/103) | [#111](https://github.com/RobertVejvoda/FPS/pull/111) | `implemented-by: claude` | 2026-05-15 | Email channel for v1 critical operational notifications, with a Dapr-ready provider boundary and local no-cost validation path. |
| N004 Email Observability And Staging Validation | Done | [#122](https://github.com/RobertVejvoda/FPS/issues/122) | [#123](https://github.com/RobertVejvoda/FPS/pull/123) | `implemented-by: claude` | 2026-05-15 | Safe email failure logging, categories, and staging validation checklist. |
| N005 Notification Preferences | Planned | - | - | Unassigned | After mobile notification consumption | User notification preferences for optional channels and reminders; mandatory operational notifications stay non-disableable. |
| A002 Audit Query And Erasure Support | Done | [#105](https://github.com/RobertVejvoda/FPS/issues/105) | [#112](https://github.com/RobertVejvoda/FPS/pull/112) | `implemented-by: claude` | 2026-05-15 | Auditor query API and GDPR PII mapping erasure support; retention and integrity jobs remain out of scope. |
| A003 Audit Retention And Integrity | Planned | - | - | Unassigned | After A002 | Retention jobs, integrity verification, export evidence, and operational audit hardening. |

### Configuration, Customer, Reporting, And Billing

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| CFG001 Parking Policy/Slot Source | Done | - | [#42](https://github.com/RobertVejvoda/FPS/pull/42) | PR author: RobertVejvoda | 2026-05-11 | Configuration-owned policy shape. |
| CFG002 Admin Policy/Slot Management | Done | [#107](https://github.com/RobertVejvoda/FPS/issues/107) | [#125](https://github.com/RobertVejvoda/FPS/pull/125) | `implemented-by: claude` | 2026-05-15 | Admin-facing management for tenant policy, location overrides, and slot/capacity configuration. |
| CFG003 Configuration Publication And Audit | Planned | - | - | Unassigned | After CFG002 | Publish policy/slot changes safely to Booking consumers, preserve version history, and audit policy-sensitive changes. |
| CUST001 Tenant Onboarding | Planned | - | - | Unassigned | After production provisioning model | Tenant creation and initial admin/user setup. |
| REPORT001 Reporting Read Models | Done | [#109](https://github.com/RobertVejvoda/FPS/issues/109) | [#124](https://github.com/RobertVejvoda/FPS/pull/124) | `implemented-by: claude` | 2026-05-15 | Tenant-scoped operational reporting read models and summary/fairness APIs; exports and dashboards remain out of scope. |
| REPORT002 Reporting Dashboards And Exports | Planned | - | - | Unassigned | After REPORT001 | Dashboard-facing aggregates, CSV/PDF export path, and manager-safe report views. |
| BILL001 Billing Stub To Workflow | Planned | - | - | Unassigned | Later commercialisation phase | Subscription, invoice generation, and payment-provider integration. |

### Mobile

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| MOB001 React Native App Shell | Done | - | [#48](https://github.com/RobertVejvoda/FPS/pull/48), [#51](https://github.com/RobertVejvoda/FPS/pull/51) | PR author: RobertVejvoda | 2026-05-11 to 2026-05-12 | Expo app shell and development session gate. |
| MOB002 Mobile My Bookings | Done | - | [#49](https://github.com/RobertVejvoda/FPS/pull/49), [#55](https://github.com/RobertVejvoda/FPS/pull/55), [#76](https://github.com/RobertVejvoda/FPS/pull/76) | `implemented-by: claude` on #55; other PRs authored by RobertVejvoda | 2026-05-11 to 2026-05-13 | Read-only My Bookings screen and follow-up rendering refactor. |
| MOB003 Mobile Real Login | Done | [#75](https://github.com/RobertVejvoda/FPS/issues/75) | [#78](https://github.com/RobertVejvoda/FPS/pull/78) | `implemented-by: claude` plus Codex repair commit | 2026-05-14 | Real OIDC Authorization Code + PKCE login in Expo mobile app. |
| MOB004 Mobile Booking Submission | Done | [#85](https://github.com/RobertVejvoda/FPS/issues/85) | [#87](https://github.com/RobertVejvoda/FPS/pull/87) | `implemented-by: claude` plus Codex review fix | 2026-05-14 | Employee request submission from mobile. |
| MOB005 Mobile Booking Actions | Done | [#91](https://github.com/RobertVejvoda/FPS/issues/91) | [#95](https://github.com/RobertVejvoda/FPS/pull/95) | PR author: RobertVejvoda; Codex reviewed | 2026-05-14 | Cancel and confirm-usage actions from mobile. |
| MOB006 Mobile Notifications | Planned | - | - | Unassigned | After current PR review queue | Notification list, unread count, mark-read action, and SSE or polling fallback using N002 APIs. |
| MOB007 Mobile Profile And Vehicle Details | Planned | - | - | Unassigned | After MOB006 or when Profile editing rules are clear | Employee-visible profile, vehicle, company-car, and accessibility facts; editing only if business rules allow it. |
| MOB008 Mobile Draw Status And Allocation Detail | Planned | - | - | Unassigned | After MOB007 | Employee-safe draw/allocation visibility without exposing hidden lottery internals. |
| MOB009 Mobile Production Polish | Planned | - | - | Unassigned | Before mobile pilot | Session expiry, refresh recovery, environment config, error/empty/loading states, accessibility, and production QA. |

### Web

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| WEB001 Web Employee Self-Service | Planned | - | - | Unassigned | After mobile proves core UX/API path | React web employee self-service. |
| WEB002 HR/Admin Dashboard | Planned | - | - | Unassigned | After reporting/configuration APIs mature | HR/facilities/admin operational UI. |
| WEB003 Tenant Admin Console | Planned | - | - | Unassigned | After CUST001/CFG002 | Tenant users, roles, locations, policies, and slot administration. |
| WEB004 Reporting Views | Planned | - | - | Unassigned | After REPORT001/REPORT002 | Parking summary, fairness metrics, utilization views, and exports. |

### Operations And Cloud

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| OPS000 Hosting and Deployment Strategy Options | Done | [#100](https://github.com/RobertVejvoda/FPS/issues/100) | [#102](https://github.com/RobertVejvoda/FPS/pull/102) | `implemented-by: claude` | 2026-05-15 | Deployment profile baseline merged; local, demo, and client-owned production profiles replace the earlier single-provider production assumption. |
| OPS001 Pluggable Dapr Component Baseline | Planned | - | - | Unassigned | Next production slice | Define local/demo/client-owned component profiles for pub/sub, state, secrets, bindings, identity, and observability. |
| OPS002 Demo Environment Baseline | Planned | - | - | Unassigned | After OPS001 | Stand up or document a low-cost demo path that proves the product without assuming the final client production environment. |
| OPS003 Client-Owned Production Integration | Planned | - | - | Unassigned | After OPS001/OPS002 | Document and implement the handoff model for client-owned production, including deployment assumptions, Dapr component replacement, and operational responsibilities. |
| OPS004 Observability And Performance Evidence | Planned | - | - | Unassigned | Before client pilot | Expose usage, performance, logs, metrics, and traces so Prometheus/Grafana locally can be replaced by Dynatrace, Azure Monitor, OpenTelemetry Collector, or client tooling. |
| DOCS001 Client Evaluation Pack | Planned | - | - | Unassigned | After demo plan stabilizes | Prepare shareable client material: product one-pager, role demo script, architecture overview, deployment/operations summary, security/GDPR summary, cost assumptions, and FAQ. |

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

| Item | Status | Source | Owner |
| --- | --- | --- | --- |
| GitHub Actions Node runtime refresh | Deferred | [#96](https://github.com/RobertVejvoda/FPS/issues/96), [#97](https://github.com/RobertVejvoda/FPS/pull/97) | Superseded until hosting/deployment strategy is agreed. |
| TestContainers-backed integration tests | Planned | Current validation skips Dapr/MongoDB integration tests | Unassigned |
| Collection-per-tenant implementation hardening | Planned | Decision recorded in [Versions and Decisions](./versions-and-decisions) | Unassigned |
| GitHub project / Kanban links | Planned | Docs should link to live GitHub issues/PRs rather than duplicate board state | Unassigned |

## Future Product Extension Notes

| Idea | Status | Note |
| --- | --- | --- |
| Seat/desk booking | Future | Once the parking product reaches a stable hosted v1, evaluate whether the same platform approach can support company seat or desk booking. Reuse tenant-scoped resources, policy-driven allocation, notifications, audit, reporting, and admin configuration where practical, but define separate seat-specific rules before implementation. |
