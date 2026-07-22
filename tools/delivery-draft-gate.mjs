// Draft-PR routing gate for the transitional delivery-state orchestrator (AUT-007).
//
// A REPOSITORY-NEUTRAL, pure decision function documenting exactly what the single
// `.github/workflows/delivery-state-orchestrator.yml` `pr-event-routing` job should do to a
// linked issue's Status/Owner for a given `pull_request_target` event. It exists to give the
// routing table used by that workflow's inline bash a single, unit-testable source of truth, without pulling the whole
// orchestrator into the broader AUT-005 shared-engine effort (which remains blocked/out of
// scope). Same contract style as tools/agent-loop-reducer.mjs: `route(input) -> Decision` is a
// pure function over plain data.

/**
 * @typedef {"opened"|"synchronize"|"reopened"|"ready_for_review"} PrAction
 * @typedef {"codex"|"implementer"|"human"} OwnerRole
 * @typedef {"in-review"|"in-progress"} RouteStatus
 * @typedef {"claude"|"copilot"|"codex"|"robert"|null} OwnerOption
 */

/**
 * @typedef {Object} Input
 * @property {PrAction} action
 * @property {boolean}  isDraft             // PR draft state as of this event
 * @property {string}   [currentStatus]     // linked issue's current Status field value, if any
 * @property {string}   [currentImplementer] // linked issue's current Implementer field value, if any
 */

/**
 * @typedef {Object} Route
 * @property {RouteStatus}    status
 * @property {OwnerRole}      owner
 * @property {OwnerOption}    [ownerOption] // resolved Owner option to write on the linked issue.
 *                                          // For route-in-progress: null when Implementer is
 *                                          // empty/unrecognized — the caller MUST NOT write
 *                                          // Owner=None and must instead preserve the existing
 *                                          // Owner value.
 *                                          // For route-in-review: "codex" by default, "robert"
 *                                          // when the linked issue's Implementer is "Codex" (a
 *                                          // Codex-implemented PR must not be reviewed by Codex,
 *                                          // per AGENTS.md; route to a Human reviewer instead).
 */

/**
 * @typedef {Object} Decision
 * @property {string}     action  // stable machine label (tests/logs)
 * @property {string}     reason
 * @property {Route|null} route   // null == make no board change (preserve current state)
 */

/** @param {string} action, {string} reason, {Route|null} [route] @returns {Decision} */
function decision(action, reason, route = null) {
  return { action, reason, route };
}

/**
 * Map a linked issue's Implementer field value to the Owner option that should be written when a
 * draft nudges the issue from Assigned to In progress. Returns null for an empty/unrecognized
 * Implementer so the caller preserves the existing Owner instead of overwriting it with None —
 * an empty/unrecognized Implementer is not evidence the issue has no owner.
 * @param {string} [currentImplementer]
 * @returns {OwnerOption}
 */
export function mapImplementerToOwnerOption(currentImplementer) {
  switch (currentImplementer) {
    case "Claude":
      return "claude";
    case "Copilot":
      return "copilot";
    case "Codex":
      return "codex";
    case "Human":
      return "robert";
    default:
      return null;
  }
}

/**
 * Decide what a `pull_request_target` opened/synchronize/reopened/ready_for_review event should
 * do to a linked issue's board state.
 * @param {Input} input
 * @returns {Decision}
 */
export function route(input) {
  const i = input || {};
  const status = i.currentStatus || "";

  // Only an explicit ready_for_review event, or an opened/synchronize/reopened event on a PR
  // that is ALREADY ready (never a draft), may hand the linked issue to review. Codex is the
  // default reviewer, EXCEPT when the linked issue's Implementer is "Codex" — a Codex-authored
  // PR must not be reviewed by Codex (AGENTS.md: "Codex must not review or merge a PR it
  // implemented itself … Route a Codex-authored PR's review to Claude or a human"). In that
  // case route the review handoff to Human (Robert) instead of Codex.
  if (i.action === "ready_for_review" || !i.isDraft) {
    const codexImplemented = i.currentImplementer === "Codex";
    return decision(
      "route-in-review",
      codexImplemented
        ? "PR ready for review — Codex-implemented, hand to Human (Codex must not review its own PR)"
        : "PR is ready for review (or was already ready) — hand the linked issue to Codex",
      {
        status: "in-review",
        owner: codexImplemented ? "human" : "codex",
        ownerOption: codexImplemented ? "robert" : "codex",
      },
    );
  }

  // A draft opened/synchronize/reopened event never touches Codex review. It may only nudge a
  // freshly Ready or Assigned slice into In progress under its existing Implementer — a draft PR
  // is direct evidence implementation has begun, so a slice still sitting at Ready (assignment
  // handoff not yet reconciled) advances the same way as one already at Assigned. Every other
  // Status (Needs changes, Blocked, a capped hold, Done, In review, Backlog, ...) is left
  // untouched so a WIP push can never erase a manual/terminal hold or an ongoing
  // implementation/repair state.
  if (status === "Assigned" || status === "Ready") {
    return decision(
      "route-in-progress",
      `first draft on a ${status} slice — move to In progress under the existing Implementer`,
      {
        status: "in-progress",
        owner: "implementer",
        ownerOption: mapImplementerToOwnerOption(i.currentImplementer),
      },
    );
  }

  return decision(
    "preserve",
    `draft PR event does not change the existing Status="${status || "(unset)"}"`,
    null,
  );
}

// --- CLI entry point ---
//
// `.github/workflows/delivery-state-orchestrator.yml` (`pr-event-routing` job) invokes this file
// directly as the single source of truth for the routing decision, instead of duplicating the
// branching in inline bash/YAML. Reads PR_ACTION / PR_IS_DRAFT / ISSUE_CURRENT_STATUS /
// ISSUE_CURRENT_IMPLEMENTER from the environment and writes the Decision as JSON to stdout.
function isMainModule() {
  return process.argv[1] && import.meta.url === `file://${process.argv[1]}`;
}

if (isMainModule()) {
  const result = route({
    action: process.env.PR_ACTION,
    isDraft: process.env.PR_IS_DRAFT === "true",
    currentStatus: process.env.ISSUE_CURRENT_STATUS || "",
    currentImplementer: process.env.ISSUE_CURRENT_IMPLEMENTER || "",
  });
  process.stdout.write(JSON.stringify(result));
}
