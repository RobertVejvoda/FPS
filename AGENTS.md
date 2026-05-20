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
- Roles: Codex is Product Owner (writes specs, validates, reviews). Claude is Implementer.
- Default model for routine implementation: `claude-sonnet-4-6`. Escalate to Opus only for hard problems.
- Architectural decisions go to `docs/versions-and-decisions.md` and require human approval (neither agent decides alone).
- Cost-management tips: keep agent-facing docs lean, scope tasks tightly to files expected to change, compact long sessions.
- Cross-agent validation: Claude may be used as a second reviewer for high-impact Codex-authored architecture/security/spec work, but not for routine low-risk updates.

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

`.github/workflows/agent-ready-router.yml` routes explicit ready signals:

- issue board status is synced best-effort only for FPS-specific exceptional states that GitHub Project built-ins cannot infer;
- issue board `Phase` is synced best-effort from known slice/title prefixes for Kanban grouping;
- closed issues sync to `Done`;
- issues labeled `blocked-question` sync to `Backlog`;
- issues labeled `needs-codex-review` sync to `In review` unless `blocked-question` is also present;
- issues or pull requests labeled `blocked-question` are assigned to Robert for human decision;
- issues labeled `needs-claude-action`, without `blocked-question`, receive a prepared Claude handoff comment and have `needs-claude-action` removed;
- pull requests labeled `needs-claude-action` receive a prepared Claude handoff comment and have `needs-claude-action` removed.
- closed issues have stale routing labels removed: `claude-ready`, `ready-to-implement`, `needs-claude-action`, and `needs-codex-review`;
- closed pull requests have stale routing labels removed: `claude-ready`, `needs-claude-action`, and `needs-codex-review`.

Ownership is now assignment-first for assignable GitHub actors. GitHub Project built-in workflows should handle generic lifecycle changes where reliable; the router explicitly syncs closed issues to `Done` because this status is operationally important. Copilot assignment is manual unless GitHub's own Copilot assignment flow is used directly. Claude routing remains handoff-only; assign the Claude agent through the GitHub Web UI when the handoff is ready and worth the token cost. Missing external agent services are operational blockers, not product decisions.

Board status sync is intentionally non-blocking. If the repository token cannot write to the user-owned GitHub Project, configure `PROJECT_SYNC_TOKEN` with Project access; otherwise agents should update the board manually after changing labels or closing issues.

### Implementer routing

There are two implementer agents available: **Claude** (Anthropic) and **GitHub Copilot agent** (assign-an-issue model, billed under the GitHub subscription). Codex's specs can be routed to either one. Default routing rule:

- **Copilot candidate** — slice is mechanical and file-bounded: pattern-following implementation that mirrors an existing example, test-coverage additions, mechanical refactors (renames, extracts, lint cleanup), dependency bumps with a clear repro. Codex's spec is tight (clear acceptance criteria + explicit "files expected to change").
- **Claude candidate** — slice touches architecture, cross-service flow, or design judgment; spec might be wrong and needs an implementer who can push back; cross-cutting refactors; anything where reading the diff isn't enough to validate.

When Claude picks up a Codex-assigned slice, the first step is a routing self-check: if the slice looks Copilot-shaped, flag it back to Codex/Robert before starting rather than absorbing it silently. If a Copilot PR is already open on a slice, do not start a parallel implementation — review the Copilot PR or wait.

### Ready Signals

Agents should use GitHub assignees as the primary ownership signal for assignable actors, with Project `Status` as the readiness/progress signal and short comments for handoff details. Labels describe exceptional state or explicit action requests; do not rely on implicit conversation history.

- The Kanban board `Status` field is the operational state: `Backlog`, `Ready`, `In progress`, `In review`, or `Done`.
- The board `Phase` field remains useful for product-area grouping and filtering, but agents should use `Status`, `Assignee`, `Milestone`, and `Priority` to decide what to do next.
- The issue assignee is the primary actor signal for Codex/Robert, Copilot, or a human owner.
- Claude-ready work uses GitHub's Web UI agent assignment, not a normal issue assignee exposed through the assignees API. A Claude handoff comment plus `Status = Ready` means the issue is prepared for Robert to assign to the Claude agent through the Web UI. If the issue is not specific enough, Claude should comment with the missing information instead of starting broad work.
- Copilot should work only on issue cards assigned to Copilot. Copilot candidates should remain mechanical, file-bounded, and explicit about expected files and validation.
- `ready-to-implement` is optional and secondary; `Status = Ready` plus assignee is the preferred readiness signal.
- `copilot` is optional and secondary; assignment to Copilot is the durable signal.
- `needs-claude-action` means the GitHub Actions router should prepare a Claude handoff. The router claims and removes this label before posting the handoff so duplicate workflow events do not create duplicate prompts, and the same issue or PR can be routed again later.
- `claude-ready` is legacy and should not be used for new routing. A direct handoff comment plus assignment/status is the durable signal. If present, the router removes it automatically when the issue or PR closes.
- `needs-codex-review` means Codex should review or validate next.
- `blocked-question` means no implementer should continue until Codex/Robert answers the concrete blocker.
- `active-coordination` marks the current coordination thread; it is not by itself implementation permission.

Reverse handoff from implementers should look like normal human workflow: leave a concise comment with the exact blocker or review request, then add `blocked-question` or `needs-codex-review`. Use `blocked-question` and assign Robert only when a real human product/architecture decision is needed. Use `needs-codex-review` without human assignment when Codex should review or validate next. This is separate from Claude agent assignment, which currently remains a GitHub Web UI action.

When Codex signals work to an implementer, include a short comment with:

- the target issue or PR;
- the exact next action;
- the source-of-truth docs or review comment;
- whether to implement, revise, pause, or only answer a blocker.

When an implementer finishes, it should update labels back to `needs-codex-review`, remove stale ready/action labels when permitted, and leave a concise summary with validation results.

### Attribution

GitHub actions may technically run under Robert's account unless a separate agent token or GitHub App is configured. Use issue labels and PR text to make responsibility clear:

- `initiated-by: codex` — Codex/Product Owner prepared or routed the work.
- `implemented-by: claude` — Claude implemented the PR.
- `implemented-by: copilot` — GitHub Copilot agent implemented the PR.

Copilot-specific behavior is documented in `.github/copilot-instructions.md`.
