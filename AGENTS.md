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
- Start every implementation branch from the latest `origin/master`, not from whatever branch is currently checked out. Fetch first, then branch from `origin/master` or switch to updated `master` before creating the work branch.

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
- Roles follow the **invocation, not the agent name**. **By default Codex is the Product Owner and reviewer** (`chatgpt-codex-connector`, via `@codex review`) — it keeps reviewing everyone else's PRs as normal. **Assigning Codex an issue** instead invokes its coding-agent / **implementer** role (`openai-code-agent`, opens a branch/PR), exactly as issue-assigning Claude (`anthropic-code-agent`) or Copilot (`copilot-swe-agent`) does. The invariant **Reviewer ≠ Implementer ≠ Merger** binds *per PR*, and the **only** restriction it places on Codex is that it must not **review or merge a PR it implemented itself** (such a PR is reviewed by Claude or a human) — Codex's default reviewer role is otherwise unchanged. Likewise Claude never reviews or merges its own implementation.
- Default Claude model for routine implementation: `claude-sonnet-4.6`. Escalate to Opus only for hard problems.
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
- PR ownership: whoever is invoked as the implementer opens the PR (Claude, Copilot, or Codex via issue assignment); review is done by a *different* party — `@codex review`, Claude, or a human. (The guide's fixed "Codex opens PRs" rule does not apply — roles are per-invocation.)
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

### Delivery automation: Copilot implement → Codex review → App-bot merge

The realised delivery pipeline (the "B2" model). Roles are functions and the one hard invariant is **Reviewer ≠ Implementer ≠ Merger**:

- **Implementer — GitHub Copilot coding agent** (`app/copilot-swe-agent`, billed as Copilot premium requests). A human assigns a `Ready` issue with one click ("Assign to Agent → Copilot"). This step stays human on purpose: **a GitHub App / installation token cannot assign the Copilot agent — only a user can** (Copilot is not surfaced to App tokens via `suggestedActors`; the App token sees only `anthropic-code-agent` and `openai-code-agent`). The click is also the deliberate "spend Copilot budget?" checkpoint.
- **Reviewer — Codex** (`chatgpt-codex-connector[bot]`). Codex reviews as a PR **comment** (not a formal approval) carrying a `Reviewed commit: <sha>` marker. Its "On PR open" auto-review **skips drafts**, and Copilot opens drafts, so `.github/workflows/agent-review-handoff.yml` posts `@codex review` when the PR is marked ready.
- **Merger — the "Fairspot Delivery Bot" GitHub App** (App ID `4339995`; per-repo secret `APP_PRIVATE_KEY` + variable `APP_ID`; perms Contents/Issues/Pull requests R&W). `.github/workflows/agent-auto-merge.yml` squash-merges as the App bot only when every guard passes — clean Codex verdict, Copilot author, same-repo, reviewed-SHA == head, `mergeable_state == clean`, and a **low-risk diff**. It is **OFF by default**; activate per repo with the `AUTO_MERGE_ENABLED=true` repository variable (set it `false` to emergency-stop).

**Codex has two invocation modes — role follows the invocation, not the name.** Assigning an *issue* to Codex (`openai-code-agent`) invokes it as an **Implementer** — it opens a branch/PR, exactly as assigning an issue to Claude (`anthropic-code-agent`) or Copilot (`copilot-swe-agent`) does. A `@codex review` comment (or configured auto-review) invokes `chatgpt-codex-connector` as the **Reviewer** — its default role. The restriction is narrow: Codex must **not** review or merge *the PR it implemented itself*; it still reviews every other PR as usual. When Codex is the implementer of a PR the automated pipeline deliberately does not apply — the auto-merge gate requires a Copilot author and skips Codex-authored PRs, and the review handoff only pings Codex for Copilot PRs, so it never asks Codex to review its own work. Route a Codex-authored PR's review to **Claude or a human**, and have a human merge it. The same holds for Claude- and human-authored PRs: reviewed by someone else and merged through the normal flow, never self-reviewed or self-merged. (Corollary: use *bounded, per-PR `@codex review`* requests for review work — assigning a review/triage task to Codex as an **issue** invokes the implementer channel instead and opens a WIP PR, as issue #842 showed.)

Guardrails:

- `.github/workflows/security-gate.yml` (`tools/security-gate.sh`) is a **required PR check** re-imposing the local agent deny-list — secret-bearing files, test deletion, sensitive code changed without tests, forbidden commands — in CI, because the cloud Copilot agent does not run the local Claude/Codex hook chain (`tools/llm-review.mjs`). Note: Copilot **does** run the repo's `.codex/hooks.json` **PermissionRequest** hook (`tools/review-permission.sh`) on its Ubuntu runner, so that hook must be cross-shell (`#!/usr/bin/env bash`, never zsh) — a zsh shebang there silently denied every Copilot edit and produced empty PRs (fixed in #833).
- `.github/CODEOWNERS` routes **high-risk paths** (CI/guards, auth, infra, deps, DB migrations, draw/fairness core) to a **human** reviewer; the auto-merge gate refuses those and branch protection's "Require review from Code Owners" backs it up. This is how architecture/security decisions keep human sign-off.

**Observability (so the pipeline is never a black box).** Every Copilot PR carries a single `pipeline:*` label the workflows swap at each transition — `pipeline:in-review` → `pipeline:auto-merging` → `pipeline:merged`, or `pipeline:needs-human` when the gate stops (mergeable ≠ clean, high-risk diff, or a rejected merge). The labels self-provision on first use and every label op is best-effort, so a labelling hiccup can never break the merge gate. The auto-merge gate also **posts its decision as a PR comment** (`🔀 Auto-merged …` / `⏸️ Not auto-merged: <reason> …`) on its **terminal** outcomes — a merge, or a `needs-human` stop (mergeable ≠ clean, high-risk diff, or a rejected merge) — and an `ERR` trap emits a `::warning::` if the gate hits an *unexpected* error. Scope it precisely: the label + decision comment cover those terminal outcomes and the `ERR` trap covers crashes, so a merge that *should* have happened can't fail with no trace. The gate's **early-guard `skip`s** — not-a-clean-verdict (fires on every unrelated comment), wrong author, fork, draft, empty, or a missing/stale `Reviewed commit` marker — deliberately log to the Actions run only and leave the label unchanged, to avoid commenting on every comment; those are expected no-ops, not silent failures.

**Editing the auto-merge gate — two silent-abort traps (learned the hard way, #839).** (1) GitHub's default `run:` shell on Linux is `bash -e {0}` — errexit is on by default (pipefail is *not*; that only comes with an explicit `shell: bash`, and this gate turns it on itself via `set -uo pipefail`). Because `-e` is active, any *unguarded* command that returns non-zero — especially a `VAR=$(… | grep …)` where the grep can legitimately not match — aborts the whole step with exit 1 and **no log**, before any `skip`/diagnostic runs. Guard such extractions with `|| true` and test the empty result explicitly. (2) Codex writes its freshness marker in **markdown** (`**Reviewed commit:** \`<sha>\``), so the reviewed-SHA regex must tolerate non-hex separators (`reviewed commit[^0-9a-f]{0,16}[0-9a-f]{7,40}`) between the label and the sha, not just `:`/space. Together these meant the gate died on the very first clean verdict and merged nothing — invisibly — until #839.

Per-repo setup, in order: (1) add the repo to Codex code-review preferences; (2) **create a Codex cloud environment** for it at `chatgpt.com/codex/cloud/settings/environments` — otherwise review fails with "To use Codex here, create an environment for this repo"; (3) install the Delivery Bot App and add `APP_ID` / `APP_PRIVATE_KEY`; (4) add a branch ruleset requiring `Security gate` + `CI` + Code Owner review and blocking force/direct pushes; (5) allow auto-merge + auto-delete head branches; (6) set `AUTO_MERGE_ENABLED=true`. Rolling out on `fairspot` first, then `fairspot-platform`, `atlas`, and `fairspot-architecture` (the architecture repo keeps a broad high-risk carve-out — most of it is human-merged by design).

### Implementer routing

Three agents can be invoked as **implementers** by assigning them an issue: **Claude** (`anthropic-code-agent`), the **GitHub Copilot agent** (`copilot-swe-agent`, billed under the GitHub subscription), and **Codex** (`openai-code-agent`). A spec can be routed to any of them. (Codex is also the default *reviewer* via `@codex review` — a different invocation of the same agent; the one that implements a given PR must not review it, so a Codex-implemented PR is reviewed by Claude or a human.) Default routing rule:

- **Issue placement first** — before creating, routing, splitting, or moving an issue, classify it as one of:
  - **Public open-core**: runtime, fairness, tenant self-administration, auth/security capability, self-hosted/BYOC capability, public product validation, or other work a self-hosted operator reasonably needs. Keep in `fairspot`.
  - **Private platform/commercial**: hosted operator plane, billing, usage metering as billing/pilot input, operator dashboard, commercial beta/onboarding funnel, hosted-only runbooks/evidence, marketing site, platform gateway/vhost config, or private platform service/web work. Put in `fairspot-platform`.
  - **Split**: issue bundles an open-core capability with hosted/operator/commercial work. Keep the capability slice public and create/move the platform slice private, with backlinks both ways.
  If classification is unclear, stop at a PO clarification comment instead of silently routing the issue.
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
If the PR is a draft, the implementer must mark it ready for review when the requested implementation and validation evidence are complete. Do not leave a completed PR in draft state.

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

Copilot-specific behavior is documented in `.github/copilot-instructions.md`. The accepted PR review and merge workflow for Copilot CLI—including how Codex reviews and merges under the same-account constraint—is documented in `docs/delivery-board.md` under **Copilot CLI Identity and PR Merge Workflow**.
