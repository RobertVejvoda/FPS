# Legacy Architecture Evidence

This page is retained as source evidence for the earlier ArchiMate-style view hierarchy. It is no longer the authoritative architecture entry point.

Use the [Architecture Repository](./architecture/) for current target architecture, governance, artifact status, versioning, transition states, and gap analysis. Use [Views and Diagrams](./architecture/views/) for the current viewpoint catalog, source-evidence diagram inventory, required target diagrams, and Robert TODO placeholders.

## Current Target Architecture Entry Points

| Concern | Current Target Artifact |
| --- | --- |
| Architecture repository structure | [Architecture Repository](./architecture/) |
| Architecture vision and scope | [Architecture Vision](./architecture/architecture-vision) |
| Stakeholders and concerns | [Stakeholders and Concerns](./architecture/stakeholders-and-concerns) |
| Requirements management | [Architecture Requirements](./architecture/requirements) |
| Business capabilities, value streams, actors, processes, and policies | [Business Architecture](./architecture/business/) |
| Service boundaries, DataHub/read models, API contracts, and events | [Information Systems](./architecture/information-systems/) |
| Runtime platform, deployment profiles, and observability | [Technology Architecture](./architecture/technology/) |
| Security, privacy, controls, and gaps | [Security Architecture](./architecture/security/) |
| Baseline, target, transition, and gap tracking | [Architecture States](./architecture/architecture-states/) |
| Viewpoint and diagram control | [Views and Diagrams](./architecture/views/) |
| Architecture governance and approval workflow | [Governance](./architecture/governance/) |

## Source Evidence Map

The older layer pages below remain useful evidence while detailed contracts, diagrams, and validation notes are still being migrated or superseded.

| Earlier View | Earlier Source Evidence | Current Target Home |
| --- | --- | --- |
| Motivation view | [Strategy](./strategy), [Core Values](./strategy-layer/core-values), [Business Requirements](./business-layer/requirements) | [Architecture Vision](./architecture/architecture-vision), [Principles](./architecture/principles), [Requirements](./architecture/requirements) |
| Capability view | [Business Layer](./business-layer), [Functional Architecture](./business-layer/functional-architecture), [Exchange Map Validation](./exchange-map-validation) | [Capabilities](./architecture/business/capabilities), [Value Streams](./architecture/business/value-streams) |
| Business process view | [Process](./business-layer/process), [Booking Request Lifecycle](./business-layer/booking-request-lifecycle), [Allocation Rules](./business-layer/allocation-rules), BPMN files under `docs/process/` | [Business Processes](./architecture/business/business-processes), [Policies](./architecture/business/policies), [Diagrams](./architecture/views/diagrams) |
| Role and actor view | [Personas](./business-layer/personas), [Roles](./business-layer/roles), [Role Intent Roadmap](./business-layer/role-intent-roadmap) | [Stakeholders and Concerns](./architecture/stakeholders-and-concerns), [Actors and Roles](./architecture/business/actors-roles) |
| Application cooperation view | [Application Layer](./application-layer), [Software Architecture](./technology-layer/software-architecture), [Application Architecture 1 Validation](./application-arch-1-validation) | [Application Architecture](./architecture/information-systems/application-architecture), [Service Catalog](./architecture/information-systems/service-catalog), [Diagrams](./architecture/views/diagrams) |
| Data and read-model view | [DataHub](./application-layer/datahub), [Reporting Business](./business-layer/reporting), [Reporting Application](./application-layer/reporting) | [Data Architecture](./architecture/information-systems/data-architecture), [Integrations and Events](./architecture/information-systems/integrations-events) |
| Technology deployment view | [Production](./production), [Hosting and Deployment Strategy](./production/hosting-deployment-strategy), [NAS Cloudflare Deployment](./production/nas-cloudflare-deployment-profile) | [Deployment Profiles](./architecture/technology/deployment-profiles), [Runtime Platform](./architecture/technology/runtime-platform), [Observability](./architecture/technology/observability) |
| Security and privacy view | [Security](./security), [Security Model](./security/security-model), [Data Privacy](./security/data-privacy), [Cloudflare WAF Profile](./security/cloudflare-waf-profile) | [Security Architecture](./architecture/security/security-architecture), [Privacy Architecture](./architecture/security/privacy-architecture), [Controls](./architecture/security/controls) |
| Roadmap and transition view | [Roadmap](./roadmap), [Implementation Tracker](./implementation-tracker), [Versions and Decisions](./versions-and-decisions) | [Transition Architectures](./architecture/architecture-states/transition-architectures), [Gap Analysis](./architecture/architecture-states/gap-analysis), [Architecture Version Register](./architecture/architecture-states/architecture-version-register) |

## Legacy Modeling Notes

These notes are preserved because they still guide diagram refresh work:

- Prefer clear ArchiMate-style concepts and view names over framework-heavy process text.
- Keep core architecture provider-neutral. Provider-specific setup belongs in deployment profiles and runbooks unless a provider decision is recorded in [Versions and Decisions](./versions-and-decisions).
- Keep views layered but connected: business capability should trace to application services, technology components, security controls, and implementation slices.
- Use placeholders when the model is not mature yet; do not invent decisions that are not recorded in the architecture repository or decision log.
- Use architecture state pages to distinguish target architecture from current-state evidence and implementation gaps.

## Retirement Rule

This page can be retired only after the authoritative target diagrams exist and no current architecture artifact depends on this legacy view hierarchy as source evidence.
