"""
Stylized low-poly combat helm for PeeperLeeper (Gorilla Tag / Strayed style).

Flat-shaded facets and solid colours to match the character: a snug gunmetal
dome, an angry slanted brow plate, a muzzle guard with grill slots, cheek
guards, a rust-orange crest and chunky bolts. Every part is its own object
under a "CombatHelm" empty.

Writes:
  CombatHelm.fbx              - helmet only, positioned for the character
  PeeperLeeper_CombatHelm.fbx - character + helmet parented to the Head bone
  CombatHelm.blend            - editable source (modifiers + grill cutters still live)

Run:  python build_combat_helm.py PeeperLeeper.fbx [out_dir]      (pip install bpy)
 or:  blender -b -P build_combat_helm.py -- PeeperLeeper.fbx [out_dir]

Character space as Blender imports it: Z up, face looks toward -Y, +X = character's left.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
CHAR_FBX = argv[0] if argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), "PeeperLeeper.fbx")
OUT = os.path.abspath(argv[1] if len(argv) > 1 else os.path.dirname(os.path.abspath(__file__)))

# ---------------------------------------------------------------------------
# Fit parameters (character space). Tweak these to resize or restyle.
# ---------------------------------------------------------------------------
C = Vector((0.0, -0.006, 0.212))    # centre of the dome ellipsoid
RX, RY, RZ = 0.185, 0.179, 0.166    # dome inner radii
THICK = 0.009                       # plate thickness
SEG = 20                            # facets around the dome (lower = chunkier)
BROW_LOW, BROW_HIGH = 0.247, 0.282  # brow bottom edge: centre (low) -> outer (high)
VISOR_BOTTOM = 0.196                # top edge of the muzzle guard
MUZZLE_BOTTOM = 0.088

d = math.radians


def rim_v(u):
    """Polar angle of the dome's lower edge at azimuth u (0 = front)."""
    return 1.713 - 0.52 * math.cos(u) - 0.113 * math.cos(2 * u)


def surf(u, v, off=0.0):
    return Vector((C.x + (RX + off) * math.sin(v) * math.sin(u),
                   C.y - (RY + off) * math.sin(v) * math.cos(u),
                   C.z + (RZ + off) * math.cos(v)))


def surf_normal(u, v, off=0.0):
    p = surf(u, v, off) - C
    return Vector((p.x / (RX + off) ** 2, p.y / (RY + off) ** 2, p.z / (RZ + off) ** 2)).normalized()


def v_at_z(z, off):
    return math.acos(max(-1.0, min(1.0, (z - C.z) / (RZ + off))))


def brow_bottom(u):
    t = min(1.0, abs(u) / d(42))
    return BROW_LOW + (BROW_HIGH - BROW_LOW) * (t * t * (3 - 2 * t))


# ---------------------------------------------------------------------------
# Scene
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=CHAR_FBX)
char_objects = list(bpy.data.objects)
arm = next(o for o in char_objects if o.type == "ARMATURE")
scene = bpy.context.scene

col = bpy.data.collections.new("CombatHelm")
scene.collection.children.link(col)
cutter_col = bpy.data.collections.new("Cutters (edit grill slots here)")
scene.collection.children.link(cutter_col)

root = bpy.data.objects.new("CombatHelm", None)
root.empty_display_type = "SPHERE"
root.empty_display_size = 0.05
root.location = C
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


def material(name, color, metallic, roughness):
    m = bpy.data.materials.new(name)
    if m.node_tree is None:   # Blender 4.x
        m.use_nodes = True
    m.diffuse_color = (*color, 1)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


GUNMETAL = material("Gunmetal", (0.11, 0.12, 0.14), 0.7, 0.42)
STEEL = material("Steel_Trim", (0.42, 0.44, 0.47), 0.85, 0.35)
ACCENT = material("Rust_Orange", (0.90, 0.24, 0.03), 0.2, 0.55)
BOLT = material("Bolt", (0.62, 0.62, 0.64), 0.9, 0.3)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def finish(obj, mat, parent, thickness=THICK, ref=None):
    me = obj.data
    me.update()
    ref = ref if ref is not None else C
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
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(32))   # smooth surfaces, crisp plate edges
    centre = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
    me.transform(Matrix.Translation(-centre))
    obj.location = centre
    bpy.context.view_layer.update()
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def grid_object(name, fn, nu, nv, mat, parent, thickness=THICK, ref=None, close_u=False):
    """Sheet from fn(s, t) -> point with s, t in 0..1."""
    bm = bmesh.new()
    cols = nu if close_u else nu + 1
    g = [[bm.verts.new(fn(i / nu, j / nv)) for j in range(nv + 1)] for i in range(cols)]
    for i in range(nu):
        i2 = (i + 1) % cols
        for j in range(nv):
            bm.faces.new((g[i][j], g[i2][j], g[i2][j + 1], g[i][j + 1]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return finish(bpy.data.objects.new(name, me), mat, parent, thickness, ref)


def bolt(name, pos, normal, parent, radius=0.009, height=0.006):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=radius, radius2=radius * 0.7, depth=height)
    bmesh.ops.translate(bm, vec=(0, 0, height / 2), verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    me.materials.append(BOLT)
    col.objects.link(obj)
    obj.matrix_world = Matrix.Translation(pos) @ normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bpy.context.view_layer.update()
    mw = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = mw
    return obj


# ---------------------------------------------------------------------------
# Dome
# ---------------------------------------------------------------------------
shell = group("Shell")
bm = bmesh.new()
RINGS = 7
top = bm.verts.new(surf(0, 0))
rows = []
for j in range(1, RINGS + 1):
    row = []
    for i in range(SEG):
        u = 2 * math.pi * i / SEG
        row.append(bm.verts.new(surf(u, rim_v(u) * j / RINGS)))
    rows.append(row)
for i in range(SEG):
    bm.faces.new((top, rows[0][i], rows[0][(i + 1) % SEG]))
for j in range(RINGS - 1):
    for i in range(SEG):
        i2 = (i + 1) % SEG
        bm.faces.new((rows[j][i], rows[j + 1][i], rows[j + 1][i2], rows[j][i2]))
me = bpy.data.meshes.new("Dome")
bm.to_mesh(me)
bm.free()
finish(bpy.data.objects.new("Dome", me), GUNMETAL, shell)

# Steel trim round the back and sides of the dome's lower edge
O_TRIM = THICK + 0.001
grid_object("Rim_Trim", lambda s, t: surf(d(75) + d(210) * s,
                                          rim_v(d(75) + d(210) * s) - 0.10 + 0.115 * t, O_TRIM),
            14, 1, STEEL, shell, thickness=0.006)

# ---------------------------------------------------------------------------
# Brow plate: slanted down toward the centre for an angry look
# ---------------------------------------------------------------------------
face = group("Face")
O_BROW = THICK + 0.002
BROW_U = d(78)


def brow_pt(s, t):
    u = -BROW_U + 2 * BROW_U * s
    lo = v_at_z(brow_bottom(u), O_BROW)
    hi = v_at_z(0.312 if abs(u) < d(50) else 0.312 - (abs(u) - d(50)) * 0.06, O_BROW)
    return surf(u, lo + (hi - lo) * t, O_BROW)


grid_object("Brow_Plate", brow_pt, 16, 2, STEEL, face, thickness=0.011)

# ---------------------------------------------------------------------------
# Cheek guards (hang below the dome on each side)
# ---------------------------------------------------------------------------
O_CHEEK = THICK + 0.002
for side, sgn in (("L", 1), ("R", -1)):
    def cheek(s, t, sgn=sgn):
        u = sgn * (d(50) + d(58) * s)
        top_v = rim_v(u) - 0.18
        bot_v = v_at_z(0.112 + 0.02 * s, O_CHEEK)
        return surf(u, top_v + (bot_v - top_v) * t, O_CHEEK)
    grid_object(f"Cheek_Guard_{side}", cheek, 4, 3, GUNMETAL, face)

# ---------------------------------------------------------------------------
# Muzzle guard over the snout (elliptical, tapers in toward the chin)
# ---------------------------------------------------------------------------
MU = d(80)


def muzzle_xy(a, t):
    """Pointed cross-section: sticks out at the snout, tucks back at the sides."""
    k = 0.92 + 0.08 * t                # narrower at the chin
    front = 0.226 + 0.01 * min(t, 0.6) / 0.6 - 0.028 * max(0.0, t - 0.6) / 0.4   # beak-like side profile
    reach = 0.158 + (front - 0.158) * math.cos(a) ** 2
    return 0.166 * k * math.sin(a), -0.006 - reach * math.cos(a)


def muzzle(s, t):
    a = -MU + 2 * MU * s
    z = MUZZLE_BOTTOM + (VISOR_BOTTOM - MUZZLE_BOTTOM) * t
    x, y = muzzle_xy(a, t)
    return Vector((x, y, z))


muz = grid_object("Muzzle_Guard", muzzle, 16, 5, GUNMETAL, face, ref=Vector((0, 0, 0.14)))
for i, x in enumerate((-0.036, 0.0, 0.036)):
    bpy.ops.mesh.primitive_cube_add(size=2, location=(x, -0.235, 0.137))
    cut = bpy.context.active_object
    cut.name = f"Cutter_Grill_{i + 1}"
    cut.scale = (0.0075, 0.04, 0.03)
    for c in cut.users_collection:
        c.objects.unlink(cut)
    cutter_col.objects.link(cut)
    cut.display_type = "WIRE"
    cut.hide_render = True
    b = muz.modifiers.new(f"Grill_{i + 1}", "BOOLEAN")
    b.object = cut
    b.solver = "EXACT"

# steel lip along the top of the muzzle guard
grid_object("Muzzle_Lip", lambda s, t: muzzle(0.06 + 0.88 * s, 1.0) + Vector((0, 0, -0.014 * (1 - t)))
            + (muzzle(0.06 + 0.88 * s, 1.0) - Vector((0, -0.006, muzzle(0.06 + 0.88 * s, 1.0).z))).normalized()
            * (THICK + 0.001),
            14, 1, STEEL, face, thickness=0.004, ref=Vector((0, 0, 0.14)))

# ---------------------------------------------------------------------------
# Crest down the middle
# ---------------------------------------------------------------------------
crest_grp = group("Crest")
bm = bmesh.new()
N = 14
ring = []
for j in range(N + 1):
    t = -0.72 + 2.52 * j / N           # negative = front, positive = back
    u, v = (0.0, -t) if t < 0 else (math.pi, t)
    h = 0.004 + 0.012 * min(1.0, j / 3, (N - j) / 3)
    base = surf(u, v, THICK - 0.001)
    n = surf_normal(u, v)
    side = Vector((0.016, 0, 0))
    ring.append((bm.verts.new(base - side), bm.verts.new(base + n * h), bm.verts.new(base + side)))
for j in range(N):
    a, b = ring[j], ring[j + 1]
    bm.faces.new((a[0], b[0], b[1], a[1]))
    bm.faces.new((a[1], b[1], b[2], a[2]))
bm.faces.new(ring[0])
bm.faces.new(tuple(reversed(ring[-1])))
me = bpy.data.meshes.new("Crest")
bm.to_mesh(me)
bm.free()
finish(bpy.data.objects.new("Crest", me), ACCENT, crest_grp, thickness=0)

# accent stripes on the cheek guards
for side, sgn in (("L", 1), ("R", -1)):
    grid_object(f"Cheek_Stripe_{side}", lambda s, t, sgn=sgn: surf(
        sgn * (d(62) + d(10) * s), rim_v(sgn * d(70)) - 0.12 + 0.62 * t, O_CHEEK + THICK + 0.0005),
        1, 3, ACCENT, face, thickness=0.002)

# ---------------------------------------------------------------------------
# Bolts
# ---------------------------------------------------------------------------
bolts = group("Bolts")
for side, sgn in (("L", 1), ("R", -1)):
    u = sgn * d(68)
    v = (v_at_z(brow_bottom(u), O_BROW) + v_at_z(0.30, O_BROW)) / 2
    bolt(f"Bolt_Brow_{side}", surf(u, v, O_BROW + 0.011), surf_normal(u, v), bolts)
    u = sgn * d(92)
    v = rim_v(u) - 0.045
    bolt(f"Bolt_Temple_{side}", surf(u, v, O_TRIM + 0.006), surf_normal(u, v), bolts)
    p = muzzle(0.5 + sgn * 0.36, 0.5)
    n = (p - Vector((0, -0.006, p.z))).normalized()
    bolt(f"Bolt_Muzzle_{side}", p + n * THICK, n, bolts, radius=0.0075)
for i, u in enumerate((d(140), d(180), d(220))):
    v = rim_v(u) - 0.045
    bolt(f"Bolt_Back_{i + 1}", surf(u, v, O_TRIM + 0.006), surf_normal(u, v), bolts, radius=0.0075)

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
        q = p - C
        r = math.sqrt((q.x / RX) ** 2 + (q.y / RY) ** 2 + (q.z / RZ) ** 2)
        if r > 1.4:
            continue                                   # arms, body
        u = math.atan2(q.x / RX, -q.y / RY)
        v = math.acos(max(-1, min(1, (q.z / RZ) / r)))
        if v < rim_v(u) and r > 0.97:
            bad.append(("dome", tuple(round(c, 3) for c in p)))
        if MUZZLE_BOTTOM <= p.z <= VISOR_BOTTOM and p.y < -0.03:
            t = (p.z - MUZZLE_BOTTOM) / (VISOR_BOTTOM - MUZZLE_BOTTOM)
            # find the muzzle wall in the same direction (seen from the head's axis)
            ang = math.atan2(p.x, -(p.y + 0.006))
            if abs(ang) < MU * 0.95:
                walls = [muzzle_xy(-MU + 2 * MU * i / 200, t) for i in range(201)]
                wx, wy = min(walls, key=lambda w: abs(math.atan2(w[0], -(w[1] + 0.006)) - ang))
                inside_gap = math.hypot(wx, wy + 0.006) - math.hypot(p.x, p.y + 0.006)
            else:
                inside_gap = 1.0
            if inside_gap < 0.006:
                bad.append(("muzzle", tuple(round(c, 3) for c in p)))
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
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CombatHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True,
                         mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False)

bpy.context.view_layer.update()
mw = root.matrix_world.copy()
root.parent = arm
root.parent_type = "BONE"
root.parent_bone = "Head"
bpy.context.view_layer.update()
root.matrix_world = mw
bpy.context.view_layer.update()

select_only(helm_objs + char_objects)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PeeperLeeper_CombatHelm.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH", "ARMATURE"}, use_mesh_modifiers=True,
                         mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=bool(bpy.data.actions))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "CombatHelm.blend"))
dg = bpy.context.evaluated_depsgraph_get()
meshes = [o for o in helm_objs if o.type == "MESH"]
print("parts:", len(meshes))
print("tris:", sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in meshes))
