# FairSpot Guided Tours

A role-based way to explore FairSpot. Pick the tour that matches why you are here — each one is a short, guided path through the product story, the screens or APIs you would touch, and the deeper docs behind them.

FairSpot gives people **a fair shot at scarce shared capacity**. When more people want a parking space, a desk, a court, or a charger than there are to go around, FairSpot replaces "who emailed the operator first" with a transparent booking and **Draw** process: obligations like company-car parking are honoured first, and the rest is allocated by documented fairness rules so access improves over time and everyone can see why they got the outcome they did. Parking is the first launch module and proof vertical; the same model covers seats, sport courts, desks, lockers, and chargers.

## Pick your path

| You are a… | Start here | You'll learn |
| --- | --- | --- |
| Sponsor / business evaluator | [Sponsor &amp; Evaluator Tour](./sponsor-evaluator) | What FairSpot does, the value and trust story, the parking proof path, and how a pilot is scoped. |
| Resource user / participant | [Resource User Tour](./resource-user) | Sign in, request capacity, follow My Bookings, get notified, cancel — and see fair allocation in action. |
| Tenant administrator / customer IT | [Tenant Admin Tour](./tenant-admin) | Tenant setup, identity/login paths, first admin, policy, locations and slots, and go-live readiness. |
| HR / facilities / resource operator | [HR &amp; Operator Tour](./operator-hr) | Eligibility facts, company-car and fixed-space rules, Draw timing, manual actions, and the evidence behind outcomes. |
| Auditor / security evaluator | [Auditor &amp; Security Tour](./auditor-security) | Tenant isolation, claim-based identity, audit evidence, pseudonymisation, and the GDPR/privacy summary. |
| Technical evaluator / self-hosted operator | [Technical Evaluator Tour](./technical-evaluator) | The local/container evaluation path, the demo-seed story, these Docsify docs, and the release/evidence boundary. |

## Follow along in the demo

Every tour points at the **Green Logistics** showcase — a small, synthetic tenant designed for guided evaluation. Bring the container stack up with seed data and you can walk the same steps yourself:

```bash
./tools/start-container-stack.sh --seed
```

| What | Where |
| --- | --- |
| API gateway | `http://localhost:10000` |
| Web app | `./tools/start-smoke-web.sh` → `http://localhost:5200` |
| Mobile (Expo) | `./tools/start-smoke-mobile.sh` |
| Keycloak sign-in | `http://localhost:8180` (realm `fps-local`) |

Green Logistics demo users share the password `Dev1234!`. The full user list, roles, locations, slots, and the seeded Draw story are in [Demo Seed Data](../demo-seed-data), and a step-by-step run is in the [Green Logistics Walkthrough](./green-logistics-walkthrough). Use synthetic demo data only unless a customer-approved pilot explicitly changes that rule.

## Presenter decks and journey diagrams

- **Decks.** Slide outlines for live sessions — [Tour Decks](./decks/) — for the sponsor/evaluator, tenant-admin, and resource-user tours. Each slide traces back to a doc so decks stay current.
- **Journey diagrams.** Editable draw.io sources for the core journeys: the [booking → Draw → notification loop](./diagrams/booking-draw-loop.drawio) and the [tenant onboarding flow](./diagrams/tenant-onboarding.drawio), registered in the [diagram catalog](../architecture/views/diagrams). Rendered PNGs are a pending manual export.

## About these tours

- **Source of truth.** Tours are reader paths, not new facts. They summarise and link into the canonical pages ([Product Overview](../Home), [Client Evaluation Pack](../client-evaluation-pack), [Demo and Evaluation](../demo-and-evaluation), the [Architecture Repository](../architecture/), and [Security Architecture](../architecture/security/)). Where a tour and a source page disagree, the source page wins.
- **Screenshots.** Product screens shown here come from real demo flows. Where a screen has not been captured yet, the tour marks a **📷 screenshot gap** rather than inventing one.
- **Open core.** These tours cover the public open-core product only. Hosted-operator runbooks, the operator console, onboarding-queue internals, and usage metering live in the private `fairspot-platform` companion and are referenced by contract/summary only. See the [Open-Core Boundary](../strategy-layer/open-core-boundary).
