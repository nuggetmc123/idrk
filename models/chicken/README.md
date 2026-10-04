# Low-poly chicken

![preview](preview.png)

Flat-shaded, low-poly hen in a survival-game style (think Rust / Stray-style wildlife).

- `chicken.fbx`: ready to drop into Unity / Unreal / Godot
- `chicken.blend`: Blender source
- `build_chicken.py`: script that generates both (`pip install bpy` then `python3 build_chicken.py`)

| | |
|---|---|
| Size | ~0.5 m tall, real-world metres |
| Polycount | 426 verts / 736 tris |
| Materials | 7 solid colours (Feather, FeatherDark, FeatherLight, Comb, Beak, Leg, Eye) |
| Rig | Root, Body, Neck, Head, Tail, Wing.L/R, Leg.L/R |
| Animations | `Idle` (61f), `Walk` (25f, loops), `Peck` (31f), `Flap` (21f) @ 30 fps |

Exported with forward `-Z` and up `Y`, so in Unity the chicken faces `+Z`.
Change the colours in `PALETTE` at the top of the script and re-run it to make a white or black chicken.
