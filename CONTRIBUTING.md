# Contributing — Agent Collaboration Protocol

This document defines the git workflow for Claude and Codex working in parallel on this repository.

## Copyright and License

FPS is licensed under AGPL-3.0-or-later. By submitting a contribution to this repository, you agree that your contribution is provided under AGPL-3.0-or-later unless a separate written agreement says otherwise.

The project copyright notice is maintained in `NOTICE`. AI-assisted changes prepared by Codex, Claude, or similar tools under Robert Vejvoda's direction are treated as project contributions under the same repository license.

Do not submit code, documentation, generated assets, or third-party content unless you have the right to contribute it under AGPL-3.0-or-later and it is compatible with the repository license.

## Scope Boundaries

| Agent | Owns | Never touches |
|-------|------|---------------|
| **Codex** | `docs/**` | `code/**`, `tools/**`, `.claude/**` |
| **Claude** | `code/**`, `tools/**`, `.claude/**` | `docs/**` |

These boundaries are hard. Even small cross-scope edits must not happen.

## Branch Naming

| Agent | Pattern | Example |
|-------|---------|---------|
| Codex | `docs/<topic>` | `docs/executable-draw-rules` |
| Claude | `feature/<SliceId>-<name>` | `feature/B004-run-scheduled-draw` |
| Either | `fix/<description>` | `fix/dapr-store-name` |

## Implementation Handoff Flow

```
Codex pushes docs/<topic> branch with completed requirements
  ↓
Claude cron watcher detects new branch and notifies Robert
  ↓
Claude reads requirements from the branch (no need to wait for master merge)
  ↓
Claude implements in feature/* branch, merges to master via PR
  ↓
Codex merges docs/<topic> to master independently — no ordering dependency
```

Scopes do not overlap, so merge order does not matter.

## Master Protection Rules

- Never push directly to `master` — always via PR
- Never `git push --force` on any branch
- Never `git rebase` on `master` or any `docs/*` branch
- No `--no-verify` on commits

## Shared Files

A small number of files appear in both scopes:

| File | Owner | Rule |
|------|-------|------|
| `docs/business-layer.md` | Codex | Claude never edits |
| `docs/development-plan.md` | Codex | Claude never edits |
| `docs/versions-and-decisions.md` | Codex | Claude never edits |
| `CONTRIBUTING.md` | Shared | Either agent may update via PR; discuss before changing |

## Conflict Prevention

- Pull from `origin/master` before starting any new branch
- If `FPS.sln` or a shared config file needs updating, the agent doing the work pulls master first
- Codex does not touch `FPS.sln`, `*.csproj`, or `code/` files

## Signalling

When Codex completes a requirements branch, push it to `origin/docs/<topic>`. Claude's watcher will detect it within 10 minutes and notify Robert.

When Claude completes an implementation slice and merges to master, the CI run on master is the confirmation signal.
