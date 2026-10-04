"""
Welded scrap "bucket helm" for PeeperLeeper (Rust heavy-plate / Strayed style).

Eight flat scrap plates welded into an octagonal bucket, a chamfered lid, an eye
slit with a rebar bar, breathing holes, a brow bar, a neck guard and a road sign
bolted on top. Every part is its own object under a "ScrapHelm" empty.

Writes:
  ScrapHelm.fbx              - helmet only, positioned for the character
  PeeperLeeper_ScrapHelm.fbx - character + helmet parented to the Head bone
  ScrapHelm.blend            - editable source (modifiers + hole cutters still live)

Run:  python build_scrap_helm.py PeeperLeeper.fbx [out_dir]      (pip install bpy)
 or:  blender -b -P build_scrap_helm.py -- PeeperLeeper.fbx [out_dir]

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
random.seed(3)

# ---------------------------------------------------------------------------
# Fit parameters (character space). Tweak these to resize the helmet.
# ---------------------------------------------------------------------------
# Footprint corners for the +X half, front to back; mirrored for -X.
HALF = [(0.110, -0.235), (0.205, -0.130), (0.205, 0.070), (0.080, 0.185)]
Z_SIDE_TOP = 0.340      # where the walls stop and the chamfer starts
Z_LID = 0.400           # height of the flat lid
LID_SCALE = 0.62        # lid size relative to the walls
Z_BOTTOM_FRONT = 0.085  # lower edge at the front (covers the snout)
Z_BOTTOM_BACK = 0.105   # lower edge at the back
THICK = 0.006           # plate thickness
DENT = 0.0018           # hammered-dent strength
TEX_M = 0.25            # metres covered by one repeat of the rust texture
EYE_Z = 0.229           # eye slit height (PeeperLeeper's eyes are at z 0.227)

CORNERS = [Vector((x, y, 0)) for x, y in HALF] + \
          [Vector((-x, y, 0)) for x, y in reversed(HALF)]
# order: front-left(+x) ... back-left, back-right ... front-right; make it a loop
# starting at the front so facet 0 is the front plate
CORNERS = [CORNERS[-1]] + CORNERS[:-1]   # (-0.11,-0.235), (0.11,-0.235), ...
FACET_NAMES = ["Front", "Front_L", "Side_L", "Back_L", "Back", "Back_R", "Side_R", "Front_R"]
CENTRE = Vector((0, sum(c.y for c in CORNERS) / 8, 0))


def bottom_z(p):
    """Lower edge height at footprint point p (lower at the front)."""
    t = (p.y - CORNERS[0].y) / (CORNERS[4].y - CORNERS[0].y)
    return Z_BOTTOM_FRONT + (Z_BOTTOM_BACK - Z_BOTTOM_FRONT) * max(0.0, min(1.0, t))


def lid_point(c):
    return Vector((CENTRE.x + (c.x - CENTRE.x) * LID_SCALE, CENTRE.y + (c.y - CENTRE.y) * LID_SCALE, Z_LID))


# ---------------------------------------------------------------------------
# Scene
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=CHAR_FBX)
char_objects = list(bpy.data.objects)
arm = next(o for o in char_objects if o.type == "ARMATURE")
scene = bpy.context.scene

helm_col = bpy.data.collections.new("ScrapHelm")
scene.collection.children.link(helm_col)
cutter_col = bpy.data.collections.new("Cutters (edit holes here)")
scene.collection.children.link(cutter_col)

root = bpy.data.objects.new("ScrapHelm", None)
root.empty_display_type = "CUBE"
root.empty_display_size = 0.05
root.location = (0, CENTRE.y, 0.24)
helm_col.objects.link(root)
_groups = {}


def group(name):
    if name not in _groups:
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.02
        helm_col.objects.link(e)
        e.parent = root
        _groups[name] = e
    return _groups[name]


# ---------------------------------------------------------------------------
# Textures and materials
# ---------------------------------------------------------------------------
def fractal(size, beta, seed):
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((size, size)))
    k = np.sqrt(np.fft.fftfreq(size)[None, :] ** 2 + np.fft.fftfreq(size)[:, None] ** 2)
    k[0, 0] = 1.0
    out = np.real(np.fft.ifft2(f / k ** beta))
    return (out - out.min()) / (out.max() - out.min())


def save_image(name, rgb):
    size = rgb.shape[0]
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((size, size, 1))], axis=2).astype(np.float32)
    img = bpy.data.images.new(name, size, size)
    img.pixels.foreach_set(rgba.ravel())
    path = os.path.join(TEX, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.filepath = path
    return img


def rust_layer(base, rust_amount, seed, size=512):
    big, fine, grain = fractal(size, 1.6, seed), fractal(size, 0.9, seed + 1), fractal(size, 0.3, seed + 2)
    mask = np.clip((big * 0.7 + fine * 0.3 - (1 - rust_amount)) * 6, 0, 1)[..., None]
    pits = (np.clip((fine - 0.62) * 5, 0, 1)[..., None]) * mask
    rust = np.array((0.45, 0.20, 0.07)) * (0.7 + 0.6 * fine[..., None])
    col = base * (0.85 + 0.3 * grain[..., None])
    col = col * (1 - mask) + rust * mask
    return col * (1 - pits) + np.array((0.13, 0.06, 0.03)) * pits


def make_metal_tex(name, base, rust_amount, seed, size=512):
    return save_image(name, rust_layer(np.array(base)[None, None, :] * np.ones((size, size, 3)),
                                       rust_amount, seed, size))


def make_sign_tex(name, size=512):
    """Worn yellow/black chevron road sign."""
    yy, xx = np.mgrid[0:size, 0:size] / size
    stripes = ((xx + yy) * 6) % 1.0 < 0.5
    base = np.where(stripes[..., None], np.array((0.85, 0.62, 0.05)), np.array((0.05, 0.05, 0.05)))
    worn = fractal(size, 1.2, 91)
    bare = np.clip((worn - 0.55) * 5, 0, 1)[..., None]          # paint chipped to bare steel
    base = base * (1 - bare) + np.array((0.4, 0.4, 0.42)) * bare
    border = ((xx < 0.04) | (xx > 0.96) | (yy < 0.04) | (yy > 0.96))[..., None]
    base = np.where(border, np.array((0.06, 0.06, 0.06)), base)
    return save_image(name, rust_layer(base, 0.35, 95, size))


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


MAT_PLATE = material("Scrap_Plate", (0.35, 0.33, 0.31), 0.6, 0.75,
                     make_metal_tex("scrap_plate", (0.36, 0.36, 0.37), 0.5, 11))
MAT_DARK = material("Dark_Steel", (0.20, 0.19, 0.19), 0.7, 0.6,
                    make_metal_tex("dark_steel", (0.20, 0.20, 0.21), 0.35, 23))
MAT_SIGN = material("Road_Sign", (0.8, 0.6, 0.1), 0.3, 0.6, make_sign_tex("road_sign"))
MAT_WELD = material("Weld_Bead", (0.11, 0.10, 0.10), 0.6, 0.85)
MAT_BOLT = material("Bolt", (0.50, 0.48, 0.45), 0.9, 0.45)
MAT_REBAR = material("Rebar", (0.30, 0.17, 0.10), 0.7, 0.8)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def finish(obj, mat, parent, thickness=THICK, bevel=True, smooth_angle=35, solid_offset=1.0):
    obj.data.materials.append(mat)
    helm_col.objects.link(obj)
    if thickness:
        sol = obj.modifiers.new("Thickness", "SOLIDIFY")
        sol.thickness = thickness
        sol.offset = solid_offset
        sol.use_even_offset = True
    if bevel:
        bev = obj.modifiers.new("Edge Bevel", "BEVEL")
        bev.width = 0.0012
        bev.segments = 1
        bev.limit_method = "ANGLE"
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


def outward(obj, ref):
    """Flip faces so normals point away from point `ref`."""
    me = obj.data
    me.update()
    if sum((p.center - ref).dot(p.normal) for p in me.polygons) < 0:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(me)
        bm.free()


def quad_sheet(name, grid_fn, nu, nv, mat, parent, ref, dent=DENT, seed=0, thickness=THICK,
               jag_bottom=0.0):
    """Sheet from grid_fn(s, t) -> point, s,t in 0..1; t=0 is the bottom edge."""
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.verify()
    grid = []
    # physical size of the sheet, for evenly scaled UVs (random offset so plates differ)
    w = sum((grid_fn((i + 1) / 8, 0.5) - grid_fn(i / 8, 0.5)).length for i in range(8))
    h = sum((grid_fn(0.5, (j + 1) / 8) - grid_fn(0.5, j / 8)).length for j in range(8))
    uoff, voff = random.random(), random.random()
    for i in range(nu + 1):
        row = []
        for j in range(nv + 1):
            s, t = i / nu, j / nv
            p = grid_fn(s, t)
            if j == 0 and jag_bottom:
                p = p + Vector((0, 0, random.uniform(-jag_bottom, jag_bottom)))
            if 0 < i < nu and 0 < j < nv:   # dents inside the plate, edges stay straight
                n = (p - ref)
                n.z = 0 if abs(n.z) < 1e-9 else n.z
                p = p + n.normalized() * noise.noise(p * 26 + Vector((seed, seed * 2.3, 0))) * dent
            row.append(bm.verts.new(p))
        grid.append(row)
    for i in range(nu):
        for j in range(nv):
            f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            for loop, (ci, cj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv_layer].uv = (uoff + ci / nu * w / TEX_M, voff + cj / nv * h / TEX_M)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    outward(obj, ref)
    return finish(obj, mat, parent, thickness=thickness)


def bolt(name, pos, normal, parent, radius=0.0055, height=0.004, sides=6):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=radius, radius2=radius * 0.8,
                          depth=height)
    bmesh.ops.translate(bm, vec=(0, 0, height / 2), verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    obj.data.materials.append(MAT_BOLT)
    helm_col.objects.link(obj)
    obj.matrix_world = Matrix.Translation(pos) @ normal.to_track_quat("Z", "Y").to_matrix().to_4x4() \
        @ Matrix.Rotation(random.uniform(0, math.pi), 4, "Z")
    bpy.context.view_layer.update()
    mw = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = mw
    return obj


def weld(name, pts, parent, radius=0.0035):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 1
    cu.resolution_u = 1
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for p, co in zip(sp.points, pts):
        p.co = (*co, 1)
        p.radius = random.uniform(0.6, 1.35)
    tmp = bpy.data.objects.new(name + "_curve", cu)
    scene.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    return finish(bpy.data.objects.new(name, me), MAT_WELD, parent, thickness=0, bevel=False,
                  smooth_angle=80)


def line(a, b, step=0.005):
    n = max(3, int((b - a).length / step))
    return [a.lerp(b, i / n) for i in range(n + 1)]


def cutter(name, target, shape, loc, scale, rot=(math.pi / 2, 0, 0)):
    if shape == "box":
        bpy.ops.mesh.primitive_cube_add(size=2, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=1, depth=2, location=loc, rotation=rot)
    c = bpy.context.active_object
    c.name = name
    c.scale = scale
    for col in c.users_collection:
        col.objects.unlink(c)
    cutter_col.objects.link(c)
    c.display_type = "WIRE"
    c.hide_render = True
    m = target.modifiers.new(name.replace("Cutter_", ""), "BOOLEAN")
    m.object = c
    m.solver = "EXACT"
    target.modifiers.move(target.modifiers.find("Edge Bevel"), len(target.modifiers) - 1)


# ---------------------------------------------------------------------------
# Walls: 8 flat plates
# ---------------------------------------------------------------------------
walls = group("Wall_Plates")
ref = Vector((CENTRE.x, CENTRE.y, 0.24))
wall_objs = []
for k, name in enumerate(FACET_NAMES):
    a, b = CORNERS[k], CORNERS[(k + 1) % 8]

    def fn(s, t, a=a, b=b):
        p = a.lerp(b, s)
        zb = bottom_z(p)
        return Vector((p.x, p.y, zb + (Z_SIDE_TOP - zb) * t))

    nu = max(2, int((b - a).length / 0.025))
    wall_objs.append(quad_sheet(f"Plate_{name}", fn, nu, 8, MAT_PLATE, walls, ref, seed=k,
                                jag_bottom=0.004))
front = wall_objs[0]

# ---------------------------------------------------------------------------
# Lid: 8 chamfer plates + top plate
# ---------------------------------------------------------------------------
lid = group("Lid")
for k, name in enumerate(FACET_NAMES):
    a, b = CORNERS[k], CORNERS[(k + 1) % 8]
    a0, b0 = Vector((a.x, a.y, Z_SIDE_TOP)), Vector((b.x, b.y, Z_SIDE_TOP))
    a1, b1 = lid_point(a), lid_point(b)
    quad_sheet(f"Chamfer_{name}", lambda s, t, a0=a0, b0=b0, a1=a1, b1=b1: a0.lerp(b0, s).lerp(a1.lerp(b1, s), t),
               3, 2, MAT_PLATE, lid, ref, dent=DENT * 0.5, seed=10 + k)

bm = bmesh.new()
uv_layer = bm.loops.layers.uv.verify()
top_c = bm.verts.new(Vector((CENTRE.x, CENTRE.y, Z_LID + 0.004)))
ring = [bm.verts.new(lid_point(c)) for c in CORNERS]
for k in range(8):
    f = bm.faces.new((top_c, ring[k], ring[(k + 1) % 8]))
    for loop in f.loops:
        loop[uv_layer].uv = (loop.vert.co.x / TEX_M, loop.vert.co.y / TEX_M)
me = bpy.data.meshes.new("Lid_Top")
bm.to_mesh(me)
bm.free()
lid_top = bpy.data.objects.new("Lid_Top", me)
outward(lid_top, ref)
finish(lid_top, MAT_PLATE, lid)

# ---------------------------------------------------------------------------
# Face: eye slit, breathing holes, brow bar, rebar
# ---------------------------------------------------------------------------
face = group("Face_Parts")
fy = CORNERS[0].y            # front plane (plates grow outward to fy - THICK)
cutter("Cutter_EyeSlit", front, "box", (0, fy, EYE_Z), (0.090, 0.03, 0.017), rot=(0, 0, 0))
for r, z in enumerate((0.165, 0.138)):
    for c, x in enumerate((-0.045, -0.015, 0.015, 0.045)):
        cutter(f"Cutter_Breath_{r + 1}{c + 1}", front, "cyl", (x + (0.015 if r else 0), fy, z),
               (0.0055, 0.0055, 0.03))

out_y = fy - THICK
quad_sheet("Brow_Bar", lambda s, t: Vector((-0.118 + 0.236 * s, out_y, 0.254 + 0.018 * t)),
           6, 1, MAT_DARK, face, Vector((0, 0, 0.26)), dent=0, thickness=0.007)

bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.0045, radius2=0.0045, depth=0.085)
bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 0.01],
                          cuts=24, use_grid_fill=True)
for v in bm.verts:   # rebar ribs
    v.co.x *= 1 + 0.12 * math.sin(v.co.z * 400)
    v.co.y *= 1 + 0.12 * math.sin(v.co.z * 400)
me = bpy.data.meshes.new("Rebar_Bar")
bm.to_mesh(me)
bm.free()
rebar = bpy.data.objects.new("Rebar_Bar", me)
rebar.data.transform(Matrix.Translation((0.0, out_y - 0.004, EYE_Z)))
finish(rebar, MAT_REBAR, face, thickness=0, bevel=False, smooth_angle=60)

# ---------------------------------------------------------------------------
# Neck guard flap at the back
# ---------------------------------------------------------------------------
neck = group("Neck_Guard_Parts")
nb_a = Vector((CORNERS[4].x, CORNERS[4].y + THICK + 0.001, Z_BOTTOM_BACK + 0.03))
nb_b = Vector((CORNERS[5].x, CORNERS[5].y + THICK + 0.001, Z_BOTTOM_BACK + 0.03))
drop = Vector((0, 0.035, -0.07))
quad_sheet("Neck_Guard", lambda s, t: nb_a.lerp(nb_b, s) + drop * (1 - t), 4, 3, MAT_DARK, neck,
           Vector((0, 0, 0.2)), dent=DENT * 0.6, seed=40, thickness=0.005)

# ---------------------------------------------------------------------------
# Road sign bolted on the lid
# ---------------------------------------------------------------------------
sign_grp = group("Road_Sign_Parts")
sm = Matrix.Translation((0.01, CENTRE.y - 0.01, Z_LID + 0.007)) @ Matrix.Rotation(math.radians(17), 4, "Z") \
    @ Matrix.Rotation(math.radians(-2), 4, "X")
quad_sheet("Road_Sign", lambda s, t: sm @ Vector((-0.12 + 0.24 * s, -0.09 + 0.18 * t, 0)), 6, 5,
           MAT_SIGN, sign_grp, sm @ Vector((0, 0, -1)), dent=DENT * 0.6, seed=50, thickness=0.003)
sign = bpy.data.objects["Road_Sign"]
uv = sign.data.uv_layers.active.data
for poly in sign.data.polygons:   # map the sign texture once across the whole plate
    for li in poly.loop_indices:
        co = sm.inverted() @ (sign.matrix_world @ sign.data.vertices[sign.data.loops[li].vertex_index].co)
        uv[li].uv = ((co.x + 0.12) / 0.24, (co.y + 0.09) / 0.18)
sn = (sm.to_3x3() @ Vector((0, 0, 1))).normalized()
for i, (x, y) in enumerate(((-0.1, -0.07), (0.1, -0.07), (-0.1, 0.07), (0.1, 0.07))):
    bolt(f"Bolt_Sign_{i + 1}", sm @ Vector((x, y, 0.003)), sn, sign_grp, radius=0.006)

# ---------------------------------------------------------------------------
# Welds and bolts
# ---------------------------------------------------------------------------
welds = group("Welds")
for k, name in enumerate(FACET_NAMES):
    c = CORNERS[(k + 1) % 8]
    out = (Vector((c.x, c.y, 0)) - Vector((CENTRE.x, CENTRE.y, 0))).normalized() * (THICK * 0.8)
    base = Vector((c.x, c.y, bottom_z(c) + 0.006)) + out
    weld(f"Weld_Corner_{k + 1}", line(base, Vector((c.x, c.y, Z_SIDE_TOP)) + out), welds)
ring_pts = []
for k in range(9):
    c = CORNERS[k % 8]
    out = (Vector((c.x, c.y, 0)) - Vector((CENTRE.x, CENTRE.y, 0))).normalized() * (THICK * 0.8)
    if k:
        ring_pts += line(prev, Vector((c.x, c.y, Z_SIDE_TOP)) + out)[1:]
    prev = Vector((c.x, c.y, Z_SIDE_TOP)) + out
weld("Weld_Lid_Ring", ring_pts, welds)
weld("Weld_Brow", line(Vector((-0.118, out_y - 0.002, 0.2725)), Vector((0.118, out_y - 0.002, 0.2725))), welds,
     radius=0.003)

bolts = group("Bolts")
n_b = 0
for k in range(8):
    a, b = CORNERS[k], CORNERS[(k + 1) % 8]
    n = Vector((b.y - a.y, -(b.x - a.x), 0)).normalized()
    if n.dot(a - CENTRE) < 0:
        n = -n
    for s in (0.22, 0.78):
        p = a.lerp(b, s)
        for z in (bottom_z(p) + 0.022, Z_SIDE_TOP - 0.02):
            if k == 0 and z > 0.2:     # brow bar already there
                continue
            n_b += 1
            bolt(f"Bolt_{n_b:02d}", Vector((p.x, p.y, z)) + n * THICK, n, bolts)
for s in (0.2, 0.8):
    p = nb_a.lerp(nb_b, s) + Vector((0, 0.005, -0.008))
    bolt(f"Bolt_Neck_{int(s * 10)}", p, Vector((0, 1, 0)), neck)
for x in (-0.105, 0.105):
    bolt(f"Bolt_Brow_{'L' if x > 0 else 'R'}", Vector((x, out_y - 0.007, 0.263)), Vector((0, -1, 0)), face,
         radius=0.0045)

# UVs for anything without them
for o in [o for o in helm_col.objects if o.type == "MESH" and not o.data.uv_layers]:
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------------------
# Fit check: nothing of the character may poke through the walls or lid
# ---------------------------------------------------------------------------
def inside_footprint(x, y, scale, margin):
    for k in range(8):
        a, b = CORNERS[k], CORNERS[(k + 1) % 8]
        a = CENTRE + (a - CENTRE) * scale
        b = CENTRE + (b - CENTRE) * scale
        n = Vector((b.y - a.y, -(b.x - a.x), 0)).normalized()
        if n.dot(a - CENTRE) < 0:
            n = -n
        if n.dot(Vector((x, y, 0)) - a) > -margin:
            return False
    return True


dg = bpy.context.evaluated_depsgraph_get()
bad, near = 0, []
for o in char_objects:
    if o.type != "MESH":
        continue
    me = o.evaluated_get(dg).to_mesh()
    for v in me.vertices:
        p = o.matrix_world @ v.co
        if p.z < bottom_z(p) - 0.005 or not inside_footprint(p.x, p.y, 1.25, 0):
            continue            # below the helmet, or well outside it (arms etc.)
        if p.z > Z_SIDE_TOP:
            sc = 1 + (LID_SCALE - 1) * (p.z - Z_SIDE_TOP) / (Z_LID - Z_SIDE_TOP)
        else:
            sc = 1.0
        if p.z > Z_LID - 0.008 or not inside_footprint(p.x, p.y, sc, 0.006):
            bad += 1
            near.append(tuple(round(c, 3) for c in p))
    o.evaluated_get(dg).to_mesh_clear()
print(f"FIT CHECK: {bad} character vertices within 6 mm of / through the helmet", near[:8])

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
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "ScrapHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True,
                         mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True,
                         add_leaf_bones=False, bake_anim=False)

bpy.context.view_layer.update()
mw = root.matrix_world.copy()
root.parent = arm
root.parent_type = "BONE"
root.parent_bone = "Head"
bpy.context.view_layer.update()
root.matrix_world = mw
bpy.context.view_layer.update()

select_only(helm_objs + char_objects)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PeeperLeeper_ScrapHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH", "ARMATURE"}, use_mesh_modifiers=True,
                         mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True,
                         add_leaf_bones=False, bake_anim=bool(bpy.data.actions))

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "ScrapHelm.blend"))
dg = bpy.context.evaluated_depsgraph_get()
meshes = [o for o in helm_objs if o.type == "MESH"]
print("parts:", len(meshes))
print("tris:", sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in meshes))
