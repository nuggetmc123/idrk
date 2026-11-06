# Warehouse model

A simple, empty warehouse you can walk into, made for a Gorilla Tag–style VR fan game.

| File | Use it for |
| --- | --- |
| `warehouse.blend` | Open straight in Blender (5.x). |
| `warehouse.glb` | Blender (File → Import → glTF 2.0), Unity (with the glTFast package), Bevy/Rust, Godot. |
| `warehouse.obj` + `warehouse.mtl` | Blender (File → Import → Wavefront OBJ) or Unity (drag in, no plugins). Keep the two files together. |
| `make_warehouse.py` | Regenerates the `.glb`, `.obj` and `.mtl`. Change the numbers at the top to resize it, then run `python3 make_warehouse.py`. |

Size: 24 m wide × 36 m long, 8 m walls, 10 m to the roof peak. Units are meters.

- **Big door** (6 m × 5 m, yellow frame) on the front wall.
- **Small door** (1.4 m × 2.6 m) on the back wall.
- **Open windows** along the top of both side walls, which let light in.
- **Empty inside**, with a flat concrete floor.

## Object names

Every piece is its own object, named for what it is. "Left" and "right" mean as seen
standing outside, facing that wall.

- `Floor`
- `FrontWall_LeftOfDoor`, `FrontWall_RightOfDoor`, `FrontWall_AboveDoor`, `FrontWall_Gable`
- `BackWall_LeftOfDoor`, `BackWall_RightOfDoor`, `BackWall_AboveDoor`, `BackWall_Gable`
- `LeftWall_BelowWindows`, `LeftWall_AboveWindows`, `LeftWall_WindowPost_1` … `_7` (same for `RightWall_…`)
- `Roof_Left`, `Roof_Right`
- `DoorFrame_Left`, `DoorFrame_Right`, `DoorFrame_Top`

Materials: `Concrete` (floor), `MetalWall` (walls), `Roof`, `Trim` (door frame). They're
plain colors, so you can swap in your own textures. The UVs are set up so a texture
repeats every 2 m.

## Making it walkable in Unity

The pieces need colliders before players can stand on the floor or climb the walls.
Every piece is a box shape, so the simplest way is to select all the objects in the
imported model and click **Add Component → Box Collider**. The two `_Gable`
triangles and the two roof panels are the exceptions: give those a **Mesh Collider**
instead. Mark the objects **Static**, and put them on the layer your gorilla
player's hands and body collide with.
