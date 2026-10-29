# Notes for Claude

## Flopzone (Unity VR ragdoll shooter in `FlopzoneVR/`)
- **Every time Flopzone is changed, send the user a fresh project zip** built with
  `FlopzoneVR/Tools/make_project_zip.sh <scratchpad>/Flopzone.zip` (Assets/Packages/ProjectSettings at the zip root).
- Unity Hub rejects a project that only has `ProjectSettings/ProjectVersion.txt`; keep the full ProjectSettings set.
- Its top menu is **Flopzone** with exactly: Set Up Multiplayer, Set Up VR (Quest), Build for Quest (APK) (the user's requested layout, like Nullbound).
- Everything must set itself up when the project opens (`Assets/Flopzone/Editor/FlopzoneSetup.cs`; bump `Version` when setup needs to rerun).
- Unity isn't available in the cloud session. Type-check with Roslyn against Unity reference DLLs and say it wasn't tested in Unity.
- Server: `FlopzoneVR/Server` (Cloudflare Worker); test with `npx wrangler dev` + `node test/smoke.mjs`.
