# Kart VR

A first-person VR go-kart game for Unity (Meta Quest / PC VR). You sit in the
kart and drive with the controllers, with no arm-swing movement. Everyone
meets in a **Hub**. From there you can **Race**, play **Bumper Karts**, or
**Free Roam**, either with whoever is online or in a **private lobby** that
your friends join with a 6-digit code.

## What's in the game

| Area | What you do |
|---|---|
| **Hub** | Where everyone spawns and meets. Three portals (Race / Bumper Karts / Free Roam), two lobby-code keypads, kart color buttons, a big board showing your lobby. |
| **Race** | 3-lap race on a ~370 m track with walls, 8 checkpoints (no shortcuts), boost pads, a 5-second countdown, live positions, results, then the next race starts automatically. |
| **Bumper Karts** | 2-minute rounds on a round platform over lava. Ram people for **+1**, fall off for **−1**. Highest score wins. |
| **Free Roam** | A big open map with ramps, a mega ramp, a plateau, hills, slalom poles and a boost strip. |

### Lobby codes / private lobbies
- **Create:** drive up to a keypad in the hub and poke **CREATE LOBBY** (or use the wrist menu). A 6-digit code appears on the keypad, your dashboard and the hub board. Give it to your friends.
- **Join:** type the code on the keypad and poke **JOIN**.
- **In a private lobby**, driving into a portal takes the **whole lobby** to that mode together. The **Hub** button on the wrist menu brings everyone back.
- **In public**, a portal puts you in a public session of that mode with whoever else is playing it.
- **Leave Lobby** (keypad or wrist menu) takes you back to the public hub.

### Controls
| | VR | Keyboard (testing without a headset) |
|---|---|---|
| Gas | Right trigger | W / ↑ |
| Brake / reverse | Left trigger | S / ↓ |
| Steer | Left thumbstick | A / D |
| Drift (hold through a corner; long drifts give a mini-boost) | Either grip | Space |
| Respawn | B | R |
| Recenter view to the seat | X | C |
| Show/hide wrist menu | Y | Tab |
| Press buttons | Touch them with your hand | Left click |
| Look around | Your head | Hold right mouse |

The wrist menu floats above your left controller. Poke it with your right hand:
**Recenter, Respawn, Hub, Create/Leave Lobby**, plus your lobby info.

---

## Setup (about 20 minutes, one time)

### 1. Open the project
This `KartVR` folder **is** a Unity project.
1. Install **Unity Hub** and **Unity 6 LTS** (any 6000.0 or newer version). Tick **Android Build Support** if you want to play on Quest.
2. Unity Hub → **Add** → **Add project from disk** → pick this **`KartVR`** folder. If Hub says the editor version isn't installed, choose the Unity 6 version you have.
3. The first open takes a few minutes while Unity downloads packages (Input System, XR Plugin Management, OpenXR are already listed in `Packages/manifest.json`).
4. Unity will say there are **compile errors** and offer **Safe Mode**. Click **Ignore**. The errors are only because Photon Fusion isn't imported yet (step 4).
5. If asked to **enable the new Input System backends**, click **Yes** (Unity restarts).

### 2. Turn on VR
**Edit → Project Settings → XR Plug-in Management** (click *Install* if it asks):
- **Android tab:** tick **OpenXR**. Under OpenXR, add the **Oculus Touch Controller Profile** (and the **Meta Quest Touch Plus/Pro** profiles if listed) and enable the **Meta Quest Support** feature.
- **Windows tab** (for Quest Link / SteamVR testing): tick **OpenXR** and add the same controller profiles.
- Fix anything the yellow ⚠ "Project Validation" list shows.

### 3. TextMeshPro
**Window → TextMeshPro → Import TMP Essential Resources.**

### 4. Photon Fusion 2 (the multiplayer)
1. Make a free account at **dashboard.photonengine.com**, click **Create a new app**, pick **Fusion**, version **Fusion 2**. Copy the **App ID**.
2. Import **Photon Fusion 2** from the Unity Asset Store (free) or from photonengine.com → SDKs.
3. When the **Fusion Hub** window opens (or **Tools → Fusion → Fusion Hub**), paste your App ID.

The free Photon plan allows 20 players online at once, which is plenty for playing with friends.

### 5. Build the world
**Menu bar → KartVR → Build Prototype World.** This generates:
- `Assets/KartVR/Prefabs/Kart.prefab`, the networked kart
- `Assets/KartVR/Scenes/KartWorld.unity`, with the hub, race track, bumper arena, free-roam map, VR rig and managers. It's added to Build Settings for you.

Then select the **GameState** object in the scene and, on its **NetworkObject**, tick
**Is Master Client Object** if you see it. That way, if the player who started a
session leaves, the session's game state moves to someone else. (The code also has a fallback.)
If Fusion complains about the Kart prefab not being registered, run **Tools → Fusion → Rebuild Prefab Table**.

### 6. Play
- **In the editor:** press Play. You'll connect to the public hub. With no headset you get keyboard and mouse controls.
- **Test multiplayer on one PC:** **Window → Multiplayer → Multiplayer Play Mode** (Unity 6), turn on a second player, and press Play. Or make a Windows build and run it next to the editor.
- **On Quest:** turn on Developer Mode, plug in the headset, then **File → Build Profiles → Android → Switch Platform**. In Player Settings use **IL2CPP + ARM64** and press **Build And Run**.

---

## How it works (for when you want to change things)

```
Scripts/
  Core/         KartMode (Hub/Race/Bumper/FreeRoam), PlayerProfile (name + color), KartUI helpers
  Network/      SessionManager: connects, matchmaking, private codes, switching sessions
                GameState: which mode this session is in (owned by the master client)
  Kart/         KartController: driving physics, network sync, laps, bumping
                KartAvatar: your head/hands/name/color as others see them
                KartDashboard: speed/lap/position/countdown on your dashboard
                KartInput: all controls (VR + keyboard + gamepad)
  Player/       KartRig: the VR camera + hands, sits in your kart's seat; HandPoker
  Interaction/  PokeButton (touch-to-press 3D buttons), KeypadTerminal, WristMenu
  World/        SpawnArea, Checkpoint, BoostPad, ModePortal, InfoBoard
  Modes/        RaceManager (countdown → race → results loop), BumperManager (rounds + scores)
Editor/         KartWorldBuilder: the "Build Prototype World" menu
```

- **Networking:** Photon Fusion 2 in **Shared Mode**. Each player owns and simulates their own kart, so your own driving never lags. Other karts smoothly follow their owner's updates. When you ram someone, an RPC tells *their* game to knock their kart. A session stays alive as long as anyone is in it.
- **Public vs private:** public sessions are matched by a `mode` property, so a public race session only matches other public race sessions. A private lobby is a hidden session named after its code. Inside one, modes are switched by changing `GameState.Mode`, and every kart teleports to that mode's spawn area.
- **All maps live in one scene**, spread far apart. Switching session reloads the scene for a clean start.
- **Tuning:** select the Kart prefab and look at `KartController`. There's a separate *Tuning* block per mode (top speed, acceleration, turn rate, grip, drift grip, bump power). Laps and round lengths are on the `GameState` object (`RaceManager` / `BumperManager`).
- **Adding your own maps or art:** replace the generated blocks with your models. The things that matter are a `SpawnArea` (with child slots) for each mode, `Checkpoint`s in order with index 0 at the finish line, and `ModePortal`s in the hub. Re-running the builder overwrites the scene and prefab, so after you start customising, stop using it (or copy the scene first).

## Known limits / ideas for next steps
- This is a prototype with block art. Swap in real kart and track models.
- Your name is a random "Racer1234" saved on the device. A VR keyboard for renaming would be a nice addition.
- No anti-cheat: each player reports their own laps and scores. Fine for friends.
- Possible additions: a physical steering wheel you grab, items/power-ups, more tracks, lap-time leaderboards, voice chat (Photon Voice).
- Comfort: the kart is auto-leveled to reduce rolling. If anyone gets motion sick, a tunneling vignette is a good add.

## Note on versions
The code targets **Unity 6** (`Rigidbody.linearVelocity`, `PhysicsMaterial`) and **Photon Fusion 2**. It
has been checked for C# errors, but not inside the Unity editor itself. If a Fusion
update renames something, the Console error will point at the exact line. The usual
places are the `INetworkRunnerCallbacks` method list in `SessionManager.cs` and the
`StartGameArgs` fields.
