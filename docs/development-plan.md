# FPS Development Plan

## What We Are Building

The **Fair Parking System (FPS)** is a multi-tenant SaaS platform that replaces manual, email-based parking management with a transparent, algorithm-driven allocation system. The core idea is a daily **Draw process**: employees submit requests for the next day's slot; a weighted lottery runs nightly and assigns slots fairly, favouring those who park least often.

The system is not a simple CRUD app — the Draw is a long-running distributed process that locks slots, runs an allocation algorithm, fires notifications, and updates metrics. This makes it an ideal candidate for **Dapr Workflows**.

Booking implementation requirements are maintained under the business layer. The key product-owner handoff documents are [Booking Vertical Slices](business-layer/booking-vertical-slices), [Booking Event Contracts](business-layer/booking-event-contracts), [Booking Authorization](business-layer/booking-authorization), [Booking Reason Codes](business-layer/booking-reason-codes), [Booking API Contract](business-layer/booking-api-contract), and [Booking Context Contract](business-layer/booking-context-contract).

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
| Write side (CQRS) | **Dapr state store → MongoDB** | Aggregate persistence by ID; tenant isolated per MongoDB database |
| Read side (CQRS) | **MongoDB driver** | Projections, aggregation pipelines, reporting queries |
| Multi-tenancy | **Database-per-tenant** | Each tenant gets `fps_{tenant_id}` MongoDB database; resolved from request context |
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
| Frontend (mobile) | **React Native + Expo** | Managed workflow, no native build tooling, OTA updates |

---

## Discrepancies & Issues Found in Docs

These need resolution before or during development. Raised here so decisions can be logged in `versions-and-decisions.md`.

### 🔴 Critical

**1. Dapr version is 1.4.0 — Dapr Workflows did not exist then**
Dapr Workflows (the `dapr/go-sdk` workflow engine, `.NET SDK Dapr.Workflow`) were introduced in Dapr 1.10 and stabilised in 1.12. The system must target **Dapr 1.14+** to use workflows. All Dapr component configs will need updating.

**2. Multi-tenancy isolation strategy is undefined**
The docs describe a multi-tenant SaaS but never define isolation: database-per-tenant, schema-per-tenant, or row-level with tenant ID? This affects every service, every query, and the entire deployment model. A decision is needed before any data layer is built.

**3. Draw algorithm edge cases are unspecified**
- What happens when requests > slots? (documented: lottery) — but what if a user submits duplicate requests?
- What if a slot is cancelled by admin during an active draw?
- What is the cut-off time for next-day request submission?
- Is the draw atomic — does a partial failure roll back all allocations?

### 🟡 Important

**4. Mobile platform decision: React Native vs .NET MAUI**
Both are listed. They require different skill sets, build pipelines, and maintenance effort. Pick one.

**5. Event topology** ✅ *Resolved for Booking*
Booking event contracts are defined in `docs/business-layer/booking-event-contracts.md`. Other services may add their contracts slice-by-slice.

**6. Draw process volume not quantified**
NFRs specify 10,000 concurrent users and 100 TPS but never state how many booking requests a single Draw processes. This determines whether the workflow runs in seconds or minutes, and whether it needs partitioning.

**7. GDPR vs. audit log tension** ✅ *Resolved — pseudonymisation*
Audit records store `actor_hash` (SHA-256 of `user_id`). A separate `PiiMapping` collection holds the hash→identity link. On GDPR erasure: delete the mapping row. Audit log stays immutable and anonymous.

### 🟢 Minor / Future

**8. AI/ML allocation enhancement** — mentioned as future, no tech chosen. Skip for now.

**9. Gamification** — badges/leaderboards have no business case. Skip unless explicitly prioritised.

**10. Card reader / physical confirmation integration** — no vendor or protocol specified. Leave as a stub interface.

---

## Development Phases

### Phase 0 — Foundation (Week 1–2)

Goal: every developer can run the full stack locally with a single command.

- [ ] Set up `.NET Aspire` app host (`FPS.AppHost`)
- [ ] Configure Dapr sidecars per service in Aspire
- [ ] Dapr components: RabbitMQ pub/sub, MongoDB state store, Redis cache, Vault secrets
- [ ] Shared `docker-compose.yaml` for infrastructure (already exists, needs Dapr update)
- [ ] Finalise `FPS.SharedKernel` — ensure it compiles and is referenced by at least one service
- [ ] Establish `FPS.sln` (a single solution file referencing all services for cross-service navigation)
- [ ] CI: GitHub Actions workflow — build + test on every PR (per service or mono-workflow)
- [ ] ~~Decision log: resolve multi-tenancy strategy, mobile platform, Dapr version~~ ✅ All resolved

**Deliverable**: `docker compose up` starts all infra; `dotnet run` in AppHost starts all services.

---

### Phase 1 — Booking Domain (Week 3–6)

This is the core of FPS. Everything else is supporting.

Implementation should follow vertical slices from `docs/business-layer/booking-vertical-slices.md`. Do not complete the whole domain layer before application and API work; each story should cut through the layers and be independently testable.

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
- [x] Implement Booking story-by-story against `docs/business-layer/booking-vertical-slices.md`

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
> **Vertical slices**: Booking implementation order and story acceptance criteria are defined in `docs/business-layer/booking-vertical-slices.md`.
>
> **Authorization**: Booking role/action permissions are defined in `docs/business-layer/booking-authorization.md`.
>
> **API contract**: Booking response and error shapes are defined in `docs/business-layer/booking-api-contract.md`.

**Infrastructure layer** (`FPS.Booking.Infrastructure`):
- [x] Dapr state store client — save/load Booking request state and related Booking read-model data
- [x] Booking query repository — bookings by date, requests per user, draw inputs, allocation history
- [ ] Tenant context middleware — resolves `fps_{tenant_id}` MongoDB database per request
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

Booking Phase 1 is complete. New implementation work should now proceed through these focused slices. Each slice should be its own branch and PR, and implementation must stop when a business rule or cross-service contract is missing.

| Order | Slice | Goal | Depends on | Must not include |
| ---: | --- | --- | --- | --- |
| 1 | `ID001` Authenticated User Context | Resolve authenticated tenant, user, and roles from claims and expose `GET /me`. | Existing Identity service and Booking authorization docs. | Profile vehicle data, mobile UI, Keycloak deployment automation. |
| 2 | `BK011` Booking Uses Auth Context | Replace Booking API tenant/user parameters with authenticated context for employee-facing actions. | `ID001`. | New Booking business behavior. |
| 3 | `P001` Profile Vehicle Snapshot | Add Profile-owned vehicle and company-car eligibility data needed by Booking validation/allocation. | `ID001`; Booking context contract. | Booking allocation changes beyond consuming the snapshot. |
| 4 | `N001` Booking Notification Consumer | Consume Booking events and create idempotent in-app notification records. | Booking event contracts. | Email sending, push notifications, SSE, notification history API, mobile notification UI. |
| 5 | `A001` Booking Audit Consumer | Persist append-only audit records for Booking events with pseudonymised actors. | Booking event contracts; GDPR audit decision. | Audit query UI, GDPR erasure endpoint/workflow, PII mapping persistence beyond documented shape. |
| 6 | `CFG001` Parking Policy/Slot Source | Move default/in-memory Booking policy and slot inputs toward Configuration-owned contracts. | Booking context contract; parking policy configuration docs. | Tenant onboarding or admin UI. |
| 7 | `API001` OpenAPI Client Contract | Stabilise OpenAPI output and generated TypeScript client for web/mobile. | Authenticated API surface from `ID001` and `BK011`. | React or React Native implementation. |
| 8 | `MOB001` React Native App Shell | Scaffold React Native + Expo mobile client and generated API-client consumption. | `API001`. | Booking business rule changes. |

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
- [ ] Keycloak integration (OIDC provider, not reinventing auth)
- [ ] JWT validation middleware
- [ ] User roles: `employee`, `hr_manager`, `admin`, `auditor`, `accounting`
- [ ] Service-to-service auth via Dapr mTLS (Sentry) — no extra work needed
- [ ] `GET /me` — current user from token claims

**Profile** (`FPS.Profile`):
- [ ] `UserProfile` aggregate — name, vehicles, preferences, scheduler settings
- [ ] `Vehicle` entity — type (car/motorcycle/EV), plate
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

---

### Phase 3 — Notification & Audit (Week 9–10)

**Notification** (`FPS.Notification`):
- [ ] Subscribe to: `slot.allocated`, `booking.cancelled`, `draw.completed`, `penalty.applied`
- [ ] Email channel (SMTP or SendGrid)
- [ ] In-app notification store (read via API)
- [ ] V1 mandatory channels: in-app and email for critical operational notifications
- [ ] **SSE endpoint** `GET /notifications/stream` — clients connect, events from Dapr pub/sub are bridged to connected SSE clients; no Azure dependency, no extra infrastructure
- [ ] Idempotent — duplicate events must not send duplicate emails
- [ ] Implement notification behavior against `docs/business-layer/notification.md`

**Audit** (`FPS.Audit`):
- [ ] Subscribe to all domain events — record everything
- [ ] Immutable append-only store (never UPDATE or DELETE audit records)
- [ ] PII pseudonymisation:
  - Audit records store `actor_hash` (SHA-256 of `user_id`) — never name or email
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
- [ ] Mobile app (React Native + Expo) — employee self-service, booking, notifications
- [ ] Shared API client generated from OpenAPI specs (used by both web and mobile)
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
| 1 | ~~Multi-tenancy isolation strategy?~~ → **Database-per-tenant (`fps_{tenant_id}`)** | ✅ Decided | — |
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
