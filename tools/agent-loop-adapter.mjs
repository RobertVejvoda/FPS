// GitHub adapter for the agent delivery-loop reducer.
//
// This is the repo-facing side of the loop: it turns a GitHub event into the reducer's neutral
// facts, gathers durable state, invokes the pure reducer, and applies the decision's side
// effects. All repo specifics come from a config JSON (see agent-loop-config.fairspot.json) —
// this file has no FairSpot literals. The pure helpers (classify / parseLedger / buildInput /
// resolveRoute / commentBody) are exported and unit-tested; only main() touches gh.
//
// Idempotency: every acting transition writes ONE comment carrying a hidden ledger marker
//   <!-- agent-loop v1 sha=<reviewedSha> round=<n|-> -->
// Dedup is "already a marker for this reviewed SHA"; the repair-round count is "number of round
// markers". A per-PR concurrency group in the workflows serializes transitions; the marker (and
// a re-check just before acting) backstops any residual race.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { transition } from "./agent-loop-reducer.mjs";

export const MARKER_RE = /agent-loop v1 sha=([0-9a-fA-F]{7,40}) round=([0-9]+|-)/g;
const CLEAN_RE = /did.?n.?t find|no (major |significant )?(issues|findings)|no findings|looks good|lgtm|swish/i;
const SEVERITY_RE = /\bP[012]\b/;
const REVIEWED_COMMIT_RE = /reviewed commit[^0-9a-f]{0,16}([0-9a-f]{7,40})/i;

/** Classify a Codex verdict from its text. @returns {"clean"|"blocking"|"advisory"} */
export function classifySeverity(body, inlineBodies = []) {
  if (CLEAN_RE.test(body || "")) return "clean";
  const haystack = [body || "", ...inlineBodies].join("\n");
  return SEVERITY_RE.test(haystack) ? "blocking" : "advisory";
}

/** Extract Codex's reviewed-commit SHA from a conversation comment (markdown-tolerant). */
export function reviewedCommitFrom(body) {
  const m = REVIEWED_COMMIT_RE.exec(body || "");
  return m ? m[1] : "";
}

/**
 * Confused-deputy binding: is the artifact-named PR the one that actually triggered this run? The
 * PR's head branch must equal the trusted workflow_run.head_branch. Absent a trusted branch (e.g.
 * a channel where it does not apply), the check is skipped. @returns {boolean} true = OK to act.
 */
export function boundToTriggeringRun(prHeadRef, wrHeadBranch) {
  return !wrHeadBranch || (prHeadRef || "") === wrHeadBranch;
}

/** Classify the PR author + changed files into neutral reducer facts. @returns {{implementerKind:"loop"|"manual", isHighRisk:boolean}} */
export function classify({ authorLogin, authorType, files }, config) {
  const isLoop = (config.loopImplementers || []).some(
    (a) => a.login === authorLogin && (!a.type || a.type === authorType)
  );
  const patterns = (config.highRiskPathPatterns || []).map((p) => new RegExp(p));
  const isHighRisk = (files || []).some((f) => patterns.some((re) => re.test(f)));
  return { implementerKind: isLoop ? "loop" : "manual", isHighRisk };
}

/**
 * Parse the durable ledger from the bot's prior comments. A repair round is a DISTINCT reviewed
 * SHA that was marked as a round — so a verdict delivered through both channels (two markers for
 * the same SHA) counts once, and a cross-channel race cannot inflate the round count.
 * @returns {{priorRounds:number, processedShas:Set<string>}}
 */
export function parseLedger(commentBodies) {
  const processedShas = new Set();
  const roundShas = new Set();
  for (const body of commentBodies || []) {
    MARKER_RE.lastIndex = 0;
    let m;
    while ((m = MARKER_RE.exec(body)) !== null) {
      const sha = m[1].toLowerCase();
      processedShas.add(sha);
      if (m[2] !== "-") roundShas.add(sha);
    }
  }
  return { priorRounds: roundShas.size, processedShas };
}

/** Build the neutral reducer input from facts + gathered state. */
export function buildInput({ event, verdict, reviewedSha, headSha }, facts, state, config) {
  const shaLc = (reviewedSha || "").toLowerCase();
  const headLc = (headSha || "").toLowerCase();
  // Compare by prefix — Codex's conversation marker is often an abbreviated SHA, so a strict !==
  // against the 40-char head would spuriously read as "moved". (Same idea as the auto-merge gate.)
  const sameHead = !!shaLc && !!headLc && (headLc.startsWith(shaLc) || shaLc.startsWith(headLc));
  const processed = [...state.ledger.processedShas].some((s) => s.startsWith(shaLc) || shaLc.startsWith(s));
  return {
    event,
    verdict,
    headMoved: event === "verdict" && !!reviewedSha && !!headSha && !sameHead,
    alreadyProcessed: event === "verdict" && !!shaLc && processed,
    implementerKind: facts.implementerKind,
    isHighRisk: facts.isHighRisk,
    priorRounds: state.ledger.priorRounds,
    maxRounds: config.maxRounds || 3,
    capped: state.capped,
    isDraft: state.isDraft,
  };
}

/** Map the reducer's abstract route roles to concrete Project option IDs. @returns {{status?:string, owner?:string, implementer?:string}|null} */
export function resolveRoute(route, config) {
  if (!route) return null;
  const p = config.project || {};
  const ownerId = { reviewer: p.owners?.reviewer, implementer: p.owners?.loopImplementer, human: p.owners?.human, none: p.owners?.none };
  const out = {};
  if (route.status) out.status = p.status?.[route.status];
  if (route.owner) out.owner = ownerId[route.owner];
  if (route.implementer) out.implementer = p.implementers?.loopImplementer;
  return out;
}

/** The single human-facing comment body (with the hidden ledger marker) for an acting decision. */
export function commentBody(decision, ctx) {
  const round = decision.recordRound ? String(ctx.round) : "-";
  const marker = `\n<!-- agent-loop v1 sha=${ctx.reviewedSha || "-"} round=${round} -->`;
  let text = "";
  switch (decision.comment) {
    case "manual-handback":
      text = "⏸️ Codex flagged a **P0/P1/P2** finding, so this PR was converted to **Draft** and handed back to its implementer (Status → Needs changes). Address the review comments above, then mark it **Ready for review** to re-trigger Codex.";
      break;
    case "cap-terminal":
      text = `🛑 The autonomous fix loop hit its **${ctx.maxRounds}-round cap** without a clean verdict. This PR is now **held for a human** (Draft, Blocked / Owner=Robert). A human must resolve the findings and deliberately route it back.`;
      break;
    default:
      if (decision.comment && decision.comment.startsWith("nudge-round-")) {
        text = `@copilot Codex found blocking issues — please **address the review** (round ${ctx.round} of ${ctx.maxRounds}). Read Codex's review comments above, fix them, and push to this branch.`;
      } else if (decision.requestReview) {
        text = "@codex review";
      } else {
        return null;
      }
  }
  return text + marker;
}

// ------------------------------- gh integration (main only) -------------------------------
const sh = (bin, args, opts = {}) => execFileSync(bin, args, { encoding: "utf8", ...opts }).trim();
const gh = (args, token) => sh("gh", args, { env: { ...process.env, ...(token ? { GH_TOKEN: token } : {}) } });
const ghSafe = (args, token) => { try { return gh(args, token); } catch { return ""; } };

function loadConfig() {
  const path = process.env.LOOP_CONFIG || "tools/agent-loop-config.fairspot.json";
  return JSON.parse(readFileSync(path, "utf8"));
}

function setStage(pr, repo, stage, config, appTok) {
  const labels = config.labels || {};
  const target = stage ? labels[stage]?.name : null;
  if (target) {
    ghSafe(["label", "create", target, "--repo", repo, "--color", labels[stage].color], appTok);
    ghSafe(["pr", "edit", pr, "--repo", repo, "--add-label", target], appTok);
  }
  // Clear every OTHER stage label (single-stage invariant). The cap marker is orthogonal.
  for (const key of ["in-review", "addressing-feedback", "auto-merging", "needs-human"]) {
    if (key === stage) continue;
    const name = labels[key]?.name;
    if (name) ghSafe(["pr", "edit", pr, "--repo", repo, "--remove-label", name], appTok);
  }
}

function routeProject(pr, repo, resolved, config, boardTok) {
  if (!resolved || !boardTok) { if (!boardTok) console.log("::warning::no PROJECT_SYNC_TOKEN — board not routed"); return; }
  const p = config.project;
  const linked = ghSafe(["api", "graphql", "-f",
    `query=query($o:String!,$r:String!,$n:Int!){repository(owner:$o,name:$r){pullRequest(number:$n){closingIssuesReferences(first:10){nodes{url}}}}}`,
    "-f", `o=${p.owner}`, "-f", `r=${repo.split("/")[1]}`, "-F", `n=${pr}`,
    "--jq", ".data.repository.pullRequest.closingIssuesReferences.nodes[].url"], boardTok);
  const urls = linked.split("\n").filter(Boolean);
  if (urls.length === 0) { console.log(`::notice::PR #${pr} has no linked closing issue — board not routed`); return; }
  for (const url of urls) {  // handles the "multiple linked issues" scenario by routing each
    const itemId = ghSafe(["project", "item-add", p.number, "--owner", p.owner, "--url", url, "--format", "json", "--jq", ".id"], boardTok);
    if (!itemId) { console.log(`::warning::could not resolve project item for ${url}`); continue; }
    const edit = (field, opt) => opt && ghSafe(["project", "item-edit", "--id", itemId, "--project-id", p.id, "--field-id", field, "--single-select-option-id", opt], boardTok);
    edit(p.fieldStatus, resolved.status);
    edit(p.fieldOwner, resolved.owner);
    edit(p.fieldImplementer, resolved.implementer);
  }
}

async function main() {
  const config = loadConfig();
  const repo = process.env.REPO;
  const pr = process.env.PR;
  const appTok = process.env.APP_TOKEN;
  const boardTok = process.env.BOARD_TOKEN;
  const event = process.env.EVENT;           // "verdict" | "push"
  let verdict, reviewedSha = "";

  if (event === "verdict") {
    const reviewId = process.env.REVIEW_ID || "";
    if (reviewId) {
      // from-review: fetch + VERIFY the formal review (author, PR pairing, reviewed SHA) — the
      // stage-1 artifact only names it; the review is authoritative here.
      let review;
      try { review = JSON.parse(gh(["api", `/repos/${repo}/pulls/${pr}/reviews/${reviewId}`], appTok)); }
      catch { console.log(`::warning::could not fetch review ${reviewId} for PR #${pr} — aborting`); return; }
      if ((review.user?.login || "") !== config.reviewerBot) { console.log(`review author '${review.user?.login}' is not the reviewer bot — ignoring`); return; }
      reviewedSha = review.commit_id || "";
      const inline = JSON.parse(ghSafe(["api", `/repos/${repo}/pulls/${pr}/comments`, "--paginate", "--jq", `[.[] | select(.pull_request_review_id == ${Number(reviewId)}) | .body]`], appTok) || "[]");
      verdict = classifySeverity(review.body, inline);
    } else {
      // from-comment: the conversation verdict (comment author verified in the workflow guard).
      const body = process.env.COMMENT_BODY || "";
      const abbrev = reviewedCommitFrom(body);
      // Resolve the (often abbreviated) marker SHA to the full SHA so the ledger stays consistent
      // with the formal-review channel (which carries the full commit_id) — keeps dedup + round
      // counting exact across channels.
      reviewedSha = abbrev ? (ghSafe(["api", `/repos/${repo}/commits/${abbrev}`, "--jq", ".sha"], appTok) || abbrev) : "";
      verdict = classifySeverity(body, []);
    }
    // The loop adapter only acts on findings; a clean verdict is the merge gate's job.
    if (verdict === "clean") { console.log("clean verdict — handled by the merge gate, not the loop"); return; }
  }

  // Gather state (single PR fetch + comments).
  const prJson = JSON.parse(gh(["api", `/repos/${repo}/pulls/${pr}`], appTok));

  // Confused-deputy guard (from-review only): the unprivileged stage-1 artifact NAMED the PR +
  // review, but a tampered stage-1 could name an unrelated PR that has an existing Codex review.
  // Bind them to the TRUSTED triggering run — the resolved PR's head branch must equal
  // workflow_run.head_branch — so an untrusted run can only ever act on its own PR.
  if (event === "verdict" && process.env.REVIEW_ID &&
      !boundToTriggeringRun(prJson.head?.ref, process.env.WR_HEAD_BRANCH)) {
    console.log(`::warning::PR #${pr} head branch '${prJson.head?.ref}' != triggering run branch '${process.env.WR_HEAD_BRANCH}' — refusing (confused-deputy guard)`);
    return;
  }
  const files = gh(["api", `/repos/${repo}/pulls/${pr}/files`, "--paginate", "--jq", ".[].filename"], appTok).split("\n").filter(Boolean);
  const commentBodies = JSON.parse(ghSafe(["api", `/repos/${repo}/issues/${pr}/comments`, "--paginate", "--jq", "[.[].body]"], appTok) || "[]");
  const labelNames = (prJson.labels || []).map((l) => l.name);
  const capMarker = config.labels?.capMarker?.name || "autofix-capped";

  const facts = classify({ authorLogin: prJson.user?.login, authorType: prJson.user?.type, files }, config);
  const state = { ledger: parseLedger(commentBodies), capped: labelNames.includes(capMarker), isDraft: !!prJson.draft };
  const input = buildInput({ event, verdict, reviewedSha, headSha: prJson.head?.sha }, facts, state, config);

  const decision = transition(input);
  console.log(`decision: ${decision.action} — ${decision.reason}`);
  if (decision.action.startsWith("ignore")) return;

  // Re-check dedup right before acting (narrows the residual race window).
  if (event === "verdict" && reviewedSha) {
    const fresh = parseLedger(JSON.parse(ghSafe(["api", `/repos/${repo}/issues/${pr}/comments`, "--paginate", "--jq", "[.[].body]"], appTok) || "[]"));
    if (fresh.processedShas.has(reviewedSha.toLowerCase())) { console.log("re-check: already processed — skipping"); return; }
  }

  const round = state.ledger.priorRounds + 1;
  const ctx = { round, maxRounds: config.maxRounds || 3, reviewedSha };

  if (decision.applyStage) setStage(pr, repo, decision.applyStage, config, appTok);

  // Add the cap marker BEFORE drafting, so the orchestrator's converted_to_draft handler (and the
  // gate) both see it and leave the terminal Blocked/Robert route intact. Verify with retry — the
  // marker gates dso's route preservation.
  let capMarked = true;
  if (decision.addCapMarker) {
    capMarked = false;
    for (let a = 0; a < 3 && !capMarked; a++) {
      ghSafe(["label", "create", capMarker, "--repo", repo, "--color", config.labels.capMarker.color], appTok);
      ghSafe(["pr", "edit", pr, "--repo", repo, "--add-label", capMarker], appTok);
      capMarked = ghSafe(["pr", "view", pr, "--repo", repo, "--json", "labels", "--jq", ".labels[].name"], appTok).split("\n").includes(capMarker);
    }
    if (!capMarked) console.log(`::warning::${capMarker} could not be applied after retries — will re-assert the terminal route after drafting`);
  }

  // Route AFTER the marker so the draft event cannot overwrite the terminal route (the handler
  // now skips autofix-capped PRs), and BEFORE the draft so the route is settled first.
  if (decision.route) routeProject(pr, repo, resolveRoute(decision.route, config), config, boardTok);

  const draftMutate = () => ghSafe(["api", "graphql", "-f", "query=mutation($id:ID!){convertPullRequestToDraft(input:{pullRequestId:$id}){pullRequest{isDraft}}}", "-f", `id=${prJson.node_id}`], appTok);
  const draftNow = () => ghSafe(["api", `/repos/${repo}/pulls/${pr}`, "--jq", ".draft"], appTok) === "true";
  if (decision.setDraft === true) {
    // Draft is the durable hold; for a capped PR it is the PRIMARY fail-closed control, so verify it.
    for (let a = 0; a < 3 && !draftNow(); a++) draftMutate();
    if (decision.addCapMarker && !draftNow()) {
      ghSafe(["pr", "comment", pr, "--repo", repo, "--body", `🛑 **Cap enforcement failed** on #${pr} — a human must hold this PR; do not merge until findings are resolved.`], appTok);
      console.log(`::error::cap enforcement failed on #${pr}`);
      process.exitCode = 1;
      return;
    }
  }
  if (decision.setDraft === false) ghSafe(["pr", "ready", pr, "--repo", repo], appTok);

  // If the cap marker never stuck, dso's draft handler will have overwritten the terminal route —
  // restore it (best-effort; the Draft above still holds the PR regardless).
  if (decision.addCapMarker && !capMarked && decision.route) routeProject(pr, repo, resolveRoute(decision.route, config), config, boardTok);

  const body = commentBody(decision, ctx);
  if (body) ghSafe(["pr", "comment", pr, "--repo", repo, "--body", body], appTok);
}

// Only run gh integration when executed directly (imports for tests get the pure helpers only).
function isMain() { try { return process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href; } catch { return false; } }
if (isMain()) { main().catch((e) => { console.error(e); process.exitCode = 1; }); }
