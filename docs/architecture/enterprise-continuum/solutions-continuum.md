# Solutions Continuum

The Solutions Continuum classifies actual or candidate solution building blocks from generic to organization-specific.

## Concept

The Architecture Continuum describes architecture guidance. The Solutions Continuum describes implementable assets.

Examples:

| Architecture Guidance | Solution Building Block |
| --- | --- |
| Use OIDC for authentication. | Keycloak local/demo profile or customer IdP integration. |
| Use portable service integration. | Dapr sidecars, components, pub/sub, state store, workflow. |
| Use public ingress protection. | Cloudflare Tunnel, WAF rules, origin hardening. |
| Use event-fed read models. | DataHub PostgreSQL projections and event inbox. |

## FairSpot Operator Solution Assets

| Continuum Level | Solution Building Block | Current Evidence | Status |
| --- | --- | --- | --- |
| Foundation Solution | GitHub Actions CI, .NET 10, React/React Native, Docsify docs site. | Repository workflows, code structure, docs site. | Partial |
| Foundation Solution | Dapr runtime building blocks. | Dapr component profiles and runtime platform docs. | Partial |
| Common Systems Solution | OIDC/Keycloak integration profile. | Identity and NAS/Cloudflare auth docs. | Partial |
| Common Systems Solution | Observability, backup/restore, local harness, hosted smoke runbooks. | [Production Runbooks](/production) | Partial |
| Common Systems Solution | Cloudflare protected public-domain profile. | [NAS Cloudflare Deployment](/production/nas-cloudflare-deployment-profile), [Cloudflare WAF Profile](/security/cloudflare-waf-profile) | Partial |
| Industry Solution | Client evaluation pack and synthetic demo data. | [Client Evaluation Pack](/client-evaluation-pack), [Demo Seed Data](/demo-seed-data) | Partial |
| Organization-Specific Solution | Release 1 accepted hosted evaluation baseline. | Release 1 validation issue and branch. | In progress |
| Organization-Specific Solution | FairSpot Operator support and commercial service tooling. | Not yet modeled. | Gap |

## Reuse Rule

Do not document a solution building block as organization-specific when it is reusable across projects.

For example, Dapr Workflow is a Foundation/Common Systems solution asset. FairSpot Draw scheduling is the organization/product-specific use of that reusable asset.

## Next ADM Step

During Phases C and D, link each target application/technology choice to a reusable solution building block or explicitly mark it as FairSpot Operator-specific.
