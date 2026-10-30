# Notes for Claude

## Sloppy (Unity VR ragdoll shooter in `FlopzoneVR/`; called Flopzone in code and folders)
- The game is named **Sloppy** in everything players and the user see (signs, popups, the Unity menu, the APK,
  `com.sloppy.game`). Internal names (folders, `namespace Flopzone`, define, prefs keys) stay Flopzone.
- **Every time the game is changed, send the user a fresh project zip** built with
  `FlopzoneVR/Tools/make_project_zip.sh <scratchpad>/Sloppy.zip` (Assets/Packages/ProjectSettings at the zip root).
- Unity Hub rejects a project that only has `ProjectSettings/ProjectVersion.txt`; keep the full ProjectSettings set.
- Its top menu is **Sloppy** with exactly: Set Up Multiplayer, Set Up VR (Quest), Build for Quest (APK) (the user's requested layout, like Nullbound).
- Everything must set itself up when the project opens (`Assets/Flopzone/Editor/FlopzoneSetup.cs`; bump `Version` when setup needs to rerun).
- Unity isn't available in the cloud session. Type-check with Roslyn against Unity reference DLLs and say it wasn't tested in Unity.
- Online play is Photon Fusion 2 (user's choice, like Nullbound). `Scripts/Net/PhotonLink.cs` is behind `#if FLOPZONE_FUSION`,
  which `Assets/Flopzone/Setup/Editor/FusionSwitch.cs` (own asmdef) sets when Fusion.Runtime.dll exists. Only use Fusion APIs that
  Nullbound proved (callbacks list, StartGameArgs, runner.SendReliableDataToPlayer); type-check against a Fusion stub with FLOPZONE_FUSION defined.
- Bans/moderators without a server: `Assets/Flopzone/Resources/moderation.json` (headsets fetch it from the main branch on GitHub).
- Optional account server: `FlopzoneVR/Server` (Cloudflare Worker); test with `npx wrangler dev` + `node test/smoke.mjs`.
