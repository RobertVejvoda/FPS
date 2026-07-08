# Tour Decks

Lightweight, presentation-ready **slide outlines** derived from the [Guided Tours](../) and their source docs. These are the "deck" views for live evaluation sessions — the docs remain the source of truth; a slide is just a presentation of a doc.

Each deck is a Markdown page, one slide per section, and every slide carries a **Source:** link back to the doc it presents so the deck can be kept current. No binary decks are committed; if a `.pptx` is ever produced, keep this text source beside it.

| Deck | For | Built from |
| --- | --- | --- |
| [Sponsor &amp; Evaluator Deck](./sponsor-evaluator-deck) | A pilot go/no-go conversation | [Sponsor &amp; Evaluator Tour](../sponsor-evaluator), [Client Evaluation Pack](../../client-evaluation-pack) |
| [Tenant Admin Deck](./tenant-admin-deck) | A customer-IT setup walkthrough | [Tenant Admin Tour](../tenant-admin), [Tenant Onboarding](../../business-layer/tenant-onboarding) |
| [Resource User Deck](./resource-user-deck) | An end-user "how it works" intro | [Resource User Tour](../resource-user), [Green Logistics Walkthrough](../green-logistics-walkthrough) |

**How to present:** read top to bottom — each `---` separates a slide. To generate actual slides, run any Markdown slide tool (e.g. Marp) over the page; the outline is written to survive that without edits. Keep the **Source:** lines when you adapt a deck so the trace back to docs stays intact.
