# Privacy Architecture

| Data Area | Purpose | Minimization | Rights / Retention Notes |
| --- | --- | --- | --- |
| Employee identity/profile facts | Booking eligibility, notification, audit actor mapping. | Store only facts needed for parking operation. | Erasure workflow classifies delete/anonymise/pseudonymise/retain. |
| Vehicle facts | Booking vehicle selection and eligibility. | Employee-safe facts only in employee UI. | Profile owns active/default vehicle facts. |
| Booking and allocation history | Operational lifecycle and fairness evidence. | Employee sees own safe status/reasons only. | Retention must preserve business evidence where required. |
| Audit records | Compliance and dispute evidence. | Pseudonymised actors; PII mapping is restricted. | Mapping can be deleted/anonymised while audit evidence remains. |
| Reports/read models | Management insight. | Aggregate or role-safe detail depending on report. | DataHub projections must preserve classification and tenant scope. |

## Source Evidence

- [Data Privacy](/security/data-privacy)
- [Employee Data Erasure Workflow decision](/versions-and-decisions)
- [Security Model](/security/security-model)
