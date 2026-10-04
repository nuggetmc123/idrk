# Metal Helmet (fits PeeperLeeper)

![front](previews/front_3q.png)

| File | What it is |
|---|---|
| `PeeperLeeper_MetalHelmet.fbx` | Player + helmet, already attached to the **Head** bone. Easiest: just use this as your player model. |
| `MetalHelmet.fbx` | Helmet only (for a cosmetic/hat system). |
| `MetalHelmet.blend` | Editable source. Includes the player for reference. Modifiers are still live. |
| `make_helmet.py` | Generator: change the numbers in `P` and run it again to rebuild everything. |

## Parts (each one is a separate object you can edit or delete)

All parts are children of the empty `MetalHelmet`:

- `Helmet_Dome`: the main shell (Solidify = thickness, Bevel = soft edges)
- `Helmet_Rim`: the rolled rim around the brim
- `Helmet_Crest`: the ridge from front to back
- `Helmet_NoseGuard`: the bar between the eyes
- `Helmet_CheekGuard_L` / `Helmet_CheekGuard_R`
- `Helmet_NeckGuard`
- `Helmet_Rivets`

Materials: `Helmet_Steel`, `Helmet_DarkSteel`, `Helmet_Brass` (all metallic, so you can recolor each one separately).
Every part has UVs, so you can add your own textures.

Fit: the generator checks every vertex against the player mesh. Nothing pokes through,
and the closest point is about 7 mm from the head.

## Unity
- **Option A:** drop `PeeperLeeper_MetalHelmet.fbx` in and use it in place of the old player model.
  The helmet is already under `Armature/Hips/Spine/Chest/Neck/Head`, so it follows the head.
- **Option B (cosmetic):** import `MetalHelmet.fbx` with the same import settings as your player.
  Put both at the same position with the player in its rest pose, then drag `MetalHelmet` onto the
  `Head` bone and keep its world position. Save it as a prefab, and toggle it on and off as a cosmetic.
- To turn off a single piece (for example, no nose guard), disable that child object.
- The materials import as Standard/Lit. Set Metallic to about 1 and Smoothness to about 0.65 for the shine.

## Regenerate
```
pip install bpy==4.2.0      # or use a Blender 4.x install
python3 make_helmet.py path/to/PeeperLeeper.fbx . --render
# or: blender --background --python make_helmet.py -- path/to/PeeperLeeper.fbx . --render
```
