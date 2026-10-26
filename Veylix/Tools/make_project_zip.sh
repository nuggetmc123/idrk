#!/usr/bin/env bash
# Zip the Veylix Unity project (Assets, Packages, ProjectSettings, README) so it can be
# opened with Unity Hub > Add > Add project from disk.
# Usage: Veylix/Tools/make_project_zip.sh [output.zip]
set -euo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$repo/Veylix-UnityProject.zip}"
rm -f "$out"
cd "$repo"
zip -qr "$out" Veylix -x "Veylix/Tools/*" "Veylix/Library/*" "Veylix/Temp/*" "Veylix/Logs/*" "Veylix/UserSettings/*" "Veylix/Builds/*"
echo "wrote $out"
