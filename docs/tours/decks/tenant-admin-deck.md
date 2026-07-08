# Tenant Admin Deck

A slide outline for a customer-IT setup walkthrough. One slide per section; each carries a **Source:** link. Presenter view of the [Tenant Admin Tour](../tenant-admin).

---

## 1 — What you're setting up

- Your organisation is a **tenant**, isolated from every other tenant by authenticated context and tenant-scoped storage.
- Goal of this session: know what to configure and what "ready" looks like.

*Source: [Tenant Admin Tour](../tenant-admin)*

---

## 2 — Identity and sign-in

- SSO-first: your people sign in through your IdP by default; FairSpot never stores their passwords.
- FairSpot accounts are a fallback/break-glass path.
- Tenant discovery from work email or SSO.

*Source: [Tenant Discovery and Login Modes](../../business-layer/tenant-login-modes)*

---

## 3 — First admin and roles

- Establish the first administrator, then assign roles: employee, HR manager, report viewer, auditor, admin.
- Roles gate what each person can see and do.

*Source: [Tenant Onboarding](../../business-layer/tenant-onboarding)*

---

## 4 — Policy

- Configure the allocation policy — the documented rules for sharing scarce capacity.
- Includes where obligations (company-car, fixed slots) take precedence.
- Admin/HR-managed configuration, not code changes.

*Source: [Business Policies](../../architecture/business/policies)*

---

## 5 — Locations and slots

- Set up locations and their slots/capacity (e.g. general, EV, accessible, motorcycle, company-car).
- Location override and slot/capacity APIs cover parking today and the wider resource map.

*Source: [Tenant Admin Tour → Locations and slots](../tenant-admin)*

---

## 6 — Profile facts

- Minimum profile facts drive eligibility (company car, accessibility need).
- Facts are managed, not self-service claims — so priority stays explainable.

*Source: [HR Import Contract](../../hr-import)*

---

## 7 — Readiness &amp; go-live

- Confirm identity, policy, locations/slots, and profile facts are in place so the Draw behaves as configured.
- Hosted-operator onboarding (operator console, onboarding queue) is out of scope for self-service — it lives in the private `fairspot-platform` companion.

*Source: [Tenant Admin Tour → Readiness](../tenant-admin)*
