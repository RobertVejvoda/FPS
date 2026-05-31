# Security Architecture

FairSpot security centers on authenticated context, tenant isolation, least privilege, Dapr runtime hardening, privacy by default, and traceable business evidence.

| Security Area | Target Direction | Status | Source Evidence |
| --- | --- | --- | --- |
| Authentication | OIDC/SSO-first customer identity; local accounts are fallback/local only. | Partial | [Authentication](/security/authentication), [Identity](/business-layer/identity) |
| Authorization | Role-centered access for employee, HR/facility, tenant admin, system admin, auditor, support/operator, and system actors. | Partial | [Authorization](/security/authorization), [Roles](/business-layer/roles), [Actors and Roles](/architecture/business/actors-roles) |
| Tenant isolation | Tenant context derives from authenticated claims or trusted service context and controls API, storage, event, read-model, audit, and backup boundaries. | Partial | [Security Model](/security/security-model), [Tenant Storage Contract](/production/tenant-storage-contract) |
| Service-to-service security | Dapr service invocation with mTLS/Sentry where supported; user context forwarded only where downstream authorization requires it. | Placeholder | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Dapr component hardening | Component scopes, secret scopes, API token/app token hardening, resiliency policies, and state encryption where supported. | Placeholder | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Secrets | Runtime secrets come from profile-specific secret stores and are never committed, printed, exported casually, or embedded in component files. | Partial | [Environments](/security/environments), [Security Model](/security/security-model) |
| Public ingress | Hosted profiles use Cloudflare/WAF/rate limits/Access and block admin, internal, metrics, Dapr, Swagger/OpenAPI, database, broker, and observability surfaces. | Partial | [Cloudflare WAF Profile](/security/cloudflare-waf-profile), [Deployment Profiles](/architecture/technology/deployment-profiles) |
| Audit evidence | Business evidence is append-only where possible and pseudonymised where required. Audit is separate from technical telemetry. | Partial | [Audit](/security/audit), [Security Model](/security/security-model) |
| Observability safety | Logs, metrics, and traces support operations without secrets, raw personal data, full payloads, or business-audit replacement. | Partial | [Observability](/architecture/technology/observability), [Local Observability](/local-observability) |
| DataHub/read-model privacy | Projections preserve tenant scope, minimal data, role-safe views, and approved exports only. | Placeholder | [Data Architecture](/architecture/information-systems/data-architecture), [Data Privacy](/security/data-privacy) |

## Trust Boundary Rules

- End users access FairSpot through HTTPS and OIDC Authorization Code + PKCE where applicable.
- Public ingress terminates at the selected gateway/tunnel profile; internal services and Dapr sidecars remain private.
- Backend services validate JWTs/claims and must not trust request bodies, query strings, or caller-supplied headers for tenant/user identity.
- Service-owned stores use tenant-safe keys, collections, partitions, or schemas derived centrally from trusted context.
- Dapr secures runtime transport and component access, but does not replace application authorization or privacy filtering.
- Domain events omit secrets, stack traces, hidden lottery internals, raw names/emails/license plates, and unrelated employee data.
- Audit PII mapping is a restricted path with reason capture and its own audit trail.

## Required Trust Boundary Diagram

Placeholder: a security trust-boundary view should show browser/mobile clients, Cloudflare/Tunnel/WAF, API gateway, Keycloak/OIDC, services, Dapr sidecars, state stores, pub/sub broker, DataHub PostgreSQL, Audit PII mapping, observability backends, and secret stores.
