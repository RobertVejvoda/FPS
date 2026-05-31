# Business Processes

| Process | Trigger | Main Outcome | Exceptions / Notes | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant onboarding | New company tenant is prepared. | Tenant is ready for pilot or production use. | Identity and seed data must match tenant context. | [Tenant Onboarding](/business-layer/tenant-onboarding) |
| Future booking request | Employee requests a future parking slot. | Request waits for scheduled Draw. | Cut-off, duplicate, capacity, and eligibility rules apply. | [Booking](/business-layer/booking) |
| Same-day booking request | Employee requests parking for today. | Immediate allocation or rejection based on capacity. | Same-day path must use correct location and slot state. | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Scheduled Draw | Draw schedule reaches configured time or controlled trigger occurs. | Requests are allocated, rejected, or remain pending based on policy. | Multi-instance safety and idempotency are required. | [Allocation Rules](/business-layer/allocation-rules) |
| Cancellation and reallocation | Employee or HR cancels an allocated request. | Booking is cancelled and eligible pending request may be reallocated. | Late cancellation penalties and notification apply. | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Audit review | Authorized reviewer investigates a decision. | Business evidence can be reviewed safely. | PII mapping access must be controlled and audited. | [Audit](/business-layer/audit) |
