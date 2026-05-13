# GitHub Copilot Agent Instructions

FPS is documentation-first unless the assigned issue explicitly asks for implementation work.

## Role

GitHub Copilot agent is an implementer for mechanical, file-bounded slices. Use the issue body as the immediate assignment and the referenced documentation as the source of truth.

## Scope Control

- Stay inside the assigned issue scope.
- Do not implement adjacent future slices, even when they appear nearby in the codebase.
- Do not make architectural decisions. If the issue requires a new durable architecture decision, stop and ask for clarification.
- Do not edit secrets, tokens, private keys, `.env` files, or unrelated generated artifacts.
- Do not remove tests or validation scripts.

## Source Of Truth

- Follow `AGENTS.md` first for repository-specific agent policy.
- Follow the issue body acceptance criteria.
- Use referenced docs under `docs/` for business rules, API contracts, event contracts, and slice boundaries.
- Keep terminology consistent with existing docs and code.

## Implementation Style

- Prefer existing patterns, project structure, and local abstractions.
- Keep changes narrowly scoped to the files needed for the issue.
- Add tests that directly prove the acceptance criteria.
- If behavior is ambiguous or conflicts with docs, stop and comment on the issue instead of guessing.

## Validation

- Run `./tools/validate.sh` before reporting the PR ready when feasible.
- If validation cannot be run, state why and list the narrower checks that were run.
- In the PR body, include a concise summary, test results, and any skipped validation.

## Attribution

- PRs opened by Copilot should clearly say they were implemented by GitHub Copilot agent.
- Reference the assigned issue number in the PR body.
- Do not claim Codex, Claude, or Robert performed the implementation.
