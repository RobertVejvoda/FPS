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
| 2. Platform integration foundation | Done | Connect Booking to identity, profile facts, events, audit, notification, configuration, reporting, and API contracts. | `ID001`, `BK011`, `P001`, `N001`, `A001`, `CFG001`, `API001`, `CI001`, `N002`-`N004`, `A002`, `CFG002`, `REPORT001`, `OPS000`. Future platform integration includes `CUST002`, `P002`, `ID002`, and `OPS005` for SSO-first customer integration. | Tenant/user identity is authenticated, supporting services consume Booking facts, Configuration and Reporting have first usable APIs, and production strategy is framed. | This turns Booking from an isolated backend into a platform that clients can evaluate. |
| 3. Mobile employee foundation | Done | Prove the employee self-service path on the primary user channel. | Expo shell, authenticated login, My Bookings, booking submission, cancel, and confirm usage. | Employee can log in, submit, view, cancel, and confirm bookings from mobile against generated API contracts. | Employee experience validates whether the backend workflow is usable, not only technically complete. |
| 4. Mobile product completion | Done | Complete the employee mobile experience enough for demo and pilot. | `MOB006` notifications, `MOB007` profile/vehicle details, `MOB008` employee-safe Draw/allocation detail, `MOB009` production polish. | Mobile handles notifications, profile facts, safe outcome visibility, session recovery, empty/loading/error states, accessibility, and environment configuration. | Demo and pilot need a coherent employee journey before admin/web work becomes meaningful. |
| 5. Web and admin surfaces | Ready | Give HR, facilities, tenant admins, and managers operational control. | `WEB001` employee web self-service, `WEB002` HR/admin dashboard, `WEB003` tenant admin console, `WEB004` reporting views. | Admin users can configure policy/slots, inspect operational state, and use reporting safely through web UI. | Backend admin/reporting APIs now exist; web can build on them after employee flow is stable. |
| 6. Operations and deployment | Ready | Make FPS credible outside local development. | `OPS001` Dapr component baseline, `OPS002` demo environment, `OPS003` client-owned production integration, `OPS004` observability/performance evidence. | Local/demo/client production profiles are documented and tested; metrics/logs/traces/export path exist; backup/restore and runbooks are credible. | Client evaluation depends on proof that FPS can run, be observed, and be handed over. |
| 7. Demo and client evaluation pack | Ready | Prepare material for business, architecture, security, and operator evaluation. | Product one-pager, role-based demo scripts, architecture overview, deployment/operations summary, security/GDPR summary, cost/hosting assumptions, commercialisation options note, FAQ. | A new evaluator can understand value, architecture, security posture, deployment model, and roadmap without reading every internal page. | Materials should be based on working product and operational proof, not promises. |
| 8. Commercialisation impact and Billing | Backlog | Decide how FPS can recover cost without weakening the free/open core. | `BILL000` impact review, free-core vs paid-add-on boundaries, support subscription shape, future dual-license posture, later `BILL001` billing workflow only after approval. | Commercial model is approved before implementation; billing behavior maps to real product decisions. | Billing too early would encode business assumptions before value, deployment, and support model are clear. |

## Milestone Plan

Milestones are delivery checkpoints across phases. Phases explain the product area and sequencing; milestones explain what we need to prove at a useful checkpoint.

| Milestone | Board issues | Goal | Exit criteria | Why this milestone exists |
| --- | --- | --- | --- | --- |
| `Demo v0` | `MOB006`, `OPS001`, `OPS002`, `CUST002`, `DOCS001` | Show a credible first demo path: employee notifications, deployment component strategy, low-cost demo plan, SSO-first integration contract, and client-facing material outline. | A new evaluator can understand and see the employee flow, how FPS would run for demo, and how company identity/data integration will work. | This is the first point where FPS becomes explainable to someone outside the implementation team. |
| `Employee Pilot` | `MOB007`, `MOB008`, `MOB009`, `N005` | Complete the employee-facing mobile experience enough for pilot use. | Mobile covers profile/vehicle facts, employee-safe allocation detail, notifications/preferences, session recovery, accessibility, and production polish. | User testing should happen only after the core employee journey is coherent, not while screens are still placeholders. |
| `Client Evaluation` | `ID002`, `P002`, `CFG003`, `CUST001`, `REPORT002`, `WEB001`-`WEB004` | Give business, HR/facilities, architecture, and reporting stakeholders enough product surface to evaluate FPS. | Admin/reporting workflows, web surfaces, user/profile mapping, customer onboarding, and configuration publication are credible enough for review. | Client evaluators need more than the employee mobile app; they need operating, reporting, and onboarding evidence. |
| `Production Handoff` | `A003`, `OPS003`, `OPS004`, `OPS005` | Prepare FPS for client-owned production operation. | Observability, performance evidence, audit retention/integrity, integration secret handling, and production responsibility split are documented and implemented where needed. | Production will run in the client's environment, so portability and operational evidence must be explicit. |
| `Commercialisation Later` | `BILL000`, `BILL001` | Decide how FPS can recover cost without weakening the free/open core. | Commercial model is approved before Billing behavior is implemented. | Billing and paid features should follow product proof, not lead it. |

## Current Priority

The current next product phase is **Client Evaluation**, with local test harness work continuing in parallel so the implemented employee journey can be exercised on real devices.

| Slice | Goal | Notes |
| --- | --- | --- |
| `OPS006` Local Test Harness | Coordinate local dependencies, Dapr sidecars, seeded data, gateway, and health/log visibility. | The employee mobile flow is implemented; reliable local startup and smoke evidence are now the practical blocker for repeatable device testing. |
| `P002` Profile Mapping And Minimal Facts | Implement SSO-derived profile mapping and the minimum policy facts needed by Booking. | CSV/file import is fallback/bootstrap only, not the primary company integration. |
| `ID002` User Provisioning Integration | Map IdP subjects, claims/groups, roles, local-account fallback, and deactivation behavior. | SSO/OIDC first; SCIM is optional lifecycle support where available. |
| `CUST001` Tenant Onboarding | Make evaluator/customer onboarding credible enough for client review. | Should follow the SSO-first integration contract and profile/identity mapping decisions. |

Operations work can run in parallel where it does not block client-evaluation feature work:

| Slice | Goal | Notes |
| --- | --- | --- |
| `OPS006` Local Test Harness | Add coordinated local startup and diagnostics on top of the completed auth, gateway, sidecar, and seed slices. | Practical next step for repeatable local API/mobile testing. |
| `OPS003` Client-Owned Production Integration | Document and implement the handoff model for client-owned production. | Should build on the existing local/demo/client-owned Dapr profile split. |
| `OPS004` Observability And Performance Evidence | Produce portable logs, metrics, traces, performance evidence, and runbooks. | Client production will replace local/demo tooling, so evidence must be portable. |

Before client onboarding or realistic user testing, prepare customer-data integration implementation:

| Slice | Goal | Notes |
| --- | --- | --- |
| `P002` Profile Mapping And Minimal Facts | Implement SSO-derived profile mapping and the minimum policy facts needed by Booking. | CSV/file import is fallback/bootstrap only, not the primary company integration. |
| `ID002` User Provisioning Integration | Map IdP subjects, claims/groups, roles, local-account fallback, and deactivation behavior. | SSO/OIDC first; SCIM is optional lifecycle support where available. |
| `OPS005` Integration Secrets And Observability | Define integration credentials, credential-verifier handling, audit, metrics, retries, and error evidence. | Required before customer-owned production integrations. |

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
