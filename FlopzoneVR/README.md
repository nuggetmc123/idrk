# Flopzone

A VR ragdoll shooter for Meta Quest, made in Unity. Grab guns off your hips, reload by
pulling mags from your waist, and send other players flopping across 10 maps in Team
Deathmatch, Free For All, 1v1, 2v2, 3v3 and Capture the Flag. Meet in the hub, party up
with friends, and go.

## Open it

1. Install **Unity 6 (6000.0 LTS)** from Unity Hub, with the **Android Build Support** module
   (tick *OpenJDK* and *Android SDK & NDK Tools* too). Unity 2022.3 LTS should also work.
2. Unity Hub → **Add** → **Add project from disk** → choose this `FlopzoneVR` folder.
3. Open it. **Everything sets itself up** the first time (and again after updates):
   - makes the game scene and its assets, and adds the scene to the build
   - sets the Quest/Android player settings (IL2CPP, ARM64, Android 12L+, internet permission)
   - imports the VR packages (XR Plug-in Management + OpenXR) and turns on OpenXR with
     Meta Quest support and the Touch controller profile
   - sets *Active Input Handling* to *Both* (Unity asks to restart once for that)
   - opens the **Flopzone** tab

If Unity ever shows the setup didn't finish (for example it was offline while importing),
press **Run setup again** on the Flopzone tab.

## The Flopzone tab

Menu bar → **Flopzone → Open Flopzone Tab** (or Window → Flopzone).

| Button | What it does |
| --- | --- |
| **Import Multiplayer** | Multiplayer is built into the game. This saves your server URL and checks the server is reachable. With no URL the game plays offline with bots. See [`Server/README.md`](Server/README.md) to deploy the free server. |
| **Import VR Support** | Installs XR Plug-in Management + OpenXR and switches on Quest support (Android) and PC VR (so Quest Link works in Play mode). |
| **Make APK** | Switches to Android and builds `Builds/Flopzone.apk`. |
| **Make APK and run it on my Quest** | Same, then installs and launches it over USB (developer mode on). |

The tab also shows a status checklist, and has a **Moderation** section for kicking,
banning and muting players by player ID from your computer.

## Playing

**First launch:** a "Welcome to Flopzone — Loading to hub..." popup, then the tutorial:
pick a name (must be unique; slurs and swearing are refused), then learn to grab, shoot,
drop the mag, reload, jump, sprint, crouch and open the menu. After that, the hub.

**Hub:** walk into one of the six glowing portals to start a match, try all 22 guns on
the gun table, and shoot targets on the range.

### Controls (Quest)

| | |
| --- | --- |
| Move | Left stick |
| Sprint | Click the left stick |
| Turn | Right stick (snap or smooth) |
| Crouch | Click the right stick |
| Jump | A |
| Grab a gun | Reach to your hip, hold Grip (left hip: primary, right hip: sidearm) |
| Shoot | Trigger |
| Drop the mag | B, or grab the mag out of the gun with your other hand |
| Reload | Put your empty hand at your waist and **hold Trigger** to pull a mag, push it into the gun |
| Menu | ≡ button on the left controller |

Jump, Unload Mag, Sprint and Crouch can be rebound in **Menu → Controls**, with
**Reset to default**. Volume (master, music, sound effects), turning, and toggle/hold
options are in **Menu → Settings**.

**Keyboard testing in the editor** (no headset): WASD move, click to look, left-click
shoot, E / Q grab right / left, R reload, F drop mag, Space jump, Shift sprint, C crouch,
Tab menu.

### The menu (≡)

Play · Loadout (pick your primary and sidearm) · Friends (add by name or player ID,
invite to party) · Party (up to 6, leader takes everyone) · Chat (keyboard + quick chat) ·
**Report** (pick a player and a reason) · Settings · Controls · Profile (your name and
**player ID**) · **MOD** (moderators only).

### Game modes

| Mode | Players | Win |
| --- | --- | --- |
| Team Deathmatch | 5 v 5 | first team to 40 kills |
| Free For All | 8 | first to 20 kills |
| 1v1 | 1 v 1 | first to 10 |
| 2v2 | 2 v 2 | first to 15 |
| 3v3 | 3 v 3 | first to 20 |
| Capture the Flag | 5 v 5 | 3 captures |

Parties stay on one team. A full party of 6 that picks 3v3 is split 3 and 3.
Offline, empty seats are filled with bots.

### Maps

Warehouse · Rooftops · Dune Outpost · Frostbite · Jungle Ruins · Neon City · Shipyard ·
Foundry · Castle Keep · Moon Base (low gravity) — plus the Hub and the Training Range.

### Guns (22)

- **Pistols:** Flop 9, Duck Deagle, Buzz Mini, Revolt Revolver, Squeak .22, Twin Hammer
- **Shotguns:** Boomstick, Sawed Slam, Auto Thunder, Drum Brute, Slug Lord
- **Snipers** (working scopes): Longshot, Whisper, Elephant Gun, Marksman DMR, Needle
- **Assault rifles:** Rattle AR, Bulldog, Viper Carbine, Burst Bravo, Heavy HMR, Spitfire

Every gun has muzzle flash, tracer, shell ejection, recoil, haptics, its own sound and an
ammo counter. Hits spray blood and leave splats; kills turn players into physics
ragdolls you can keep shooting around.

## Moderation

- **Auto-mod:** in chat, "fuck", "shit" and "damn" are fine; slurs and other bad words
  mute the sender for 15 minutes. Names allow no swearing at all.
- **Reports:** Menu → Report → player → reason. Moderators see them with the player's recent chat.
- **Player IDs:** every player has one (Menu → Profile). Moderators can kick, mute, ban,
  unban and reset names by ID from the in-game MOD tab, the Flopzone tab, or a browser.
  How to become a moderator: [`Server/README.md`](Server/README.md#make-yourself-a-moderator).

## How it's built

There are no imported art or sound files: every model (guns, players, maps) is built
from shapes in code, and every sound effect and both music tracks are synthesised when
the game starts. The only packages are Unity's own XR Plug-in Management and OpenXR.

```
Assets/Flopzone/
  Editor/      auto-setup on load, the Flopzone tab, APK builds
  Scripts/
    Core/      JSON, word filter, settings, materials, sound synthesis, audio
    Input/     headset + controllers (Unity XR input), keyboard fallback
    Player/    rig + movement, hands, avatars, ragdolls, other players
    Weapons/   the 22 guns, gun models, mags, effects
    World/     the 10 maps, the hub, portals, targets
    Net/       server connection, session state, offline server with bots
    Game/      game flow, tutorial, bots, capture the flag
    UI/        VR UI kit, keyboard, menu, HUD
Server/        the Cloudflare Worker (see its README)
```
