# HR &amp; Operator Tour

**Who this is for:** the HR, facilities, or resource operator who runs the day-to-day — managing eligibility, timing the Draw, handling support cases, and explaining outcomes.

**What matters to you:** setting the rules that make allocation fair and explainable, and having the evidence to back up every outcome.

## What you manage

1. **Eligibility facts.** Profile facts drive who is eligible for what — an HR-assigned company car, an EV charger need, an accessibility need. These are managed facts, not self-service claims, so priority is always explainable. Bring them in via the [HR Import Contract](../hr-import).
2. **Company-car and fixed-space rules.** Obligations get **Tier-1 fixed-slot precedence** — the assigned slot is allocated before the fairness Draw. An employee cannot self-assign company-car status; it is an HR/facilities-controlled obligation, which keeps the fairness story honest.
3. **Draw timing.** The Draw allocates scarce capacity on a schedule, after a cutoff, using documented rules. Everyone outside the fixed obligations is selected by weighted fairness using recent allocation history and active penalties (for example a late-cancellation penalty). The timing and workflow are in [Draw Scheduling and Workflow](../production/draw-scheduling-and-workflow).
4. **Manual and support actions.** Where policy allows, operators can correct or intervene — and every such action is recorded as evidence.
5. **Evidence.** Reporting gives tenant-scoped operational summaries and fairness read models, and Audit preserves the trail behind decisions — so you can explain "why this outcome" to a user or a manager.

> 📷 **Screenshot gap:** web _HR/operator_ reporting and configuration surfaces, and a completed _Draw result_ — real screens not yet captured.

## The fairness model, in operator terms

- **Two tiers.** Fixed obligations (company-car, reserved) first; then a weighted fairness Draw for the rest.
- **Explainable outcomes.** Users see safe statuses and employee-visible reasons; you see the operational reporting and audit evidence behind them.
- **Self-correcting.** Cancelling a booking promotes the next fairly-ranked waitlisted person, so freed capacity still flows by the rules.

## Try it in the demo

**Green Logistics** seeds a realistic operator picture: `gl-hr-admin` (Lucie Prochazkova, `Dev1234!`) sees reports, configuration, and HR import; `gl-employee1` holds the company-car slot `VIP-01` (Tier-1); `gl-employee7` carries seeded recent-winner history and `gl-employee8` a late-cancellation penalty, so the Draw's fairness weighting is visible. The seed runs a Draw over ten `GL-HQ` requests where demand exceeds capacity, then cancels one allocation and shows the next fair employee promoted. Full detail: [Demo Seed Data](../demo-seed-data) and the [Green Logistics Walkthrough](./green-logistics-walkthrough).

## Go deeper

- [Business Policies](../architecture/business/policies) and [Business Processes](../architecture/business/business-processes).
- [Draw Scheduling and Workflow](../production/draw-scheduling-and-workflow).
- [HR Import Contract](../hr-import).
