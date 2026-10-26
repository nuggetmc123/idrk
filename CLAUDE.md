# Notes for Claude

## KartVR (Unity VR kart game in `KartVR/`)
- **Every time KartVR is changed, send the user a fresh project zip.** Build it with
  `KartVR/Tools/make_project_zip.sh <scratchpad>/KartVR-UnityProject.zip` and send it as a file.
  The user opens it with Unity Hub > Add > Add project from disk (they want a project, not a .unitypackage).
- Unity isn't available in the cloud session. Type-check scripts with `mcs` against API stubs before pushing,
  and say clearly that changes weren't tested in the Unity editor.
- The browser game (`index.html` etc.) is separate; don't touch it for KartVR work.

## Nullbound (Unity VR fighting game in `Nullbound/`)
- Same rules as KartVR: **every time Nullbound is changed, send the user a fresh project zip** built with
  `Nullbound/Tools/make_project_zip.sh <scratchpad>/Nullbound-UnityProject.zip`.
- Type-check before pushing (Roslyn + Unity/Fusion refs, and the Fusion weaver) and say it wasn't tested in Unity.
- KartVR and Nullbound are separate projects; don't mix their code.

## Veylix (Unity VR Formula racing game in `Veylix/`)
- Same rules: **every time Veylix is changed, send the user a fresh project zip** built with
  `Veylix/Tools/make_project_zip.sh <scratchpad>/Veylix.zip` (project files at the zip root), plus `Veylix/Tools/make_unitypackage.py <scratchpad>/Veylix.unitypackage` as the fallback.
- Type-check before pushing (Roslyn + Unity refs) and say it wasn't tested in Unity.
- Its top menu is **Veylix** (Import Quest, Make APK, Import Multiplayer, Build Game World). Keep it separate from KartVR/Nullbound.

## Unity project zips (all projects)
- Unity Hub rejects a folder with only `ProjectSettings/ProjectVersion.txt` ("Please add a valid project").
  Ship the full ProjectSettings set (see `Veylix/ProjectSettings`) and put Assets/Packages/ProjectSettings at the zip root.
