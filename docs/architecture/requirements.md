# Architecture Requirements

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Target |
| ADM Phase | Requirements Management |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | Before customer architecture review |

| ID | Requirement | Source | Affected Views | Status |
| --- | --- | --- | --- | --- |
| AR-001 | FairSpot must preserve tenant isolation across API, persistence, events, audit, and read models. | Security and customer integration decisions | Security, data, application | Draft |
| AR-002 | Booking writes remain owned by Booking; cross-service read models are projected through DataHub. | DataHub direction decision | Information systems, data | Draft |
| AR-003 | Hosted pilot must expose only intended public surfaces through the selected ingress/WAF profile. | Customer-first deployability | Technology, security | Draft |
| AR-004 | Employees must see safe, understandable booking and allocation information without hidden lottery internals. | My Spots / UX decisions | Business, application | Draft |
| AR-005 | Architecture artifacts must separate target state from current-state evidence and known gaps. | TOGAF repository decision | Governance, architecture states | Draft |

## Source Evidence

- [Business Requirements](/business-layer/requirements)
- [Requirements Traceability](/requirements-traceability)
- [Gap Analysis](/architecture/architecture-states/gap-analysis)
