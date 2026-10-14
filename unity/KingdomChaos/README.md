# Kingdom Chaos for Unity

A C# port of the browser game in `kingdom/index.html`: the Knight, three weapons, two pets,
all of World 1 (four stages, the Goblin Captain and King Slime), XP, coins, the shop and the
Training Room. Everything is drawn from code, so there are no sprites, prefabs or audio
files to set up.

Works in Unity 2021.3 or newer, including Unity 6, with the Built-in render pipeline or URP.

## Getting it into Unity

There are two downloads; use whichever matches how you add things.

**Option A: Package Manager** (`com.nuggetmc.kingdomchaos-0.1.1.tgz`)

1. Open **Window > Package Manager**.
2. Click the **+** in the top-left corner and choose **Install package from tarball…**
   (called **Add package from tarball…** in older versions).
3. Pick the `.tgz` file. **Kingdom Chaos** appears in the list under **In Project**.

**Option B: Import Package** (`KingdomChaos.unitypackage`)

1. Choose **Assets > Import Package > Custom Package…** from the top menu. This is
   not the Package Manager window; a `.unitypackage` never shows up there.
2. Pick the `.unitypackage` file and click **Import** in the window that lists the files.

Then, either way, press **Play** and click inside the Game view so it gets your keyboard.
The game adds itself to whatever scene is open, so there's nothing to set up.

**Tools > Kingdom Chaos > Create Game Scene** is optional: it makes a dedicated
`Assets/KingdomChaos/KingdomChaos.unity` scene and adds it to Build Settings, which you want
before making a standalone build.

## Controls

| Key | Action |
| --- | --- |
| A / D | Move |
| W / S | Move up and down the lane |
| Space | Jump, twice for a double jump |
| Shift | Dodge roll |
| Left click / J | Light combo |
| Right click / K | Heavy attack |
| S + attack in the air | Ground slam |
| Q | Royal Spin |
| E | Eat food, open chests |
| F | Pet fury |
| Esc / P | Pause |
| M | Mute |

Input works whichever setting **Project Settings > Player > Active Input Handling** is on
(old Input Manager, new Input System, or Both).

## What's where

| File | What it holds |
| --- | --- |
| `Runtime/KCData.cs` | Every number: weapons, pets, enemy stats, stage waves, the save format. Balancing happens here. |
| `Runtime/KingdomChaosGame.cs` | The game: player, combat, enemy AI (one method per enemy), level flow, drawing, HUD and menus. |
| `Runtime/KCPainter.cs` | A small vector drawing layer (circles, rounded rectangles, outlines) that builds one mesh per frame. |
| `Runtime/KCInput.cs` | Keyboard and mouse, for either input system. |
| `Runtime/KCAudio.cs` | Sound effects, synthesised at startup. |
| `Editor/KingdomChaosMenu.cs` | The **Tools > Kingdom Chaos** menu item. |

The game runs in "canvas pixels", the same units as the browser version: the view is
960 × 540, `x` runs along the level, `y` is depth in the walking lane and `z` is height. A
thing is drawn at screen position `(x - camX, y - z)`.

Progress saves to `PlayerPrefs` under `kingdomChaos.save.v1`. To wipe it, use **Reset save**
on the world map.

## Troubleshooting

- **Nothing draws, just a black screen (URP 2D Renderer).** Select
  `KingdomChaosVertexColor.mat` and change its shader to
  **Universal Render Pipeline/2D/Sprite-Unlit-Default**. Any shader that uses vertex
  colours works.
- **The game doesn't react to keys.** Click inside the Game view so it has focus.
