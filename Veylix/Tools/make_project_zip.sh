#!/usr/bin/env bash
# Zip the Veylix Unity project so it can be opened with Unity Hub > Add > Add project from disk.
# Assets/, Packages/ and ProjectSettings/ sit at the top of the zip, so the folder you extract
# into IS the project folder (Unity Hub calls a folder "invalid" if Assets/ is one level deeper).
# Usage: Veylix/Tools/make_project_zip.sh [output.zip]
set -euo pipefail
proj="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$(dirname "$proj")/Veylix.zip}"
case "$out" in /*) ;; *) out="$PWD/$out" ;; esac
rm -f "$out"
cd "$proj"
zip -qr "$out" Assets Packages ProjectSettings README.md .gitignore -x "Library/*" "Temp/*" "Logs/*" "UserSettings/*" "Builds/*"
echo "wrote $out"
