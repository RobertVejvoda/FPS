# Tenant Admin Tour

**Who this is for:** a tenant administrator or customer IT owner setting an organisation up on FairSpot and getting it ready to go live.

**What matters to you:** knowing what to configure, how your people sign in, and what "ready" looks like — without needing hosted-operator procedures.

## The setup journey

1. **Tenant and identity.** Your organisation is a tenant, isolated from every other tenant by authenticated context and tenant-scoped storage. Decide how your people sign in: company SSO (your IdP, the default) or FairSpot accounts as a fallback. FairSpot is SSO-first and never stores your users' IdP passwords. The sign-in and tenant-discovery paths are in [Tenant Discovery and Login Modes](../business-layer/tenant-login-modes).
2. **First admin.** A first administrator is established for the tenant, who can then manage roles (employee, HR manager, report viewer, auditor, admin) and the rest of the setup. The onboarding shape is described in [Tenant Onboarding](../business-layer/tenant-onboarding).
3. **Policy.** Configure the tenant's allocation policy — the documented rules that govern how scarce capacity is shared, including where obligations like company-car parking take precedence. Policy is admin/HR-managed configuration, not code changes.
4. **Locations and slots.** Set up locations and their slots/capacity — for example a parking location with general, EV, accessible, motorcycle, and company-car slots. Configuration exposes location override and slot/capacity APIs for parking today and the wider resource map.
5. **Profile facts.** The minimum user/profile facts needed for policy (for example an HR-assigned company car or an accessibility need) drive eligibility. See the [HR Import Contract](../hr-import) for bringing those facts in.
6. **Readiness.** Confirm what must be in place before go-live — identity, policy, locations/slots, and profile facts — so the Draw behaves as configured.

> 📷 **Screenshot gap:** web _Tenant setup / policy / slots_ admin surfaces — real screens not yet captured. Source flow: web admin → configuration.

> 📊 **Journey diagram:** [tenant-onboarding.drawio](./diagrams/tenant-onboarding.drawio) — the setup → readiness → go-live flow (draw.io source; rendered PNG pending export).

## How to think about it

- **No code changes to run parking.** HR and admins manage rules, overrides, and capacity through configuration.
- **Isolation by design.** Employee APIs never accept a tenant or user id from a request body; the tenant comes from authenticated context. Your data and policies stay yours.
- **Obligations vs. lottery.** A company-car or fixed slot is an explainable, HR/facilities-controlled obligation — not a lottery preference an employee can self-assign.

## Try it in the demo

In **Green Logistics**, sign in as `gl-tenant-admin` (Karel Urban, password `Dev1234!`) for the admin console and readiness view, and `gl-hr-admin` (Lucie Prochazkova) for reports, configuration, and HR import. The seeded `GL-HQ` location and its six labelled slots show a realistic policy/capacity setup. Full context: [Demo Seed Data](../demo-seed-data).

## Go deeper

- [Tenant Admin Deck](./decks/tenant-admin-deck) — the slide version of this tour.
- [Tenant Onboarding](../business-layer/tenant-onboarding) and [Tenant Discovery and Login Modes](../business-layer/tenant-login-modes).
- [HR Import Contract](../hr-import) — bringing in profile/eligibility facts.
- [Business Policies](../architecture/business/policies) — the policy model behind configuration.

Detailed hosted-operator onboarding (the operator console and onboarding-queue internals) lives in the private `fairspot-platform` companion and is out of scope for self-service tenant setup.
