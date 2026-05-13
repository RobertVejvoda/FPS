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

**Not in effect (FPS-specific overrides)**
- PR ownership: Claude opens PRs; Codex reviews. (The guide's "Codex opens PRs" rule does not apply.)
- Task tracking: GitHub issues, not `.codex/tasks/active/` or `.codex/results/` files. TASK-XXX / RESULT-XXX schemas are reference material only.
- Agent index file: this `AGENTS.md` is the canonical session index. No `CLAUDE.md` is maintained.
- Docs structure: keep the existing `docs/` layout (layer-based folders + `versions-and-decisions.md`). Do not introduce `architecture.md` / `conventions.md` / `constraints.md` / `decisions.md` without explicit approval.
- CI auto-trigger: the `claude-implement.yml` workflow described in the guide is not adopted.
