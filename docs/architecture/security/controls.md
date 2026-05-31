# Controls

| Control ID | Control | Evidence | Owner | Status |
| --- | --- | --- | --- | --- |
| SEC-001 | Tenant/user context must come from authenticated claims or service context. | [Security Model](/security/security-model) | Architecture Owner | Draft |
| SEC-002 | Public hosted profile must block internal/admin paths at ingress/WAF. | [Cloudflare WAF Profile](/security/cloudflare-waf-profile) | Operations/Security | Draft |
| SEC-003 | Secrets must use profile-specific secret stores and must not be committed. | [Environments](/security/environments) | Operations/Security | Draft |
| SEC-004 | Audit records must preserve business evidence without unnecessary PII exposure. | [Audit](/security/audit) | Audit/Security | Draft |
| SEC-005 | Role-specific UI must avoid exposing technical tenant/internal identifiers to employees. | [My Spots UX](/business-layer/my-spots-ux) | Product Owner | Draft |
| SEC-006 | Dapr sidecar APIs, metrics, internal health, Swagger/OpenAPI, database, broker, and observability endpoints must not be publicly exposed. | [Deployment Profiles](/architecture/technology/deployment-profiles), [Cloudflare WAF Profile](/security/cloudflare-waf-profile) | Operations/Security | Draft |
| SEC-007 | Dapr mTLS/Sentry, component scopes, secret scopes, resiliency policies, and state encryption must be used where supported by the deployment profile. | [Runtime Platform](/architecture/technology/runtime-platform), [Dapr-First Standards](/production/dapr-first-production-standards) | Operations/Security | Draft |
| SEC-008 | Business events must omit secrets, stack traces, raw names/emails/license plates, hidden lottery internals, and unrelated employee details. | [Integrations and Events](/architecture/information-systems/integrations-events), [Security Model](/security/security-model) | Architecture Owner | Draft |
| SEC-009 | DataHub projections must preserve tenant scope, classification, role-safe output shape, idempotent event processing, and export controls. | [Data Architecture](/architecture/information-systems/data-architecture), [Privacy Architecture](/architecture/security/privacy-architecture) | Architecture Owner | Draft |
| SEC-010 | Secret access, rotation, export, and break-glass activity must be tracked without recording secret values. | [Security Model](/security/security-model) | Operations/Security | Draft |
| SEC-011 | Technical telemetry must not replace business audit evidence and must not contain confidential payloads or secrets. | [Observability](/architecture/technology/observability), [Local Observability](/local-observability) | Operations/Security | Draft |
| SEC-012 | Rights-request workflows must preserve audit accountability while deleting, anonymising, pseudonymising, or retaining data according to policy. | [Data Privacy](/security/data-privacy) | Privacy/Security | Draft |

## Evidence Needed Before Hosted Pilot

- Cloudflare/WAF rules applied or exported for the public domain.
- Origin scan or smoke evidence showing internal paths are not public.
- OIDC public-domain redirect/CORS/cookie validation.
- Dapr sidecar/component access hardening evidence for the selected profile.
- Tenant-scoped persistence restart test for P0 state.
- Log/trace sample review showing no secrets or raw confidential payloads.
- Audit record sample for privileged Draw/cancellation and PII mapping access where applicable.
