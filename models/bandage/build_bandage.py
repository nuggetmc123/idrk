"""Builds a game-ready bandage roll (Rust / Strayed VR style) and exports it to FBX.

Run with:  pip install bpy numpy && python3 build_bandage.py
Outputs (next to this script): bandage.fbx, bandage.blend, textures/*.png
Units: metres. Roll axis is +Y; the roll sits on the ground (Z = 0).
"""
import math
import os

import bpy
import numpy as np

OUT = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)

WIDTH = 0.05          # bandage width (5 cm)
R_INNER = 0.009       # radius of the hole in the middle
TURNS = 7             # wraps in the roll
THICK = 0.0019        # cloth thickness per layer
GAP = 0.0004          # gap between layers so the wraps read on the sides
SEG_PER_TURN = 32
WIDTH_SEGS = 4
TILE = 0.05           # metres of cloth per texture repeat (along the strip)

PITCH = THICK + GAP
R_OUTER = R_INNER + THICK + PITCH * (TURNS - 1)
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- centre-line (x, z)
def build_path():
    pts = []
    n = TURNS * SEG_PER_TURN
    end = math.pi / 3                      # outer end of the cloth, on the upper side of the roll
    for i in range(n + 1):
        t = i / n
        th = end - 2 * math.pi * TURNS * (1 - t)
        r = R_OUTER - PITCH * TURNS * (1 - t)
        pts.append((r * math.cos(th), R_OUTER + r * math.sin(th)))
    return pts


def path_frames(pts):
    """Arc length + left normal (points inward on the spiral, up on the tail)."""
    s, out = 0.0, []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        tx, tz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, tz)
        tx, tz = tx / L, tz / L
        if i:
            s += math.hypot(p[0] - pts[i - 1][0], p[1] - pts[i - 1][1])
        out.append((p, (-tz, tx), s))
    return out


# ---------------------------------------------------------------- mesh
def build_mesh():
    frames = path_frames(build_path())
    verts, faces, uvs = [], [], []
    ring = (WIDTH_SEGS + 1) * 2
    last = len(frames) - 1
    for i, ((x, z), (nx, nz), s) in enumerate(frames):
        for side in (0, 1):            # 0 = outer/bottom face of the cloth, 1 = inner/top
            off = THICK * side
            for j in range(WIDTH_SEGS + 1):
                v = j / WIDTH_SEGS
                y = (v - 0.5) * WIDTH
                px, pz = x + nx * off, z + nz * off
                if i == last:          # roughly-cut outer end
                    px -= nz * rng.uniform(0, 0.0015)
                    pz += nx * rng.uniform(0, 0.0015)
                verts.append((px, y, pz))
    def vid(i, side, j):
        return i * ring + side * (WIDTH_SEGS + 1) + j

    for i in range(last):
        u0, u1 = frames[i][2] / TILE, frames[i + 1][2] / TILE
        for j in range(WIDTH_SEGS):
            v0, v1 = j / WIDTH_SEGS, (j + 1) / WIDTH_SEGS
            # outer face
            faces.append((vid(i, 0, j), vid(i, 0, j + 1), vid(i + 1, 0, j + 1), vid(i + 1, 0, j)))
            uvs.append(((u0, v0), (u0, v1), (u1, v1), (u1, v0)))
            # inner face
            faces.append((vid(i, 1, j), vid(i + 1, 1, j), vid(i + 1, 1, j + 1), vid(i, 1, j + 1)))
            uvs.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
        # side walls (the layered edges of the roll)
        for j, flip in ((0, False), (WIDTH_SEGS, True)):
            q = (vid(i, 0, j), vid(i + 1, 0, j), vid(i + 1, 1, j), vid(i, 1, j))
            faces.append(q[::-1] if flip else q)
            e = 0.04
            uv = ((u0, 0), (u1, 0), (u1, e), (u0, e))
            uvs.append(uv[::-1] if flip else uv)
    # end caps
    for i, flip in ((0, True), (last, False)):
        for j in range(WIDTH_SEGS):
            q = (vid(i, 0, j), vid(i, 1, j), vid(i, 1, j + 1), vid(i, 0, j + 1))
            faces.append(q[::-1] if flip else q)
            uvs.append(((0, j / WIDTH_SEGS), (0.04, j / WIDTH_SEGS),
                        (0.04, (j + 1) / WIDTH_SEGS), (0, (j + 1) / WIDTH_SEGS)))

    me = bpy.data.meshes.new("Bandage")
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    for poly, puv in zip(me.polygons, uvs):
        for li, uv in zip(poly.loop_indices, puv):
            uvl.data[li].uv = uv
    me.validate()
    me.update()
    # make sure everything faces outward
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
