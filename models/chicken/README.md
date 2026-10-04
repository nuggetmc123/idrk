# Low-poly chicken

![preview](preview.png)

Flat-shaded, low-poly hen in a survival-game style (Rust-style wildlife): russet body,
golden neck hackles, black tail fan and flight feathers, serrated red comb, wattles,
amber eyes, jointed yellow legs with claws.

- `chicken.fbx`: ready to drop into Unity / Unreal / Godot
- `chicken.blend`: Blender source
- `build_chicken.py`: script that generates both (`pip install bpy` then `python3 build_chicken.py`)

| | |
|---|---|
| Size | ~2.75 m tall (`SCALE = 5.0` at the top of the script; 1.0 gives a real-size ~0.55 m hen) |
| Polycount | ~1,670 tris |
| Parts | 53 separate meshes, grouped in Blender into `Chicken_Head`, `Chicken_Body`, `Chicken_Wings`, `Chicken_Tail`, `Chicken_Legs` collections |
| Materials | 10 solid colours (`PALETTE` in the script) |
| Rig | Root, Body, Neck1, Neck2, Head, Tail, Wing.L/R, Thigh.L/R, Shank.L/R, Foot.L/R |
| Animations @ 30 fps | `Idle` (73f), `Walk` (25f loop), `Run` (17f loop), `Peck` (31f), `Flap` (25f), `Death` (41f) |

Exported with forward `-Z` and up `Y`, so in Unity the chicken faces `+Z`.

## Parts

- **Head (13):** Neck, Head, Beak.Upper, Beak.Lower, Comb, Wattle.L/R, EarLobe.L/R, Eye.L/R, Pupil.L/R
- **Body (2):** Body, BodyFluff
- **Wings (10):** Wing.L/R, WingFeather.L.1-4, WingFeather.R.1-4
- **Tail (6):** TailFeather.1-6
- **Legs (22 = 11 per side):** Thigh, Shank, Spur, Toe.Inner/Middle/Outer/Back, Claw.Inner/Middle/Outer/Back

## Editing parts

Every part is its own object (origin at its centre) parented to `ChickenRig` with an Armature
modifier and vertex groups for its bones. Select any part in Blender to move, scale or reshape
it in Edit Mode; geometry you extrude or duplicate inherits the vertex groups, so it keeps
following the rig. To move a part to a different bone, rename its vertex group to that bone's
name. Re-export with File > Export > FBX (Armature + Mesh, "Add Leaf Bones" off).

Change the colours in `PALETTE` and re-run the script to make a white or black chicken.
