# Agent Cooperation Guide
> How Codex (Product Owner) and Claude (Implementer) work together on FPS

---

## Roles

| Agent | Role | Responsibilities |
|-------|------|-----------------|
| **Codex** | Product Owner | Write task specs, validate results, open PRs, manage backlog |
| **Claude** | Implementer | Read specs, implement, write results, flag blockers |

Neither agent makes architectural decisions alone — those go to `docs/decisions.md` and require human approval.

---

## Repository Structure

```
fps/
├── CLAUDE.md                        ← Claude reads this every session (keep lean)
├── .codex/
│   ├── tasks/
│   │   ├── active/                  ← Codex drops tasks here
│   │   │   └── TASK-001.md
│   │   └── done/                    ← Archived after completion
│   │       └── TASK-001.md
│   └── results/
│       └── TASK-001.md              ← Claude writes results here
└── docs/
    ├── architecture.md              ← System design, component map
    ├── conventions.md               ← Coding standards, agreed patterns
    ├── constraints.md               ← What never to touch and why
    └── decisions.md                 ← ADR log of key architectural choices
```

---

## CLAUDE.md Structure (keep under 100 lines)

```markdown
# FPS — Claude Session Index

## Quick Reference
- Architecture: docs/architecture.md
- Conventions: docs/conventions.md
- Constraints: docs/constraints.md
- Decisions log: docs/decisions.md

## Session Rules
- Read the active task before touching any code
- Ask before scanning large directories
- Write result to .codex/results/TASK-XXX.md when done
- If a decision is made mid-session, append it to docs/decisions.md immediately
- Prefer targeted edits over full-file rewrites
- Confirm with human if task scope seems too broad
```

---

## Task Lifecycle

```
Codex writes TASK-XXX.md (status: draft)
       ↓
Codex sets status: ready
       ↓
Claude picks up, sets status: in-progress
       ↓
Claude implements + writes RESULT-XXX.md
       ↓
Codex validates RESULT-XXX.md
       ↓
Pass → Codex opens PR, moves task to done/
Fail → Codex updates task with feedback, status: ready (loop)
```

---

## Task File Schema (`TASK-XXX.md`)

```markdown
# TASK-001: [Short title]

## Status
`draft` | `ready` | `in-progress` | `review` | `done`

## Owner
Codex (spec) · Claude (implementation)

## Context
_Why this task exists. Link to ADR or docs/decisions.md if relevant._

## Acceptance Criteria
- [ ] Criterion 1 (testable, specific)
- [ ] Criterion 2
- [ ] Criterion 3

## Scope
**In:** what Claude should touch
**Out:** what Claude must not touch

## Files Expected to Change
- `src/Services/XyzService.cs` — reason
- `src/Models/Xyz.cs` — reason

## Dependencies
- Blocked by: TASK-000 (or none)
- Related: TASK-002

## Notes for Claude
_Edge cases, gotchas, preferred approach._

---
_Created: YYYY-MM-DD · By: Codex_
_Updated: — · By: —_
```

---

## Result File Schema (`RESULT-XXX.md`)

```markdown
# RESULT-001: [Same title as task]

## Status
`done` | `partial` | `blocked`

## Summary
_One paragraph: what was done and why._

## Files Changed
- `src/Services/XyzService.cs` — what changed
- `src/Models/Xyz.cs` — what changed

## Acceptance Criteria Review
- [x] Criterion 1 — passed
- [x] Criterion 2 — passed
- [ ] Criterion 3 — not implemented, reason: ...

## Deviations from Spec
_Anything Claude did differently and why. Empty if none._

## Suggested Follow-up Tasks
_Optional. Empty if none._

## Blockers / Issues
_Empty if clean._

---
_Implemented: YYYY-MM-DD · By: Claude_
_Reviewed: — · By: Codex_
```

---

## Environment Setup

### Claude Code
```bash
# Install
npm install -g @anthropic-ai/claude-code

# Configure model (use Sonnet to save costs, Opus for hard problems only)
claude config set model claude-sonnet-4-20250514

# Run a task non-interactively
claude --print "Process task in .codex/tasks/active/TASK-001.md"
```

### .claudeignore (put in repo root)
```
node_modules/
dist/
build/
bin/
obj/
*.generated.*
.git/
```

### Codex
- Connect to GitHub repo via Codex UI
- Give it access to `.codex/tasks/` and `docs/` folders
- Instruct it to never edit `src/` directly — only write task specs

---

## GitHub Actions — Automated Trigger (optional)

Triggers Claude automatically when Codex drops a new task:

```yaml
# .github/workflows/claude-implement.yml
name: Claude Implement Task

on:
  push:
    paths:
      - '.codex/tasks/active/**.md'

jobs:
  implement:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Install Claude Code
        run: npm install -g @anthropic-ai/claude-code

      - name: Find active task
        id: task
        run: |
          TASK=$(ls .codex/tasks/active/*.md | head -1)
          echo "task=$TASK" >> $GITHUB_OUTPUT

      - name: Run Claude
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          claude --print "Process task in ${{ steps.task.outputs.task }}" \
            > .codex/results/$(basename ${{ steps.task.outputs.task }} | sed 's/TASK/RESULT/')

      - name: Commit result
        run: |
          git config user.name "claude-bot"
          git config user.email "claude@fps"
          git add .codex/results/
          git commit -m "result: $(basename ${{ steps.task.outputs.task }})"
          git push
```

---

## Cost Management

| Model | Use for |
|-------|---------|
| `claude-sonnet-4` | All routine implementation tasks |
| `claude-opus-4` | Hard architectural problems only |

- Keep `CLAUDE.md` under 100 lines — it's read every session
- Use `Scope / Files Expected to Change` in tasks to pre-focus Claude
- Run `/compact` in long interactive sessions before context grows too large
- Codex (OpenAI) and Claude (Anthropic) bill separately — no cross-charges unless you explicitly call one API from the other

## Cross-Agent Validation

Claude can also validate Codex-authored work when the change is high-impact enough to justify a second model read.

Use Claude validation for architecture, security, privacy, GDPR, authentication, secrets, audit, billing, tenant isolation, production operations, cross-service plans, or substantial specs that will drive non-trivial implementation.

Do not use Claude validation by default for typo fixes, tracker updates, Home/sidebar maintenance, link fixes, or mechanical cleanup where local review and validation are enough.

Validation prompts should be narrow: ask Claude to find gaps, contradictions, implementation risks, and missing acceptance criteria. Claude should report findings first and should not rewrite or broaden scope unless Codex or Robert explicitly requests edits.

---

## Bootstrapping Checklist

- [ ] FPS repo pushed to GitHub
- [ ] `.codex/tasks/active/`, `.codex/tasks/done/`, `.codex/results/` folders created
- [ ] `CLAUDE.md` written at repo root
- [ ] `docs/architecture.md` populated from GAD
- [ ] `.claudeignore` committed
- [ ] `claude config set model claude-sonnet-4-20250514` run locally
- [ ] TASK-001 written and validated manually before enabling GitHub Action
- [ ] `ANTHROPIC_API_KEY` added to GitHub repo secrets (for CI use)
