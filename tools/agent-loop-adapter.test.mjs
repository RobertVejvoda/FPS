// Tests for the pure (non-gh) helpers of the delivery-loop adapter.
// Run: node --test tools/agent-loop-adapter.test.mjs

import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { classify, parseLedger, buildInput, resolveRoute, commentBody, classifySeverity, reviewedCommitFrom } from "./agent-loop-adapter.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const config = JSON.parse(readFileSync(join(here, "agent-loop-config.fairspot.json"), "utf8"));

// classify: loop vs manual + high-risk detection (repo config drives it, not the logic).
test("classify: Copilot bot -> loop implementer", () => {
  assert.equal(classify({ authorLogin: "Copilot", authorType: "Bot", files: ["README.md"] }, config).implementerKind, "loop");
});
test("classify: human -> manual implementer", () => {
  assert.equal(classify({ authorLogin: "RobertVejvoda", authorType: "User", files: ["README.md"] }, config).implementerKind, "manual");
});
test("classify: a .github/ change is high-risk; a docs change is not", () => {
  assert.equal(classify({ authorLogin: "Copilot", authorType: "Bot", files: [".github/workflows/x.yml"] }, config).isHighRisk, true);
  assert.equal(classify({ authorLogin: "Copilot", authorType: "Bot", files: ["docs/guide.md"] }, config).isHighRisk, false);
});
test("classify: a draw/fairness path is high-risk", () => {
  assert.equal(classify({ authorLogin: "Copilot", authorType: "Bot", files: ["code/server/Draw/Engine.cs"] }, config).isHighRisk, true);
});

// parseLedger: dedup set + round count from hidden markers.
test("parseLedger: counts unique repair rounds and records processed SHAs", () => {
  const bodies = [
    "@copilot fix it\n<!-- agent-loop v1 sha=abc1234 round=1 -->",
    "@copilot fix it\n<!-- agent-loop v1 sha=def5678 round=2 -->",
    "unrelated comment",
    "⏸️ drafted\n<!-- agent-loop v1 sha=aaa0000 round=- -->",   // a non-round action still records the SHA
  ];
  const { priorRounds, processedShas } = parseLedger(bodies);
  assert.equal(priorRounds, 2);                          // rounds only, not comments
  assert.ok(processedShas.has("abc1234"));
  assert.ok(processedShas.has("aaa0000"));               // deduped by SHA even without a round
  assert.equal(processedShas.size, 3);
});
test("parseLedger: the same SHA marked twice counts once in the set", () => {
  const { processedShas } = parseLedger(["<!-- agent-loop v1 sha=abc1234 round=1 -->", "<!-- agent-loop v1 sha=ABC1234 round=1 -->"]);
  assert.equal(processedShas.size, 1);                   // case-insensitive dedup
});
test("parseLedger: a dual-channel race (same SHA marked twice) counts as ONE round, not two", () => {
  const both = parseLedger([
    "@copilot fix\n<!-- agent-loop v1 sha=abc1234 round=1 -->",
    "@copilot fix\n<!-- agent-loop v1 sha=abc1234 round=1 -->",   // second channel, same reviewed SHA
  ]);
  assert.equal(both.priorRounds, 1);                     // rounds are DISTINCT SHAs, not markers
});

// buildInput: derives headMoved + alreadyProcessed from state.
test("buildInput: headMoved true when reviewed SHA != head", () => {
  const facts = { implementerKind: "loop", isHighRisk: false };
  const state = { ledger: { priorRounds: 0, processedShas: new Set() }, capped: false, isDraft: false };
  const i = buildInput({ event: "verdict", verdict: "blocking", reviewedSha: "aaa1111", headSha: "bbb2222" }, facts, state, config);
  assert.equal(i.headMoved, true);
  assert.equal(i.alreadyProcessed, false);
});
test("buildInput: alreadyProcessed true when the reviewed SHA is in the ledger", () => {
  const facts = { implementerKind: "loop", isHighRisk: false };
  const state = { ledger: { priorRounds: 1, processedShas: new Set(["aaa1111"]) }, capped: false, isDraft: false };
  const i = buildInput({ event: "verdict", verdict: "blocking", reviewedSha: "AAA1111", headSha: "aaa1111" }, facts, state, config);
  assert.equal(i.alreadyProcessed, true);
});

// resolveRoute: abstract roles -> concrete Project option IDs from config.
test("resolveRoute: needs-changes / implementer maps to the loop-implementer option IDs", () => {
  const r = resolveRoute({ status: "needs-changes", owner: "implementer", implementer: "implementer" }, config);
  assert.equal(r.status, config.project.status["needs-changes"]);
  assert.equal(r.owner, config.project.owners.loopImplementer);
  assert.equal(r.implementer, config.project.implementers.loopImplementer);
});
test("resolveRoute: blocked / human maps to Blocked + Robert", () => {
  const r = resolveRoute({ status: "blocked", owner: "human" }, config);
  assert.equal(r.status, config.project.status.blocked);
  assert.equal(r.owner, config.project.owners.human);
});

// commentBody: one comment per transition, carrying the ledger marker.
test("commentBody: nudge carries the round + a round marker", () => {
  const b = commentBody({ comment: "nudge-round-2", recordRound: true }, { round: 2, maxRounds: 3, reviewedSha: "abc1234" });
  assert.match(b, /round 2 of 3/);
  assert.match(b, /<!-- agent-loop v1 sha=abc1234 round=2 -->/);
});
test("commentBody: cap-terminal marks the round and reads as a human hold", () => {
  const b = commentBody({ comment: "cap-terminal", recordRound: true }, { round: 3, maxRounds: 3, reviewedSha: "abc1234" });
  assert.match(b, /held for a human/);
  assert.match(b, /round=3/);
});
test("commentBody: reready posts @codex review with a non-round marker", () => {
  const b = commentBody({ requestReview: true, recordRound: false, comment: null }, { round: 1, maxRounds: 3, reviewedSha: "" });
  assert.match(b, /@codex review/);
  assert.match(b, /round=-/);
});

// classifySeverity: clean vs blocking vs advisory, across both channels.
test("classifySeverity: a clean verdict is 'clean'", () => {
  assert.equal(classifySeverity("Codex Review: Didn't find any major issues. Breezy!", []), "clean");
});
test("classifySeverity: a P1 inline finding is 'blocking'", () => {
  assert.equal(classifySeverity("### Codex Review", ["**P1 Badge** something wrong"]), "blocking");
});
test("classifySeverity: findings without P0/P1/P2 are 'advisory'", () => {
  assert.equal(classifySeverity("### Codex Review", ["**P3 Badge** a nit"]), "advisory");
});

// reviewedCommitFrom: tolerant of Codex's markdown marker.
test("reviewedCommitFrom: extracts the SHA from a markdown 'Reviewed commit' marker", () => {
  assert.equal(reviewedCommitFrom("Breezy!\n**Reviewed commit:** `820c1ffed1`"), "820c1ffed1");
});
