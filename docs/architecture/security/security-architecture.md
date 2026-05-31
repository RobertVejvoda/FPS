# Security Architecture

| Security Area | Target Direction | Source Evidence |
| --- | --- | --- |
| Authentication | OIDC/SSO-first customer identity; local accounts are fallback only. | [Authentication](/security/authentication), [Identity](/business-layer/identity) |
| Authorization | Role-centered access for employee, HR/facility, admin, auditor, and support flows. | [Authorization](/security/authorization), [Roles](/business-layer/roles) |
| Tenant isolation | Tenant context derives from authenticated claims/service context and controls storage/event/read boundaries. | [Security Model](/security/security-model) |
| Secrets | Runtime secrets come from secret stores and are never committed or logged. | [Environments](/security/environments) |
| Public ingress | Hosted profiles use WAF/rate limits and block admin/internal surfaces. | [Cloudflare WAF Profile](/security/cloudflare-waf-profile) |
| Audit evidence | Business evidence is append-only/pseudonymised where required. | [Audit](/security/audit) |
