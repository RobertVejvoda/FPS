# Architecture Board

The FairSpot architecture board is lightweight and can operate asynchronously through reviewed documentation and pull requests.

The board is not a standing committee for every change. It is the escalation and approval mechanism for architecture-significant decisions, target/baseline changes, security/privacy risk acceptance, and exceptions to approved principles.

## Responsibilities

- approve architecture principles and standards;
- approve target and transition architecture baselines;
- review architecture-significant changes;
- approve or reject waivers;
- resolve conflicts between business, technology, security, and delivery concerns;
- ensure durable decisions are recorded in [Versions and Decisions](/versions-and-decisions);
- trigger re-review when assumptions, risks, or constraints change.

## FairSpot Board Roles

| Board Role | FairSpot Actor |
| --- | --- |
| Sponsor | Robert |
| Architecture Owner | Robert + Codex |
| Business Owner | Robert |
| Security / Privacy Lead | Codex review; Robert accepts risk |
| Delivery Lead | Codex |
| Implementer input | Claude / Copilot / Human when implementation feasibility matters |

Claude or Copilot may provide evidence and implementation concerns, but must not approve its own implementation as architecture-complete.

## Decision Rights

| Decision | Accountable | Consulted |
| --- | --- | --- |
| Product/domain scope | Robert | Codex, implementation agents when feasibility matters |
| Architecture principles | Robert | Codex, security/privacy reviewer where relevant |
| Target architecture baseline | Robert | Codex, client IT/security reviewer when available |
| Security/privacy risk acceptance | Robert | Codex, client security/privacy reviewer when available |
| Delivery sequencing | Codex / Robert | Implementer for effort/risk feedback |
| Implementation acceptance | Codex or human reviewer | Implementer provides validation evidence |

## Operating Rules

- Most board work happens through pull requests, issue comments, and architecture review notes.
- Durable decisions are recorded in [Versions and Decisions](/versions-and-decisions).
- Architecture-significant PRs should link the relevant issue, requirement, decision, or architecture artifact.
- Implementers may challenge unclear or risky specs before coding; that challenge is input, not approval.
- Customer-facing architecture claims require explicit review of gaps and assumptions.

## Robert TODOs

- Robert TODO: confirm whether any external/customer role should be represented in the board for pilot review.
- Robert TODO: confirm whether board approval should be recorded as PR approval, issue comment, or a separate approval table for customer-facing artifacts.
