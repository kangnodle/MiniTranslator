#!/usr/bin/env bash
set -euo pipefail

if [[ "${OS:-}" != "Windows_NT" ]]; then
    echo "MiniTranslator requires Windows. Run this script in Git Bash or MSYS." >&2
    exit 1
fi

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."

echo "Restoring dependencies..."
dotnet restore MiniTranslator.slnx

echo "Formatting..."
dotnet format MiniTranslator.slnx --no-restore

echo "Linting..."
dotnet format MiniTranslator.slnx --no-restore --verify-no-changes

echo "Building..."
dotnet build MiniTranslator.slnx --no-restore

echo "Testing..."
dotnet test MiniTranslator.slnx --no-build --no-restore

echo "Running MiniTranslator (exit through the tray menu)..."
dotnet run --project apps/MiniTranslator.Wpf --no-build --no-restore -- "$@"
