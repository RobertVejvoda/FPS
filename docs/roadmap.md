# Roadmap

This roadmap explains the high-level delivery direction for FairSpot. Phases describe product and operational capability areas. This page stays focused on business-readable sequence and milestones.

## Roadmap Principles

| Principle | Meaning |
| --- | --- |
| Prove the core first | Fair allocation of scarce shared resources is the product center. Parking is the first proof vertical; supporting services matter because they make allocation usable, auditable, and trusted. |
| Keep slices vertical | Each implementation slice should deliver a visible capability or operational proof, not only an isolated layer. |
| Finish the employee path before broadening UI | Mobile employee self-service proves the API and user workflow before web/admin surfaces expand the product. |
| Make operations pluggable | Dapr and OpenTelemetry boundaries must be proven before client-owned production can be credible. |
| Keep commercialisation after product proof | Billing and paid features are intentionally late until the free/open core and client value are clear. |

## Phase Plan

| Phase | Kanban Status | Goal | What Is Inside | Exit Criteria | Why This Order |
| --- | --- | --- | --- | --- | --- |
| 0. Foundation and repository setup | Done | Make the repo buildable, navigable, and safe for multi-agent work. | Monorepo structure, .NET baseline, docs structure, CI visibility, generated-client tooling, agent cooperation rules, tracker and traceability foundations. | New work can be reviewed through PRs, validated by CI/tooling, and tracked in docs. | Without this, later implementation creates invisible drift and poor handoffs. |
| 1. Booking core B001-B010 | Done | Prove the first parking fairness engine and lifecycle. | Submit requests, same-day booking, cancel pending, scheduled Draw, allocated cancellation/reallocation, confirm usage, no-show, view bookings, view Draw status, manual correction. | Booking rules are implemented and merged with tests and documented behavior. | Booking and fair allocation are the product center; other services depend on their events and state. |
| 2. Platform integration foundation | Done | Connect Booking to identity, profile facts, events, audit, notification, configuration, reporting, and API contracts. | `ID001`, `BK011`, `P001`, `N001`, `A001`, `CFG001`, `API001`, `CI001`, `N002`-`N004`, `A002`, `CFG002`, `REPORT001`, `OPS000`, `CUST002`, `P002`, `ID002`, `OPS005`. | Tenant/user identity is authenticated, supporting services consume Booking facts, Configuration and Reporting have first usable APIs, and SSO-first customer integration is framed. | This turns Booking from an isolated backend into a platform that clients can evaluate. |
| 3. Mobile employee foundation | Done | Prove the employee self-service path on the primary user channel. | Expo shell, authenticated login, My Bookings, booking submission, cancel, and confirm usage. | Employee can log in, submit, view, cancel, and confirm bookings from mobile against generated API contracts. | Employee experience validates whether the backend workflow is usable, not only technically complete. |
| 4. Mobile product completion | Done | Complete the employee mobile experience enough for demo and pilot. | `MOB006` notifications, `MOB007` profile/vehicle details, `MOB008` employee-safe Draw/allocation detail, `MOB009` production polish. | Mobile handles notifications, profile facts, safe outcome visibility, session recovery, empty/loading/error states, accessibility, and environment configuration. | Demo and pilot need a coherent employee journey before admin/web work becomes meaningful. |
| 5. Web and admin surfaces | In progress | Give HR, facilities, tenant admins, and managers operational control. | `WEB001` employee web self-service, `WEB002` HR/admin dashboard, `WEB003` tenant admin console, `WEB004` reporting views, `WEB005`-`WEB008` supporting profile, notification, reporting, configuration, and audit surfaces. | Employee, HR/admin, reporting, configuration, and audit web surfaces exist; broader tenant administration remains in `WEB003`. | Backend admin/reporting APIs now exist; remaining web value is tenant onboarding and administration clarity. |
| 6. Operations and deployment | Done for current evaluation baseline | Make FairSpot credible outside local development. | `OPS001` Dapr component baseline, `OPS002` demo environment, `OPS003` client-owned production integration, `OPS004` observability/performance evidence, `OPS005` integration secrets, `OPS006` local test harness. | Local/demo/client production profiles are documented and tested; local harness, metrics/logs/traces/export path, backup/restore, secret-handling, and runbook evidence exist for evaluation. | Client evaluation depends on proof that FairSpot can run, be observed, and be handed over. |
| 7. Demo and client evaluation pack | In progress | Prepare material for business, architecture, security, and operator evaluation. | Product one-pager, role-based demo scripts, architecture overview, deployment/operations summary, security/GDPR summary, cost/hosting assumptions, commercialisation options note, FAQ, and pilot story cleanup. | A new evaluator can understand value, architecture, security posture, deployment model, roadmap, and pilot story without reading every internal page. | Materials should be based on working product and operational proof, not promises. |
| 8. Commercialisation impact and Billing | Done for impact review; Billing deferred | Decide how FairSpot can recover cost without weakening the free/open core. | `BILL000` impact review, free-core vs paid-add-on boundaries, support subscription shape, future dual-license posture, later `BILL001` billing workflow only after approval. | Commercial posture is documented and Billing behavior remains deferred until a concrete offer is approved. | Billing too early would encode business assumptions before value, deployment, and support model are clear. |

## Milestone Plan

Milestones are delivery checkpoints across phases. Phases explain the product area and sequencing; milestones explain what we need to prove at a useful checkpoint.

| Milestone | Board issues | Goal | Exit criteria | Why this milestone exists |
| --- | --- | --- | --- | --- |
| `Demo v0` | `MOB006`, `OPS001`, `OPS002`, `CUST002`, `DOCS001` | Show a credible first demo path: employee notifications, deployment component strategy, low-cost demo plan, SSO-first integration contract, and client-facing material outline. | A new evaluator can understand and see the employee flow, how FairSpot would run for demo, and how company identity/data integration will work. | This is the first point where FairSpot becomes explainable to someone outside the implementation team. |
| `Employee Pilot` | `MOB007`, `MOB008`, `MOB009`, `N005` | Complete the employee-facing mobile experience enough for pilot use. | Mobile covers profile/vehicle facts, employee-safe allocation detail, notifications/preferences, session recovery, accessibility, and production polish. | User testing should happen only after the core employee journey is coherent, not while screens are still placeholders. |
| `Client Evaluation` | `ID002`, `P002`, `CFG003`, `CUST001`, `CUST003`-`CUST007`, `REPORT003`, `WEB001`-`WEB004` | Give business, HR/facilities, architecture, and reporting stakeholders enough product surface to evaluate FairSpot. | Admin/reporting workflows, web surfaces, user/profile mapping, customer onboarding, fixed operational reports, configuration publication, and readiness checks are credible enough for review. | Client evaluators need more than the employee mobile app; they need operating, reporting, and onboarding evidence. |
| `Production Handoff` | `A003`, `OPS003`, `OPS004`, `OPS005`, `OPS006` | Prepare FairSpot for client-owned production operation and repeatable local testing. | Observability, performance evidence, audit retention/integrity, integration secret handling, local harness, and production responsibility split are documented and implemented where needed. | Production will run in the client's environment, so portability and operational evidence must be explicit. |
| `Commercialisation Later` | `BILL000`, `BILL001` | Decide how FairSpot can recover cost without weakening the free/open core. | `BILL000` documents the commercial posture; `BILL001` remains deferred until a real Billing workflow is approved. | Billing and paid features follow product proof, not lead it. |
| `Resource Map and Preferences` | Future `MAP`/`PREF` slices | Prove FairSpot can allocate within uploaded maps and prefer employee/team zones before fallback. | Tenant admins can publish a resource map with zones; employees can express a preferred zone; allocation records assigned zone and fallback reason when preference cannot be met. | This makes the broader FairSpot story concrete for seats, sport courts, desks, lockers, chargers, and other limited workplace resources beyond the first parking module. |

## Release Validation Model

Releases are validation checkpoints cut from `master`. They are not a replacement for product milestones. A milestone says what capability area we are proving; a release branch says what exact code and docs are being tested.

| Release | Branch | Related Milestone | Purpose | Fix Rule | Exit Criteria |
| --- | --- | --- | --- | --- | --- |
| `Release 1` | `release/1` | Client Evaluation / Production Handoff preparation | Validate the current customer-ready hosted evaluation baseline after merged architecture, DataHub, mobile schedule visibility, Dapr hardening, and operations docs work. | Test fixes branch from `release/1`, merge back into `release/1`, then merge `release/1` back to `master` when accepted. No unrelated feature expansion. | Local and hosted smoke scenarios pass or have accepted residual risk; release notes list known gaps; readiness status is updated; `release/1` is merged back to `master` and tagged. |
| `Release 1.x` | Normal feature branch or short-lived `release/1.x` branch | Client Evaluation follow-up | Contain small fixes or evidence updates discovered after Release 1 validation. | Keep fixes issue-backed and small. Larger capability changes return to normal milestone planning. | Fix PRs are merged to `master`; release notes are updated if user-visible. |

Release branches should be short-lived. If validation discovers a new requirement rather than a defect, create a normal issue and route it through the delivery board instead of expanding the release branch silently.

### Release 1 Scope

Release 1 should validate what is already merged, not wait for every planned customer-ready feature. The goal is to learn whether the current baseline can be demonstrated and hosted safely enough for evaluation.

| Area | In Release 1 Validation | Not Required For Release 1 |
| --- | --- | --- |
| Employee mobile | Login/session, My Spots, request submission, cancellation/confirmation where implemented, vehicle/default behavior, and Draw schedule visibility. | Full app store distribution approval. |
| Booking and Draw | Request lifecycle, same-day behavior, manual/scheduled Draw behavior where implemented, employee-safe Draw status, and schedule metadata for the current parking vertical. | Full resource-map allocation across every future resource type. |
| Data and reads | Service-owned state evidence and first event-fed projection/inbox behavior already merged. | Complete replacement of every reporting/read path. |
| Hosted operations | NAS/Cloudflare/WAF/auth/runbook evidence and smoke/reset scenarios sufficient for evaluation. | Client-owned production handoff completeness. |
| Architecture and docs | Architecture Repository alignment, roadmap/readiness/work-package traceability, release notes, and known gaps. | Final ArchiMate diagram refresh for every viewpoint. |
| Commercialisation | Free-core/deferred Billing decision remains documented. | Billing implementation. |

## Current Priority

The current next product phase is **Customer-Ready Hosted Evaluation**. The product story is coherent enough to explain, but the hosted pilot is not ready for real customer data until persistence, DataHub, role-specific UI, and hosted operations evidence are closed.

In TOGAF terms, the roadmap is the business-readable companion to [Transition Architectures](./architecture/architecture-states/transition-architectures). This page explains the sequence; the architecture state pages own gaps, work package groups, and validation gates.

| Slice | Goal | TOGAF Placement | Notes |
| --- | --- | --- | --- |
| Customer durable tenant state | Make tenant onboarding/readiness state survive restart and hosted deployment changes. | Phase C/E/F | Closes `GAP-001`; implementation issue #317. |
| DataHub first projections | Build event-fed read models for customer-facing reports, HR/admin views, and readiness summaries. | Phase C/E/F | Starts with #332, then #335/#334. |
| Hosted public-domain evidence | Prove NAS/Cloudflare/WAF/auth/smoke/reset behavior before real customer data. | Phase D/G | Issues #316, #315, #314, and Dapr hardening #378. |
| Role-centered UX validation | Validate Employee, HR/facility, tenant admin, system admin, auditor, and sponsor default views. | Phase B/C/G | Draw schedule #340 is merged; Draw progress #339, HR operations #310, and UX follow-ups remain. |
| Contract evidence consolidation | Make API/event/generated client evidence discoverable from Information Systems Architecture. | Phase C/G | Issue #377. |
| Diagram refresh | Produce Robert-approved target views for capability/value stream, application cooperation, DataHub, deployment, workflow, trust boundary, privacy/audit, and transition roadmap. | Cross-ADM / Architecture Definition | Needed before architecture baseline. |
| `BILL001` deferred decision | Keep Billing out of implementation until a concrete commercial offer is approved. | Phase H / Commercial decision | Source of truth: [Commercialisation Impact Review](./strategy-layer/commercialisation). |

## Board Usage

Use [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2) for visibility:

| Status | Meaning |
| --- | --- |
| Backlog | Future idea or phase not ready for detailed slicing. |
| Ready | Todo work with enough context to prepare concrete issues/PRs. |
| In progress | Actively being implemented or prepared. |
| In review | PR or review queue is active. |
| Done | Merged, closed, or otherwise completed. |

Use the `Phase` field for broad grouping and filtering on real issue cards. Use `Milestone` for release checkpoints, `Priority` for steering, and `Status` for operational state. Use GitHub issues as the canonical slice cards; PRs should link back to the issue they implement. Phase draft cards are optional roadmap markers only and do not contain or own work.

Agent workflow:

| Actor | Board usage |
| --- | --- |
| Codex | Creates or updates slice issues, keeps roadmap evidence aligned, and assigns the next actor. |
| Claude | Uses assignment plus `Status = Ready` or a direct handoff comment as the durable signal. If a Ready issue lacks enough implementation detail, Claude should comment with the blocker instead of guessing. |
| Copilot | Works only on issues assigned to Copilot. Mechanical slices should still have clear expected files and acceptance criteria. |

When a phase or slice changes status, update the board if the change affects delivery evidence. New slices should naturally appear on the board by creating a GitHub issue, adding it to `FPS Delivery Kanban`, setting the `Phase`, and setting `Status` to `Backlog`, `Ready`, `In progress`, `In review`, or `Done`.
