# Business Capabilities

This capability map is the parking-first customer-ready target. It does not model every historical feature idea from the legacy functional architecture.

## Capability Map

| Capability | Target Description | Priority | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant onboarding and readiness | Prepare a company tenant, identity mapping, locations, policy, initial data, support contacts, timezone, and launch readiness checks. | P0 | Partial | [Tenant Onboarding](/business-layer/tenant-onboarding), [Business Process Flows](/business-layer/business-process-flows) |
| Customer / tenant administration | Maintain tenant lifecycle state, tenant identity, support contacts, and readiness evidence durably. | P0 | Placeholder | [Customer](/business-layer/customer), [Tenant Storage Contract](/production/tenant-storage-contract) |
| Employee parking request | Let employees request parking for future Draw allocation or same-day allocation using authenticated tenant/user context. | P0 | Partial | [Booking](/business-layer/booking), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Profile and eligibility facts | Resolve employee active status, vehicle, company-car, accessibility, reserved-space, and location facts needed by policy. | P0 | Partial | [Profile](/business-layer/profile), [Allocation Rules](/business-layer/allocation-rules) |
| Configuration and parking policy | Maintain tenant policy, locations, time slots, capacity/resource maps, zones, capability rules, and Draw schedule. | P0 | Partial | [Configuration](/business-layer/configuration), [Parking Policy Configuration](/business-layer/parking-policy-configuration) |
| Fair allocation Draw | Allocate scarce parking capacity using auditable company-car priority, weighted fairness, slot matching, and deterministic evidence. | P0 | Partial | [Allocation Rules](/business-layer/allocation-rules), [Business Process Flows](/business-layer/business-process-flows) |
| Booking lifecycle management | Support cancellation, reallocation, usage confirmation, no-show, expiry, penalties, and employee-safe reason codes. | P0 | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Booking Reason Codes](/business-layer/booking-reason-codes) |
| HR / facility operations | Give HR and facility managers role-specific queues, support views, next Draw visibility, controlled manual Draw, and cancellation workflows. | P0 | Placeholder | [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [My Spots UX](/business-layer/my-spots-ux) |
| Administrator operations | Give tenant and system administrators a different default workspace for tenant setup, policies, users, readiness, and platform operations. | P0 | Placeholder | [Roles](/business-layer/roles), [Tenant Onboarding](/business-layer/tenant-onboarding) |
| Notification | Notify affected users about request, allocation, rejection, cancellation, reallocation, usage, and operational events. | P0 | Partial | [Notification](/business-layer/notification) |
| Audit and compliance evidence | Preserve append-only, privacy-aware evidence for tenant setup, policy changes, booking decisions, Draw actions, manual actions, and sensitive access. | P0 | Partial | [Audit](/business-layer/audit), [Security](/security) |
| Operational insight and read models | Provide tenant-scoped demand, allocation, rejection, cancellation, no-show, fairness, and utilization summaries through DataHub-backed projections. | P1 | Placeholder | [Reporting](/business-layer/reporting), [DataHub](/application-layer/datahub) |
| Pilot feedback | Capture authenticated pilot feedback with tenant context and safe operational review. | P2 | Deferred | [Feedback](/business-layer/feedback), [Business Process Flows](/business-layer/business-process-flows) |
| Commercialisation and billing | Future tenant-level commercial records or billing only after the commercial model is approved. | Deferred | Deferred | [Commercialisation](/strategy-layer/commercialisation), [Billing](/business-layer/billing) |

## Capability Dependencies

| Capability | Depends On | Why It Matters |
| --- | --- | --- |
| Employee parking request | Tenant readiness, identity, profile, configuration | Requests cannot be customer-ready if tenant context, policy, or employee eligibility is unstable. |
| Fair allocation Draw | Booking, configuration, profile, audit, notification | Allocation must be policy-correct, explainable, idempotent, and communicated. |
| Cancellation and reallocation | Booking lifecycle, original Draw evidence, notification, audit | Released capacity should be reused fairly and defensibly. |
| HR / facility operations | Booking lifecycle, audit, notification, operational insight | HR needs safe exception handling without exposing hidden lottery internals to employees. |
| Operational insight and read models | Booking events, audit events, DataHub storage | Customer-facing reports must survive restart and be tenant-scoped. |

## Visible Placeholders

- Customer / tenant administration needs durable storage before customer-ready deployment.
- HR and administrator default workspaces need implementation and validation.
- Operational insight should be reframed around DataHub/read models, not the obsolete Reporting PostgreSQL direction.
- Billing remains deferred and should not appear as a required customer-first capability.
