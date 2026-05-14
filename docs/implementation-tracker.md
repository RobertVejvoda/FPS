# Implementation Tracker

This page tracks the delivery plan as slices. It is the first place to update when a new slice is created, assigned, implemented, reviewed, or merged.

The tracker complements the [Development Plan](./development-plan): the Development Plan explains scope and acceptance criteria; this page records progress, PRs, implementer attribution, and current ownership.

## Tracking Rules

- Every implementation slice should have a stable slice ID.
- Every slice should be linked to the issue or PR that carries its work.
- Future PRs should use `initiated-by:*` and `implemented-by:*` labels where possible so agent/user attribution is clear.
- Historical rows before attribution labels became routine use the GitHub PR author or known PR labels. Treat those as repository metadata, not a perfect authorship record.
- When a new slice is added, update this page in the same PR that adds or approves the slice spec.

## Current Status

| Area | Status | Notes |
| --- | --- | --- |
| Booking core | Done | B001-B010 implemented and merged. |
| Platform integration foundation | Done | ID001, BK011, P001, N001, A001, CFG001, API001, CI001 are merged. |
| Mobile foundation | Done | MOB001-MOB005 are merged for the current employee mobile flow. |
| Notification v1 completion | In progress | N002 is implemented. N003 email delivery is prepared for Claude handoff; preferences remain planned. |
| Audit v1 completion | Planned | Query API, retention/integrity, and GDPR erasure remain. |
| Production operations | In progress | OPS000 is prepared for Claude to refresh the hosting/deployment strategy around Dapr portability and cost before production hardening continues. |

## Slice Tracker

| Slice | Status | Issue | PR | Implementer signal | Merged / target | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Foundation / monorepo | Done | - | [#1](https://github.com/RobertVejvoda/FPS/pull/1), [#2](https://github.com/RobertVejvoda/FPS/pull/2), [#3](https://github.com/RobertVejvoda/FPS/pull/3), [#4](https://github.com/RobertVejvoda/FPS/pull/4), [#5](https://github.com/RobertVejvoda/FPS/pull/5) | PR author: RobertVejvoda | 2026-05-09 | Monorepo, .NET 10 baseline, tooling docs, naming and housekeeping. |
| Booking policy docs | Done | - | [#6](https://github.com/RobertVejvoda/FPS/pull/6), [#7](https://github.com/RobertVejvoda/FPS/pull/7), [#8](https://github.com/RobertVejvoda/FPS/pull/8) | PR author: RobertVejvoda | 2026-05-09 to 2026-05-10 | Allocation, executable Draw, and policy requirements. |
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
| ID001 Authenticated User Context | Done | - | [#26](https://github.com/RobertVejvoda/FPS/pull/26) | PR author: RobertVejvoda | 2026-05-10 | Current user abstraction and `GET /me`. |
| BK011 Booking Uses Auth Context | Done | - | [#27](https://github.com/RobertVejvoda/FPS/pull/27), [#33](https://github.com/RobertVejvoda/FPS/pull/33) | PR author: RobertVejvoda | 2026-05-10 to 2026-05-11 | Spec and implementation for authenticated Booking API scoping. |
| P001 Profile Vehicle Snapshot | Done | - | [#30](https://github.com/RobertVejvoda/FPS/pull/30), [#34](https://github.com/RobertVejvoda/FPS/pull/34) | PR author: RobertVejvoda | 2026-05-11 | Profile-owned eligibility/vehicle facts consumed by Booking. |
| N001 Booking Notification Consumer | Done | - | [#35](https://github.com/RobertVejvoda/FPS/pull/35), [#36](https://github.com/RobertVejvoda/FPS/pull/36) | PR author: RobertVejvoda | 2026-05-11 | Idempotent in-app notification records. |
| A001 Booking Audit Consumer | Done | - | [#37](https://github.com/RobertVejvoda/FPS/pull/37), [#39](https://github.com/RobertVejvoda/FPS/pull/39) | PR author: RobertVejvoda | 2026-05-11 | Append-only pseudonymised audit records. |
| Global architecture refresh | Done | - | [#40](https://github.com/RobertVejvoda/FPS/pull/40) | PR author: RobertVejvoda | 2026-05-11 | Refresh of global architecture overview. |
| CFG001 Parking Policy/Slot Source | Done | - | [#42](https://github.com/RobertVejvoda/FPS/pull/42) | PR author: RobertVejvoda | 2026-05-11 | Configuration-owned policy shape. |
| API001 OpenAPI Client Contract | Done | - | [#43](https://github.com/RobertVejvoda/FPS/pull/43), [#44](https://github.com/RobertVejvoda/FPS/pull/44) | PR author: RobertVejvoda | 2026-05-11 | OpenAPI and generated TypeScript client. |
| CI001 Build Status and CI Visibility | Done | - | [#46](https://github.com/RobertVejvoda/FPS/pull/46), [#47](https://github.com/RobertVejvoda/FPS/pull/47) | PR author: RobertVejvoda | 2026-05-11 | Badges, CI trigger expansion, manual/weekly runs, stale client check. |
| MOB001 React Native App Shell | Done | - | [#48](https://github.com/RobertVejvoda/FPS/pull/48), [#51](https://github.com/RobertVejvoda/FPS/pull/51) | PR author: RobertVejvoda | 2026-05-11 to 2026-05-12 | Expo app shell and development session gate. |
| MOB002 Mobile My Bookings | Done | - | [#49](https://github.com/RobertVejvoda/FPS/pull/49), [#55](https://github.com/RobertVejvoda/FPS/pull/55), [#76](https://github.com/RobertVejvoda/FPS/pull/76) | `implemented-by: claude` on #55; other PRs authored by RobertVejvoda | 2026-05-11 to 2026-05-13 | Read-only My Bookings screen and follow-up rendering refactor. |
| Agent routing and cost hygiene | Done | - | [#56](https://github.com/RobertVejvoda/FPS/pull/56), [#57](https://github.com/RobertVejvoda/FPS/pull/57), [#59](https://github.com/RobertVejvoda/FPS/pull/59), [#60](https://github.com/RobertVejvoda/FPS/pull/60), [#62](https://github.com/RobertVejvoda/FPS/pull/62)-[#72](https://github.com/RobertVejvoda/FPS/pull/72) | PR author: RobertVejvoda | 2026-05-13 | Copilot assignment path and Claude handoff-only routing. |
| Plan/status and stack docs | Done | - | [#73](https://github.com/RobertVejvoda/FPS/pull/73), [#77](https://github.com/RobertVejvoda/FPS/pull/77) | PR author: RobertVejvoda | 2026-05-13 to 2026-05-14 | Plan tracking, stack versions, collection-per-tenant decision. |
| MOB003 Mobile Real Login | Done | [#75](https://github.com/RobertVejvoda/FPS/issues/75) | [#78](https://github.com/RobertVejvoda/FPS/pull/78) | `implemented-by: claude` plus Codex repair commit | 2026-05-14 | Real OIDC Authorization Code + PKCE login in Expo mobile app. |
| MOB004 Mobile Booking Submission | Done | [#85](https://github.com/RobertVejvoda/FPS/issues/85) | [#87](https://github.com/RobertVejvoda/FPS/pull/87) | `implemented-by: claude` plus Codex review fix | 2026-05-14 | Employee request submission from mobile. |
| MOB005 Mobile Booking Actions | Done | [#91](https://github.com/RobertVejvoda/FPS/issues/91) | [#95](https://github.com/RobertVejvoda/FPS/pull/95) | PR author: RobertVejvoda; Codex reviewed | 2026-05-14 | Cancel and confirm-usage actions from mobile. |
| N002 Notification API And Stream | Done | [#88](https://github.com/RobertVejvoda/FPS/issues/88) | [#93](https://github.com/RobertVejvoda/FPS/pull/93), [#94](https://github.com/RobertVejvoda/FPS/pull/94) | `implemented-by: claude` plus Codex SSE casing fix | 2026-05-14 | Notification history API, unread counts, mark-read API, and SSE stream. |
| N003 Notification Email Delivery | Ready | [#103](https://github.com/RobertVejvoda/FPS/issues/103) | - | `initiated-by: codex`, `claude-ready` | Next Notification Claude slice | Email channel for v1 critical operational notifications, with a Dapr-ready provider boundary and local no-cost validation path. |
| A002 Audit Query And Erasure Support | Planned | - | - | Unassigned | After A001 foundation | Auditor query API and GDPR PII mapping erasure workflow. |
| OPS000 Hosting And Deployment Strategy Options | Ready | [#100](https://github.com/RobertVejvoda/FPS/issues/100) | - | `initiated-by: codex`, `claude-ready` | Next architecture Claude slice | Compare hosting/deployment options with Dapr as the portability boundary and cost as a first-class constraint. |
| OPS001 Local/Production Dapr Hardening | Planned | - | - | Unassigned | Before production deployment | Dapr components, tenant collection/index provisioning, secrets, and runbooks. |
| CFG002 Admin Policy/Slot Management | Planned | - | - | Unassigned | After CFG001 | Admin-facing management for policy, slots, and Draw schedules. |
| CUST001 Tenant Onboarding | Planned | - | - | Unassigned | After production provisioning model | Tenant creation and initial admin/user setup. |
| REPORT001 Reporting Read Models | Planned | - | - | Unassigned | After enough event volume/contracts stabilize | Utilization, fairness, penalty, and export read models. |
| BILL001 Billing Stub To Workflow | Planned | - | - | Unassigned | Later commercialisation phase | Subscription, invoice generation, and payment-provider integration. |
| WEB001 Web Employee Self-Service | Planned | - | - | Unassigned | After mobile proves core UX/API path | React web employee self-service. |
| WEB002 HR/Admin Dashboard | Planned | - | - | Unassigned | After reporting/configuration APIs mature | HR/facilities/admin operational UI. |

## Maintenance Items

| Item | Status | Source | Owner |
| --- | --- | --- | --- |
| GitHub Actions Node runtime refresh | Deferred | [#96](https://github.com/RobertVejvoda/FPS/issues/96), [#97](https://github.com/RobertVejvoda/FPS/pull/97) | Superseded until hosting/deployment strategy is agreed. |
| TestContainers-backed integration tests | Planned | Current validation skips Dapr/MongoDB integration tests | Unassigned |
| Collection-per-tenant implementation hardening | Planned | Decision recorded in [Versions and Decisions](./versions-and-decisions) | Unassigned |
