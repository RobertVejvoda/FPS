# Billing Business

This page is a documentation-only guardrail. It does not approve product Billing implementation.

Billing is a deferred commercial capability. FPS does not currently need in-product financial collection to prove its parking allocation value, and implementing Billing too early would encode unvalidated business assumptions.

The source of truth for the current decision is [Commercialisation Impact Review](../strategy-layer/commercialisation). The short version is:

- keep the free/open core useful and trustworthy;
- recover cost first through support, implementation, pilot setup, production readiness, and client-specific integration;
- defer in-product Billing until the commercial offer is approved;
- keep employee booking data out of commercial records unless a later decision explicitly approves a commercial purpose.

## Current Scope

| Area | Current decision |
| --- | --- |
| Product Billing | Deferred. |
| Subscription enforcement | Out of scope. |
| Invoice handling | Out of scope; may stay in external accounting tooling. |
| Support/service contracts | Candidate commercial path, but not yet an FPS product workflow. |
| Employee-level commercial metering | Not approved. Do not charge by employee allocation behavior by default. |

## Future Billing Gate

Before `BILL001` or any Billing implementation starts, the project must answer:

- what is being sold: support, implementation, hosted demo, dual license, product subscription, or another offer;
- who owns the commercial record and who is allowed to manage it;
- whether invoice handling belongs inside FPS or outside FPS;
- which data is required for commercial records and which employee data is explicitly excluded;
- which financial-record, tax, privacy, security, and audit obligations apply.

## Possible Future Capabilities

If Billing becomes necessary, the first implementation should be contract-level and tenant-scoped:

| Capability | Notes |
| --- | --- |
| Commercial account summary | Tenant-level commercial status, agreement reference, support tier, renewal date, and responsible contact. |
| Support subscription record | Response targets, covered environments, support window, and escalation contact. |
| Service package tracking | Pilot setup, production readiness review, or integration package status. |
| External invoice reference | Link to an external accounting invoice rather than storing financial collection details in FPS. |
| Commercial audit events | Changes to commercial records should be audited without exposing employee booking details. |

Financial collection workflows, refund/dispute handling, fraud handling, and provider callbacks should remain outside FPS unless a later approved commercial model proves they are needed.
