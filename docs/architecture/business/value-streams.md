# Value Streams

These value streams describe the target operating model that should be visible to customers and implementation teams.

| Value Stream | Trigger | Main Actors | Outcome | Status |
| --- | --- | --- | --- | --- |
| Tenant goes live | Company wants to pilot or run FairSpot. | Tenant administrator, FairSpot operator, customer sponsor, identity administrator, HR/facilities. | Tenant is configured, identity is mapped, administrator access works, policy/locations/capacity exist, readiness is visible, and audit evidence exists. | Partial |
| Employee gets parking outcome | Employee needs parking. | Employee, Booking, Configuration, Profile, Notification, Audit, DataHub. | Employee submits a future or same-day request and receives an understandable `Pending`, `Allocated`, `Rejected`, `Cancelled`, `Used`, `NoShow`, or `Expired` outcome. | Partial |
| Fair Draw allocates scarce capacity | Request window closes or authorized user triggers Draw. | Booking processor, HR/facilities, Configuration, Profile, Notification, Audit, DataHub. | Scarce capacity is allocated using company-car priority, weighted fairness, slot matching, recorded seed/order evidence, employee-safe reasons, and notifications. | Partial |
| HR manages operational exceptions | Cancellation, support question, failed setup, or disputed outcome occurs. | HR/facilities user, tenant administrator, Booking, Notification, Audit. | Privileged user can inspect queues, see next Draw timing, cancel with reason, trigger controlled Draw action, notify affected employees, and preserve evidence. | Placeholder |
| Facilities keeps capacity accurate | Parking layout, closure, capability, or capacity changes. | Facilities coordinator, tenant administrator, Configuration, Audit, DataHub. | Locations, zones, spaces, capacity pools, closures, and capabilities are validated, published, audited, and visible in utilization/shortage summaries. | Placeholder |
| Administrator manages tenant operations | Tenant setup, policy, role, or readiness change is needed. | Tenant administrator, system administrator, Customer, Configuration, Identity, Audit. | Administrator sees a role-appropriate default view for tenant setup, policies, users, readiness, and platform operations. | Placeholder |
| Auditor reviews fairness | Dispute, compliance review, or management question occurs. | Auditor, HR/facilities, Audit, DataHub, Booking. | Reviewer can inspect decision evidence and operational summaries without exposing unrelated personal data or hidden employee-facing diagnostics. | Partial |
| Sponsor evaluates business value | Pilot review, management question, or renewal/rollout decision occurs. | Executive sponsor, HR manager, DataHub, Audit. | Sponsor sees concise evidence for HR effort avoided, demand, utilization, allocation rate, unmet demand, fairness trend, no-show/cancellation trend, and capacity pressure. | Placeholder |
| Customer evaluates deployment | Client IT or sponsor reviews readiness. | Product sponsor, client IT/operator, security reviewer, tenant administrator. | Hosting profile, WAF/auth/secret/backup/observability expectations, known gaps, and transition plan are clear. | Partial |
| Pilot feedback is handled | Pilot user or evaluator reports an issue or suggestion. | Employee/evaluator, HR/facilities, product/support operator. | Authenticated feedback is tenant-scoped, reviewed, optionally answered, and audited where sensitive. | Deferred |

## Value Stream Priority

| Priority | Value Streams |
| --- | --- |
| Customer-ready P0 | Tenant goes live; Employee gets parking outcome; Fair Draw allocates scarce capacity; HR manages operational exceptions; Facilities keeps capacity accurate; Administrator manages tenant operations. |
| Customer-ready P1 | Auditor reviews fairness; Sponsor evaluates business value; Customer evaluates deployment. |
| Pilot support P2 | Pilot feedback is handled. |
| Deferred | Billing/payment value streams. |

## Value Stream To Capability Trace

| Value Stream | Primary Capabilities | Missing Evidence |
| --- | --- | --- |
| Tenant goes live | Tenant onboarding and readiness; customer/tenant administration; configuration and parking policy; profile and eligibility facts; audit. | Durable Customer storage, readiness UI/API evidence, tenant object storage smoke evidence. |
| Employee gets parking outcome | Employee parking request; booking lifecycle management; notification; employee-safe explanation; profile/default vehicle. | Final employee UI validation, next Draw visibility, same-day request validation evidence. |
| Fair Draw allocates scarce capacity | Fair allocation Draw; configuration policy; profile facts; audit; notification; DataHub projections. | Workflow execution diagram, multi-instance-safe trigger evidence, DataHub projection evidence. |
| HR manages operational exceptions | HR/facility operations; booking lifecycle; notification; audit; operational insight. | HR operations UI validation, HR cancellation with reason and notification evidence. |
| Facilities keeps capacity accurate | Facilities resource-map operations; configuration and parking policy; audit; operational insight. | Resource-map workspace, publication validation, closure handling, and utilization by zone/capability evidence. |
| Administrator manages tenant operations | Tenant administration; configuration; identity/profile setup; readiness; audit. | Tenant administrator default workspace and durable Customer persistence. |
| Auditor reviews fairness | Audit evidence; DataHub projections; safe report views. | Audit/privacy view and role-specific evidence timeline validation. |
| Sponsor evaluates business value | Management summary; DataHub projections; audit evidence; operational insight. | Sponsor-safe management summary and DataHub-backed trend evidence. |
| Customer evaluates deployment | Deployment profile; WAF/security; observability; backup/restore; gap analysis. | Hosted smoke run evidence and customer-facing architecture review. |
| Pilot feedback is handled | Pilot feedback; notification/audit where sensitive; support review. | Robert decision on whether this belongs before pilot. |

## Value Stream Boundaries

- Employee value streams must use employee-safe language and must not expose lottery seeds, raw weights, other employees, tenant IDs, GUIDs, or internal service errors.
- HR and administrator value streams may show deeper operational state, but still need tenant-scoped authorization and audit for privileged actions.
- Facilities value streams can expose physical capacity and closure detail, but must not bypass allocation policy or employee privacy rules.
- Sponsor value streams should aggregate by tenant/location/time period and avoid employee-level detail by default.
- Auditor value streams use business audit evidence, not raw technical logs.
- Operator value streams belong primarily in Technology/Operations; Business Architecture only states the business readiness concern.
- Billing/payment value streams are intentionally excluded from the customer-ready target.

## Missing Diagrams

- Capability-to-value-stream map is still a placeholder.
- End-to-end employee request and Draw value stream diagram is still a placeholder.
- HR/admin operations value stream diagram is still a placeholder.
- Facilities resource-map operations diagram is still a placeholder.
- Sponsor management summary value stream diagram is still a placeholder.
- Robert TODO: provide or approve the business value stream diagrams after the source content stabilizes.
