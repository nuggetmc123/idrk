# NULLBOUND VR

A **VR battlegrounds fighting game for Meta Quest**, made with Unity. You throw real punches
with your hands, chain 4-hit combos, block, dash, use four special moves per fighter, and
**Awaken** when your bar fills. You can play solo against bots, online with friends using a room
code, or over the same Wi-Fi.

It plays like a Roblox battlegrounds game, but every fighter, move, map and sound here is
original. Nothing is copied from another game.

## What's in it

| | |
| --- | --- |
| **Menu tabs** | **NULLBOUND** (play), **FIGHTERS**, **CONTROLS**, **VR SETUP**, **SETTINGS**. In a match, the menu button opens **MATCH** (scoreboard, room code, add/remove bots, leave). |
| **Combat** | Real punches: hand speed sets damage. 4-hit combo finisher, uppercut (punch up), downslam (punch down in the air), block (both grips), guard break, dash, evasive (dash while you're stunned), knockback, stun, KO and respawn. |
| **Awakening** | Dealing and taking damage fills the purple bar. Click both sticks to fire your ultimate. For 20 seconds after that you hit harder and your cooldowns are halved. |
| **6 fighters** | KAIZEN (brawler), VOLTEX (cyborg), NYX (assassin), GOLIATH (tank), RIME (ice monk), EMBER (fire dancer). Each has 4 moves and an ultimate. |
| **Multiplayer** | Up to 10 fighters per match. Bots fill the empty slots and leave when real players join. Online play uses Unity Relay room codes. LAN play uses the host's IP address. |
| **VR setup** | A first-launch wizard covers your play area, height calibration (standing or seated), main hand, snap or smooth turning, comfort vignette, your name, and a punching-bag test. |
| **Comfort** | Snap or smooth turn, a vignette while moving, a reduced-knockback option, and seated mode. |
| **Desktop test mode** | With no headset connected, it runs with keyboard and mouse so you can test in the Unity editor. |

## 1. Open it in Unity

1. Install **Unity Hub**, then **Unity 6000.0.84f1** (LTS) with **Android Build Support**
   (tick *OpenJDK* and *Android SDK & NDK Tools*).
2. In Unity Hub: **Add > Add project from disk** and pick this `nullbound-vr` folder.
3. The first open takes a few minutes while packages import. NULLBOUND then **sets itself up
   automatically**: Quest settings, OpenXR, the scene and the player prefab. If that
   doesn't happen, click **Nullbound > Setup Everything** in the top menu bar.
4. Press **Play** to test with keyboard and mouse:
   WASD to move, the mouse to look, left/right click to punch, 1-4 for moves, Shift to dash,
   F to block, G to awaken, Esc for the menu.
   If you have a Quest connected with Link or Air Link, Play runs in VR.

Commit the `.meta` files Unity creates next to the scripts.

## 2. Put it on your Quest (the APK)

### Turn on developer mode (one time)
1. Install the **Meta Horizon** app on your phone and sign in with the account the headset uses.
2. Make a developer organization at <https://developer.oculus.com/manage/organizations/create/>
   (any name).
3. In the phone app: **Devices > your headset > Headset settings > Developer mode > On**.
   Reboot the headset.

### Build the APK
- **In Unity:** **Nullbound > Build Quest APK**. It is saved to `Builds/Android/Nullbound.apk`.
- **Or in GitHub (no Unity on your PC needed):** see [Build the APK in GitHub](#build-the-apk-in-github).

### Install it
- **Meta Quest Developer Hub** (easiest, PC or Mac): plug the Quest in with USB-C, accept
  "Allow USB debugging" inside the headset, then drag `Nullbound.apk` onto the device.
- **SideQuest:** connect the headset and drag the APK into SideQuest.
- **adb:** `adb install -r Nullbound.apk`

On the headset, open **Library > Unknown Sources > Nullbound**.

## 3. Multiplayer

- **PLAY SOLO VS BOTS** works offline. Set the bot count with **BOTS - / +**.
- **LAN HOST / LAN JOIN:** both headsets on the same Wi-Fi. The host's IP shows on their MATCH
  tab and on the wrist. Friends tap LAN JOIN and type it in. No accounts needed.
- **HOST ONLINE MATCH / JOIN WITH CODE:** play from anywhere. The host gets a 6-character room
  code to share. This needs a free Unity Cloud project, set up once:
  1. In Unity: **Edit > Project Settings > Services**, sign in, and **Link** (or create) a
     project.
  2. Go to <https://cloud.unity.com>, open that project, and turn on **Relay** (it's free for
     small games).
  3. Commit the change Unity makes to `ProjectSettings/` (that's where the project ID lives),
     then rebuild the APK.

Everyone in a match needs the same build: the game refuses a mismatched version.

## Build the APK in GitHub

The workflow `.github/workflows/nullbound-apk.yml` builds the APK on every push that changes
`nullbound-vr/`, and also when you click **Actions > Build NULLBOUND Quest APK > Run workflow**.
It needs your Unity login saved as repository secrets
(**Settings > Secrets and variables > Actions > New repository secret**):

| Secret | What to put |
| --- | --- |
| `UNITY_EMAIL` | your Unity account email |
| `UNITY_PASSWORD` | your Unity account password |
| `UNITY_LICENSE` | the full text of your Unity license file (`.ulf`). Activate Unity once on your PC with a free Personal license, then copy the file from `C:\ProgramData\Unity\Unity_lic.ulf` (Windows) or `/Library/Application Support/Unity/Unity_lic.ulf` (Mac). |

When the run finishes, download **Nullbound-Quest-APK** from the run's *Artifacts* section and
install it as described above. Without the secrets, the workflow skips the build and shows a
warning.

## Controls (right-handed; left-handed swaps them in SETTINGS)

| Action | Quest controllers |
| --- | --- |
| Punch | Swing your real fist into an enemy |
| Uppercut / Downslam | Punch upward / punch down while in the air |
| Moves 1-4 | X, Y, left trigger, right trigger |
| Block | Hold both grips |
| Jump / Dash | A / B (dash goes the way your left stick points) |
| Evasive | B while you're being comboed |
| Awaken (ultimate) | Click both thumbsticks when the purple bar is full |
| Move / Turn | Left stick / right stick |
| Menu | Menu button (left controller) |
| Menu clicks | Point the laser, pull the trigger |

## How the code is laid out

Everything is in `Assets/Nullbound/Scripts`. The scene only holds a `NetworkManager` and the
`NBGame` component. The world, VR rig, menus and HUD are built at runtime from primitives, so
there are no art files to lose.

| File | Job |
| --- | --- |
| `NBGame` | Starts everything; switches between the lobby and a match |
| `NBInput` | Reads Quest controllers (or keyboard and mouse) and turns them into game actions |
| `NBRig` / `NBHand` | VR camera, gloves, body, walking, turning, jumping, knockback |
| `NBPlayer` | The networked fighter: health, avatar, pose sync, and all server-side combat and moves |
| `NBLocalFighter` | Your own punch detection, cooldowns and button presses |
| `NBBot` | Bot AI (runs on the host) |
| `NBFighters` | The roster and the shared rules (damage, cooldowns, combo window...) |
| `NBProjectile` | Shots and fireballs |
| `NBNet` | Solo / LAN / online (Relay) hosting and joining, bots |
| `NBMenu` | The tabbed menu, VR setup wizard and on-panel keyboard |
| `NBHud` | Wrist HUD, combo counter, kill feed |
| `NBArena` | Builds the arena, the lobby and the skybox decorations |
| `NBFX` / `NBAudio` / `NBMat` | Effects, synthesized sounds, materials |
| `Editor/NBProjectSetup` | One-click setup and the APK build (menu **Nullbound**) |

**Adding a fighter:** add an entry to `NBFighters.All`. Moves are data (`MoveKind.Projectile`,
`DashStrike`, `Slam`, `Launcher`, `Beam`, `Teleport`, `Buff`, `Meteor`, `Pull`, `Flurry`) with
numbers for damage, range, knockback and so on. The engine already knows how to run every kind.
