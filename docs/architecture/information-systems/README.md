# Information Systems Architecture

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Target |
| ADM Phase | Phase C - Information Systems Architecture |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | Before customer architecture review |

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
