![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)
![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)

The Fair Parking System (FPS) is an open-source, multi-tenant SaaS platform for companies where more employees need parking than the building can provide.

FPS replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request parking, company-car obligations are handled first, and remaining spaces are allocated by documented fairness rules so access improves over time instead of depending on who emailed HR first.

## Executive Summary

FPS is being built as a documentation-led product with focused implementation slices. The parking product is the first concrete reservation domain; the same pattern could later support other scarce workplace resources such as desks, chairs, or seats, but only after parking v1 is stable.

The implemented backend now covers Booking, Identity/Profile context, Notification, Audit, Reporting read models, and Configuration policy/slot management. The mobile employee flow covers login, booking submission, booking actions, and My Bookings. The next product direction is demo readiness, mobile completion, web/admin surfaces, client-owned deployment guidance, and production operations.

Progress is tracked slice-by-slice in the [Implementation Tracker](./implementation-tracker) and mapped to requirements in [Requirements Traceability](./requirements-traceability).

## Product Outcomes

- **Fair access to scarce parking**: allocate spaces with explicit, auditable rules instead of first-come, first-served coordination.
- **Lower operational load**: reduce HR and facilities work by automating request intake, Draw execution, notification, and audit records.
- **Tenant isolation by design**: keep company data and policies isolated for SaaS use through authenticated context and tenant-scoped persistence.
- **Employee trust**: make booking status, outcomes, and visible reasons understandable to employees.
- **Operational evidence**: preserve event, notification, and audit trails so policy decisions can be reviewed later.

## Current Product Shape

- Employees can submit future and same-day booking requests through the backend API and mobile app.
- Employees can cancel bookings and confirm usage from mobile.
- The daily Draw allocates scarce spaces using documented allocation rules.
- Company-car employees receive first allocation priority where policy requires it.
- Remaining employees are selected by weighted fairness using recent allocation history and active penalties.
- Booking emits events consumed by Notification, Audit, and Reporting services.
- Notification supports in-app records/API/SSE plus email delivery and email-failure observability.
- Audit supports append-only pseudonymised records plus auditor query and GDPR PII mapping erasure.
- Reporting supports tenant-scoped parking summary and fairness read models.
- Configuration supports admin/HR-managed tenant policy, location override, and slot/capacity APIs.
- OpenAPI and generated TypeScript client contracts support web and React Native clients.
- The React Native + Expo mobile app has the current employee self-service foundation.

Use these pages first:

- [Demo and Evaluation](./demo-and-evaluation): how different roles should experience FPS and what client-facing material is planned.
- [Architecture Views](./architecture-views): ArchiMate-style hierarchy for business, application, technology, security, production, and implementation views.
- [Implementation Tracker](./implementation-tracker): what is done, what is next, PRs, implementer attribution, and dates.
- [Development Plan](./development-plan): detailed roadmap, slice scope, acceptance criteria, and open risks.
- [Implementation](./implementation): how work is sliced, assigned, reviewed, tracked, and merged.
- [Software Architecture](./technology-layer/software-architecture): current bounded contexts, integration model, technology choices, and tenant isolation decision.
- [Security](./security): actors, roles, data classification, encryption, secret access, service-level controls, and GDPR alignment.
- [Production](./production): production runtime model, cloud deployment path, operations, backups, restore, monitoring, incidents, and readiness gates.

## Reader Paths

| Reader | Start here | Purpose |
| --- | --- | --- |
| Business evaluator | [Strategy](./strategy), [Business Layer](./business-layer), [Demo and Evaluation](./demo-and-evaluation) | Understand the problem, product value, roles, and demo story. |
| Architect | [Architecture Views](./architecture-views), [Business Layer](./business-layer), [Application Layer](./application-layer), [Technology Layer](./technology-layer) | Understand the ArchiMate-style hierarchy and how capabilities map to services and technology. |
| Security reviewer | [Security](./security), [Requirements Traceability](./requirements-traceability), [Production](./production) | Understand roles, data classes, GDPR, auditability, encryption, and operational controls. |
| Client IT / operator | [Production](./production), [Hosting and Deployment Strategy](./production/hosting-deployment-strategy), [Monitoring](./production/monitoring) | Understand local, demo, and client-owned deployment, Dapr portability, observability, and operations. |
| Implementer | [Implementation](./implementation), [Implementation Tracker](./implementation-tracker), [Development Plan](./development-plan) | Understand slice order, ownership, validation, and remaining work. |

## Global Architecture Document

1. **[Versions and decisions](./versions-and-decisions)**

Durable architecture decisions, version choices, licensing decisions, and implementation milestones.

2. **[Strategy and motivation](./strategy)**

Strategic goals, product scope, stakeholder outcomes, and high-level success measures for FPS.

3. **[Architecture views](./architecture-views)**

ArchiMate-style view hierarchy and placeholders for motivation, business, application, technology, data/security, production, and implementation views.

4. **[Business layer](./business-layer)**

Business requirements, domain processes, service contracts, and bounded-context rules.

5. **[Implementation](./implementation)**

Development plan, slice tracking, handoff workflow, validation gates, and tooling.

6. **[Application layer](./application-layer)**

Application structure, user-facing components, service responsibilities, and implementation conventions.

7. **[Technology layer](./technology-layer)**

Infrastructure, deployment, non-functional requirements, runtime technology choices, and the [Software Architecture](./technology-layer/software-architecture) overview.

8. **[Security](./security)**

Cross-cutting security model, data classification, authentication, authorization, privacy, auditability, operational controls, and service-specific security notes.

9. **[Production](./production)**

Local, demo, and client-owned production profiles, operational model, backup/restore, monitoring, incident handling, maintenance, and readiness evidence.

10. **[Demo and evaluation](./demo-and-evaluation)**

Role-based demo plan, client evaluation scenarios, and planned shareable client material.

11. **[Glossary](./glossary)**

Definitions and acronyms used across the product, business, application, and technology documentation.
