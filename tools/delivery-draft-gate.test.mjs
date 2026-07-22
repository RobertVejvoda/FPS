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
import { route, mapImplementerToOwnerOption } from "./delivery-draft-gate.mjs";

const GATE_PATH = fileURLToPath(new URL("./delivery-draft-gate.mjs", import.meta.url));

// Invokes the gate exactly as `.github/workflows/delivery-state-orchestrator.yml`
// (`pr-event-routing` job) does: as a subprocess reading PR_ACTION / PR_IS_DRAFT /
// ISSUE_CURRENT_STATUS / ISSUE_CURRENT_IMPLEMENTER from the environment and parsing its stdout as
// JSON. This proves the CLI contract the production workflow actually depends on, not just the
// exported `route()` function.
function runGateCli({ action, isDraft, currentStatus, currentImplementer }) {
  const stdout = execFileSync(process.execPath, [GATE_PATH], {
    env: {
      ...process.env,
      PR_ACTION: action,
      PR_IS_DRAFT: String(isDraft),
      ISSUE_CURRENT_STATUS: currentStatus || "",
      ISSUE_CURRENT_IMPLEMENTER: currentImplementer || "",
    },
    encoding: "utf8",
  });
  return JSON.parse(stdout);
}

// --- The #854/#855 regression scenario, for each coding-agent implementer ---
for (const implementer of ["Copilot", "Claude", "Codex"]) {
  test(`${implementer}: first draft PR on an Assigned issue moves it to In progress, not Codex review`, () => {
    const d = route({ action: "opened", isDraft: true, currentStatus: "Assigned", currentImplementer: implementer });
    assert.equal(d.action, "route-in-progress");
    assert.deepEqual(d.route, {
      status: "in-progress",
      owner: "implementer",
      ownerOption: mapImplementerToOwnerOption(implementer),
    });
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
  assert.deepEqual(d.route, { status: "in-review", owner: "codex", ownerOption: "codex" });
});

test("ready_for_review still routes to In review / Codex even from Needs changes", () => {
  const d = route({ action: "ready_for_review", isDraft: false, currentStatus: "Needs changes" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex", ownerOption: "codex" });
});

// --- A non-draft PR opened directly (human PR, or already-ready agent PR) still routes normally ---
test("a non-draft opened/synchronize/reopened event still routes to In review / Codex", () => {
  for (const action of ["opened", "synchronize", "reopened"]) {
    const d = route({ action, isDraft: false, currentStatus: "Assigned" });
    assert.equal(d.action, "route-in-review");
    assert.deepEqual(d.route, { status: "in-review", owner: "codex", ownerOption: "codex" });
  }
});

// --- Codex-implemented PRs are NEVER routed back to Codex for review (AGENTS.md) ---
// Codex may act as implementer when an issue is assigned to it, but a PR it authored must be
// reviewed by Claude or a human — never by Codex itself. The gate must hand such a PR's review
// to Human (Robert) instead of the default Codex reviewer.
for (const action of ["ready_for_review", "opened", "synchronize", "reopened"]) {
  test(`Codex-implemented PR (${action}, non-draft) routes review to Human, not Codex`, () => {
    const d = route({ action, isDraft: false, currentStatus: "In progress", currentImplementer: "Codex" });
    assert.equal(d.action, "route-in-review");
    assert.equal(d.route.status, "in-review");
    assert.equal(d.route.owner, "human");
    assert.equal(d.route.ownerOption, "robert");
  });
}

test("Codex-implemented ready_for_review from Needs changes still routes to Human, not Codex", () => {
  const d = route({ action: "ready_for_review", isDraft: false, currentStatus: "Needs changes", currentImplementer: "Codex" });
  assert.equal(d.action, "route-in-review");
  assert.equal(d.route.ownerOption, "robert");
});

for (const implementer of ["Claude", "Copilot", "Human", "", undefined, "SomeUnknownValue"]) {
  test(`non-Codex-implemented (Implementer=${JSON.stringify(implementer)}) ready_for_review routes review to Codex`, () => {
    const d = route({ action: "ready_for_review", isDraft: false, currentStatus: "In progress", currentImplementer: implementer });
    assert.equal(d.action, "route-in-review");
    assert.equal(d.route.owner, "codex");
    assert.equal(d.route.ownerOption, "codex");
  });
}

// --- CLI conformance: the actual invocation contract the production workflow relies on ---
test("CLI: draft opened on Assigned with recognized Implementer -> route-in-progress JSON with mapped ownerOption", () => {
  const d = runGateCli({ action: "opened", isDraft: true, currentStatus: "Assigned", currentImplementer: "Copilot" });
  assert.equal(d.action, "route-in-progress");
  assert.deepEqual(d.route, { status: "in-progress", owner: "implementer", ownerOption: "copilot" });
});

test("CLI: draft opened on Assigned with empty/unrecognized Implementer -> ownerOption null (preserve existing Owner)", () => {
  const d = runGateCli({ action: "opened", isDraft: true, currentStatus: "Assigned", currentImplementer: "" });
  assert.equal(d.action, "route-in-progress");
  assert.deepEqual(d.route, { status: "in-progress", owner: "implementer", ownerOption: null });
});

test("CLI: draft synchronize on Needs changes -> preserve JSON", () => {
  const d = runGateCli({ action: "synchronize", isDraft: true, currentStatus: "Needs changes" });
  assert.equal(d.action, "preserve");
  assert.equal(d.route, null);
});

test("CLI: ready_for_review -> route-in-review JSON regardless of prior status", () => {
  const d = runGateCli({ action: "ready_for_review", isDraft: false, currentStatus: "Blocked" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex", ownerOption: "codex" });
});

test("CLI: non-draft opened -> route-in-review JSON", () => {
  const d = runGateCli({ action: "opened", isDraft: false, currentStatus: "Assigned" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "codex", ownerOption: "codex" });
});

test("CLI: Codex-implemented ready_for_review -> route-in-review JSON with ownerOption robert (Human)", () => {
  const d = runGateCli({ action: "ready_for_review", isDraft: false, currentStatus: "In progress", currentImplementer: "Codex" });
  assert.equal(d.action, "route-in-review");
  assert.deepEqual(d.route, { status: "in-review", owner: "human", ownerOption: "robert" });
});

test("CLI: Codex-implemented non-draft opened -> route-in-review JSON routed to Human, not Codex", () => {
  const d = runGateCli({ action: "opened", isDraft: false, currentStatus: "Assigned", currentImplementer: "Codex" });
  assert.equal(d.action, "route-in-review");
  assert.equal(d.route.ownerOption, "robert");
});

// --- Owner-preservation fallback: an empty/unrecognized Implementer must never become Owner=None ---
test("mapImplementerToOwnerOption maps each recognized Implementer to its Owner option role", () => {
  assert.equal(mapImplementerToOwnerOption("Claude"), "claude");
  assert.equal(mapImplementerToOwnerOption("Copilot"), "copilot");
  assert.equal(mapImplementerToOwnerOption("Codex"), "codex");
  assert.equal(mapImplementerToOwnerOption("Human"), "robert");
});

for (const currentImplementer of [undefined, "", "Robert", "SomeUnknownValue"]) {
  test(`mapImplementerToOwnerOption(${JSON.stringify(currentImplementer)}) returns null so Owner is preserved, not set to None`, () => {
    assert.equal(mapImplementerToOwnerOption(currentImplementer), null);
  });

  test(`route(): draft on Assigned with Implementer=${JSON.stringify(currentImplementer)} yields ownerOption null (preserve Owner)`, () => {
    const d = route({ action: "opened", isDraft: true, currentStatus: "Assigned", currentImplementer });
    assert.equal(d.action, "route-in-progress");
    assert.equal(d.route.ownerOption, null);
  });
}

// --- Workflow conformance: `/fps-route assign Codex` records Owner=Codex AND Implementer=Codex ---
// The workflow's set_implementer/assign branches are defined in the orchestrator YAML shell; these
// tests read the YAML directly and assert the durable predicates so a regression in the shell
// dispatch (e.g. Codex silently dropped from the assign case, or OPT_IMPL_CODEX removed) fails the
// suite, not just the reducer contract.
import { readFileSync } from "node:fs";

const ORCH_YAML_PATH = fileURLToPath(new URL("../.github/workflows/delivery-state-orchestrator.yml", import.meta.url));
const ORCH_YAML = readFileSync(ORCH_YAML_PATH, "utf8");

test("workflow catalogues OPT_IMPL_CODEX with the known Project option id 533f5ac2", () => {
  assert.match(ORCH_YAML, /OPT_IMPL_CODEX:\s*533f5ac2\b/);
});

test("workflow set_implementer supports Codex and maps it to OPT_IMPL_CODEX", () => {
  assert.match(ORCH_YAML, /Codex\)\s*IMPL_OPTION="\$OPT_IMPL_CODEX";\s*IMPL_DISPLAY="Codex"\s*;;/);
});

test("workflow set_implementer error notice advertises Codex as a supported implementer", () => {
  assert.match(ORCH_YAML, /Unsupported route implementer[^\n]*supported:[^\n]*Codex/);
});

test("workflow /fps-route assign case dispatches Codex through set_implementer (writes Implementer=Codex)", () => {
  // Locate the `assign)` case body (from `assign)` up to the next top-level `blocked)` sibling)
  // and assert Codex is dispatched to set_implementer within it.
  const assignBlock = ORCH_YAML.match(/\bassign\)[\s\S]*?\n\s{12}blocked\)/);
  assert.ok(assignBlock, "assign case not found in orchestrator workflow");
  assert.match(
    assignBlock[0],
    /Codex\)\s*set_implementer\s+"\$ARG_OWNER"\s*;;/,
    "assign case must dispatch Codex through set_implementer so Implementer=Codex is recorded",
  );
});

test("workflow /fps-route assign usage notice lists Codex as an allowed owner", () => {
  assert.match(ORCH_YAML, /Usage:\s*\/fps-route assign[^\n]*Codex/);
});
