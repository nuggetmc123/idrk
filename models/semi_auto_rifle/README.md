# Semi-Automatic Rifle (scrap-built, Rust SAR / Stray(ed) VR style)

![preview](preview.png)

| File | What it is |
| --- | --- |
| `semi_auto_rifle.fbx` | The model. Drop it into Unity, Unreal, Godot, etc. |
| `semi_auto_rifle.blend` | Blender source file |
| `build_rifle.py` | Script that generates both: `pip install bpy && python3 build_rifle.py` |

**Specs:** about 7.4k triangles, real-world scale in meters (about 1.2 m long), UV-unwrapped, 7 PBR materials
(wood, dark wood, gun steel, worn steel, rusty metal, duct tape, brass).

**Orientation:** the muzzle points forward (+Z in Unity, -Y in Blender) and up is up.

## Hierarchy (built for VR)

```
SemiAutoRifle            root
├── Rifle_Body           all static parts merged into one mesh
├── Magazine             pivot at the top of the mag; move it down/out to drop the mag
├── Bolt                 charging handle; slide it along local +Y in Blender (backward) to rack it
├── Trigger              pivot on the trigger pin; rotate it on X to pull
└── Socket_*             empties: GripRight, GripLeft, Muzzle, MagWell, EjectPort, RearSight, FrontSight
```

Use `Socket_GripRight` / `Socket_GripLeft` as the hand attach points, `Socket_Muzzle` for spawning
bullets and muzzle flash, `Socket_EjectPort` for spent casings, and `Socket_MagWell` as the snap point
when a magazine is inserted.

The materials are flat colours with no textures. The UVs are there so you can paint rust and wear in
Substance Painter or Blender.
