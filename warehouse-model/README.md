# Warehouse model

A walk-in warehouse for a Gorilla Tag–style VR fan game. It has a roll-up garage door,
a huge open doorway in the side wall, an office and a bathroom, a few rows of shelves, and
a ladder up to the roof.

| File | Use it for |
| --- | --- |
| `warehouse.blend` | Open straight in Blender (5.x). |
| `warehouse.fbx` | **Unity**: drag it into your Assets folder. |
| `warehouse.glb` | Blender (File → Import → glTF 2.0), Bevy/Rust, Godot, or Unity with glTFast. |
| `warehouse.obj` + `warehouse.mtl` | Any 3D program. Keep the two files together. |
| `make_warehouse.py` | Regenerates the `.glb`, `.obj` and `.mtl`. Change the numbers at the top, then run `python3 make_warehouse.py`. |

Size: 18 m wide × 26 m long, 6 m walls, 7.6 m to the roof peak. Units are meters.

![Front](preview_front.jpg)
![Inside](preview_inside.jpg)
![Layout with the roof hidden](preview_overview.jpg)
![Office and bathroom](preview_rooms.jpg)
![Ladder side](preview_back.jpg)

## What's in it (every piece is its own named object)

"Left" and "right" mean as seen standing outside, facing that wall. `LeftWall` is on
your left when you face the garage door.

- **Building:** `Floor`, `Roof_Left`, `Roof_Right`.
- **Front wall:** `FrontWall_LeftOfDoor`, `FrontWall_RightOfDoor`, `FrontWall_AboveDoor`, `FrontWall_Gable`.
- **Back wall** (it has the small door): the same four pieces, named `BackWall_…`.
- **Side walls:**
  - Left wall: `LeftWall_LeftOfOpening` and `LeftWall_RightOfOpening` (with the huge open doorway between them), `LeftWall_AboveWindows`, and `LeftWall_WindowPost_1` … `_7`.
  - Right wall: `RightWall_BelowWindows`, `RightWall_AboveWindows`, and `RightWall_WindowPost_1` … `_7`.
- **Side opening:** a 9 m × 4.3 m open doorway in the left wall, framed by `SideOpeningFrame_Left`, `SideOpeningFrame_Right` and `SideOpeningFrame_Top`.
- **Garage door:**
  - `GarageDoor` is rolled up to 2.4 m, so you can walk under it. Move it down 2.4 m to close it.
  - `GarageDoor_Track_Left`, `GarageDoor_Track_Right`, `GarageDoor_RollBox`.
  - `DoorFrame_Left`, `DoorFrame_Right`, `DoorFrame_Top` (the yellow trim).
- **Roof access:** `RoofLadder`, on the outside of the right wall.
- **Shelves:** `Shelf_RightWall_1` … `_5` and `Shelf_Middle_1` … `_4` (9 units, 4 shelf levels each).
- **Office and bathroom** (back-left corner, 3 m walls, with a flat ceiling you can climb onto):
  - Walls: `Office_FrontWall_…` (a doorway and a window), `Bathroom_FrontWall_…` (a doorway), `Office_Bathroom_Wall`, `Bathroom_SideWall`, `Rooms_Ceiling`.
  - Floors: `Office_Floor` (carpet), `Bathroom_Floor` (tile).
  - Office: `Office_Desk`, `Office_Computer`, `Office_Chair`, `Office_FilingCabinet`.
  - Bathroom: `Bathroom_Toilet`, `Bathroom_Sink`, `Bathroom_Mirror`.

Materials: `Concrete`, `MetalWall`, `Roof`, `Trim`, `GarageDoor`, `Steel`, `ShelfFrame`,
`Wood`, `RoomWall`, `Carpet`, `Tile`, `Porcelain`, `Plastic`, `Mirror`. They're plain colors, so you can swap in your own textures.
The UVs are set up so a texture repeats every 2 m.

## Using it in Unity

- **Colliders:** select every object and add a **Mesh Collider**. Mark them all **Static**.
  Put them on the layer your gorilla player's hands and body collide with. That makes
  the floor, walls, shelves, rooms, ladder rungs and roof all climbable.
- **Lighting:** the model has no lights of its own. Light enters through the windows and
  doors, so add your own lights to brighten the inside. If you build for Quest, set
  them to **Baked** and bake the lighting (Window → Rendering → Lighting).
