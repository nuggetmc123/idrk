# Drivable survival boat (FBX)

A low-poly, beat-up wooden motor skiff in the style of Rust's rowboat / Stranded Deep's small boats:
plank hull, three benches, a fuel can, and a
clean army-green tiller outboard.

![preview](preview.png)

| File | What |
|---|---|
| `Boat.fbx` | The model, textures embedded (Unity / Unreal / Roblox / Godot) |
| `Boat_LostVR.fbx` | Your edited boat with "LOST VR" banners (`Banner_L`, `Banner_R`) on both sides, textures embedded |
| `textures/T_Banner.png` | The banner texture (2048×384) |
| `add_banner.py` / `make_banner.py` | Put the banner on any boat FBX that has a `Hull`: `python3 add_banner.py in.fbx out.fbx` |
| `Boat_Collider.fbx` | Optional simple physics hull (separate so it doesn't cover the boat) |
| `Boat.glb` | Same model as glTF (web / three.js / Godot) |
| `Boat.blend` | Blender source |
| `textures/T_Boat.png` | The one 2048² texture all parts share (also embedded in the FBX) |
| `Unity/BoatController.cs` | Buoyancy + driving + enter/exit script |
| `build_boat.py` | Regenerates everything (`pip install bpy && python3 build_boat.py`) |
| `render_preview.py` | Re-renders the preview images |

**Specs:** ~4.5 m long, 1.7 m beam, ~1.2k triangles (low poly, flat shaded), real-world metres, Y-up, bow faces +Z in Unity.

## What's in the FBX

Every part is its own object (41 of them), each with its origin at its own centre, so you can
select, move, hide, recolour or delete any of them. No empties, no nested parents, no rotations,
one material (`M_Boat`) and one texture (`T_Boat.png`), so nothing gets misplaced on import.

| Group | Parts |
|---|---|
| Hull | `Hull`, `Gunwale`, `Keel_Strip`, `Transom_Plate`, `Rib_0`-`Rib_3` |
| Inside | `Seat_Rear`, `Seat_Middle`, `Seat_Front` (+ `_Support`), `Floorboard_0`-`Floorboard_3` |
| Fittings | `Bow_Ring`, `Cleat_L/R`, `Oarlock_L/R` |
| Fuel can | `Fuel_Can`, `Fuel_Can_Handle`, `Fuel_Can_Cap` |
| Motor (steers) | `Motor_Cowling`, `Motor_Cowling_Band`, `Motor_Clamp`, `Motor_Midsection`, `Motor_Leg`, `Motor_AntiCav_Plate`, `Motor_Gearcase`, `Motor_Skeg`, `Motor_Tiller`, `Motor_Tiller_Grip`, `Motor_Pull_Cord` |
| Propeller (spins) | `Propeller_Hub`, `Propeller_Blade_0`-`_2` |

To steer/spin them yourself, group them under pivots at these points (Blender coords / Unity coords):
steering axis `(0, 2.38, 0)` / `(0, 0, -2.38)`, propeller shaft `(0, 2.67, -0.34)` / `(0, -0.34, -2.67)`.
The Unity script does this for you.

## Unity quick start

1. Drag `Boat.fbx` into `Assets/`. If it shows up white, drag `textures/T_Boat.png` into Assets too and set it as the Albedo/Base Map of the `M_Boat` material (or use Materials tab → **Extract Textures**).
2. Put the boat in the scene, add a **Rigidbody** (mass ~350) and **BoatController** to the top object.
3. Collision is added for you. (Optional: drag `Boat_Collider.fbx` in as a child for a cheaper collider.)
4. Drag your player into the `Player` field and set `Water Level` to your ocean height.
5. Play: walk up, **E** to get in/out, **W/S** throttle, **A/D** steer, **Space** cuts the engine.

For waves, subclass `BoatController` and override `GetWaterHeight(worldPos)`.
On Unity versions before 6, rename `linearDamping/angularDamping/linearVelocity` to `drag/angularDrag/velocity`.

**Roblox:** Import with the 3D Importer (File → Import 3D). You get a Model with every part as its own MeshPart in the right place. If the boat is grey, upload `textures/T_Boat.png` as a Decal/Image and paste its ID into each MeshPart's `TextureID`. Weld everything to `Hull`, and add a `VehicleSeat` on the rear bench.

**Blender:** textures don't show in the default *Solid* view. Switch to *Material Preview* (press `Z` → Material Preview, or the second sphere icon top-right).

**Unreal:** import `Boat_Collider.fbx` too, renamed to `UCX_Boat`, to get it as auto collision.
