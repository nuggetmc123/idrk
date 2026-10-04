"""
Rust-style heavy plate helm for PeeperLeeper.

A tall, round-topped welded bucket with a narrow eye slit, framed by rebar bars
welded onto rusty sheet metal. Every part is its own object under a "HeavyHelm" empty.

Writes:
  HeavyHelm.fbx              - helmet only, positioned for the character
  PeeperLeeper_HeavyHelm.fbx - character + helmet parented to the Head bone
  HeavyHelm.blend            - editable source (modifiers + eye-slit cutter still live)

Run:  python build_heavy_helm.py PeeperLeeper.fbx [out_dir]      (pip install bpy)
 or:  blender -b -P build_heavy_helm.py -- PeeperLeeper.fbx [out_dir]

Character space as Blender imports it: Z up, face looks toward -Y, +X = character's left.
"""
import math
import os
import random
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector, noise

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
CHAR_FBX = argv[0] if argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), "PeeperLeeper.fbx")
OUT = os.path.abspath(argv[1] if len(argv) > 1 else os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)
random.seed(5)

# ---------------------------------------------------------------------------
# Fit / shape parameters (character space)
# ---------------------------------------------------------------------------
CY = -0.022            # centre of the bucket front-to-back
AX, AY = 0.192, 0.215  # half width / half depth of the walls
SQUARE = 3.2           # cross-section squareness (2 = oval, higher = boxier)
ZC = 0.300             # where the straight walls turn into the rounded top
RZ = 0.120             # height of the rounded top (top of helmet = ZC + RZ)
THICK = 0.006
DENT = 0.0014
TEX_M = 0.22           # metres per repeat of the rust texture
SLIT_Z, SLIT_H, SLIT_W = 0.229, 0.032, 0.125   # eye slit centre height, half height, half width
BAR_R = 0.0055         # rebar radius


def bottom_z(u):
    """Lower edge: chin height at the front, lower at the sides (cheek drops), up at the back."""
    c = math.cos(u)
    return 0.068 - 0.014 * (1 - c * c) + 0.026 * max(0.0, -c)


def se(u, off=0.0, s=1.0):
    """Superellipse cross-section point (x, y) at azimuth u (0 = front)."""
    su, cu = math.sin(u), math.cos(u)
    e = 2.0 / SQUARE
    return (math.copysign(abs(su) ** e, su) * (AX + off) * s,
            CY - math.copysign(abs(cu) ** e, cu) * (AY + off) * s)


def shell_pt(u, w, off=0.0):
    """Shell surface. w=0 at the very top, w=1 where the rounded top meets the
    walls, w>1 runs down the wall (w=2 is the lower edge)."""
    if w <= 1.0:
        phi = w * math.pi / 2
        x, y = se(u, off, math.sin(phi))
        return Vector((x, y, ZC + (RZ + off) * math.cos(phi)))
    x, y = se(u, off)
    return Vector((x, y, ZC + (bottom_z(u) - ZC) * (w - 1.0)))


def outward_dir(p):
    return (p - Vector((0, CY, min(p.z, ZC) if p.z < ZC else ZC))).normalized()


# ---------------------------------------------------------------------------
# Scene
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=CHAR_FBX)
char_objects = list(bpy.data.objects)
arm = next(o for o in char_objects if o.type == "ARMATURE")
scene = bpy.context.scene

col = bpy.data.collections.new("HeavyHelm")
scene.collection.children.link(col)
cutter_col = bpy.data.collections.new("Cutters (edit eye slit here)")
scene.collection.children.link(cutter_col)
root = bpy.data.objects.new("HeavyHelm", None)
root.empty_display_type = "CUBE"
root.empty_display_size = 0.05
root.location = (0, CY, 0.25)
col.objects.link(root)
_groups = {}


def group(name):
    if name not in _groups:
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.02
        col.objects.link(e)
        e.parent = root
        _groups[name] = e
    return _groups[name]


# ---------------------------------------------------------------------------
# Textures / materials
# ---------------------------------------------------------------------------
def fractal(size, beta, seed):
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((size, size)))
    k = np.sqrt(np.fft.fftfreq(size)[None, :] ** 2 + np.fft.fftfreq(size)[:, None] ** 2)
    k[0, 0] = 1.0
    out = np.real(np.fft.ifft2(f / k ** beta))
    return (out - out.min()) / (out.max() - out.min())


def rust_texture(name, base, rust, dark, amount, seed, size=512):
    big, fine, grain = fractal(size, 1.5, seed), fractal(size, 0.9, seed + 1), fractal(size, 0.25, seed + 2)
    streak = fractal(size, 1.0, seed + 3)
    streak = np.repeat(streak.mean(axis=0, keepdims=True), size, axis=0)   # vertical rust run-off
    mask = np.clip((big * 0.6 + fine * 0.25 + streak * 0.15 - (1 - amount)) * 5, 0, 1)[..., None]
    pits = np.clip((fine - 0.6) * 5, 0, 1)[..., None] * mask
    col = np.array(base) * (0.8 + 0.4 * grain[..., None])
    col = col * (1 - mask) + np.array(rust) * (0.7 + 0.6 * fine[..., None]) * mask
    col = np.clip(col * (1 - pits) + np.array(dark) * pits, 0, 1)
    rgba = np.concatenate([col, np.ones((size, size, 1))], axis=2).astype(np.float32)
    img = bpy.data.images.new(name, size, size)
    img.pixels.foreach_set(rgba.ravel())
    path = os.path.join(TEX, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.filepath = path
    return img


def material(name, color, metallic, roughness, image=None):
    m = bpy.data.materials.new(name)
    if m.node_tree is None:   # Blender 4.x
        m.use_nodes = True
    m.diffuse_color = (*color, 1)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if image:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = image
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


MAT_PLATE = material("Rusted_Plate", (0.24, 0.19, 0.16), 0.55, 0.8,
                     rust_texture("rusted_plate", (0.25, 0.22, 0.20), (0.34, 0.19, 0.11),
                                  (0.09, 0.06, 0.04), 0.56, 7))
MAT_BAR = material("Rebar", (0.42, 0.38, 0.34), 0.75, 0.6,
                   rust_texture("rebar", (0.48, 0.46, 0.43), (0.42, 0.22, 0.10), (0.15, 0.09, 0.05), 0.35, 19))
MAT_WELD = material("Weld_Bead", (0.10, 0.09, 0.09), 0.6, 0.85)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def finish(obj, mat, parent, thickness=THICK, bevel=True, smooth_angle=40, ref=None):
    me = obj.data
    me.update()
    ref = ref if ref is not None else Vector((0, CY, 0.25))
    if sum((p.center - ref).dot(p.normal) for p in me.polygons) < 0:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(me)
        bm.free()
    me.materials.append(mat)
    col.objects.link(obj)
    if thickness:
        sol = obj.modifiers.new("Thickness", "SOLIDIFY")
        sol.thickness = thickness
        sol.offset = 1.0
        sol.use_even_offset = True
    if bevel:
        bev = obj.modifiers.new("Edge Bevel", "BEVEL")
        bev.width = 0.0012
        bev.segments = 1
        bev.limit_method = "ANGLE"
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(smooth_angle))
    centre = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
    me.transform(Matrix.Translation(-centre))
    obj.location = centre
    bpy.context.view_layer.update()
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def shell_panel(name, u0, u1, w0, w1, nu, nv, off, parent, seed, close_u=False):
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    cols = nu if close_u else nu + 1
    g = []
    for i in range(cols):
        u = u0 + (u1 - u0) * i / nu
        row = []
        for j in range(nv + 1):
            w = w0 + (w1 - w0) * j / nv
            p = shell_pt(u, w, off)
            if 0 < j < nv and (close_u or 0 < i < nu):
                p = p + outward_dir(p) * noise.noise(p * 24 + Vector((seed, 2 * seed, 0))) * DENT
            row.append(bm.verts.new(p))
        g.append(row)
    # UVs: arc length around (u) and down (w), measured on the surface
    du = [0.0]
    for i in range(nu):
        a, b = shell_pt(u0 + (u1 - u0) * i / nu, 1.5, off), shell_pt(u0 + (u1 - u0) * (i + 1) / nu, 1.5, off)
        du.append(du[-1] + (a - b).length)
    for i in range(nu):
        i2 = (i + 1) % cols
        for j in range(nv):
            f = bm.faces.new((g[i][j], g[i2][j], g[i2][j + 1], g[i][j + 1]))
            for loop, (ci, cj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                w = w0 + (w1 - w0) * cj / nv
                if close_u:   # the top cap: planar from above, no pinching at the centre
                    co = loop.vert.co
                    loop[uv_layer].uv = (co.x / TEX_M, co.y / TEX_M)
                else:
                    loop[uv_layer].uv = (du[ci] / TEX_M + seed * 0.37,
                                         -(w * 0.13 if w <= 1 else 0.13 + (w - 1) * 0.23) / TEX_M)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return finish(bpy.data.objects.new(name, me), MAT_PLATE, parent)


def tube(name, pts, mat, parent, radius, ribbed=False, smooth_angle=70):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 1
    cu.resolution_u = 1
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for k, (p, co) in enumerate(zip(sp.points, pts)):
        p.co = (*co, 1)
        p.radius = (1.0 + (0.18 if k % 2 else -0.05)) if ribbed else random.uniform(0.6, 1.35)
    tmp = bpy.data.objects.new(name + "_curve", cu)
    scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    obj = bpy.data.objects.new(name, me)
    return finish(obj, mat, parent, thickness=0, bevel=False, smooth_angle=smooth_angle,
                  ref=sum(pts, Vector()) / len(pts) - outward_dir(sum(pts, Vector()) / len(pts)))


def resample(pts, step):
    out, acc = [pts[0]], 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length
        while acc + seg >= step:
            t = (step - acc) / seg
            a = a.lerp(b, t)
            seg = (b - a).length
            out.append(a)
            acc = 0.0
        acc += seg
    if (out[-1] - pts[-1]).length > step * 0.3:
        out.append(pts[-1])
    return out


def ring_at_z(z, u0, u1, off, n=80):
    """Horizontal path around the wall at height z."""
    return [Vector((*se(u0 + (u1 - u0) * i / n, off), z)) for i in range(n + 1)]


def meridian_x(x, z_front, z_back, off, n=200):
    """Path in the vertical plane at constant x: up the front wall, over the top, down the back."""
    pts = []
    # front wall: solve superellipse for y at this x
    def wall_y(s, sign):
        e = SQUARE
        if s < 1e-6:
            return None
        r = 1 - abs(x / ((AX + off) * s)) ** e
        if r <= 0:
            return None
        return CY + sign * (AY + off) * s * r ** (1 / e)
    for i in range(n + 1):
        z = z_front + (ZC - z_front) * i / n
        pts.append(Vector((x, wall_y(1.0, -1), z)))
    phis = [math.pi / 2 - (math.pi / 2) * i / n for i in range(n + 1)]
    top = []
    for phi in phis:
        y = wall_y(math.sin(phi), -1)
        if y is not None:
            top.append(Vector((x, y, ZC + (RZ + off) * math.cos(phi))))
    pts += top + [Vector((p.x, 2 * CY - p.y, p.z)) for p in reversed(top)]
    for i in range(n + 1):
        z = ZC + (z_back - ZC) * i / n
        pts.append(Vector((x, wall_y(1.0, 1), z)))
    return pts


# ---------------------------------------------------------------------------
# Shell plates
# ---------------------------------------------------------------------------
d = math.radians
plates = group("Plates")
W_SPLIT = 0.72          # rounded-top cap covers w 0..W_SPLIT
OFF_SIDE, OFF_FB, OFF_CAP = 0.0, 0.0025, 0.0065
shell_panel("Plate_Top", 0, 2 * math.pi, 0.0, W_SPLIT + 0.02, 32, 5, OFF_CAP, plates, 1, close_u=True)
front = shell_panel("Plate_Front", d(-48), d(48), W_SPLIT - 0.06, 2.0, 12, 11, OFF_FB, plates, 2)
shell_panel("Plate_Back", d(132), d(228), W_SPLIT - 0.06, 2.0, 12, 11, OFF_FB, plates, 3)
shell_panel("Plate_Side_L", d(40), d(140), W_SPLIT - 0.06, 2.0, 12, 11, OFF_SIDE, plates, 4)
shell_panel("Plate_Side_R", d(220), d(320), W_SPLIT - 0.06, 2.0, 12, 11, OFF_SIDE, plates, 5)

# Eye slit through the front plate
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, CY - AY, SLIT_Z))
cut = bpy.context.active_object
cut.name = "Cutter_EyeSlit"
cut.scale = (SLIT_W, 0.06, SLIT_H)
for c in cut.users_collection:
    c.objects.unlink(cut)
cutter_col.objects.link(cut)
cut.display_type = "WIRE"
cut.hide_render = True
b = front.modifiers.new("EyeSlit", "BOOLEAN")
b.object = cut
b.solver = "EXACT"
front.modifiers.move(front.modifiers.find("Edge Bevel"), len(front.modifiers) - 1)

# ---------------------------------------------------------------------------
# Rebar bars
# ---------------------------------------------------------------------------
bars = group("Rebar")
OFF_BAR = OFF_FB + THICK + BAR_R * 0.85
BAR_STEP = 0.0065
U_SLIT_END = math.asin(((SLIT_W + 0.016) / AX) ** (SQUARE / 2))   # azimuth just past the slit's ends
z_top_bar = SLIT_Z + SLIT_H + BAR_R + 0.002
z_low_bar = SLIT_Z - SLIT_H - BAR_R - 0.002


def bar(name, pts):
    return tube(name, resample(pts, BAR_STEP), MAT_BAR, bars, BAR_R, ribbed=True)


bar("Bar_Brow", ring_at_z(z_top_bar, -d(58), d(58), OFF_BAR))
bar("Bar_Cheek", ring_at_z(z_low_bar, -d(58), d(58), OFF_BAR))
bar("Bar_Chin", ring_at_z(0.112, -d(46), d(46), OFF_BAR))
for side, sgn in (("L", 1), ("R", -1)):
    x, y = se(sgn * U_SLIT_END, OFF_BAR)
    bar(f"Bar_SlitEnd_{side}", [Vector((x, y, z_low_bar - 0.012)), Vector((x, y, z_top_bar + 0.012))])
    bar(f"Bar_Crown_{side}", meridian_x(sgn * 0.052, z_top_bar - 0.004, 0.17, OFF_CAP + THICK + BAR_R * 0.85))

# ---------------------------------------------------------------------------
# Welds: plate seams and where the bars meet the plate
# ---------------------------------------------------------------------------
welds = group("Welds")
w_cap = W_SPLIT + 0.01
tube("Weld_Top_Seam", resample([shell_pt(2 * math.pi * i / 120, w_cap, OFF_CAP + THICK * 0.5)
                                for i in range(121)], 0.008), MAT_WELD, welds, 0.0032)
for name, u in (("Weld_Front_L", d(48)), ("Weld_Front_R", d(-48)),
                ("Weld_Back_L", d(132)), ("Weld_Back_R", d(228))):
    pts = [shell_pt(u, W_SPLIT + 0.05 + (1.97 - W_SPLIT - 0.05) * i / 60, OFF_FB + THICK * 0.6) for i in range(61)]
    tube(name, resample(pts, 0.008), MAT_WELD, welds, 0.003)
blobs = []
for side, sgn in (("L", 1), ("R", -1)):
    for z in (z_top_bar, z_low_bar):
        x, y = se(sgn * U_SLIT_END, OFF_BAR)
        blobs.append(Vector((x, y, z)))
    for z in (0.112,):
        x, y = se(sgn * d(46), OFF_BAR)
        blobs.append(Vector((x, y, z)))
for i, p in enumerate(blobs):
    tube(f"Weld_Blob_{i + 1}", [p + Vector((0, 0, -0.006)), p, p + Vector((0, 0, 0.006))], MAT_WELD, welds,
         BAR_R * 1.35)

# ---------------------------------------------------------------------------
# Fit check
# ---------------------------------------------------------------------------
dg = bpy.context.evaluated_depsgraph_get()
bad = []
for o in char_objects:
    if o.type != "MESH":
        continue
    me = o.evaluated_get(dg).to_mesh()
    for vert in me.vertices:
        p = o.matrix_world @ vert.co
        u = math.atan2(p.x / AX, -(p.y - CY) / AY)
        if p.z < bottom_z(u) or abs(p.x) > 0.3 or abs(p.y - CY) > 0.3:
            continue
        s = 1.0 if p.z <= ZC else math.sqrt(max(0.0, 1 - ((p.z - ZC) / RZ) ** 2))
        if s <= 0:
            bad.append(tuple(round(c, 3) for c in p))
            continue
        f = abs(p.x / (AX * s)) ** SQUARE + abs((p.y - CY) / (AY * s)) ** SQUARE
        if f > 0.93 ** SQUARE:
            bad.append(tuple(round(c, 3) for c in p))
    o.evaluated_get(dg).to_mesh_clear()
print(f"FIT CHECK: {len(bad)} character vertices too close to the helmet", bad[:6])

# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


helm_objs = [root] + list(root.children_recursive)
select_only(helm_objs)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "HeavyHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True, mesh_smooth_type="OFF",
                         path_mode="COPY", embed_textures=True, add_leaf_bones=False, bake_anim=False)

bpy.context.view_layer.update()
mw = root.matrix_world.copy()
root.parent = arm
root.parent_type = "BONE"
root.parent_bone = "Head"
bpy.context.view_layer.update()
root.matrix_world = mw
bpy.context.view_layer.update()

select_only(helm_objs + char_objects)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PeeperLeeper_HeavyHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH", "ARMATURE"}, use_mesh_modifiers=True,
                         mesh_smooth_type="OFF", path_mode="COPY", embed_textures=True,
                         add_leaf_bones=False, bake_anim=bool(bpy.data.actions))

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HeavyHelm.blend"))
dg = bpy.context.evaluated_depsgraph_get()
meshes = [o for o in helm_objs if o.type == "MESH"]
print("parts:", len(meshes))
print("tris:", sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in meshes))
