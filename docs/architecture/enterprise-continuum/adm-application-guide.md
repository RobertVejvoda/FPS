# ADM Application Guide

This guide explains how to walk through the ADM checklist while learning Enterprise Continuum thinking from FairSpot.

## Method

For each ADM phase:

| Step | Action |
| --- | --- |
| 1 | Explain the TOGAF concept in plain language. |
| 2 | Apply it to FairSpot Operator. |
| 3 | Place the result in the existing architecture repository. |
| 4 | Mark whether the result is product architecture, operator architecture, reusable architecture, or reusable solution. |
| 5 | Record gaps, evidence, and next artifact updates. |

## Preliminary - Architecture Capability

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| Who owns architecture? | Robert is accountable; Codex prepares architecture content and reviews implementation evidence; implementers provide evidence but do not approve their own work. |
| What is the architecture scope? | Product architecture plus the fictive operator architecture needed to run FairSpot commercially. |
| What is reusable? | TOGAF repository structure, Dapr-first standards, OIDC, observability, CI/CD, deployment profile patterns. |
| What is organization-specific? | FairSpot Operator commercial operating model, service catalog, hosted evaluation baseline, support process. |

Repository outputs:

- [Governance](/architecture/governance/)
- [RACI](/architecture/governance/raci)
- [Artifact Register](/architecture/artifact-register)
- [Enterprise Continuum](/architecture/enterprise-continuum/)

## Phase A - Architecture Vision

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| Why are we doing this architecture? | To prepare a credible commercial operator model for FairSpot evaluation, hosted demo, support, and production-readiness services. |
| What is out of scope now? | Product Billing, full managed production operation, and enterprise-scale rollout remain deferred until explicitly approved. |
| Which stakeholders matter? | Sponsor, customer evaluator, client IT/security, HR/facilities, support/operator, auditor, implementer. |

Repository outputs:

- [Architecture Vision](/architecture/architecture-vision)
- [Stakeholders and Concerns](/architecture/stakeholders-and-concerns)
- [Constraints](/architecture/constraints)

## Phase B - Business Architecture

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| What capabilities does the operator need? | Customer evaluation, tenant onboarding, hosted demo operation, support, production-readiness review, implementation assistance, feedback handling. |
| Which product capabilities are reused? | Parking request, Draw, allocation, notification, audit, reporting/DataHub, customer setup. |
| Which capabilities are deferred? | Billing workflow, broad support portal, enterprise account management. |

Repository outputs:

- [Capabilities](/architecture/business/capabilities)
- [Value Streams](/architecture/business/value-streams)
- [Business Processes](/architecture/business/business-processes)

## Phase C - Information Systems Architecture

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| Which systems support the operator? | FairSpot services, Customer, Identity, DataHub, Notification, Audit, support/ticketing candidate, CRM/customer registry candidate, Billing later. |
| Which data matters? | Tenant readiness, customer contacts, support cases, deployment evidence, audit evidence, DataHub projections, commercial account metadata later. |

Repository outputs:

- [Application Architecture](/architecture/information-systems/application-architecture)
- [Data Architecture](/architecture/information-systems/data-architecture)
- [Service Catalog](/architecture/information-systems/service-catalog)

## Phase D - Technology Architecture

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| Which reusable technology assets are used? | Dapr, OpenTelemetry, OIDC, Cloudflare, PostgreSQL/DataHub, object storage, GitHub Actions, Expo/mobile tooling. |
| What must be proven before customer data? | WAF/auth profile, hosted smoke, backup/restore, tenant isolation, secrets, observability, Dapr hardening evidence. |

Repository outputs:

- [Runtime Platform](/architecture/technology/runtime-platform)
- [Deployment Profiles](/architecture/technology/deployment-profiles)
- [Technology Standards](/architecture/technology/standards)
- [Security Architecture](/architecture/security/)

## Phases E/F - Opportunities, Solutions, And Migration

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| Which gaps block the target? | Customer durable state, DataHub projections, hosted public-domain evidence, role-centered UX validation, Dapr hardening, authoritative diagrams. |
| Which work packages close them? | Customer-ready hosted pilot, role-centered UX, diagram refresh, DataHub/read-model slices, hosted WAF/smoke slices. |

Repository outputs:

- [Gap Analysis](/architecture/architecture-states/gap-analysis)
- [Work Packages](/architecture/implementation-migration/work-packages)
- [Readiness](/architecture/implementation-migration/readiness)

## Phase G - Implementation Governance

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| How do we prove conformance? | PR evidence, CI, API stale-check, mobile typecheck, smoke runbooks, release validation, architecture review, waivers. |
| What cannot self-approve? | Claude, Copilot, Codex, or any implementer cannot approve its own implementation as architecture-complete. |

Repository outputs:

- [Architecture Contract](/architecture/governance/architecture-contract)
- [Architecture Review](/architecture/governance/architecture-review)
- [Readiness](/architecture/implementation-migration/readiness)

## Phase H - Architecture Change Management

| Learning Question | FairSpot Operator Answer |
| --- | --- |
| What triggers architecture change? | New customer requirement, new hosting profile, Billing activation, support model change, security exception, new Dapr capability decision. |
| Where is the change recorded? | Change control, waivers, architecture decisions, version register, affected artifacts. |

Repository outputs:

- [Change Control](/architecture/governance/change-control)
- [Waivers](/architecture/governance/waivers)
- [Architecture Decisions](/architecture/decisions)
- [Architecture Version Register](/architecture/architecture-states/architecture-version-register)

## Next Lesson

The next practical lesson is Preliminary: update governance and artifact rules so the repository clearly distinguishes product architecture from FairSpot Operator enterprise architecture.
