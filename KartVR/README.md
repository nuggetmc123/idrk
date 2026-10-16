# Kart VR

A first-person VR go-kart game for Unity (Meta Quest / PC VR). Everyone meets
in a **Hub**, walking around with full bodies (thumbstick movement, no arm-swing
movement). Every player has a **garage** where their customized kart is on display. From the hub
you can **Race**, play **Bumper Karts**, or **Free Roam**, sitting in your kart and driving
with the controllers, either with whoever is online or in a **private lobby** that your
friends join with a 6-digit code.

## What's in the game

| Area | What you do |
|---|---|
| **Hub** | You **walk around on foot** with a full body (helmet, racing suit, arms that follow your real hands, legs that step). There's a plaza with a fountain and a spinning gold trophy, lamps, trees and benches. Three glowing portals (Race / Bumper Karts / Free Roam), two lobby-code kiosks, a big lobby board, and **12 garages**. |
| **Garages** | Every player automatically gets their own garage bay. **Your kart is parked in it on a turntable whenever you're in the hub, so everyone walking by can see it.** The sign above the door shows the owner's name and the neon trim glows in their paint color. Walk into **YOUR GARAGE** and use the wall panel to change your kart. Only the owner can use a garage's panel. |
| **Race** | 3-lap race on a ~370 m track with red/white barriers, painted lines, a start gantry with **countdown lights**, a grandstand full of fans, a "KART VR" arch, boost pads with chevrons, **a jump**, and trees. Live positions, results, then the next race starts automatically. |
| **Bumper Karts** | 2-minute rounds on a neon-lit platform over a lava lake. A **spinning sweeper bar** shoves anyone in its way, and **bouncy bumpers** fling you across the arena. Ram people for **+1**, fall off for **−1**. |
| **Free Roam** | A stunt park: a mega ramp with **boost hoops** to fly through, a **skate bowl** of quarter pipes, a kicker chain, a plateau with a drop-off jump, hills, a lake, rocks, a ring road and lots of trees. |

### Customizing your kart (in your garage)
| Part | Options |
|---|---|
| **Paint** and **Accent** | 12 colors each |
| **Pattern** | Solid, Racing Stripes, Two-Tone, Checkered, Flames, Camo |
| **Booster** | None, Twin Rockets, Jet Engine, Mega Rockets. Boosters give you a **boost button (A)** with a recharge meter on your dashboard, and shoot flames when used. Each one is a trade-off: Twin Rockets are balanced, the Jet lasts longest, Mega Rockets are the fastest but recharge slowly. |
| **Wheels** | Standard, Chunky Off-Road (with tread), Racing Slicks (colored rims) |
| **Spoiler** | None, Lip, Wing, Big Wing |

Your choices are saved on your device and everyone else sees them, in your garage and on the track.

### Lobby codes / private lobbies
- **Create:** walk up to a lobby-code kiosk in the hub and poke **CREATE LOBBY** (or use the wrist menu). A 6-digit code appears on the kiosk, your wrist menu and the hub board. Give it to your friends.
- **Join:** type the code on the kiosk and poke **JOIN**.
- **In a private lobby**, walking into a portal takes the **whole lobby** to that mode together. The **Hub** button on the wrist menu brings everyone back.
- **In public**, a portal puts you in a public session of that mode with whoever else is playing it.
- **Leave Lobby** (kiosk or wrist menu) takes you back to the public hub.

### Controls
| | VR | Keyboard (testing without a headset) |
|---|---|---|
| **Walk** (hub) | Left thumbstick (the way you're looking); hold **right trigger** to run | W/A/S/D, hold Shift to run |
| **Turn** (hub) | Right thumbstick (snap turn) | Q / E |
| Gas | Right trigger | W / ↑ |
| Brake / reverse | Left trigger | S / ↓ |
| **Steer** | **Grab the steering wheel** (grip) with one or both hands and turn it, or use the left thumbstick | A / D |
| Drift (hold through a corner; long drifts give a mini-boost) | X | Space |
| **Booster** (if your kart has one) | A | Left Shift |
| Respawn | B | R |
| Recenter view | Click the left thumbstick | C |
| Show/hide wrist menu | Y | Tab |
| Press buttons | Touch them with your hand | Left click |
| Look around | Your head | Hold right mouse |

**The steering wheel:** in VR, put your hand on the wheel and squeeze **grip** to hold it, then turn it like a real
wheel. One hand or two both work. Let go and it springs back to the middle. Not holding it? The left thumbstick turns
the wheel smoothly for you.

The wrist menu floats above your left controller. Poke it with your right hand:
**Recenter, Respawn, Hub, Create/Leave Lobby**, plus your lobby info.

---

## Setup

### Play solo first (about 5 minutes, almost no clicks)
1. Install **Unity Hub** and **Unity 6** (any 6000.x version). Tick **Android Build Support** if you want to play on Quest later.
2. Unity Hub → **Add** → **Add project from disk** → pick this **`KartVR`** folder. If it says the editor version
   isn't installed, pick the Unity 6 version you have.
3. If Unity asks to **enable the new Input System**, click **Yes** (Unity restarts).
4. **Wait.** The first time it opens, the project sets itself up: it adds the text resources, builds the whole
   game world and opens it. A "Kart VR is ready!" message pops up when it's done.
5. Press **▶ Play**. You can walk around the hub, customize your kart in **YOUR GARAGE**, and race, bump and free roam, **solo**.

(You can rebuild the world any time with **KartVR → Build Prototype World**.)

Without Photon Fusion the game runs **offline**: everything works except playing with other people.
**KartVR → Check Multiplayer** tells you which mode you're in.

### Turn on multiplayer (optional, 3 steps)
Open **KartVR → Set Up Multiplayer**. It has buttons for each step:
1. **Open Photon website** → make a free account → **Create a new app** → **Fusion** → **Fusion 2** → copy the **App ID**.
2. **Paste the App ID** into the window.
3. **Find Photon Fusion 2 in the Asset Store** → **Add to My Assets** → **Open in Unity** → **Import**.

That's it. When the import finishes, the project switches multiplayer on, fills in your App ID and rebuilds the
world with real networking by itself, then tells you it's ready. (Photon can't be included in this download: it's
licensed to you through your own free account.)

The free Photon plan allows 20 players online at once, which is plenty for playing with friends.

### Turn on VR (optional, one click)
Click **KartVR → Set Up VR (Quest)**. It installs Unity's VR support (OpenXR), switches it on for PC (Quest Link)
and for the Quest headset, and adds the Quest controllers. Unity reloads once in the middle; say **Yes** if it asks to
restart. A message lists what was done (and anything it couldn't do, with the exact click). Every time you press
**▶ Play** it also re-checks that the Quest controller profiles are on (without them your hands don't move).

Doing it by hand instead: **Edit → Project Settings → XR Plug-in Management** (click *Install XR Plugin Management* if
that's all you see). The platform tabs are **icons**: the **monitor** icon is PC, the **Android robot** is Quest. On each,
tick **OpenXR**, then in **OpenXR** (left list) add the **Oculus Touch Controller Profile** with **+**; on the Android tab
also tick **Meta Quest Support**.

**Play in VR on your PC:** install the Meta Quest Link app, connect the Quest (cable or Air Link), set Link as the
OpenXR runtime (Link app → Settings → General), then press **▶ Play** in Unity.

### Test with friends on one PC
**Window → Multiplayer → Multiplayer Play Mode** (Unity 6), turn on a second player, and press Play
(multiplayer needs Photon Fusion). Or make a Windows build and run it next to the editor.

### Play on Quest
Turn on Developer Mode and plug in the headset, then **File → Build Profiles → Android → Switch Platform**.
In Player Settings use **IL2CPP + ARM64**, then **Build And Run**.

---

## How it works (for when you want to change things)

```
Scripts/
  Core/         KartMode (Hub/Race/Bumper/FreeRoam), PlayerProfile (name + color), KartUI helpers
  Network/      SessionManager: connects, matchmaking, private codes, switching sessions
                GameState: which mode this session is in (owned by the master client)
                OfflineFusion/: a tiny stand-in for Photon Fusion so the game runs solo before Fusion is
                imported (deleted automatically once Fusion is in the project, restored if it is removed)
Setup/Editor/   FusionSwitch: detects Photon Fusion, removes/restores the stand-in, spots duplicate script copies
  Core/         KartLook: paint/pattern/booster/wheels/spoiler (packed into one networked int)
  Kart/         KartController: driving physics, booster, garage parking, network sync, laps, bumping
                KartVisuals: builds the detailed kart model from its KartLook (incl. booster flames)
                KartAvatar: the driver's body sitting in the kart
                KartDashboard: speed/boost meter/lap/position/countdown on your dashboard
                KartInput: all controls (VR + keyboard + gamepad)
  Player/       PlayerAvatar: you on foot in the hub (walk, snap turn, synced body)
                AvatarBody: the racer body: Models/Racer.fbx parts on IK joints (or simple shapes as a fallback)
                KartRig: the VR camera + hands; rides your avatar in the hub, your kart elsewhere
  Interaction/  PokeButton (touch-to-press 3D buttons), KeypadTerminal, WristMenu, GarageEditor
  World/        Garage, SpawnArea, Checkpoint, BoostPad, ModePortal, InfoBoard,
                Rotator (clock-synced spinning), Bouncer, StartLights
  Modes/        RaceManager (countdown → race → results loop), BumperManager (rounds + scores)
Editor/         KartWorldBuilder: the "Build Prototype World" menu
```

- **Networking:** Photon Fusion 2 in **Shared Mode**. Each player owns and simulates their own kart, so your own driving never lags. Other karts smoothly follow their owner's updates. When you ram someone, an RPC tells *their* game to knock their kart. A session stays alive as long as anyone is in it.
- **Public vs private:** public sessions are matched by a `mode` property, so a public race session only matches other public race sessions. A private lobby is a hidden session named after its code. Inside one, modes are switched by changing `GameState.Mode`, and every kart teleports to that mode's spawn area.
- **All maps live in one scene**, spread far apart. Switching session reloads the scene for a clean start.
- **Each player spawns two network objects:** a `Player` (on foot) and a `Kart`. In the hub the kart parks itself in the player's garage and the VR rig rides the avatar; in other modes the avatar hides and the rig sits in the kart.
- **Garages** are claimed automatically: each player takes the lowest free bay, and if two pick the same one, the lower player id keeps it.
- **Performance:** scenery is marked static so Unity batches it, karts and bodies share materials, and there are no real-time point lights (glowing parts use emissive materials). That keeps it light enough for Quest.
- **Tuning:** select the Kart prefab and look at `KartController`. There's a separate *Tuning* block per mode (top speed, acceleration, turn rate, grip, drift grip, bump power). Laps and round lengths are on the `GameState` object (`RaceManager` / `BumperManager`).
- **Adding your own maps or art:** replace the generated blocks with your models. The things that matter are a `SpawnArea` (with child slots) for each mode, `Checkpoint`s in order with index 0 at the finish line, and `ModePortal`s in the hub. Re-running the builder overwrites the scene and prefab, so after you start customising, stop using it (or copy the scene first).

## The player model (`Assets/KartVR/Models/Racer.fbx`)
Every player's body uses this model, walking in the hub and sitting in karts.
- It's made of **separate rigid parts** (no skeleton needed). The game attaches each part to a joint and
  moves the joints with simple IK, so arms follow your real controllers and legs step when you walk.
- Joints are **measured from the parts themselves** (e.g. the elbow is where `UpperArm_L` and `Forearm_L`
  overlap), so you can re-export it with different proportions and it still lines up.
- **Colors:** orange parts become the player's **paint** color, white parts their **accent** color, and dark
  parts stay dark. The visor stays glossy black.
- On your own body the helmet is hidden so it doesn't block your view, but you can look down and see your suit and gloves.

To use a different model, export it from Blender as FBX with parts named like these:
`Helmet, Visor, HelmetStripe, ChinBar, Neck, Torso, SuitStripe, Hips, Belt,
Shoulder_L/R, UpperArm_L/R, Forearm_L/R, GloveCuff_L/R, Glove_L/R, Thigh_L/R, Shin_L/R, Boot_L/R`
(`Helmet`, `Torso`, `Hips`, `Neck` and the `_L/_R` arm and leg parts are required; extras follow the torso).
Model it standing, facing -Y in Blender (the default front), feet at the origin. Replace `Racer.fbx` and run
**KartVR > Build Prototype World** again. If the model is missing or a required part isn't found, players fall back
to a simple body made of shapes.

## Known limits / ideas for next steps
- This is a prototype with block art. Swap in real kart and track models.
- Your name is a random "Racer1234" saved on the device. A VR keyboard for renaming would be a nice addition.
- More than 12 players in one session: extra karts park out of sight (there are 12 garages).
- No anti-cheat: each player reports their own laps and scores. Fine for friends.
- Possible additions: a physical steering wheel you grab, items/power-ups, more tracks, lap-time leaderboards, voice chat (Photon Voice), more kart parts and stickers.
- Comfort: the kart is auto-leveled to reduce rolling. If anyone gets motion sick, a tunneling vignette is a good add.

## Note on versions
The code targets **Unity 6** (`Rigidbody.linearVelocity`, `PhysicsMaterial`) and **Photon Fusion 2**. It
has been checked for C# errors, but not inside the Unity editor itself. If a Fusion
update renames something, the Console error will point at the exact line. The usual
places are the `INetworkRunnerCallbacks` method list in `SessionManager.cs` and the
`StartGameArgs` fields.
