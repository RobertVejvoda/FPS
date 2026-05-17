# Roadmap

This roadmap explains the phase and slice cards used in the [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2). Phase cards show high-level delivery direction; issue cards show concrete implementation slices.

The detailed evidence remains in the [Implementation Tracker](./implementation-tracker). Requirement coverage remains in [Requirements Traceability](./requirements-traceability). Operational board rules are defined in [Delivery Board](./delivery-board).

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
| 4. Mobile product completion | Ready | Complete the employee mobile experience enough for demo and pilot. | `MOB006` notifications, `MOB007` profile/vehicle details, `MOB008` employee-safe Draw/allocation detail, `MOB009` production polish. | Mobile handles notifications, profile facts, safe outcome visibility, session recovery, empty/loading/error states, accessibility, and environment configuration. | Demo and pilot need a coherent employee journey before admin/web work becomes meaningful. |
| 5. Web and admin surfaces | Ready | Give HR, facilities, tenant admins, and managers operational control. | `WEB001` employee web self-service, `WEB002` HR/admin dashboard, `WEB003` tenant admin console, `WEB004` reporting views. | Admin users can configure policy/slots, inspect operational state, and use reporting safely through web UI. | Backend admin/reporting APIs now exist; web can build on them after employee flow is stable. |
| 6. Operations and deployment | Ready | Make FPS credible outside local development. | `OPS001` Dapr component baseline, `OPS002` demo environment, `OPS003` client-owned production integration, `OPS004` observability/performance evidence. | Local/demo/client production profiles are documented and tested; metrics/logs/traces/export path exist; backup/restore and runbooks are credible. | Client evaluation depends on proof that FPS can run, be observed, and be handed over. |
| 7. Demo and client evaluation pack | Ready | Prepare material for business, architecture, security, and operator evaluation. | Product one-pager, role-based demo scripts, architecture overview, deployment/operations summary, security/GDPR summary, cost/hosting assumptions, commercialisation options note, FAQ. | A new evaluator can understand value, architecture, security posture, deployment model, and roadmap without reading every internal page. | Materials should be based on working product and operational proof, not promises. |
| 8. Commercialisation impact and Billing | Backlog | Decide how FPS can recover cost without weakening the free/open core. | `BILL000` impact review, free-core vs paid-add-on boundaries, support subscription shape, future dual-license posture, later `BILL001` billing workflow only after approval. | Commercial model is approved before implementation; billing behavior maps to real product decisions. | Billing too early would encode business assumptions before value, deployment, and support model are clear. |

## Current Priority

The current next product phase is **Phase 4: Mobile product completion**. It should be split into small implementation slices:

| Slice | Goal | Notes |
| --- | --- | --- |
| `MOB006` Mobile Notifications | Show notification list, unread count, mark-read, and SSE or polling fallback. | Uses `N002` APIs and should keep failure states visible. |
| `MOB007` Mobile Profile And Vehicle Details | Show employee-visible profile, vehicle, company-car, and accessibility facts. | Editing needs business-rule confirmation before implementation. |
| `MOB008` Mobile Draw Status And Allocation Detail | Show employee-safe Draw/allocation status and reasons. | Do not expose hidden lottery internals or other employees. |
| `MOB009` Mobile Production Polish | Session expiry, refresh recovery, environment config, loading/empty/error states, accessibility, and pilot QA. | Should prepare mobile for demo/pilot use. |

Operations work can run in parallel where it does not block mobile:

| Slice | Goal | Notes |
| --- | --- | --- |
| `OPS001` Pluggable Dapr Component Baseline | Define local/demo/client component profiles for pub/sub, state, secrets, bindings, identity, and observability. | Needed before demo environment implementation. |
| `OPS002` Demo Environment Baseline | Stand up or document a low-cost demo path. | Should prove product behavior without assuming final client production. |

Before client onboarding or realistic user testing, prepare customer-data integration planning:

| Slice | Goal | Notes |
| --- | --- | --- |
| `CUST002` SSO-First Customer Integration Contract | Define SSO/OIDC mapping, minimal employee/profile data, local-account fallback, classification, and audit behavior. | Must clarify that company users authenticate through the customer IdP and FPS does not store company passwords. |
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

Use the `Phase` field for broad grouping. Use `Milestone` for release checkpoints, `Priority` for steering, and `Status` for operational state. Use GitHub issues as the canonical slice cards; PRs should link back to the issue they implement. Draft cards are acceptable only for phase markers or temporary planning notes that are not yet a slice.

Agent workflow:

| Actor | Board usage |
| --- | --- |
| Codex | Creates or updates slice issues, sets `Phase` and `Status`, keeps the [Implementation Tracker](./implementation-tracker) aligned, and routes ready issues. |
| Claude | Looks for `Status = Ready` issue cards with `claude-ready` or a direct handoff comment. If a Ready issue lacks enough implementation detail, Claude should comment with the blocker instead of guessing. |
| Copilot | Works only on issues explicitly labeled `copilot` and assigned to Copilot. Mechanical slices should still have clear expected files and acceptance criteria. |

When a phase or slice changes status, update both the board and the [Implementation Tracker](./implementation-tracker) if the change affects delivery evidence. New slices should naturally appear on the board by creating a GitHub issue, adding it to `FPS Delivery Kanban`, setting the `Phase`, and setting `Status` to `Backlog`, `Ready`, `In progress`, `In review`, or `Done`.
