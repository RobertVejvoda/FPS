# Roadmap

This roadmap explains the high-level delivery direction for FPS. Phases describe product and operational capability areas; issue cards in GitHub carry the detailed implementation work.

Detailed implementation evidence, requirement traceability, and board operating rules belong in the [GitHub Wiki](https://github.com/RobertVejvoda/FPS/wiki) and GitHub issues. This page stays focused on business-readable sequence and milestones.

## Roadmap Principles

| Principle | Meaning |
| --- | --- |
| Prove the core first | Parking allocation fairness is the product center. Supporting services matter only because they make that decision usable, auditable, and trusted. |
| Keep slices vertical | Each implementation slice should deliver a visible capability or operational proof, not only an isolated layer. |
| Finish the employee path before broadening UI | Mobile employee self-service proves the API and user workflow before web/admin surfaces expand the product. |
| Make operations pluggable | Dapr and OpenTelemetry boundaries must be proven before client-owned production can be credible. |
| Keep commercialisation after product proof | Billing and paid features are intentionally late until the free/open core and client value are clear. |

## Phase Plan

| Phase | Kanban Status | Goal | What Is Inside | Exit Criteria | Why This Order |
| --- | --- | --- | --- | --- | --- |
| 0. Foundation and repository setup | Done | Make the repo buildable, navigable, and safe for multi-agent work. | Monorepo structure, .NET baseline, docs structure, CI visibility, generated-client tooling, agent cooperation rules, tracker and traceability foundations. | New work can be reviewed through PRs, validated by CI/tooling, and tracked in docs. | Without this, later implementation creates invisible drift and poor handoffs. |
| 1. Booking core B001-B010 | Done | Prove the parking fairness engine and lifecycle. | Submit requests, same-day booking, cancel pending, scheduled Draw, allocated cancellation/reallocation, confirm usage, no-show, view bookings, view Draw status, manual correction. | Booking rules are implemented and merged with tests and documented behavior. | Booking is the product center; other services depend on its events and state. |
| 2. Platform integration foundation | Done | Connect Booking to identity, profile facts, events, audit, notification, configuration, reporting, and API contracts. | `ID001`, `BK011`, `P001`, `N001`, `A001`, `CFG001`, `API001`, `CI001`, `N002`-`N004`, `A002`, `CFG002`, `REPORT001`, `OPS000`, `CUST002`, `P002`, `ID002`, `OPS005`. | Tenant/user identity is authenticated, supporting services consume Booking facts, Configuration and Reporting have first usable APIs, and SSO-first customer integration is framed. | This turns Booking from an isolated backend into a platform that clients can evaluate. |
| 3. Mobile employee foundation | Done | Prove the employee self-service path on the primary user channel. | Expo shell, authenticated login, My Bookings, booking submission, cancel, and confirm usage. | Employee can log in, submit, view, cancel, and confirm bookings from mobile against generated API contracts. | Employee experience validates whether the backend workflow is usable, not only technically complete. |
| 4. Mobile product completion | Done | Complete the employee mobile experience enough for demo and pilot. | `MOB006` notifications, `MOB007` profile/vehicle details, `MOB008` employee-safe Draw/allocation detail, `MOB009` production polish. | Mobile handles notifications, profile facts, safe outcome visibility, session recovery, empty/loading/error states, accessibility, and environment configuration. | Demo and pilot need a coherent employee journey before admin/web work becomes meaningful. |
| 5. Web and admin surfaces | In progress | Give HR, facilities, tenant admins, and managers operational control. | `WEB001` employee web self-service, `WEB002` HR/admin dashboard, `WEB003` tenant admin console, `WEB004` reporting views, `WEB005`-`WEB008` supporting profile, notification, reporting, configuration, and audit surfaces. | Employee, HR/admin, reporting, configuration, and audit web surfaces exist; broader tenant administration remains in `WEB003`. | Backend admin/reporting APIs now exist; remaining web value is tenant onboarding and administration clarity. |
| 6. Operations and deployment | Done for current evaluation baseline | Make FPS credible outside local development. | `OPS001` Dapr component baseline, `OPS002` demo environment, `OPS003` client-owned production integration, `OPS004` observability/performance evidence, `OPS005` integration secrets, `OPS006` local test harness. | Local/demo/client production profiles are documented and tested; local harness, metrics/logs/traces/export path, backup/restore, secret-handling, and runbook evidence exist for evaluation. | Client evaluation depends on proof that FPS can run, be observed, and be handed over. |
| 7. Demo and client evaluation pack | In progress | Prepare material for business, architecture, security, and operator evaluation. | Product one-pager, role-based demo scripts, architecture overview, deployment/operations summary, security/GDPR summary, cost/hosting assumptions, commercialisation options note, FAQ, and pilot story cleanup. | A new evaluator can understand value, architecture, security posture, deployment model, roadmap, and pilot story without reading every internal page. | Materials should be based on working product and operational proof, not promises. |
| 8. Commercialisation impact and Billing | Backlog | Decide how FPS can recover cost without weakening the free/open core. | `BILL000` impact review, free-core vs paid-add-on boundaries, support subscription shape, future dual-license posture, later `BILL001` billing workflow only after approval. | Commercial model is approved before implementation; billing behavior maps to real product decisions. | Billing too early would encode business assumptions before value, deployment, and support model are clear. |

## Milestone Plan

Milestones are delivery checkpoints across phases. Phases explain the product area and sequencing; milestones explain what we need to prove at a useful checkpoint.

| Milestone | Board issues | Goal | Exit criteria | Why this milestone exists |
| --- | --- | --- | --- | --- |
| `Demo v0` | `MOB006`, `OPS001`, `OPS002`, `CUST002`, `DOCS001` | Show a credible first demo path: employee notifications, deployment component strategy, low-cost demo plan, SSO-first integration contract, and client-facing material outline. | A new evaluator can understand and see the employee flow, how FPS would run for demo, and how company identity/data integration will work. | This is the first point where FPS becomes explainable to someone outside the implementation team. |
| `Employee Pilot` | `MOB007`, `MOB008`, `MOB009`, `N005` | Complete the employee-facing mobile experience enough for pilot use. | Mobile covers profile/vehicle facts, employee-safe allocation detail, notifications/preferences, session recovery, accessibility, and production polish. | User testing should happen only after the core employee journey is coherent, not while screens are still placeholders. |
| `Client Evaluation` | `ID002`, `P002`, `CFG003`, `CUST001`, `CUST003`-`CUST007`, `REPORT003`, `WEB001`-`WEB004` | Give business, HR/facilities, architecture, and reporting stakeholders enough product surface to evaluate FPS. | Admin/reporting workflows, web surfaces, user/profile mapping, customer onboarding, fixed operational reports, configuration publication, and readiness checks are credible enough for review. | Client evaluators need more than the employee mobile app; they need operating, reporting, and onboarding evidence. |
| `Production Handoff` | `A003`, `OPS003`, `OPS004`, `OPS005`, `OPS006` | Prepare FPS for client-owned production operation and repeatable local testing. | Observability, performance evidence, audit retention/integrity, integration secret handling, local harness, and production responsibility split are documented and implemented where needed. | Production will run in the client's environment, so portability and operational evidence must be explicit. |
| `Commercialisation Later` | `BILL000`, `BILL001` | Decide how FPS can recover cost without weakening the free/open core. | Commercial model is approved before Billing behavior is implemented. | Billing and paid features should follow product proof, not lead it. |

## Current Priority

The current next product phase is **Pilot Story and Client Evaluation**. Most platform, customer onboarding, reporting, operations, and employee surfaces are now implemented; the next work should make the story easy to test, explain, and trust.

| Slice | Goal | Notes |
| --- | --- | --- |
| `CUST007` Tenant Readiness Check | Finish review and merge readiness validation. | PR #216 is the active Codex review item for issue #205. |
| `WEB003` Tenant Admin Console | Close the last major tenant-admin product surface. | This is the remaining web/admin gap after WEB001, WEB002, and WEB004-WEB008. |
| Pilot story cleanup | Explain the open-source, privacy-first, fair-allocation story for small companies under about 150 employees. | Emphasize company-owned deployment/data, anonymous or pseudonymous records, and parking-first value before paid product mechanics. |
| Test scenarios and evidence | Turn local harness, demo seed, and readiness checks into a small repeatable test plan. | Use real-device/mobile smoke paths and business-readable evidence, not only service health. |
| `BILL000` Commercialisation Impact Review | Reframe billing around support/service value after pilot proof. | Billing implementation (`BILL001`) should stay deferred until the story, pilot scope, and support model are clearer. |

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

When a phase or slice changes status, update the board and maintainer evidence in the GitHub Wiki if the change affects delivery evidence. New slices should naturally appear on the board by creating a GitHub issue, adding it to `FPS Delivery Kanban`, setting the `Phase`, and setting `Status` to `Backlog`, `Ready`, `In progress`, `In review`, or `Done`.
