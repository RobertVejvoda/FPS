#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Fix GitHub issue/PR conversation comments that contain literal "\n" text.

Dry-run is the default. Use --apply to edit comments.

Usage:
  tools/fix-github-comment-newlines.sh [options]

Options:
  --dry-run              List matching comments without changing anything (default).
  --apply                Replace literal "\n" sequences with real newlines.
  --issue NUMBER         Limit to one issue number. Pull requests work too because
                         GitHub PR conversation comments are issue comments.
  --pr NUMBER            Alias for --issue NUMBER.
  --since YYYY-MM-DD     Only include comments created on or after this date.
  --author LOGIN         Only include comments by this GitHub login.
                         Defaults to the authenticated gh user.
  --repo OWNER/REPO      Repository to update. Defaults to the current gh repo.
  --limit N              Stop after N matching comments.
  -h, --help             Show this help.

Examples:
  tools/fix-github-comment-newlines.sh --dry-run
  tools/fix-github-comment-newlines.sh --apply --since 2026-05-20
  tools/fix-github-comment-newlines.sh --apply --issue 303
USAGE
}

mode="dry-run"
issue_number=""
since=""
repo=""
author=""
limit=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run)
      mode="dry-run"
      shift
      ;;
    --apply)
      mode="apply"
      shift
      ;;
    --issue|--pr)
      issue_number="${2:-}"
      shift 2
      ;;
    --since)
      since="${2:-}"
      shift 2
      ;;
    --author)
      author="${2:-}"
      shift 2
      ;;
    --repo)
      repo="${2:-}"
      shift 2
      ;;
    --limit)
      limit="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

need gh
need jq

gh_api_retry() {
  local attempt=1
  local max_attempts=4
  local delay=2

  while true; do
    if gh api "$@"; then
      return 0
    fi

    if [[ "$attempt" -ge "$max_attempts" ]]; then
      return 1
    fi

    echo "gh api failed; retrying in ${delay}s ($attempt/$max_attempts)..." >&2
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

gh_api_paginate_retry() {
  local attempt=1
  local max_attempts=4
  local delay=2

  while true; do
    if gh api --paginate "$@"; then
      return 0
    fi

    if [[ "$attempt" -ge "$max_attempts" ]]; then
      return 1
    fi

    echo "gh api --paginate failed; retrying in ${delay}s ($attempt/$max_attempts)..." >&2
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner -q .nameWithOwner)"
fi

if [[ -z "$author" ]]; then
  author="$(gh_api_retry user --jq .login)"
fi

if [[ -n "$since" && ! "$since" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]; then
  echo "--since must use YYYY-MM-DD" >&2
  exit 2
fi

if [[ -n "$limit" && ! "$limit" =~ ^[0-9]+$ ]]; then
  echo "--limit must be a non-negative integer" >&2
  exit 2
fi

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

comments_json="$tmpdir/comments.json"
matches_jsonl="$tmpdir/matches.jsonl"

use_paginate="true"
if [[ -n "$issue_number" ]]; then
  endpoint="repos/$repo/issues/$issue_number/comments?per_page=100"
  use_paginate="false"
else
  endpoint="repos/$repo/issues/comments?per_page=100"
fi

echo "Repository: $repo"
echo "Author:     $author"
echo "Mode:       $mode"
if [[ -n "$issue_number" ]]; then echo "Issue/PR:   #$issue_number"; fi
if [[ -n "$since" ]]; then echo "Since:      $since"; fi
if [[ -n "$limit" ]]; then echo "Limit:      $limit"; fi
echo

if [[ "$use_paginate" == "true" ]]; then
  gh_api_paginate_retry "$endpoint" > "$comments_json"
else
  gh_api_retry "$endpoint" > "$comments_json"
fi

jq_filter='
  .[]
  | select(.user.login == $author)
  | select(.body | contains("\\n"))
  | select(($since == "") or (.created_at >= ($since + "T00:00:00Z")))
  | {id, html_url, created_at, body}
'

jq -c --arg author "$author" --arg since "$since" "$jq_filter" "$comments_json" > "$matches_jsonl"

if [[ -n "$limit" ]]; then
  limited="$tmpdir/matches-limited.jsonl"
  head -n "$limit" "$matches_jsonl" > "$limited"
  mv "$limited" "$matches_jsonl"
fi

count="$(wc -l < "$matches_jsonl" | tr -d ' ')"

if [[ "$count" == "0" ]]; then
  echo "No matching comments found."
  exit 0
fi

echo "Matching comments: $count"
echo

if [[ "$mode" == "dry-run" ]]; then
  jq -r '
    [.id, .created_at, .html_url, (.body[0:120] | gsub("\n"; " ") )]
    | @tsv
  ' "$matches_jsonl"
  echo
  echo "Dry-run only. Re-run with --apply to update these comments."
  exit 0
fi

echo "Applying newline fixes..."
while IFS= read -r comment; do
  id="$(jq -r .id <<<"$comment")"
  url="$(jq -r .html_url <<<"$comment")"
  body="$(jq -r '.body | gsub("\\\\n"; "\n")' <<<"$comment")"
  payload="$tmpdir/payload-$id.json"

  jq -n --arg body "$body" '{body: $body}' > "$payload"
  gh_api_retry --method PATCH "repos/$repo/issues/comments/$id" --input "$payload" >/dev/null
  echo "Updated $id $url"
done < "$matches_jsonl"

echo
echo "Done. Updated $count comments."
