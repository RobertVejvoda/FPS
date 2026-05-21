# Billing Application

This page is a documentation-only guardrail. It does not approve product Billing implementation.

Billing is not an active FPS application module. The current commercialisation decision defers in-product Billing until the project validates a real support, implementation, hosted-demo, dual-license, or subscription offer. See [Commercialisation Impact Review](../strategy-layer/commercialisation) and [Billing Business](../business-layer/billing).

## Current Application Position

| Area | Position |
| --- | --- |
| Financial collection | Out of scope. |
| Invoice generation | Deferred. External accounting tools may handle invoices before FPS needs an internal workflow. |
| Subscription enforcement | Deferred. Do not block core allocation, audit, reporting, or privacy features behind paid unlocks. |
| Commercial records | Future candidate, tenant-scoped, contract-level, and separate from employee booking data. |

## Possible Future Application Functions

If Billing is later approved, prefer minimal contract-management functions before financial processing:

- maintain a tenant commercial account summary;
- record support subscription or service-package status;
- store external invoice references where needed;
- audit commercial-record changes;
- expose finance/admin views only to explicit commercial roles.

Financial collection, refund/dispute handling, fraud handling, and provider callbacks require a separate approved decision because they introduce financial, tax, privacy, security, and operational obligations.
