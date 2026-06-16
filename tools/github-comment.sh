#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE' >&2
Usage:
  tools/github-comment.sh issue ISSUE_NUMBER BODY_FILE
  tools/github-comment.sh pr PR_NUMBER BODY_FILE
  tools/github-comment.sh update COMMENT_ID BODY_FILE

Posts or updates GitHub comments from a Markdown file so real newlines are
preserved. Avoid inline --body strings for multi-line comments.

Environment:
  GH_REPO  Repository in owner/name form. Defaults to RobertVejvoda/fairspot.
USAGE
}

if [ "$#" -ne 3 ]; then
  usage
  exit 2
fi

mode="$1"
target="$2"
body_file="$3"
repo="${GH_REPO:-RobertVejvoda/fairspot}"

if [ ! -f "$body_file" ]; then
  printf 'ERROR: body file not found: %s\n' "$body_file" >&2
  exit 1
fi

case "$mode" in
  issue)
    gh issue comment "$target" --repo "$repo" --body-file "$body_file"
    ;;
  pr)
    gh pr comment "$target" --repo "$repo" --body-file "$body_file"
    ;;
  update)
    tmp="$(mktemp)"
    trap 'rm -f "$tmp"' EXIT
    jq -n --rawfile body "$body_file" '{body: $body}' > "$tmp"
    gh api "repos/$repo/issues/comments/$target" \
      -X PATCH \
      --input "$tmp" \
      --jq .html_url
    ;;
  *)
    usage
    exit 2
    ;;
esac
