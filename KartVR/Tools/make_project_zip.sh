#!/usr/bin/env bash
# Zip the KartVR Unity project (Assets, Packages, ProjectSettings, README) so it can be
# opened with Unity Hub > Add > Add project from disk.
# Usage: KartVR/Tools/make_project_zip.sh [output.zip]
set -euo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$repo/KartVR-UnityProject.zip}"
rm -f "$out"
cd "$repo"
zip -qr "$out" KartVR -x "KartVR/Tools/*" "KartVR/Library/*" "KartVR/Temp/*" "KartVR/Logs/*" "KartVR/UserSettings/*" "KartVR/Builds/*"
echo "wrote $out"
