# Governance

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.2 |
| **Architecture State** | Cross-cutting |
| **ADM Phase** | Preliminary + Phases G/H |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | On governance change |

Governance is established in Preliminary, executed in Phase G, and evolved in Phase H.

For FairSpot, governance is intentionally lightweight. It should make architecture decisions reviewable and traceable without creating a separate bureaucracy from the delivery process. GitHub issues, pull requests, the delivery board, and architecture repository pages are the operating records.

## Governance Model

| Area | Rule | Source Of Truth |
| --- | --- | --- |
| Architecture ownership | Robert is accountable for architecture direction. Codex maintains architecture docs and prepares reviewable recommendations. | This repository, [RACI](/architecture/governance/raci) |
| Durable decisions | Durable architecture decisions are recorded in [Versions and Decisions](/versions-and-decisions). | Decision log |
| Architecture artifacts | Governed artifacts declare status, version, owner, accountable owner, and review trigger. | [Artifact Register](/architecture/artifact-register) |
| Delivery work | Implementation work is tracked through GitHub issues and the FPS Delivery Kanban. | [Delivery Board](/delivery-board) |
| Implementer routing | Claude and Copilot can implement, but they do not approve their own PRs. | Delivery board and PR review |
| Exceptions | Exceptions to approved principles, controls, or standards are recorded as waivers or durable decisions. | [Waivers](/architecture/governance/waivers), [Versions and Decisions](/versions-and-decisions) |

## Preliminary Scope

Preliminary contains the architecture method and governance rules, not product behavior. Product value, stakeholders, requirements, business processes, applications, technology, and security details belong in later ADM-aligned sections.

| Preliminary Content | New Repository Location | Migration Status |
| --- | --- | --- |
| Architecture principles | [Principles](/architecture/principles) | Partial |
| Architecture board | [Architecture Board](/architecture/governance/architecture-board) | Partial |
| RACI | [RACI](/architecture/governance/raci) | Partial |
| Artifact lifecycle | [Artifact Lifecycle](/architecture/governance/artifact-lifecycle) | Partial |
| Architecture review gates | [Architecture Review](/architecture/governance/architecture-review) | Partial |
| Architecture contract | [Architecture Contract](/architecture/governance/architecture-contract) | Draft |
| Change control | [Change Control](/architecture/governance/change-control) | Partial |
| Waivers | [Waivers](/architecture/governance/waivers) | Draft |
| Source-evidence migration rules | [Architecture Migration Tracker](/architecture/migration-tracker) | Partial |

## Contents

- [Architecture Board](/architecture/governance/architecture-board)
- [RACI](/architecture/governance/raci)
- [Artifact Lifecycle](/architecture/governance/artifact-lifecycle)
- [Architecture Review](/architecture/governance/architecture-review)
- [Architecture Contract](/architecture/governance/architecture-contract)
- [Change Control](/architecture/governance/change-control)
- [Waivers](/architecture/governance/waivers)

## Requirement Coverage

Governance interprets central requirements from [Requirements Management](/architecture/requirements) as review, conformance, waiver, and change-control expectations. Governance does not redefine the requirement; it defines how FairSpot proves or accepts it.

| Requirement | Governance Interpretation | Impacted Artifacts | Evidence / Gap |
| --- | --- | --- | --- |
| AR-005 / AR-016 | Architecture artifacts must separate target state, current evidence, gaps, metadata, ownership, and review triggers. | [Artifact Lifecycle](/architecture/governance/artifact-lifecycle), [Architecture Review](/architecture/governance/architecture-review), [Artifact Register](/architecture/artifact-register) | Customer-facing approval state remains Draft until review. |
| AR-001 / AR-023 | Tenant isolation, backup/restore, export, and privileged operations require explicit review evidence or accepted residual risk. | [Architecture Contract](/architecture/governance/architecture-contract), [Architecture Review](/architecture/governance/architecture-review), [Waivers](/architecture/governance/waivers) | Release 1 validation and hosted readiness evidence must record remaining risks. |
| AR-003 / AR-014 / AR-022 | Hosted ingress, WAF, Dapr hardening, and smoke evidence must be reviewed before customer traffic. | [Architecture Board](/architecture/governance/architecture-board), [Architecture Review](/architecture/governance/architecture-review), [Change Control](/architecture/governance/change-control) | Hosted-profile evidence and any waivers remain open. |
| AR-024 | Role-safe output shapes for sponsor, facilities, HR support, and DataHub projection views need approval before external use. | [Architecture Contract](/architecture/governance/architecture-contract), [Architecture Review](/architecture/governance/architecture-review) | Role-specific output review is not complete. |

## Robert TODOs

- Robert TODO: confirm whether the lightweight board membership is enough for customer-facing architecture review.
- Robert TODO: confirm which governance artifacts must be approved before moving Customer-Ready Target v0.1 from Draft to In Review.
- Robert TODO: confirm whether customer/external review needs a signed approval record or whether PR/review history is sufficient for now.
