# Tenant Discovery and Login Modes

**Status:** Implemented on web for Release 1 — the sign-in screen offers company-SSO work-email discovery and FairSpot-account sign-in. Mobile currently uses a single OIDC sign-in. Original decision and remaining follow-ups tracked under AUTH001–AUTH005.
**Tracks:** Issue #539 (AUTH001)
**Related decisions:** `versions-and-decisions.md` → *Two-path login model and tenant discovery*

---

## Overview

FairSpot presents employees with two login entry paths:

| Path | Label on login screen | When used |
|---|---|---|
| Company SSO | "Continue with company SSO" | Normal login for employees of an SSO-integrated company |
| FairSpot account | "Sign in with FairSpot account" | Demo users, small tenants without SSO, break-glass admin, and fallback local accounts |

Both paths use the same Keycloak instance. Company SSO is brokered through Keycloak's identity-provider broker to the company's external IdP. FairSpot-local accounts are stored in Keycloak and validated directly.

---

## Pre-Auth Tenant Discovery

Before showing the SSO path, FairSpot can help the user find the right tenant and IdP by asking for their work email or company domain.

**Discovery is routing only.** It selects a candidate tenant and suggests an IdP. It does not grant access, does not establish session state, and does not prove the user belongs to the tenant.

### Discovery flow

```
User types: alice@greenlogistics.example
                        │
                        ▼
          Extract domain: greenlogistics.example
                        │
                        ▼
     Look up configured tenant by email-domain mapping
                        │
              ┌─────────┴──────────┐
         Found                 Not found
              │                     │
              ▼                     ▼
  Suggest "Continue with     Fall back to manual
   company SSO" for that     tenant / path selection
       tenant's IdP                 │
              │                     ▼
              └─────────┬──────────┘
                        │
                        ▼
          User selects path and proceeds to login
```

**What discovery must NOT do:**

- Grant or imply tenant membership.
- Reveal which tenants exist to unauthenticated users (keep responses opaque for unknown domains).
- Accept tenant identity from the request body for any downstream operation.
- Replace post-auth enforcement.

---

## Post-Auth Tenant Enforcement

Tenant access is determined **after** authentication, from the validated token and FairSpot configuration — never from pre-auth routing alone.

After the IdP issues a token, FairSpot validates:

| Check | Requirement |
|---|---|
| Trusted issuer | Token `iss` matches a configured issuer for a tenant. Unknown issuers fail closed. |
| Audience | Token `aud` matches the expected FairSpot API client. |
| Signature and expiry | Standard JWT validation passes. |
| Tenant mapping | Issuer-to-tenant mapping is deterministic. If both issuer mapping and a `tenant_id` claim exist, they must agree. |
| Stable subject | Token contains a stable OIDC `sub` or equivalent immutable external subject. |
| Tenant membership | User's `(tenantId, issuer, externalSubject)` tuple must be provisioned in FairSpot. Unknown subjects fail closed. |
| Active status | User must be active. Deactivated users cannot create new parking requests. |

FairSpot services derive tenant identity from authenticated context only. Employee-facing APIs must not accept caller-supplied tenant, user, or role values.

---

## Company SSO Path

This is the primary login path for enterprise customers.

1. User visits the FairSpot login page.
2. Optional: user enters work email for tenant discovery (see above).
3. User selects "Continue with company SSO".
4. Keycloak acts as a broker and redirects the user to the company's external IdP (e.g. Entra ID, Okta, Google Workspace).
5. The company IdP authenticates the user and issues a token back to Keycloak.
6. Keycloak issues a FairSpot token with mapped claims.
7. FairSpot validates the token and resolves the tenant, user, and roles from configured mappings.

**Key properties:**

- FairSpot never sees or stores the user's company password.
- Tenant and role mapping are configured per tenant, not inferred from arbitrary claims.
- A new SSO user not yet provisioned in FairSpot profile facts is rejected at tenant-membership check until provisioned by SSO mapping, admin entry, or SCIM.
- Role/group claims from the IdP are mapped through tenant-scoped configuration. Unmapped groups are ignored unless the tenant configuration explicitly rejects them.

See [Customer Integration](./customer-data-import.md) for the full SSO contract, claim requirements, and integration modes.

---

## FairSpot Account Path

This is the fallback path for accounts owned by FairSpot Identity.

**Allowed use cases:**

- Demo users (e.g. `employee1`, `hr-admin` in the `demo` tenant; `gl-employee1` in the Green Logistics tenant).
- Small tenants without a company IdP.
- Break-glass administrator accounts.
- Fallback accounts for employees who cannot complete SSO for operational reasons.

**Rules:**

- FairSpot-local accounts are tenant-scoped and distinguishable from SSO-mapped users in admin views.
- FairSpot Identity owns password hashing, reset, lockout, and credential-verifier storage. Credential verifiers are **Secret** data.
- Customer passwords and external password hashes must never be imported.
- Creating, disabling, resetting, or privilege-changing a local account requires an audit record with actor, target user, tenant, reason, and timestamp.
- Break-glass accounts should be few, named, periodically reviewed, and disabled when no longer needed.

See [Customer Integration](./customer-data-import.md) → *Local Account Fallback* for the full local-account rules.

---

## Green Logistics Demo Tenant

FairSpot ships **two** local demo tenants. **Green Logistics** is the out-of-the-box one — `dev-seed.sh` populates its profiles, vehicles, bookings, and Draw for the full employee booking/Draw/notification/audit flow, and it also demonstrates company-SSO / work-email tenant discovery (the `greenlogistics.example` domain). The `demo` tenant remains a **bare scaffold** (the Customer/Configuration startup seed creates it, but no profile/booking/draw data is seeded into it) used only for multi-tenant isolation and SSO-contrast checks.

| Aspect | Detail |
|---|---|
| Tenant id | `greenlogistics` (the out-of-the-box seeded demo tenant; `demo` remains a bare isolation scaffold) |
| Email domain | `greenlogistics.example` (reserved `.example` domain for demo) |
| Login path | FairSpot-local accounts for local demo; company-SSO broker when an external IdP is configured |
| Demo users | `gl-employee1` (Jan Novak), `gl-tenant-admin`, `gl-hr-admin`, `gl-auditor`, `gl-report-viewer` — all `tenant_id=greenlogistics` |
| Identity seeding | Provisioned by `tools/dev-setup-auth.sh` in the `fps-local` realm. |
| Data seeding | `dev-seed.sh` seeds the canonical `gl-v1` dataset (employees, vehicles, ~20 GL-HQ slots, policy) and triggers a Draw. (Visible Draw allocations are pending the Booking slot-source unification — #665.) |

See [Demo Seed Data](../demo-seed-data) for the full user list, password, and the "which tenant to use" guidance. For local/demo runs without an external IdP, Green Logistics users sign in through the FairSpot-account path; the SSO broker path requires a configured external IdP.

Local Keycloak remains the identity provider for all demo and local environments. It brokers outbound to an external IdP when SSO is configured; it validates FairSpot-local credentials directly otherwise.

---

## Keycloak Role in Both Paths

Keycloak is the single OIDC issuer for FairSpot in all current deployment profiles (local, demo, NAS).

| Scenario | Keycloak role |
|---|---|
| FairSpot-local account | Keycloak validates credentials and issues a token directly |
| Company SSO | Keycloak acts as identity broker, redirects to external IdP, receives the result, maps claims, and issues a FairSpot token |
| Demo without external IdP | FairSpot-local accounts in Keycloak (seeded demo users) |
| Break-glass admin | FairSpot-local account in Keycloak, not published through SSO broker |

This keeps a single token issuer, a single token-validation configuration across FairSpot services, and a single realm configuration per environment. Adding a new company SSO means adding a Keycloak identity-provider broker configuration for that tenant — no FairSpot service code changes are required.

External IdP broker setup is tracked in AUTH006 (issue #544).

---

## What Tenant Discovery Is Not

To avoid security misunderstandings, this table records explicit non-goals:

| Misunderstanding | Correct model |
|---|---|
| "Typing my company email gives me access to that tenant" | Discovery only routes to an IdP suggestion. Access requires authenticated token + tenant membership. |
| "The tenant in the login URL grants access" | Tenant in routing context is a hint only. Post-auth enforcement determines access. |
| "If discovery fails to find my company, I cannot log in" | Discovery failure falls back to manual path selection. FairSpot-local account path is always available for provisioned users. |
| "Company SSO means FairSpot has my company password" | FairSpot never receives the company password. The company IdP authenticates the user independently. |

---

## Planned Implementation Slices

| Slice | Issue | Scope |
|---|---|---|
| AUTH002: Add tenant branding and discovery model | #540 | Tenant discovery data model, email-domain mapping, opaque lookup behavior |
| AUTH003: Seed Green Logistics demo tenant | #541 | Demo tenant record, seeded users, and FairSpot-local login for demo flow |
| AUTH004: Add company SSO and FairSpot account entry paths | #542 | Login screen with two entry paths, discovery email input, and IdP routing |
| AUTH005: Implement domain-based tenant discovery | #543 | Domain-to-tenant lookup, opaque response for unknown domains, routing to IdP |
| AUTH006: Document external IdP broker test setup | #544 | Keycloak identity-provider broker configuration guidance for demo/test |
