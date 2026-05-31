# Baseline Architecture

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Baseline |
| ADM Phase | Cross-ADM |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | Before first customer-ready baseline |

FairSpot does not yet maintain a full enterprise baseline architecture. The baseline is current-state evidence: what is implemented, documented, deployable, or known to be missing.

## Current-State Evidence

| Area | Current State | Evidence | Confidence |
| --- | --- | --- | --- |
| Business | Parking-first fair allocation product with documented booking, Draw, cancellation, notification, audit, reporting, and configuration behavior. | [Business Layer](/business-layer), [Allocation Rules](/business-layer/allocation-rules), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) | High |
| Applications | Backend services, web, mobile, DataHub skeleton, and supporting infrastructure exist in the repository; implementation completeness varies by slice. | [Software Architecture](/technology-layer/software-architecture), [Implementation Tracker](/implementation-tracker) | Medium |
| Data | Service-owned persistence and event-fed read-model direction are documented; DataHub is the target for cross-service CQRS reads. | [DataHub](/application-layer/datahub), [Function Map Validation](/function-map-validation) | Medium |
| Technology | Dapr-first, provider-neutral runtime direction is documented with local/demo/client production profiles. | [Dapr-First Standards](/production/dapr-first-production-standards), [Production](/production) | High |
| Security | Security model, privacy, audit, and Cloudflare/WAF guidance are documented; validation before hosted pilot remains required. | [Security](/security), [Security Model](/security/security-model), [Gap Register](/security/gap-register) | Medium |

## Baseline Limits

- Existing current-state evidence is assembled from docs, implementation tracker, and PR history rather than one formal baseline model.
- Some implementation tracker entries are historical and may need review before customer-facing claims.
- Billing is intentionally deferred and should not be treated as part of the customer-ready target.
