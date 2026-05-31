# Information Systems Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.4 |
| **Architecture State** | Target |
| **ADM Phase** | Phase C - Information Systems Architecture |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before customer architecture review |

Information systems architecture defines FairSpot application, data, service, API, and event boundaries.

## Migration Status

Core information systems direction has been restated from legacy application, technology, DataHub, and contract docs. It is still `Draft` because DataHub contracts, Customer persistence, Reporting cleanup, and generated API contract evidence are not complete.

| Area | Status | Notes |
| --- | --- | --- |
| Application architecture | Partial | Service boundaries and client applications are stated. Some legacy Reporting semantics need cleanup. |
| Data architecture | Partial | Write ownership and DataHub read-model direction are stated. Customer storage and projections remain gaps. |
| Integrations and events | Partial | Event families, envelope expectations, and Dapr/outbox direction are stated. Full event catalog implementation remains pending. |
| Service catalog | Partial | Services and ownership are stated. Criticality and persistence gaps are explicit. |
| API contracts | Partial | Contract boundaries are stated. Some OpenAPI/read API contracts still need source-of-truth generation or publication. |

## Requirement Interpretation

| Requirement | Information Systems Interpretation | Evidence | Gap |
| --- | --- | --- | --- |
| AR-001 | APIs, events, service stores, DataHub projections, and audit references derive tenant scope from authenticated or trusted service context. | [Application Architecture](/architecture/information-systems/application-architecture), [Data Architecture](/architecture/information-systems/data-architecture), [API Contracts](/architecture/information-systems/api-contracts) | Projection isolation evidence and generated contract checks. |
| AR-002 / AR-012 / AR-013 | Owning services keep command-side writes; DataHub owns durable cross-service reads; Reporting does not own operational projections. | [Data Architecture](/architecture/information-systems/data-architecture), [Integrations and Events](/architecture/information-systems/integrations-events) | DataHub inbox, checkpoint, projection health, and first projections. |
| AR-006 / AR-008 | Web/mobile/API boundaries must support role-specific employee, HR, tenant admin, system admin, and audit workflows. | [Application Architecture](/architecture/information-systems/application-architecture), [Service Catalog](/architecture/information-systems/service-catalog) | Role-specific API/read contracts and HR cancellation validation evidence. |
| AR-011 | Customer tenant lifecycle, readiness, identity setup, first admins, and parking bootstrap must be durably stored. | [Service Catalog](/architecture/information-systems/service-catalog), [Data Architecture](/architecture/information-systems/data-architecture) | Customer durable persistence slice. |
| AR-017 / AR-020 | Configuration owns resource-map publication; DataHub provides advisory impact/read-model projections where reliable. | [Application Architecture](/architecture/information-systems/application-architecture), [Data Architecture](/architecture/information-systems/data-architecture), [API Contracts](/architecture/information-systems/api-contracts) | Resource-map publication APIs/events and impact-preview projections. |
| AR-018 | HR support reads need safe request lookup and lifecycle explanation without hidden Draw internals or unrelated employee data. | [API Contracts](/architecture/information-systems/api-contracts), [Data Architecture](/architecture/information-systems/data-architecture) | HR query contracts and privacy-shaped DataHub/Booking reads. |
| AR-019 | Sponsor summaries require aggregated DataHub-backed business-value projections. | [Data Architecture](/architecture/information-systems/data-architecture), [Service Catalog](/architecture/information-systems/service-catalog) | Sponsor-safe projection definitions, filters, and export boundaries. |

## Legacy Evidence Disposition

| Legacy Source | Target Disposition |
| --- | --- |
| `application-layer/**` | Source evidence for service responsibilities and client surfaces. New authoritative service/data/API/event boundaries belong here. |
| `application-layer/datahub.md` | Migrated directionally into [Data Architecture](/architecture/information-systems/data-architecture) and [Integrations and Events](/architecture/information-systems/integrations-events). Detailed implementation slices remain delivery work. |
| `technology-layer/software-architecture.md` and service package diagrams | Source evidence for component shape; authoritative target service ownership belongs in [Application Architecture](/architecture/information-systems/application-architecture) and [Service Catalog](/architecture/information-systems/service-catalog). |
| Booking API/event contract pages | Remain implementation contracts until generated API/event evidence and source-of-truth contracts are consolidated here. |
| Reporting legacy pages | Deferred/obsolete for operational projection storage; keep only report catalog/configuration/presentation if retained. |

## Contents

- [Application Architecture](/architecture/information-systems/application-architecture)
- [Data Architecture](/architecture/information-systems/data-architecture)
- [Integrations and Events](/architecture/information-systems/integrations-events)
- [Service Catalog](/architecture/information-systems/service-catalog)
- [API Contracts](/architecture/information-systems/api-contracts)

## Source Evidence

- [Application Layer](/application-layer)
- [DataHub](/application-layer/datahub)
- [Software Architecture](/technology-layer/software-architecture)
- [Booking API Contract](/business-layer/booking-api-contract)
- [Booking Event Contracts](/business-layer/booking-event-contracts)
