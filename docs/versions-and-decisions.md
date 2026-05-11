---
title: Versions and decisions
---

## About

**FPS** stands for Fair Parking System, designed for companies with more employees owning cars than available parking slots. Currently, employees must email HR to request parking for the next day, operating on a "first-come, first-served" basis, which can be discouraging. The new system aims to be fair by evenly distributing parking slots among interested employees.

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



## Document reviews


| Name | Role | Date
| ---- | ---- | ----



## Document validations


| Name | Role | Date
| ---- | ---- | ----


## Decision Log


| Purpose | Rational | By | Date
| ------- | -------- | -- | ----
| Public cloud hosting | Use public cloud hosting on MS Azure. It's secured and works well SignalR Service used for mobile app | Architect | 1.10.2024
| ASP.NET Core, .NET 9 | Modern powerful language with great community support, cross platform | Architect | 1.10.2024
| React | Web UI for both web and mobile progressive apps | Architect | 1.11.2024
| Dapr | Dapr helps with consistency, bindings and abstractions, important software component build block. | Architect | 1.10.2024
| .NET Aspire | Use .NET Aspire to orchestrate microservices with improved view on logs and traces | Architect | 1.10.2024
| ~~.NET MAUI~~ *(reversed 9.5.2026)* | Replaced by React Native + Expo. | Architect | 13.10.2024
| Development tools | Use Visual Studio Code for development. It does not require any licences and is effective with full language support | Architect | 13.10.2024
| ~~Xcode~~ *(reversed 9.5.2026)* | No longer required — Expo managed workflow removes need for native build tooling. | Architect | 13.10.2024
| **React Native + Expo** | Mobile platform. Expo managed workflow, no native build tooling required. Larger community, better AI support than MAUI. TypeScript consistent with React web frontend. OTA updates without App Store review. | Architect | 9.5.2026
| **No PostgreSQL, no EF Core** | Persistence via Dapr state store (write side) and MongoDB driver (read side). PostgreSQL removed from stack. | Architect | 9.5.2026
| **CQRS persistence split** | Commands use Dapr state store backed by MongoDB. Queries use MongoDB driver directly for aggregation pipelines and projections. | Architect | 9.5.2026
| **Database-per-tenant (MongoDB)** | Each tenant isolated in its own MongoDB database `fps_{tenant_id}`. Resolved from request context. Equivalent to schema-per-tenant in relational DBs. | Architect | 9.5.2026
| **Dapr 1.14+** | Minimum Dapr version updated from 1.4.0. Dapr Workflows require 1.10+. | Architect | 9.5.2026
| **.NET 10** | Upgraded from .NET 9 (LTS). Released Nov 2025. | Architect | 9.5.2026
| **Draw volume cap: 500** | Maximum 500 booking requests per tenant per Draw. Single sequential Dapr Workflow — no fan-out needed. Booking service enforces the cap at submission time. | Architect | 9.5.2026
| **Same-day booking supported** | Employees can request a slot for the current day outside the Draw (e.g. during commute). System allocates immediately if a slot is available. Same 500-cap per date applies. Consistent with process.md flow already documented. | Architect | 9.5.2026
| **Company car: auto-scheduled, guaranteed allocation** | Company car employees use the same auto-scheduler as regular employees. In the Draw, their requests are allocated first (Tier 1) before the weighted lottery runs for remaining slots. No penalty ever applies. `HasCompanyCar` flag on `UserProfile` drives this. No separate admin workflow needed. | Architect | 9.5.2026
| **Tier 2 lottery weight** | Tier 2 uses `1 / (1 + RecentAllocationCount + ActivePenaltyScore)`, calculated from a draw-time snapshot of user metrics. `RecentAllocationCount` counts successful non-company-car allocations in the tenant lookback window, including same-day allocations. The lookback window is tenant-configurable and defaults to `10` days. `ActivePenaltyScore` covers active penalties from late cancellations, no-shows, policy violations, or manual HR adjustments. Rejected requests are not in the denominator; any future reward for repeated rejected requests must be a separate positive factor. | Architect | 9.5.2026
| **Company-car capacity overflow** | If company-car requests exceed available matching capacity, FPS rejects the overflow requests for now. This keeps the first implementation simple and treats the case as a tenant configuration issue that should be rare. | Architect | 9.5.2026
| **Cancellation reallocation** | When an allocated reservation is cancelled and another eligible requestor exists, FPS automatically reallocates the released space instead of only notifying a waitlist. The action must be auditable and notification events are sent to affected requestors. | Architect | 9.5.2026
| **Executable Draw rules** | Draw implementation follows `docs/business-layer/allocation-rules.md`. The rules define duplicate detection, allocation precedence, seeded lottery reproducibility, slot matching, same-day metric updates, automatic cancellation reallocation, default penalties, audit payload, and idempotency expectations. | Architect | 9.5.2026
| **Booking request lifecycle** | Request lifecycle follows `docs/business-layer/booking-request-lifecycle.md`. Late cancellation starts after a slot has been allocated. Cancellation before allocation does not create a penalty. Usage confirmation, no-show handling, terminal statuses, employee-visible reasons, and audit requirements are defined there. | Architect | 9.5.2026
| **Parking policy configuration** | Parking policy uses tenant-level defaults with optional per-location overrides. Location overrides win for that location; missing fields fall back to tenant defaults. Required fields, defaults, slot capability settings, penalty policy, usage confirmation, and policy publication behavior are defined in `docs/business-layer/parking-policy-configuration.md`. | Architect | 9.5.2026
| **V1 notification channels** | V1 requires both in-app and email notifications for critical operational events. Preferences may control reminders and informational notifications, but cannot disable booking, allocation, rejection, cancellation, reallocation, no-show, penalty, or manual-correction notifications. Details are defined in `docs/business-layer/notification.md`. | Architect | 9.5.2026
| **Booking implementation model** | Booking will be implemented story-by-story using vertical slices. Each story cuts through domain, application, API, persistence, notification, audit, and tests where needed. The implementation order and acceptance criteria are defined in `docs/business-layer/booking-vertical-slices.md`. | Architect | 10.5.2026
| **Booking Phase 1 slice completion** | Booking vertical slices B001-B010 are implemented and merged. Remaining Booking-adjacent work is integration and hardening: authenticated tenant/user/role context, Profile-provided eligibility and vehicle snapshots, Notification and Audit consumers, and production infrastructure concerns. | Architect | 10.5.2026
| **Draw cut-off: configurable, default 18:00** | Request submission cut-off is configurable per tenant, stored in Configuration service. Default is 18:00 local time. Draw workflow triggered by Dapr cron binding at the configured time. Requests after cut-off rejected; 500-cap enforced before cut-off. Docs implied the lock mechanism but did not specify timing or configurability. | Architect | 9.5.2026
| **GDPR audit: pseudonymisation** | Audit records store `actor_hash` (SHA-256 of `user_id`) — never name or email. Separate `PiiMapping` collection holds hash→identity. On GDPR erasure: delete mapping row only. Audit log remains immutable and anonymous. Chosen over field redaction to preserve append-only invariant. GDPR Article 25 explicitly names pseudonymisation as a privacy-enhancing technique. | Architect | 9.5.2026
| **Payment provider: stub** | `IPaymentProvider` interface in Application layer, `StubPaymentProvider` in Infrastructure. Real provider (Stripe/PayU) wired in later without touching domain or application logic. | Architect | 9.5.2026
| **Real-time frontend: SSE** | Server-Sent Events via `GET /notifications/stream` on Notification service. Dapr pub/sub events bridged to connected SSE clients. No Azure dependency, no extra infrastructure, no per-connection cost. Azure SignalR rejected (Azure-specific, cost); MQTT rejected (poor browser support). ASP.NET Core native SSE, React Native via EventSource. | Architect | 9.5.2026
| **ID001 JWT claim mapping** | `userId` resolved from `ClaimTypes.NameIdentifier` with `sub` fallback. `tenantId` from `tenant_id` custom claim. `roles` from `ClaimTypes.Role` array (empty list when absent). Missing `userId` or `tenantId` in an authenticated token returns 401 — both are required for any FPS operation. Stable names chosen for cross-service use via `ICurrentUser` in `FPS.SharedKernel`. Keycloak token mapper must emit `tenant_id` when Phase 2 wires the OIDC provider. | Architect | 10.5.2026
| **Project license: AGPL-3.0-or-later** | FPS is licensed under the GNU Affero General Public License v3.0 or later. The project remains open source while requiring network-service distributors to provide source code for modified versions, reducing the risk of closed SaaS forks. | Architect | 11.5.2026
| **Project copyright and contribution notice** | Repository copyright notice is recorded in `NOTICE`. Contributions, including AI-assisted changes prepared under Robert Vejvoda's direction, are accepted under AGPL-3.0-or-later unless a separate written agreement says otherwise. `CONTRIBUTING.md` records this contribution rule for future collaborators. | Architect | 11.5.2026
| **N001 notification channel split** | `N001` implements the Booking-event consumer and idempotent in-app notification records only. Email remains a v1 requirement but is implemented in a later Notification slice after the in-app record contract, recipient mapping, and deduplication behavior are stable. | Architect | 11.5.2026
| **A001 audit scope split** | `A001` implements the Booking-event Audit consumer and append-only pseudonymised audit records only. Audit query APIs, retention/backup/integrity jobs, and GDPR erasure/PiiMapping persistence are later slices after the audit record contract and idempotency behavior are stable. | Architect | 11.5.2026
| **CI visibility** | Repository entry points should show GitHub Actions red/green status for CI and documentation deployment. CI should validate code, tooling, workflow, and generated-client changes, support manual runs, and run weekly to detect environment drift. | Architect | 11.5.2026
| **MOB001 app shell scope** | The first mobile slice is an Expo managed TypeScript app shell only. It consumes generated API client types and supports development bearer-token handoff, but real login, booking workflows, push/SSE notifications, native packaging, and app-store delivery are later slices. | Architect | 11.5.2026
