# Agent Instructions

This repository is documentation-first unless the user explicitly asks for code changes.

## Scope

- Default work area: `docs/`.
- Do not modify application or infrastructure code unless the user asks for implementation work.
- Keep documentation changes consistent with the existing architecture, terminology, and decision log.
- When a design decision is made, record it in the relevant docs and, when durable, in `docs/versions-and-decisions.md`.

## Safety Rules

- Follow the same project safety gates configured for Claude.
- Use the repo review hooks before shell, edit, write, or multi-edit actions where supported.
- Do not bypass hooks.
- Do not force push.
- Do not edit secrets, tokens, private keys, or `.env` files.
- Do not remove tests or validation scripts as part of documentation work.

## Validation

- For documentation-only changes, review the changed Markdown for clarity and internal consistency.
- For code changes, run `./tools/validate.sh` when feasible and report the result.
- Keep pull requests focused on one logical unit of work.

### Cross-Agent Validation

Use Claude as a second reviewer only when the quality risk justifies the token cost.

Good Claude validation candidates:

- architecture, security, privacy, GDPR, auth, secrets, audit, billing, tenant isolation, or production operations changes;
- cross-service designs or implementation plans where one missed assumption can affect multiple bounded contexts;
- substantial Codex-authored specs that will drive non-trivial implementation by Claude or Copilot;
- PR reviews where the diff is large enough that an independent implementation-focused read may catch gaps.

Do not route routine work to Claude validation by default:

- typo fixes, tracker updates, Home/sidebar maintenance, link fixes, and other low-risk documentation cleanup;
- mechanical changes already covered by validation and local review.

When requesting Claude validation, ask for a focused review of gaps, contradictions, implementation risk, and missing acceptance criteria. Claude should report findings first and should not rewrite or broaden scope unless Codex/Robert explicitly asks for edits.

## Cooperation Model

See `AGENT_COOPERATION.md` at the repo root for the full Codex / Claude cooperation reference. The sections below record which parts of that guide are in effect for FPS today; treat any guidance in the file that contradicts this list as background context, not policy.

**In effect**
- Roles: Codex is Product Owner (writes specs, validates, reviews). Claude and GitHub Copilot agent are Implementers.
- Default Claude model for routine implementation: `claude-sonnet-4-6`. Escalate to Opus only for hard problems.
- GitHub Copilot Pro+ may be used as an implementation route for controlled experiments and broader slices when Codex prepares tight scope, acceptance criteria, expected files, and validation evidence.
- Architectural decisions go to `docs/versions-and-decisions.md` and require human approval (neither agent decides alone).
- Cost-management tips: keep agent-facing docs lean, scope tasks tightly to files expected to change, compact long sessions.
- Cross-agent validation: Claude may be used as a second reviewer for high-impact Codex-authored architecture/security/spec work, but not for routine low-risk updates.
- Reviewer independence: an implementer must not approve, merge, or mark done its own PR. Claude and Copilot may report validation results and request review, but Codex or a human reviewer must approve acceptance.

### Context And Cost Hygiene

All agents should keep session history small and handoffs explicit:

- Use `/compact` before or during long interactive sessions when the tool supports it.
- Before compacting, leave a concise state summary covering current branch, goal, files changed, validation run, blockers, and next action.
- Prefer linking to issue bodies, PRs, and focused docs over pasting long history into prompts.
- Keep implementer prompts short and bounded to expected files, acceptance criteria, and validation commands.
- Do not ask another agent to re-read broad directories or full conversation history when a focused summary is enough.

**Not in effect (FPS-specific overrides)**
- PR ownership: Claude opens PRs; Codex reviews. (The guide's "Codex opens PRs" rule does not apply.)
- Task tracking: GitHub issues, not `.codex/tasks/active/` or `.codex/results/` files. TASK-XXX / RESULT-XXX schemas are reference material only.
- Delivery board: use the [FPS Delivery Kanban](https://github.com/users/RobertVejvoda/projects/2). GitHub issues are the canonical slice cards; phase draft cards are optional high-level markers only and do not contain work. New implementation slices should be added to the board with `Milestone`, `Phase`, `Priority`, and `Status` set. Board rules are documented in `docs/delivery-board.md`.
- Agent index file: this `AGENTS.md` is the canonical session index. No `CLAUDE.md` is maintained.
- Docs structure: keep the existing `docs/` layout (layer-based folders + `versions-and-decisions.md`). Do not introduce `architecture.md` / `conventions.md` / `constraints.md` / `decisions.md` without explicit approval.
- The guide's `.codex/tasks/active/**` CI auto-trigger is not adopted.

### Automated Routing

`.github/workflows/agent-ready-router.yml` is transitional FPS-specific glue. It may prepare compatibility handoff comments and clean stale legacy routing labels, but it is no longer the assignment model:

- issue board `Phase` is synced best-effort from known slice/title prefixes for Kanban grouping;
- closed issues sync to `Done` as a cleanup convenience;
- issues labeled `needs-claude-action`, without a blocker, receive a prepared Claude handoff comment and have `needs-claude-action` removed;
- pull requests labeled `needs-claude-action` receive a prepared Claude handoff comment and have `needs-claude-action` removed;
- closed issues have stale routing labels removed: `claude-ready`, `ready-to-implement`, `needs-claude-action`, and `needs-codex-review`;
- closed pull requests have stale routing labels removed: `claude-ready`, `needs-claude-action`, and `needs-codex-review`.

Delivery ownership is state-machine-first. GitHub Project fields `Status`, `Owner`, and `Implementer` are the durable workflow signals. GitHub assignees and Claude/Copilot UI assignment may still invoke a tool or notify a person, but they are not the workflow state. Labels must not be used for assignment; use labels only for slice taxonomy, durable attribution, and temporary compatibility triggers while the orchestrator is being replaced.

The next automation target is a delivery state orchestrator that reads issue and pull request events, reconciles `Status` / `Owner` / `Implementer`, and treats labels only as taxonomy or temporary compatibility triggers. Board sync is intentionally non-blocking. If the repository token cannot write to the user-owned GitHub Project, configure `PROJECT_SYNC_TOKEN` with Project access; otherwise agents should update the board manually after changing responsibility.

### Implementer routing

There are two implementer agents available: **Claude** (Anthropic) and **GitHub Copilot agent** (assign-an-issue model, billed under the GitHub subscription). Codex's specs can be routed to either one. Default routing rule:

- **Copilot candidate** — slice is mechanical and file-bounded: pattern-following implementation that mirrors an existing example, test-coverage additions, mechanical refactors (renames, extracts, lint cleanup), dependency bumps with a clear repro. Codex's spec is tight (clear acceptance criteria + explicit "files expected to change").
- **Claude candidate** — slice touches architecture, cross-service flow, or design judgment; spec might be wrong and needs an implementer who can push back; cross-cutting refactors; anything where reading the diff isn't enough to validate.
- **Copilot Pro+ controlled candidate** — a broader slice may be assigned to Copilot when the goal is to evaluate Pro+ behavior or to preserve Claude quota. Codex must make the scope unusually explicit, name non-goals, list expected files, require validation evidence, and call out safety constraints. Treat the first PR as an implementation proposal that needs strict Codex review, not as automatically equivalent to a Claude implementation.

When Claude or Copilot picks up a Codex-assigned slice, the first step is a routing self-check: if the slice looks better suited to the other implementer, flag it back to Codex/Robert before starting rather than absorbing it silently. If a PR is already open on a slice, do not start a parallel implementation — review the existing PR or wait.

Only one implementer owns an issue at a time. If a slice moves from Claude to Copilot, or from Copilot to Claude, update `Owner`, `Implementer`, and the handoff comment before work starts.

### Implementer Ready-For-Review Gate

Before Claude or Copilot moves a PR to `In review` / `Owner = Codex`, the implementer must complete and report the slice-specific validation checklist from the issue or source-of-truth doc.

For UI and UX work, this includes:

- run the relevant web build/typecheck for any web changes;
- run the relevant mobile typecheck/build validation for any mobile changes;
- run any terminology or safety grep specified by the issue/spec;
- fix all employee-visible forbidden terms or technical identifiers, or explicitly classify remaining hits as internal route/API/type names or admin-only surfaces;
- include screenshots or concise visual notes for changed web and mobile screens;
- include validation command results in the PR body or final handoff comment.

Do not treat copy-only UI changes as exempt from build/typecheck validation. If a checklist item cannot be run, say exactly why and keep the work out of `In review` unless Codex/Robert explicitly accepts the gap.

### State Machine

Agents should use GitHub Project fields as the source of truth. Do not use labels for assignment. Labels are for slice type/classification, durable attribution, and temporary compatibility triggers only.

- `Status` is the lifecycle state: `Backlog`, `Ready`, `Assigned`, `In progress`, `In review`, `Needs changes`, `Blocked`, or `Done`.
- `Owner` is who must act next: `Codex`, `Claude`, `Copilot`, `Robert`, `Human`, or `None`.
- `Implementer` is who should implement or repair the slice when implementation is needed: `Claude`, `Copilot`, `Human`, `Codex`, or `None`.
- `Phase` is only product-area grouping and filtering; do not use it as workflow state.
- `Milestone` and `Priority` drive ordering, not ownership.

Allowed workflow:

- `Backlog -> Ready`: Codex has prepared enough scope, acceptance criteria, and validation guidance.
- `Ready -> Assigned`: a concrete `Owner` is selected or a handoff exists for an agent that must be invoked through UI.
- `Assigned -> In progress`: the owner starts work.
- `In progress -> In review`: a PR or implementation result is ready for Codex review.
- `In review -> Needs changes`: Codex found required changes; `Owner` returns to `Implementer`.
- `Needs changes -> In progress`: implementer is actively fixing review findings.
- `Blocked`: use only for a concrete dependency, decision, or missing evidence. Set `Owner` to the actor who can unblock it.
- `Done`: terminal closed/merged/accepted state.

Claude-ready work has `Owner = Claude` plus a direct handoff comment. GitHub Web UI agent assignment may still be needed to invoke Claude, but the board fields remain the durable state. Copilot work has `Owner = Copilot` plus Copilot assignment where available. Codex review work has `Status = In review`, `Owner = Codex`.

For Copilot Pro+ experiments, the issue or handoff comment must say `Copilot Pro+ controlled route` and include the extra review expectations. This makes it clear that broader Copilot usage is deliberate and measured.

Reverse handoff from implementers should look like normal human workflow: leave a concise comment with the exact blocker or review request, then update Project fields. Use `Status = Blocked`, `Owner = Robert` only when a real human product/architecture decision is needed. Use `Status = In review`, `Owner = Codex` when Codex should review or validate next. Use `Status = Needs changes`, `Owner = Implementer` when Codex has requested fixes.

When Codex signals work to an implementer, include a short comment with:

- the target issue or PR;
- the exact next action;
- the source-of-truth docs or review comment;
- whether to implement, revise, pause, or only answer a blocker.

When an implementer finishes, it should set `Status = In review`, `Owner = Codex`, remove stale temporary routing labels when permitted, and leave a concise summary with validation results.

### PR Monitoring Loops

Claude PR monitoring loops must watch both formal PR reviews and PR conversation comments.
GitHub blocks formal `CHANGES_REQUESTED` reviews when the reviewer and PR author share the
same GitHub account, so Codex may leave authoritative review feedback as a PR comment with
`/fps-state needs-changes ...`. A loop that only polls `reviews` is incomplete.

For every monitored PR, poll:

- CI/check state;
- formal PR reviews;
- PR conversation comments, including `/fps-state` and `/fps-route` commands;
- new Codex/Product Owner comments that contain review findings.

If a Codex comment requests changes or includes `/fps-state needs-changes`, treat it exactly
like a blocking changes-request review: notify Robert when appropriate, address the findings,
rerun the slice validation checklist, post a fix summary, and return the PR to Codex review.

### Attribution

GitHub actions may technically run under Robert's account unless a separate agent token or GitHub App is configured. Use issue labels and PR text to make responsibility clear:

- `initiated-by: codex` — Codex/Product Owner prepared or routed the work.
- `implemented-by: claude` — Claude implemented the PR.
- `implemented-by: copilot` — GitHub Copilot agent implemented the PR.

Copilot-specific behavior is documented in `.github/copilot-instructions.md`.
