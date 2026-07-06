# Licensing Strategy

FairSpot is licensed as open-source software under **AGPL-3.0-or-later**. The durable decision is recorded in [Versions and Decisions](../versions-and-decisions), and the full license text is in the [repository LICENSE](https://github.com/RobertVejvoda/fairspot/blob/master/LICENSE).

## Current Position

| Area | Position |
| --- | --- |
| Repository license | AGPL-3.0-or-later. |
| Source availability | Modified network-service deployments must make corresponding source available under AGPL terms. |
| Client production | Clients can run FairSpot in their own environment, subject to AGPL obligations and any separate written agreement. |
| Brand identity | FairSpot name and logo are covered by the [FairSpot Brand Policy](./brand-policy). Forks and commercial offers must not imply official status or endorsement. |
| Pricing | No product pricing is decided in the documentation. |
| Commercial offer | First planning frame is documented in [Commercialisation Impact Review](./commercialisation). Do not publish subscription prices, discounts, referral credits, or sales commitments until there is an approved business model. |

## Open Core And The Platform Plane

FairSpot is **open core**: the public `fairspot` repository (this one) stays AGPL and contains the runtime, the fairness/Draw engine, tenant self-administration, and all customer-facing and architectural documentation. The **commercial line is the platform plane** — the hosted operator product (cross-tenant operations, platform console, hosted-deployment runbooks, onboarding-queue internals, usage metering) in the separate private `fairspot-platform` repository (open/private split #660, platform epic #633, licensing decision #642).

This means: the AGPL obligations above apply to the open core; the private platform plane and any dual-license offer are separate commercial decisions, **TBD with legal**, and must not be implied as already part of the public deliverable. Which documentation is public versus private is classified in the [Open-Core Documentation Boundary](./open-core-boundary), and the funnel/hosting model is in [Evaluation & Onboarding](./evaluation-and-onboarding).

## Why AGPL Fits FairSpot

- FairSpot is intended to stay open and inspectable.
- Fairness, auditability, and tenant trust benefit from source transparency.
- The network-service clause reduces the risk of closed SaaS forks that modify FairSpot without sharing improvements.
- The license still allows paid services such as implementation, hosting assistance, support, training, deployment packaging, and client-specific integration work, as long as those offers are documented separately and do not contradict AGPL.
- The in-product Legal/About notices preserve source, license, copyright, and brand attribution for users of web and mobile deployments.

## Future Commercial Options

The project may later define a commercial model, but it should be recorded as a separate business decision before any public pricing appears in the docs. The current impact review is [Commercialisation Impact Review](./commercialisation). Candidate options:

| Option | Description | Notes |
| --- | --- | --- |
| Support subscription | Paid support, maintenance guidance, upgrade help, and response targets for clients running FairSpot themselves. | Compatible with client-owned production. |
| Implementation package | Fixed-scope deployment, identity integration, observability integration, and demo/client pilot setup. | Fits the current Dapr/OpenTelemetry portability strategy. |
| Hosted demo service | FairSpot-operated demo or sandbox environment for evaluation only. | Must stay separate from client production ownership. |
| Dual licensing | Offer a separate commercial license for clients who cannot accept AGPL obligations. | Requires explicit legal/business approval before being documented as available. |

## Free Core And Paid Add-On Direction

FairSpot should remain useful as a free/open product. The free core should be good enough to prove the fairness model, run a normal tenant, and preserve trust in the allocation process. Paid options, if introduced later, should add convenience, scale, integration depth, or enterprise assurance rather than making the free version unusable.

| Layer | Free/open core candidate | Paid or sponsored candidate |
| --- | --- | --- |
| Tenant operation | Standard tenant setup, parking policies, slot configuration, employee booking, Draw, notifications, audit, and basic reporting. | Dedicated tenant deployment package, advanced tenant provisioning, migration support, and environment-specific hardening. |
| Reporting | Standard parking summary, fairness, utilization, and operational reports. | Enhanced reports, custom dashboards, export packs, executive analytics, benchmarking, and scheduled reporting. |
| Deployment | Local setup and documented client-owned production guidance. | Paid implementation package, managed demo/pilot environment, production readiness review, and client-specific deployment templates. |
| Support | Community documentation and public issue discussion. | Support subscription with response targets, upgrade help, release guidance, and security advisory handling. |
| Integrations | Generic OIDC, Dapr component contracts, OpenTelemetry guidance, and standard APIs. | Client-specific integrations for Entra/Keycloak, Dynatrace, HR systems, access control, license plate recognition, workplace calendars, or BI tools. |
| Licensing | AGPL source license. | Future dual-license option for clients that need commercial terms, subject to explicit legal/business approval. |

## Attribution And Brand Boundaries

AGPL requires copyright and license notices to be preserved when the software is conveyed, and the network-service clause requires modified server deployments to provide corresponding source to users. It does not force a reseller or hosted-service operator to market the product as Robert Vejvoda's work.

FairSpot therefore also keeps a visible Legal/About surface in the web and mobile clients and maintains a separate [FairSpot Brand Policy](./brand-policy). The policy allows truthful "based on FairSpot" attribution while preventing modified forks, hosted offers, or commercial services from implying that they are official FairSpot or endorsed by Robert Vejvoda without separate written agreement.

This is not a product promise. It is a planning frame for later commercialisation work so future Billing and licensing discussions do not accidentally weaken the open-source core.

## Documentation Rule

Do not add pricing or sales contact details to this page unless Robert explicitly approves the commercial model. Until then, this page should describe the repository license and possible future business models only.
