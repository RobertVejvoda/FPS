# Work Packages

Work packages group implementation and documentation work that closes architecture gaps. Delivery details remain in GitHub issues and the FPS Delivery Kanban.

## Work Package Register

| Work Package | Objective | Closes / Supports | Expected Evidence | Status |
| --- | --- | --- | --- | --- |
| WP-001 Architecture Repository Alignment | Bring FairSpot `docs/architecture/` into structural alignment with the TOGAF template. | GAP-005, [issue #383](https://github.com/RobertVejvoda/fairspot/issues/383) | New pages, sidebar, artifact register, link validation. | In progress |
| WP-002 Customer-Ready Hosted Pilot | Prove durable state, DataHub inbox/projections, hosted profile, WAF/auth, and observability evidence. | GAP-001, GAP-002, GAP-003, GAP-008 | Persistence tests, DataHub projection health, hosted smoke runbook, Dapr hardening evidence. | Planned |
| WP-003 Role-Centered UX | Validate role-specific default workspaces and customer-visible operational flows. | GAP-004 | Draw schedule/status, HR operations, My Spots detail, admin readiness views. | Planned |
| WP-004 Contract Evidence Consolidation | Make generated/source-of-truth API and event contracts discoverable from architecture. | GAP-007, [issue #377](https://github.com/RobertVejvoda/fairspot/issues/377) | API contract links, event ownership table, generated client evidence. | Planned |
| WP-005 Diagram Refresh | Replace placeholders with Robert-approved target views and diagrams. | GAP-006 | Updated views/diagrams index and ArchiMate/source diagrams. | Robert-owned |

## Delivery Trace

| Work Package | Candidate Issues / Sources |
| --- | --- |
| WP-001 | [Issue #383](https://github.com/RobertVejvoda/fairspot/issues/383) |
| WP-002 | DATAHUB003/[#332](https://github.com/RobertVejvoda/fairspot/issues/332), DATAHUB004/[#335](https://github.com/RobertVejvoda/fairspot/issues/335), DATAHUB005/[#334](https://github.com/RobertVejvoda/fairspot/issues/334), OPS014/[#378](https://github.com/RobertVejvoda/fairspot/issues/378), hosted smoke/WAF issues |
| WP-003 | DRAW005/[#340](https://github.com/RobertVejvoda/fairspot/issues/340), DRAW004/[#339](https://github.com/RobertVejvoda/fairspot/issues/339), HR operations/[#310](https://github.com/RobertVejvoda/fairspot/issues/310), My Spots detail/[#323](https://github.com/RobertVejvoda/fairspot/issues/323) |
| WP-004 | API007/[#377](https://github.com/RobertVejvoda/fairspot/issues/377) |
| WP-005 | Views and diagrams placeholders in [Views and Diagrams](/architecture/views/) |
