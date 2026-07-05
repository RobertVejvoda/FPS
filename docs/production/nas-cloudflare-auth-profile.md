# NAS Cloudflare Auth Profile

> **Moved private (#684):** the detailed hosted-operator auth runbook now lives in the private `fairspot-platform` repository at `docs/runbooks/nas-cloudflare-auth-profile.md`.

This public page records the identity contract only. Environment-specific Keycloak, Envoy, Cloudflare, and client-redirect setup belongs to the private platform runbook.

## Public Contract

| Area | Requirement |
| --- | --- |
| Login modes | FairSpot supports company SSO and FairSpot-local fallback accounts according to [Tenant Login Modes](../business-layer/tenant-login-modes). |
| Token issuer | The selected OIDC provider issues the authenticated subject, tenant claim, and role claims used by FairSpot services. |
| Tenant isolation | Backend authorization derives tenant/user/role identity from validated claims. Tenant context is never trusted from request bodies or UI state. |
| Public domains | Hosted profiles use separate application and authentication hostnames, protected by HTTPS. |
| Platform plane | Cross-tenant platform roles are separate from tenant roles and are only honored from the trusted platform issuer. |
| MFA / passkeys | For FairSpot-local accounts, FairSpot-controlled Keycloak enforces MFA (passkey/WebAuthn preferred, OTP/TOTP fallback, recovery codes). For company SSO, the customer IdP enforces MFA. Detailed WebAuthn/OTP realm configuration lives in the private platform runbook. See [Authentication → MFA and Passkeys](../security/authentication). |
| Secrets | Client secrets, realm signing material, admin passwords, tunnel tokens, and recovery keys are secret data and stay out of Git. |

## Public References

- [Tenant Login Modes](../business-layer/tenant-login-modes)
- [Security Architecture](../architecture/security/)
- [Authentication](../security/authentication)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
- [Keycloak WebAuthn / passkeys](https://www.keycloak.org/docs/latest/server_admin/#webauthn)
