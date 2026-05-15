![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)
![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)

The Fair Parking System (FPS) is an open-source, multi-tenant SaaS platform for companies where more employees need parking than the building can provide.

FPS replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request parking, company-car obligations are handled first, and remaining spaces are allocated by documented fairness rules so access improves over time instead of depending on who emailed HR first.

## Executive Summary

FPS is being built as a documentation-led product with focused implementation slices. The core Booking domain is implemented, the surrounding Identity/Profile/Notification/Audit/API-client foundation is in place, and the active product direction is now mobile employee self-service plus production hardening.

The current roadmap is known end-to-end: complete mobile login and booking actions, finish Notification and Audit v1 capabilities, then harden Configuration, Customer, Reporting, Billing, and production operations. Progress is tracked slice-by-slice in the [Implementation Tracker](./implementation-tracker).

## Product Outcomes

- **Fair access to scarce parking**: allocate spaces with explicit, auditable rules instead of first-come, first-served coordination.
- **Lower operational load**: reduce HR and facilities work by automating request intake, Draw execution, notification, and audit records.
- **Tenant isolation by design**: keep company data and policies isolated for SaaS use through authenticated context and tenant-scoped persistence.
- **Employee trust**: make booking status, outcomes, and visible reasons understandable to employees.
- **Operational evidence**: preserve event, notification, and audit trails so policy decisions can be reviewed later.

## Current Product Shape

- Employees can submit future and same-day booking requests through the backend API.
- The daily Draw allocates scarce spaces using documented allocation rules.
- Company-car employees receive first allocation priority where policy requires it.
- Remaining employees are selected by weighted fairness using recent allocation history and active penalties.
- Booking emits events consumed by Notification and Audit services.
- OpenAPI and generated TypeScript client contracts support web and React Native clients.
- The React Native + Expo mobile app has an app shell and read-only My Bookings screen.

Use these pages first:

- [Implementation Tracker](./implementation-tracker): what is done, what is next, PRs, implementer attribution, and dates.
- [Development Plan](./development-plan): detailed roadmap, slice scope, acceptance criteria, and open risks.
- [Implementation](./implementation): how work is sliced, assigned, reviewed, tracked, and merged.
- [Software Architecture](./technology-layer/software-architecture): current bounded contexts, integration model, technology choices, and tenant isolation decision.
- [Security](./security): actors, roles, data classification, encryption, secret access, service-level controls, and GDPR alignment.

## Global Architecture Document

1. **[Versions and decisions](./versions-and-decisions)** 

Durable architecture decisions, version choices, licensing decisions, and implementation milestones.

2. **[Strategy layer](./strategy)**

Strategic goals, product scope, stakeholder outcomes, and high-level success measures for FPS.

3. **[Business layer](./business-layer)** 

Business requirements, domain processes, service contracts, and bounded-context rules.

4. **[Implementation](./implementation)**

Development plan, slice tracking, handoff workflow, validation gates, and tooling.

5. **[Application layer](./application-layer)**

Application structure, user-facing components, service responsibilities, and implementation conventions.

6. **[Technology layer](./technology-layer)**

Infrastructure, deployment, non-functional requirements, runtime technology choices, and the [Software Architecture](./technology-layer/software-architecture) overview.

7. **[Security](./security)**

Cross-cutting security model, data classification, authentication, authorization, privacy, auditability, operational controls, and service-specific security notes.

8. **[Glossary](./glossary)**

Definitions and acronyms used across the product, business, application, and technology documentation.
