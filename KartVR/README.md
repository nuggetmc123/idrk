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
| **Hub** | One **big indoor garage** where you **walk around on foot** with a full body (helmet, racing suit, arms that follow your real hands, legs that step). It has **6 garage bays** (one for each player on the server), a showroom trophy in the middle, a big **MAP BOARD** on the wall (touch Race, Canyon Race, Bumper Karts, Ice Bumper, Tag, Free Roam or Tutorial to go there), the **SERVER BOARD**, a lobby-code kiosk and the lobby board. |
| **Garages** | Every player automatically gets their own garage bay. **Your kart is parked in it on a turntable whenever you're in the hub, so everyone walking by can see it.** The sign above the door shows the owner's name and the neon trim glows in their paint color. Walk into **YOUR GARAGE** and use the wall panel to change your kart. Only the owner can use a garage's panel. |
| **Race** | 3-lap race on a big ~630 m track with red/white barriers, painted lines, a start gantry with **countdown lights**, a grandstand full of fans, a "KART VR" arch, boost pads with chevrons, **a jump**, and trees. Live positions, results, then the next race starts automatically. |
| **Bumper Karts** | 2-minute rounds on a neon-lit platform over a lava lake. A **spinning sweeper bar** shoves anyone in its way, and **bouncy bumpers** fling you across the arena. Ram people for **+1**, fall off for **−1**. |
| **Canyon Race** | A second race track (~780 m) through a red-rock desert canyon: tall striped cliff walls, mesas, boulders and cacti, plus boost pads, a jump and a grandstand. Same 3-lap race rules. |
| **Ice Bumper** | Bumper karts on a **slippery sheet of ice** over freezing water: two crossed sweeper bars, **snowman bumpers**, icebergs and snowy pine islands under the northern lights. |
| **Free Roam** | **One big open world** (about 900 m across): roads crossing the middle plus two ring roads, a **stunt park** (mega ramp, boost hoops, skate bowl, kickers), a **jump strip** of ever-bigger ramps and a plateau drop-off, a **lake** with an island and hills, a **forest**, rocks and big hills round the edge. Plus **build mode** (below). |
| **Tag** | Tag in karts in a neon night arena with pillars, broken walls to hide behind, a raised centre platform with ramps, bouncy bumpers and boost pads. One player is **IT** (a red ring and "IT!" float over them). **Bump someone to make them IT**; the new IT can't tag anyone for 3 seconds (no tag-backs). You get a point for every second you're not IT; highest score after 2.5 minutes wins. IT drives a little faster. |
| **Sky domes** | Every map has its own painted sky dome (day, sunny mountains, lava night, desert, northern lights, sunset), so you never see the other maps. Just inside the dome is a **solid invisible wall** nobody can drive, walk or jump past, and past that the barrier puts you back as a backup. |

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
- **In a private lobby**, touching a map on the map board takes the **whole lobby** there together. The **Hub** button on the wrist menu brings everyone back.
- **In public**, the map board takes you to that map on **your server**, with the other people on it.

### Servers
Each server holds **6 players** (one per garage). You start on **Server 1**; if it's full you're put on Server 2, then 3,
and so on. Use the **SERVER BOARD** in the garage to hop to another server (e.g. to meet a friend: both pick the same
number). People on the same hub server go to the same map server when they use the map board.
- **Leave Lobby** (kiosk or wrist menu) takes you back to the public hub.

### Tutorial (first time you play)
The first time the game starts on a headset you're put in **Tutorial Park**, its own small map, solo, instead of
the hub: a ring road with numbered station signs (drive, steer, brake, drift, boost pad, jump, respawn, build), cones,
and a **BUILD ZONE** in the middle. You can go back any time with the **TUTORIAL** button on the hub's map wall. A panel
in front of you walks through driving (gas, steering, brake/reverse, drift, boost, top speed, respawn) and building
(enter build mode, fly, pick, place, delete, back to the kart). Each step ticks off when you do it, or point at
**Skip step** and pull the trigger. At the end press **PLAY!** and you go to the hub; after that the game always starts
there. Menu > Controls > **Play the tutorial again** replays it.

### Names, moderators and anti-cheat
**Your name.** The first time you play, a keyboard pops up: pick your name (3-16 letters, numbers or _). It's
**locked** after that. Rude names (also spelled with numbers), "admin"/"staff"-style names, moderators' names and banned
names are refused. If a bad one gets through, a moderator can **force a rename** or ban the player.

**Your Player ID.** Menu > Menu shows your name and a Player ID like `3F9A2-C01B7`. It comes from a secret key made
on your headset, so nobody can copy it to pretend to be you.

**Making someone a moderator / banning for good.** Edit `Assets/KartVR/Resources/moderation.json`, **raise
`"version"` by 1**, and push it to GitHub (`nuggetmc123/idrk`). Every headset downloads the newest list when the game
starts, so no new APK is needed (the list built into the APK is the fallback).
```json
{
  "version": 2,
  "mods": [ { "name": "YourName", "id": "3F9A2-C01B7" } ],
  "bans": [ { "name": "", "id": "AB12C-34D5E", "device": "", "reason": "cheating", "until": "" } ],
  "blockedNames": [ "SomeBadName" ]
}
```
A ban can use the Player ID, the headset code (`device`, shown in the mod menu: survives reinstalling) or the name.
`"until": ""` is forever, or put a date like `"2026-12-31"`. Moderators' names are reserved for them.

**Mod menu** (Menu > Mod Menu, only for moderators): everyone on your server, with a red **!** next to anyone the
anti-cheat caught. Pick a player and **Kick** (can't play online for 5 minutes), **Mute** (10 minutes), **Force
rename**, or **Ban** 1 day / 7 days / forever (press twice). It shows their Player ID and headset code for a permanent
ban in moderation.json. Commands are **signed** with the mod's secret key and checked by the player's game, so a faked
command does nothing.

**Mod tools** (bottom row of the mod menu, only work for real moderators):
- **Invisible:** nobody else sees you, your kart, your name or your talk light, and karts pass through you. You see
  no body either, just two balls for hands (your kart stays visible to you so you can drive).
- **Spawn Menu:** a board in front of you; tap anything and it appears a few metres ahead, on any map (hub too).
  Physics things anyone can push around: **Kart, Beach Ball, Crate, Barrel, Football, Giant Cone**; plus every build
  piece (ramps, roads, trees, walls, bouncers, boost pads...). **Undo last** and **Remove all mine**. Up to 25 physics
  things at once (the oldest goes); they vanish when you leave.
- **Blaster:** a toy laser blaster in your right hand while you're walking (hub, or out of your kart with Y). Pull the
  right trigger: the bolt shoves karts and knocks players back (no damage). Everyone sees the bolt.

Every player's game proves its Player ID with its secret key when it joins a server, and the mod tools, mod
commands and build-anywhere pieces only work for a moderator who passed that check.

**Live events** (bottom row of the mod menu): start one for everyone on the server, or **Stop Event**.
- **Disco Sing-Along** (2 min): a party song, a disco ball and coloured lights, and the words of an original
  karting song floating in front of everyone, in time with the music.
- **Last Kart Standing** (up to 2.5 min): a red ring closes in on the middle of the map; anyone caught outside is
  out (their kart stops). Last kart inside wins. Not in the hub (karts are parked there).
- **Low Gravity** (1 min): everything floats; ramps send you flying.
- **Turbo Frenzy** (1 min): everyone's kart is 60% faster.
- **Ball Rain** (1 min): beach balls fall from the sky all around you.

**Anti-cheat.** There's no game server (Photon Shared Mode), so nothing is 100% hack-proof, but:
- A modified copy of the APK can't go online (Android's genuine-app check). Banned players can still play solo.
- Every headset watches the others: impossible speeds several checks in a row = flagged. Flagged players can't push
  you, and mods see the warning.
- Bumps are capped (nobody can launch you into space), building is rate-limited, and rude names sent by a hacked
  game show as "Player".
- Everyone must play the **same version** of the game online; build a new APK for everybody after updates.

### Speed screen
Your kart has a small screen above the steering wheel showing your speed in **MPH**, with lap, position, boost and
messages underneath. (The old floating speed text is gone.)

### Controls
| | VR | Keyboard (testing without a headset) |
|---|---|---|
| **Walk** (hub) | Left thumbstick (the way you're looking); **click the right thumbstick** to run | W/A/S/D, hold Shift to run |
| **Turn** (hub) | Right thumbstick (snap turn) | Q / E |
| Gas | Right trigger | W / ↑ |
| Brake / reverse | Left trigger | S / ↓ |
| **Steer** | **Grab the steering wheel** (grip) with one or both hands and turn it, or use the left thumbstick | A / D |
| Drift (hold through a corner; long drifts give a mini-boost) | X | Space |
| **Booster** (if your kart has one) | A | Left Shift |
| Respawn | B | R |
| Run (hub) | Click the right thumbstick (you stop running when you stop walking) | Shift |
| **Get out and walk** (any map) / get back in | **Y** | Y |
| Recenter view | Menu > Recenter | C |
| **Menu + settings** | **Menu button** (three lines, left controller) | Tab / Esc |
| Press buttons | Touch them with your hand, **or point the laser and pull the trigger** | Left click |
| Look around | Your head | Hold right mouse |

**The steering wheel:** in VR, put your hand on the wheel and squeeze **grip** to hold it, then turn it like a real
wheel. One hand or two both work. Let go and it springs back to the middle. Not holding it? The left thumbstick turns
the wheel smoothly for you.

### Build mode (Free Roam)
**Click the right thumbstick** (or the **Build Mode** button on the wrist menu, or **B** on the keyboard) to hop out of
your kart and **fly around**. Point with your right hand: a blue preview shows where the piece goes. Everyone in the
session sees what you build (up to 250 pieces; after that the oldest go). You can't fly or build past the barrier.

Pieces: Ramp, Big Ramp, Kicker, Road, Road Corner, Boost Pad, Bush, Tree, Pine Tree, Wall, Cone, Rock, Quarter Pipe,
Bouncer, Boost Hoop, Block.

| | VR | Keyboard |
|---|---|---|
| Fly | Left thumbstick (hold **left grip** = fast) | W/A/S/D (Shift = fast) |
| Up / down | Right thumbstick up / down | Space / Left Ctrl |
| Turn | Right thumbstick left / right | Q / E |
| **Place** | Right trigger | F |
| Rotate 45° | Right grip | T |
| Change piece | A (next) / X (previous) | 2 / 1 |
| Delete (point at a piece, the laser turns red) | Left trigger | G |
| Undo your last piece | B | R |
| Back to your kart | Click the right thumbstick | B |

### The menu (three-lines button)
It starts closed. Press the **menu button** on your left controller to open it above your left hand, and poke it
with your right hand (or point at it and pull the trigger). Four pages:
- **Menu:** Recenter, Respawn, Hub, Create/Leave Lobby, Build Mode (in Free Roam), plus your server/lobby info.
- **Sound:** sound effects, kart engine and music volume (- / +).
- **Controls:** turning style (Snap 30°, Snap 45° or Smooth), steering (grab the wheel, or left stick only), and a
  list of the controls.
- **Voice:** mic mode (open mic / push-to-talk / muted), a live mic level meter, voice volume, and **Mic Test**.

### Voice chat
Talk to everyone on your server: their voices come from their head (or their kart), and a **green light** shows above
whoever is talking. It runs through Photon Fusion, so there's nothing extra to install.
- **Open mic** (default) sends only while you're actually speaking; **push-to-talk** sends while you hold the **left thumbstick in**
  (V on the keyboard); **Muted** turns your mic off. Change it in **Menu > Voice**.
- **Mic Test** plays your own voice back through the whole send/receive path, to check your mic works.
- On Quest the headset asks for microphone permission the first time; say **Allow**. (If you said no: Settings > Apps
  > Kart VR > Permissions > Microphone.)
- Voice is 16 kHz, about 6 KB/s per talking player. Packets that arrive late or shuffled are put back in order, and a
  lost packet is covered over so the voice doesn't stutter.

### Sound and music
A soft engine hum (pitch follows your speed; turn it down with Menu > Sound > Kart engine), boosts, bumps, checkpoint dings, lap and finish fanfares, countdown beeps,
build-mode pops and menu clicks, plus two original music loops (a chill one in the garage and Free Roam, a fast one
for racing and bumping). **All of it is generated by the game's code** (no audio files), so there's nothing
copyrighted in it.

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

### Play on Quest without Unity, and share it with friends
**1. Build the game file (APK).** Install **Android Build Support** for your Unity version (Unity Hub > Installs >
gear > Add modules, tick it with OpenJDK and Android SDK & NDK), then click **KartVR > Build for Quest (APK)**. It sets
everything a Quest app needs (64-bit ARM / IL2CPP, Vulkan, Android 10+ targeting 12L, landscape, internet, a package
name, a higher version number each build) and makes **Builds/KartVR.apk**. The first build takes a while.

**2. Put it on your own headset.** Turn on Developer Mode (Meta Horizon phone app > your headset > Developer Mode;
you need a free developer account), install **Meta Quest Developer Hub** on your PC, plug the headset in, and drag
the APK onto it. It shows up in your library under **Unknown Sources**.

**3. Let friends download it in VR (no Unity, no developer mode).**
1. Make a key once: Edit > Project Settings > Player > Android > Publishing Settings > **Keystore Manager > Create
   New**. Keep the keystore file and its passwords safe: every update must be signed with the same key. Build again.
2. At **developers.meta.com/horizon** create an organization and a new **Meta Horizon Store** app for Quest, and fill in
   the basics it asks for.
3. Upload the APK to the **Alpha** (or Beta) release channel with Meta Quest Developer Hub (or the Oculus Platform
   Utility).
4. In the dashboard, invite friends to that channel by email or with an **invite link**. They accept, and the game
   appears in their store/library on the headset to download.
5. Each update: build again (the version code is the build time, so it always goes up, even in a fresh copy of the project) and upload to the same channel.

**If the APK opens as a flat window / you can't move.** VR didn't start on the headset. **Build for Quest** now checks
the Android OpenXR settings (OpenXR ticked, Oculus Touch Controller Profile, Meta Quest Support) and won't build until
they're on. It opens the page to tick them.

**If the APK can't join a lobby.** The hub's info board and the server board say why. Usually it's one of these:
- **No Photon App ID in the build.** Paste it in **KartVR > Set Up Multiplayer**, then build again. The build menu now
  copies it into Fusion's settings by itself and warns you if it's missing.
- **No internet on the headset.** Check its Wi-Fi.
- If going online fails, you now **play solo** (walk, drive, every map) instead of being stuck. Touch a server
  button to try online again.
- The game asks for the **microphone** when it starts. Press **Allow**: the popup pauses the game until you answer.

Release channels are invite-only. To put it in the store for everyone, copy the build to the **Production** channel
and submit it for Meta's review. Even alpha uploads must meet Meta's packaging rules (the build menu sets those).

---

## How it works (for when you want to change things)

```
Scripts/
  Core/         KartMode (which map/mode), KartMaps (where each map is + its dome/barrier), PlayerProfile, KartUI helpers
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
  Build/        BuildMode (fly + place pieces), BuildCatalog (the pieces), BuildManager (syncs pieces to everyone)
  World/        SkyDomes (painted dome per map; also added at Play if an old scene lacks them)
Editor/         KartWorldBuilder (+ .Maps, .Domes): the "Build Prototype World" menu
```

- **Networking:** Photon Fusion 2 in **Shared Mode**. Each player owns and simulates their own kart, so your own driving never lags. Other karts smoothly follow their owner's updates. When you ram someone, an RPC tells *their* game to knock their kart. A session stays alive as long as anyone is in it.
- **Public vs private:** public sessions are matched by a `mode` property, so a public race session only matches other public race sessions. A private lobby is a hidden session named after its code. Inside one, modes are switched by changing `GameState.Mode`, and every kart teleports to that mode's spawn area.
- **All maps live in one scene**, spread far apart. Switching session reloads the scene for a clean start.
- **Each player spawns two network objects:** a `Player` (on foot) and a `Kart`. In the hub the kart parks itself in the player's garage and the VR rig rides the avatar; in other modes the avatar hides and the rig sits in the kart.
- **Garages** are claimed automatically: each player takes the lowest free bay, and if two pick the same one, the lower player id keeps it.
- **Performance:** scenery is marked static so Unity batches it, karts and bodies share materials, and there are no real-time point lights (glowing parts use emissive materials). That keeps it light enough for Quest.
- **Tuning:** select the Kart prefab and look at `KartController`. There's a separate *Tuning* block per mode (top speed, acceleration, turn rate, grip, drift grip, bump power). Laps and round lengths are on the `GameState` object (`RaceManager` / `BumperManager`).
- **Adding your own maps or art:** replace the generated blocks with your models. The things that matter are a `SpawnArea` (with child slots) for each mode, `Checkpoint`s in order with index 0 at the finish line (and their `map` set to the race they belong to), `ModePortal`s in the hub, and an entry in `KartMaps` (position + dome size) for each map. Re-running the builder overwrites the scene and prefab, so after you start customising, stop using it (or copy the scene first).

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
