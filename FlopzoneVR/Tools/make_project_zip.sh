#!/usr/bin/env bash
# Zip the Sloppy Unity project with Assets/Packages/ProjectSettings at the zip root,
# so "Extract All" + Unity Hub > Add > Add project from disk just works.
# Usage: FlopzoneVR/Tools/make_project_zip.sh [output.zip]
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
out="$(realpath -m "${1:-$here/../Sloppy.zip}")"
rm -f "$out"
cd "$here"
git ls-files . | grep -v '^Tools/' | python3 -c "
import sys, zipfile
z = zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED)
for d in ('Assets/', 'Packages/', 'ProjectSettings/'): z.writestr(d, '')
for f in sys.stdin.read().split(): z.write(f, f)
z.writestr('HOW TO OPEN.txt', '1. Right-click the zip > Extract All.\n2. Unity Hub > Add > Add project from disk > pick the extracted folder (it has Assets, Packages and ProjectSettings inside).\n3. Open with Unity 6 LTS (with Android Build Support). Everything sets itself up.\n')
z.close()" "$out"
echo "wrote $out"
