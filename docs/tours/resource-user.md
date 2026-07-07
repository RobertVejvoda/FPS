# Resource User Tour

**Who this is for:** the person who actually needs a space — an employee requesting parking today, or a participant asking for any scarce shared resource. This tour follows the everyday loop: sign in, ask, see your bookings, get notified, and understand the outcome.

**What matters to you:** getting a fair chance at a space, always knowing your request's status and your next action, and never having to understand hidden allocation internals or see anyone else's data.

## The journey

1. **Sign in.** You authenticate through your organisation's identity (SSO) or a FairSpot account — you never type in a tenant or user id. FairSpot resolves your tenant, identity, and roles from your sign-in. The two sign-in paths are explained in [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes).
2. **Request capacity.** Ask for a space for a date and location. Your request appears immediately with a safe status and, where relevant, an employee-visible reason — for example if the date is outside the booking window.
3. **The Draw runs.** When demand is higher than supply, FairSpot allocates the scarce spaces by documented fairness rules. Obligations such as an HR-assigned company-car slot are honoured first; everyone else is selected by weighted fairness that considers recent allocation history — so if you missed out last time, your chances improve. You see your result, not the lottery internals.
4. **Get notified.** A notification records the outcome (allocated, waitlisted, or a safe reason). Your notification history and unread state reflect the booking event.
5. **See My Bookings.** Your bookings list shows what you've requested and its current state. You can **cancel** a booking you no longer need — and when you do, the next fairly-ranked waitlisted person is promoted, so a freed space still goes to someone by the rules.
6. **Confirm usage.** Where the flow asks for it, confirm you used the space, which keeps the fairness signals and no-show handling honest.

> 📷 **Screenshot gap:** mobile _Request capacity_ form and _My Bookings_ list — real screens not yet captured. Source flow: Expo mobile app → new request → My Bookings.

> 📷 **Screenshot gap:** in-app _Notifications_ with allocated/waitlisted states — not yet captured.

## What you will never see

FairSpot keeps user views safe by design: your own bookings, your own notifications, and understandable reasons — **not** other users, and not the hidden allocation internals. This is a product guarantee, not a UI accident (see the [Security &amp; GDPR summary](./client-evaluation-pack#security-and-gdpr-summary)).

## Try it in the demo

In the **Green Logistics** showcase, sign in as a general employee (for example `gl-employee5`, password `Dev1234!`) and submit a `GL-HQ` parking request, then watch the Draw place you as allocated or waitlisted. `gl-employee1` holds a company-car slot (`VIP-01`) and is allocated before the fair Draw — showing how obligations differ from lottery preference. Full steps: [Green Logistics Walkthrough](./tours/green-logistics-walkthrough).

## Go deeper

- [Demo and Evaluation](./demo-and-evaluation) — demo roles and the booking story.
- [Tenant Discovery and Login Modes](./business-layer/tenant-login-modes) — how sign-in resolves your tenant.
- [Product Overview](./Home) — the fairness model in business terms.
