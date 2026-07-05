# Core Values

FairSpot is guided by four product values. They are meant to be practical decision filters: when scope, design, architecture, or operations conflict, choose the option that best preserves these values.

## Fair

FairSpot exists to make access to scarce workplace resources more equitable and explainable. Access to parking, seats, sport courts, desks, lockers, chargers, or similar bookable resources must not depend on who emailed first, who knows HR/facilities best, or who understands hidden process details.

Fair means:

- allocation rules are documented and visible enough to be trusted;
- company-car, accessibility, reservation, and policy constraints are handled explicitly;
- weighted Draw behavior improves access over time instead of rewarding speed alone;
- outcomes have employee-visible reasons where possible;
- sensitive allocation evidence is auditable without exposing other employees private data.

## Simple

FairSpot should reduce coordination work, not move spreadsheet complexity into software screens. Employees, HR, admins, and auditors should be guided by business-readable flows rather than internal IDs, hidden policy fields, or technical terminology.

Simple means:

- employees can request, view, cancel, and confirm bookings without understanding implementation details;
- HR and tenant admins can manage policy, locations, vehicles, and exceptions through guided controls;
- demo and pilot users see realistic data and clear next actions;
- common workflows are repeatable through local harnesses, seed data, and smoke scripts;
- product documentation explains the story before implementation mechanics.

## Trustworthy

FairSpot handles employee, tenant, policy, allocation, and audit data. Trust requires more than encryption: the product must preserve privacy, tenant boundaries, auditability, and operational evidence.

Trustworthy means:

- identity, tenant, and role context come from authenticated claims or trusted service context;
- confidential and secret data are not exposed in logs, traces, issue comments, demos, or employee-facing messages;
- technical telemetry stays in observability tools, while business activity is represented by Audit service records;
- audit actors are pseudonymised and resolved only through an approved PII mapping path;
- privacy workflows, including erasure, are governed, tracked, and auditable.

## Practical

FairSpot should be useful to evaluate, operate, and adapt before it becomes a large platform. The architecture should stay provider-neutral and cost-aware while still leaving a path to client-owned production.

Practical means:

- parking remains the first launch proof until the demo and hosted baseline are stable;
- local setup, smoke testing, and observability should make the system easy to prove;
- Dapr and OpenTelemetry are used as portability boundaries rather than vendor commitments;
- client-owned deployment, support, and production handoff are part of the product story;
- additional resources such as seats, sport courts, desks, lockers, and chargers reuse the same model instead of creating one-off booking products.
