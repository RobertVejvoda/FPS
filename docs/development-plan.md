# FPS Development Plan

## What We Are Building

The **Fair Parking System (FPS)** is a multi-tenant SaaS platform that replaces manual, email-based parking management with a transparent, algorithm-driven allocation system. The core idea is a daily **Draw process**: employees submit requests for the next day's slot; a weighted lottery runs nightly and assigns slots fairly, favouring those who park least often.

The system is not a simple CRUD app — the Draw is a long-running distributed process that locks slots, runs an allocation algorithm, fires notifications, and updates metrics. This makes it an ideal candidate for **Dapr Workflows**.

Booking implementation planning is maintained in [Booking Implementation Slices](implementation/booking-vertical-slices). Business rules and contracts remain under the business layer: [Booking Event Contracts](business-layer/booking-event-contracts), [Booking Authorization](business-layer/booking-authorization), [Booking Reason Codes](business-layer/booking-reason-codes), [Booking API Contract](business-layer/booking-api-contract), and [Booking Context Contract](business-layer/booking-context-contract).

Progress, PRs, implementer attribution, and current ownership are tracked in the [Implementation Tracker](implementation-tracker). Update that tracker whenever a slice is created, assigned, implemented, reviewed, or merged.

---

## Architecture Style

### Onion Architecture (Clean Architecture) per service

Each microservice is structured in concentric layers. Dependencies always point inward — outer layers depend on inner ones, never the reverse.

```
┌───────────────────────────────────┐
│  API / Infrastructure (outermost) │  ← HTTP controllers, Dapr bindings, DB adapters
│  ┌───────────────────────────┐    │
│  │  Application              │    │  ← Use cases (commands, queries, workflows)
│  │  ┌───────────────────┐   │    │
│  │  │  Domain (core)    │   │    │  ← Aggregates, value objects, domain events
│  │  └───────────────────┘   │    │
│  └───────────────────────────┘    │
└───────────────────────────────────┘
```

**Why this matters here**: the Draw algorithm lives in the Domain layer, completely independent of Dapr, databases, or HTTP. It can be unit-tested in isolation with zero infrastructure.

### Tactical DDD

| Pattern | Used For |
|---|---|
| Aggregate | `BookingRequest`, `SlotAllocation`, `ParkingSlot`, `UserProfile` |
| Value Object | `TimeSlot`, `VehicleType`, `BookingStatus`, `UserId`, `SlotCriteria` |
| Domain Event | `BookingSubmitted`, `DrawCompleted`, `SlotAllocated`, `BookingCancelled` |
| Repository | One per aggregate root, interface in Domain, implementation in Infrastructure |
| Domain Service | `ParkingAllocationService` (two-tier allocation: company car guaranteed first, then weighted lottery, stateless) |

### CQRS + MediatR

Commands mutate state. Queries read state. No shared model between the two. MediatR dispatches both. Dapr Workflows orchestrate multi-step commands.

### Shared Kernel

`FPS.SharedKernel` (already started in `code/server/Shared/`) contains:
- `IAggregateRoot`, `IEntity`, `IDomainEvent`
- `ValueObject` base class
- `DomainException`, `BaseException`
- `IRepository<T>`, `IUnitOfWork`
- `DomainEvent`, `IEventPublisher`, `InMemoryEventPublisher`
- `DaprHealthCheck`

This package is referenced by all services — it must remain stable and have no infrastructure dependencies.

---

## Technology Stack

| Concern | Choice | Note |
|---|---|---|
| Backend | **.NET 10** | Upgrade from .NET 9 documented in wiki — .NET 10 released Nov 2025 |
| API | ASP.NET Core Web API | Minimal APIs for simple endpoints |
| Distributed runtime | **Dapr 1.14+** | ⚠️ Docs said 1.4.0 — updated; Workflows require 1.10+ |
| Workflows | **Dapr Workflow** | .NET SDK `Dapr.Workflow` |
| CQRS | MediatR | Commands and queries dispatched per service |
| Write side (CQRS) | **Dapr state store → MongoDB** | Aggregate persistence by ID; tenant isolated by tenant-specific MongoDB collections |
| Read side (CQRS) | **MongoDB driver** | Projections, aggregation pipelines, reporting queries |
| Multi-tenancy | **Collection-per-tenant** | Each tenant gets tenant-specific collections in a service-owned MongoDB database; collection names are resolved from authenticated/service context |
| Cache / session | Redis | Identity sessions, rate limiting |
| Message broker | **RabbitMQ via Dapr pub/sub** | All async domain events |
| Auth | **Keycloak** (OIDC/OAuth 2.0) | JWT tokens, RBAC, MFA |
| API gateway | Traefik | Routing, TLS, rate limiting |
| Dev orchestration | **.NET Aspire** | Local dev, service discovery, dashboard |
| Observability | Prometheus + Grafana + Loki + Jaeger | Already designed |
| Secrets | Vault | All credentials, API keys |
| Object storage | MinIO | Files, report exports |
| IaC | Terraform + Helm | K8s manifests |
| Frontend (web) | React | Consistent with mobile stack |
| Frontend (mobile) | **React Native 0.81.5 + Expo SDK 54** | Managed workflow, no native build tooling; checked in at `code/mobile/fps-mobile` |

---

## Current Plan Tracking

Last validated: 14.5.2026 by Codex against `origin/master` after PR #99, with N002 implemented, MOB005 in Claude review/implementation flow, and OPS000 prepared in issue #100.

Overall status: **on track, with the expected scope shift from backend foundation to mobile and product hardening.** Booking Phase 1 and the first integration/mobile sequence are merged. The plan remains coherent because mobile work is now using the generated API client and authenticated backend scoping instead of hand-copying DTOs or trusting client-supplied tenant/user identity.

What is done:

- Booking core vertical slices `B001`-`B010`.
- Authenticated user context and Booking API authenticated scoping: `ID001`, `BK011`.
- Profile vehicle/eligibility snapshot consumed by Booking: `P001`.
- Booking event consumers for in-app Notification and pseudonymised Audit records: `N001`, `A001`.
- Notification history API, unread count, mark-read API, and SSE stream: `N002`.
- Parking policy/configuration hardening: `CFG001`.
- OpenAPI/TypeScript client generation and stale-check tooling: `API001`.
- CI/docs visibility and weekly/manual validation hooks: `CI001`.
- Mobile foundation, first read-only employee screen, real OIDC login, and booking submission: `MOB001`, `MOB002`, `MOB003`, `MOB004`.
- Agent routing docs and handoff-only Claude automation.

What is planned next:

- Mobile booking actions after real login: MOB005 is ready for Claude handoff with cancel and confirm-usage actions.
- Notification v1 completion: email delivery and preferences remain later slices.
- Audit v1 completion: query API, retention/integrity jobs, and GDPR PII mapping erasure workflow.
- Production infrastructure: first refresh hosting/deployment options in OPS000 with Dapr as the portability boundary and cost as a first-class constraint; then continue Dapr components, tenant collection/index provisioning, secrets, observability, and runbooks.

Plan validation notes:

- The dependency order has held: API client and CI landed before mobile, and `MOB002` stayed read-only.
- The remaining risk is not the Booking domain; it is production integration depth: real IdP setup, production persistence/provisioning, notification delivery, and operational hardening. OPS000 now precedes production infrastructure work so the deployment target is revalidated before more CI/CD or hosting-specific changes.
- Phase headings below remain useful as roadmap groupings, but completed slice tracking is now more accurate than the original week numbers.

---

## Discrepancies & Plan Risks

This section tracks historical gaps that were found while planning and the remaining risks that still need future slices.

### Resolved

**1. Dapr version was outdated** ✅ *Resolved*
FPS targets **Dapr 1.14+**. Dapr Workflows require at least 1.10, and the current architecture treats durable workflow adoption as a future hardening step where needed.

**2. Multi-tenancy isolation strategy was undefined** ✅ *Resolved, revised 14.5.2026*
The current decision is collection-per-tenant in MongoDB. Services use a service-owned database and resolve tenant-specific collection names from authenticated/service context.

**3. Draw algorithm edge cases were underspecified** ✅ *Resolved for Booking Phase 1*
Executable Draw rules are now documented in `docs/business-layer/allocation-rules.md` and implemented across Booking slices `B001`-`B010`.

**4. Mobile platform decision: React Native vs .NET MAUI** ✅ *Resolved*
The project uses React Native with Expo managed workflow. The checked-in mobile baseline is React Native 0.81.5 and Expo SDK 54.

**5. Event topology** ✅ *Resolved for Booking*
Booking event contracts are defined in `docs/business-layer/booking-event-contracts.md`. Other services may add their contracts slice-by-slice.

**6. Draw process volume was not quantified** ✅ *Resolved*
The Draw cap is 500 requests per tenant per Draw.

**7. GDPR vs. audit log tension** ✅ *Resolved — pseudonymisation*
Audit records store `actor_hash` (SHA-256 of `user_id`). A separate `PiiMapping` collection holds the hash→identity link. On GDPR erasure: delete the mapping row. Audit log stays immutable and anonymous.

### Remaining Risks

**8. Production authentication and authorization**
The code has authenticated context and claim mapping, but production Keycloak/OIDC setup, token lifecycle, and full role-policy wiring are still planned.

**9. Production Dapr/MongoDB/tenant provisioning**
The architecture is decided, but production-grade component configuration, tenant collection/index provisioning, secrets, and runbooks remain future operational work.

**10. Notification and Audit v1 completion**
N001 and A001 established the first consumers. Email, SSE/history APIs, audit query, retention/integrity, and GDPR erasure workflows remain planned.

### Minor / Future

**11. AI/ML allocation enhancement** — mentioned as future, no tech chosen. Skip for now.

**12. Gamification** — badges/leaderboards have no business case. Skip unless explicitly prioritised.

**13. Card reader / physical confirmation integration** — no vendor or protocol specified. Leave as a stub interface.

---

## Development Phases

### Phase 0 — Foundation (Week 1–2)

Goal: every developer can run the full stack locally with a single command.

- [ ] Set up `.NET Aspire` app host (`FPS.AppHost`)
- [ ] Configure Dapr sidecars per service in Aspire
- [ ] Dapr components: RabbitMQ pub/sub, MongoDB state store, Redis cache, Vault secrets
- [x] Shared `docker-compose.yaml` for infrastructure exists under `code/infrastructure` (production-grade Dapr component and collection provisioning hardening remains)
- [x] `FPS.SharedKernel` compiles and is referenced by services
- [x] Establish `FPS.sln` for server-side cross-service navigation and validation
- [x] CI: GitHub Actions workflow builds and tests on PRs and `master`
- [ ] ~~Decision log: resolve multi-tenancy strategy, mobile platform, Dapr version~~ ✅ All resolved

**Deliverable**: `docker compose up` starts all infra; `dotnet run` in AppHost starts all services.

---

### Phase 1 — Booking Domain (Week 3–6)

This is the core of FPS. Everything else is supporting.

Implementation should follow vertical slices from `docs/implementation/booking-vertical-slices.md`. Do not complete the whole domain layer before application and API work; each story should cut through the layers and be independently testable.

**Current implementation status**: Phase 1 Booking vertical slices B001-B010 are implemented and merged:

- B001 Submit Future Booking Request
- B002 Submit Same-Day Booking Request
- B003 Cancel Pending Request
- B004 Run Scheduled Draw
- B005 Cancel Allocated Reservation And Reallocate
- B006 Confirm Usage
- B007 Mark No-Show
- B008 View My Bookings
- B009 View Draw Status
- B010 Manual Correction

Remaining work in this phase is hardening and integration with the surrounding platform: real authenticated tenant/user/role context, Profile-provided eligibility and vehicle snapshots, Notification and Audit consumers, and production infrastructure concerns.

**Domain layer** (`FPS.Booking.Domain`):
- [x] `BookingRequest` aggregate — submit, cancel, expire
  - Must follow `docs/business-layer/booking-request-lifecycle.md`
- [x] `SlotAllocation` aggregate — allocate, confirm, release
- [x] `ParkingSlot` value object (or entity depending on ownership)
- [x] `TimeSlot` value object (date + half/full day)
- [x] `BookingStatus`, `VehicleType`, `UserId` value objects
- [x] `ParkingAllocationService` — two-tier allocation:
  - **Tier 1**: requests from users with `HasCompanyCar = true` are guaranteed — allocated first, no lottery, no penalty ever
  - **Tier 2**: weighted lottery on remaining slots: `weight = 1 / (1 + RecentAllocationCount + ActivePenaltyScore)`
    - `RecentAllocationCount`: successful non-company-car allocations in the tenant's configured lookback window; same-day allocations count
    - Lookback window is tenant-configurable and defaults to `10` days
    - `ActivePenaltyScore`: active penalty points from late cancellations, no-shows, policy violations, or manual HR adjustments
    - Rejected requests are not part of the default denominator; if FPS later rewards repeated unlucky requestors, it should be modelled as a separate positive factor
    - If company-car requests exceed available matching capacity, reject overflow requests for now
- [ ] `HasCompanyCar` flag on `UserProfile` aggregate — set by admin, read by allocation service
  - Booking currently consumes company-car eligibility through request/profile snapshots. The Profile aggregate remains Phase 2 work.
- [x] Domain events: `BookingSubmitted`, `BookingCancelled`, `DrawStarted`, `SlotAllocated`, `DrawCompleted`
- [x] Unit tests for allocation service (pure domain, no infrastructure)
- [x] Implement Draw behavior against `docs/business-layer/allocation-rules.md`
- [x] Implement request lifecycle behavior against `docs/business-layer/booking-request-lifecycle.md`
- [x] Resolve allocation policy from `docs/business-layer/parking-policy-configuration.md`
- [x] Implement Booking story-by-story against `docs/implementation/booking-vertical-slices.md`

**Application layer** (`FPS.Booking.Application`):
- [x] Commands: `SubmitBookingRequest`, `CancelBooking`, `TriggerDraw`, `ConfirmSlotUsage`
- [x] Commands: `EvaluateNoShow`, `ApplyManualCorrection`
- [x] Queries: `GetMyBookings`, `GetDrawStatus`
- [ ] Query: `GetAllocationResult`
  - Covered by `GetMyBookings` and `GetDrawStatus` for the current Booking slices; keep a distinct allocation-result query only if a later consumer needs it.
- [x] `IBookingRepository`, `IBookingQueryRepository`, `IDrawRepository`, `IPenaltyRepository`, `ICorrectionAuditRepository` interfaces

**Dapr Workflow — Draw Process** (`BookingWorkflow`, future platform hardening):

The current Booking implementation runs Draw behavior through application handlers and domain services. A durable Dapr workflow can be added later if operational replay, long-running orchestration, or cross-service compensation requires it.

```
BookingWorkflow
  ├── Activity: LockTimeSlotsActivity        (prevent new requests)
  ├── Activity: LoadRequestsActivity          (fetch all requests for date)
  ├── Activity: RunAllocationAlgorithmActivity (weighted lottery → assignments)
  ├── Activity: PersistAllocationsActivity    (save results)
  ├── Activity: UpdateUserMetricsActivity     (update RecentAllocationCount / ActivePenaltyScore per user)
  ├── Activity: PublishNotificationsActivity  (fire booking.allocated events)
  └── Activity: UnlockTimeSlotsActivity       (compensate if anything above fails)
```

Each activity is idempotent. The workflow is durable — if it crashes mid-run, Dapr replays from the last completed activity.

> **Trigger**: the Draw workflow is started by a Dapr cron binding at the tenant's configured cut-off time (default **18:00** local time, stored in Configuration service per tenant). `LockTimeSlotsActivity` fires immediately on workflow start — no new requests accepted after this point. Requests submitted before cut-off are also subject to the 500-request cap.
>
> **Volume cap**: maximum 500 requests per tenant per Draw. The Booking service rejects submissions once this limit is reached for a given date. At 500 items the allocation algorithm completes in under a millisecond — no fan-out or child workflows needed.
>
> **Executable rules**: Draw implementation must follow `docs/business-layer/allocation-rules.md`, including duplicate detection, seeded lottery audit data, company-car overflow rejection, same-day metric updates, cancellation reallocation, and default penalties.
>
> **Request lifecycle**: Request status transitions, late-cancellation trigger, usage confirmation, no-show handling, and employee-visible reasons are defined in `docs/business-layer/booking-request-lifecycle.md`.
>
> **Policy configuration**: Tenant defaults with per-location overrides are defined in `docs/business-layer/parking-policy-configuration.md`.
>
> **Vertical slices**: Booking implementation order and story acceptance criteria are defined in `docs/implementation/booking-vertical-slices.md`.
>
> **Authorization**: Booking role/action permissions are defined in `docs/business-layer/booking-authorization.md`.
>
> **API contract**: Booking response and error shapes are defined in `docs/business-layer/booking-api-contract.md`.

**Infrastructure layer** (`FPS.Booking.Infrastructure`):
- [x] Dapr state store client — save/load Booking request state and related Booking read-model data
- [x] Booking query repository — bookings by date, requests per user, draw inputs, allocation history
- [ ] Tenant collection resolver — resolves tenant-scoped MongoDB collection names per request/service context
- [x] Dapr pub/sub publisher for domain events
- [ ] Dapr pub/sub subscriber for `configuration.slotsUpdated`
- [x] Remove: EF Core, SQL Server, `BookingDbContext` (already exists in code — dead code to delete)

**API layer** (`FPS.Booking.API`):
- [x] `POST /bookings` — submit request (future date → queued for Draw; today → immediate allocation if slot available); same 500-cap applies to both paths
- [x] `DELETE /bookings/{id}` — cancel
- [x] `GET /bookings` — my bookings
- [x] `POST /draws/trigger` — admin-only, trigger draw manually
- [x] `GET /draws/{date}/status` — draw status
- [x] `POST /bookings/{id}/confirm-usage` — confirm usage
- [x] `POST /bookings/no-show-evaluation` — evaluate no-shows
- [x] `POST /bookings/{id}/manual-corrections` — apply manual correction

**Tests**:
- [x] Unit: Domain (allocation algorithm, aggregate invariants)
- [x] Application handler tests
- [x] API controller tests with mocked application layer
- [ ] Integration: Dapr state store and MongoDB queries against real MongoDB (TestContainers)
  - Integration test scaffolding exists, but current validation skips these tests.

---

### Phase 2 — Identity & Profile (Week 7–8)

### Implementation Partner Queue

Booking Phase 1 and the first integration/mobile sequence are complete. New implementation work should still proceed through focused slices. Each slice should be its own branch and PR, and implementation must stop when a business rule or cross-service contract is missing.

| Status | Slice | Goal | Notes |
| --- | --- | --- | --- |
| Done | `ID001` Authenticated User Context | Resolve authenticated tenant, user, and roles from claims and expose `GET /me`. | Merged. Claim mapping is recorded in [Versions and Decisions](versions-and-decisions). |
| Done | `BK011` Booking Uses Auth Context | Replace Booking API tenant/user parameters with authenticated context for employee-facing actions. | Merged. Employee Booking APIs do not trust caller-supplied tenant/requestor identity. |
| Done | `P001` Profile Vehicle Snapshot | Add Profile-owned vehicle and company-car eligibility data needed by Booking validation/allocation. | Merged. Booking consumes Profile facts through a boundary. |
| Done | `N001` Booking Notification Consumer | Consume Booking events and create idempotent in-app notification records. | Merged. Email, history API, unread counts, and SSE remain later Notification slices. |
| Done | `A001` Booking Audit Consumer | Persist append-only audit records for Booking events with pseudonymised actors. | Merged. Audit query, retention/integrity, and GDPR erasure remain later Audit slices. |
| Done | `CFG001` Parking Policy/Slot Source | Move default/in-memory Booking policy and slot inputs toward Configuration-owned contracts. | Merged. |
| Done | `API001` OpenAPI Client Contract | Stabilise OpenAPI output and generated TypeScript client for web/mobile. | Merged. Client stale-check tooling is available. |
| Done | `CI001` Build Status and CI Visibility | Make repository health visible and keep CI reliable across code, tooling, generated clients, and docs. | Merged. |
| Done | `MOB001` React Native App Shell | Scaffold React Native + Expo mobile client and generated API-client consumption. | Merged. Uses development bearer-token handoff only. |
| Done | `MOB002` Mobile My Bookings | Implement the first read-only employee booking screen in mobile. | Merged. Read-only, authenticated-scoped, cursor-paginated `GET /bookings` screen. |
| Done | `MOB003` Mobile Real Login | Replace development bearer-token handoff with a production OIDC/Keycloak flow. | Merged in PR #78. |
| Done | `MOB004` Mobile Booking Submission | Let employees submit parking requests from mobile. | Merged in PR #87. |
| Ready | `MOB005` Mobile Booking Actions | Add cancel and confirm-usage actions to mobile. | Prepared in [issue #91](https://github.com/RobertVejvoda/FPS/issues/91). Must use existing Booking endpoints and employee-safe error/reason handling. |
| Done | `N002` Notification API And Stream | Expose notification history, unread counts, mark-read API, and SSE stream. | Implemented in PR #93; SSE JSON casing follow-up in [PR #94](https://github.com/RobertVejvoda/FPS/pull/94). Email remains separate. |
| Ready | `OPS000` Hosting And Deployment Strategy Options | Compare hosting/deployment options before more production infrastructure work. | Prepared in [issue #100](https://github.com/RobertVejvoda/FPS/issues/100). Dapr APIs/components/bindings are the portability boundary; cost is a first-class criterion. |
| Next | `A002` Audit Query And Erasure Support | Add auditor query API and GDPR PII mapping erasure workflow. | Builds on A001 append-only audit records. |
| Next | `OPS001` Local/Production Dapr Hardening | Align local Dapr components, tenant provisioning, secrets, and operational runbooks. | Start after OPS000 clarifies the target hosting/deployment path. Should stay infrastructure-focused; no product behavior changes. |

#### Slice ID001: Authenticated User Context

Purpose: establish the security/context foundation required before Profile, Notification, Audit, and mobile work.

Scope:

- Add a shared current-user abstraction usable by API controllers and application handlers.
- Resolve `tenantId`, `userId`, and roles from authenticated claims.
- Expose `GET /me` in `FPS.Identity`.
- Add tests for authenticated and unauthenticated requests.
- Document any final claim-name decision in `docs/versions-and-decisions.md` if the implementation chooses stable claim names not already documented.

Acceptance criteria:

- Authenticated requests can obtain tenant, user, and roles without trusting request body or query string identity.
- `GET /me` returns current user identity, tenant, and roles.
- Unauthenticated access returns `401`.
- Tests prove a caller cannot spoof tenant or user through request payload fields.

Stop and ask before implementation if:

- required claim names are ambiguous;
- the slice would require a full Keycloak deployment or provisioning flow;
- Booking endpoint migration cannot be kept small;
- implementation would introduce mobile, Profile vehicle, Notification, or Audit behavior.

**Identity** (`FPS.Identity`):
- [x] Shared authenticated user context abstraction and claim mapping
- [x] JWT validation middleware/test path for current APIs
- [x] `GET /me` — current user from token claims
- [ ] Production Keycloak/OIDC integration and provisioning
- [ ] User roles fully wired to production authorization policies: `employee`, `hr_manager`, `admin`, `auditor`, `accounting`
- [ ] Service-to-service auth via Dapr mTLS (Sentry) — no extra work needed

**Profile** (`FPS.Profile`):
- [x] Booking-facing profile/vehicle eligibility snapshot
- [x] Booking consumes Profile facts for vehicle/company-car eligibility decisions
- [ ] Full `UserProfile` aggregate — name, vehicles, preferences, scheduler settings
- [ ] Full `Vehicle` entity management — type (car/motorcycle/EV), plate
- [ ] Commands: `RegisterVehicle`, `SetParkingSchedule`, `UpdatePreferences`
- [ ] Queries: `GetProfile`, `GetVehicles`

#### Slice BK011: Booking Uses Auth Context

Purpose: migrate employee-facing Booking APIs from caller-supplied tenant/user headers to the authenticated context established by `ID001`.

Scope:

- Register and consume the shared `ICurrentUser` abstraction in `FPS.Booking.API`.
- Add authentication and authorization middleware to Booking API using the same JWT claim mapping documented by `ID001`.
- For employee self-service Booking endpoints, resolve `tenantId` from `ICurrentUser.TenantId` and requestor/actor ID from `ICurrentUser.UserId`.
- Remove trust in `X-Tenant-Id`, `X-Requestor-Id`, request body, or query string for employee tenant/user identity.
- Add or update API tests proving spoofed tenant/user headers or query parameters cannot change the authenticated context used by Booking.

Employee-facing endpoints in scope:

- `POST /bookings`
- `GET /bookings`
- `DELETE /bookings/{requestId}`
- `POST /bookings/{requestId}/confirm-usage`

Out of scope:

- New Booking business behavior, status transitions, allocation rules, or response fields.
- Profile vehicle or company-car snapshot integration.
- Notification or Audit consumers.
- Keycloak deployment/provisioning automation.
- Mobile/web frontend changes.
- Admin/system endpoint authorization redesign, except for preserving existing behavior while avoiding accidental trust in employee identity headers.

Acceptance criteria:

- Authenticated employee calls to the in-scope endpoints use only authenticated `tenantId` and `userId`.
- Missing or invalid authentication returns `401`.
- Authenticated callers cannot spoof another tenant or requestor by sending `X-Tenant-Id`, `X-Requestor-Id`, `tenantId`, `requestorId`, `userId`, or similar request fields.
- Existing Booking command/query handlers can continue accepting tenant/requestor parameters internally; this slice changes the API boundary, not domain behavior.
- API tests cover at least one write path and one read path for spoofing resistance.
- `./tools/validate.sh` passes before the PR is reported ready.

Implementation notes for Claude:

- Start from updated `master` after `ID001` is merged; do not branch from the ID001 feature branch.
- Prefer a small Booking API adapter change over refactoring application commands.
- If current test infrastructure makes real JWT setup expensive, mirror the `ID001` test approach with a local test signing key.
- If an admin/system endpoint needs a broader auth model to avoid trusting headers, stop and ask Codex before widening this slice.

#### Slice P001: Profile Vehicle Snapshot

Purpose: make Profile the source of truth for employee parking eligibility, vehicle capability data, and company-car entitlement needed by Booking validation and allocation.

Scope:

- Add a Profile-owned `UserProfile` model for the authenticated tenant/user.
- Add vehicle records owned by Profile, including the minimum parking fields Booking needs for slot matching.
- Expose a Booking-facing snapshot query that returns employee eligibility, vehicles, and company-car/accessibility/reserved-space eligibility for one requestor in one tenant.
- Include a stable snapshot version or timestamp so Booking can record which Profile facts influenced a booking decision.
- Update Booking to consume the Profile snapshot through a Booking-owned application interface when validating submission and allocation inputs.
- Add tests for eligible profile, ineligible profile, missing required vehicle, vehicle capability mismatch, and company-car entitlement propagation.

Profile snapshot fields:

| Field | Meaning |
| --- | --- |
| `tenantId` | Tenant that owns the profile snapshot. |
| `userId` | Requestor identity from authenticated context. |
| `profileStatus` | `Active`, `Inactive`, or `Suspended`. Only `Active` can request parking. |
| `parkingEligible` | Whether the employee may request parking under tenant/Profile rules. |
| `hasCompanyCar` | Whether the requestor enters Tier 1 company-car allocation when policy enables it. |
| `accessibilityEligible` | Whether the requestor may use accessibility-reserved capacity. |
| `reservedSpaceEligible` | Whether the requestor may use reserved-space capacity where policy requires it. |
| `vehicles[]` | Registered vehicles usable for parking requests. |
| `snapshotVersion` | Stable version, ETag, or timestamp for audit/replay. |

Vehicle snapshot fields:

| Field | Meaning |
| --- | --- |
| `vehicleId` | Stable Profile-owned vehicle identifier. |
| `licensePlate` | Plate or registration value required for operational use. |
| `vehicleType` | Canonical type such as `car`, `motorcycle`, or another documented slot-matching value. |
| `isElectric` | Whether the vehicle may require EV-capable parking. |
| `isActive` | Inactive vehicles cannot be selected for new booking requests. |

Out of scope:

- Booking allocation rule changes beyond consuming Profile-provided facts.
- Profile UI, mobile UI, or self-service vehicle management screens.
- HR/admin bulk profile management.
- Keycloak user provisioning or identity lifecycle.
- Notification, Audit, Reporting, or Configuration work.
- Storing Profile private data in Booking responses or exposing it to other employees.

Acceptance criteria:

- Booking no longer relies on request body fields for company-car entitlement or vehicle capability facts when Profile snapshot data is required.
- Booking records the Profile snapshot version/reference on decisions that depend on eligibility, vehicle, company-car, accessibility, or reserved-space facts.
- If Profile is unavailable for required validation, Booking fails safely and does not accept or allocate the request.
- Employee-facing Booking responses do not expose Profile private data beyond existing Booking outcome fields.
- Tests fake Profile through a Booking-owned interface; Booking must not read Profile persistence models directly.
- `./tools/validate.sh` passes before the PR is reported ready.

Implementation notes for Claude:

- Start from updated `master` after `BK011` is merged.
- Keep the first Profile implementation minimal and slice-focused; do not build the full Profile product area.
- Prefer a small Profile API/query plus Booking anti-corruption interface over coupling Booking to Profile internals.
- If the current Booking request DTO still accepts vehicle/company-car fields for compatibility, Booking must prefer Profile snapshot facts and must not let a caller spoof elevated eligibility.

#### Slice N001: Booking Notification Consumer

Purpose: create the first Notification service slice by consuming published Booking events and storing idempotent in-app notification records for affected recipients.

Scope:

- Add or complete a `FPS.Notification` service that subscribes to the Booking event topic.
- Accept Booking event envelopes defined in `docs/business-layer/booking-event-contracts.md` and ignore unknown additive payload fields.
- Create one in-app notification record per required recipient and source event.
- Deduplicate records with the stable key `eventId + recipientId + notificationType + channel`.
- Store the notification fields needed by future notification history: tenant, recipient, channel, notification type, source event ID, related booking request ID, date/time slot, location, message text, read/unread state, delivery status, and timestamps.
- Generate employee-facing messages from event type plus safe payload fields such as `reasonText`; do not expose algorithm diagnostics or other employees.
- Treat malformed events with missing required envelope fields as rejected or ignored without throwing unhandled service errors.
- Add handler tests for idempotency, known event mapping, missing recipient behavior, reason text, unread default, and reallocation/cancellation recipient handling where the event payload supplies multiple affected recipients.

N001 event types in scope:

- `booking.requestSubmitted`
- `booking.requestRejected`
- `booking.requestCancelled`
- `booking.slotAllocated`
- `booking.drawCompleted`
- `booking.penaltyApplied`
- `booking.noShowRecorded`
- `booking.usageConfirmed`
- `booking.requestExpired`
- `booking.manualCorrectionApplied`

Recipient rules:

- Employee-facing notifications go to `payload.requestorId`.
- Reallocation or released-slot scenarios must notify both affected employees only when the published event payload includes enough recipient data, such as `payload.affectedRecipientIds`, to do so.
- If an event type requires HR/facility/admin notification but no configured recipient source exists yet, record the gap in code or tests and stop before inventing a recipient directory.
- Do not query Booking or Profile to infer extra recipients in N001.

Out of scope:

- Email sending, SMTP/SendGrid integration, push notifications, or user preference handling.
- SSE streaming, notification history API, unread-count API, or frontend/mobile notification UI.
- Notification admin/support views.
- Publishing new Booking events or changing Booking state transitions.
- Audit, Reporting, or Configuration behavior.
- A production persistence adapter beyond the smallest repository abstraction needed for tests.

Acceptance criteria:

- Given a valid in-scope Booking event with `requestorId`, Notification stores one unread in-app record for that recipient.
- Given the same event is delivered twice, Notification stores only one record for the same event, recipient, notification type, and in-app channel.
- Given two distinct recipients are explicitly present for a reallocation/cancellation outcome, Notification stores separate deduplicated records for each recipient.
- Given an event has employee-visible `reasonText`, the stored message includes that reason without exposing internal diagnostics.
- Given an event lacks a recipient needed for an employee notification, Notification does not create a misleading record and the behavior is covered by a test.
- Notification failure does not roll back Booking state; N001 only consumes already-persisted Booking events.
- `./tools/validate.sh` passes before the PR is reported ready.

Implementation notes for Claude:

- Start from updated `master` after `P001` is merged.
- If continuing from PR `#28`, rebase or recreate it from current `master` and keep only N001 changes.
- Remove scaffold leftovers such as template `UnitTest1` files and unused `.http` samples unless they provide real value.
- Keep event DTOs local to Notification unless a shared cross-service contract package already exists.
- Do not expand N001 to satisfy the full v1 email requirement; that remains a later Notification slice.

#### Slice A001: Booking Audit Consumer

Purpose: create the first Audit service slice by consuming Booking events and storing immutable audit records with pseudonymised actor identity.

Scope:

- Add a minimal `FPS.Audit` service that subscribes to the Booking event topic.
- Accept Booking event envelopes defined in `docs/business-layer/booking-event-contracts.md` and tolerate additive payload fields.
- Store one append-only audit record per unique Booking event.
- Deduplicate by `eventId`; duplicate event delivery must not create duplicate audit records.
- Store event envelope fields needed for traceability: event ID, event type, event version, occurred-at timestamp, tenant ID, correlation ID, causation ID, actor type, actor hash, source, and captured payload.
- Store entity references when present, especially booking request ID, requestor ID hash, location ID, date/time slot, previous/new status, reason code, draw attempt ID, policy/snapshot references, and source event ID.
- Pseudonymise user IDs before writing audit records. Audit records must store `actorHash` and requestor/affected-user hashes, not names, emails, or raw user IDs.
- Define the `PiiMapping` record shape in code or docs when useful, but do not implement the GDPR erasure workflow or a production PII mapping store in A001.
- Add tests for append-only behavior, idempotency by event ID, actor pseudonymisation, requestor pseudonymisation, and payload capture without unsupported PII.

Audit record minimum fields:

| Field | Meaning |
| --- | --- |
| `auditRecordId` | Internal Audit record ID. |
| `sourceEventId` | Booking `eventId`; unique idempotency key. |
| `eventType` | Booking event type. |
| `eventVersion` | Booking event schema version. |
| `occurredAt` | Business event timestamp from Booking. |
| `recordedAt` | Audit service ingestion timestamp. |
| `tenantId` | Tenant that owns the event. |
| `correlationId` | Request/workflow correlation ID. |
| `causationId` | Source command, workflow activity, or event ID. |
| `actorType` | `employee`, `hr`, `admin`, `system`, or `integration`. |
| `actorHash` | SHA-256 hash of `actorId` when an actor ID is present. |
| `source` | Producing service, normally `booking`. |
| `entityType` | `bookingRequest`, `drawAttempt`, `penalty`, or another stable entity category when known. |
| `entityId` | Primary entity ID when known, usually booking request ID. |
| `payload` | Captured event payload with raw user IDs removed or hashed. |

Pseudonymisation rules:

- Hash actor/requestor/affected-user IDs with SHA-256 using a stable implementation so the same source ID produces the same hash.
- Do not store names, emails, license plates, or raw profile data in Audit records in A001.
- If a payload field is needed for traceability but contains a user ID, store the hash and a clearly named field such as `requestorHash`.
- If a payload includes operational data that may be sensitive but is already part of Booking event contracts, keep only the minimum needed for audit replay and review.

Out of scope:

- Audit query API such as `GET /audit`.
- GDPR erasure endpoint/workflow such as `DELETE /pii-mapping/{userId}`.
- Persistent `PiiMapping` storage beyond shape/documentation.
- Audit UI, reporting dashboards, search/indexing, retention jobs, backup jobs, or integrity verification jobs.
- Changes to Booking event publication or Booking state transitions.
- Notification, email, Profile, Configuration, or frontend/mobile behavior.
- Production persistence beyond the smallest repository abstraction needed for tests.

Acceptance criteria:

- Given a valid Booking event, Audit stores exactly one immutable audit record.
- Given the same event is delivered twice, Audit stores one record and treats the second delivery as a no-op.
- Given an event has `actorId`, the audit record stores `actorHash` and does not store raw `actorId`.
- Given an event has `payload.requestorId` or `payload.affectedRecipientIds`, the audit payload stores hashed user references and does not store raw IDs.
- Given an event has no actor ID, the record is still stored with actor type/source and a null actor hash.
- Audit records cannot be updated or deleted through the repository interface used by A001.
- `./tools/validate.sh` passes before the PR is reported ready.

Implementation notes for Claude:

- Start from updated `master` after N001 is merged.
- Use the Booking event contract as the source of truth; do not add new Booking events in A001.
- Mirror the N001 consumer pattern where useful, but keep Audit models separate from Notification models.
- Keep event DTOs local to Audit unless a shared cross-service contract package already exists.
- If a field appears to require raw PII for audit value, stop and ask before storing it.

#### Slice API001: OpenAPI Client Contract

Purpose: stabilise the backend OpenAPI output and create the first generated TypeScript API client contract that can be reused by future web and React Native work.

Scope:

- Enable deterministic OpenAPI JSON output for the currently implemented public API services that frontend/mobile clients need first: Identity, Booking, Profile, Notification, and Audit where applicable.
- Document and normalise route, operation ID, request DTO, response DTO, error response, authentication, and pagination conventions needed by generated clients.
- Generate a TypeScript client package from the OpenAPI output under a stable repository path such as `code/clients/typescript`.
- Add a repeatable script or documented command that regenerates the client from the current OpenAPI specs.
- Include generated types for authenticated user context, Booking submission/history/cancellation/usage confirmation, Profile snapshot basics, Notification records when exposed, and Audit only if a public API exists.
- Ensure generated client code does not require browser-only APIs so it can be consumed later by both React web and React Native + Expo.
- Add a lightweight validation step that fails when generated client output is stale after backend API contract changes.

Client contract conventions:

| Area | Requirement |
| --- | --- |
| Auth | Bearer token authentication must be represented in OpenAPI security metadata; tenant/user identity must not appear as caller-supplied tenant/requestor parameters for employee endpoints. |
| Operation IDs | Operation IDs must be stable, unique, and human-readable enough to generate useful client method names. |
| DTOs | Request and response schemas must use explicit DTOs rather than anonymous shapes where practical. |
| Errors | Common `400`, `401`, `403`, `404`, and conflict/validation responses should be represented consistently enough for clients to handle them. |
| Pagination | Cursor/page-size parameters and response shapes must be modelled consistently for list endpoints such as `GET /bookings`. |
| Dates/times | Date-only, time-slot, and timestamp fields must be represented consistently as strings with documented semantics. |
| Generated output | Generated client files should be deterministic: repeated generation without API changes should produce no diff. |

Out of scope:

- React web implementation, React Native screens, Expo setup, or UI state management.
- Changing Booking, Profile, Notification, Audit, or Identity business behavior to make client generation easier.
- Creating new public APIs that are not already required by implemented slices.
- Auth provider provisioning, login UI, token refresh UX, or Keycloak deployment automation.
- SSE streaming client implementation for notifications; this remains a later frontend/mobile slice.
- Publishing the generated client to npm or setting up package release automation.

Acceptance criteria:

- Each in-scope API service can produce OpenAPI JSON in a deterministic local development/CI-friendly way.
- The generated TypeScript client compiles or passes the repository's chosen TypeScript validation command if one is introduced in this slice.
- A documented regeneration command exists and is safe for Claude/Codex to run repeatedly.
- The generated client does not expose spoofable employee `tenantId` or `requestorId` parameters for endpoints that must use authenticated context.
- `GET /me` and employee-facing Booking endpoints are represented with bearer-token security metadata.
- Stale generated output is detectable by validation or a documented diff check.
- Existing backend validation remains green before the PR is reported ready.

Implementation notes for Claude:

- Start from updated `master` after `CFG001` is merged.
- Keep API001 focused on contracts and generated client plumbing; do not start MOB001 or web UI work.
- Prefer a small, repeatable generator setup over hand-written TypeScript clients.
- If current controllers cannot produce stable operation IDs or schemas without widening an API contract, stop and ask Codex before changing behavior.
- If choosing or adding a generator package, document the decision and command in the repo docs so the next frontend/mobile slice can use it without rediscovery.

#### Slice CI001: Build Status and CI Visibility

Purpose: make project health obvious to contributors and prevent code, tooling, generated clients, and documentation workflow changes from drifting without a visible signal.

Scope:

- Add GitHub Actions status badges for the main CI workflow and documentation deployment workflow to the repository entry points.
- Expand CI triggers beyond `code/**` so relevant changes under `.github/workflows/**`, `tools/**`, and generated client/tooling paths are validated.
- Add `workflow_dispatch` so maintainers can manually run CI.
- Add a scheduled CI run, initially weekly, to catch SDK, dependency, and environment drift even when no PR is active.
- Keep CI based on the repository solution and validation tools already in use; do not introduce a second build system.
- After `API001` lands, include the API client stale-check script in CI so generated OpenAPI and TypeScript client artifacts cannot silently drift.
- Document the CI strategy in `docs/tooling.md`.

Status badge expectations:

| Badge | Target |
| --- | --- |
| CI | `.github/workflows/ci.yml` on `master`. |
| Docs | `.github/workflows/docs.yml` on `master`. |

CI trigger expectations:

| Trigger | Requirement |
| --- | --- |
| `pull_request` to `master` | Runs when code, tools, workflow files, or generated client paths change. |
| `push` to `master` | Runs for the same relevant paths after merge. |
| `workflow_dispatch` | Allows a manual health check from GitHub. |
| `schedule` | Runs weekly until the project has enough active development to justify daily builds. |

Out of scope:

- Changing Booking, Profile, Notification, Audit, or Identity product behavior.
- Adding frontend/mobile screens.
- Replacing GitHub Actions with another CI provider.
- Publishing packages, containers, or releases.
- Enforcing branch protection in code; branch protection is a GitHub repository setting to configure after the workflow names are stable.

Acceptance criteria:

- `README.md` and `docs/Home.md` show visible CI and docs status signals.
- CI still restores, builds, and tests `code/server/FPS.sln` on Ubuntu with .NET 10.
- CI runs when `.github/workflows/**` or `tools/**` changes, not only when `code/**` changes.
- Manual and scheduled CI runs are available.
- If `API001` has merged, CI runs the generated API client stale check and fails on stale output.
- `docs/tooling.md` explains what the badges mean, what triggers CI, and where branch protection should point.

Implementation notes for Claude:

- Start from updated `master` after `API001` is resolved, unless Codex explicitly reprioritises this slice sooner.
- Keep this to GitHub Actions, entry badges, and tooling documentation.
- Use existing workflow names where possible so badge URLs and branch-protection settings remain stable.
- If the API client stale-check script depends on .NET 10 user-install PATH locally, make sure CI and local scripts agree on SDK selection.

#### Slice MOB001: React Native App Shell

Purpose: create the first mobile client foundation without implementing end-user booking behavior yet.

Scope:

- Scaffold an Expo managed React Native app under a stable path such as `code/mobile/fps-mobile`.
- Use TypeScript and keep the app in the managed Expo workflow; do not commit generated native `ios/` or `android/` directories.
- Consume the generated API client contract from `code/clients/typescript` for shared DTO/path typing.
- Add a small API access layer that centralises base URL, bearer token attachment, JSON parsing, and typed error handling.
- Add a development-only authentication handoff screen that accepts an API base URL and bearer token, then verifies the session with `GET /me` when possible.
- Add a simple authenticated app shell with navigation placeholders for:
  - Home / current parking status;
  - My bookings;
  - New booking;
  - Notifications;
  - Profile / settings.
- Add loading, empty, error, unauthenticated, and offline/unreachable API states at shell level.
- Add package scripts for local start, typecheck, and any formatter/linter selected by the scaffold.
- Add CI validation for the mobile app typecheck if the scaffold introduces a TypeScript project.
- Document how to run the app locally against a backend/API gateway base URL.

Mobile app shell contract:

| Area | Requirement |
| --- | --- |
| Platform | React Native + Expo managed workflow. |
| Language | TypeScript. |
| API contract | Use `@fps/api-client` generated types from `code/clients/typescript`; do not hand-copy DTOs. |
| Auth in MOB001 | Developer-provided bearer token only. Real login, token refresh, Keycloak browser flow, MFA, and secure production session lifecycle are later slices. |
| Navigation | Shell-level navigation only; screens may use typed mock data or read-only API probes. |
| Configuration | API base URL must be environment/config driven, not hard-coded to one developer machine. |
| State | Keep state local and simple; no global state framework unless the app shell genuinely needs it. |

Out of scope:

- Real username/password login, SSO, Keycloak, token refresh, biometric auth, MFA, or production credential storage.
- Booking submission, cancellation, usage confirmation, no-show handling, or Draw status workflows beyond placeholders or read-only probes.
- Push notifications, SSE streaming, notification preferences, unread-count APIs, or mobile background delivery.
- Maps, payments, feedback, profile editing, admin/HR screens, reporting, billing, or tenant onboarding.
- EAS build, app-store packaging, OTA update setup, native module customisation, or generated `ios/` / `android/` projects.
- Changing backend API behavior to fit the app shell.

Acceptance criteria:

- A developer can install dependencies and start the Expo app from the documented mobile path.
- The app renders a usable shell on a mobile simulator/device or Expo Go without requiring native build tooling.
- TypeScript validates the mobile project.
- The app imports generated API client types from `code/clients/typescript` rather than duplicating API DTOs.
- API base URL and bearer token are configurable for local development.
- `GET /me` is the only required live backend probe; if backend/auth is unavailable, the app shows a clear shell-level error without crashing.
- CI includes the mobile typecheck if the mobile TypeScript project is added.
- No backend business behavior changes are included.

Implementation notes for Claude:

- Start from updated `master` after CI001 is merged.
- Implement MOB001 only.
- Prefer a small Expo scaffold with clear scripts over a large app architecture.
- Keep screens intentionally thin; the goal is a stable mobile foundation for later booking slices.
- If generated API client packaging is not directly consumable by Metro/TypeScript, stop and ask Codex before replacing it with hand-written DTOs.
- If Expo scaffolding introduces platform-specific generated files or native build requirements, stop and ask before committing them.

#### Slice MOB002: Mobile My Bookings

Purpose: deliver the first useful employee mobile screen by showing the authenticated user's booking requests and outcomes through the existing `GET /bookings` contract.

Scope:

- Implement the My Bookings screen inside the MOB001 Expo app.
- Call `GET /bookings` through the mobile API access layer using the development bearer token and configured API base URL from MOB001.
- Use generated API client types from `code/clients/typescript` for request query parameters and response shapes.
- Display upcoming and recent booking requests returned by the API with employee-safe fields:
  - requested date;
  - time slot;
  - location label or location ID;
  - current status;
  - employee-visible reason text when present;
  - allocated slot label or ID only when returned and safe for the employee;
  - next action label only when returned by the API.
- Add a simple segmented filter for `Upcoming` and `Recent`, implemented through date-range query parameters when practical.
- Support pull-to-refresh or an equivalent explicit refresh control.
- Support pagination using the API cursor when `nextCursor` is returned.
- Add screen-level loading, refreshing, empty, error, unauthenticated, and unreachable-backend states.
- Add basic UI tests or component tests for list rendering, empty state, and error state if the MOB001 test setup supports it; otherwise keep TypeScript validation as the minimum gate and document the test gap.
- Keep mobile typecheck wired into CI.

Mobile display rules:

| Area | Requirement |
| --- | --- |
| Ownership | The screen must rely on backend authenticated scoping. The mobile app must not send tenant ID or requestor ID. |
| Status text | Use stable status values from the API and map them to short employee-facing labels in the app. |
| Reason text | Show `reasonText`/employee-visible reason only when the API returns it. Do not infer hidden reasons. |
| Next action | Render a non-destructive label or disabled placeholder only. Do not execute cancellation or confirmation in MOB002. |
| Pagination | Treat cursors as opaque strings. |
| Privacy | Do not show lottery weights, seed, candidate order, other employees, audit diagnostics, hidden slot metadata, or raw Profile data. |

Out of scope:

- Booking submission, booking cancellation, usage confirmation, no-show handling, or Draw status screens.
- Real login, token refresh, Keycloak, MFA, biometric auth, or production credential storage.
- Push notifications, SSE streaming, notification preferences, unread-count APIs, or background updates.
- Profile editing, vehicle management, maps, payments, feedback, HR/admin screens, reporting, or billing.
- Backend API behavior changes unless `GET /bookings` is unusable as documented; if so, stop and ask before changing the backend.

Acceptance criteria:

- A signed-in development session can open My Bookings and load data from `GET /bookings`.
- The app does not send tenant ID, requestor ID, or user ID as query/body/header identity parameters for My Bookings.
- The screen handles loading, empty, API error, invalid/expired token, and network-unreachable states without crashing.
- The screen can refresh the current list.
- If `nextCursor` is returned, the user can load the next page and items append without duplicating already-rendered booking request IDs.
- The screen uses generated API client types instead of hand-written Booking DTOs.
- TypeScript validation passes in CI.
- No backend business behavior changes are included.

Implementation notes for Claude:

- Start from updated `master` after MOB001 is merged.
- Keep MOB002 focused on read-only bookings.
- Prefer simple React Native components and local screen state over adding a global state framework.
- If the current generated client only provides types, write a small typed fetch wrapper in the mobile app rather than hand-writing API DTOs.
- If `GET /bookings` response fields differ from the B008 contract, stop and ask Codex before changing backend or mobile expectations.

#### Slice MOB003: Mobile Real Login

Purpose: replace the development-only bearer-token handoff with a real employee login flow in the Expo mobile app.

Scope:

- Add an Expo-compatible OIDC Authorization Code + PKCE login flow.
- Support runtime configuration for API base URL, issuer/discovery or authorization endpoint, client ID, scopes, and redirect URI.
- Complete browser callback handling and restore a valid session when the app starts.
- Store only the session material needed for API calls and restoration, using Expo-compatible storage primitives.
- Call `GET /me` after login and before entering the authenticated shell.
- Attach the bearer token through the existing mobile API access layer.
- Add logout that clears local session state and returns the user to the unauthenticated/login state.
- Preserve the current development flow only if it remains clearly marked as development-only and cannot be confused with production login.
- Add clear UI states for unauthenticated, login cancelled, login failed, invalid/expired token, and unreachable backend.
- Keep mobile typecheck wired into CI.

Mobile auth rules:

| Area | Requirement |
| --- | --- |
| Auth flow | OIDC Authorization Code + PKCE through an Expo managed-workflow-compatible browser-auth package. |
| Client secret | Do not store a client secret in the mobile app. |
| Backend scoping | The app must not send tenant ID, requestor ID, user ID, or roles for employee API scoping. |
| Identity display | Use `GET /me` only for display/session state; backend services remain authoritative. |
| Configuration | Do not hardcode developer-machine URLs, secrets, tokens, tenant IDs, or user IDs. |
| Native projects | Do not commit generated `ios/` or `android/` directories. |

Out of scope:

- Booking submission, cancellation, usage confirmation, no-show handling, or Draw status screens.
- Push notifications, SSE streaming, notification preferences, unread-count APIs, or background updates.
- Profile editing, vehicle management, maps, payments, feedback, HR/admin screens, reporting, or billing.
- Keycloak provisioning, realm/client setup automation, MFA policy design, tenant onboarding, or backend business behavior changes.
- App-store packaging, EAS build, OTA updates, native module customisation, or generated native projects.

Acceptance criteria:

- A user can start login from the mobile app and complete an OIDC Authorization Code + PKCE flow.
- After successful login, the app calls `GET /me` and enters the existing authenticated shell.
- Logout clears local session state.
- Expired/invalid tokens return the app to a clear unauthenticated/session-expired state or an equivalent recoverable state.
- Login cancellation, login failure, invalid configuration, and unreachable backend are handled without crashing.
- The app does not send tenant ID, requestor ID, user ID, or roles for employee API scoping.
- The implementation uses generated API client types and the existing mobile API access layer where practical.
- `npm run typecheck` passes in `code/mobile/fps-mobile`.

Implementation notes for Claude:

- Start from updated `master` after MOB002 is merged.
- Keep MOB003 focused on authentication only.
- Prefer Expo managed workflow packages and local state over adding a broad app framework.
- If current Identity/OpenAPI endpoints cannot support the flow without backend changes, stop and ask Codex before changing backend code.
- If Keycloak-specific configuration is needed, document the required settings instead of implementing provisioning automation in this slice.

---

### Phase 3 — Notification & Audit (Week 9–10)

**Notification** (`FPS.Notification`):
- [x] Booking event consumer for N001 event envelope/types
- [x] Idempotent in-app notification record store for Booking events
- [ ] Subscribe to any non-Booking event families required after Configuration/Customer/Reporting expand
- [ ] Email channel (SMTP or SendGrid)
- [ ] In-app notification history API and unread-count API
- [ ] V1 mandatory channels: in-app and email for critical operational notifications
- [ ] **SSE endpoint** `GET /notifications/stream` — clients connect, events from Dapr pub/sub are bridged to connected SSE clients; no Azure dependency, no extra infrastructure
- [ ] Idempotent — duplicate events must not send duplicate emails
- [ ] Implement notification behavior against `docs/business-layer/notification.md`

**Audit** (`FPS.Audit`):
- [x] Booking event consumer for A001 event envelope/types
- [x] Immutable append-only audit record store for Booking events
- [x] PII pseudonymisation for actor/requestor/affected-user references in A001 records:
  - Audit records store `actor_hash` (SHA-256 of `user_id`) — never name or email
- [ ] Subscribe to all domain event families as each producing service is implemented
- [ ] PII mapping persistence and erasure workflow:
  - Separate `PiiMapping` collection: `{ actor_hash, user_id, name, email }`
  - On GDPR deletion request: delete the `PiiMapping` row — audit records remain intact and anonymous
- [ ] `GET /audit` — paginated, filterable by entity/date/actor (auditor role only)
- [ ] `DELETE /pii-mapping/{userId}` — called by Identity service on GDPR erasure request

---

### Phase 4 — Configuration & Customer (Week 11–12)

**Configuration** (`FPS.Configuration`):
- [ ] `ParkingSlot` management — add/remove/update slots
- [ ] `DrawSchedule` — configure draw time per tenant
- [ ] `TimeSlot` definitions — full day, morning, afternoon
- [ ] Publishes `configuration.slotsUpdated` when slots change

**Customer** (`FPS.Customer`):
- [ ] Tenant management (this is the multi-tenancy anchor service)
- [ ] Tenant onboarding workflow (Dapr Workflow: create tenant → provision slots → create admin user → send welcome)
- [ ] User-to-tenant assignment
- [ ] Subscription tier management

---

### Phase 5 — Reporting & Billing (Week 13–15)

**Reporting** (`FPS.Reporting`):
- [ ] Subscribe to events and materialise read models in MongoDB
- [ ] Queries: utilisation rate, peak times, fairness metrics per user, penalty history
- [ ] Export: CSV, PDF
- [ ] Dashboard aggregates for HR managers

**Billing** (`FPS.Billing`):
- [ ] Subscription plans (Small/Medium/Large pricing)
- [ ] Monthly invoice generation (Dapr Workflow: calculate → generate PDF → send → record)
- [ ] Payment provider integration (stub, expandable to Stripe/PayU)
- [ ] Metering: track per-slot allocations for usage-based billing

---

### Phase 6 — Frontend (Week 16–20)

- [ ] Web app (React) — employee self-service, HR dashboard, admin panel
- [x] Mobile app (React Native + Expo) — app shell and read-only My Bookings
- [ ] Mobile app (React Native + Expo) — real login, booking actions, notifications
- [x] Shared API client generated from OpenAPI specs (used by both web and mobile)
- [ ] Shared TypeScript types between web and mobile
- [ ] Real-time updates via SSE — subscribe to `GET /notifications/stream` on Notification service

---

### Phase 7 — Production Readiness (Week 21–24)

- [ ] Kubernetes Helm charts per service
- [ ] Horizontal Pod Autoscaler configs
- [ ] Vault integration for all secrets
- [ ] Full observability stack (Prometheus, Grafana dashboards, Loki, Jaeger)
- [ ] Load testing (k6 or NBomber) against NFR targets
- [ ] Penetration testing pass
- [ ] Runbooks for Draw process failure, Keycloak outage, database failover
- [ ] Multi-region deployment strategy

---

## Dapr Workflow Design

Dapr Workflow is the right tool for:

| Process | Why Workflow |
|---|---|
| **Draw Process** | Long-running, multi-step, durable, needs compensation on failure |
| **Tenant Onboarding** | Sequential steps across multiple services |
| **Monthly Billing** | Scheduled, multi-step, auditable |
| **Cancellation with Penalty** | Conditional logic, side effects (penalty record, notification, reallocation) |

**Key properties of Dapr Workflows used here**:
- **Durable execution**: workflow state survives service restarts
- **Replay-safe activities**: all activities must be idempotent
- **Compensation**: `UnlockTimeSlotsActivity` runs if Draw fails mid-flight
- **Scheduled triggers**: Draw starts via Dapr cron binding at configured time
- **Child workflows**: Draw can spawn a child workflow per time-slot batch if volume warrants it

**.NET SDK pattern** (Dapr.Workflow):

```csharp
// Workflow definition
public class BookingDrawWorkflow : Workflow<DrawInput, DrawResult>
{
    public override async Task<DrawResult> RunAsync(WorkflowContext ctx, DrawInput input)
    {
        await ctx.CallActivityAsync<bool>(nameof(LockTimeSlotsActivity), input.Date);
        var requests = await ctx.CallActivityAsync<List<BookingRequest>>(
            nameof(LoadRequestsActivity), input.Date);
        var allocations = await ctx.CallActivityAsync<List<Allocation>>(
            nameof(RunAllocationAlgorithmActivity), requests);
        await ctx.CallActivityAsync(nameof(PersistAllocationsActivity), allocations);
        await ctx.CallActivityAsync(nameof(UpdateUserMetricsActivity), allocations);
        await ctx.CallActivityAsync(nameof(PublishNotificationsActivity), allocations);
        return new DrawResult(allocations.Count);
    }
}
```

---

## Development Standards

### Code

- No comments explaining *what* code does — name things well instead
- Comments only for *why*: hidden constraints, non-obvious invariants, external bug workarounds
- No abbreviations in names (`req` → `bookingRequest`, `svc` → `allocationService`)
- One class per file, file name matches class name
- `record` types for value objects and DTOs
- `sealed` by default on domain classes — open only when inheritance is justified

### Error Handling

- Domain invariants throw `DomainException` (already in SharedKernel)
- Application layer maps domain exceptions to HTTP problem details
- No swallowing exceptions; no empty catch blocks
- Dapr activity failures propagate to workflow for retry/compensation

### Testing

- Domain layer: pure unit tests, no mocks, no infrastructure — fast
- Application layer: unit tests with mocked repositories
- Infrastructure layer: integration tests with TestContainers (real MongoDB, real Redis)
- API layer: integration tests with `WebApplicationFactory`
- Minimum 80% coverage on Domain + Application layers (NFR502)
- Draw algorithm: property-based tests (FsCheck or similar) to verify fairness invariants

### Git Workflow

- `main` branch is always deployable
- Feature branches: `feature/<short-description>`
- Fix branches: `fix/<short-description>`
- PRs required for all changes to `main`
- One PR per logical unit of work — no "mega PRs"
- Commit messages: present tense, imperative (`add weighted lottery`, not `added` or `adding`)

---

## Open Questions

These need answers before the relevant phase begins:

| # | Question | Needed By | Owner |
|---|---|---|---|
| 1 | ~~Multi-tenancy isolation strategy?~~ → **Collection-per-tenant in service-owned MongoDB databases** | ✅ Decided | — |
| 2 | ~~Mobile platform: React Native or .NET MAUI?~~ → **React Native + Expo** | ✅ Decided | — |
| 3 | ~~Volume of booking requests per Draw?~~ → **max 500 per tenant per Draw** | ✅ Decided | — |
| 4 | ~~Hard cut-off time for request submission?~~ → **Configurable per tenant, default 18:00** | ✅ Decided | — |
| 5 | ~~Draw schedule — fixed or configurable?~~ → **Configurable per tenant via Dapr cron binding** | ✅ Decided | — |
| 6 | ~~Same-day slot requests outside the Draw?~~ → **Yes — immediate allocation, same 500 cap** | ✅ Decided | — |
| 7 | ~~Company car pre-allocations — manual or automatic?~~ → **Same auto-scheduler as all employees; always approved (Tier 1 in Draw)** | ✅ Decided | — |
| 8 | ~~GDPR deletion: pseudonymise or redact?~~ → **Pseudonymise — `actor_hash` in audit log, separate `PiiMapping` collection** | ✅ Decided | — |
| 9 | ~~Payment provider?~~ → **Stub (`IPaymentProvider`) — real provider wired in later** | ✅ Decided | — |
| 10 | ~~Real-time channel?~~ → **SSE via Notification service, bridged from Dapr pub/sub** | ✅ Decided | — |
| 11 | ~~What determines Tier 2 lottery weight?~~ → **`1 / (1 + RecentAllocationCount + ActivePenaltyScore)` from draw-time user metrics** | ✅ Decided | — |
| 12 | ~~What are the executable Draw rules?~~ → **See `docs/business-layer/allocation-rules.md`** | ✅ Decided | — |
| 13 | ~~When does late cancellation start?~~ → **After a slot has been allocated; before allocation no penalty applies** | ✅ Decided | — |
| 14 | ~~How is parking policy configured?~~ → **Tenant defaults with per-location overrides** | ✅ Decided | — |
| 15 | ~~Which notification channels are required for v1?~~ → **Both in-app and email for critical operational notifications** | ✅ Decided | — |
| 16 | ~~How should Booking be implemented?~~ → **Story-driven vertical slices, not layer-by-layer** | ✅ Decided | — |
