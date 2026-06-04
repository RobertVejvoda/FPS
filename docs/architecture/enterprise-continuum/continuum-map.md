# Continuum Map

This page maps FairSpot assets into the Enterprise Continuum. It is intentionally simple: the goal is to learn where an asset belongs before deciding whether it needs more detail.

## Concept

The Enterprise Continuum is a classification model. It helps an architect distinguish reusable architecture guidance, reusable solution building blocks, and organization-specific architecture assets.

For FairSpot, this prevents mixing three different things:

| Asset Type | Example | Why It Matters |
| --- | --- | --- |
| Product architecture | FairSpot Booking, Draw, Customer, DataHub, mobile, and web architecture. | Describes the product being built. |
| Operator enterprise architecture | How a fictive company commercializes, hosts, supports, and governs FairSpot. | Describes the enterprise running the product commercially. |
| Reusable solution building blocks | Dapr, Cloudflare, OIDC, PostgreSQL, observability stack, runbooks. | Describes implemented or candidate technology assets. |

## FairSpot Operator Scope

FairSpot Operator is a fictive company that:

- offers FairSpot evaluation and pilot setup;
- operates a hosted demo/evaluation profile;
- helps customers deploy or validate client-owned production;
- provides support, readiness review, and implementation assistance;
- keeps product Billing deferred until the commercial offer is approved.

## Continuum Placement

| Continuum Area | FairSpot Asset | Current Repository Location | Status |
| --- | --- | --- | --- |
| Foundation Architecture | TOGAF-inspired repository rules, ADM checklist, architecture governance concepts. | [ADM Checklist](/architecture/adm-checklist), [Governance](/architecture/governance/) | Draft |
| Foundation Solution | Dapr building blocks, OpenTelemetry, OIDC, HTTP APIs, GitHub Actions, Docsify. | [Technology Standards](/architecture/technology/standards), [Runtime Platform](/architecture/technology/runtime-platform) | Partial |
| Common Systems Architecture | Identity, support, customer onboarding, observability, backup/restore, deployment evidence. | [Technology Architecture](/architecture/technology/), [Operations Runbooks](/production) | Partial |
| Common Systems Solution | Keycloak/OIDC profile, Cloudflare, NAS hosting, Dapr components, generated API clients. | [Deployment Profiles](/architecture/technology/deployment-profiles), [NAS Cloudflare Deployment](/production/nas-cloudflare-deployment-profile) | Partial |
| Industry Architecture | SaaS/workplace-resource allocation, GDPR/privacy, tenant isolation, auditability, customer-owned deployment. | [Security Architecture](/architecture/security/), [Privacy Architecture](/architecture/security/privacy-architecture), [Commercialisation](/strategy-layer/commercialisation) | Partial |
| Industry Solution | Customer evidence pack, support package, production-readiness review, demo seed/runbooks. | [Client Evaluation Pack](/client-evaluation-pack), [Demo Seed Data](/demo-seed-data), [Readiness](/architecture/implementation-migration/readiness) | Partial |
| Organization-Specific Architecture | FairSpot Operator target operating model and commercial service capabilities. | To be added through Business Architecture and Architecture Vision updates. | Gap |
| Organization-Specific Solution | Actual hosted demo, customer onboarding evidence, support process, accepted release baseline. | Release 1 validation and hosted smoke evidence. | Gap |

## First Learning Outcome

The immediate gap is not another product feature. The immediate architecture gap is distinguishing **FairSpot as product** from **FairSpot Operator as enterprise**.

The next ADM step is Preliminary: define the architecture capability, governance boundary, and repository placement for the fictive operator architecture.
