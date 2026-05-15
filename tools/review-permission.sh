#!/bin/zsh
set -eu

INPUT="$(cat)"
TOOL="$(printf '%s' "$INPUT" | jq -r '.tool_name // ""')"
CMD="$(printf '%s' "$INPUT" | jq -r '.tool_input.command // ""')"
FILE="$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // ""')"

emit() {
  local decision="$1"
  local reason="${2:-}"

  case "$decision" in
    allow)
      jq -n \
        --arg event "PermissionRequest" \
        '{hookSpecificOutput:{hookEventName:$event,decision:{behavior:"allow"}}}'
      ;;
    deny)
      jq -n \
        --arg event "PermissionRequest" \
        --arg reason "$reason" \
        '{hookSpecificOutput:{hookEventName:$event,decision:{behavior:"deny",message:$reason}}}'
      ;;
  esac
  exit 0
}

deny() { emit deny "$1"; }
allow() { emit allow; }
ask() {
  # PermissionRequest supports only allow/deny decisions. Exit with no stdout to
  # preserve the normal user permission prompt for cases that need confirmation.
  printf '%s\n' "$1" >&2
  exit 0
}

# ── Auto-allow: read-only tools ───────────────────────────────────────────────
case "$TOOL" in
  Read|Glob|Grep) allow ;;
esac

# ── Auto-allow: safe read-only shell commands ─────────────────────────────────
case "$CMD" in
  "git status"*|"git diff"*|"git log"*|"git branch"*|"git show"*) allow ;;
  "dotnet test"*|"dotnet build"*|"dotnet restore"*) allow ;;
  ls*|find*|cat*|head*|tail*|echo*|pwd|whoami|which*|wc*) allow ;;
  "gh run"*|"gh pr view"*|"gh repo view"*) allow ;;
esac

# ── Hard block: destructive / rule-violating commands ─────────────────────────
case "$CMD" in
  *"rm -rf"*|*"rm -fr"*)
    deny "rm -rf is blocked. Use targeted deletion with specific paths." ;;
  "sudo "*|*" sudo "*)
    deny "sudo is blocked. Explain why elevated privileges are needed." ;;
  *"chmod -R 777"*)
    deny "chmod -R 777 is blocked. Use the minimum required permissions." ;;
  *"git push --force"*|*"git push -f "*)
    deny "Force push is blocked per project rules." ;;
  *"--no-verify"*)
    deny "--no-verify is blocked. Fix the hook failure instead of bypassing it." ;;
  *"git commit --no-verify"*)
    deny "--no-verify is blocked." ;;
  *"drop table"*|*"DROP TABLE"*)
    deny "Dropping tables is blocked. Use a migration instead." ;;
  *"delete_migration"*|*"remove_migration"*)
    deny "Deleting migrations is blocked. Create a new migration to revert." ;;
esac

# ── Hard block: editing sensitive files ───────────────────────────────────────
case "$FILE" in
  *.env|*".env."*|*secret*|*password*|*token*|*"private_key"*|*"private.key"*)
    deny "Editing sensitive files is blocked. Review manually." ;;
esac

# ── Hard block: removing tests ────────────────────────────────────────────────
case "$CMD" in
  *"rm "*"test"*|*"rm "*"spec"*|*"rm "*"Test"*|*"rm "*"Spec"*)
    deny "Removing test files is blocked." ;;
esac

# ── Ask: auth / security / payment code without mention of tests ──────────────
case "$FILE" in
  *"Auth"*|*"auth"*|*"Security"*|*"security"*|*"Payment"*|*"payment"*|*"Billing"*|*"billing"*)
    ask "Editing auth/security/payment file. Ensure tests cover this change." ;;
esac

# ── Default: allow ────────────────────────────────────────────────────────────
allow
