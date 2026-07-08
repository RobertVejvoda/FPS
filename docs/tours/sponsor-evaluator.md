# Sponsor &amp; Evaluator Tour

**Who this is for:** a business sponsor, procurement lead, or product evaluator deciding whether FairSpot is worth a pilot — without reading the whole repository.

**What matters to you:** the value and trust story, proof that it works, how it would roll out, and where the boundaries are.

## The value in one minute

FairSpot gives an organisation **fair, explainable access to scarce shared capacity**. Instead of manual email and spreadsheet coordination — opaque, slow, and easy to feel unfair — people request capacity and FairSpot allocates it by documented rules:

- **Fair access:** spaces are allocated by explicit, auditable fairness rules, not first-come-first-served or who knows the operator. Access improves over time.
- **Lower operational load:** request intake, the Draw, notifications, and audit records are automated, reducing HR and facilities effort.
- **Trust:** allocation rules, notifications, audit, GDPR erasure behaviour, and tenant isolation are explicit rather than implicit habits — so outcomes can be explained and reviewed.

Parking is the first launch module and proof vertical because it has visible scarcity, real policy complexity, and direct user-trust impact. The same model extends to seats, sport courts, desks, lockers, and chargers — one scarce-capacity direction, not a separate later product.

## The proof path

The clearest proof is the parking loop, end to end: a person requests a space → obligations (company-car) are honoured first → the Draw fairly allocates the rest → the person is notified and can act → operators and auditors can see the evidence behind every outcome. Walk it yourself in the [Green Logistics Walkthrough](./green-logistics-walkthrough), or hand it to a colleague using the [Resource User Tour](./resource-user).

## Rollout posture

FairSpot is designed to move along a clear path, with ownership shifting to the client over time:

| Profile | Owner | Purpose |
| --- | --- | --- |
| Local | Delivery team | Development and validation. |
| NAS / Cloudflare (Release 1 evaluation) | Delivery team / evaluator | Self-hosted, reviewable demo at a public HTTPS domain. |
| DigitalOcean demo | Delivery team | Cloud-hosted evaluation and operational evidence. |
| Client production | Client IT / operations | Real operation under client controls. |

The current direction is **client-owned production**: FairSpot provides the architecture, deployment guidance, component boundaries, and evidence; managed production operation is not promised until that model is explicitly agreed. **Release 1 is for synthetic/demo evaluation only** and is not approved for real customer data unless explicitly agreed.

## What to check next

- [Client Evaluation Pack](../client-evaluation-pack) — the one-page summary, deployment/cost assumptions, FAQ, and security/GDPR position.
- [Roadmap](../roadmap) and [Release 1 Scope](../roadmap#release-1-scope) — the authoritative, current status: what is ready, demo-only, and deferred.
- [Demo and Evaluation](../demo-and-evaluation) — the guided demo story and roles.

## Boundaries to know

- FairSpot is **SSO-first** and does not store customer passwords or replace the customer's identity provider.
- **Billing is not implemented** — commercialisation is a later decision; the AGPL project still supports paid implementation, support, and integration services.
- Cost is discussed **profile-based**, not as numeric commitments, until a provider and region are chosen.
- Hosted-operator internals (operator console, onboarding queue, usage metering) are a separate commercial platform plane in the private `fairspot-platform` companion — referenced here by summary only.
