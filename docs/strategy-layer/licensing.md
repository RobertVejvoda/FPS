# Licensing Strategy

FPS is licensed as open-source software under **AGPL-3.0-or-later**. The durable decision is recorded in [Versions and Decisions](../versions-and-decisions), and the full license text is in the [repository LICENSE](https://github.com/RobertVejvoda/FPS/blob/master/LICENSE).

## Current Position

| Area | Position |
| --- | --- |
| Repository license | AGPL-3.0-or-later. |
| Source availability | Modified network-service deployments must make corresponding source available under AGPL terms. |
| Client production | Clients can run FPS in their own environment, subject to AGPL obligations and any separate written agreement. |
| Pricing | No product pricing is decided in the documentation. |
| Commercial offer | Not defined yet. Do not publish subscription prices, discounts, referral credits, or sales commitments until there is an approved business model. |

## Why AGPL Fits FPS

- FPS is intended to stay open and inspectable.
- Fairness, auditability, and tenant trust benefit from source transparency.
- The network-service clause reduces the risk of closed SaaS forks that modify FPS without sharing improvements.
- The license still allows paid services such as implementation, hosting assistance, support, training, deployment packaging, and client-specific integration work, as long as those offers are documented separately and do not contradict AGPL.

## Future Commercial Options

The project may later define a commercial model, but it should be recorded as a separate business decision before any public pricing appears in the docs. Candidate options:

| Option | Description | Notes |
| --- | --- | --- |
| Support subscription | Paid support, maintenance guidance, upgrade help, and response targets for clients running FPS themselves. | Compatible with client-owned production. |
| Implementation package | Fixed-scope deployment, identity integration, observability integration, and demo/client pilot setup. | Fits the current Dapr/OpenTelemetry portability strategy. |
| Hosted demo service | FPS-operated demo or sandbox environment for evaluation only. | Must stay separate from client production ownership. |
| Dual licensing | Offer a separate commercial license for clients who cannot accept AGPL obligations. | Requires explicit legal/business approval before being documented as available. |

## Free Core And Paid Add-On Direction

FPS should remain useful as a free/open product. The free core should be good enough to prove the fairness model, run a normal tenant, and preserve trust in the allocation process. Paid options, if introduced later, should add convenience, scale, integration depth, or enterprise assurance rather than making the free version unusable.

| Layer | Free/open core candidate | Paid or sponsored candidate |
| --- | --- | --- |
| Tenant operation | Standard tenant setup, parking policies, slot configuration, employee booking, Draw, notifications, audit, and basic reporting. | Dedicated tenant deployment package, advanced tenant provisioning, migration support, and environment-specific hardening. |
| Reporting | Standard parking summary, fairness, utilization, and operational reports. | Enhanced reports, custom dashboards, export packs, executive analytics, benchmarking, and scheduled reporting. |
| Deployment | Local setup and documented client-owned production guidance. | Paid implementation package, managed demo/pilot environment, production readiness review, and client-specific deployment templates. |
| Support | Community documentation and public issue discussion. | Support subscription with response targets, upgrade help, release guidance, and security advisory handling. |
| Integrations | Generic OIDC, Dapr component contracts, OpenTelemetry guidance, and standard APIs. | Client-specific integrations for Entra/Keycloak, Dynatrace, HR systems, access control, license plate recognition, workplace calendars, or BI tools. |
| Licensing | AGPL source license. | Future dual-license option for clients that need commercial terms, subject to explicit legal/business approval. |

This is not a product promise. It is a planning frame for later commercialisation work so future Billing and licensing discussions do not accidentally weaken the open-source core.

## Documentation Rule

Do not add pricing or sales contact details to this page unless Robert explicitly approves the commercial model. Until then, this page should describe the repository license and possible future business models only.
