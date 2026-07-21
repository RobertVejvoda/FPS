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

## Requirement Intake

Product decisions, UX expectations, and business rules discussed in chat must become trackable repository work before implementation starts. The durable record should be a GitHub issue body, an explicit issue comment, or a linked requirements document referenced by the issue.

Use this rule for every new or changed requirement:

- record the requirement on the owning issue before routing work to Claude, Copilot, or a human implementer;
- make the requirement reviewable as acceptance criteria, a checklist item, or a clearly labeled issue comment;
- keep later clarifications on the same issue when they refine the same slice;
- create a new issue when the clarification changes scope, creates backend/API work, or belongs to a different product area;
- require PRs to link the issue they satisfy with `Closes #N`, `Fixes #N`, or an equivalent GitHub closing keyword.

Chat history is useful context, but it is not the source of truth for delivery. If a requirement matters for fairness, security, privacy, tenant isolation, auditability, usability, or demo readiness, it should be visible from the issue and traceable to the PR that implements it.

## Field Meaning

| Field | Meaning | Rule |
| --- | --- | --- |
| Milestone | Delivery checkpoint such as `Demo v0`, `Employee Pilot`, `Client Evaluation`, `Production Handoff`, or `Commercialisation Later`. | Every open implementation slice should have one. |
| Phase | Product or architecture area such as Mobile, Operations, Platform, Web, Demo, or Commercialisation. | Keep this field correct on issue cards so the board can be grouped or filtered by area; do not use it as workflow state. |
| Priority | Steering signal: `P0`, `P1`, or `P2`. | `P0` is current critical path, `P1` is near follow-up, `P2` is later backlog. |
| Status | State-machine lifecycle state. | Allowed values are `Backlog`, `Ready`, `Assigned`, `In progress`, `In review`, `Needs changes`, `Blocked`, and `Done`. Agents should act only when `Status` and `Owner` point at them. |
| Owner | Actor responsible for the next action. | Allowed values should include `Codex`, `Claude`, `Copilot`, `Robert`, `Human`, and `None`. This is the primary assignment signal for workflow, including non-assignable agents. |
| Implementer | Actor expected to implement or repair the slice when implementation is needed. | Allowed values should include `Claude`, `Copilot`, `Human`, `Codex`, and `None`. This preserves routing intent while `Owner` can move between implementer and reviewer. |
| Assignee | GitHub-native assignment where the actor is assignable. | Use for humans and Copilot when available. For Claude routes, Robert may be assigned only as a notification/UI-invocation hook; the Project `Owner` remains the durable responsibility signal. Do not rely on assignee for Claude or Codex ownership. |
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
| Copilot | Acts when `Owner = Copilot`, ideally with GitHub Copilot coding-agent assignment where available. Default Copilot candidates are mechanical, file-bounded, and explicit about expected files and validation. Copilot Pro+ may also be used for controlled broader slices when Codex marks the issue or handoff as a `Copilot Pro+ controlled route` and requires stricter review evidence. |
| Robert | Acts only when `Owner = Robert`, usually with `Status = Blocked` for a real product, architecture, or operational decision. |

## Reviewer Independence

Implementers must not approve, merge, or mark done their own PRs. This applies to Claude, Copilot, Codex, and human implementers. The Implementer must therefore differ from both Reviewer and Merger; Reviewer and Merger do not need to be different actors for every PR.

The independent reviewer who records acceptance may also merge a low-risk business or documentation PR when the accepted SHA is still the current head, applicable checks are green, no unresolved actionable current-head finding or terminal automation hold remains, and the diff contains no repository-defined high-risk path or architecture, security, privacy, production, or commercially material decision requiring human approval. A new commit invalidates the acceptance. If risk classification is uncertain, treat the PR as high-risk and route it to human-controlled merge. The separate Delivery App merger remains the default automated route for eligible Copilot PRs.

When Claude or Copilot finishes implementation, the correct handoff is:

- leave a concise PR comment with scope summary, validation results, and any known gaps;
- ensure the PR links its owning issue with `Closes #N`, `Fixes #N`, or equivalent;
- if the PR is a draft, mark it ready for review once the implementation and validation evidence are complete;
- set or request `Status = In review`, `Owner = Codex`;
- wait for Codex or a human reviewer to approve, request changes, merge, or close the issue.

Implementer validation is evidence, not acceptance. Acceptance requires an independent reviewer; merge execution may be performed by that reviewer only under the low-risk rule above.

## Copilot CLI Identity and PR Merge Workflow

GitHub Copilot CLI currently runs under Robert's GitHub identity in this repository. This creates a constraint: GitHub may block a formal `APPROVED` review record when the reviewer and PR author share the same account.

The accepted workflow for Copilot CLI PRs is:

- **Copilot CLI opens the PR** and leaves a concise handoff comment with scope, validation results, and `/fps-route codex-review`.
- **Codex reviews the diff** and records findings through PR comments, `/fps-state needs-changes`, or a final summary comment. Codex does not submit a formal GitHub review to avoid the same-account block; the comment record is the review evidence.
- **Low-risk, mechanical PRs** (documentation, pattern-following implementation, lint cleanup, dependency bumps): Codex may merge after review even if GitHub blocks a formal approval record. The review comment or `/fps-state` command is sufficient evidence.
- **Higher-risk PRs** (architecture, cross-service, security, production, or any slice where the diff alone is not enough to validate): Codex should request Robert manual review, or route to Claude for independent validation before merging.
- **A separate bot identity or GitHub App token** is only needed if branch protection policy or audit requirements demand a formal approval from a distinct GitHub account. Record that decision in `docs/versions-and-decisions.md` when it is made.

This workflow preserves reviewer independence through recorded Codex commentary, without requiring a separate GitHub App or token for routine Copilot CLI work. See `AGENTS.md` for the full reviewer independence policy.

## Reverse Handoff

When Claude, Copilot, or a human implementer needs Codex/Robert action, use the same durable GitHub state a human would use:

- leave a concise issue or PR comment with the exact blocker, review request, or decision needed;
- set `Status = Blocked`, `Owner = Robert` when implementation must stop for a product/architecture decision;
- set `Status = In review`, `Owner = Codex` when the work is ready for Codex review or validation;
- set `Status = Needs changes`, `Owner = Implementer` when Codex review requests implementation changes.

Implementers can request these transitions by leaving a short `/fps-route` comment on the issue or PR:

| Command | Result |
| --- | --- |
| `/fps-route codex-review` | `Status = In review`, `Owner = Codex`. |
| `/fps-route claude-fix` | `Status = Needs changes`, `Owner = Claude`, `Implementer = Claude`; assigns Robert for Claude UI invocation. |
| `/fps-route copilot-fix` | `Status = Needs changes`, `Owner = Copilot`, `Implementer = Copilot`. |
| `/fps-route claude-question` | `Status = Blocked`, `Owner = Codex`, `Implementer = Claude`. |
| `/fps-route robert-decision` | `Status = Blocked`, `Owner = Robert`. |
| `/fps-route assign Claude` | `Status = Assigned`, `Owner = Claude`, `Implementer = Claude`; assigns Robert for Claude UI invocation. |
| `/fps-route assign Copilot` | `Status = Assigned`, `Owner = Copilot`, `Implementer = Copilot`. |
| `/fps-route blocked [Robert\|Codex\|Claude\|Copilot]` | `Status = Blocked`, `Owner = Robert` if omitted, otherwise the explicit owner. |

On an issue comment, `/fps-route` updates that issue's board card. On a PR comment, it updates linked closing issues discovered from the PR body. `/fps-route` is accepted from trusted repository collaborators and known agent bots; `/fps-state` remains the repository-owner override for authoritative state corrections.

For Claude-bound routes, the workflow also assigns the issue to Robert and posts a short note. This is intentionally notification-only: Robert receives the GitHub notification, opens the issue, and invokes or reassigns Claude through the GitHub UI. The board `Owner = Claude` remains the durable workflow state.

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

`.github/workflows/delivery-state-orchestrator.yml` reconciles Project fields from issue and PR events. Labels do not drive assignment; Project fields are the durable workflow signals.

| Event | State-machine update |
| --- | --- |
| Issue opened | `Status = Backlog`, `Owner = Codex` if Status is not already set. |
| PR opened, synchronized, or reopened, non-draft (or `ready_for_review`) | Linked closing issues: `Status = In review`; `Owner = Codex` by default, or `Owner = Robert` when the linked issue's `Implementer = Codex` so Codex never reviews its own implementation (per `AGENTS.md` reviewer-independence policy); `Implementer` set from `implemented-by: claude` or `implemented-by: copilot` attribution labels when present. |
| PR opened, synchronized, or reopened while still a draft | Linked closing issues: never routed to Codex review. If `Status = Assigned`, advances to `Status = In progress` with `Owner` set from the existing `Implementer` (preserved, not overwritten to `None`, when `Implementer` is empty/unrecognized); any other `Status` (`Needs changes`, `Blocked`, a capped hold, `Done`, `In review`, ...) is left untouched (AUT-007, `tools/delivery-draft-gate.mjs`). |
| PR review submits `CHANGES_REQUESTED` | Linked closing issues: `Status = Needs changes`, `Owner = current Implementer`. |
| Repository owner comments `/fps-state needs-changes [owner]` on PR | Linked closing issues: `Status = Needs changes`, `Owner = explicit owner or current Implementer`. Use this path when a formal CHANGES_REQUESTED review cannot be submitted (same-account limitation). |
| Repository owner comments `/fps-state in-review` on PR | Linked closing issues: `Status = In review`, `Owner = Codex`. |
| Repository owner comments `/fps-state blocked [Robert\|Codex]` on PR | Linked closing issues: `Status = Blocked`, `Owner = Robert` (default) or `Codex`. |
| Trusted actor comments `/fps-route codex-review` on an issue or PR | Target issue, or PR linked closing issues: `Status = In review`, `Owner = Codex`. |
| Trusted actor comments `/fps-route claude-fix` on an issue or PR | Target issue, or PR linked closing issues: `Status = Needs changes`, `Owner = Claude`, `Implementer = Claude`; assign Robert for notification/UI invocation. |
| Trusted actor comments `/fps-route assign Claude` on an issue or PR | Target issue, or PR linked closing issues: `Status = Assigned`, `Owner = Claude`, `Implementer = Claude`; assign Robert for notification/UI invocation. |
| PR merged | Linked closing issues: `Status = Done`, `Owner = None`. |
| Issue closed | `Status = Done`, `Owner = None`. |

Linked issues are discovered via GitHub's `closingIssuesReferences` API; PRs must include `Closes #N`, `Fixes #N`, or equivalent keywords in the PR body. All board writes are best-effort and log `::notice::` on success or `::warning::` on failure. `PROJECT_SYNC_TOKEN` must have project write access; without it, writes may fall back to the read-only repository token.

After pushing requested fixes, an implementer signals readiness for re-review by commenting `/fps-route codex-review` on the PR (or by updating `Status = In review`, `Owner = Codex` on the board manually if the orchestrator is not yet on master). `/fps-state in-review` remains available as the repository-owner override.

Transitions not yet automated — set these fields manually when they occur:

| Event | Manual update |
| --- | --- |
| Codex accepts a spec as ready | `Status = Ready`, `Owner = selected actor`, `Implementer = selected implementer`. |
| Handoff is prepared or actor is assigned | `Status = Assigned`, `Owner = selected actor`. |

`.github/workflows/agent-ready-router.yml` handles remaining compatibility work:

| Signal | Action |
| --- | --- |
| Issue title has a known slice prefix such as `B`, `MOB`, `WEB`, `OPS`, `BILL`, `CI`, `DOCS`, or platform prefixes such as `A`, `BK`, `CFG`, `CUST`, `ID`, `N`, `P`, `REPORT` | Sync board `Phase` for grouping/filtering. |
| Legacy Claude handoff is requested manually | Prepare a Claude handoff comment. |
| Issue is closed | Remove stale routing labels: `claude-ready`, `ready-to-implement`, `needs-claude-action`, `needs-codex-review`. |
| PR is closed or merged | Remove stale routing labels: `claude-ready`, `needs-claude-action`, `needs-codex-review`. |

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
