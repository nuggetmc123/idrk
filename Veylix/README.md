# Veylix — VR Formula racing (Unity)

A seated VR open-wheel racing game for Meta Quest, which also plays on desktop with keyboard, mouse or gamepad.
Inside the car you reach out and grab the steering wheel with your hands and turn it. The game has 25 maps,
7 cars across different racing eras, 10 fictional teams, AI opponents, dynamic weather and time of day, damage,
and online multiplayer through Photon Fusion.

> **Status:** the scripts are type-checked against the Unity and Fusion APIs, but this project has **not been opened
> or tested in the Unity editor yet**. Expect a few first-run fixes.

## Opening the project
1. Right-click `Veylix.zip` > **Extract All…** In Unity Hub click **Add > Add project from disk** and pick the
   extracted `Veylix` folder: the one that directly contains `Assets`, `Packages` and `ProjectSettings`.
   Use **Unity 6 (6000.0.40f1)** or newer.
2. On first open Veylix offers to switch *Active Input Handling* to **Both**. Accept it, and Unity restarts.
3. Wait for the import to finish. Veylix then builds the game world by itself (textures, materials, prefabs and
   `Assets/Veylix/Scenes/Veylix.unity`) and opens the scene. **Veylix > Build Game World** does the same by hand.
   The scene then shows your garage hub with your car, the same place you start in when you press Play.
   The tracks are built when you pick one (see *Seeing the tracks in the editor* below).
4. Press Play. Without a headset you play on keyboard, mouse or gamepad.

### If Unity Hub says "Please add a valid project"
Use `Veylix.unitypackage` instead:
1. In Unity Hub click **New project**, pick Unity 6 and the **3D (Built-In Render Pipeline)** template, and create it.
2. In the editor: **Assets > Import Package > Custom Package…**, choose `Veylix.unitypackage` and click **Import**.
3. Veylix adds the Input System package by itself; accept the restart prompt for *Active Input Handling*.
4. Run **Veylix > Build Game World** and press Play.

## The Veylix menu (top bar)
| Item | What it does |
|---|---|
| **Import Quest (VR Setup)** | Installs OpenXR and the Meta Quest support packages, switches the build target to Android and configures XR, input and graphics for Quest. |
| **Make APK (Quest)** | Builds `Builds/Veylix.apk`, ready to sideload or upload. |
| **Import Multiplayer (Photon Fusion)** | Imports Photon Fusion 2 and asks for your App ID. Until Fusion is imported, an offline stand-in keeps everything compiling. |
| **Build Game World** | Regenerates every texture, material, prefab and the main scene. |
| **Preview a Track** | Builds any of the 25 maps in the editor so you can look around it. |
| **Check Multiplayer** | Shows whether real Fusion or the offline stand-in is active, and repairs the setup. |
| **Signing ›** | Choose the keystore used for store uploads. |

## Controls
**VR (Quest controllers)**
- **Steer:** reach to the wheel, then squeeze **grip** to hold it. Use one or both hands, and let go to release.
- **Throttle / brake:** right trigger / left trigger. Thumbstick pedals are an option in Settings.
- **Shift:** **A** shifts up and **X** or **Y** shifts down (the paddles animate on the wheel).
- **Handbrake:** on the Trainer and Hillclimb cars, grab the lever by your right leg with grip.
- **Reset to track:** hold **B** for a second. **Recenter:** click both thumbsticks in. **Pause:** menu button.
- Force feedback comes through controller haptics: steering load, kerbs, slides, lock-ups and impacts.

**Desktop**
- Use **W/S** or the arrow keys to drive and **A/D** to steer.
- **E/Shift** shifts up and **Q/Ctrl** shifts down, and **Space** is the handbrake.
- Hold **R** to reset, press **C** for the chase camera and **Esc** to pause.
- Gamepads work too.

## Your driver
The first time you play, before you reach the garage, you create your driver in a dark studio after the start-up
screen: a name (on-screen keyboard), a race number, and your kit (suit and stripe colours, helmet colour, design and
visor, gloves). Your driver stands in a pool of light beside the screen and changes as you go. Your driver stands next to your car in the garage, sits in
your car in the same kit, your number goes on your cars, and online everyone sees your driver in your car.
**DRIVER** in the main menu changes it later.

## The watch (in a race, VR)
Press the menu button (three lines, left controller) and a screen rises from the watch on your left wrist: music,
engine, effects and crowd volume, music while racing, pit stop (pick tyres, box this lap), resume, reset car, cockpit
or chase view, restart, recenter, exit. Press the button again to put it away. Offline the race pauses; online it
keeps going. On a monitor the same button (Esc) opens the pause menu.

## Music and sound
The music is synthesised live (no audio files): a relaxed theme in the garage, an ambient bed while loading and an
optional driving track while racing. Race moments have their own sounds: laps, fastest lap, personal best, positions
gained and lost, track-limit warnings, penalties, the pit call, finishing and winning.

## Racing rules
Track limits (three warnings, then a 5-second penalty), yellow flags around stopped cars (the AI respects them),
blue flags when a faster car is about to lap you, and pit stops: request one from the watch (or P on a keyboard),
you come in at the end of the lap, the crew fits the tyres you picked and repairs damage (longer the more there is),
and you rejoin at pit exit.

## Your garage (the hub)
When you're not driving you're in your own garage: your car on a turntable under spotlights, a workbench and tool
wall, a tyre rack, a wall with all 25 tracks, your trophies and career stats, and the roller door out to the track.
Walk around (left stick or WASD; right stick or Q/E turns) and point at the stations:
**CUSTOMISE** (by the car: paint, livery, number, wheels, wings, tyres, brakes, suspension, changes show live on the
car) and **FIND RACE** at the roller door. The menu button hides or brings back the menu.

## Find race
**FIND RACE** (at the door, or in the main menu) starts a search: a timer counts up in the top left of your view and
you can keep walking around your garage. Races are up to 12 drivers on a circuit picked for you. Online, everyone
searching waits together and the race starts as soon as 12 are found, or at one minute if at least one other driver
has turned up. After a minute you're always on your way to a race. Press **FIND RACE** again (or **STOP SEARCH** in
the menu) to stop. **MORE MODES** in the main menu has every other mode with your own choice of track.

## If something goes wrong
Every error the game hits is written to **Veylix-errors.txt** next to the project's Assets folder (on a headset, in the
app's data folder), one copy of each. If a map fails to build you're taken back to the garage with a message instead
of being left on the loading screen.

## Loading screens
A splash screen while your garage is built at start-up, and before every session a loading screen with the track
drawn as a live outline, the session details (mode, car, laps, time, weather), progress and tips.

## Online (needs Photon Fusion: Veylix > Import Multiplayer)
- **FIND A RACE**: matchmaking on the track you picked: join drivers already there or open a race and wait for them.
  **FIND PLAYERS** on the race setup screen does the same for the race you just set up.
- **ANY TRACK**: join whichever race is filling up.
- **PRIVATE ROOM** / **JOIN WITH CODE**: race friends only.
- **PARTY**: create a party (its code starts with 0) and friends join from their garage. The leader picks the track
  (FIND RACE, MORE MODES or the Online menu) and chooses **PARTY ONLY** (a private race) or **PARTY VS OTHERS** (a public race
  other players matchmake into). Everyone in the party goes, and comes back to the party after the race.

## Seeing the tracks in the editor
The game builds each map from code when you pick it, so tracks aren't separate scene files. Use
**Veylix > Preview a Track** to build any of the 25 maps in the Scene view, then go back to the game scene to drive.

## Crashes
Damage changes handling. Carrying too much speed into a corner (sliding sideways over kerbs, gravel or grass) can trip
the car into a roll. Touching wheels with another car can launch you into the air, and a hard hit in the side can
knock a car over. Wings, wheels and mirrors break off and tumble away, and a violent crash can split the fuel cell,
start a fire and sometimes end in an explosion. The AI cars crash the same way.

## The driver
Every car has a driver in a race suit in its team colours, with a six-point harness, legs out to the pedals and arms on
the wheel. In your own cockpit the arms follow your hands, so looking down you see yourself driving.

## Content
- **Circuits (15):**
  - Monaco Street, Silverstone Inspired, Desert GP, Japanese Mountain, Coastal GP
  - Alpine, Tropical Island, Night City, Forest GP, Modern Stadium
  - European Countryside, Snowy Mountain, Australian Outback, Industrial Port, Neon City
- **Free roam (10):**
  - Open Mountain Roads, Coastal Highway, Desert Highway, Countryside Roads, Large Modern City
  - Forest Roads, Snowy Mountain Pass, Tropical Island Roads, Airport Test Track, Massive Free Roam Island
- **Modes:** Grand Prix, Quick Race, Championship, Time Trial (with ghost and personal bests), Practice, Elimination, Free Roam and Online.
- **Cars:**
  - Veylix VX-26 and Strahl S-24 (hybrid era)
  - Corvane CV10 (V10 era) and Meridian M8 (V8 era)
  - Halvorsen H72 (1970s)
  - Formula Trainer FT-4 and Hillclimb Special HX
- **Garage:** paint, livery pattern, number, wheels, wing level, tyre compound, brakes and suspension.

## Layout
- `Assets/Veylix/Scripts`: the game, split into Core, Vehicle, Maps, AI, Race, Env, Player, Cockpit, UI and Network.
- `Assets/Veylix/Editor`: the asset and world builder, plus first-open auto setup.
- `Assets/Veylix/Setup`: the Veylix menu's setup tools (Import Quest, Make APK, Import Multiplayer, Signing) and the
  Fusion switch. They sit in their own assembly, so the menu still appears even if game code has an error.
- `Tools/make_project_zip.sh`: packs this folder as a zip for Unity Hub.
