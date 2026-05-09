#!/bin/sh
set -eu

# Ensure .NET 10 (user install) is on PATH
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

echo "== Git status =="
git status --short

echo "== .NET info =="
dotnet --version
dotnet --list-sdks

echo "== Restore =="
dotnet restore code/server/FPS.sln

echo "== Build =="
dotnet build code/server/FPS.sln --no-restore

echo "== Test =="
dotnet test code/server/FPS.sln --no-build

echo "== Check unwanted tracked build artifacts =="
if git ls-files | grep -E '(^|/)(bin|obj)/' >/dev/null; then
  git ls-files | grep -E '(^|/)(bin|obj)/'
  echo "ERROR: bin/obj folders are tracked in git"
  exit 1
fi

echo "== Check suspicious staged files =="
if git diff --cached --name-only | grep -Ei '(\.env|secret|password|token|private.*key)' >/dev/null; then
  git diff --cached --name-only | grep -Ei '(\.env|secret|password|token|private.*key)'
  echo "ERROR: suspicious file staged"
  exit 1
fi

echo "Validation passed."
