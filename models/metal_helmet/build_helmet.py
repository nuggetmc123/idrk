"""
Scrap-metal helmet generator for PeeperLeeper (Rust / Strayed style).

Builds every part as its own object under a "MetalHelmet" empty, sized to the
PeeperLeeper head, then writes:
  MetalHelmet.fbx              - helmet only, positioned for the character
  PeeperLeeper_MetalHelmet.fbx - character + helmet parented to the Head bone
  MetalHelmet.blend            - editable source (modifiers + eye-hole cutters still live)

Run with Blender:   blender -b -P build_helmet.py -- <PeeperLeeper.fbx> [out_dir]
or with the bpy pip module:  python build_helmet.py <PeeperLeeper.fbx> [out_dir]

Character space (as Blender imports it): Z up, the face looks toward -Y,
+X is the character's left.
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
CHAR_FBX = argv[0] if argv else os.path.join(os.path.dirname(__file__), "PeeperLeeper.fbx")
OUT = os.path.abspath(argv[1] if len(argv) > 1 else os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)

random.seed(7)

# ---------------------------------------------------------------------------
# Fit parameters (all in character space). Tweak these to resize the helmet.
# ---------------------------------------------------------------------------
C = Vector((0.0, -0.006, 0.212))   # centre of the skull ellipsoid
RX, RY, RZ = 0.190, 0.180, 0.168   # inner radii of the shell
THICK = 0.005                      # sheet-metal thickness
DENT = 0.0016                      # how hammered/dented the plates look
TEX_M = 0.25                       # metres of helmet covered by one repeat of the rust texture


def rim_v(u):
    """Polar angle of the helmet's bottom edge at azimuth u (0 = front).
    High over the eyes, down over the ears, lowest at the back of the neck."""
    return 1.80 - 0.45 * math.cos(u) - 0.15 * math.cos(2 * u)


def surf(u, v, off=0.0):
    """Point on the shell ellipsoid grown by `off`."""
    return Vector((
        C.x + (RX + off) * math.sin(v) * math.sin(u),
        C.y - (RY + off) * math.sin(v) * math.cos(u),
        C.z + (RZ + off) * math.cos(v),
    ))


def surf_normal(u, v, off=0.0):
    p = surf(u, v, off) - C
    return Vector((p.x / (RX + off) ** 2, p.y / (RY + off) ** 2, p.z / (RZ + off) ** 2)).normalized()


# ---------------------------------------------------------------------------
# Scene setup
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=CHAR_FBX)
char_objects = list(bpy.data.objects)
arm = next(o for o in char_objects if o.type == "ARMATURE")

scene = bpy.context.scene
helmet_col = bpy.data.collections.new("MetalHelmet")
scene.collection.children.link(helmet_col)
cutter_col = bpy.data.collections.new("Cutters (edit eye holes here)")
scene.collection.children.link(cutter_col)

root = bpy.data.objects.new("MetalHelmet", None)
root.empty_display_type = "SPHERE"
root.empty_display_size = 0.05
root.location = C
helmet_col.objects.link(root)

group_empties = {}


def group(name):
    if name not in group_empties:
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.02
        helmet_col.objects.link(e)
        e.parent = root
        e.location = (0, 0, 0)
        group_empties[name] = e
    return group_empties[name]


# ---------------------------------------------------------------------------
# Procedural rust textures (tileable fractal noise via FFT)
# ---------------------------------------------------------------------------
def fractal(size, beta, seed):
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((size, size)))
    ky = np.fft.fftfreq(size)[:, None]
    kx = np.fft.fftfreq(size)[None, :]
    k = np.sqrt(kx ** 2 + ky ** 2)
    k[0, 0] = 1.0
    out = np.real(np.fft.ifft2(f / k ** beta))
    return (out - out.min()) / (out.max() - out.min())


def make_rust_texture(name, steel, rust, dark, rust_amount, seed, size=512):
    big = fractal(size, 1.6, seed)
    fine = fractal(size, 0.9, seed + 1)
    grain = fractal(size, 0.3, seed + 2)
    rust_mask = np.clip((big * 0.75 + fine * 0.25 - (1.0 - rust_amount)) * 6.0, 0, 1)
    dark_mask = np.clip((fine - 0.62) * 5.0, 0, 1) * rust_mask
    steel_c = np.array(steel)[None, None, :] * (0.85 + 0.3 * grain[..., None])
    rust_c = np.array(rust)[None, None, :] * (0.75 + 0.5 * fine[..., None])
    col = steel_c * (1 - rust_mask[..., None]) + rust_c * rust_mask[..., None]
    col = col * (1 - dark_mask[..., None]) + np.array(dark)[None, None, :] * dark_mask[..., None]
    col = np.clip(col, 0, 1)
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
        tex.location = (-350, 250)
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


MAT_PLATE = material("Rusty_Plate", (0.42, 0.26, 0.16), 0.55, 0.75,
                     make_rust_texture("rusty_plate", (0.42, 0.42, 0.44), (0.46, 0.21, 0.08),
                                       (0.16, 0.08, 0.04), 0.55, 11))
MAT_STEEL = material("Scrap_Steel", (0.35, 0.35, 0.37), 0.75, 0.55,
                     make_rust_texture("scrap_steel", (0.33, 0.34, 0.36), (0.40, 0.19, 0.08),
                                       (0.12, 0.07, 0.04), 0.30, 23))
MAT_BAND = material("Iron_Band", (0.22, 0.20, 0.19), 0.7, 0.65,
                    make_rust_texture("iron_band", (0.22, 0.21, 0.21), (0.35, 0.16, 0.07),
                                      (0.10, 0.06, 0.03), 0.40, 37))
MAT_WELD = material("Weld_Bead", (0.12, 0.11, 0.11), 0.6, 0.85)
MAT_RIVET = material("Rivet", (0.55, 0.53, 0.50), 0.9, 0.4)


# ---------------------------------------------------------------------------
# Object helpers
# ---------------------------------------------------------------------------
def finish(obj, mat, parent, thickness=THICK, bevel=True, smooth_angle=50):
    obj.data.materials.append(mat)
    helmet_col.objects.link(obj)
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
    # pivot at the part's own centre so it rotates/scales sensibly when edited
    pts = [v.co for v in obj.data.vertices]
    centre = sum(pts, Vector()) / len(pts)
    obj.data.transform(Matrix.Translation(-centre))
    obj.location = centre
    bpy.context.view_layer.update()
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(smooth_angle))
    return obj


def patch(name, u0, u1, vlo, vhi, nu, nv, off, mat, parent, dent=DENT, seed=0, close_u=False,
          thickness=THICK):
    """Curved sheet on the shell. vlo/vhi are functions of u (or numbers)."""
    vlo_f = vlo if callable(vlo) else (lambda u, c=vlo: c)
    vhi_f = vhi if callable(vhi) else (lambda u, c=vhi: c)
    bm = bmesh.new()
    cols = nu if close_u else nu + 1
    grid = []
    for i in range(cols):
        u = u0 + (u1 - u0) * i / nu
        a, b = vlo_f(u), vhi_f(u)
        row = []
        for j in range(nv + 1):
            v = a + (b - a) * j / nv
            p = surf(u, v, off)
            n = surf_normal(u, v, off)
            d = noise.noise(p * 28.0 + Vector((seed, seed * 1.7, 0))) * dent
            row.append(bm.verts.new(p + n * d))
        grid.append(row)
    # UVs straight from the surface grid, so the texture flows across each plate
    uv_layer = bm.loops.layers.uv.verify()
    ku, kv = (RX + off) / TEX_M, (RZ + off) / TEX_M
    for i in range(nu):
        i2 = (i + 1) % cols
        for j in range(nv):
            f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i2][j + 1], grid[i2][j]))
            for loop, (ci, cj) in zip(f.loops, ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j))):
                u = u0 + (u1 - u0) * ci / nu
                a, b = vlo_f(u), vhi_f(u)
                loop[uv_layer].uv = (u * ku, -(a + (b - a) * cj / nv) * kv)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    fix_normals_outward(obj)
    return finish(obj, mat, parent, thickness=thickness)


def fix_normals_outward(obj):
    me = obj.data
    me.update()
    outward = sum(((p.center - C).dot(p.normal) for p in me.polygons), 0.0)
    if outward < 0:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(me)
        bm.free()


def cap(name, vmax, rings, segs, off, mat, parent):
    bm = bmesh.new()
    top = bm.verts.new(surf(0, 0, off))
    rows = []
    for j in range(1, rings + 1):
        v = vmax * j / rings
        row = []
        for i in range(segs):
            u = 2 * math.pi * i / segs
            p = surf(u, v, off)
            d = noise.noise(p * 28.0 + Vector((3, 5, 0))) * DENT
            row.append(bm.verts.new(p + surf_normal(u, v, off) * d))
        rows.append(row)
    for i in range(segs):
        bm.faces.new((top, rows[0][i], rows[0][(i + 1) % segs]))
    for j in range(rings - 1):
        for i in range(segs):
            i2 = (i + 1) % segs
            bm.faces.new((rows[j][i], rows[j + 1][i], rows[j + 1][i2], rows[j][i2]))
    uv_layer = bm.loops.layers.uv.verify()      # planar from above
    for f in bm.faces:
        for loop in f.loops:
            loop[uv_layer].uv = (loop.vert.co.x / TEX_M, loop.vert.co.y / TEX_M)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    fix_normals_outward(obj)
    return finish(obj, mat, parent)


def rivet(name, u, v, off, parent, radius=0.0052, height=0.6):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=radius)
    for vert in [v_ for v_ in bm.verts if v_.co.z < -1e-6]:
        bm.verts.remove(vert)
    bmesh.ops.scale(bm, vec=(1, 1, height), verts=bm.verts)
    bmesh.ops.contextual_create(bm, geom=[e for e in bm.edges if e.is_boundary])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    obj.data.materials.append(MAT_RIVET)
    for p in obj.data.polygons:
        p.use_smooth = True
    helmet_col.objects.link(obj)
    n = surf_normal(u, v, off)
    obj.matrix_world = Matrix.Translation(surf(u, v, off) - n * 0.0005) @ n.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bpy.context.view_layer.update()
    mw = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = mw
    return obj


def weld(name, pts, parent, radius=0.0026):
    """Lumpy weld bead following a list of surface points."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 1
    cu.resolution_u = 1
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for p, co in zip(sp.points, pts):
        p.co = (*co, 1)
        p.radius = random.uniform(0.7, 1.3)
    tmp = bpy.data.objects.new(name + "_curve", cu)
    scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    obj = bpy.data.objects.new(name, me)
    return finish(obj, MAT_WELD, parent, thickness=0, bevel=False, smooth_angle=80)


def seam_points(fn, t0, t1, step=0.004):
    samples = [fn(t0 + (t1 - t0) * i / 64) for i in range(65)]
    length = sum((b - a).length for a, b in zip(samples, samples[1:]))
    n = max(4, int(length / step))
    return [fn(t0 + (t1 - t0) * i / n) for i in range(n + 1)]


# ---------------------------------------------------------------------------
# The helmet
# ---------------------------------------------------------------------------
plates = group("Plates")
CAP_V = 0.62
OFF_SIDE, OFF_FB, OFF_CAP = 0.0, 0.003, 0.0085
OFF_BAND = 0.0085
OFF_FACE = 0.016
d = math.radians

# Crown cap sits on top of the four lower plates (overlapping them).
cap("Plate_Crown", CAP_V, 6, 48, OFF_CAP, MAT_PLATE, plates)

# Four lower plates, front/back lapped over the sides.
band_top = 0.52
patch("Plate_Front", d(-50), d(50), band_top, rim_v, 18, 9, OFF_FB, MAT_PLATE, plates, seed=1)
patch("Plate_Back", d(130), d(230), band_top, rim_v, 18, 11, OFF_FB, MAT_PLATE, plates, seed=2)
patch("Plate_Side_L", d(40), d(140), band_top, rim_v, 18, 11, OFF_SIDE, MAT_PLATE, plates, seed=3)
patch("Plate_Side_R", d(220), d(320), band_top, rim_v, 18, 11, OFF_SIDE, MAT_PLATE, plates, seed=4)

# Iron rim band running all the way round the bottom edge.
patch("Rim_Band", 0, 2 * math.pi, lambda u: rim_v(u) - 0.16, lambda u: rim_v(u) + 0.015,
      72, 3, OFF_BAND, MAT_BAND, group("Rim"), dent=DENT * 0.4, seed=5, close_u=True)

# Flat bar welded over the crown, front to back. It steps down where it
# leaves the crown plate so it lies flat on the front/back plates.
def strip_off(v):
    t = min(1.0, max(0.0, (v - (CAP_V - 0.02)) / 0.05))
    return (OFF_CAP + THICK) * (1 - t) + (OFF_FB + THICK) * t


bm = bmesh.new()
uv_layer = bm.loops.layers.uv.verify()
strip, along = [], []
N = 40
for j in range(N + 1):
    t = -1.05 + 2.15 * j / N        # meridian angle: negative = front, positive = back
    u = 0.0 if t < 0 else math.pi
    v = abs(t)
    p = surf(u, v, strip_off(v))
    side = Vector((1, 0, 0))
    strip.append((bm.verts.new(p - side * 0.018), bm.verts.new(p + side * 0.018)))
    along.append(t * RZ / TEX_M)
for j in range(N):
    f = bm.faces.new((strip[j][0], strip[j][1], strip[j + 1][1], strip[j + 1][0]))
    for loop, (sx, k) in zip(f.loops, ((-1, j), (1, j), (1, j + 1), (-1, j + 1))):
        loop[uv_layer].uv = (sx * 0.018 / TEX_M, along[k])
me = bpy.data.meshes.new("Crown_Strip")
bm.to_mesh(me)
bm.free()
cs = bpy.data.objects.new("Crown_Strip", me)
fix_normals_outward(cs)
finish(cs, MAT_BAND, plates, thickness=0.004)

# Face plate with eye slots (Rust "metal facemask" vibe), hinged at the temples.
face_grp = group("Faceplate_Parts")
FZ_TOP, FZ_BOT = 0.282, 0.190
fr = RZ + OFF_FACE
face_v = lambda z: math.acos((z - C.z) / fr)
U_FACE = d(64)
face = patch("Faceplate", -U_FACE, U_FACE, face_v(FZ_TOP), face_v(FZ_BOT), 26, 8, OFF_FACE,
             MAT_STEEL, face_grp, dent=DENT * 0.6, seed=6)

EYE_Z = 0.229
for side, x in (("L", 0.056), ("R", -0.056)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=1.0, depth=0.2,
                                        location=(x, -0.2, EYE_Z), rotation=(math.pi / 2, 0, 0))
    cut = bpy.context.active_object
    cut.name = f"Cutter_EyeSlot_{side}"
    cut.scale = (0.040, 0.022, 1.0)
    cut.rotation_euler.y = math.radians(-8 if side == "L" else 8)   # slight angry tilt
    for c in cut.users_collection:
        c.objects.unlink(cut)
    cutter_col.objects.link(cut)
    cut.display_type = "WIRE"
    cut.hide_render = True
    bool_mod = face.modifiers.new(f"EyeSlot_{side}", "BOOLEAN")
    bool_mod.object = cut
    bool_mod.solver = "EXACT"
# breathing holes under the eyes
for i, x in enumerate((-0.03, 0.0, 0.03)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.0055, depth=0.2,
                                        location=(x, -0.2, 0.203), rotation=(math.pi / 2, 0, 0))
    cut = bpy.context.active_object
    cut.name = f"Cutter_Vent_{i + 1}"
    for c in cut.users_collection:
        c.objects.unlink(cut)
    cutter_col.objects.link(cut)
    cut.display_type = "WIRE"
    cut.hide_render = True
    bool_mod = face.modifiers.new(f"Vent_{i + 1}", "BOOLEAN")
    bool_mod.object = cut
    bool_mod.solver = "EXACT"
# bevel after the cuts so the hole edges get softened too
face.modifiers.move(face.modifiers.find("Edge Bevel"), len(face.modifiers) - 1)

# Hinge plates + bolts where the face plate meets the sides.
for side, u in (("L", d(57)), ("R", d(-57))):
    v = face_v(0.236)
    hp = patch(f"Hinge_Plate_{side}", u - d(9), u + d(9), v - 0.11, v + 0.11, 4, 4,
               OFF_FACE + THICK, MAT_BAND, face_grp, dent=0, thickness=0.004)
    rivet(f"Hinge_Bolt_{side}", u, v, OFF_FACE + THICK + 0.004, face_grp, radius=0.0085, height=0.7)

# Weld seams where the lower plates lap over each other.
welds = group("Welds")
for name, u in (("Weld_Front_L", d(50)), ("Weld_Front_R", d(-50)),
                ("Weld_Back_L", d(130)), ("Weld_Back_R", d(230))):
    weld(name, seam_points(lambda v, u=u: surf(u, v, OFF_FB + THICK), CAP_V - 0.02, rim_v(u) - 0.17), welds)
weld("Weld_Crown_Ring",
     seam_points(lambda t: surf(t, CAP_V, OFF_CAP + THICK * 0.6), 0, 2 * math.pi), welds)

# Rivets round the rim band (skipping the part the face plate covers).
rim_rivets = group("Rivets_Rim")
k = 0
for i in range(24):
    u = 2 * math.pi * i / 24 + d(7.5)
    v = rim_v(u) - 0.075
    z = surf(u, v).z
    uu = math.atan2(math.sin(u), math.cos(u))
    if abs(uu) < U_FACE + d(4) and FZ_BOT - 0.01 < z < FZ_TOP + 0.01:
        continue
    k += 1
    rivet(f"Rivet_Rim_{k:02d}", u, v, OFF_BAND + THICK, rim_rivets)

# Rivets along the crown edge.
crown_rivets = group("Rivets_Crown")
for i in range(12):
    u = 2 * math.pi * i / 12 + d(15)
    rivet(f"Rivet_Crown_{i + 1:02d}", u, CAP_V - 0.06, OFF_CAP + THICK, crown_rivets)
# Rivets on the crown strip.
for i, t in enumerate((-0.85, -0.45, 0.45, 0.85)):
    u = 0.0 if t < 0 else math.pi
    rivet(f"Rivet_Strip_{i + 1:02d}", u, abs(t), strip_off(abs(t)) + 0.004, crown_rivets, radius=0.0045)

# A scrap patch plate bodged onto the back-left.
patch_grp = group("Scrap_Patch_Parts")
pu, pv = d(150), 1.30
patch("Scrap_Patch", pu - d(16), pu + d(16), pv - 0.18, pv + 0.14, 4, 4, OFF_FB + THICK,
      MAT_STEEL, patch_grp, dent=DENT * 0.5, seed=8, thickness=0.004)
for i, (du, dv) in enumerate(((-12, -0.13), (12, -0.13), (-12, 0.10), (12, 0.10))):
    rivet(f"Rivet_Patch_{i + 1:02d}", pu + d(du), pv + dv, OFF_FB + THICK + 0.004, patch_grp,
          radius=0.0042)

# ---------------------------------------------------------------------------
# UVs
# ---------------------------------------------------------------------------
helmet_meshes = [o for o in helmet_col.objects if o.type == "MESH" and not o.data.uv_layers]
for o in helmet_meshes:
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------------------
# Fit check: no character vertex may poke through the helmet
# ---------------------------------------------------------------------------
dg = bpy.context.evaluated_depsgraph_get()


def shell_s(p, off):
    q = p - C
    return (q.x / (RX + off)) ** 2 + (q.y / (RY + off)) ** 2 + (q.z / (RZ + off)) ** 2


worst, bad = 9.0, 0
for o in char_objects:
    if o.type != "MESH":
        continue
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    for vert in me.vertices:
        p = o.matrix_world @ vert.co
        q = p - C
        u = math.atan2(q.x / RX, -q.y / RY)
        r = math.sqrt((q.x / RX) ** 2 + (q.y / RY) ** 2 + (q.z / RZ) ** 2)
        v = math.acos(max(-1, min(1, (q.z / RZ) / r)))
        if v < rim_v(u) + 0.015:
            s = math.sqrt(shell_s(p, 0))
            worst = min(worst, 1 - s)
            if s > 0.985:
                bad += 1
        # face plate band
        if FZ_BOT <= p.z <= FZ_TOP and abs(u) < U_FACE and shell_s(p, OFF_FACE) > 0.97:
            bad += 1
    ev.to_mesh_clear()
print(f"FIT CHECK: {bad} character vertices too close to/through the helmet "
      f"(tightest clearance {worst * 100:.1f}% of radius)")

# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


helmet_objs = [root] + list(root.children_recursive)
select_only(helmet_objs)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "MetalHelmet.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True,
                         mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True,
                         add_leaf_bones=False, bake_anim=False)

# Attach to the Head bone so it follows animation, then export with the character.
bpy.context.view_layer.update()
mw = root.matrix_world.copy()
root.parent = arm
root.parent_type = "BONE"
root.parent_bone = "Head"
bpy.context.view_layer.update()
root.matrix_world = mw
bpy.context.view_layer.update()

select_only(helmet_objs + char_objects)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PeeperLeeper_MetalHelmet.fbx"),
                         use_selection=True, object_types={"EMPTY", "MESH", "ARMATURE"},
                         use_mesh_modifiers=True, mesh_smooth_type="FACE", path_mode="COPY",
                         embed_textures=True, add_leaf_bones=False,
                         bake_anim=bool(bpy.data.actions))

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MetalHelmet.blend"))
print("parts:", len([o for o in helmet_objs if o.type == "MESH"]))
dg = bpy.context.evaluated_depsgraph_get()
print("tris:", sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons)
                   for o in helmet_objs if o.type == "MESH"))
