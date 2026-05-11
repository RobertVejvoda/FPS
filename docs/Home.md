---
title: Fair Parking System (FPS) 
---

![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)
![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)

The Fair Parking System (FPS) is an open-source, multi-tenant SaaS platform for companies where more employees need parking than the building can provide.

FPS replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request parking, company-car obligations are handled first, and remaining spaces are allocated by documented fairness rules so access improves over time instead of depending on who emailed HR first.

### Product Goals

- **Fair access to scarce parking**: Allocate spaces with explicit, auditable rules instead of first-come, first-served coordination.
- **Lower operational load**: Reduce HR and facilities work by automating request intake, Draw execution, notification, and audit records.
- **Tenant isolation by design**: Keep company data and policies isolated for SaaS use.
- **Employee trust**: Make booking status, outcomes, and visible reasons understandable to employees.
- **Operational evidence**: Preserve event, notification, and audit trails so policy decisions can be reviewed later.

### What FPS Does

- Accepts future and same-day parking requests.
- Runs the daily Draw for scarce spaces.
- Gives company-car employees first allocation priority where policy requires it.
- Uses weighted allocation for remaining employees based on recent allocation history and active penalties.
- Records booking events for notification and audit consumers.
- Exposes API contracts for future web and React Native clients.

### Current Build Focus

The backend Booking vertical slices are implemented. Current work is hardening the platform around authenticated user context, Profile snapshots, Notification and Audit consumers, Configuration-owned policy, OpenAPI client generation, and then the mobile shell.

The implementation queue is maintained in the [Development Plan](./development-plan).

---


### Global Architecture Document

1. **[Versions and decisions](./versions-and-decisions)** 

Durable architecture decisions, version choices, licensing decisions, and implementation milestones.

2. **[Strategy layer](./strategy-layer)** 

Strategic goals, product scope, stakeholder outcomes, and high-level success measures for FPS.

3. **[Business layer](./business-layer)** 

Business requirements, domain processes, vertical slices, service contracts, and bounded-context rules.

4. **[Application layer](./application-layer)** 

Application structure, user-facing components, service responsibilities, and implementation conventions.

5. **[Technology layer](./technology-layer)** 

Infrastructure, security, deployment, non-functional requirements, and the [Software Architecture](./technology-layer/software-architecture) overview.

6. **[Glossary](./glossary)** 

Definitions and acronyms used across the product, business, application, and technology documentation.
