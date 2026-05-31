# Transition Architectures

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.2 |
| **Architecture State** | Transition |
| **ADM Phase** | Phases E/F |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | On milestone change |

Transition architectures describe staged movement from current-state evidence toward the customer-ready target.

In TOGAF terms, this page is the architecture-controlled home for Opportunities and Solutions plus Migration Planning. The public [Roadmap](/roadmap) stays business-readable; this page maps that roadmap into architecture work package groups, target transitions, risks, and exit criteria.

| Transition | From Version | To Version | Capabilities Added | Risks | Exit Criteria |
| --- | --- | --- | --- | --- | --- |
| T1 Customer-ready docs and architecture governance | Current State v0.1 | Customer-Ready Target v0.1 | TOGAF map, artifact register, architecture states, public docs cleanup, requirements cross-phase traceability, legacy evidence disposition. | Documentation can look complete before validation catches up. | Gaps are explicit and client-facing docs avoid maintainer-only workflow. |
| T2 Hosted pilot readiness | Current State v0.1 | Hosted Pilot Target v0.1 | Durable customer state, DataHub read models, Cloudflare/WAF profile, smoke runbooks, role-centered UI, Dapr hardening, authoritative diagrams. | Runtime and security gaps can block public domain deployment. | Hosted smoke path passes and known exceptions are accepted. |
| T3 Customer production handoff | Hosted Pilot Target v0.1 | Customer-Owned Production Target v1.0 | Client-owned identity, secrets, backup/restore, observability, operations, support boundary, deployment-specific component profiles. | Client environment differences can require profile-specific work. | Handoff checklist and recovery evidence are accepted. |

## Work Package Groups

| Work Package Group | Closes Gaps | Target Transition | Notes |
| --- | --- | --- | --- |
| Architecture repository consolidation | GAP-005, GAP-006 | T1 | Finish artifact consistency, diagram placeholders, customer-facing approval rules, and legacy source disposition. |
| Customer durable tenant state | GAP-001 | T2 | Persist tenant lifecycle, readiness, identity setup metadata, first admins, parking bootstrap, and object storage metadata. |
| DataHub first projections | GAP-002 | T2 | Implement event inbox, projection checkpoints, booking/draw/readiness projections, and projection health. |
| Hosted WAF and smoke evidence | GAP-003 | T2 | Prove public domain login/API/web, no internal exposure, WAF/rate-limit rules, backup/restore/reset, and log review. |
| Role-centered UX validation | GAP-004 | T2 | Validate Employee, HR/facility, tenant admin, system admin, auditor, and sponsor default views. |
| Contract consolidation | GAP-007 | T2 | Link generated API/OpenAPI and event contracts from Information Systems Architecture. |
| Dapr hosted hardening | GAP-008 | T2 | Prove component scopes, secret scopes, resiliency, mTLS/Sentry where supported, state encryption, and outbox behavior. |
| Customer-owned production handoff | GAP-001 through GAP-008 as applicable | T3 | Convert pilot evidence into client-owned deployment guidance and acceptance checklist. |

## Roadmap Trace

| Roadmap Area | Architecture Transition | Work Package Groups | Current Implementation Cards |
| --- | --- | --- | --- |
| Customer-ready docs and architecture governance | T1 | Architecture repository consolidation | Documentation migration slices, diagram refresh, architecture review/baseline. |
| Customer data and readiness | T2 | Customer durable tenant state | `DATA010` / issue #317 and Customer readiness follow-ups. |
| DataHub/read models | T2 | DataHub first projections | `DATAHUB003` / #332, `DATAHUB004` / #335, `DATAHUB005` / #334. |
| Hosted public-domain readiness | T2 | Hosted WAF and smoke evidence, Dapr hosted hardening | `OPS012` / #316, `SEC010` / #315, `OPS013` / #314, `OPS014` / #378. |
| Role-centered customer UX | T2 | Role-centered UX validation | `DRAW005` / #340, `DRAW004` / #339, HR operations / #310, UX follow-ups. |
| Contract evidence | T2 | Contract consolidation | `API007` / #377. |
| Customer-owned production | T3 | Customer-owned production handoff | OPS/BYOC and handoff issues after hosted pilot evidence. |
