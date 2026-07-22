// Agent delivery-loop transition reducer.
//
// A REPOSITORY-NEUTRAL, pure decision function for the autonomous "Codex finding -> fix ->
// re-review -> merge" loop. It contains NO repository specifics — no project IDs, issue
// numbers, branch names, HIGH_RISK path patterns, or actor logins. All of those are resolved
// by the GitHub event *adapters* (the workflows / apply script) and passed in as classified
// facts; the adapters also map the abstract roles/stages this reducer returns onto concrete
// actors, Project option IDs, and labels.
//
// The contract is intentionally separable from GitHub: `transition(input) -> decision` is a
// pure function over plain data, so it is unit-testable without any network or gh calls.
//
// Durable state lives in the Project (Status / Owner / Implementer). Pipeline labels are
// observability only. The terminal cap fails CLOSED via Draft + an enforcement marker, never
// relying on a best-effort label alone.

/**
 * @typedef {"verdict"|"push"} EventKind
 * @typedef {"clean"|"blocking"|"advisory"} Verdict   // classified Codex severity
 * @typedef {"loop"|"manual"} ImplementerKind         // loop = participates in the autonomous
 *                                                    //   fix loop (e.g. Copilot); manual =
 *                                                    //   human / Claude (draft + hand-back).
 * @typedef {"reviewer"|"implementer"|"human"|"none"} Role
 * @typedef {"in-review"|"needs-changes"|"blocked"} Status
 * @typedef {"in-review"|"addressing-feedback"|"auto-merging"|"needs-human"} Stage
 */

/**
 * @typedef {Object} Input
 * @property {EventKind}       event
 * @property {Verdict}        [verdict]          // required for event==="verdict"
 * @property {boolean}        [headMoved]        // reviewed SHA !== current head (stale)
 * @property {boolean}        [alreadyProcessed] // this (PR, reviewedSHA) already handled (dedup)
 * @property {ImplementerKind} implementerKind
 * @property {boolean}         isHighRisk
 * @property {number}          priorRounds       // unique blocking repair rounds already recorded
 * @property {number}          maxRounds
 * @property {boolean}         capped            // terminal cap already in effect (marker present)
 * @property {boolean}        [isDraft]          // current draft state
 */

/**
 * @typedef {Object} Route  // durable Project target; roles/status are abstract (adapter maps them)
 * @property {Status}  status
 * @property {Role}    owner
 * @property {Role}   [implementer]
 */

/**
 * @typedef {Object} Decision
 * @property {string}   action         // stable machine label for the transition (tests/logs)
 * @property {string}   reason
 * @property {Stage|null}  applyStage
 * @property {Route|null}  route
 * @property {boolean|null} setDraft    // true=convert to draft, false=mark ready, null=leave
 * @property {boolean}  recordRound     // count this as a new unique repair round
 * @property {boolean}  addCapMarker    // add the terminal-cap enforcement marker (+ draft fallback)
 * @property {boolean}  nudgeImplementer// post the single "@implementer, address the review" nudge
 * @property {boolean}  requestReview   // re-request a fresh reviewer pass (@codex review)
 * @property {boolean|null} mergeEligible // hint for the merge gate (clean && low-risk && !capped)
 * @property {string|null} comment      // AT MOST ONE human-facing comment key per transition
 */

/** @returns {Decision} */
function base(action, reason, over = {}) {
  return {
    action, reason,
    applyStage: null, route: null, setDraft: null,
    recordRound: false, addCapMarker: false, nudgeImplementer: false,
    requestReview: false, mergeEligible: null, comment: null,
    ...over,
  };
}

/**
 * Pure transition. Same input always yields the same decision (idempotency is enforced by the
 * adapter via `alreadyProcessed` / `headMoved` / `capped`, all supplied as facts).
 * @param {Input} input
 * @returns {Decision}
 */
export function transition(input) {
  const i = input || {};
  const maxRounds = Number.isFinite(i.maxRounds) && i.maxRounds > 0 ? i.maxRounds : 3;
  const prior = Number.isFinite(i.priorRounds) && i.priorRounds >= 0 ? i.priorRounds : 0;

  if (i.event === "push") {
    // A fix push. A capped loop is held for a human and must NOT silently resume.
    if (i.capped) return base("ignore-capped-push", "PR is capped (held for a human) — a push does not resume it");
    // Otherwise resume review of the new head.
    return base("reready-review", "implementer pushed a fix — resume review of the new head", {
      applyStage: "in-review",
      route: { status: "in-review", owner: "reviewer" },
      requestReview: true,
    });
  }

  if (i.event === "verdict") {
    // Idempotency / staleness / hold guards (facts supplied by the adapter).
    if (i.alreadyProcessed) return base("ignore-duplicate", "verdict for this (PR, reviewed SHA) was already processed");
    if (i.headMoved)        return base("ignore-stale", "reviewed SHA no longer matches the PR head — a fix already landed");
    if (i.capped)           return base("ignore-capped", "PR is capped (held for a human) — verdicts do not resume it");

    if (i.verdict === "clean") {
      // Codex is satisfied. Merge is the gate's job; the reducer only classifies eligibility and
      // normalizes the board to the reviewer. High-risk stays re-reviewable but needs a human merge.
      return base("clean-verdict", i.isHighRisk
        ? "clean verdict, high-risk path — re-reviewable but requires a human merge"
        : "clean verdict, low-risk — eligible for auto-merge", {
        applyStage: i.isHighRisk ? "in-review" : "auto-merging",
        route: { status: "in-review", owner: "reviewer" },
        mergeEligible: !i.isHighRisk,
      });
    }

    if (i.verdict !== "blocking") {
      // advisory (P3 / unlabelled) — not a repair round, leave the PR as-is.
      return base("ignore-advisory", "advisory-only verdict (no P0/P1/P2) — no repair round");
    }

    // Blocking (P0/P1/P2).
    if (i.implementerKind === "manual") {
      // Human / Claude PR: draft it (a human might otherwise merge with open findings) and hand
      // it back to its implementer. Not part of the round-counted autonomous loop.
      return base("draft-and-handback", "blocking finding on a manually-authored PR — draft + hand back to the implementer", {
        setDraft: true,
        applyStage: "addressing-feedback",
        route: { status: "needs-changes", owner: "implementer" },
        comment: "manual-handback",
      });
    }

    // Loop implementer (e.g. Copilot). Count this as a repair round.
    const round = prior + 1;
    if (round >= maxRounds) {
      // TERMINAL CAP — fail closed. Draft is the durable hold; the marker + needs-human label are
      // the enforcement/observability; Blocked/human is the durable owner.
      return base("cap-terminal", `blocking finding reached the round cap (${round}/${maxRounds})`, {
        setDraft: true,
        addCapMarker: true,
        applyStage: "needs-human",
        route: { status: "blocked", owner: "human" },
        recordRound: true,
        comment: "cap-terminal",
      });
    }
    // Route the fix back to the loop implementer; the PR stays Ready (the gate blocks a merge
    // without a clean verdict), and a distinct nudge (at most one) drives the next round.
    return base("nudge-loop", `blocking finding — route the fix back to the loop implementer (round ${round}/${maxRounds})`, {
      nudgeImplementer: true,
      applyStage: "addressing-feedback",
      route: { status: "needs-changes", owner: "implementer", implementer: "implementer" },
      recordRound: true,
      comment: `nudge-round-${round}`,
    });
  }

  return base("ignore-unknown", `unknown event '${String(i.event)}'`);
}

// ---- CLI adapter: `node agent-loop-reducer.mjs '<json>'` (or JSON on stdin) -> decision JSON ----
function isMain() {
  try { return process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href; }
  catch { return false; }
}
if (isMain()) {
  const arg = process.argv[2];
  const read = (s) => JSON.parse(s);
  const run = (payload) => {
    const out = transition(read(payload));
    process.stdout.write(JSON.stringify(out));
  };
  if (arg) {
    run(arg);
  } else {
    let buf = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (d) => (buf += d));
    process.stdin.on("end", () => run(buf));
  }
}
