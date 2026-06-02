# Architecture Decisions

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Cross-cutting |
| **ADM Phase** | Preliminary / Phase H |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-06-02 |
| **Next Review** | On durable architecture decision change |

Architecture decisions here restate durable architecture choices from [Versions and Decisions](/versions-and-decisions) in ADR-style form. The product-level history stays in `versions-and-decisions.md`; this page is the architecture-layer decision index.

## Decision Register

| Decision ID | Decision | Rationale | Decision Maker | Date | Status | Related Artifacts |
| --- | --- | --- | --- | --- | --- | --- |
| ADR-001 | Core architecture is provider-neutral bring-your-own-cloud, not Azure-only. | FairSpot must run locally, in a small hosted profile, and in client-owned environments without binding the core architecture to one cloud. | Codex/Product Owner | 2026-05-22 | Accepted | [Deployment Profiles](/architecture/technology/deployment-profiles), [Constraints](/architecture/constraints) |
| ADR-002 | ASP.NET Core / .NET is the server platform. | .NET gives a mature cross-platform backend stack and aligns with existing service implementation. | Robert | 2024-10-01 | Accepted | [Technology Standards](/architecture/technology/standards), [Runtime Platform](/architecture/technology/runtime-platform) |
| ADR-003 | Dapr is the runtime building-block layer. | Dapr provides provider-neutral workflow, pub/sub, state, secrets, service invocation, resiliency, and outbox capabilities where suitable. | Robert / Codex | 2024-10-01 / 2026-05-30 | Accepted | [Technology Standards](/architecture/technology/standards), [Runtime Platform](/architecture/technology/runtime-platform) |
| ADR-004 | React and React Native + Expo replace MAUI for client applications. | TypeScript consistency, Expo managed workflow, OTA update options, and ecosystem fit are stronger for FairSpot. | Robert | 2026-05-09 | Accepted | [Technology Standards](/architecture/technology/standards), [Deployment Profiles](/architecture/technology/deployment-profiles) |
| ADR-005 | EF Core is not the primary persistence model. | The architecture boundary is service-owned persistence and Dapr/state-store contracts; database products are profile-specific implementation choices. | Robert | 2026-05-09 | Accepted | [Runtime Platform](/architecture/technology/runtime-platform), [Data Architecture](/architecture/information-systems/data-architecture) |
| ADR-006 | Writes stay service-owned and cross-service reads move to event-fed DataHub read models. | Command ownership remains clear while HR/admin/reporting views can query tenant-scoped projections without reading private service stores. | Codex/Product Owner | 2026-05-30 | Accepted | [Data Architecture](/architecture/information-systems/data-architecture), [Integrations and Events](/architecture/information-systems/integrations-events), [Risk Register](/architecture/architecture-states/risk-register) |
| ADR-007 | Tenant isolation is a cross-phase architecture requirement. | Tenant context affects business process, API scoping, persistence keys, events, projections, security, audit, and implementation review. | Robert / Codex | 2026-05-14 | Accepted | [Requirements](/architecture/requirements), [Security Architecture](/architecture/security/security-architecture), [Architecture Contract](/architecture/governance/architecture-contract) |
| ADR-008 | Generated API contracts are authoritative where available. | Generated or source-of-truth contracts reduce drift between implementation, clients, and architecture documentation. | Codex/Product Owner | 2026-06-02 | Draft | [API Contracts](/architecture/information-systems/api-contracts), [Technology Standards](/architecture/technology/standards) |

## Relationship To Product History

| Source | Use |
| --- | --- |
| [Versions and Decisions](/versions-and-decisions) | Historical product, delivery, commercial, and architecture decision log. |
| This page | Architecture decision index used by architecture reviews and conformance checks. |
