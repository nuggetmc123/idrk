# Drivable survival boat (FBX)

A low-poly, beat-up wooden motor skiff in the style of Rust's rowboat / Stranded Deep's small boats:
plank hull, three benches, a fuel can, and a
clean army-green tiller outboard.

![preview](preview.png)

| File | What |
|---|---|
| `Boat.fbx` | The model, textures embedded (Unity / Unreal / Roblox / Godot) |
| `Boat.glb` | Same model as glTF (web / three.js / Godot) |
| `Boat.blend` | Blender source |
| `textures/T_Boat.png` | The one 2048² texture all parts share (also embedded in the FBX) |
| `Unity/BoatController.cs` | Buoyancy + driving + enter/exit script |
| `build_boat.py` | Regenerates everything (`pip install bpy && python3 build_boat.py`) |
| `render_preview.py` | Re-renders the preview images |

**Specs:** ~4.5 m long, 1.7 m beam, ~1.2k triangles (low poly, flat shaded), real-world metres, Y-up, bow faces +Z in Unity.

## What's in the FBX

Four plain meshes, no empties, no nested parents, no rotations, one material (`M_Boat`) and
one texture (`T_Boat.png`), so parts land where they should in any engine:

| Mesh | Origin | Use |
|---|---|---|
| `Boat` | centre of the boat at the waterline | hull, benches, fuel can... everything static |
| `Motor` | on the outboard's steering axis | rotate around up to steer |
| `Propeller` | on the propeller shaft | spin around the boat's forward axis |
| `Boat_Collider` | same as `Boat` | simple convex hull for physics, hide it |

## Unity quick start

1. Drag `Boat.fbx` into `Assets/`. If it shows up white, drag `textures/T_Boat.png` into Assets too and set it as the Albedo/Base Map of the `M_Boat` material (or use Materials tab → **Extract Textures**).
2. Put the boat in the scene, add a **Rigidbody** (mass ~350) and **BoatController** to the top object.
3. On `Boat_Collider` add a **MeshCollider** with *Convex* ticked.
4. Drag your player into the `Player` field and set `Water Level` to your ocean height.
5. Play: walk up, **E** to get in/out, **W/S** throttle, **A/D** steer, **Space** cuts the engine.

For waves, subclass `BoatController` and override `GetWaterHeight(worldPos)`.
On Unity versions before 6, rename `linearDamping/angularDamping/linearVelocity` to `drag/angularDrag/velocity`.

**Roblox:** Import with the 3D Importer (File → Import 3D). You get a Model with `Boat`, `Motor`, `Propeller` and `Boat_Collider` MeshParts in the right places. If the boat is grey, upload `textures/T_Boat.png` as a Decal/Image and paste its ID into each MeshPart's `TextureID`. Delete or hide `Boat_Collider` (or set it `Transparency = 1`), weld everything to `Boat`, and add a `VehicleSeat` on the rear bench.

**Unreal:** rename `Boat_Collider` to `UCX_Boat` before import to get it as auto collision.
