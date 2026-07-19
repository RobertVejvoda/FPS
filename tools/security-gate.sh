#!/usr/bin/env bash
# security-gate.sh — CI enforcement of the agent security rules that the local
# Claude/Codex hooks (tools/llm-review.mjs, tools/review-permission.sh) enforce
# ONLY inside a developer's local session. The GitHub Copilot coding agent runs
# in GitHub's cloud and never executes those hooks, so this gate re-imposes the
# same deny-list on every pull request as a required status check.
#
# Usage: security-gate.sh <base-ref> <head-ref>
# Exit 0 = pass, 1 = one or more hard violations.
set -uo pipefail

BASE="${1:-origin/master}"
HEAD="${2:-HEAD}"

changed="$(git diff --name-only "$BASE" "$HEAD")"
deleted="$(git diff --name-only --diff-filter=D "$BASE" "$HEAD")"
# Scan added lines for forbidden commands, but EXCLUDE the guard file itself and
# .github/ — those legitimately contain these patterns (and are high-risk paths
# gated to a human reviewer, so they are never auto-merged unreviewed anyway).
added="$(git diff --unified=0 "$BASE" "$HEAD" -- ':(exclude)tools/security-gate.sh' ':(exclude).github' | grep '^+' | grep -v '^+++' || true)"

fail=0
violation() { printf '::error::%s\n' "$1"; fail=1; }
warn()      { printf '::warning::%s\n' "$1"; }

# 1. Secret-bearing files must never be committed.
if printf '%s\n' "$changed" | grep -Eq '(^|/)\.env($|\.)|\.pem$|\.p12$|(^|/)id_rsa|(^|/)secrets?/'; then
  violation "Secret-bearing file added/modified (.env / *.pem / id_rsa / secrets/). Blocked — review manually."
fi

# 2. Tests must not be deleted.
if printf '%s\n' "$deleted" | grep -Eq '(\.|_|/)([Tt]est|[Ss]pec)s?(\.|_|/|$)|\.Tests?/'; then
  violation "Test/spec file deleted. Removing tests is blocked — add a replacement or keep them."
fi

# 3. Auth / security / payment code changed without any accompanying test change.
if printf '%s\n' "$changed" | grep -Eiq '(^|/)(auth|security|payment|billing)([/.]|$)'; then
  if ! printf '%s\n' "$changed" | grep -Eiq '(\.|_|/)([Tt]est|[Ss]pec)|\.Tests?/'; then
    violation "Auth/security/payment code changed with no test change. Add tests or split out the sensitive change."
  fi
fi

# 4. Forbidden command patterns introduced in the diff (scripts / workflows).
if printf '%s\n' "$added" | grep -Eq 'rm[[:space:]]+-[rf]{1,2}[[:space:]]|--no-verify|chmod[[:space:]]+-R[[:space:]]+777|(^|[^[:alnum:]])sudo[[:space:]]|[Dd][Rr][Oo][Pp][[:space:]]+[Tt][Aa][Bb][Ll][Ee]|git[[:space:]]+push[[:space:]]+(--force|-f)([[:space:]]|$)'; then
  violation "Forbidden command introduced (rm -rf / sudo / chmod 777 / --no-verify / force-push / DROP TABLE)."
fi

# 5. Dependency / supply-chain manifests — flag for human review (enforced by CODEOWNERS).
if printf '%s\n' "$changed" | grep -Eq '(^|/)(package(-lock)?\.json|yarn\.lock|pnpm-lock\.yaml)$|\.csproj$|Directory\.(Packages|Build)\.props$|(^|/)Dockerfile'; then
  warn "Dependency/build manifest changed — supply-chain review required (CODEOWNERS gates this to a human)."
fi

if [ "$fail" -ne 0 ]; then
  echo "Security gate: FAILED — see ::error:: annotations above."
  exit 1
fi
echo "Security gate: passed."
