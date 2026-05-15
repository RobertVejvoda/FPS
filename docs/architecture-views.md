# Architecture Views

This page prepares an ArchiMate-style hierarchy for FPS architecture documentation. It is a navigation and structure page: detailed diagrams and models can be added over time without changing the overall story.

## Viewpoint Map

| View | Audience | Question Answered | Current Source Pages |
| --- | --- | --- | --- |
| Motivation view | Sponsors, product owners, architects | Why does FPS exist and which outcomes matter? | [Strategy](./strategy), [Core Values](./strategy-layer/core-values), [Business Requirements](./business-layer/requirements) |
| Capability view | Business evaluators, architects | Which business capabilities does FPS provide? | [Business Layer](./business-layer), [Functional Architecture](./business-layer/functional-architecture) |
| Business process view | HR, facilities, auditors | How do requests, Draw, allocation, cancellation, confirmation, and audit work? | [Process](./business-layer/process), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle), [Allocation Rules](./business-layer/allocation-rules) |
| Role and actor view | Business evaluators, security reviewers | Which actors use or operate FPS and what are they responsible for? | [Personas](./business-layer/personas), [Roles](./business-layer/roles), [Authorization](./security/authorization) |
| Application cooperation view | Architects, implementers | Which services collaborate and through which contracts/events? | [Application Layer](./application-layer), [Function Map](./application-layer/function-map), [Booking Event Contracts](./business-layer/booking-event-contracts) |
| Application component view | Architects, developers | What are the bounded contexts and service responsibilities? | [Software Architecture](./technology-layer/software-architecture), service pages under Application and Technology layers |
| Data and security view | Security reviewers, architects | Which data exists, who can access it, and how is it protected? | [Security](./security), [Data Privacy](./security/data-privacy), [Traceability](./security/traceability) |
| Technology deployment view | Client IT, operators | How does FPS run locally, in demo, and in client-owned production? | [Production](./production), [Hosting and Deployment Strategy](./production/hosting-deployment-strategy), [Monitoring](./production/monitoring) |
| Implementation roadmap view | Product owners, maintainers | Which slices deliver which capabilities and evidence? | [Implementation Tracker](./implementation-tracker), [Requirements Traceability](./requirements-traceability), [Development Plan](./development-plan) |

## Layer Hierarchy

| ArchiMate-style layer | FPS documentation section | Content to maintain |
| --- | --- | --- |
| Strategy / Motivation | [Strategy](./strategy) | Product goals, value, constraints, licensing, future extension notes. |
| Business | [Business Layer](./business-layer) | Actors, roles, business requirements, processes, policies, reason codes, booking lifecycle. |
| Application | [Application Layer](./application-layer) | Application functions, service responsibilities, API and event contracts, user-facing app surfaces. |
| Technology | [Technology Layer](./technology-layer) | Runtime technologies, service packages, Dapr boundaries, data stores, non-functional requirements. |
| Security | [Security](./security) | Data classification, authentication, authorization, encryption, audit, compliance, security operations. |
| Production / Operations | [Production](./production) | Deployment options, observability, backup/restore, incidents, maintenance, readiness evidence. |
| Implementation / Traceability | [Implementation](./implementation), [Requirements Traceability](./requirements-traceability) | Slice order, PR evidence, validation, remaining gaps, GitHub links. |

## Planned View Content

| Priority | View | Goal | Notes |
| --- | --- | --- | --- |
| 1 | Capability view | Show FPS as a fair reservation capability set, with parking as the first product domain. | Add future desk/chair/seat booking only as a future capability option. |
| 2 | Application cooperation view | Show Booking publishing events to Notification, Audit, Reporting, and future consumers. | Use Dapr pub/sub as the integration boundary. |
| 3 | Data and security view | Show tenant/user context, public/internal/confidential/secret data, and audit/erasure flows. | Link to security pages rather than duplicating controls. |
| 4 | Technology deployment view | Show local, demo, and client-owned production deployment variants. | Keep Dapr components pluggable. |
| 5 | Implementation roadmap view | Show slice order and evidence from GitHub issues/PRs. | Keep the tracker as the source of truth. |

## Modeling Rules

- Prefer ArchiMate-style concepts and clear view names over framework-heavy process text.
- Keep views layered but connected: business capability should trace to application services, technology components, security controls, and implementation slices.
- Use placeholders when the model is not mature yet; do not invent decisions that are not recorded in [Versions and Decisions](./versions-and-decisions).
- When a view becomes durable, link it from this page and update the corresponding layer index.
