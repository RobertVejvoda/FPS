# Fair Parking System

![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)
![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)

The Fair Parking System (FPS) is an open-source, multi-tenant SaaS platform for companies where more employees need parking than the building can provide.

FPS replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request parking, company-car obligations are handled first, and remaining spaces are allocated by documented fairness rules so access improves over time instead of depending on who emailed HR first.

## Executive Summary

FPS is being built as a documentation-led product. The parking product is the first concrete reservation domain; the same pattern could later support other scarce workplace resources such as desks, chairs, or seats, but only after parking v1 is stable.

The implemented backend now covers Booking, Identity/Profile context, Notification, Audit, Reporting read models, and Configuration policy/slot management. The mobile employee flow covers login, booking submission, booking actions, My Bookings, notifications, profile details, allocation status, and demo/pilot polish. The next product direction is repeatable local testing, client-evaluation features, web/admin surfaces, client-owned deployment guidance, and production operations.

This site is the product and business-facing view of FPS: problem, goals, actors, policy model, architecture summary, trust story, roadmap, and demo/evaluation narrative. Detailed implementation notes, delivery board mechanics, agent routing, tooling, and runbooks belong in the [GitHub Wiki](https://github.com/RobertVejvoda/FPS/wiki).

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
- The React Native + Expo mobile app has the current employee self-service path for demo/pilot evaluation.

## Reader Paths

| Reader | Start here | Purpose |
| --- | --- | --- |
| Business evaluator | [Strategy](./strategy), [Business](./business-layer), [Demo and Evaluation](./demo-and-evaluation) | Understand the problem, product value, roles, and demo story. |
| Product owner | [Roadmap](./roadmap), [Versions and Decisions](./versions-and-decisions), [Business Requirements](./business-layer/requirements) | Understand priorities, durable decisions, and outcome coverage. |
| Architect | [Architecture Summary](./architecture-views), [Software Architecture](./technology-layer/software-architecture), [Technology Direction](./technology-layer) | Understand the capability model, bounded contexts, integration direction, and platform choices. |
| Security or client IT reviewer | [Security](./security), [Security Model](./security/security-model), [Production Model](./production) | Understand tenant isolation, privacy, auditability, deployment ownership, and operational evidence. |
| Maintainer or implementer | [GitHub Wiki](https://github.com/RobertVejvoda/FPS/wiki) | Work with implementation slices, delivery-board mechanics, tooling, agent handoffs, and runbooks. |

## Site Scope

Keep GitHub Pages focused on material that is useful without repository context:

- product idea, goals, scope, and licensing;
- personas, roles, requirements, and business process;
- booking and allocation policy in business terms;
- high-level architecture, security, and production posture;
- roadmap, demo narrative, and durable decisions.

Move or mirror these working materials to the GitHub Wiki as they mature:

- implementation tracker, vertical slice specs, and acceptance criteria;
- agent routing, assignment rules, and delivery board mechanics;
- local development setup, tooling, CI details, and validation commands;
- service-level technical notes, generated contract details, and runbooks;
- historical coordination notes that are useful for maintainers but not product readers.
