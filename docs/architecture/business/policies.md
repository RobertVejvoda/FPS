# Business Policies

Business policies define target behavior. Tenant-specific configuration may adjust permitted values, but cannot bypass audit, tenant isolation, or employee-safe communication rules.

| Policy | Rule | Owner | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant readiness gate | Employees can use customer-ready booking only when tenant identity, administrator access, policy, location/capacity, profile facts, notification, audit, DataHub/read-model path, and durable tenant storage are ready. | Product / Tenant administrator | Placeholder | [Business Process Flows](/business-layer/business-process-flows), [Tenant Storage Contract](/production/tenant-storage-contract) |
| Authenticated tenant context | Tenant and user identity come from authenticated context, never from request body. | Product / Security | Partial | [Booking API Contract](/business-layer/booking-api-contract), [Security Model](/security/security-model) |
| Company-car priority | Company-car employees are allocated before Tier 2 where policy requires it. Tier 1 overflow is rejected because it indicates configuration drift. | Product / Tenant policy owner | Partial | [Allocation Rules](/business-layer/allocation-rules) |
| Weighted fairness | Non-company-car allocation uses recent successful non-company-car allocation count and active penalties. Every eligible Tier 2 request keeps a non-zero weight unless policy excludes it before lottery. | Product / Tenant policy owner | Partial | [Allocation Rules](/business-layer/allocation-rules) |
| Slot matching | Allocation must satisfy tenant, location, date, time slot, vehicle/resource capability, accessibility, EV/charger, reserved-only, and active/inactive resource constraints. | Product / Tenant policy owner | Partial | [Allocation Rules](/business-layer/allocation-rules) |
| Zone preference fallback | Preferred zone and team default zone guide placement after eligibility/fairness selection, but do not normally make an eligible employee lose allocation when another compatible resource exists. | Product / Tenant policy owner | Placeholder | [Allocation Rules](/business-layer/allocation-rules) |
| Same-day support | Employees can request same-day parking when tenant policy allows it. v1 immediate allocation rejects when no matching capacity exists; same-day waitlist is future policy scope. | Product owner | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) |
| Late cancellation | Late cancellation starts after allocation in v1; no additional hours-before-start threshold applies unless tenant policy later changes it. | Product owner | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| No-show detection | No-show automation runs only when tenant policy enables it and a valid usage confirmation source exists. | Product / Tenant policy owner | Placeholder | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Penalty ledger | Booking owns v1 booking-related penalty records. Penalties are idempotent by source event ID and penalty type. | Product owner | Placeholder | [Allocation Rules](/business-layer/allocation-rules) |
| HR cancellation | HR/facility users can cancel tenant-scoped requests only with a required reason. Affected employees must receive notification. | Product / Tenant administrator | Placeholder | [Business Process Flows](/business-layer/business-process-flows) |
| Draw trigger control | Employees must not trigger Draw. Authorized HR/facility/admin Draw triggers must be explicit, tenant-scoped, idempotent, and audited. | Product / Security | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Next Draw visibility | Next scheduled Draw time must be available to readers and shown in UI where employees or HR need to understand allocation timing. | Product owner | Placeholder | [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Mandatory operational notifications | Critical booking, allocation, rejection, cancellation, reallocation, no-show, and HR-action notifications cannot be disabled by user preference. | Product / Compliance | Partial | [Notification](/business-layer/notification) |
| Employee-safe explanation | Employees see stable business reason codes and clear text, not seeds, internal weights, stack traces, or hidden diagnostics. | Product / Security | Partial | [Booking Reason Codes](/business-layer/booking-reason-codes), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Audit append-only evidence | Booking decisions, Draw attempts, privileged actions, policy changes, readiness transitions, and sensitive access produce append-only evidence. | Product / Compliance | Partial | [Audit](/business-layer/audit) |
| DataHub read model | Customer-facing operational insight must come from durable tenant-scoped read models, not volatile in-memory state. | Product / Architecture owner | Placeholder | [DataHub](/application-layer/datahub) |
| Billing deferred | Billing, invoice generation, payment collection, subscription enforcement, and employee parking charges are not part of the customer-ready target. | Product sponsor | Deferred | [Commercialisation](/strategy-layer/commercialisation), [Billing](/business-layer/billing) |

## Policy Gaps To Track

- Tenant readiness gate depends on physical Customer/tenant storage.
- HR and administrator actions need UI and authorization validation.
- DataHub read models need durable implementation before customer reporting can be trusted.
- Zone preference/resource map behavior is documented but needs customer-ready implementation proof.
- No-show and usage confirmation need tenant capability decisions before enabling automation.
