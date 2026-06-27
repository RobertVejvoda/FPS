# Demo and Evaluation

This page defines how FairSpot should be demonstrated to business stakeholders, client IT, operators, and future users. The shareable evaluator-facing summary is available in the [Client Evaluation Pack](./client-evaluation-pack).

## Demo Goal

The demo should prove that FairSpot is more than a booking form. It should show fair access to limited workplace resources, currently parking. Future resources such as workplace desks, chairs, seats, lockers, or chargers may reuse the same tenant, policy, notification, audit, and reporting foundations after parking v1 is stable.

## Demo Roles

| Role | What They Should See | Success Signal |
| --- | --- | --- |
| Employee | Login, view own bookings, submit a parking request, cancel, confirm usage, receive status notification. | Employee understands request status and next action without seeing hidden lottery details. |
| Company-car employee | HR-assigned company-car handling with a fixed compatible parking slot. | The employee sees that the space is ready when the request is on time, and the priority is explainable as an HR/facilities-controlled obligation rather than lottery preference. |
| HR / facilities | Tenant policy, location override, slots/capacity, Draw outcome, operational reporting. | HR can manage parking rules without code changes and can explain outcomes. |
| Tenant admin | User roles, tenant/location setup, policy configuration, slot setup. | Admin can see which setup is required before go-live. |
| Auditor / compliance | Audit query, pseudonymised actor references, GDPR PII mapping erasure behavior. | Audit evidence exists without exposing unnecessary personal data. |
| Client IT / operator | Local stack, demo deployment option, Dapr components, metrics/logs/traces, backup/restore and incident notes. | IT can see how FairSpot plugs into their environment and observability stack. |
| Sponsor / procurement | Product value, deployment ownership model, cost path, license posture, implementation roadmap. | Sponsor can decide whether FairSpot is worth a pilot. |

## Demo Data Set

| Data Set | Purpose |
| --- | --- |
| Tenant with one office location | Keeps the story simple for first demo. |
| Employees with normal parking needs | Shows regular request and allocation behavior. |
| Company-car employees | Shows HR-assigned fixed-slot allocation outside the Tier 2 fairness lottery. |
| HR/admin users | Shows policy and slot configuration. |
| Auditor user | Shows audit query and erasure workflow. |
| Enough requests to exceed capacity | Shows why fairness and Draw are needed. |
| Notifications and reporting examples | Shows operational evidence after the allocation flow. |

## Demo Tracks

| Track | Goal |
| --- | --- |
| Employee mobile demo | Show the employee self-service path: login, My Spots, request, cancel/confirm, notifications, profile, and Draw schedule visibility. |
| HR / facilities backend demo | Show policy, slots, reporting, and operational evidence. |
| Auditor demo | Show pseudonymised audit query and GDPR erasure behavior. |
| Client IT demo | Show the containerized local/NAS stack, Dapr component boundary, and observability approach. |
| Sponsor evaluation | Show value, roadmap, costs, and deployment ownership model. |

For the live, per-slice delivery state and remaining gaps per track, see the [Roadmap](./roadmap) and [Implementation Tracker](./implementation-tracker). (The previous "Gaps To Close" column listed slices such as `MOB006`–`MOB009`, web/admin UI, and `OPS001`–`OPS003` that are now delivered.)

## Client-Facing Materials

The first version of these materials is collected in the [Client Evaluation Pack](./client-evaluation-pack). Keep this table as the checklist for future improvements.

| Material | Audience | Purpose |
| --- | --- | --- |
| One-page product summary | Sponsor, business evaluator | Explain problem, value, and parking v1 scope. |
| Role-based demo script | Demo facilitator | Keep employee, HR, auditor, and operator demos consistent. |
| Architecture overview | Architect, client IT | Show ArchiMate-style layers, Dapr boundaries, services, and data/security controls. |
| Deployment and operations summary | Client IT, operator | Explain local/demo/client-owned production options and pluggable components. |
| Security and GDPR summary | Security reviewer, DPO | Summarize roles, data classes, audit, erasure, encryption, secrets, and traceability. |
| Cost and hosting assumptions | Sponsor, procurement, client IT | Explain demo cost path, production ownership, and usage/performance tracking. |
| Commercialisation options note | Sponsor, procurement | Explain that support subscription, dual licensing, dedicated tenant packaging, and enhanced reports are future options, not current product promises. |
| FAQ | All evaluators | Capture common product, security, deployment, and roadmap questions. |

## Demo Readiness Checklist

- Seeded tenant, users, roles, locations, policies, slots, and request history exist.
- Each demo role has a known login and a scripted path.
- Data is fake and safe to share.
- Demo can be reset without manual database editing.
- Any evaluator-facing seed/reset action is authenticated, rate-limited where practical, and limited to synthetic sandbox tenants.
- Demo credentials are shared only with approved evaluators or issued through a controlled request flow.
- Expected notifications, audit records, and reporting results are predictable.
- Local observability shows metrics/logs/traces for the demo flow.
- Client-facing materials are linked from this page and updated before external sharing.
- Employee mobile scenarios are checked with the [Mobile Device Testing Plan](./production/mobile-device-testing) before any external demo.
