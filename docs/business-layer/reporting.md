# Reporting Business

> Status: legacy transitional capability. Reporting remains the business-facing report surface, but new durable cross-service read models should be owned by [DataHub](../application-layer/datahub.md).

Reporting helps HR, facilities, tenant administrators, and client sponsors understand whether FairSpot is operating fairly and efficiently. Reporting must stay manager-safe: it explains parking operations and outcomes without exposing hidden lottery internals, raw audit payloads, secrets, or unnecessary employee-private data.

Reporting is no longer the target owner for PostgreSQL projection storage. If the component remains, its durable responsibility should be limited to report catalog/configuration metadata such as report names, allowed filters, export definitions, column policies, and role-specific presentation rules. DataHub owns the event-fed PostgreSQL read models that reports query.

Reporting is split into three product layers.

| Layer | Audience | Purpose | Current direction |
| --- | --- | --- | --- |
| Operational reporting | HR, facilities, tenant administrators | Monitor parking demand, allocation outcomes, cancellations, no-shows, rejections, fairness, and utilization. | Fixed report catalog and safe exports. |
| Audit/evidence reporting | Auditors, security, client IT, support | Explain why sensitive actions happened and prove audit retention/integrity/export evidence. | Owned by Audit; Reporting may link to audit evidence but must not duplicate raw audit payloads. |
| Business/customer reporting | Client sponsor, product owner, commercial owner | Show adoption, HR effort saved, utilization trends, fairness trend, support volume, and future billing/support metrics. | Later slice after operational reporting is credible. |

## Operational Report Catalog

The first reporting catalog should be small and fixed. FairSpot should not build a custom report designer until client feedback proves it is needed.

| Report | Questions Answered | Safe Contents |
| --- | --- | --- |
| Daily parking summary | How many requests, allocations, rejections, cancellations, confirmations, and no-shows happened for the selected tenant/date/location range? | Aggregated counts, allocation rate, cancellation rate, no-show rate, location/date grouping. |
| Fairness trend | Is allocation becoming fairer over time? | Pseudonymised or bucketed fairness metrics, request/allocation counts, recent-allocation distribution, no raw employee names or hidden weights. |
| Utilization by location and slot type | Which locations or capability pools are over/under-used? | Capacity, allocated count, utilization percentage, slot capability/category, date/time grouping. |
| Rejection/cancellation/no-show reasons | Why are requests failing or capacity being released? | Reason-code counts and safe employee-visible reason groups. |
| Employee-safe allocation outcome export | What outcomes can a manager share or investigate without exposing lottery internals? | Tenant-scoped rows with date, location, status, reason code, safe request reference, and pseudonymised/allowed employee reference only where role policy permits. |

## Privacy Rules

- Reporting reads tenant-scoped projections only.
- Reporting must not expose lottery seeds, random ordering, raw weights, hidden eligibility diagnostics, or unrelated employee private data.
- Employee identifiers in manager reports should be pseudonymised or omitted unless a role-specific business rule explicitly allows named operational views.
- Audit/evidence exports belong to Audit. Reporting may link to audit records or export IDs, but it must not return raw audit payloads.
- CSV exports must be deterministic and protected against spreadsheet formula injection.
- Report errors must not include secrets, tokens, raw payloads, or cross-tenant identifiers.

## Export Rules

Supported v1 export format is CSV. PDF, Excel, custom report builders, scheduled report delivery, and external BI feeds are later extensions.

CSV exports must:

- be tenant-scoped;
- include generated timestamp and selected filters where useful;
- use stable column ordering;
- escape values correctly;
- neutralize spreadsheet formula injection for values beginning with `=`, `+`, `-`, or `@`;
- include only fields approved for the selected report;
- be covered by tests for empty data, normal data, tenant isolation, and privacy-safe shaping.

## Out Of Scope For Operational Reporting

- billing, invoices, revenue, and payment reporting;
- system health, infrastructure incidents, backup status, and operational telemetry reporting;
- raw security login/session reports;
- broad user activity monitoring;
- custom report designer;
- scheduled report delivery;
- direct SIEM or BI integration;
- audit integrity and retention evidence implementation.

Those areas may become separate Billing, Operations, Security, Audit, or BI slices.

## Slice Direction

| Slice | Purpose | Notes |
| --- | --- | --- |
| `REPORT001` Reporting Read Models | Build tenant-scoped parking summary and fairness projections from Booking events. | Done. |
| `WEB006` Web Reporting Dashboard And CSV Export | Expose first web reporting views. | Done. |
| `REPORT002` Reporting Dashboards And Exports | Dashboard-facing aggregates and summary CSV export. | Done by backend/web combination; keep as completed parent/history. |
| `REPORT003` Operational Report Catalog And Export Hardening | Add or harden the fixed operational report catalog, utilization/reason/outcome exports, privacy-safe shaping, and CSV hardening. | Valid only as report-surface/catalog work; do not add Reporting-owned PostgreSQL projections. |
| Future business reporting | Sponsor-level adoption, saved HR effort, satisfaction/support, and commercial metrics. | Separate from operational reporting. |
| Future BI/export integration | External BI dataset/feed or scheduled reports. | Only after client demand is clear. |

## DataHub Direction

Reporting should become a consumer of DataHub read models rather than owning new durable projection storage itself.

Target direction:

- owning business services keep accepting changes and owning operational state;
- domain events feed DataHub projections;
- report screens and exports query approved DataHub read models;
- Reporting-specific projection code becomes obsolete once equivalent DataHub projections exist;
- any remaining Reporting persistence stores only report catalog/configuration metadata, not operational event projections.

This preserves the business reporting surface while moving CQRS/read-model ownership into a clearer architecture component.
