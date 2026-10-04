# Bandage model

A rolled-up cloth bandage (just the roll, no loose strip), in the style of the bandages in Rust and Strayed VR.

![preview](preview.png)

| File | What it is |
|---|---|
| `bandage.fbx` | The model, with both textures embedded (FBX 7.4, binary). Works in Unity, Unreal and Blender. |
| `textures/bandage_albedo.png` | 1024² tileable gauze colour map (off-white with light dirt) |
| `textures/bandage_normal.png` | 1024² tangent-space normal map (OpenGL / +Y) |
| `bandage.blend` | Blender source file |
| `build_bandage.py` | Script that generates everything above |

- About 4.5k triangles, 2.3k vertices, one mesh, one material (`M_Bandage`).
- Real-world size: about 5 cm wide and 4.6 cm across (1 unit = 1 m). The pivot sits on the ground under the roll.
- The UVs repeat the texture every 5 cm along the strip, so leave texture wrap on **Repeat**.
- **Unreal:** tick *Flip Green Channel* on the normal map, because Unreal expects DirectX-style normal maps.

To change the width, number of wraps or hole size, edit the constants at the top of
`build_bandage.py` and run `pip install bpy numpy && python3 build_bandage.py`.
