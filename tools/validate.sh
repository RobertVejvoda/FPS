#!/bin/sh
set -eu

# Ensure .NET 10 (user install) is on PATH
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
CONFIGURATION="${CONFIGURATION:-Release}"

echo "== Git status =="
git status --short

echo "== .NET info =="
dotnet --version
dotnet --list-sdks

echo "== Restore =="
dotnet restore code/server/FPS.sln

echo "== Build =="
dotnet build code/server/FPS.sln --no-restore --configuration "$CONFIGURATION"

echo "== Test =="
dotnet test code/server/FPS.sln --no-build --configuration "$CONFIGURATION" --verbosity minimal

echo "== Check unwanted tracked build artifacts =="
if git ls-files | grep -E '(^|/)(bin|obj)/' >/dev/null; then
  git ls-files | grep -E '(^|/)(bin|obj)/'
  echo "ERROR: bin/obj folders are tracked in git"
  exit 1
fi

echo "== Check suspicious staged files =="
# Keycloak theme FTL templates use "password" in their standard filenames
# (login-reset-password.ftl, login-update-password.ftl) but contain no secrets.
# Exempt only those specific paths; all other files remain subject to the check.
if git diff --cached --name-only \
    | grep -v 'keycloak/themes/.*\.ftl$' \
    | grep -Ei '(\.env|secret|password|token|private.*key)' >/dev/null; then
  git diff --cached --name-only \
    | grep -v 'keycloak/themes/.*\.ftl$' \
    | grep -Ei '(\.env|secret|password|token|private.*key)'
  echo "ERROR: suspicious file staged"
  exit 1
fi

echo "Validation passed."
