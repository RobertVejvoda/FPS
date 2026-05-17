# Development Tooling

Scripts in `tools/` automate quality gates and permission review for AI-assisted development.

---

## Continuous Integration

Repository health is visible through two GitHub Actions status badges on `README.md` and `docs/Home.md`:

| Badge | Workflow | What it means |
|---|---|---|
| **CI** | `.github/workflows/ci.yml` | Latest run of restore + build + test + generated-client stale-check on `master`. |
| **Docs** | `.github/workflows/docs.yml` | Latest deploy of `docs/` to GitHub Pages on `master`. |

### When CI runs

| Trigger | Behaviour |
|---|---|
| `pull_request` to `master` | Runs when `code/**`, `tools/**`, or `.github/workflows/**` change. |
| `push` to `master` | Runs after merge for the same path set. |
| `workflow_dispatch` | A maintainer can run CI manually from the Actions tab. |
| `schedule` (weekly) | Runs every Monday at 06:00 UTC so SDK, dependency, and environment drift is caught even when no PR is active. |

The `docs` workflow runs on `push` to `master` when anything under `docs/**` changes and is also exposed via `workflow_dispatch` for manual republish.

## Agent Routing

`.github/workflows/agent-ready-router.yml` keeps only FPS-specific routing glue that GitHub Project built-in workflows cannot infer:

| Signal | Automated action |
|---|---|
| Issue title has a known slice prefix such as `B`, `MOB`, `WEB`, `OPS`, `BILL`, `CI`, `DOCS001`, or platform prefixes such as `A`, `BK`, `CFG`, `CUST`, `ID`, `N`, `P`, `REPORT` | Syncs the Delivery Kanban `Phase` field |
| Issue is closed | Syncs the Delivery Kanban status to `Done` |
| Issue has `blocked-question` | Syncs the Delivery Kanban status to `Backlog` |
| Issue has `needs-codex-review` | Syncs the Delivery Kanban status to `In review` unless `blocked-question` is also present |
| Issue or PR has `blocked-question` | Assigns Robert for human decision |
| Issue has `needs-claude-action` and does not have `blocked-question` | Prepares a Claude handoff comment and removes `needs-claude-action` |
| PR has `needs-claude-action` | Prepares a Claude handoff comment and removes `needs-claude-action` |

Ownership is assignment-first for assignable GitHub actors. Use the issue assignee to show whether Codex/Robert, Copilot, or a human owns the next action. Claude uses GitHub Web UI agent assignment rather than a normal issue assignee exposed through the assignees API. Use the Project `Status` field for `Backlog`, `Ready`, `In progress`, `In review`, and `Done`. Labels should describe exceptional states or explicit action requests, not normal ownership.

`needs-claude-action` is a router trigger, not a durable waiting state. GitHub Actions removes it after posting the handoff so the same issue or PR can be routed again later by re-adding the label. The durable Claude waiting state is the handoff comment plus Project status until Robert assigns the Claude agent through the GitHub Web UI.

Reverse handoff from Claude, Copilot, or a human implementer should use normal GitHub state: leave the exact blocker or review request in a comment, then add `blocked-question` or `needs-codex-review`. Assign Robert only for a real human decision. `needs-codex-review` uses label and board status only because Codex is not exposed as a normal GitHub assignee in this repository.

Required setup:

- `PROJECT_SYNC_TOKEN` repository secret with access to the user-owned FPS Delivery Kanban Project.
- Copilot coding agent enabled for the repository/account.

Safety notes:

- Closed issues take precedence over routing labels and are synced to `Done`.
- `blocked-question` prevents automated Claude handoff routing and syncs the board back to `Backlog`.
- `active-coordination` is not an implementation trigger.
- Copilot assignment is manual unless GitHub's own Copilot assignment flow is used directly.
- Claude routing is handoff-only. Manual Claude invocation remains available when the prepared prompt is worth the token cost.
- `claude-ready` is legacy and should not be used for new routing.
- Implementers should add `needs-codex-review` when they finish and should remove stale ready/action labels when permitted.

### What CI checks

1. **.NET build and test** — restore, build, and test `code/server/FPS.sln` against .NET 10 in Release configuration on Ubuntu.
2. **Generated API client stale-check** — `./tools/check-api-client-stale.sh` runs after the build. It re-captures OpenAPI from each in-process service and diffs against the committed `code/clients/typescript/openapi/*.json` and `code/clients/typescript/src/*.d.ts`. CI fails if either differs, so the generator must be re-run before merge whenever a public API contract changes.

### Branch protection

Branch protection is not encoded in this repository; it is a GitHub repository setting. Once workflow names are stable, point the required check at the `build-and-test` job from `CI` so PRs cannot merge to `master` until that check passes. Keep the job name stable when editing `ci.yml` so this configuration does not need to be re-wired.

---

## Commit Workflow

Every commit must follow this sequence:

```
1. Run ./tools/validate.sh   ← must pass before staging
2. git diff                  ← review what changed
3. git status --short        ← confirm what is staged
4. git commit                ← pre-commit hook re-runs validate.sh
```

**Hard rules:**
- Never use `git commit --no-verify`
- Never force push (`--force` / `--force-with-lease`)
- Use zsh/macOS-compatible syntax only — no bash-specific constructs

### Large refactor commits

The LLM PR reviewer may return `REQUEST_CHANGES` for large dead-code removal refactors because it cannot distinguish intentional deletion from accidental. `validate.sh` (build + tests) is always the hard gate.

For approved large refactors, set `PR_REVIEW_SKIP=1` to skip the OpenAI call while `validate.sh` (build + tests) still runs in full:

```sh
# Commit
PR_REVIEW_SKIP=1 git commit -m "your message"

# Push (run via ! in Claude Code to avoid PreToolUse interference)
! PR_REVIEW_SKIP=1 git push -u origin your-branch
```

Use only for deliberate, pre-verified refactors. The env var is checked inside `pr-review.mjs` — it has no effect on any other hook.

---

## GitHub Pages Documentation Site

The project documentation is published from the `docs/` folder to GitHub Pages. The site is intentionally **not** built with Jekyll. It is a static [Docsify](https://docsify.js.org/) site served directly by the browser.

### How the site works

| File | Purpose |
|---|---|
| `docs/index.html` | Loads Docsify and points it at the wiki content. |
| `docs/Home.md` | Default landing page for the documentation site. |
| `docs/_sidebar.md` | Main navigation used by Docsify. |
| `docs/.nojekyll` | Disables GitHub Pages Jekyll processing. |
| `.github/workflows/docs.yml` | Publishes the `docs/` folder to GitHub Pages. |

GitHub Pages normally supports Jekyll processing, but FPS does not need it. The `.nojekyll` file is required so GitHub Pages serves files exactly as they exist in `docs/`. This avoids Jekyll ignoring underscore-prefixed files such as `_sidebar.md`, which Docsify needs for navigation.

### Publishing flow

1. Documentation changes are committed under `docs/`.
2. The GitHub Actions workflow `.github/workflows/docs.yml` runs on pushes to the configured publishing branch.
3. The workflow uploads the `docs/` folder as the Pages artifact.
4. GitHub Pages serves `docs/index.html`, and Docsify loads the Markdown pages in the browser.

### Local preview

Docsify can be previewed by serving the `docs/` folder with any static file server. For example:

```sh
npx docsify-cli serve docs
```

or:

```sh
npx http-server docs
```

Then open the printed local URL in a browser.

### Editing rules

- Put article content in Markdown files under `docs/`.
- Update `docs/_sidebar.md` when adding important new pages.
- Keep `docs/.nojekyll` in place.
- Do not add Jekyll front matter, layouts, collections, or plugins unless the documentation platform is intentionally changed away from Docsify.
- Avoid relying on build-time transforms; GitHub Pages serves the checked-in files directly.

---

## Build Status and CI

Repository health should be visible on the public entry points:

| Signal | Workflow |
|---|---|
| CI badge | `.github/workflows/ci.yml` |
| Docs badge | `.github/workflows/docs.yml` |

The CI badge is the red/green signal for backend build and test health. The docs badge shows whether the documentation site was deployed successfully.

### CI strategy

CI should run for pull requests and pushes to `master` when relevant build inputs change:

- `code/**`
- `tools/**`
- `.github/workflows/**`
- generated API client paths once `API001` is merged

CI should also support:

- `workflow_dispatch` for manual maintainer runs;
- a weekly scheduled run to catch SDK, dependency, and environment drift.

After the workflow names are stable, repository branch protection should require the CI build/test check before merging implementation PRs.

### API client stale check

After `API001` lands, CI should run the generated API client stale-check script as part of the build. This prevents backend OpenAPI changes from being merged without updated generated client artifacts.

The stale check should be self-contained in a clean checkout and should select the same .NET SDK path as the local validation tools.

---

## tools/validate.sh

Runs automatically as a pre-commit hook and can be called manually before staging.

```sh
./tools/validate.sh
```

**What it checks:**

| Step | What |
|---|---|
| `dotnet restore` | All packages resolve |
| `dotnet build` | Solution compiles without errors |
| `dotnet test` | All tests pass (skipped tests are fine) |
| Tracked artifacts | Fails if `bin/` or `obj/` folders are committed |
| Staged secrets | Fails if filenames matching `*.env`, `secret`, `password`, `token`, `private*key` are staged |

The script targets `code/server/FPS.sln` and must be run from the repo root.

---

## tools/review-permission.sh

Runs automatically on every `PermissionRequest` event via `.claude/settings.json`. Claude Code calls it before asking you to approve a tool. The script can grant or deny the request; when it needs human confirmation, it exits without structured output so the normal permission prompt is shown.

```sh
# Hook configuration (.claude/settings.json)
PermissionRequest → ./tools/review-permission.sh
```

The script reads the pending request as JSON on stdin. For automatic decisions it emits Claude Code's `PermissionRequest` JSON shape:

```json
{
  "hookSpecificOutput": {
    "hookEventName": "PermissionRequest",
    "decision": {
      "behavior": "allow"
    }
  }
}
```

For human confirmation cases it writes the reason to stderr and exits without stdout. `PermissionRequest` hooks do not support the `permissionDecision: "ask"` output used by `PreToolUse`.

### Decision rules

**Auto-allow** — granted immediately, no prompt:

| Category | Examples |
|---|---|
| Read-only tools | `Read`, `Glob`, `Grep` |
| Safe git commands | `git status`, `git diff`, `git log`, `git branch`, `git show` |
| Build/test commands | `dotnet test`, `dotnet build`, `dotnet restore` |
| Shell reads | `ls`, `find`, `cat`, `head`, `tail`, `echo`, `pwd`, `wc` |
| Safe GitHub CLI | `gh run *`, `gh pr view *`, `gh repo view *` |

**Hard block** — denied with explanation:

| Pattern | Reason |
|---|---|
| `rm -rf` / `rm -fr` | Use targeted deletion |
| `sudo` | Elevated privileges require explicit justification |
| `chmod -R 777` | Use minimum required permissions |
| `git push --force` / `-f` | Force push is prohibited |
| `--no-verify` | Fix the hook failure, never bypass |
| `DROP TABLE` | Use a migration instead |
| `delete_migration` / `remove_migration` | Create a reversing migration |
| Editing `.env`, `secret*`, `password*`, `token*`, `private*key*` | Sensitive files require manual review |
| `rm *test*` / `rm *spec*` | Removing test files is blocked |

**Human confirmation** — leaves the normal permission prompt in place and writes a note:

| Pattern | Reason |
|---|---|
| Editing `*Auth*`, `*Security*`, `*Payment*`, `*Billing*` files | Confirm test coverage before proceeding |

### Extending the rules

Edit `tools/review-permission.sh` directly. The file uses `case` statements with zsh glob patterns — add new `deny`, `allow`, or `ask` calls in the appropriate section. Commit the change so the rules are shared across the team.

---

## .claude/settings.json

Tracked in git (`.claude/**` is excluded but `.claude/settings.json` is re-included). Contains project-wide Claude Code hooks applied to all contributors:

```json
{
  "hooks": {
    "PermissionRequest": [
      {
        "hooks": [{ "type": "command", "command": "./tools/review-permission.sh", "timeout": 10 }]
      }
    ]
  }
}
```

Use `/hooks` in Claude Code to inspect or temporarily disable hooks for the current session.
