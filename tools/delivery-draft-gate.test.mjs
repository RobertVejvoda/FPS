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

  test(`route(): draft on Ready with Implementer=${JSON.stringify(currentImplementer)} yields ownerOption null (preserve Owner)`, () => {
    const d = route({ action: "opened", isDraft: true, currentStatus: "Ready", currentImplementer });
    assert.equal(d.action, "route-in-progress");
    assert.equal(d.route.ownerOption, null);
  });
}

// --- Ready is treated the same as Assigned for draft opened/synchronize/reopened ---
// A slice can sit at Ready when the assignment-handoff comment has not yet reconciled it to
// Assigned; a draft PR on such a slice is direct evidence implementation has begun, so it must
// advance to In progress under the recorded Implementer instead of being left at Ready.
for (const action of ["opened", "synchronize", "reopened"]) {
  for (const implementer of ["Copilot", "Claude"]) {
    test(`${implementer}: draft ${action} on a Ready issue moves it to In progress under the recorded Implementer`, () => {
      const d = route({ action, isDraft: true, currentStatus: "Ready", currentImplementer: implementer });
      assert.equal(d.action, "route-in-progress");
      assert.deepEqual(d.route, {
        status: "in-progress",
        owner: "implementer",
        ownerOption: mapImplementerToOwnerOption(implementer),
      });
    });
  }
}

test("CLI: draft opened on Ready with recognized Implementer -> route-in-progress JSON with mapped ownerOption", () => {
  const d = runGateCli({ action: "opened", isDraft: true, currentStatus: "Ready", currentImplementer: "Claude" });
  assert.equal(d.action, "route-in-progress");
  assert.deepEqual(d.route, { status: "in-progress", owner: "implementer", ownerOption: "claude" });
});

test("CLI: draft synchronize on Ready with recognized Implementer -> route-in-progress JSON with mapped ownerOption", () => {
  const d = runGateCli({ action: "synchronize", isDraft: true, currentStatus: "Ready", currentImplementer: "Copilot" });
  assert.equal(d.action, "route-in-progress");
  assert.deepEqual(d.route, { status: "in-progress", owner: "implementer", ownerOption: "copilot" });
});

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

// --- Needs-changes handback: Implementer=Codex must return ownership to Codex, never fall through to None ---
// These read the orchestrator YAML directly and assert the durable predicates for the three
// Needs-changes paths so a regression that drops Codex from any of them fails the suite.
test("workflow CHANGES_REQUESTED handler maps Implementer=Codex to OPT_OWNER_CODEX (no Owner=None fallthrough)", () => {
  // First IMPLEMENTER_NAME case block belongs to the pull_request_review CHANGES_REQUESTED handler.
  const idx = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in');
  assert.ok(idx > 0, "CHANGES_REQUESTED IMPLEMENTER_NAME case block not found");
  const block = ORCH_YAML.slice(idx, ORCH_YAML.indexOf("esac", idx));
  assert.match(block, /Codex\)\s*OWNER_OPTION="\$OPT_OWNER_CODEX"\s*;;/);
  assert.match(block, /Claude\)\s*OWNER_OPTION="\$OPT_OWNER_CLAUDE"\s*;;/);
  assert.match(block, /Copilot\)\s*OWNER_OPTION="\$OPT_OWNER_COPILOT"\s*;;/);
});

test("workflow converted_to_draft handler maps Implementer=Codex to OPT_OWNER_CODEX (no Owner=None fallthrough)", () => {
  // Second IMPLEMENTER_NAME case block belongs to the pr-converted-to-draft handler.
  const first = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in');
  const second = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in', first + 1);
  assert.ok(second > first, "converted_to_draft IMPLEMENTER_NAME case block not found");
  const block = ORCH_YAML.slice(second, ORCH_YAML.indexOf("esac", second));
  assert.match(block, /Codex\)\s*OWNER_OPTION="\$OPT_OWNER_CODEX"\s*;;/);
});

test("workflow /fps-state needs-changes Implementer fallback maps Codex to OPT_OWNER_CODEX with display Codex", () => {
  // Third IMPLEMENTER_NAME case block belongs to the /fps-state needs-changes owner-fallback.
  const first = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in');
  const second = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in', first + 1);
  const third = ORCH_YAML.indexOf('case "$IMPLEMENTER_NAME" in', second + 1);
  assert.ok(third > second, "/fps-state needs-changes IMPLEMENTER_NAME case block not found");
  const block = ORCH_YAML.slice(third, ORCH_YAML.indexOf("esac", third));
  assert.match(block, /Codex\)\s*OWNER_OPTION="\$OPT_OWNER_CODEX";\s*OWNER_DISPLAY="Codex"\s*;;/);
});

test("workflow: every Needs-changes IMPLEMENTER_NAME case block handles Codex before the None fallthrough", () => {
  // Defensive: make sure no future edit reintroduces a Codex-less block. All three blocks must
  // contain a `Codex)` arm, and it must appear before the wildcard `*)` fallthrough.
  const re = /case "\$IMPLEMENTER_NAME" in([\s\S]*?)esac/g;
  const blocks = [...ORCH_YAML.matchAll(re)].map((m) => m[1]);
  assert.equal(blocks.length, 3, `expected 3 IMPLEMENTER_NAME case blocks, found ${blocks.length}`);
  for (const [i, body] of blocks.entries()) {
    const codexIdx = body.search(/\bCodex\)/);
    const wildcardIdx = body.search(/^\s*\*\)/m);
    assert.ok(codexIdx >= 0, `block #${i + 1} missing Codex) arm`);
    assert.ok(wildcardIdx > codexIdx, `block #${i + 1} Codex) arm must precede the *) fallthrough`);
    assert.match(body, /Codex\)[^\n]*OPT_OWNER_CODEX/);
  }
});

// --- Manual In-review routing family: reviewer-independence when Implementer=Codex ---
// These read the orchestrator YAML directly and assert the workflow's per-issue reviewer
// selection for the two manual `In review` entry points (`/fps-route codex-review` and
// `/fps-state in-review`). Both must default to Owner=Codex, override to Owner=Robert per
// linked issue when that issue's Implementer=Codex, preserve an explicit non-Codex owner,
// and resolve per linked issue so a multi-issue PR cannot leak the first issue's result.

test("workflow /fps-route codex-review case defers per-issue Owner resolution (does not hardcode Owner=Codex upfront)", () => {
  // The codex-review case body must NOT call `set_owner Codex` directly, and MUST set the
  // per-issue routing flag that triggers the loop-body Implementer lookup.
  const caseBlock = ORCH_YAML.match(/codex-review\)\s*\n[\s\S]*?;;/);
  assert.ok(caseBlock, "codex-review) case not found");
  assert.doesNotMatch(caseBlock[0], /\bset_owner\s+Codex\b/, "codex-review must not hardcode Owner=Codex outside the per-issue loop");
  assert.match(caseBlock[0], /CODEX_REVIEW_ROUTE="true"/);
  assert.match(caseBlock[0], /STATUS_OPT="\$OPT_STATUS_IN_REVIEW"/);
});

test("workflow /fps-route codex-review resolves Owner per linked issue with the Implementer=Codex → Robert exception", () => {
  // The route-comment-command target loop must, when CODEX_REVIEW_ROUTE=true, (a) reset Owner
  // to Codex per iteration (so no leakage from a prior issue), (b) look up Implementer for the
  // current ITEM_ID, and (c) override to Robert when Implementer=Codex.
  const idx = ORCH_YAML.indexOf('if [ "$CODEX_REVIEW_ROUTE" = "true" ]; then');
  assert.ok(idx > 0, "codex-review per-issue Owner resolution block not found in target loop");
  const block = ORCH_YAML.slice(idx, idx + 2000);
  // Per-iteration reset to Codex default
  assert.match(block, /OWNER_OPTION="\$OPT_OWNER_CODEX"/);
  assert.match(block, /OWNER_DISPLAY="Codex"/);
  // Per-issue Implementer lookup keyed to the current ITEM_ID (no leakage from a prior loop)
  assert.match(block, /-f id="\$ITEM_ID"/);
  assert.match(block, /Implementer/);
  // Reviewer-independence override
  assert.match(block, /"\$CR_IMPLEMENTER_NAME" = "Codex"/);
  assert.match(block, /OWNER_OPTION="\$OPT_OWNER_ROBERT"/);
  assert.match(block, /OWNER_DISPLAY="Robert"/);
});

test("workflow /fps-state in-review applies the Implementer=Codex → Robert override per linked issue", () => {
  // The state-comment-command loop must, when COMMAND=in-review and the resolved Owner would
  // be Codex, look up the current linked issue's Implementer and override to Robert if Codex.
  const idx = ORCH_YAML.indexOf('if [ "$COMMAND" = "in-review" ] && [ "$OWNER_DISPLAY" = "Codex" ]; then');
  assert.ok(idx > 0, "in-review reviewer-independence block not found in /fps-state loop");
  const block = ORCH_YAML.slice(idx, idx + 2000);
  // Per-issue Implementer lookup keyed to the current ITEM_ID
  assert.match(block, /-f id="\$ITEM_ID"/);
  assert.match(block, /Implementer/);
  // Reviewer-independence override when Implementer=Codex
  assert.match(block, /"\$IMPLEMENTER_NAME_REVIEW" = "Codex"/);
  assert.match(block, /OWNER_OPTION="\$OPT_OWNER_ROBERT"/);
  assert.match(block, /OWNER_DISPLAY="Robert"/);
});

test("workflow /fps-state in-review preserves an explicit non-Codex reviewer (Robert/Human/Claude/Copilot)", () => {
  // The reviewer-independence override guard specifically checks `OWNER_DISPLAY = "Codex"`,
  // so an explicit non-Codex EXPLICIT_OWNER never triggers the override — the guard itself
  // is the durable predicate that preserves an explicit Robert/Human/Claude/Copilot reviewer.
  const guard = ORCH_YAML.match(/if \[ "\$COMMAND" = "in-review" \] && \[ "\$OWNER_DISPLAY" = "Codex" \]; then/);
  assert.ok(guard, "in-review override must be gated on OWNER_DISPLAY=Codex to preserve explicit non-Codex owners");
});

test("workflow: both manual In-review paths are inside per-linked-issue loops (no cross-issue leakage)", () => {
  // Structural guarantee: the reviewer-independence overrides live inside `for ... do` loops
  // that iterate linked issues, so the per-iteration Implementer lookup cannot leak the first
  // issue's result into subsequent iterations. We assert each override sits between its loop's
  // opening `for ... do` and the start of the next job stanza (a stable structural marker).
  const routeLoopIdx = ORCH_YAML.indexOf("for TARGET_ISSUE in $TARGET_ISSUES");
  assert.ok(routeLoopIdx > 0, "codex-review target loop not found");
  const routeCodexIdx = ORCH_YAML.indexOf('CODEX_REVIEW_ROUTE" = "true"', routeLoopIdx);
  assert.ok(routeCodexIdx > routeLoopIdx, "codex-review per-issue block must appear inside the TARGET_ISSUES loop");

  const stateLoopIdx = ORCH_YAML.indexOf("for ISSUE_NUM in $LINKED_ISSUES");
  assert.ok(stateLoopIdx > 0, "/fps-state linked-issues loop not found");
  const routeJobIdx = ORCH_YAML.indexOf("route-comment-command:", stateLoopIdx);
  const inReviewIdx = ORCH_YAML.indexOf('COMMAND" = "in-review" ] && [ "$OWNER_DISPLAY" = "Codex"', stateLoopIdx);
  assert.ok(inReviewIdx > stateLoopIdx && inReviewIdx < routeJobIdx, "in-review override must appear inside the LINKED_ISSUES loop, not after the state job");
});

// --- Draft-PR guard for manual In-review commands (AUT-007) -------------------------------
// /fps-state in-review and /fps-route codex-review must not mutate linked-issue Status/Owner
// while the PR target is still a draft — Codex intentionally skips draft PRs, so a review
// handoff while draft would leave the board in a false review state. The guard queries the
// PR's isDraft field, and, when true, emits a notice pointing the implementer at "mark ready
// for review" and exits before any project item-edit. Once non-draft, the existing per-issue
// routing (including the Implementer=Codex → Robert reviewer-independence exception) applies.

test("workflow /fps-state in-review is draft-gated: exits before any board mutation when the PR is still a draft", () => {
  // The guard must appear inside the /fps-state comment step, gated on COMMAND=in-review,
  // must query isDraft on the target PR, and must exit before the linked-issue lookup and
  // per-issue loop that write Status/Owner.
  const inStateHandler = ORCH_YAML.match(
    /COMMAND="\$\(echo "\$COMMENT_BODY" \| awk '\{print \$2\}' \| head -1\)"[\s\S]+?\n {2}route-comment-command:/,
  );
  assert.ok(inStateHandler, "/fps-state comment handler section not found");
  const section = inStateHandler[0];
  assert.match(section, /if \[ "\$COMMAND" = "in-review" \]; then[\s\S]{0,800}pullRequest\(number:\$pr\)\{isDraft\}/,
    "/fps-state in-review must query PR isDraft when COMMAND=in-review");
  assert.match(section, /case "\$PR_IS_DRAFT_STATE" in[\s\S]{0,800}true\)[\s\S]{0,400}exit 0/,
    "/fps-state in-review must exit 0 when the PR is a draft (no board mutation)");
  // Structural: the draft guard must precede the LINKED_ISSUES lookup so no writes happen first.
  const guardIdx = section.indexOf('"$COMMAND" = "in-review"');
  const linkedIdx = section.indexOf("closingIssuesReferences(first:10)");
  assert.ok(guardIdx > 0 && linkedIdx > guardIdx,
    "/fps-state in-review draft guard must precede the linked-issues lookup and per-issue loop");
});

test("workflow /fps-route codex-review is draft-gated: exits before any board mutation when the PR target is still a draft", () => {
  const inRouteHandler = ORCH_YAML.match(
    /Parse: \/fps-route <command> \[<owner>\][\s\S]+$/,
  );
  assert.ok(inRouteHandler, "/fps-route comment handler section not found");
  const section = inRouteHandler[0];
  // Guard is gated on the codex-review route flag AND on IS_PR_COMMENT (issue targets must not
  // be affected — codex-review on an issue must continue to route to In review).
  assert.match(
    section,
    /\$CODEX_REVIEW_ROUTE" = "true" \] && \[ "\$IS_PR_COMMENT" = "true"[\s\S]{0,800}pullRequest\(number:\$pr\)\{isDraft\}/,
    "/fps-route codex-review must query PR isDraft when the target is a PR",
  );
  assert.match(
    section,
    /case "\$PR_IS_DRAFT_STATE" in[\s\S]{0,800}true\)[\s\S]{0,400}exit 0/,
    "/fps-route codex-review must exit 0 when the PR target is a draft",
  );
  // Structural: the draft guard must precede the TARGET_ISSUES resolution and per-issue loop.
  const guardIdx = section.indexOf('"$CODEX_REVIEW_ROUTE" = "true" ] && [ "$IS_PR_COMMENT"');
  const loopIdx = section.indexOf("for TARGET_ISSUE in $TARGET_ISSUES");
  assert.ok(guardIdx > 0 && loopIdx > guardIdx,
    "/fps-route codex-review draft guard must precede the TARGET_ISSUES per-issue loop");
});

test("workflow /fps-route codex-review draft guard does not fire for issue (non-PR) targets", () => {
  // The IS_PR_COMMENT gate ensures a /fps-route codex-review on an issue still routes normally
  // — the draft-PR concept does not apply to issue targets.
  const section = ORCH_YAML.slice(ORCH_YAML.indexOf("Parse: /fps-route <command> [<owner>]"));
  const guardLine = section.match(/if \[ "\$CODEX_REVIEW_ROUTE" = "true" \] && \[ "\$IS_PR_COMMENT" = "true" \]; then/);
  assert.ok(guardLine, "codex-review draft guard must be jointly gated on CODEX_REVIEW_ROUTE and IS_PR_COMMENT");
});

test("workflow: draft-gated manual In-review commands fall through only on explicit isDraft=false (fail-closed)", () => {
  // AUT-007 fail-closed contract: only an explicit "false" value for PR_IS_DRAFT_STATE may fall
  // through to the existing non-draft routing. "true" preserves state with the mark-ready
  // notice. Empty/null/unexpected/error values also preserve state and emit a warning — an
  // API/permission/parse error must not silently produce a false review handoff.
  const stateGuard = ORCH_YAML.match(
    /if \[ "\$COMMAND" = "in-review" \]; then[\s\S]+?\n {10}fi\n/,
  );
  assert.ok(stateGuard, "in-review guard block not found");
  const stateBody = stateGuard[0];
  assert.match(stateBody, /case "\$PR_IS_DRAFT_STATE" in/,
    "in-review guard must use a case statement on PR_IS_DRAFT_STATE (not a bare = 'true' test)");
  assert.match(stateBody, /true\)[\s\S]{0,400}exit 0/,
    "in-review guard 'true' arm must exit 0 with the mark-ready notice");
  assert.match(stateBody, /false\)\s*\n\s*:\s*\n\s*;;/,
    "in-review guard must have an explicit 'false' arm that is the only fallthrough to non-draft routing");
  assert.match(stateBody, /\*\)[\s\S]{0,600}::warning::[\s\S]{0,400}exit 0/,
    "in-review guard '*' (unknown/error) arm must warn and exit 0 without mutating the board");

  const routeGuard = ORCH_YAML.match(
    /if \[ "\$CODEX_REVIEW_ROUTE" = "true" \] && \[ "\$IS_PR_COMMENT" = "true" \]; then[\s\S]+?\n {10}fi\n/,
  );
  assert.ok(routeGuard, "codex-review guard block not found");
  const routeBody = routeGuard[0];
  assert.match(routeBody, /case "\$PR_IS_DRAFT_STATE" in/,
    "codex-review guard must use a case statement on PR_IS_DRAFT_STATE (not a bare = 'true' test)");
  assert.match(routeBody, /true\)[\s\S]{0,400}exit 0/,
    "codex-review guard 'true' arm must exit 0 with the mark-ready notice");
  assert.match(routeBody, /false\)\s*\n\s*:\s*\n\s*;;/,
    "codex-review guard must have an explicit 'false' arm that is the only fallthrough to non-draft routing");
  assert.match(routeBody, /\*\)[\s\S]{0,600}::warning::[\s\S]{0,400}exit 0/,
    "codex-review guard '*' (unknown/error) arm must warn and exit 0 without mutating the board");
});
