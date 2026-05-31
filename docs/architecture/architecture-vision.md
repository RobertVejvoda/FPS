# Architecture Vision

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Target |
| Target Version | Customer-Ready Target v0.1 |
| ADM Phase | Phase A - Architecture Vision |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | Before customer architecture review |

FairSpot is an open-source, parking-first fair allocation platform for companies where demand for shared workplace resources exceeds supply.

## Problem

Companies with limited parking often coordinate requests through email, spreadsheets, or informal priority rules. That creates avoidable HR/facilities work, low transparency, weak auditability, and low employee trust.

## Target Outcome

FairSpot provides a transparent request and Draw process with role-appropriate user experiences, auditable allocation rules, tenant-scoped data, and a deployment model that can run locally, in a hosted pilot, or in a customer-owned environment.

## Goals

- Make the employee booking and allocation outcome understandable.
- Make HR/facility operation repeatable and auditable.
- Keep tenant identity and data boundaries explicit.
- Use Dapr-first provider-neutral runtime contracts where they fit.
- Prepare enough architecture evidence for customer and client IT evaluation.

## Non-Goals

- Billing and commercial enforcement are not part of the customer-ready target.
- A full enterprise baseline model is not required for FairSpot v1.
- Provider-specific deployment products are examples or profiles, not core architecture.

## Source Evidence

- [Strategy](/strategy)
- [Client Evaluation Pack](/client-evaluation-pack)
- [Versions and Decisions](/versions-and-decisions)
