# Delivery Board

The [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2) is the operational view of delivery. It complements the [Roadmap](./roadmap), [Implementation Tracker](./implementation-tracker), and [Requirements Traceability](./requirements-traceability).

## Board Purpose

The board should answer four questions without reading the whole repository:

| Question | Board signal |
| --- | --- |
| Where are we heading? | Milestone and Roadmap phase. |
| What matters next? | Priority. |
| Who owns the next action? | Assignee or agent assignment, then Status. |
| What evidence proves progress? | Linked issue, linked PR, tracker row, and validation notes. |

## Field Meaning

| Field | Meaning | Rule |
| --- | --- | --- |
| Milestone | Delivery checkpoint such as `Demo v0`, `Employee Pilot`, `Client Evaluation`, `Production Handoff`, or `Commercialisation Later`. | Every open implementation slice should have one. |
| Phase | Product or architecture area such as Mobile, Operations, Platform, Web, Demo, or Commercialisation. | Keep this field correct on issue cards so the board can be grouped or filtered by area; do not use it as workflow state. |
| Priority | Steering signal: `P0`, `P1`, or `P2`. | `P0` is current critical path, `P1` is near follow-up, `P2` is later backlog. |
| Status | Operational state: Backlog, Ready, In progress, In review, or Done. | Agents should act only on issue cards in a state that matches their role. |
| Assignee | Primary ownership signal for assignable GitHub actors: Codex/Robert, Copilot, or a human owner. | Assign the actor expected to take or supervise the next action. Claude uses GitHub Web UI agent assignment rather than normal issue assignment. |
| Labels | Exceptional state and audit signal. | Use labels for blockers, review requests, explicit handoff generation, and attribution; do not use labels as the primary owner signal. |

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
- assignee or agent assignment matching the expected next actor: Codex/Robert, Claude agent, Copilot, or human;
- validation expected from the implementer.

If any of these are missing, keep the issue in `Backlog` or add `blocked-question`.

## Agent Workflow

| Actor | How to use the board |
| --- | --- |
| Codex | Owns issue preparation, milestone/priority/status hygiene, tracker updates, and PR review. Assign Codex/Robert when product/spec/review action is next. |
| Claude | Uses GitHub Web UI agent assignment plus `Ready` status and a direct handoff comment as the durable signal. If the issue is ambiguous, Claude should ask on the issue instead of widening scope. |
| Copilot | Uses assignment to the GitHub Copilot coding agent as the durable signal. Copilot candidates should be mechanical, file-bounded, and have explicit expected files and acceptance criteria. |

## Reverse Handoff

When Claude, Copilot, or a human implementer needs Codex/Robert action, use the same durable GitHub state a human would use:

- leave a concise issue or PR comment with the exact blocker, review request, or decision needed;
- add `blocked-question` when implementation must stop for a product/architecture decision;
- add `needs-codex-review` when the work is ready for Codex review or validation;
- assign Robert only for a real human decision, not for routine Codex review.

The router assigns Robert automatically for issue and PR label `blocked-question`. `needs-codex-review` uses label and board status only because Codex is not exposed as a normal GitHub assignee in this repository. Claude agent assignment remains separate because GitHub exposes it through the Web UI agent picker rather than the normal assignees API.

## Status Rules

| Status | Rule |
| --- | --- |
| Backlog | Known work, but not enough context or not on the near path. |
| Ready | Prepared enough for the routed actor to start. |
| In progress | Someone is actively preparing or implementing it. |
| In review | A PR or review queue is active. |
| Done | Issue is closed or completed, PR is merged where applicable, and tracker/docs are updated if needed. |

## Automation

Prefer GitHub Project built-in workflows for generic board lifecycle:

| Signal | Board status |
| --- | --- |
| Item is added to the project | Backlog |
| Issue or pull request is closed | Done |
| Pull request is merged | Done |

Use GitHub Project built-in auto-add workflows to add FPS repository issues to the board when practical. Use built-in archive workflows for old `Done` items when the board becomes noisy.

`.github/workflows/agent-ready-router.yml` now keeps only FPS-specific glue that built-ins cannot infer:

| Signal | Action |
| --- | --- |
| Issue title has a known slice prefix such as `B`, `MOB`, `WEB`, `OPS`, `BILL`, `CI`, `DOCS001`, or platform prefixes such as `A`, `BK`, `CFG`, `CUST`, `ID`, `N`, `P`, `REPORT` | Sync board `Phase` for grouping/filtering. |
| Issue has `blocked-question` | Sync board status to `Backlog`. |
| Issue has `needs-codex-review` | Sync board status to `In review` unless `blocked-question` is also present. |
| Issue or PR has `blocked-question` | Assign Robert for human decision. |
| Issue has `needs-claude-action` | Prepare a Claude handoff comment and remove `needs-claude-action`. Robert can then assign the Claude agent through the GitHub Web UI. |
| PR has `needs-claude-action` | Prepare a Claude handoff comment and remove `needs-claude-action`. |
| Issue is closed | Remove stale routing labels: `claude-ready`, `ready-to-implement`, `needs-claude-action`, `needs-codex-review`. |
| PR is closed or merged | Remove stale routing labels: `claude-ready`, `needs-claude-action`, `needs-codex-review`. |

The sync is best-effort and must not block agent routing. `PROJECT_SYNC_TOKEN` must have permission to update the user-owned Project. If that token is missing or insufficient, agents should still use assignees, labels, and comments as the source of truth and update the board manually when needed.

Closed issues and closed pull requests are routing cleanup boundaries. Attribution labels such as `implemented-by: claude` are preserved; temporary routing labels are removed automatically so completed work does not remain visible as ready or in review.

## Done Evidence

Before moving an implementation issue to `Done`, confirm:

- linked PR is merged or the issue explicitly documents why no PR was needed;
- validation result is recorded in the PR or issue;
- tracker row is updated for slice status, PR, implementer, and date;
- requirements traceability is updated when requirement coverage changed;
- stale action labels are removed where the actor has permission.

## Phase Usage

Phases remain useful as a field on real issue cards because they make the Kanban readable by product area:

- Foundation and repository setup.
- Booking core.
- Platform integration foundation.
- Mobile employee foundation and completion.
- Web and admin surfaces.
- Operations and deployment.
- Demo and client evaluation pack.
- Commercialisation impact and Billing.

Phase draft cards are optional roadmap markers only. They do not own work and should not be treated as containers for issue cards. Daily work should be driven by `Status`, assignee or agent assignment, `Milestone`, and `Priority`; `Phase` is for grouping and filtering.
