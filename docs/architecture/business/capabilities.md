# Business Capabilities

This capability map is the shared-resource customer-ready target, proven first through parking. It does not model every historical feature idea from the legacy functional architecture.

## Capability Map

| Capability | Target Description | Priority | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant onboarding and readiness | Prepare a company tenant, identity mapping, locations, policy, initial data, support contacts, timezone, and launch readiness checks. | P0 | Partial | [Tenant Onboarding](/business-layer/tenant-onboarding), [Business Process Flows](/business-layer/business-process-flows) |
| Customer / tenant administration | Maintain tenant lifecycle state, tenant identity, support contacts, and readiness evidence durably. | P0 | Placeholder | [Customer](/business-layer/customer), [Tenant Storage Contract](/production/tenant-storage-contract) |
| Employee resource request | Let employees request a limited resource for future Draw allocation or same-day allocation using authenticated tenant/user context; parking is the current implemented resource type. | P0 | Partial | [Booking](/business-layer/booking), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Profile and eligibility facts | Resolve employee active status, vehicle, company-car, accessibility, reserved-space, and location facts needed by policy. | P0 | Partial | [Profile](/business-layer/profile), [Allocation Rules](/business-layer/allocation-rules) |
| Resource configuration and policy | Maintain tenant policy, locations, time slots, capacity/resource maps, zones, capability rules, and Draw schedule; parking policy is the current implementation contract. | P0 | Partial | [Configuration](/business-layer/configuration), [Parking Policy Configuration](/business-layer/parking-policy-configuration) |
| Fair allocation Draw | Allocate scarce resource capacity using auditable priority rules, weighted fairness, resource matching, and deterministic evidence; parking includes company-car priority and slot matching today. | P0 | Partial | [Allocation Rules](/business-layer/allocation-rules), [Business Process Flows](/business-layer/business-process-flows) |
| Booking lifecycle management | Support cancellation, reallocation, usage confirmation, no-show, expiry, penalties, and employee-safe reason codes. | P0 | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Booking Reason Codes](/business-layer/booking-reason-codes) |
| HR / facility operations | Give HR and facility managers role-specific queues, support views, next Draw visibility, controlled manual Draw, and cancellation workflows. | P0 | Placeholder | [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [My Spots UX](/business-layer/my-spots-ux) |
| Facilities resource-map operations | Let facilities maintain locations, zones, capacity pools, spaces, temporary closures, and resource capabilities with validation and audit. | P0 | Placeholder | [Personas](/business-layer/personas), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [Business Requirements](/business-layer/requirements) |
| Administrator operations | Give tenant and system administrators a different default workspace for tenant setup, policies, users, readiness, and platform operations. | P0 | Placeholder | [Roles](/business-layer/roles), [Tenant Onboarding](/business-layer/tenant-onboarding) |
| Notification | Notify affected users about request, allocation, rejection, cancellation, reallocation, usage, and operational events. | P0 | Partial | [Notification](/business-layer/notification) |
| Audit and compliance evidence | Preserve append-only, privacy-aware evidence for tenant setup, policy changes, booking decisions, Draw actions, manual actions, and sensitive access. | P0 | Partial | [Audit](/business-layer/audit), [Security](/security) |
| Operational insight and read models | Provide tenant-scoped demand, allocation, rejection, cancellation, no-show, fairness, and utilization summaries through DataHub-backed projections. | P1 | Placeholder | [Reporting](/business-layer/reporting), [DataHub](/application-layer/datahub) |
| Management summary | Give customer sponsors concise business-value evidence: HR effort avoided, demand, allocation rate, unmet demand, utilization, fairness trend, no-show/cancellation trend, and capacity pressure. | P1 | Placeholder | [Personas](/business-layer/personas), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [Reporting](/business-layer/reporting) |
| Pilot feedback | Capture authenticated pilot feedback with tenant context and safe operational review. | P2 | Deferred | [Feedback](/business-layer/feedback), [Business Process Flows](/business-layer/business-process-flows) |
| Commercialisation and billing | Future tenant-level commercial records or billing only after the commercial model is approved. | Deferred | Deferred | [Commercialisation](/strategy-layer/commercialisation), [Billing](/business-layer/billing) |

## Capability Dependencies

| Capability | Depends On | Why It Matters |
| --- | --- | --- |
| Employee resource request | Tenant readiness, identity, profile, configuration | Requests cannot be customer-ready if tenant context, policy, or employee eligibility is unstable. |
| Fair allocation Draw | Booking, configuration, profile, audit, notification | Allocation must be policy-correct, explainable, idempotent, and communicated. |
| Cancellation and reallocation | Booking lifecycle, original Draw evidence, notification, audit | Released capacity should be reused fairly and defensibly. |
| HR / facility operations | Booking lifecycle, audit, notification, operational insight | HR needs safe exception handling without exposing hidden lottery internals to employees. |
| Facilities resource-map operations | Resource configuration and policy, audit, operational insight | False or stale capacity creates avoidable rejections, manual corrections, and low employee trust. |
| Operational insight and read models | Booking events, audit events, DataHub storage | Customer-facing reports must survive restart and be tenant-scoped. |

## Capability Disposition

This table separates customer-first capabilities from source-evidence ideas that should not drive near-term implementation.

| Disposition | Capabilities / Ideas | Direction |
| --- | --- | --- |
| Customer-ready core | Tenant readiness, employee request, profile eligibility, configuration/policy, Draw allocation, booking lifecycle, notification, audit. | Keep in P0/P1 delivery until end-to-end customer demo is credible. |
| Customer-ready operations | HR/facility operations, facilities resource-map operations, tenant administrator readiness, system administrator/operator visibility, safe operational insight. | Keep visible as customer-first gaps; do not collapse these into employee screens. |
| DataHub target | Demand, allocation, rejection, cancellation, no-show, fairness, utilization, and management summaries. | Implement as event-fed DataHub read models; Reporting may present/report over approved views. |
| Pilot support | Authenticated feedback from pilot users/evaluators. | Reasonable P2 candidate after core flows and persistence are stable. |
| Future optional product | Motorcycle-specific capacity, recurring reserved-space release, sustainability incentives, advanced demand prediction, optimization, broad support portal. | Keep out of the customer-ready baseline unless Robert/customer priority changes. |
| Deferred commercial scope | Billing, invoice generation, payments, subscription enforcement, employee-level charging. | Do not implement as part of customer-first deployability. |

## Business Objects

| Business Object | Owning Capability | Notes |
| --- | --- | --- |
| Tenant | Tenant onboarding and readiness | Customer service must persist lifecycle/readiness durably. |
| Location / zone / resource map | Configuration and parking policy | Versioned, auditable, tenant-scoped configuration. |
| Space / capacity pool | Facilities resource-map operations | Individual resources or pooled capacity with capabilities, active windows, closures, and publication evidence. |
| Employee profile facts | Profile and eligibility facts | Minimum facts only; avoid broad HR import. |
| Vehicle / capability facts | Profile and eligibility facts | Includes default vehicle, company-car, EV, accessibility, and capability signals where policy needs them. |
| Booking/resource request | Employee resource request / booking lifecycle | Employee-owned request intent; status changes are audit-visible. Parking request is the current concrete implementation. |
| Draw attempt | Fair allocation Draw | Includes algorithm version, seed/order evidence, decisions, and safe reason codes. |
| Allocation / reservation | Booking lifecycle management | Assigned spot/resource for a date and time slot. |
| Penalty | Booking lifecycle management | Booking-owned v1 penalty ledger for booking-related penalties. |
| Notification | Notification | User-facing operational communication. |
| Audit record | Audit and compliance evidence | Business evidence, distinct from technical telemetry. |
| Projection / report view | Operational insight and read models | DataHub-owned tenant-scoped read model. |

## Visible Placeholders

- Customer / tenant administration needs durable storage before customer-ready deployment.
- HR and administrator default workspaces need implementation and validation.
- Facilities resource-map maintenance and policy-impact preview need target UI/API validation before customers depend on dynamic capacity changes.
- Customer sponsor management summary needs DataHub-backed projections before it can replace manual evaluation notes.
- Operational insight should be reframed around DataHub/read models, not the obsolete Reporting PostgreSQL direction.
- Billing remains deferred and should not appear as a required customer-first capability.
- Robert TODO: confirm whether pilot feedback is P2 for customer demos or should remain deferred.
