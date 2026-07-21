// Scenario tests for the AUT-007 draft-PR routing gate.
// Run: node --test tools/delivery-draft-gate.test.mjs
//
// These reproduce the RobertVejvoda/fairspot#854/#855 regression: assigning a Ready issue to a
// coding agent opens a draft implementation PR, and that draft opened/synchronize event must
// never hand the linked issue to Codex review. Coverage is deliberately agent-identity-agnostic
// (Copilot, Claude, Codex) since the reducer never inspects PR author/login — only PR draft
// state, event action, and the linked issue's current Status.

import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { route } from "./delivery-draft-gate.mjs";

const GATE_PATH = fileURLToPath(new URL("./delivery-draft-gate.mjs", import.meta.url));

// Invokes the gate exactly as `.github/workflows/delivery-state-orchestrator.yml`
// (`pr-event-routing` job) does: as a subprocess reading PR_ACTION / PR_IS_DRAFT /
// ISSUE_CURRENT_STATUS from the environment and parsing its stdout as JSON. This proves the CLI
// contract the production workflow actually depends on, not just the exported `route()` function.
function runGateCli({ action, isDraft, currentStatus }) {
  const stdout = execFileSync(process.execPath, [GATE_PATH], {
    env: {
      ...process.env,
      PR_ACTION: action,
      PR_IS_DRAFT: String(isDraft),
      ISSUE_CURRENT_STATUS: currentStatus || "",
    },
    encoding: "utf8",
  });
  return JSON.parse(stdout);
}

// --- The #854/#855 regression scenario, for each coding-agent implementer ---
for (const implementer of ["Copilot", "Claude", "Codex"]) {
  test(`${implementer}: first draft PR on an Assigned issue moves it to In progress, not Codex review`, () => {
    const d = route({ action: "opened", isDraft: true, currentStatus: "Assigned" });
    assert.equal(d.action, "route-in-progress");
    assert.deepEqual(d.route, { status: "in-progress", owner: "implementer" });
  });

  test(`${implementer}: a further draft push (synchronize) does not move the issue to Codex review`, () => {
    const d = route({ action: "synchronize", isDraft: true, currentStatus: "In progress" });
    assert.equal(d.action, "preserve");
    assert.equal(d.route, null);
  });
}

// --- Draft opened/synchronize/reopened never route to Codex review, regardless of prior state ---
for (const action of ["opened", "synchronize", "reopened"]) {
  for (const currentStatus of ["Assigned", "In progress", "Needs changes", "Blocked", "Done", "In review", ""]) {
    test(`draft ${action} with Status="${currentStatus || "(unset)"}" never routes to In review/Codex`, () => {
      const d = route({ action, isDraft: true, currentStatus });
      assert.notEqual(d.action, "route-in-review");
      assert.ok(d.route === null || d.route.owner !== "codex");
    });
  }
}

// --- Draft pushes preserve Needs changes / Blocked / other manual/terminal holds ---
test("a draft synchronize preserves an existing Needs changes hold", () => {
  const d = route({ action: "synchronize", isDraft: true, currentStatus: "Needs changes" });
  assert.equal(d.action, "preserve");
  assert.equal(d.route, null);
});

test("a draft synchronize preserves an existing Blocked hold", () => {
  const d = route({ action: "synchronize", isDraft: true, currentStatus: "Blocked" });
  assert.equal(d.action, "preserve");
  assert.equal(d.route, null);
});

test("a draft opened event preserves Status when already In progress (no-op, not regressed)", () => {
  const d = route({ action: "opened", isDraft: true, currentStatus: "In progress" });
  assert.equal(d.action, "preserve");
  assert.equal(d.route, null);
});

// --- ready_for_review routes to In review/Codex exactly once, regardless of prior state ---
test("ready_for_review routes a linked issue to In review / Codex", () => {
  const d = route({ action: "ready_for_review", isDraft: false, currentStatus: "In progress" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex" });
});

test("ready_for_review still routes to In review / Codex even from Needs changes", () => {
  const d = route({ action: "ready_for_review", isDraft: false, currentStatus: "Needs changes" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex" });
});

// --- A non-draft PR opened directly (human PR, or already-ready agent PR) still routes normally ---
test("a non-draft opened/synchronize/reopened event still routes to In review / Codex", () => {
  for (const action of ["opened", "synchronize", "reopened"]) {
    const d = route({ action, isDraft: false, currentStatus: "Assigned" });
    assert.equal(d.action, "route-in-review");
    assert.deepEqual(d.route, { status: "in-review", owner: "codex" });
  }
});

// --- CLI conformance: the actual invocation contract the production workflow relies on ---
test("CLI: draft opened on Assigned -> route-in-progress JSON", () => {
  const d = runGateCli({ action: "opened", isDraft: true, currentStatus: "Assigned" });
  assert.equal(d.action, "route-in-progress");
  assert.deepEqual(d.route, { status: "in-progress", owner: "implementer" });
});

test("CLI: draft synchronize on Needs changes -> preserve JSON", () => {
  const d = runGateCli({ action: "synchronize", isDraft: true, currentStatus: "Needs changes" });
  assert.equal(d.action, "preserve");
  assert.equal(d.route, null);
});

test("CLI: ready_for_review -> route-in-review JSON regardless of prior status", () => {
  const d = runGateCli({ action: "ready_for_review", isDraft: false, currentStatus: "Blocked" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex" });
});

test("CLI: non-draft opened -> route-in-review JSON", () => {
  const d = runGateCli({ action: "opened", isDraft: false, currentStatus: "Assigned" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex" });
});
