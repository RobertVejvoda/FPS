# RACI

This RACI defines architecture governance responsibilities. It does not replace delivery-board ownership for individual implementation issues.

| Architecture Artifact / Activity | Sponsor | Architecture Owner | Business Owner | Security Lead | Delivery Lead | Implementer |
| --- | --- | --- | --- | --- | --- | --- |
| Architecture Vision | A | R | C | C | C | I |
| Stakeholders and Concerns | C | R | A | C | C | I |
| Architecture Principles | A | R | C | C | C | I |
| Business Architecture | I | C | A/R | C | C | I |
| Information Systems Architecture | I | A/R | C | C | C | C |
| Technology Architecture | I | A/R | I | C | C | C |
| Security Architecture | I | C | C | A/R | C | I |
| Architecture States and Gap Analysis | A | R | C | C | C | C |
| Roadmap / Transition Architectures | A | R | C | C | C | I |
| Work Packages | I | C | C | C | A/R | R |
| Architecture Review | I | A/R | C | C | C | I |
| Waiver Approval | A | R | C | C | C | I |
| Change Control | C | A/R | C | C | C | I |

Legend: `R` responsible, `A` accountable, `C` consulted, `I` informed.

Every row should have one accountable owner. If multiple actors are accountable, the activity should be split or escalated.

## Role Mapping

| RACI Role | FairSpot Mapping | Notes |
| --- | --- | --- |
| Sponsor | Robert | Accepts product/architecture direction and external-facing risk. |
| Architecture Owner | Robert + Codex | Robert is accountable; Codex prepares and maintains reviewable architecture content. |
| Business Owner | Robert | Owns business value, scope, and customer-facing fit. |
| Security Lead | Codex review; Robert risk acceptance | Security/privacy findings can require Robert acceptance when risk remains. |
| Delivery Lead | Codex | Maintains issue readiness, routing, PR review, validation, and merge decisions. |
| Implementer | Claude, Copilot, Human, or Codex when explicitly implementing | Provides implementation evidence and validation, but does not self-approve. |

## Robert TODOs

- Robert TODO: confirm whether Security Lead should remain Codex review plus Robert acceptance or name a separate human reviewer before customer pilot.
- Robert TODO: confirm whether customer pilot artifacts require a customer-side consulted role in the RACI.
