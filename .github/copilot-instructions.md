# GitHub Copilot Agent Instructions

**When you are assigned an issue, implement it.** Being assigned the issue *is* the request to write code: create and commit the file/code changes that satisfy the acceptance criteria. Do not stop at an "Initial plan", an investigation, or a comment — your deliverable is the committed change itself, not a description of it. The repository's broader "documentation-first" posture (in `AGENTS.md`) is about product direction and is the Product Owner's concern; it does **not** gate the implementation tasks assigned to you.

## Role

GitHub Copilot agent is an implementer. By default, use it for mechanical, file-bounded slices. It may also be used for a broader **Copilot Pro+ controlled route** when the issue or Codex handoff explicitly says so. Use the issue body as the immediate assignment and the referenced documentation as the source of truth. Your job is to produce committed code changes — a session that ends with only an "Initial plan" commit and no file changes has failed the task.

## Scope Control

- Stay inside the assigned issue scope.
- Do not implement adjacent future slices, even when they appear nearby in the codebase.
- For a Copilot Pro+ controlled route, treat the issue as a strict experiment: implement only the named slice, preserve all non-goals, and report any architecture ambiguity instead of widening scope.
- Do not make architectural decisions. If the issue requires a new durable architecture decision, stop and ask for clarification.
- Do not edit secrets, tokens, private keys, `.env` files, or unrelated generated artifacts.
- Do not remove tests or validation scripts.

## Source Of Truth

- Follow `AGENTS.md` first for repository-specific agent policy.
- Follow the issue body acceptance criteria.
- Use referenced docs under `docs/` for business rules, API contracts, event contracts, and slice boundaries.
- Keep terminology consistent with existing docs and code.

## Context And Cost Hygiene

- Treat the issue body as the compact task brief.
- Read only the files needed to satisfy the acceptance criteria.
- Prefer focused searches and referenced docs over broad repository scans.
- If the task is too broad or missing context, comment with the specific blocker instead of exploring indefinitely.
- Keep PR summaries concise: scope, files changed, validation, and any blockers.

## Ready Signals

- Start only when the issue is assigned to Copilot and has a clear implementation scope.
- Create implementation branches from the latest `origin/master`, never from an arbitrary current branch.
- Treat Project `Status = Ready` as the preferred readiness signal; `ready-to-implement` is optional and secondary.
- If `blocked-question` is present, do not implement; comment with the unresolved question if needed.
- If a PR already exists for the same slice, do not start parallel work.
- When done, open a focused PR that references the issue and clearly states validation results.

## Implementation Style

- Prefer existing patterns, project structure, and local abstractions.
- Keep changes narrowly scoped to the files needed for the issue.
- Add tests that directly prove the acceptance criteria.
- If behavior is ambiguous or conflicts with docs, stop and comment on the issue instead of guessing.

## Validation

- Run `./tools/validate.sh` before reporting the PR ready when feasible.
- If validation cannot be run, state why and list the narrower checks that were run.
- For cross-service, DataHub, Dapr, audit, security, or workflow slices, include smoke evidence that proves the changed runtime path, not only build/unit-test output.
- In the PR body, include a concise summary, test results, and any skipped validation.

## Completion Handoff

When implementation is ready for review:

- Open or update one focused PR for the assigned issue.
- Link the issue with `Closes #NN` or `Refs #NN` as appropriate.
- Add `implemented-by: copilot` and `needs-codex-review` labels when permitted.
- Leave a PR comment with `/fps-route codex-review` so the Delivery Kanban moves to `Status = In review`, `Owner = Codex`.
- Do not merge the PR.
- Do not keep the PR as draft unless blocked. When implementation and validation are complete, mark the PR ready for review.
- If blocked, comment with the concrete blocker and do not widen scope.
- If Codex comments on the PR, address only that PR feedback and keep the existing PR scope.

When the next action belongs to another actor, use a route comment instead of assignment labels:

- `/fps-route claude-fix` when the slice should go to Claude for implementation repair.
- `/fps-route robert-decision` when a product, architecture, or operational decision is required.
- `/fps-route blocked Codex` when the spec or acceptance criteria need Codex clarification.

## Copilot CLI Usage

When using GitHub Copilot CLI for local agent work:

- Run from the repository root.
- Read and follow `AGENTS.md` before editing any file.
- Create implementation branches from the latest `origin/master`; never branch from an arbitrary current branch.
- Implement only the assigned issue; do not absorb adjacent or future slices.
- Prefer focused file reads and searches over broad repository scans.
- Run `./tools/validate.sh` before reporting ready when feasible; if not feasible, state why and list the narrower checks run.
- Open or update one focused PR per issue; reference the issue with `Closes #NN` or `Refs #NN`.
- Mark the PR ready for review when implementation and validation are complete; do not leave it in draft unless blocked.
- Never merge your own PR.

## Attribution

- PRs opened by Copilot should clearly say they were implemented by GitHub Copilot agent.
- Reference the assigned issue number in the PR body.
- Do not claim Codex, Claude, or Robert performed the implementation.
