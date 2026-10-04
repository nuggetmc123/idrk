"""Builds a game-ready loose cloth bandage strip (Rust / Strayed VR style) and exports it to FBX.

Run with:  pip install bpy numpy && python3 build_bandage.py
Outputs (next to this script): bandage.fbx, bandage.blend, textures/*.png
Units: metres. The strip lies on the ground (Z = 0) and runs roughly along +X.
"""
import math
import os

import bpy
import numpy as np

OUT = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)

LENGTH = 0.30         # strip length (30 cm)
WIDTH = 0.05          # strip width (5 cm)
THICK = 0.0018        # cloth thickness
LEN_SEGS = 90
WIDTH_SEGS = 6
TILE = 0.05           # metres of cloth per texture repeat (along the strip)

rng = np.random.default_rng(7)


# ---------------------------------------------------------------- centre-line
def centre(t):
    """Point on the centre-line at t in [0, 1]: a lazy S-bend seen from above."""
    x = (t - 0.5) * LENGTH
    y = 0.035 * math.sin(t * math.pi * 1.6 - 0.4)
    return x, y


def height(t, v):
    """Lift of the cloth at (t along, v across) - soft wrinkles, rumpled edges, curled ends."""
    w = 2 * v - 1                                           # -1 .. 1 across the width
    z = 0.0030 * max(0.0, math.sin(t * math.pi * 6.0 + 0.8)) ** 2      # ridges across the strip
    z += 0.0022 * max(0.0, math.sin(t * math.pi * 3.0 + 2.0 + w)) ** 3  # diagonal fold
    z += 0.0012 * (w * w) * (0.5 + 0.5 * math.sin(t * 23.0))           # wavy edges
    z += 0.012 * max(0.0, (t - 0.88) / 0.12) ** 2 * (0.7 + 0.3 * w)     # one end curls up
    z += 0.004 * max(0.0, (0.06 - t) / 0.06) ** 2                       # other end lifts a touch
    return z


# ---------------------------------------------------------------- mesh
def build_mesh():
    rows = []
    s = 0.0
    prev = None
    for i in range(LEN_SEGS + 1):
        t = i / LEN_SEGS
        a, b = centre(max(t - 1e-3, 0)), centre(min(t + 1e-3, 1))
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty)
        tx, ty = tx / L, ty / L
        cx, cy = centre(t)
        if prev:
            s += math.hypot(cx - prev[0], cy - prev[1])
        prev = (cx, cy)
        rows.append((t, cx, cy, tx, ty, s))

    verts = []
    ring = (WIDTH_SEGS + 1) * 2
    for i, (t, cx, cy, tx, ty, _) in enumerate(rows):
        nx, ny = -ty, tx                                  # across the strip, in the ground plane
        for side in (0, 1):                               # 0 = underside, 1 = top
            for j in range(WIDTH_SEGS + 1):
                v = j / WIDTH_SEGS
                off = (v - 0.5) * WIDTH
                along = 0.0
                if i in (0, LEN_SEGS):                    # frayed, roughly-cut ends
                    along = rng.uniform(-0.004, 0.004) if 0 < j < WIDTH_SEGS else rng.uniform(-0.002, 0.0)
                    along *= -1 if i == 0 else 1
                elif j in (0, WIDTH_SEGS):                # slightly ragged long edges
                    off += rng.uniform(-0.0006, 0.0006) * (1 if j else -1)
                z = height(t, v) + THICK * side
                verts.append((cx + nx * off + tx * along, cy + ny * off + ty * along, z))

    def vid(i, side, j):
        return i * ring + side * (WIDTH_SEGS + 1) + j

    faces, uvs = [], []
    for i in range(LEN_SEGS):
        u0, u1 = rows[i][5] / TILE, rows[i + 1][5] / TILE
        for j in range(WIDTH_SEGS):
            v0, v1 = j / WIDTH_SEGS, (j + 1) / WIDTH_SEGS
            faces.append((vid(i, 0, j), vid(i + 1, 0, j), vid(i + 1, 0, j + 1), vid(i, 0, j + 1)))
            uvs.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
            faces.append((vid(i, 1, j), vid(i, 1, j + 1), vid(i + 1, 1, j + 1), vid(i + 1, 1, j)))
            uvs.append(((u0, v0), (u0, v1), (u1, v1), (u1, v0)))
        for j, flip in ((0, False), (WIDTH_SEGS, True)):   # long edges
            q = (vid(i, 0, j), vid(i, 1, j), vid(i + 1, 1, j), vid(i + 1, 0, j))
            uv = ((u0, 0), (u0, 0.04), (u1, 0.04), (u1, 0))
            faces.append(q[::-1] if flip else q)
            uvs.append(uv[::-1] if flip else uv)
    for i, flip in ((0, False), (LEN_SEGS, True)):          # cut ends
        for j in range(WIDTH_SEGS):
            q = (vid(i, 0, j), vid(i, 0, j + 1), vid(i, 1, j + 1), vid(i, 1, j))
            uv = ((0, j / WIDTH_SEGS), (0, (j + 1) / WIDTH_SEGS),
                  (0.04, (j + 1) / WIDTH_SEGS), (0.04, j / WIDTH_SEGS))
            faces.append(q[::-1] if flip else q)
            uvs.append(uv[::-1] if flip else uv)

    me = bpy.data.meshes.new("Bandage")
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    for poly, puv in zip(me.polygons, uvs):
        for li, uv in zip(poly.loop_indices, puv):
            uvl.data[li].uv = uv
    me.validate()
    me.update()
    obj = bpy.data.objects.new("Bandage", me)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.shade_auto_smooth(angle=math.radians(50))
    return obj


# ---------------------------------------------------------------- textures (tileable gauze)
def blur(a, r):
    for ax in (0, 1):
        acc = np.zeros_like(a)
        for d in range(-r, r + 1):
            acc += np.roll(a, d, axis=ax)
        a = acc / (2 * r + 1)
    return a


def make_textures(size=1024):
    yy, xx = np.mgrid[0:size, 0:size] / size
    threads = 48
    warp = 0.5 + 0.5 * np.cos(2 * np.pi * xx * threads)
    weft = 0.5 + 0.5 * np.cos(2 * np.pi * yy * threads)
    over = (np.floor(xx * threads) + np.floor(yy * threads)) % 2
    weave = np.where(over > 0, warp ** 0.6, weft ** 0.6)
    holes = (1 - warp ** 0.3) * (1 - weft ** 0.3)     # open mesh between the threads
    height = np.clip(weave * 0.8 - holes * 0.9, 0, 1)

    fine = blur(rng.random((size, size)), 2)
    blotch = blur(blur(rng.random((size, size)), 24), 24)
    blotch = (blotch - blotch.min()) / (np.ptp(blotch) + 1e-9)

    base = np.array([0.90, 0.87, 0.79])            # off-white cotton
    dirt = np.array([0.62, 0.55, 0.43])            # used / grubby look
    shade = 0.80 + 0.20 * height + 0.06 * (fine - 0.5)
    stain = np.clip((blotch - 0.55) * 2.2, 0, 1)[..., None] * 0.55
    col = (base * (1 - stain) + dirt * stain) * shade[..., None]
    col = np.clip(col, 0, 1)

    h = height + 0.15 * fine
    gx = np.roll(h, -1, 1) - np.roll(h, 1, 1)
    gy = np.roll(h, -1, 0) - np.roll(h, 1, 0)
    strength = 2.5
    n = np.dstack([-gx * strength, -gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    nrm = n * 0.5 + 0.5

    def save(name, arr, colorspace):
        img = bpy.data.images.new(name, size, size, alpha=False)
        rgba = np.dstack([arr, np.ones((size, size))]).astype(np.float32)
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = os.path.join(TEX, name + ".png")
        img.file_format = "PNG"
        img.save()
        img.colorspace_settings.name = colorspace
        return img

    # srgb-encode the albedo so it saves correctly as an 8-bit PNG
    srgb = np.where(col <= 0.0031308, col * 12.92, 1.055 * col ** (1 / 2.4) - 0.055)
    return save("bandage_albedo", srgb, "sRGB"), save("bandage_normal", nrm, "Non-Color")


def make_material(albedo, normal):
    mat = bpy.data.materials.new("M_Bandage")
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.92
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = albedo
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = normal
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = 0.8
    nt.links.new(ta.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    obj = build_mesh()
    obj.data.materials.append(make_material(*make_textures()))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "bandage.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, "bandage.fbx"),
        use_selection=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE",
        use_tspace=True,
        path_mode="COPY", embed_textures=True,
        bake_space_transform=True,
    )
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"verts={len(obj.data.vertices)} tris={tris} "
          f"size={tuple(round(d, 3) for d in obj.dimensions)}")


if __name__ == "__main__":
    main()
