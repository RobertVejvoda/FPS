# FairSpot

![CI](https://github.com/RobertVejvoda/fairspot/actions/workflows/ci.yml/badge.svg?branch=master)
![Docs](https://github.com/RobertVejvoda/fairspot/actions/workflows/docs.yml/badge.svg?branch=master)

FairSpot is an open-source, multi-tenant fair allocation and booking platform for companies where demand for shared workplace resources exceeds supply. Parking is the first launch module and proof vertical; the product scope also covers seats, sport courts, desks, lockers, chargers, and other bookable limited resources.

FairSpot replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request a resource, resource-specific obligations such as company-car parking are handled first, and remaining capacity is allocated by documented fairness rules so access improves over time instead of depending on who emailed HR or facilities first.

## Executive Summary

FairSpot is being built as a documentation-led product. Parking is the first concrete launch domain because it has visible scarcity, policy complexity, and employee trust impact. Seats, sport courts, desks, lockers, chargers, and similar resources are part of the broader product direction, not a separate later product.

The implemented backend now covers Booking, Identity/Profile context, Notification, Audit, Reporting read models, and Configuration policy/slot management. The mobile employee flow covers login, booking submission, booking actions, My Bookings, notifications, profile details, allocation status, and demo/pilot polish. The next product direction is repeatable local testing, client-evaluation features, web/admin surfaces, client-owned deployment guidance, and production operations.

This site is the product and business-facing view of FairSpot: problem, goals, actors, policy model, architecture summary, trust story, roadmap, and demo/evaluation narrative.

Product strategy is not maintained as a separate public section. Strategy is expressed through this overview and then governed in the architecture and product artifacts: [Architecture Vision](./architecture/architecture-vision), [Principles](./architecture/principles), [Roadmap](./roadmap), [Commercialisation](./strategy-layer/commercialisation), and [Versions and Decisions](./versions-and-decisions). Older strategy pages remain source evidence until fully retired.

## Open Core and the Platform Plane

This site documents the **public open-core `fairspot`** product — the runtime, the fairness/Draw engine, tenant self-administration, and the architecture and security model. Everything needed to self-host and inspect a single organisation under AGPL stays here.

The **hosted operator product** — cross-tenant platform operations, the operator console, hosted-deployment runbooks, onboarding-queue internals, and usage metering — is a separate, commercial **platform plane** in the private `fairspot-platform` repository. The commercial line is the platform plane, not the fairness engine. Public Operations pages keep customer/self-hosting contracts and summaries; detailed hosted-operator runbooks live privately. See the [Open-Core Documentation Boundary](./strategy-layer/open-core-boundary) for the full public/private classification.

## Product Outcomes

- **Fair access to scarce shared resources**: allocate spaces, seats, courts, and other limited resources with explicit, auditable rules instead of first-come, first-served coordination.
- **Lower operational load**: reduce HR and facilities work by automating request intake, Draw execution, notification, and audit records.
- **Tenant isolation by design**: keep company data and policies isolated for SaaS use through authenticated context and tenant-scoped persistence.
- **Employee trust**: make booking status, outcomes, and visible reasons understandable to employees.
- **Operational evidence**: preserve event, notification, and audit trails so policy decisions can be reviewed later.

## Current Product Shape

- Employees can submit future and same-day booking requests through the backend API and mobile app, with parking as the current fully implemented resource vertical.
- Employees can cancel bookings and confirm usage from mobile.
- The daily Draw allocates scarce capacity using documented allocation rules.
- Company-car employees receive first allocation priority where policy requires it.
- Remaining employees are selected by weighted fairness using recent allocation history and active penalties.
- Booking emits events consumed by Notification, Audit, and Reporting services.
- Notification supports in-app records/API/SSE plus email delivery and email-failure observability.
- Audit supports append-only pseudonymised records plus auditor query and GDPR PII mapping erasure.
- Reporting supports tenant-scoped parking summary and fairness read models.
- Configuration supports admin/HR-managed tenant policy, location override, and slot/capacity APIs for the current parking vertical and the wider resource-map direction.
- OpenAPI and generated TypeScript client contracts support web and React Native clients.
- The React Native + Expo mobile app has the current employee self-service path for demo/pilot evaluation.

## Reader Paths

| Reader | Start here | Purpose |
| --- | --- | --- |
| Business evaluator | [Product Overview](./Home), [Business Architecture](./architecture/business/), [Demo and Evaluation](./demo-and-evaluation) | Understand the problem, product value, roles, and demo story. |
| Product owner | [Roadmap](./roadmap), [Versions and Decisions](./versions-and-decisions), [Architecture Requirements](./architecture/requirements) | Understand priorities, durable decisions, and outcome coverage. |
| Architect | [Architecture Repository](./architecture/), [Information Systems](./architecture/information-systems/), [Technology Architecture](./architecture/technology/) | Understand the capability model, bounded contexts, integration direction, and platform choices. |
| Architecture governor | [TOGAF ADM Map](./architecture/togaf-adm-map), [Artifact Register](./architecture/artifact-register), [Gap Analysis](./architecture/architecture-states/gap-analysis) | Understand architecture phase coverage, artifact status, baseline/target versions, and known gaps. |
| Security or client IT reviewer | [Security Architecture](./architecture/security/), [Deployment Profiles](./architecture/technology/deployment-profiles), [Operations](./production) | Understand tenant isolation, privacy, auditability, deployment ownership, and operational evidence. Detailed hosted-operator runbooks live in the private platform repository; public Operations pages keep the responsibility contracts. |

## Site Scope

Keep GitHub Pages focused on material that is useful without repository context:

- product idea, goals, scope, and licensing;
- stakeholders, roles, requirements, and business process;
- booking and allocation policy in business terms;
- high-level architecture, security, and operations posture;
- roadmap, demo narrative, and durable decisions.

Keep working materials out of the public product site unless they are relevant for client evaluation:

- implementation tracker, vertical slice specs, and acceptance criteria;
- agent routing, assignment rules, and delivery board mechanics;
- local development setup, tooling, CI details, and validation commands;
- service-level technical notes and generated contract details;
- historical coordination notes that are useful for maintainers but not product readers.
