#!/usr/bin/env node
/**
 * tools/pr-review.mjs
 * PR-style LLM review of git changes via OpenAI.
 *
 * Called from pre-commit (reviews staged diff) and pre-push (reviews branch diff).
 * Exits 0 for APPROVE/COMMENT, exits 1 for REQUEST_CHANGES.
 */

const isPush = process.argv.includes('--push');

// ── Read git diff ─────────────────────────────────────────────────────────────

import { execSync } from 'node:child_process';

function run(cmd) {
  try { return execSync(cmd, { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }); }
  catch { return ''; }
}

let stat, diff;
if (isPush) {
  // Branch diff vs remote tracking branch — everything not yet pushed
  stat = run('git diff @{upstream}...HEAD --stat 2>/dev/null');
  diff = run('git diff @{upstream}...HEAD 2>/dev/null');

  if (!diff.trim()) {
    // No upstream yet (new branch) — fall back to comparing vs master
    stat = run('git diff master...HEAD --stat 2>/dev/null');
    diff = run('git diff master...HEAD 2>/dev/null');
  }
} else {
  stat = run('git diff --cached --stat');
  diff = run('git diff --cached');
}

if (!diff.trim()) {
  console.log('[pr-review] No changes to review — skipping LLM call.');
  process.exit(0);
}

// Trim diff to avoid token limits (~60k chars ≈ ~15k tokens)
const MAX_DIFF_CHARS = 60_000;
const trimmed = diff.length > MAX_DIFF_CHARS;
const diffBody = trimmed ? diff.slice(0, MAX_DIFF_CHARS) + '\n\n[... diff trimmed for length ...]' : diff;

// ── OpenAI review ─────────────────────────────────────────────────────────────

const OPENAI_API_KEY = process.env.OPENAI_API_KEY;
if (!OPENAI_API_KEY) {
  console.warn('[pr-review] OPENAI_API_KEY not set — skipping LLM review.');
  process.exit(0);
}

const SYSTEM = `\
You are a senior code reviewer for the FPS (Fair Parking System) project.
FPS is a multi-tenant SaaS parking allocation platform.
Stack: .NET 10, C#, Dapr 1.14+, MongoDB (Dapr state store + driver), RabbitMQ (Dapr pub/sub), React, React Native/Expo.
Architecture: Onion/Clean Architecture per service, DDD aggregates, CQRS + MediatR, Dapr Workflows.
No EF Core. No PostgreSQL. No SQL Server. No Swagger/NSwag (uses Scalar).
Multi-tenancy: database-per-tenant (fps_{tenant_id}).

Review the git diff and return ONLY this JSON:
{
  "verdict": "APPROVE" | "COMMENT" | "REQUEST_CHANGES",
  "summary": "<one paragraph describing what changed>",
  "blocking_issues": ["<critical problem>"],
  "suggestions": ["<non-blocking improvement>"],
  "tests_to_run": ["<dotnet test command or xunit test class name>"]
}

REQUEST_CHANGES when:
- Security vulnerability (injection, auth bypass, insecure deserialization)
- Sensitive data exposed (.env, secret, private key, token, password in code)
- Test files deleted without replacement
- Auth/security/payment code changed without test coverage
- Broad deletion or repo-wide rewrite without documented plan
- Broken architecture (domain layer imports infrastructure, etc.)
- EF Core, SQL Server, Swagger, or any technology explicitly replaced by the project
- git push --force or --no-verify in scripts
- rm -rf or destructive operations without justification

COMMENT when:
- Missing test coverage for new non-trivial code
- Naming inconsistencies with project conventions
- Minor architecture drift
- Code duplication that could be shared
- Documentation gap for complex logic

APPROVE when:
- Routine safe changes with adequate test coverage
- Documentation or tooling updates
- Bug fixes with tests
- Configuration changes that look correct`;

const userMsg = `Files changed:\n${stat}\n\nFull diff:\n${diffBody}`;

console.log(`[pr-review] Sending ${isPush ? 'branch' : 'staged'} diff to OpenAI for review…`);

let review;
try {
  const res = await fetch('https://api.openai.com/v1/chat/completions', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${OPENAI_API_KEY}`,
    },
    body: JSON.stringify({
      model: 'gpt-4o',
      temperature: 0,
      response_format: { type: 'json_object' },
      messages: [
        { role: 'system', content: SYSTEM },
        { role: 'user', content: userMsg },
      ],
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`OpenAI ${res.status}: ${text}`);
  }

  const data = await res.json();
  review = JSON.parse(data.choices[0].message.content);
} catch (err) {
  console.warn(`[pr-review] OpenAI error: ${err.message} — allowing commit.`);
  process.exit(0);
}

// ── Print review ──────────────────────────────────────────────────────────────

const { verdict, summary, blocking_issues = [], suggestions = [], tests_to_run = [] } = review;

const icons = { APPROVE: '✅', COMMENT: '💬', REQUEST_CHANGES: '🚫' };
console.log(`\n${icons[verdict] ?? '?'} PR REVIEW — ${verdict}\n`);
console.log(`Summary:\n  ${summary}\n`);

if (blocking_issues.length) {
  console.log('Blocking issues:');
  for (const issue of blocking_issues) console.log(`  ✗ ${issue}`);
  console.log('');
}

if (suggestions.length) {
  console.log('Suggestions:');
  for (const s of suggestions) console.log(`  → ${s}`);
  console.log('');
}

if (tests_to_run.length) {
  console.log('Tests to run:');
  for (const t of tests_to_run) console.log(`  $ ${t}`);
  console.log('');
}

if (verdict === 'REQUEST_CHANGES') {
  console.error('[pr-review] Commit blocked — fix the issues above and try again.');
  process.exit(1);
}

process.exit(0);
