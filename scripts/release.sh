#!/usr/bin/env bash
set -euo pipefail

if [[ "${OS:-}" != "Windows_NT" ]]; then
    echo "MiniTranslator releases require Windows. Use Git Bash or MSYS." >&2
    exit 1
fi

cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
exec powershell.exe -NoProfile -File scripts/release.ps1
