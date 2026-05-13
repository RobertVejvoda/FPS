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

## Cooperation Model

See `AGENT_COOPERATION.md` at the repo root for the full Codex / Claude cooperation reference. The sections below record which parts of that guide are in effect for FPS today; treat any guidance in the file that contradicts this list as background context, not policy.

**In effect**
- Roles: Codex is Product Owner (writes specs, validates, reviews). Claude is Implementer.
- Default model for routine implementation: `claude-sonnet-4-6`. Escalate to Opus only for hard problems.
- Architectural decisions go to `docs/versions-and-decisions.md` and require human approval (neither agent decides alone).
- Cost-management tips: keep agent-facing docs lean, scope tasks tightly to files expected to change, compact long sessions.

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
- Agent index file: this `AGENTS.md` is the canonical session index. No `CLAUDE.md` is maintained.
- Docs structure: keep the existing `docs/` layout (layer-based folders + `versions-and-decisions.md`). Do not introduce `architecture.md` / `conventions.md` / `constraints.md` / `decisions.md` without explicit approval.
- The guide's `.codex/tasks/active/**` CI auto-trigger is not adopted.

### Automated Routing

`.github/workflows/agent-ready-router.yml` routes explicit ready signals:

- issues labeled `ready-to-implement` + `copilot`, without `blocked-question`, are assigned to GitHub Copilot coding agent;
- issues labeled `needs-claude-action`, without `blocked-question`, receive a prepared Claude handoff comment and are relabeled `claude-ready`;
- pull requests labeled `needs-claude-action` receive a prepared Claude handoff comment and are relabeled `claude-ready`.

The workflow invokes Copilot assignment automatically, but Claude routing is handoff-only. Manual Claude invocation remains available when the prepared prompt is worth the token cost. Missing Copilot secrets or unavailable external agent services are operational blockers, not product decisions.

### Implementer routing

There are two implementer agents available: **Claude** (Anthropic) and **GitHub Copilot agent** (assign-an-issue model, billed under the GitHub subscription). Codex's specs can be routed to either one. Default routing rule:

- **Copilot candidate** — slice is mechanical and file-bounded: pattern-following implementation that mirrors an existing example, test-coverage additions, mechanical refactors (renames, extracts, lint cleanup), dependency bumps with a clear repro. Codex's spec is tight (clear acceptance criteria + explicit "files expected to change").
- **Claude candidate** — slice touches architecture, cross-service flow, or design judgment; spec might be wrong and needs an implementer who can push back; cross-cutting refactors; anything where reading the diff isn't enough to validate.

When Claude picks up a Codex-assigned slice, the first step is a routing self-check: if the slice looks Copilot-shaped, flag it back to Codex/Robert before starting rather than absorbing it silently. If a Copilot PR is already open on a slice, do not start a parallel implementation — review the Copilot PR or wait.

### Ready Signals

Agents should use GitHub labels and short comments as the handoff signal. Do not rely on implicit conversation history.

- `ready-to-implement` means the issue is ready for an implementer to start, subject to its assignment and labels.
- `copilot` plus assignment to `Copilot` means GitHub Copilot agent should start the issue.
- `needs-claude-action` means the router should prepare a Claude handoff.
- `claude-ready` means a Claude handoff comment is prepared for manual invocation. It does not mean Claude has already run.
- `needs-codex-review` means Codex should review or validate next.
- `blocked-question` means no implementer should continue until Codex/Robert answers the concrete blocker.
- `active-coordination` marks the current coordination thread; it is not by itself implementation permission.

When Codex signals work to an implementer, include a short comment with:

- the target issue or PR;
- the exact next action;
- the source-of-truth docs or review comment;
- whether to implement, revise, pause, or only answer a blocker.

When an implementer finishes, it should update labels back to `needs-codex-review` and leave a concise summary with validation results.

### Attribution

GitHub actions may technically run under Robert's account unless a separate agent token or GitHub App is configured. Use issue labels and PR text to make responsibility clear:

- `initiated-by: codex` — Codex/Product Owner prepared or routed the work.
- `implemented-by: claude` — Claude implemented the PR.
- `implemented-by: copilot` — GitHub Copilot agent implemented the PR.

Copilot-specific behavior is documented in `.github/copilot-instructions.md`.
