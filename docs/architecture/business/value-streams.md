# Value Streams

These value streams describe the target operating model that should be visible to customers and implementation teams.

| Value Stream | Trigger | Main Actors | Outcome | Status |
| --- | --- | --- | --- | --- |
| Tenant goes live | Company wants to pilot or run FairSpot. | Tenant administrator, FairSpot operator, customer sponsor, identity administrator, HR/facilities. | Tenant is configured, identity is mapped, administrator access works, policy/locations/capacity exist, readiness is visible, and audit evidence exists. | Partial |
| Employee gets parking outcome | Employee needs parking. | Employee, Booking, Configuration, Profile, Notification, Audit, DataHub. | Employee submits a future or same-day request and receives an understandable `Pending`, `Allocated`, `Rejected`, `Cancelled`, `Used`, `NoShow`, or `Expired` outcome. | Partial |
| Fair Draw allocates scarce capacity | Request window closes or authorized user triggers Draw. | Booking processor, HR/facilities, Configuration, Profile, Notification, Audit, DataHub. | Scarce capacity is allocated using company-car priority, weighted fairness, slot matching, recorded seed/order evidence, employee-safe reasons, and notifications. | Partial |
| HR manages operational exceptions | Cancellation, support question, failed setup, or disputed outcome occurs. | HR/facilities user, tenant administrator, Booking, Notification, Audit. | Privileged user can inspect queues, see next Draw timing, cancel with reason, trigger controlled Draw action, notify affected employees, and preserve evidence. | Placeholder |
| Administrator manages tenant operations | Tenant setup, policy, role, or readiness change is needed. | Tenant administrator, system administrator, Customer, Configuration, Identity, Audit. | Administrator sees a role-appropriate default view for tenant setup, policies, users, readiness, and platform operations. | Placeholder |
| Auditor reviews fairness | Dispute, compliance review, or management question occurs. | Auditor, HR/facilities, Audit, DataHub, Booking. | Reviewer can inspect decision evidence and operational summaries without exposing unrelated personal data or hidden employee-facing diagnostics. | Partial |
| Customer evaluates deployment | Client IT or sponsor reviews readiness. | Product sponsor, client IT/operator, security reviewer, tenant administrator. | Hosting profile, WAF/auth/secret/backup/observability expectations, known gaps, and transition plan are clear. | Partial |
| Pilot feedback is handled | Pilot user or evaluator reports an issue or suggestion. | Employee/evaluator, HR/facilities, product/support operator. | Authenticated feedback is tenant-scoped, reviewed, optionally answered, and audited where sensitive. | Deferred |

## Value Stream Priority

| Priority | Value Streams |
| --- | --- |
| Customer-ready P0 | Tenant goes live; Employee gets parking outcome; Fair Draw allocates scarce capacity; HR manages operational exceptions; Administrator manages tenant operations. |
| Customer-ready P1 | Auditor reviews fairness; Customer evaluates deployment. |
| Pilot support P2 | Pilot feedback is handled. |
| Deferred | Billing/payment value streams. |

## Missing Diagrams

- Capability-to-value-stream map is still a placeholder.
- End-to-end employee request and Draw value stream diagram is still a placeholder.
- HR/admin operations value stream diagram is still a placeholder.
