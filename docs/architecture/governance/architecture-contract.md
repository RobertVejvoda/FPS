# Architecture Contract

The architecture contract is the lightweight conformance agreement between architecture governance and delivery. It is not a legal contract; it records what implementation evidence must satisfy before customer-ready acceptance.

## Conformance Expectations

| Contract Item | Expected Conformance | Evidence | Exception Path | Owner |
| --- | --- | --- | --- | --- |
| Approved principles | Delivery choices follow Dapr-first runtime, tenant isolation, own-your-data, and evidence-before-approval principles. | PR notes, architecture review, decisions, affected artifacts. | Durable decision or waiver. | Codex/Product Owner |
| Technology standards | Runtime, API, event, persistence, ingress, observability, and secrets follow [Technology Standards](/architecture/technology/standards). | Generated contracts, runbooks, smoke evidence, deployment profile. | Architecture review and waiver for production-impacting deviations. | Codex / Robert |
| API and event contracts | Generated clients, OpenAPI, and event ownership stay aligned with service behavior. | [API Contracts](/architecture/information-systems/api-contracts), [Integrations and Events](/architecture/information-systems/integrations-events), CI output. | Change control if compatibility or ownership changes. | Codex / Claude |
| Security and privacy controls | Security, privacy, audit, and GDPR expectations are reflected in implementation and runbooks. | [Security Architecture](/architecture/security/security-architecture), [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls). | Security waiver with owner and review date. | Robert / Security reviewer |
| Tenant isolation | APIs derive tenant from authenticated context, data uses tenant-scoped boundaries, and events carry tenant context safely. | Tests, generated API contracts, data architecture, security review. | Rework unless Robert explicitly accepts a time-boxed gap. | Codex / Claude |
| Residual risks and gaps | Open risks and gaps have owner, mitigation, review date, and visible status. | [Gap Analysis](/architecture/architecture-states/gap-analysis), [Risk Register](/architecture/architecture-states/risk-register), [Readiness](/architecture/implementation-migration/readiness). | Architecture Board / Robert acceptance. | Codex/Product Owner |

## Review Outcomes

| Outcome | Meaning | Follow-up |
| --- | --- | --- |
| Conformant | Evidence satisfies the contract for the reviewed scope. | Update artifact status and review date where needed. |
| Conformant With Accepted Gaps | Work may proceed with named residual gaps or risks. | Link gaps, risks, waivers, and owners. |
| Non-Conformant | Material expectation is not met. | Rework, change request, or waiver before acceptance. |
