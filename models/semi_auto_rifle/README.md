# Semi-Automatic Rifle (Rust SAR / Stray(ed) VR style)

![preview](preview.png)

| File | What it is |
| --- | --- |
| `semi_auto_rifle.fbx` | The model. Drop it into Unity, Unreal, Godot, etc. |
| `semi_auto_rifle.blend` | Blender source file |
| `build_rifle.py` | Script that generates both: `pip install bpy && python3 build_rifle.py` |

**Specs:** about 4k triangles, every part has a Scale of 5, so the gun is about 6 m long, UV-unwrapped. It's a clean,
simple version with no small details like screws, welds, tape or clamps. It has 3 materials: wood, gun steel and
worn/rusty metal.

**Orientation:** the muzzle points forward (+Z in Unity, -Y in Blender) and up is up.

## Parts (every piece is its own object)

Each part comes into Unity as a separate GameObject. In Unity each one has Scale 5, rotation 0 and its
pivot at its own centre. Child parts (bolt handle, bolt knob, mag baseplate) show Scale 1 because
they inherit 5 from their parent, so you can select any piece and move, rotate, hide, delete or swap it.

| Group | Objects |
| --- | --- |
| Body | `Receiver`, `Receiver_DustCover`, `MagWell`, `TriggerGuard` |
| Barrel | `Barrel`, `Barrel_Chamber`, `GasTube`, `GasBlock`, `GasBlock_Link`, `Muzzle` |
| Sights | `FrontSight_Base`, `FrontSight_Post`, `RearSight_Base`, `RearSight_Leaf_L`, `RearSight_Leaf_R` |
| Wood | `Handguard`, `Stock`, `ButtPlate` |
| Moving parts | `Magazine` (child: `Magazine_Baseplate`), `Bolt` (children: `Bolt_Handle`, `Bolt_Knob`), `Trigger` |

The moving parts keep the pivot they move around:
- `Magazine`: pivot at the top of the mag, so moving it down drops the mag. The baseplate follows it.
- `Bolt`: slide it backward to rack it. The handle and knob follow it.
- `Trigger`: pivot on the trigger pin, so rotating it pulls the trigger.

### In Unity

1. Drag `semi_auto_rifle.fbx` into your Assets folder.
2. Drag it into the scene. All the parts are listed under it in the Hierarchy.
3. To rearrange or group parts, right-click it and choose **Prefab > Unpack Completely**. You can then
   re-parent anything, for example put all the Body/Barrel/Wood parts under a new empty called
   `RifleBody`.
4. If the materials appear grey or locked, select the FBX, open the **Materials** tab in the
   Inspector, and choose **Extract Materials...** so you can edit the colours.

The file has meshes only, with no empties or markers. If you need attach points, add them in your
engine at these spots (Blender coordinates, in meters):

| Point | Position (x, y, z) |
| --- | --- |
| Right hand grip | 0, 0.85, -0.225 |
| Left hand grip | 0, -1.8, -0.05 |
| Muzzle | 0, -3.825, 0.02 |
| Mag well | 0, -0.4, -0.175 |
| Ejection port | 0.15, -0.3, 0.11 |

The materials are flat colours with no textures. The UVs are there so you can paint rust and wear in
Substance Painter or Blender.
