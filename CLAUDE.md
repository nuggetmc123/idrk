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
