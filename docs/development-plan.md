# FPS Development Plan

## What We Are Building

The **Fair Parking System (FPS)** is a multi-tenant SaaS platform that replaces manual, email-based parking management with a transparent, algorithm-driven allocation system. The core idea is a daily **Draw process**: employees submit requests for the next day's slot; a weighted lottery runs nightly and assigns slots fairly, favouring those who park least often.

The system is not a simple CRUD app — the Draw is a long-running distributed process that locks slots, runs an allocation algorithm, fires notifications, and updates metrics. This makes it an ideal candidate for **Dapr Workflows**.

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

**Domain layer** (`FPS.Booking.Domain`):
- [ ] `BookingRequest` aggregate — submit, cancel, expire
  - Must follow `docs/business-layer/booking-request-lifecycle.md`
- [ ] `SlotAllocation` aggregate — allocate, confirm, release
- [ ] `ParkingSlot` value object (or entity depending on ownership)
- [ ] `TimeSlot` value object (date + half/full day)
- [ ] `BookingStatus`, `VehicleType`, `UserId` value objects
- [ ] `ParkingAllocationService` — two-tier allocation:
  - **Tier 1**: requests from users with `HasCompanyCar = true` are guaranteed — allocated first, no lottery, no penalty ever
  - **Tier 2**: weighted lottery on remaining slots: `weight = 1 / (1 + RecentAllocationCount + ActivePenaltyScore)`
    - `RecentAllocationCount`: successful non-company-car allocations in the tenant's configured lookback window; same-day allocations count
    - Lookback window is tenant-configurable and defaults to `10` days
    - `ActivePenaltyScore`: active penalty points from late cancellations, no-shows, policy violations, or manual HR adjustments
    - Rejected requests are not part of the default denominator; if FPS later rewards repeated unlucky requestors, it should be modelled as a separate positive factor
    - If company-car requests exceed available matching capacity, reject overflow requests for now
- [ ] `HasCompanyCar` flag on `UserProfile` aggregate — set by admin, read by allocation service
- [ ] Domain events: `BookingSubmitted`, `BookingCancelled`, `DrawStarted`, `SlotAllocated`, `DrawCompleted`
- [ ] Unit tests for allocation service (pure domain, no infrastructure)
- [ ] Implement Draw behavior against `docs/business-layer/allocation-rules.md`
- [ ] Implement request lifecycle behavior against `docs/business-layer/booking-request-lifecycle.md`
- [ ] Resolve allocation policy from `docs/business-layer/parking-policy-configuration.md`
- [ ] Implement Booking story-by-story against `docs/business-layer/booking-vertical-slices.md`

**Application layer** (`FPS.Booking.Application`):
- [ ] Commands: `SubmitBookingRequest`, `CancelBooking`, `TriggerDraw`, `ConfirmSlotUsage`
- [ ] Queries: `GetMyBookings`, `GetAllocationResult`, `GetDrawStatus`
- [ ] `IBookingRepository`, `ISlotAllocationRepository` interfaces

**Dapr Workflow — Draw Process** (`BookingWorkflow`):

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

**Infrastructure layer** (`FPS.Booking.Infrastructure`):
- [ ] Dapr state store client — save/load `BookingRequest` and `SlotAllocation` aggregates by ID (write side)
- [ ] MongoDB driver — query read models: bookings by date, requests per user, allocation history (read side)
- [ ] Tenant context middleware — resolves `fps_{tenant_id}` MongoDB database per request
- [ ] Dapr pub/sub publisher for domain events
- [ ] Dapr pub/sub subscriber for `configuration.slotsUpdated`
- [ ] Remove: EF Core, SQL Server, `BookingDbContext` (already exists in code — dead code to delete)

**API layer** (`FPS.Booking.API`):
- [ ] `POST /bookings` — submit request (future date → queued for Draw; today → immediate allocation if slot available); same 500-cap applies to both paths
- [ ] `DELETE /bookings/{id}` — cancel
- [ ] `GET /bookings` — my bookings
- [ ] `POST /draws/trigger` — admin-only, trigger draw manually
- [ ] `GET /draws/{date}/status` — draw status

**Tests**:
- Unit: Domain (allocation algorithm, aggregate invariants)
- Integration: Dapr state store and MongoDB queries against real MongoDB (TestContainers)
- API: Controller tests with mocked application layer

---

### Phase 2 — Identity & Profile (Week 7–8)

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
