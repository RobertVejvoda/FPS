# Gap Analysis

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Gap Analysis |
| **Baseline Version** | Current State v0.1 |
| **Target Version** | Customer-Ready Target v0.1 |
| **ADM Phase** | Phases E/F + Requirements Management |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | - |
| **Next Review** | Before hosted pilot |

This page compares current-state evidence with the customer-ready target architecture.

| Gap ID | Area | Baseline Version | Target Version | Baseline State | Target State | Gap | Impact | Work Package | Owner | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GAP-001 | Customer | Current State v0.1 | Customer-Ready Target v0.1 | Customer service has in-memory repositories for some tenant state. | Customer tenant state needed for onboarding/readiness is durable. | Durable Customer state is incomplete. | Hosted pilot cannot rely on restart-safe tenant setup. | Future Customer durable state slice. | Claude/Codex | Open |
| GAP-002 | DataHub | Current State v0.1 | Customer-Ready Target v0.1 | DataHub project skeleton exists. | Event-fed read models support customer-facing reports/dashboards. | Projection catalog and first projections need implementation. | Reporting and management views may remain partial. | DataHub projection slices. | Claude/Codex | Open |
| GAP-003 | Hosted pilot security | Current State v0.1 | Customer-Ready Target v0.1 | Cloudflare/WAF and NAS deployment docs exist. | Public domain deployment is smoke-tested with WAF, auth, secrets, and no internal exposure. | End-to-end hosted validation remains needed. | Public demo risk until validated. | Hosted smoke and WAF validation. | Codex/Robert | Open |
| GAP-004 | Role-centered UI | Current State v0.1 | Customer-Ready Target v0.1 | Employee, HR, and admin views exist or are emerging. | Each role lands on a clear role-specific workspace with safe terminology and expected actions. | Role-specific polish and validation remain ongoing. | Customer evaluation can be confusing. | UX/customer evaluation slices. | Claude/Codex | Open |
| GAP-005 | Architecture validation | Current State v0.1 | Customer-Ready Target v0.1 | Architecture docs are distributed across existing layer pages. | TOGAF map, artifact status, and gap register are reviewed and baselined. | Formal architecture validation is not complete. | Harder to prove architecture readiness to client IT. | Architecture review pass. | Codex/Robert | Open |

## Gap Status Values

| Status | Meaning |
| --- | --- |
| Open | Gap is known and unresolved. |
| Planned | Gap has an accepted resolution path. |
| In Progress | Work is actively closing the gap. |
| Closed | Evidence shows the gap is closed. |
| Accepted | Gap remains by explicit risk or scope decision. |
