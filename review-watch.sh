#!/bin/bash
# Watches for new git commits and auto-reviews the diff via llm (ChatGPT)

LAST_COMMIT=""

echo "Watching for new commits... (Ctrl+C to stop)"

while true; do
  CURRENT_COMMIT=$(git rev-parse HEAD 2>/dev/null)

  if [[ "$CURRENT_COMMIT" != "$LAST_COMMIT" && -n "$LAST_COMMIT" ]]; then
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "New commit: $(git log -1 --oneline)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    git diff "$LAST_COMMIT" "$CURRENT_COMMIT" | llm "Review this git diff. Flag any bugs, security issues, or bad practices. Be concise."
    echo ""
  fi

  LAST_COMMIT="$CURRENT_COMMIT"
  sleep 5
done
