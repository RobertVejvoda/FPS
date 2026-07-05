# Access Control

FairSpot access control combines OIDC authentication, tenant-scoped authorization, role-based permissions, and operational least privilege.

## Application Access

- Employee APIs operate only on the authenticated employee's tenant/user context.
- HR/facilities, admin, report-viewer, and auditor roles are tenant-scoped.
- No cross-tenant application roles exist.
- Request bodies and query strings must not override tenant or user identity.
- Privileged reads and policy-sensitive actions must produce audit evidence where required.

## Platform Access

- Hosted operator access must be named, justified, time-bound, and logged.
- Secret-store administration is break-glass or tightly controlled operations access.
- Keycloak admin, databases, brokers, Dapr sidecars, and observability backends must not be public.
- Cloudflare Access, VPN/tunnel access, local admin access, or a client-approved equivalent should protect operator-only surfaces.

## Authentication Assurance

Authentication assurance (MFA/passkey strength) layers on top of role-based access; it does not replace claim-based scoping. Policy is defined in [Authentication → Multi-Factor Authentication and Passkeys](./authentication) and [Tenant Login Modes](../business-layer/tenant-login-modes).

- Normal employee accounts use the baseline factor of the enforcing identity provider.
- Administrator, HR/facilities, auditor, and platform/operator roles carry a stricter expectation: a phishing-resistant factor (passkey/WebAuthn) or mandatory MFA.
- Break-glass accounts carry the strictest expectation and are few, named, periodically reviewed, and disabled when no longer needed.
- For company-SSO tenants this stricter expectation is met by the customer IdP's policy; for FairSpot-local accounts it is enforced by FairSpot-controlled Keycloak.
- A satisfied second factor strengthens login assurance only. Tenant, user, and role identity continue to come solely from validated claims.

## Reviews

- Review role mappings before hosted demos and client pilots.
- Review privileged access after incidents and at regular intervals.
- Remove stale access when a user changes role or leaves the project/customer.
- Rotate secrets after human break-glass access.
