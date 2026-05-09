# Development Tooling

Scripts in `tools/` automate quality gates and permission review for AI-assisted development.

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

For approved large refactors, the project owner may run commits and pushes directly from the terminal (`!` prefix in Claude Code) with `OPENAI_API_KEY` unset, which causes `pr-review.mjs` to skip its OpenAI call while `validate.sh` still runs. This pattern is for one-off, pre-verified changes only.

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

Runs automatically on every `PermissionRequest` event via `.claude/settings.json`. Claude Code calls it before asking you to approve a tool — the script can grant, deny, or escalate to you.

```sh
# Hook configuration (.claude/settings.json)
PermissionRequest → ./tools/review-permission.sh
```

The script reads the pending request as JSON on stdin and outputs a decision.

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

**Ask** — escalated to you with a note:

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
