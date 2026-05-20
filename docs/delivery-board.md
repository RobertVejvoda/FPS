# Delivery Board

The [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2) is the operational view of delivery. It complements the [Roadmap](./roadmap), [Implementation Tracker](./implementation-tracker), and [Requirements Traceability](./requirements-traceability).

## Board Purpose

The board should answer four questions without reading the whole repository:

| Question | Board signal |
| --- | --- |
| Where are we heading? | Milestone and Roadmap phase. |
| What matters next? | Priority. |
| Who owns the next action? | `Owner` field, then GitHub assignee or agent assignment where available. |
| What evidence proves progress? | Linked issue, linked PR, tracker row, and validation notes. |

## Field Meaning

| Field | Meaning | Rule |
| --- | --- | --- |
| Milestone | Delivery checkpoint such as `Demo v0`, `Employee Pilot`, `Client Evaluation`, `Production Handoff`, or `Commercialisation Later`. | Every open implementation slice should have one. |
| Phase | Product or architecture area such as Mobile, Operations, Platform, Web, Demo, or Commercialisation. | Keep this field correct on issue cards so the board can be grouped or filtered by area; do not use it as workflow state. |
| Priority | Steering signal: `P0`, `P1`, or `P2`. | `P0` is current critical path, `P1` is near follow-up, `P2` is later backlog. |
| Status | State-machine lifecycle state. | Allowed values are `Backlog`, `Ready`, `Assigned`, `In progress`, `In review`, `Needs changes`, `Blocked`, and `Done`. Agents should act only when `Status` and `Owner` point at them. |
| Owner | Actor responsible for the next action. | Allowed values should include `Codex`, `Claude`, `Copilot`, `Robert`, `Human`, and `None`. This is the primary assignment signal for workflow, including non-assignable agents. |
| Implementer | Actor expected to implement or repair the slice when implementation is needed. | Allowed values should include `Claude`, `Copilot`, `Human`, `Codex`, and `None`. This preserves routing intent while `Owner` can move between implementer and reviewer. |
| Assignee | GitHub-native assignment where the actor is assignable. | Use for humans and Copilot when available. Do not rely on assignee for Claude or Codex because they are not consistently exposed as normal GitHub assignees. |
| Labels | Slice taxonomy and durable attribution only. | Labels must not be used for assignment or workflow state. They may describe slice type/classification or attribution such as `implemented-by: claude`. |

## State Machine

`Status` is the workflow state. `Owner` is who acts next. `Implementer` is who should implement or repair the slice when implementation is required. These fields replace label-based assignment.

Allowed transitions:

| From | To | Rule |
| --- | --- | --- |
| `Backlog` | `Ready` | Codex has prepared enough scope, acceptance criteria, and validation guidance for the next actor. |
| `Ready` | `Assigned` | A concrete `Owner` is selected, or a handoff exists for an agent that must be assigned through the GitHub UI. |
| `Assigned` | `In progress` | The owner starts implementation, review, or coordination work. |
| `Assigned` | `Blocked` | The owner cannot start because a dependency or decision is missing. |
| `In progress` | `In review` | A PR, implementation result, or validation request is ready for reviewer action. |
| `In progress` | `Blocked` | Active work cannot continue without a decision or dependency. |
| `In review` | `Needs changes` | Reviewer found changes required from the implementer. |
| `In review` | `Done` | Work is accepted and merged/closed, or no PR was needed and evidence is recorded. |
| `In review` | `Blocked` | Review cannot complete without a decision or missing evidence. |
| `Needs changes` | `Assigned` | The implementer must resume from a review finding. |
| `Needs changes` | `In progress` | The implementer is actively applying fixes. |
| `Needs changes` | `Blocked` | The requested fix cannot proceed without a decision. |
| `Blocked` | `Backlog` | The work is no longer actionable or is deferred. |
| `Blocked` | `Ready` | The blocker is resolved and the next actor can be selected. |
| `Blocked` | `Assigned` | The blocker is resolved and a next owner is selected. |
| `Blocked` | `In progress` | The same owner resumes active work after the blocker is resolved. |
| `Done` | _(terminal)_ | Closed/merged/accepted work should not move back without reopening the issue or PR. |

Recommended state ownership:

| Status | Typical Owner | Meaning |
| --- | --- | --- |
| `Backlog` | `Codex` or `None` | Known work, not ready to start. |
| `Ready` | `Codex`, `Claude`, `Copilot`, or `Human` | Scope is ready; next actor can be selected or invoked. |
| `Assigned` | Selected actor | The next actor is known but work has not visibly started. |
| `In progress` | Selected actor | The owner is actively implementing, reviewing, or coordinating. |
| `In review` | `Codex` | Implementation or evidence is ready for Codex/Product Owner review. |
| `Needs changes` | Implementer | Review found required changes. |
| `Blocked` | `Robert`, `Codex`, or dependency owner | A concrete decision, dependency, or missing evidence prevents progress. |
| `Done` | `None` | No next action. |

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
- an `Owner` for the next action;
- an `Implementer` when implementation or repair is expected;
- source-of-truth docs or explicit issue-local requirements;
- scope and out-of-scope notes;
- acceptance criteria or a clear review target;
- validation expected from the implementer.

If any of these are missing, keep the issue in `Backlog` or `Blocked` rather than encoding the gap in labels.

## Agent Workflow

| Actor | How to use the board |
| --- | --- |
| Codex | Owns issue preparation, state-machine hygiene, tracker updates, and PR review. Act when `Owner = Codex`. |
| Claude | Acts when `Owner = Claude` and a direct handoff comment is present. GitHub Web UI agent assignment may still be needed to invoke Claude, but the board fields are the durable state. |
| Copilot | Acts when `Owner = Copilot`, ideally with GitHub Copilot coding-agent assignment where available. Copilot candidates should be mechanical, file-bounded, and explicit about expected files and validation. |
| Robert | Acts only when `Owner = Robert`, usually with `Status = Blocked` for a real product, architecture, or operational decision. |

## Reverse Handoff

When Claude, Copilot, or a human implementer needs Codex/Robert action, use the same durable GitHub state a human would use:

- leave a concise issue or PR comment with the exact blocker, review request, or decision needed;
- set `Status = Blocked`, `Owner = Robert` when implementation must stop for a product/architecture decision;
- set `Status = In review`, `Owner = Codex` when the work is ready for Codex review or validation;
- set `Status = Needs changes`, `Owner = Implementer` when Codex review requests implementation changes.

Claude agent invocation remains separate because GitHub exposes it through the Web UI agent picker rather than the normal assignees API. The board fields still record durable ownership.

## Status Rules

| Status | Rule |
| --- | --- |
| Backlog | Known work, but not enough context or not on the near path. |
| Ready | Prepared enough for the routed actor to start. |
| Assigned | A specific owner is selected, but work has not visibly started. |
| In progress | The owner is actively preparing, implementing, or repairing it. |
| In review | A PR or review queue is active and Codex owns next action. |
| Needs changes | Review found changes and the implementer owns next action. |
| Blocked | A concrete dependency, decision, or missing evidence prevents progress. |
| Done | Issue is closed or completed, PR is merged where applicable, and tracker/docs are updated if needed. |

## Automation

Prefer GitHub Project built-in workflows for generic board lifecycle:

| Signal | Board status |
| --- | --- |
| Item is added to the project | Backlog |
| Issue or pull request is closed | Done |
| Pull request is merged | Done |

Use GitHub Project built-in auto-add workflows to add FPS repository issues to the board when practical. Use built-in archive workflows for old `Done` items when the board becomes noisy.

The next automation target is a delivery state orchestrator that reconciles Project fields from issue and PR events. Labels should not drive assignment. Until that orchestrator exists, agents should update `Status`, `Owner`, and `Implementer` directly when they change responsibility.

Existing `.github/workflows/agent-ready-router.yml` is transitional FPS-specific glue. It may prepare handoff comments or cleanup stale legacy labels, but it is no longer the assignment model:

| Signal | Action |
| --- | --- |
| Issue title has a known slice prefix such as `B`, `MOB`, `WEB`, `OPS`, `BILL`, `CI`, `DOCS001`, or platform prefixes such as `A`, `BK`, `CFG`, `CUST`, `ID`, `N`, `P`, `REPORT` | Sync board `Phase` for grouping/filtering. |
| Legacy Claude handoff is requested manually | Prepare a Claude handoff comment, then set `Owner = Claude` and `Status = Assigned` or `Needs changes` as appropriate. |
| Issue is closed | Remove stale routing labels: `claude-ready`, `ready-to-implement`, `needs-claude-action`, `needs-codex-review`. |
| PR is closed or merged | Remove stale routing labels: `claude-ready`, `needs-claude-action`, `needs-codex-review`. |

The state orchestrator should enforce these rules once implemented:

| Event | State-machine update |
| --- | --- |
| Issue created or added to project | `Status = Backlog`, `Owner = Codex` unless a more specific owner is supplied. |
| Codex accepts a spec as ready | `Status = Ready`, `Owner = selected actor`, `Implementer = selected implementer` when implementation is needed. |
| Handoff is prepared or actor is assigned | `Status = Assigned`, `Owner = selected actor`. |
| PR opens for an issue | `Status = In review`, `Owner = Codex`, `Implementer = PR author/agent where known`. |
| Codex review requests changes | `Status = Needs changes`, `Owner = Implementer`. |
| Implementer pushes requested fixes | `Status = In review`, `Owner = Codex`. |
| Product/architecture decision is needed | `Status = Blocked`, `Owner = Robert` or `Codex`, with a concrete blocker comment. |
| PR merged or issue closed | `Status = Done`, `Owner = None`; cleanup temporary routing labels. |

The sync is best-effort and must not block agent routing. `PROJECT_SYNC_TOKEN` must have permission to update the user-owned Project. If that token is missing or insufficient, agents should still update board fields manually after changing responsibility.

Closed issues and closed pull requests are routing cleanup boundaries. Attribution labels such as `implemented-by: claude` are preserved; temporary routing labels are removed automatically so completed work does not remain visible as ready or in review.

## Done Evidence

Before moving an implementation issue to `Done`, confirm:

- linked PR is merged or the issue explicitly documents why no PR was needed;
- validation result is recorded in the PR or issue;
- tracker row is updated for slice status, PR, implementer, and date;
- requirements traceability is updated when requirement coverage changed;
- temporary routing labels are removed where the actor has permission.

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

Phase draft cards are optional roadmap markers only. They do not own work and should not be treated as containers for issue cards. Daily work should be driven by `Status`, `Owner`, `Implementer`, `Milestone`, and `Priority`; `Phase` is for grouping and filtering.
