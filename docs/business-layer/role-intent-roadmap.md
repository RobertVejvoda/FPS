# Role Intent Roadmap

FairSpot role experiences should start from what the actor came to accomplish, not from internal service boundaries. The product should hide technical terms such as tenant, GUID, API URL, storage key, or raw user ID from employee and HR workflows unless the role is explicitly technical.

This roadmap turns the personas into implementation direction for web and mobile experiences.

## Role Entry Points

| Role | First intent | Default entry point | Product principle |
| --- | --- | --- | --- |
| Employee | Know whether they have a spot, request one quickly, and understand the outcome. | My Spots. | Self-service, fast default path, clear reason, no hidden internals. |
| HR / Facilities | Know what needs attention and help employees without manually operating every request. | Attention queue. | Manage exceptions and policy, not routine matching. |
| Auditor / Compliance | Prove what happened and whether it followed policy. | Evidence timeline. | Immutable business evidence with controlled identity resolution. |
| Customer / IT Admin | Make the company environment ready and keep integrations healthy. | Readiness and setup. | Technical setup belongs here, not in employee screens. |
| Executive Sponsor | Understand whether the process creates business value. | Management summary. | Trends, trust, utilization, and effort saved. |

## Employee Experience

Employee intent is covered by [My Spots UX](./my-spots-ux). The employee default should answer:

- Do I have a spot today or tomorrow?
- Can I request a spot for the next useful day?
- Why was my request allocated, rejected, waiting, or needing attention?
- What action can I still take?

Employee screens must not show technical tenant terminology, raw IDs, GUIDs, API URLs, hidden Draw seeds, lottery weights, or other employees.

## HR / Facilities Experience

HR and facilities should not start from a generic reporting page. Their first screen should be an attention queue that highlights operational work:

- failed, delayed, or incomplete Draws;
- high-demand days and shortage risk;
- rejected requests grouped by safe reason;
- requests needing manual correction or support follow-up;
- unusual cancellation, no-show, or repeated exception patterns;
- capacity mismatches such as overused zones, underused pools, or missing capabilities.

The support workflow should let HR:

- search by employee display value or safe request reference when permitted;
- open a request lifecycle with policy snapshot and employee-safe explanation;
- see whether an outcome came from eligibility, capacity, cutoff, policy, or allocation result;
- perform policy-allowed manual corrections with a required reason;
- link to audit evidence for disputes.

Facilities-focused users should additionally manage the resource map:

- locations, zones, capacity pools, and spaces;
- EV, accessibility, reserved, company-car, and temporary closure capabilities;
- resource-map publication with validation and audit;
- utilization and shortage trends by location, zone, and capability.

Future slice direction:

| Slice | Purpose | Notes |
| --- | --- | --- |
| `HR001` HR Attention And Support Console | Daily attention queue, safe request lookup, lifecycle support view, and exception links. | First HR experience slice after employee My Spots stabilizes. |
| `FAC001` Resource Map Operations | Facilities-facing map/capacity maintenance, closures, and validation. | Separate from HR policy tuning when map complexity grows. |
| `HR002` Policy Impact Preview | Show likely operational impact before policy/capacity changes are published. | Requires reporting projections to be credible. |

## Auditor / Compliance Experience

Auditors need evidence, not operational dashboards. The default view should be a business activity timeline backed by the Audit service.

The auditor should be able to:

- filter by date range, action, actor hash, actor type, entity type, request reference, policy version, result, reason code, and trace ID;
- open lifecycle views for booking requests, Draw attempts, policy changes, manual corrections, retention jobs, actor-resolution lookups, and erasure workflows;
- verify that manual actions have a reason, actor category, timestamp, and result;
- export safe evidence with stable columns and tenant scoping;
- request actor resolution only through a permissioned action with reason capture and an audit record.

Auditor screens must not expose raw technical logs, stack traces, secrets, raw user IDs, full lottery ordering, weights, seeds, or unrelated employee private data. Trace IDs are support correlation metadata only.

Future slice direction:

| Slice | Purpose | Notes |
| --- | --- | --- |
| `AUD008` Audit Evidence Timeline | Role-specific audit timeline with lifecycle grouping and safe filters. | Builds on existing Audit business activity model. |
| `AUD009` Controlled Actor Resolution | Reasoned actor-hash resolution flow with audit of the lookup itself. | Required before named audit investigation views. |
| `AUD010` Audit Evidence Export | Safe CSV/JSON evidence export for compliance review. | Must follow privacy and formula-injection protections. |

## Customer / IT Admin Experience

Admin intent is readiness and controlled setup. Technical tenant concepts may appear here, but employee-facing terminology should still use company/business language where possible.

The admin should be able to:

- see whether the company instance is ready for live use;
- configure identity provider, role mapping, active admin, company display name, and branding;
- verify profile facts, policy, locations, booking smoke tests, notifications, audit, reporting, and object storage;
- manage integration endpoints and storage boundaries without exposing secrets;
- understand setup gaps and suggested next actions;
- keep all sensitive setup changes audited.

Future slice direction:

| Slice | Purpose | Notes |
| --- | --- | --- |
| `ADM001` Admin Readiness Console | Consolidated readiness checks, blockers, and next actions. | Extends the current tenant admin readiness view. |
| `ADM002` Company Branding And Display Context | Admin-managed company name, logo, and safe business labels for employee UI. | Builds on tenant branding/storage docs. |
| `ADM003` Integration Health And Setup | IdP, notification, storage, and service reachability health with safe diagnostics. | Technical logs stay in observability tools. |

## Executive Sponsor Experience

The executive sponsor is not expected to operate FairSpot daily. The product should provide concise evidence of business value:

- HR effort saved or avoided;
- request volume, allocation rate, unmet demand, and utilization trend;
- fairness trend and repeated-shortage indicators;
- no-show and cancellation trends;
- capacity pressure by location;
- employee trust or support-volume indicators when feedback/support data exists.

Future slice direction:

| Slice | Purpose | Notes |
| --- | --- | --- |
| `MGT001` Management Summary | High-level business value dashboard for sponsor review. | Later than operational HR/audit/admin foundations. |
| `MGT002` Capacity Planning Pack | Evidence for lease, policy, or resource expansion decisions. | Should avoid employee-level detail by default. |

## AI Assistance Direction

AI should be considered only where it creates operational value from existing evidence:

- summarize why a day needs HR attention;
- draft employee-safe explanation text from reason codes and policy snapshots;
- suggest that HR review capacity or policy when repeated evidence supports it;
- identify anomalies for human review.

AI must not allocate spots, override policy, resolve identities, invent reasons, or expose hidden allocation internals. All suggested actions remain human-approved and audited where sensitive.
