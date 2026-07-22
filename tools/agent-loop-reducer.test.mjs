// Scenario tests for the agent delivery-loop transition reducer.
// Run: node --test tools/agent-loop-reducer.test.mjs
//
// These exercise the pure decision logic for every acceptance scenario that is a *reducer*
// decision. Scenarios that are purely GitHub-adapter mechanics (the actual dedup lookup, the
// label-mutation retry/fallback execution, resolving 0/many linked issues) are noted where they
// are the adapter's responsibility and are asserted at the contract level (i.e. the reducer
// hands the adapter what it needs).

import { test } from "node:test";
import assert from "node:assert/strict";
import { transition } from "./agent-loop-reducer.mjs";

const LOOP = "loop";     // e.g. Copilot — autonomous fix loop
const MANUAL = "manual"; // human / Claude — draft + hand-back
const verdict = (o) => transition({ event: "verdict", maxRounds: 3, priorRounds: 0, capped: false, isHighRisk: false, implementerKind: LOOP, ...o });
const push = (o) => transition({ event: "push", maxRounds: 3, priorRounds: 0, capped: false, isHighRisk: false, implementerKind: LOOP, ...o });

// 1 & 2. Formal review only / conversation finding only — both are just "a blocking verdict" to
// the reducer; the CHANNEL is the adapter's concern. A blocking finding on a loop PR nudges once.
test("blocking verdict (either channel) on a loop PR nudges the implementer, round 1", () => {
  const d = verdict({ verdict: "blocking" });
  assert.equal(d.action, "nudge-loop");
  assert.equal(d.nudgeImplementer, true);
  assert.equal(d.recordRound, true);
  assert.equal(d.setDraft, null);                 // loop PRs stay Ready
  assert.equal(d.applyStage, "addressing-feedback");
  assert.deepEqual(d.route, { status: "needs-changes", owner: "implementer", implementer: "implementer" });
});

// 3 & 4. Both channels for the same reviewed SHA / duplicate-retried events -> processed once.
test("a duplicate verdict for the same (PR, reviewed SHA) is ignored", () => {
  const d = verdict({ verdict: "blocking", alreadyProcessed: true });
  assert.equal(d.action, "ignore-duplicate");
  assert.equal(d.recordRound, false);
  assert.equal(d.nudgeImplementer, false);
});

// 5 & 11. Stale finding after a fix push / transient head race -> ignore.
test("a stale verdict (head moved since review) is ignored, no round counted", () => {
  const d = verdict({ verdict: "blocking", headMoved: true });
  assert.equal(d.action, "ignore-stale");
  assert.equal(d.recordRound, false);
  assert.equal(d.setDraft, null);
});

// 6. Rounds 1, 2, 3 and terminal cap (maxRounds = 3).
test("rounds progress: prior 0 -> round 1 nudge, prior 1 -> round 2 nudge", () => {
  assert.equal(verdict({ verdict: "blocking", priorRounds: 0 }).action, "nudge-loop");
  assert.equal(verdict({ verdict: "blocking", priorRounds: 1 }).action, "nudge-loop");
});
test("reaching the cap (prior 2 -> round 3 of 3) fails closed: draft + marker + Blocked/human", () => {
  const d = verdict({ verdict: "blocking", priorRounds: 2 });
  assert.equal(d.action, "cap-terminal");
  assert.equal(d.setDraft, true);                 // durable hold (fail closed)
  assert.equal(d.addCapMarker, true);             // enforcement marker (adapter verifies + draft fallback)
  assert.equal(d.applyStage, "needs-human");      // observability
  assert.deepEqual(d.route, { status: "blocked", owner: "human" });
  assert.equal(d.recordRound, true);
});

// 7. Push while addressing feedback -> resume review.
test("a fix push (not capped) resumes review of the new head", () => {
  const d = push({});
  assert.equal(d.action, "reready-review");
  assert.equal(d.requestReview, true);
  assert.equal(d.applyStage, "in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "reviewer" });
});

// 8. Push after terminal cap -> must NOT resume.
test("a push after the terminal cap does not resume the loop", () => {
  const d = push({ capped: true });
  assert.equal(d.action, "ignore-capped-push");
  assert.equal(d.requestReview, false);
});
test("a verdict on a capped PR is also ignored", () => {
  assert.equal(verdict({ verdict: "blocking", capped: true }).action, "ignore-capped");
});

// 9. Clean low-risk verdict -> auto-merge eligible.
test("clean + low-risk -> auto-merge eligible, stage auto-merging", () => {
  const d = verdict({ verdict: "clean", isHighRisk: false });
  assert.equal(d.action, "clean-verdict");
  assert.equal(d.mergeEligible, true);
  assert.equal(d.applyStage, "auto-merging");
  assert.deepEqual(d.route, { status: "in-review", owner: "reviewer" });
});

// 10. Clean high-risk verdict -> re-reviewable but human merge (not auto).
test("clean + high-risk -> NOT auto-merge eligible, stays re-reviewable", () => {
  const d = verdict({ verdict: "clean", isHighRisk: true });
  assert.equal(d.action, "clean-verdict");
  assert.equal(d.mergeEligible, false);
  assert.equal(d.applyStage, "in-review");
});

// 6b. Advisory (P3 / unlabelled) leaves the PR alone.
test("advisory-only verdict is ignored and counts no round", () => {
  const d = verdict({ verdict: "advisory" });
  assert.equal(d.action, "ignore-advisory");
  assert.equal(d.recordRound, false);
});

// 14. Human/Claude-authored PRs stay on the draft/human path (never nudged/capped).
test("blocking finding on a manual (human/Claude) PR -> draft + hand back, no round counted", () => {
  const d = verdict({ verdict: "blocking", implementerKind: MANUAL });
  assert.equal(d.action, "draft-and-handback");
  assert.equal(d.setDraft, true);
  assert.equal(d.nudgeImplementer, false);
  assert.equal(d.recordRound, false);             // manual PRs are not round-counted
  assert.deepEqual(d.route, { status: "needs-changes", owner: "implementer" });
});
test("a manual PR never reaches the cap regardless of prior rounds", () => {
  const d = verdict({ verdict: "blocking", implementerKind: MANUAL, priorRounds: 5 });
  assert.equal(d.action, "draft-and-handback");   // not cap-terminal
});

// High-risk high-risk loop PR with a blocking finding still nudges (risk only affects the CLEAN path).
test("blocking finding on a high-risk loop PR still nudges (risk gates merge, not repair)", () => {
  const d = verdict({ verdict: "blocking", isHighRisk: true });
  assert.equal(d.action, "nudge-loop");
});

// Determinism / idempotency: identical input -> identical decision.
test("the reducer is deterministic (same input -> same decision)", () => {
  const inp = { event: "verdict", verdict: "blocking", maxRounds: 3, priorRounds: 1, capped: false, isHighRisk: false, implementerKind: LOOP };
  assert.deepEqual(transition({ ...inp }), transition({ ...inp }));
});

// At most one human-facing comment key per transition (message-bus discipline).
test("every decision carries at most one comment key", () => {
  const inputs = [
    { event: "verdict", verdict: "blocking", priorRounds: 0 },
    { event: "verdict", verdict: "blocking", priorRounds: 2 },
    { event: "verdict", verdict: "blocking", implementerKind: MANUAL },
    { event: "verdict", verdict: "clean" },
    { event: "push" },
  ];
  for (const o of inputs) {
    const d = transition({ maxRounds: 3, priorRounds: 0, capped: false, isHighRisk: false, implementerKind: LOOP, ...o });
    assert.ok(d.comment === null || typeof d.comment === "string");
  }
});
