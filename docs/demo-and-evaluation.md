# Demo and Evaluation

This page defines how FPS should be demonstrated to business stakeholders, client IT, operators, and future users. It also records future client-facing material that should be prepared once the product is ready enough to share outside the implementation team.

## Demo Goal

The demo should prove that FPS is more than a booking form. It should show a fair, auditable reservation process for scarce shared resources, currently parking. Future resources such as workplace desks, chairs, or seats may reuse the same tenant, policy, notification, audit, and reporting foundations after parking v1 is stable.

## Demo Roles

| Role | What They Should See | Success Signal |
| --- | --- | --- |
| Employee | Login, view own bookings, submit a parking request, cancel, confirm usage, receive status notification. | Employee understands request status and next action without seeing hidden lottery details. |
| Company-car employee | Priority handling when policy requires company-car allocation first. | The reason for priority is explainable and does not look arbitrary. |
| HR / facilities | Tenant policy, location override, slots/capacity, Draw outcome, operational reporting. | HR can manage parking rules without code changes and can explain outcomes. |
| Tenant admin | User roles, tenant/location setup, policy configuration, slot setup. | Admin can see which setup is required before go-live. |
| Auditor / compliance | Audit query, pseudonymised actor references, GDPR PII mapping erasure behavior. | Audit evidence exists without exposing unnecessary personal data. |
| Client IT / operator | Local stack, demo deployment option, Dapr components, metrics/logs/traces, backup/restore and incident notes. | IT can see how FPS plugs into their environment and observability stack. |
| Sponsor / procurement | Product value, deployment ownership model, cost path, license posture, implementation roadmap. | Sponsor can decide whether FPS is worth a pilot. |

## Demo Data Set

| Data Set | Purpose |
| --- | --- |
| Tenant with one office location | Keeps the story simple for first demo. |
| Employees with normal parking needs | Shows regular request and allocation behavior. |
| Company-car employees | Shows Tier 1 allocation and capacity pressure. |
| HR/admin users | Shows policy and slot configuration. |
| Auditor user | Shows audit query and erasure workflow. |
| Enough requests to exceed capacity | Shows why fairness and Draw are needed. |
| Notifications and reporting examples | Shows operational evidence after the allocation flow. |

## Demo Tracks

| Track | Goal | Required Implemented Slices | Gaps To Close |
| --- | --- | --- | --- |
| Employee mobile demo | Show the employee self-service path. | `MOB001`-`MOB005`, `B001`-`B010`, `BK011`, `ID001` | `MOB006` notifications, `MOB007` profile/vehicle details, `MOB008` draw/allocation detail, `MOB009` polish. |
| HR / facilities backend demo | Show policy, slots, reporting, and operational evidence. | `CFG001`, `CFG002`, `REPORT001`, `A001`, `A002` | Web/admin UI, `CFG003`, `REPORT002`. |
| Auditor demo | Show pseudonymised audit query and GDPR erasure behavior. | `A001`, `A002` | `A003` retention/integrity and export evidence. |
| Client IT demo | Show local stack, Dapr component boundary, and observability approach. | Local tooling, `OPS000` proposal | `OPS001`, `OPS002`, `OPS003`. |
| Sponsor evaluation | Show value, roadmap, costs, and deployment ownership model. | Current docs and tracker | Client evaluation pack. |

## Client-Facing Materials

These materials are planned, not implemented in this cleanup.

| Material | Audience | Purpose |
| --- | --- | --- |
| One-page product summary | Sponsor, business evaluator | Explain problem, value, and parking v1 scope. |
| Role-based demo script | Demo facilitator | Keep employee, HR, auditor, and operator demos consistent. |
| Architecture overview | Architect, client IT | Show ArchiMate-style layers, Dapr boundaries, services, and data/security controls. |
| Deployment and operations summary | Client IT, operator | Explain local/demo/client-owned production options and pluggable components. |
| Security and GDPR summary | Security reviewer, DPO | Summarize roles, data classes, audit, erasure, encryption, secrets, and traceability. |
| Cost and hosting assumptions | Sponsor, procurement, client IT | Explain demo cost path, production ownership, and usage/performance tracking. |
| FAQ | All evaluators | Capture common product, security, deployment, and roadmap questions. |

## Demo Readiness Checklist

- Seeded tenant, users, roles, locations, policies, slots, and request history exist.
- Each demo role has a known login and a scripted path.
- Data is fake and safe to share.
- Demo can be reset without manual database editing.
- Expected notifications, audit records, and reporting results are predictable.
- Local observability shows metrics/logs/traces for the demo flow.
- Client-facing materials are linked from this page when prepared.
