# Privacy Architecture

| Data Area | Purpose | Minimization | Rights / Retention Notes | Status |
| --- | --- | --- | --- | --- |
| Employee identity/profile facts | Booking eligibility, notification, audit actor mapping. | Store only facts needed for parking operation and role mapping. | Erasure workflow classifies delete/anonymise/pseudonymise/retain. | Partial |
| Vehicle facts | Booking vehicle selection, default vehicle preselection, and eligibility. | Employee-safe facts only in employee UI; avoid unnecessary license plate exposure. | Profile owns active/default vehicle facts. | Partial |
| Booking and allocation history | Operational lifecycle, fairness evidence, notifications, audit, and DataHub projections. | Employee sees own safe status/reasons only; HR sees tenant/location-scoped operational detail. | Retention must preserve business evidence where required. | Partial |
| Draw evidence | Reproducible allocation decisions, algorithm version, seed, ordered candidate sequence, counts, and safe explanations. | Seed/order/weights are audit-role details, not employee-facing data. | Retention must balance fairness dispute evidence and privacy. | Partial |
| Audit records | Compliance, dispute, privileged-action, and sensitive-access evidence. | Pseudonymised actors; PII mapping is restricted. | Mapping can be deleted/anonymised while audit evidence remains. | Partial |
| Notifications | User communication and delivery evidence. | Store only needed message/status data; DataHub stores status summaries only. | Retention and deletion jobs required before production. | Placeholder |
| DataHub read models | Management insight, HR/admin dashboards, report/export data. | Aggregate or role-safe detail depending on report; no raw audit payloads or unrelated employee-private data. | Projection retention and rebuild rules must preserve classification and tenant scope. | Placeholder |
| Technical telemetry | Incident diagnosis and operational support. | No secrets, tokens, raw user IDs, names, emails, license plates, or full request payloads. | Retention and access model are operator/client responsibilities. | Partial |

## Source Evidence

- [Data Privacy](/security/data-privacy)
- [Employee Data Erasure Workflow decision](/versions-and-decisions)
- [Security Model](/security/security-model)

## Privacy Rules

- Default employee views to own data only.
- Keep event payloads minimal and projection-oriented.
- Do not expose hidden lottery weights, seeds, candidate order, raw penalties, stack traces, or unrelated employee details to employees.
- Pseudonymise audit actors where possible so immutable evidence can remain useful after PII mappings are removed.
- Rights-request workflows must call service-owned APIs/activities; they should not edit another service's database directly.
- Backups cannot usually be rewritten safely; retention policy must define backup expiry and restore-time re-erasure controls.

## Visible Privacy Gaps

- Booking and notification retention jobs remain production-blocking.
- DataHub projection privacy shape must be finalized before customer-facing reports/exports are trusted.
- Profile/Booking erasure behavior needs durable-store smoke evidence.
- Customer privacy notices, controller/processor roles, subprocessors, DPA, DPIA, and legal review remain customer/legal responsibilities.
