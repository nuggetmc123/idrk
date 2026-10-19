#!/usr/bin/env bash
# Zip the Nullbound Unity project (Assets, Packages, ProjectSettings, README) so it can be
# opened with Unity Hub > Add > Add project from disk.
# Usage: Nullbound/Tools/make_project_zip.sh [output.zip]
set -euo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$repo/Nullbound-UnityProject.zip}"
rm -f "$out"
cd "$repo"
zip -qr "$out" Nullbound -x "Nullbound/Tools/*" "Nullbound/Library/*" "Nullbound/Temp/*" "Nullbound/Logs/*" "Nullbound/UserSettings/*" "Nullbound/Builds/*"
echo "wrote $out"
