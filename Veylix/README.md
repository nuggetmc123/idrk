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
   The scene then shows a showroom with every car, and each car is also saved as a prefab in
   `Assets/Veylix/Generated/Cars`. The tracks, scenery and menus are built when the game runs.
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
