# Architecture Continuum

The Architecture Continuum classifies architecture guidance from generic to organization-specific.

## Concept

| Level | Meaning | FairSpot Learning Use |
| --- | --- | --- |
| Foundation Architecture | Generic architecture principles, methods, standards, and patterns. | TOGAF-style repository, ADM checklist, governance model, Dapr-first principle, tenant isolation principle. |
| Common Systems Architecture | Reusable architecture patterns common to many enterprises. | Identity, observability, backup/restore, support, onboarding, WAF, deployment profile, CI/CD. |
| Industry Architecture | Architecture patterns for a market or industry. | SaaS-like workplace resource allocation, GDPR/privacy, auditability, commercial support, customer-owned production. |
| Organization-Specific Architecture | Target architecture for a concrete enterprise. | FairSpot Operator commercial operating model and target enterprise architecture. |

## FairSpot Operator Example

| Level | Example Architecture Asset | Owning / Candidate Page | Gap |
| --- | --- | --- | --- |
| Foundation | Architecture governance rules and artifact lifecycle. | [Governance](/architecture/governance/), [Artifact Lifecycle](/architecture/governance/artifact-lifecycle) | Robert approval model remains draft. |
| Foundation | Dapr-first runtime and outbox/security/resiliency expectations. | [Runtime Platform](/architecture/technology/runtime-platform), [Technology Standards](/architecture/technology/standards) | Hosted-profile evidence remains incomplete. |
| Common Systems | Customer onboarding, identity mapping, support, readiness, backup/restore. | [Business Processes](/architecture/business/business-processes), [Readiness](/architecture/implementation-migration/readiness), [Production Runbooks](/production) | Operator support process is not yet modeled as enterprise process. |
| Industry | Tenant isolation, GDPR, audit evidence, customer evaluation pack. | [Security Architecture](/architecture/security/), [Client Evaluation Pack](/client-evaluation-pack) | Customer-facing approval state remains draft. |
| Organization-Specific | FairSpot Operator commercial service catalog and operating capabilities. | Candidate updates to [Capabilities](/architecture/business/capabilities), [Value Streams](/architecture/business/value-streams), [Architecture Vision](/architecture/architecture-vision) | Needs Phase A/B work. |

## How To Apply

When adding a new architecture statement, ask:

| Question | Action |
| --- | --- |
| Is this generic guidance? | Put it in principles, standards, governance, or constraints. |
| Is this a reusable enterprise function? | Put it in business/process/technology architecture as a common system pattern. |
| Is this industry-specific? | Connect it to security, privacy, compliance, customer evaluation, or commercialisation. |
| Is this specific to the fictive operator? | Put it in FairSpot Operator target architecture and mark it organization-specific. |

## Next ADM Step

Use Preliminary and Phase A to define the FairSpot Operator architecture scope before filling detailed business and technology architecture.
