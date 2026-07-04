# Evaluation & Onboarding Strategy

**Status:** Strategy note for review. Captures how prospects discover, evaluate, and adopt
FairSpot. Companion to [Commercialisation](./commercialisation.md) and
[Licensing](./licensing.md).

> Authored by Claude at Robert's direction (2026-06-29). `docs/` is normally Codex's domain —
> flagged for Codex review.

## 1. Governing model — open runtime, paid platform (Diagrid/Dapr)

FairSpot follows the **Dapr / Diagrid** pattern:

- **Open (AGPL, public `fairspot`)** — the *runtime + fairness*. Anyone can self-host a single
  organisation and get fair allocation. "Run and fair" stays open. This is the Dapr-equivalent.
- **Private (`fairspot-platform`, commercial)** — the *managed platform*: the cross-tenant
  operator product plus the operational know-how. This is the Diagrid-equivalent — the paid
  product, with know-how withheld.

A direct consequence: **we do not run a permanent, anonymous public demo site.** A standing
public demo is really us operating the paid platform for free, and carries cost, abuse, and
exposure. Evaluation happens through a *guided pilot* (below). Technical evaluators already
have a one-command local demo via the open repo (`tools/start-container-stack.sh --seed`).

## 2. The guided pilot funnel

The principle: instead of an anonymous sandbox with generic data, **every evaluation is
qualified, provisioned around the prospect's own parking problem, accompanied, and time-boxed
toward a decision.** It doubles as the sales pipeline and a feature-signal board.

| # | Stage | What happens | Plane | Status |
|---|---|---|---|---|
| 1 | **Land** | Public page; CTAs *Start a FairSpot Pilot* (high intent) + *Explore the Green Logistics tour* (low commitment, static/safe) | open | built (#656/#705) |
| 2 | **Capture** | Submit company / domain / work email / *parking challenge*; Turnstile + rate-limited → sales email + operator queue | open (intake) / private (queue) | built (#637/#650, #651/#653) |
| 3 | **Qualify (triage)** | Operator reviews fit (size, region, modules); approve/reject with reason, audited | private | to build (PLAT008C) |
| 4 | **Provision tailored workspace** | Spin up a *dedicated* eval tenant, seeded to mirror their situation, their branding — not a shared playground | private | partial (#634/#645 + seed; automation PLAT008B/C) |
| 5 | **Onboard + walkthrough** | Contact them, give access, run a guided session; in-app readiness checks act as a setup checklist | private + CS | partial |
| 6 | **Evaluate** | They invite a few HR/employees, run real or simulated Draws, see transparent outcomes + audit evidence | open engine, private host | engine built |
| 7 | **Convert** | Subscribe (hosted by us or a reseller) → `eval → active`; or self-host the open core; or auto-expire | private | to build (billing) |
| 8 | **Expand / retain** | More sites/users, modules (seats/desks), feedback loop | private | to build (PLAT006, modules) |

**What makes it "guided"** (vs an open sandbox): (1) human-qualified at triage; (2) provisioned
around their problem; (3) accompanied; (4) time-boxed with an explicit conversion step. Each
improves win-rate *and* avoids the cost/abuse of an always-on public instance.

**Levers we already have:** the **simulation clock** can advance virtual time to show a full
Draw cycle in minutes during a walkthrough; **readiness checks** give a setup checklist;
the **parking-challenge free-text** is both qualification context and a feature-signal board.

**Run it now, mostly manual; automate as volume grows:** pilot page (✅) → manual triage →
manual seed/provision → manual walkthrough → manual `eval→active`. Automate in slice order:
triage (PLAT008C) → provisioning automation (PLAT008B/C) → engagement signals (PLAT005) →
auto-expiry/reset (PLAT003) → billing → feedback (PLAT006).

### Evaluation data profiles

Keep the data paths separate. This split supports the platform epic (#633) and resolves the
showcase-vs-tailored planning point from #667. The default showcase should stay small enough to
explain in one screen; richer datasets belong either to a tailored prospect workspace or to
explicit validation tools.

| Path | Use it for | Boundary |
|---|---|---|
| **Default Green Logistics showcase** | Local development, guided customer walkthroughs, Release 1 smoke evidence, and source content for the static public tour. `./tools/dev-seed.sh` creates the small story from #704: named people, six parking slots, visible scarcity, fairness history, one reallocation, and a Seats example. | Synthetic only. It is a story-led evaluation baseline, not load data and not a permanent anonymous live demo. |
| **Tiny `demo` isolation fixture** | Tenant-isolation and SSO contrast checks. Enable it explicitly with `FPS_INCLUDE_DEMO_TENANT=1 ./tools/dev-setup-auth.sh`. | One `demo` tenant-admin and no business data by default. It must not reappear as the normal evaluator scaffold. |
| **Tailored prospect workspace** | Qualified guided pilots after triage. The tenant should be seeded/provisioned around the prospect's actual parking challenge, branding, location model, and identity path. | Dedicated tenant; use real or customer-shaped data only when the pilot scope and approvals allow it. Stable new product requirements start in strategy/product docs, then become GitHub implementation slices. |
| **Load/performance seed** | Capacity and performance validation. Use `tools/perf-seed-greenlogistics.sh` with explicit counts. | Synthetic bulk data. It must not pollute the default showcase or public tour narrative. |

The public product tour (#705) may use static screenshots, copy, and examples derived from the
Green Logistics showcase. It must not expose credentials, live tenant access, internal URLs, or
operator runbooks. Live tenant access remains guided until a future self-serve sandbox is
explicitly implemented.

## 3. Self-onboarding — two flavours, sequenced

Self-service fits the model, but split it:

- **A. Self-serve demo (ephemeral sandbox)** — instant, pre-seeded Green-Logistics-style
  workspace, fake users, isolated, abuse-capped, auto-expiring. Low-friction "see it work" for
  the non-technical buyer. ≈ demo + reset (PLAT003) with a self-service trigger.
- **B. Self-serve real pilot tenant** — automatic provisioning of *their* workspace with *their*
  identity, zero human touch. The real PLG move.

**Why not jump to B yet:**
- **Identity is the wall.** A usable tenant needs realm/OIDC/role-mapping; tenants are
  IdP-brokered and realm/seed automation is still *partial*. Zero-touch real onboarding waits on
  that automation.
- **B2B multi-user reality.** Someone on their side still configures identity, invites employees,
  sets policy — "self-onboarding" here is rarely truly zero-touch (unlike bottom-up dev tools).
- **Cost/abuse.** Automated tenant creation is a spam/resource magnet — needs verified email,
  caps, auto-expiry, and a platform hardened for unattended provisioning.

**Sequencing (all platform-side / private):** (1) guided pilot now → (2) self-serve demo (A)
next → (3) self-serve real tenant (B) later, as a *tier* alongside guided/sales, not a
replacement. Note the **open core is already self-onboarding for the technical audience**
(clone + run); self-onboarding is the feature we add for the non-technical, hosted buyer.

## 4. GTM positioning

For an HR/facility-bought workplace tool the sweet spot is **guided + sales-led as primary,
with a self-serve demo (A) for reach**, and self-serve real tenants (B) as a later growth tier.
Full bottom-up PLG is better suited to individual-developer tools. **Reseller/MSP overlay:** a
partner can own Land→Capture and Onboard→Convert for their network; we provide the platform and
provisioning (the #633 epic leaves room for a scoped partner console).

## 5. Measurement

Track drop-off across **requests → qualified → provisioned → active-eval → converted**, plus
**time-to-first-Draw** and **eval engagement** (active users / draws). An **active pilot is the
hottest lead signal.** These tell you whether the bottleneck is top-of-funnel (need more leads /
the tour) or activation (provisioning / walkthrough friction).

## 6. Dependencies & near-term needs

- Keep the **default showcase seed** small, narrative-driven, and resettable (#704). Do not use it
  as the bulk/load dataset or as a catch-all realistic company clone (#667).
- Use **tailored per-prospect workspaces** for guided pilots, once triage says the prospect is worth
  provisioning. This is where richer company-specific data belongs.
- Maintain the **static product tour / walkthrough asset** for the "explore" CTA (#705): cheap proof
  without public live-tenant exposure.
- Keep **Release 1** evidence honest (#388): synthetic/evaluation-grade where it is synthetic, and
  explicit about gaps before real customer data.
- Slices: PLAT008B/C (directory/triage/provision), PLAT003 (demo reset/expiry), PLAT005 (usage),
  PLAT006 (feedback); provisioning + identity automation; billing.

## 7. Security gate

A full external audit is **not** worth it yet (pre-real-data: Release 1 is synthetic only,
pre-revenue, and the architecture is mid repo-split). But the **new high-risk surfaces warrant a
targeted review now**: (1) **tenant isolation** / cross-tenant authorization (the existential
multi-tenant risk; #645), (2) the **public anonymous intake** (validation, rate-limit/abuse,
Turnstile-enforcement incl. the dev-skip not leaking to prod, PII handling), (3) the **platform
auth gate** (issuer fail-closed, no tenant→platform escalation). Schedule the **formal/external
audit as a gate before the first real-data pilot** — enterprise procurement (pen-test / SOC2-ish
evidence) is the natural forcing function, and the security-review-pack already points that way.
