# Kingdom Chaos for Unity

A C# port of the browser game in `kingdom/index.html`: the Knight, three weapons, two pets,
all of World 1 (four stages, the Goblin Captain and King Slime), XP, coins, the shop and the
Training Room. Everything is drawn from code, so there are no sprites, prefabs or audio
files to set up.

Works in Unity 2021.3 or newer, including Unity 6, with the Built-in render pipeline or URP.

## Getting it into Unity

1. In Unity Hub, create a new project. The **2D** or **Universal 2D** template is the
   natural fit, but any template works.
2. In the editor, choose **Assets > Import Package > Custom Package…**, pick
   `KingdomChaos.unitypackage` and click **Import**. (Or copy this `KingdomChaos` folder
   into your project's `Assets` folder.)
3. Choose **Tools > Kingdom Chaos > Create Game Scene**. This creates
   `Assets/KingdomChaos/KingdomChaos.unity`, opens it, and adds it to Build Settings.
4. Press **Play**.

If you'd rather set it up by hand: make an empty GameObject in any scene, add the
**Kingdom Chaos Game** component, and press Play. The component sets up the camera itself.

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
| `Scripts/KCData.cs` | Every number: weapons, pets, enemy stats, stage waves, the save format. Balancing happens here. |
| `Scripts/KingdomChaosGame.cs` | The game: player, combat, enemy AI (one method per enemy), level flow, drawing, HUD and menus. |
| `Scripts/KCPainter.cs` | A small vector drawing layer (circles, rounded rectangles, outlines) that builds one mesh per frame. |
| `Scripts/KCInput.cs` | Keyboard and mouse, for either input system. |
| `Scripts/KCAudio.cs` | Sound effects, synthesised at startup. |
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
