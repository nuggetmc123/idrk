# Low-poly chicken

![preview](preview.png)

Flat-shaded, low-poly hen in a survival-game style (think Rust / Stray-style wildlife).

- `chicken.fbx`: ready to drop into Unity / Unreal / Godot
- `chicken.blend`: Blender source
- `build_chicken.py`: script that generates both (`pip install bpy` then `python3 build_chicken.py`)

| | |
|---|---|
| Size | ~2.5 m tall (`SCALE = 5.0` at the top of the script; set it to 1.0 for a real-size ~0.5 m chicken) |
| Parts | 17 separate meshes: Body, Neck, Head, Beak, Comb, Wattle, Eye.L/R, Tail, Wing.L/R, Thigh.L/R, Leg.L/R, Foot.L/R |
| Materials | 7 solid colours (Feather, FeatherDark, FeatherLight, Comb, Beak, Leg, Eye) |
| Rig | Root, Body, Neck, Head, Tail, Wing.L/R, Leg.L/R |
| Animations | `Idle` (61f), `Walk` (25f, loops), `Peck` (31f), `Flap` (21f) @ 30 fps |

Exported with forward `-Z` and up `Y`, so in Unity the chicken faces `+Z`.
## Editing parts

Every part is its own object (origin at its centre) parented to `ChickenRig`, with an Armature
modifier and one vertex group fully weighted to its bone. In Blender you can select any part
and move, scale or edit it in Edit Mode; geometry you extrude or duplicate inherits the
vertex group, so it keeps following the rig. To move a part to a different bone, rename its
vertex group to that bone's name. Re-export with File > Export > FBX (Armature + Mesh,
"Add Leaf Bones" off).

Change the colours in `PALETTE` at the top of the script and re-run it to make a white or black chicken.
