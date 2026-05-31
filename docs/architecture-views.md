# Architecture Views

This page prepares an ArchiMate-style hierarchy for FairSpot architecture documentation. It is a navigation and structure page: detailed diagrams and models can be added over time without changing the overall story.

Core architecture pages describe provider-neutral product, service, data, security, and integration contracts. Local, demo, Azure, AWS, Kubernetes, and client-owned deployment details belong under [Production](./production.md) as environment profiles or implementation examples. Do not make Azure, AWS, Kubernetes, Traefik, Envoy, or any other runtime product part of the core design unless the decision is recorded in [Versions and Decisions](./versions-and-decisions.md).

FairSpot now uses a lightweight TOGAF 10-inspired architecture repository under [Architecture Repository](./architecture/). The older layer pages remain legacy/source evidence until migrated or superseded. New target-state architecture, governance, artifact status, baseline/target versioning, and gap analysis should be maintained under `docs/architecture/`.

## Viewpoint Map

| View | Audience | Question Answered | Current Source Pages |
| --- | --- | --- | --- |
| Motivation view | Sponsors, product owners, architects | Why does FairSpot exist and which outcomes matter? | [Strategy](./strategy.md), [Core Values](./strategy-layer/core-values.md), [Business Requirements](./business-layer/requirements.md) |
| Capability view | Business evaluators, architects | Which business capabilities does FairSpot provide? | [Business Layer](./business-layer.md), [Functional Architecture](./business-layer/functional-architecture.md) |
| Business process view | HR, facilities, auditors | How do requests, Draw, allocation, cancellation, confirmation, and audit work? | [Process](./business-layer/process.md), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle.md), [Allocation Rules](./business-layer/allocation-rules.md) |
| Role and actor view | Business evaluators, security reviewers | Which actors use or operate FairSpot and what are they responsible for? | [Personas](./business-layer/personas.md), [Roles](./business-layer/roles.md), [Authorization](./security/authorization.md) |
| Application cooperation view | Architects, technical evaluators | Which services collaborate and through which contracts/events? | [Software Architecture](./technology-layer/software-architecture.md), [Technology Direction](./technology-layer.md) |
| Application component view | Architects, technical evaluators | What are the bounded contexts and service responsibilities? | [Software Architecture](./technology-layer/software-architecture.md) |
| Data and security view | Security reviewers, architects | Which data exists, who can access it, and how is it protected? | [Security](./security.md), [Data Privacy](./security/data-privacy.md), [Traceability](./security/traceability.md) |
| Technology deployment view | Client IT, operators | How does FairSpot run locally, in demo, and in client-owned production? | [Production](./production.md), [Hosting and Deployment Strategy](./production/hosting-deployment-strategy.md), [Monitoring](./production/monitoring.md) |
| Roadmap view | Product owners, evaluators | Which capability areas come next? | [Roadmap](./roadmap.md), [Versions and Decisions](./versions-and-decisions.md) |
| Architecture governance view | Architects, client IT, product owners | What is draft or approved, what target is being modeled, and which gaps remain? | [TOGAF ADM Map](./architecture/togaf-adm-map.md), [Artifact Register](./architecture/artifact-register.md), [Gap Analysis](./architecture/architecture-states/gap-analysis.md) |

## Layer Hierarchy

| ArchiMate-style layer | FairSpot documentation section | Content to maintain |
| --- | --- | --- |
| Strategy / Motivation | [Strategy](./strategy.md) | Product goals, value, constraints, licensing, future extension notes. |
| Business | [Business Layer](./business-layer.md) | Actors, roles, business requirements, processes, policies, reason codes, booking lifecycle. |
| Application | [Software Architecture](./technology-layer/software-architecture.md) | Bounded contexts, service responsibilities, integration direction, and user-facing app surfaces. |
| Technology | [Technology Layer](./technology-layer.md) | Provider-neutral runtime technologies, service packages, Dapr boundaries, data stores, and non-functional requirements. |
| Security | [Security](./security.md) | Data classification, authentication, authorization, encryption, audit, compliance, security operations. |
| Production / Operations | [Production](./production.md) | Local/demo/client deployment profiles, provider-specific options, observability, backup/restore, incidents, maintenance, readiness evidence. |
| Governance / Architecture states | [Architecture Repository](./architecture/) | TOGAF ADM mapping, target architecture folders, artifact status/versioning, baseline and target versions, transition states, and gap analysis. |

## Planned View Content

| Priority | View | Goal | Notes |
| --- | --- | --- | --- |
| 1 | Capability view | Show FairSpot as a fair reservation capability set, with parking as the first product domain. | Add future desk/chair/seat booking only as a future capability option. |
| 2 | Application cooperation view | Show Booking publishing events to Notification, Audit, Reporting, and future consumers. | Use Dapr pub/sub as the integration boundary. |
| 3 | Data and security view | Show tenant/user context, public/internal/confidential/secret data, and audit/erasure flows. | Link to security pages rather than duplicating controls. |
| 4 | Technology deployment view | Show local, demo, and client-owned production deployment variants. | Keep Dapr components pluggable. |
| 5 | Product roadmap view | Show phase order, milestones, and capability evidence at a business-readable level. | Keep implementation evidence out of public-facing architecture views unless it helps client evaluation. |

## Modeling Rules

- Prefer ArchiMate-style concepts and clear view names over framework-heavy process text.
- Keep core architecture provider-neutral. Reference provider-specific setup only from production/deployment pages.
- Keep views layered but connected: business capability should trace to application services, technology components, security controls, and implementation slices.
- Use placeholders when the model is not mature yet; do not invent decisions that are not recorded in [Versions and Decisions](./versions-and-decisions.md).
- When a view becomes durable, link it from this page and update the corresponding layer index.
- Use architecture state pages to distinguish target architecture from current-state evidence and implementation gaps.
