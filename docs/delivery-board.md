# Delivery Board

The [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2) is the operational view of delivery. It complements the [Roadmap](./roadmap), [Implementation Tracker](./implementation-tracker), and [Requirements Traceability](./requirements-traceability).

## Board Purpose

The board should answer four questions without reading the whole repository:

| Question | Board signal |
| --- | --- |
| Where are we heading? | Milestone and Roadmap phase. |
| What matters next? | Priority. |
| Who can act now? | Status, labels, and assignee. |
| What evidence proves progress? | Linked issue, linked PR, tracker row, and validation notes. |

## Field Meaning

| Field | Meaning | Rule |
| --- | --- | --- |
| Milestone | Delivery checkpoint such as `Demo v0`, `Employee Pilot`, `Client Evaluation`, `Production Handoff`, or `Commercialisation Later`. | Every open implementation slice should have one. |
| Phase | Product or architecture area such as Mobile, Operations, Platform, Web, Demo, or Commercialisation. | Keep phases because they explain ownership and sequencing; do not use them as the main working queue. |
| Priority | Steering signal: `P0`, `P1`, or `P2`. | `P0` is current critical path, `P1` is near follow-up, `P2` is later backlog. |
| Status | Operational state: Backlog, Ready, In progress, In review, or Done. | Agents should act only on issue cards in a state that matches their role. |
| Labels | Routing and ownership signal. | Use `claude-ready`, `copilot`, `needs-codex-review`, `blocked-question`, and attribution labels consistently. |

## Milestones

| Milestone | Purpose | Typical contents |
| --- | --- | --- |
| `Demo v0` | Working demo checkpoint for internal walkthroughs. | First complete employee demo path, minimum deployment/demo evidence, SSO-first integration contract. |
| `Employee Pilot` | Employee-facing pilot checkpoint. | Mobile profile/status visibility, notifications, production polish, notification preferences where needed. |
| `Client Evaluation` | Material and capabilities for client evaluation. | Admin/reporting story, customer onboarding, profile/user mapping, evaluator-facing documentation. |
| `Production Handoff` | Client-owned production readiness. | Dapr component profiles, observability, backups, restore, secrets, integration operations, production responsibilities. |
| `Commercialisation Later` | Future monetisation work. | Free-core boundaries, support model, paid add-ons, billing workflow after approval. |

## Readiness Checklist

An issue can move to `Ready` only when it has:

- a stable slice ID in the title or tracker row;
- a milestone, phase, priority, and status;
- source-of-truth docs or explicit issue-local requirements;
- scope and out-of-scope notes;
- acceptance criteria or a clear review target;
- routing decision: Codex/spec, Claude, Copilot, or human;
- validation expected from the implementer.

If any of these are missing, keep the issue in `Backlog` or add `blocked-question`.

## Agent Workflow

| Actor | How to use the board |
| --- | --- |
| Codex | Owns issue preparation, milestone/priority/status hygiene, routing labels, tracker updates, and PR review. |
| Claude | Picks up `Ready` issue cards with `claude-ready` or a direct handoff comment. If the issue is ambiguous, Claude should ask on the issue instead of widening scope. |
| Copilot | Works only on issues labeled `copilot` and assigned to Copilot. Copilot candidates should be mechanical, file-bounded, and have explicit expected files and acceptance criteria. |

## Status Rules

| Status | Rule |
| --- | --- |
| Backlog | Known work, but not enough context or not on the near path. |
| Ready | Prepared enough for the routed actor to start. |
| In progress | Someone is actively preparing or implementing it. |
| In review | A PR or review queue is active. |
| Done | Issue is closed or completed, PR is merged where applicable, and tracker/docs are updated if needed. |

## Automation

`.github/workflows/agent-ready-router.yml` keeps issue cards on the board aligned with common delivery signals:

| Signal | Board status |
| --- | --- |
| Issue is closed | Done |
| Issue has `needs-codex-review` | In review |
| Issue has `blocked-question` | Backlog |
| Issue has `claude-ready`, `ready-to-implement`, `copilot`, or `needs-claude-action` | Ready |

The sync is best-effort and must not block agent routing. For user-owned GitHub Projects, the workflow may need a `PROJECT_SYNC_TOKEN` secret with permission to update the project. If that token is missing or insufficient, agents should still use labels and comments as the source of truth and update the board manually when needed.

## Done Evidence

Before moving an implementation issue to `Done`, confirm:

- linked PR is merged or the issue explicitly documents why no PR was needed;
- validation result is recorded in the PR or issue;
- tracker row is updated for slice status, PR, implementer, and date;
- requirements traceability is updated when requirement coverage changed;
- stale routing labels are removed where the actor has permission.

## Phase Usage

Phases remain useful because they tell the story of how the product grows:

1. Foundation and repository setup.
2. Booking core.
3. Platform integration foundation.
4. Mobile employee foundation and completion.
5. Web and admin surfaces.
6. Operations and deployment.
7. Demo and client evaluation pack.
8. Commercialisation impact and Billing.

They are not enough for day-to-day steering. Daily work should be driven by `Milestone`, `Priority`, and `Status`.
