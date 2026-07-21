// Draft-PR routing gate for the transitional delivery-state orchestrator (AUT-007).
//
// A REPOSITORY-NEUTRAL, pure decision function documenting exactly which of the
// `.github/workflows/delivery-state-orchestrator.yml` PR-event jobs (`pr-in-review` /
// `pr-draft-open-in-progress`) should act on a given `pull_request_target` event, and what it
// should do to the linked issue's Status/Owner. It exists to give the routing table used by that
// workflow's inline bash a single, unit-testable source of truth, without pulling the whole
// orchestrator into the broader AUT-005 shared-engine effort (which remains blocked/out of
// scope). Same contract style as tools/agent-loop-reducer.mjs: `route(input) -> Decision` is a
// pure function over plain data.

/**
 * @typedef {"opened"|"synchronize"|"reopened"|"ready_for_review"} PrAction
 * @typedef {"codex"|"implementer"} OwnerRole
 * @typedef {"in-review"|"in-progress"} RouteStatus
 */

/**
 * @typedef {Object} Input
 * @property {PrAction} action
 * @property {boolean}  isDraft         // PR draft state as of this event
 * @property {string}   [currentStatus] // linked issue's current Status field value, if any
 */

/**
 * @typedef {Object} Route
 * @property {RouteStatus} status
 * @property {OwnerRole}   owner
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
 * Decide what a `pull_request_target` opened/synchronize/reopened/ready_for_review event should
 * do to a linked issue's board state.
 * @param {Input} input
 * @returns {Decision}
 */
export function route(input) {
  const i = input || {};
  const status = i.currentStatus || "";

  // Only an explicit ready_for_review event, or an opened/synchronize/reopened event on a PR
  // that is ALREADY ready (never a draft), may hand the linked issue to Codex review.
  if (i.action === "ready_for_review" || !i.isDraft) {
    return decision(
      "route-in-review",
      "PR is ready for review (or was already ready) — hand the linked issue to Codex",
      { status: "in-review", owner: "codex" },
    );
  }

  // A draft opened/synchronize/reopened event never touches Codex review. It may only nudge a
  // freshly Assigned slice into In progress under its existing Implementer; every other Status
  // (Needs changes, Blocked, a capped hold, Done, In review, Backlog, ...) is left untouched so a
  // WIP push can never erase a manual/terminal hold or an ongoing implementation/repair state.
  if (status === "Assigned") {
    return decision(
      "route-in-progress",
      "first draft on an Assigned slice — move to In progress under the existing Implementer",
      { status: "in-progress", owner: "implementer" },
    );
  }

  return decision(
    "preserve",
    `draft PR event does not change the existing Status="${status || "(unset)"}"`,
    null,
  );
}
