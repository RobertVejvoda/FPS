# Change Control

| Change Type | Review Needed | Decision Record Needed | Notes |
| --- | --- | --- | --- |
| Minor clarification | Normal docs review | No | No behavior or architecture change. |
| New target architecture statement | Architecture review | Usually | Record if durable or controversial. |
| New service/data/integration boundary | Architecture review | Yes | Update Information Systems and gap analysis. |
| Security/privacy-impacting change | Architecture and security review | Yes | Include risk acceptance if not fully mitigated. |
| Deployment profile change | Architecture and operations review | Usually | Update Technology and Security pages. |
| Waiver to principle/control | Architecture board review | Yes | Include review or expiry date. |

## Change Sources

| Source | Handling |
| --- | --- |
| Chat requirement | Convert to issue body, issue comment, or source-of-truth doc before routing implementation. |
| GitHub issue | Treat as delivery source of truth when it has scope, acceptance criteria, validation, owner, and implementer where relevant. |
| Pull request | Review against linked issue, affected architecture artifacts, validation evidence, and known gaps. |
| Architecture decision | Record durable changes in [Versions and Decisions](/versions-and-decisions). |
| Customer or security finding | Record the finding, affected artifact, risk owner, and whether it is a blocker, gap, waiver, or follow-up slice. |

## Routing Rules

- Routine implementation goes through issues and PRs.
- Architecture-significant changes update the relevant `docs/architecture/` artifact in the same PR or a linked docs PR.
- Labels do not own workflow state; Project fields `Status`, `Owner`, and `Implementer` are the durable workflow signals.
- A merged PR closes the implementation loop only when validation evidence is recorded and linked issues are moved to `Done`.

## Robert TODOs

- Robert TODO: confirm when architecture updates may be merged directly versus requiring a separate review branch and PR.
- Robert TODO: confirm how formal customer change requests should be represented if a pilot customer starts giving feedback.
