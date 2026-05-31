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
| SEC-013 | Backup and restore operations that can expose Confidential or Secret data require authorization, evidence, restore validation, and restore-time re-erasure handling. | [Backup And Restore](/production/backup-restore), [RTO/RPO Requirements](/production/rto-rpo-requirements) | Operations/Security | Draft |
| SEC-014 | Resource-map publication and policy-impact preview must be tenant-scoped, audited, advisory where previewed, and must not expose employee-level allocation internals by default. | [Business Policies](/architecture/business/policies), [Information Systems](/architecture/information-systems/) | Product/Security | Draft |
| SEC-015 | Sponsor management summaries and exports must be aggregate by default, role-authorized, tenant-scoped, and protected against spreadsheet formula injection. | [Privacy Architecture](/architecture/security/privacy-architecture), [Data Architecture](/architecture/information-systems/data-architecture) | Product/Security | Draft |

## Evidence Needed Before Hosted Pilot

- Cloudflare/WAF rules applied or exported for the public domain.
- Origin scan or smoke evidence showing internal paths are not public.
- OIDC public-domain redirect/CORS/cookie validation.
- Dapr sidecar/component access hardening evidence for the selected profile.
- Dapr component scopes, secret scopes, resiliency policies, state encryption support, and sidecar/API token settings reviewed for the selected profile.
- Tenant-scoped persistence restart test for P0 state.
- Backup/restore drill evidence for authoritative state or an accepted hosted-pilot waiver.
- Log/trace sample review showing no secrets or raw confidential payloads.
- Audit record sample for privileged Draw/cancellation and PII mapping access where applicable.
- Role-safe DataHub projection samples for HR support, sponsor summaries, and resource-map publication views.
