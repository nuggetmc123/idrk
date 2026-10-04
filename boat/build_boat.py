"""
Builds a low-poly survival-game style motor skiff (think Rust's rowboat, minus the rust / Stranded Deep's
small boats) in Blender and exports it as FBX (+ GLB and .blend).

Run with Blender's Python:   python3 build_boat.py      (needs `pip install bpy`)
                       or:   blender -b -P build_boat.py

Conventions: Blender Z-up, bow points -Y, origin = centre of the boat at the waterline.
With the FBX settings below that lands in Unity as Y-up with the bow facing +Z.
"""
import math
import os

import bpy  # noqa: I001  (bpy must be imported before bmesh/mathutils)
import bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)
rng = np.random.default_rng(7)

# --------------------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"


# --------------------------------------------------------------------------- textures
def noise(size, scale):
    """Cheap value noise, tileable, values 0..1."""
    g = rng.random((scale, scale))
    x = np.linspace(0, scale, size, endpoint=False)
    xi = x.astype(int)
    xf = x - xi
    xf = xf * xf * (3 - 2 * xf)
    x0, x1 = xi % scale, (xi + 1) % scale
    a = g[np.ix_(x0, x0)] * (1 - xf)[None, :] + g[np.ix_(x0, x1)] * xf[None, :]
    b = g[np.ix_(x1, x0)] * (1 - xf)[None, :] + g[np.ix_(x1, x1)] * xf[None, :]
    return a * (1 - xf)[:, None] + b * xf[:, None]


def fbm(size, octaves=5, base=4):
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += noise(size, base * 2 ** o) * amp
        tot += amp
        amp *= 0.5
    return out / tot


def save_png(name, rgb):
    h, w, _ = rgb.shape
    img = bpy.data.images.new(name, w, h, alpha=False)
    px = np.ones((h, w, 4), dtype=np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    path = os.path.join(TEX, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


S = 1024


def tex_wood():
    # Planks run along U (boat length); V wraps the hull profile.
    u, v = np.meshgrid(np.linspace(0, 1, S, endpoint=False), np.linspace(0, 1, S, endpoint=False))
    planks = 9
    pv = v * planks
    pid = np.floor(pv)
    frac = pv - pid
    grain = np.sin((u * 40 + fbm(S, 4, 4) * 6 + pid * 3.1) * 6.0) * 0.5 + 0.5
    base = np.array([0.36, 0.25, 0.15])
    tint = (rng.random(planks + 1)[pid.astype(int)] - 0.5) * 0.25  # plank-to-plank variation
    dirt = fbm(S, 6, 3)
    col = base[None, None, :] * (0.75 + 0.25 * grain[..., None] + tint[..., None])
    # weathered grey bleaching
    grey = np.array([0.42, 0.40, 0.37])
    bleach = np.clip((dirt - 0.45) * 2.5, 0, 1)[..., None] * 0.6
    col = col * (1 - bleach) + grey * bleach
    # seams between planks + nail heads
    seam = (frac < 0.025) | (frac > 0.975)
    col[seam] *= 0.25
    nails = ((u * 24) % 1 < 0.02) & (np.abs(frac - 0.5) < 0.035)
    col[nails] = [0.25, 0.15, 0.09]
    # algae / waterline grime at the bottom of the hull (low V)
    algae = np.clip((0.35 - v) * 4, 0, 1) * (0.5 + 0.5 * fbm(S, 5, 8))
    col = col * (1 - algae[..., None] * 0.7) + np.array([0.12, 0.16, 0.08]) * algae[..., None] * 0.7
    return save_png("T_Boat_Wood", col)


def tex_painted_metal():
    # clean army-green paint with a little subtle mottling, no rust
    paint = np.array([0.20, 0.27, 0.20])
    col = paint * (0.92 + 0.12 * fbm(S, 4, 6))[..., None]
    return save_png("T_Boat_PaintedMetal", col)


def tex_steel():
    steel = np.array([0.42, 0.43, 0.44])
    brushed = fbm(S, 3, 32)
    return save_png("T_Boat_Steel", steel * (0.9 + 0.15 * brushed)[..., None])


IMG_WOOD = tex_wood()
IMG_PAINT = tex_painted_metal()
IMG_STEEL = tex_steel()


# --------------------------------------------------------------------------- materials
def material(name, color=(0.5, 0.5, 0.5), image=None, rough=0.8, metal=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if image:
        t = mat.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = image
        mat.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


M_WOOD = material("M_Boat_Wood", image=IMG_WOOD, rough=0.9)
M_PAINT = material("M_Boat_PaintedMetal", image=IMG_PAINT, rough=0.55, metal=0.2)
M_STEEL = material("M_Boat_Steel", image=IMG_STEEL, rough=0.4, metal=0.8)
M_BLACK = material("M_Boat_Rubber", (0.03, 0.03, 0.03), rough=0.9)
M_RED = material("M_Boat_FuelCan", (0.45, 0.05, 0.03), rough=0.5, metal=0.4)


# --------------------------------------------------------------------------- helpers
def link(obj, parent=None):
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def mesh_obj(name, bm, mat, parent=None, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return link(bpy.data.objects.new(name, me), parent)


def empty(name, loc, parent=None, kind="PLAIN_AXES", size=0.2):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = kind
    e.empty_display_size = size
    e.location = loc
    return link(e, parent)


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def smart_uv(obj, island=0.02):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=island)
    bpy.ops.object.mode_set(mode="OBJECT")


def box(name, size, loc, mat, parent=None, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    o = mesh_obj(name, bm, mat, parent)
    o.location = loc
    o.rotation_euler = rot
    smart_uv(o)
    return o


def cylinder(name, r, depth, loc, mat, parent=None, rot=(0, 0, 0), seg=6, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r,
                          radius2=r if r2 is None else r2, depth=depth)
    o = mesh_obj(name, bm, mat, parent)
    o.location = loc
    o.rotation_euler = rot
    smart_uv(o)
    return o


# --------------------------------------------------------------------------- hull
LEN_STERN, LEN_BOW = 2.2, -2.35   # Y of transom and of the stem (bow = -Y)
N_ST = 9


def station(t):
    """Half-beam, gunwale height and keel height for t in [0 (stern) .. 1 (bow)]."""
    if t < 0.35:
        w = 0.74 + (0.86 - 0.74) * (t / 0.35)
    else:
        w = 0.86 * math.sqrt(max(0.0, 1 - ((t - 0.35) / 0.65) ** 2))
    zg = 0.52 + 0.30 * t ** 2.2                      # sheer line rises to the bow
    zk = -0.26 + 0.62 * max(0.0, (t - 0.55) / 0.45) ** 1.8  # forefoot sweeps up
    return max(w, 0.012), zg, zk


def profile(t):
    """Cross-section points from port gunwale -> keel -> starboard gunwale."""
    w, zg, zk = station(t)
    half = [
        (w, zg),
        (w * 0.92, zg - (zg - zk) * 0.55),
        (w * 0.72, zk + 0.08 * (1 - t)),      # chine
        (0.0, zk),
    ]
    left = [(-x, z) for x, z in half]
    right = [(x, z) for x, z in reversed(half[:-1])]
    return left + right


bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")
rings = []
for i in range(N_ST + 1):
    t = i / N_ST
    y = LEN_STERN + (LEN_BOW - LEN_STERN) * t
    rings.append([bm.verts.new((x, y, z)) for x, z in profile(t)])

# UVs: U along the length, V along the profile arc so planks follow the hull.
def arc_v(ring):
    d = [0.0]
    for a, b in zip(ring, ring[1:]):
        d.append(d[-1] + (a.co - b.co).length)
    mid = d[len(d) // 2]
    return [abs(x - mid) / 1.6 for x in d]   # 0 at keel, ~0.6-0.8 at gunwale

arcs = [arc_v(r) for r in rings]
for i in range(N_ST):
    for j in range(len(rings[i]) - 1):
        f = bm.faces.new((rings[i][j], rings[i][j + 1], rings[i + 1][j + 1], rings[i + 1][j]))
        for loop, (ii, jj) in zip(f.loops, ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j))):
            loop[uv_layer].uv = ((LEN_STERN - rings[ii][jj].co.y) / 1.5, arcs[ii][jj])
# Transom (flat stern board)
tf = bm.faces.new(list(reversed(rings[0])))
for loop in tf.loops:
    loop[uv_layer].uv = (loop.vert.co.x * 0.6 + 0.5, loop.vert.co.z * 0.6 + 0.4)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.03)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

boat_root = empty("Boat", (0, 0, 0), kind="ARROWS", size=0.6)
hull = mesh_obj("Hull", bm, M_WOOD, boat_root)
# make sure normals point outwards (keel face should face -Z)
lowest = min(hull.data.polygons, key=lambda p: p.center.z)
if lowest.normal.z > 0:
    for p in hull.data.polygons:
        p.flip()
sol = hull.modifiers.new("thickness", "SOLIDIFY")
sol.thickness = 0.04
sol.offset = -1
sol.use_even_offset = True
apply_modifiers(hull)

# --------------------------------------------------------------------------- gunwale rail & keel strip
def rail(name, pts, radius, mat, parent, cyclic=False):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 0   # 4-sided tube
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts):
        p.co = (*c, 1)
    sp.use_cyclic_u = cyclic
    o = link(bpy.data.objects.new(name, cu), parent)
    o.data.materials.append(mat)
    bpy.context.view_layer.objects.active = o
    for s in bpy.context.selected_objects:
        s.select_set(False)
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    smart_uv(o)
    return o


gun = []
for i in range(N_ST + 1):
    t = i / N_ST
    w, zg, _ = station(t)
    gun.append((w + 0.01, LEN_STERN + (LEN_BOW - LEN_STERN) * t, zg + 0.01))
loop_pts = [(x, y, z) for x, y, z in gun] + [(-x, y, z) for x, y, z in reversed(gun)][1:]
rail("Gunwale", loop_pts, 0.035, M_WOOD, boat_root, cyclic=True)

keel = []
for i in range(N_ST + 1):
    t = i / N_ST
    _, _, zk = station(t)
    keel.append((0, LEN_STERN + (LEN_BOW - LEN_STERN) * t, zk - 0.02))
rail("Keel_Strip", keel, 0.03, M_STEEL, boat_root)

# --------------------------------------------------------------------------- interior
def beam_at(t, z):
    """Interior half-width at station t, height z (linear on the profile)."""
    pts = profile(t)[:len(profile(t)) // 2 + 1]
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        if min(z0, z1) <= z <= max(z0, z1) and z0 != z1:
            return abs(x0 + (x1 - x0) * (z - z0) / (z1 - z0)) - 0.05
    return abs(pts[0][0]) - 0.05


def y_at(t):
    return LEN_STERN + (LEN_BOW - LEN_STERN) * t


bench_z = 0.22
for n, t in (("Seat_Rear", 0.12), ("Seat_Middle", 0.42), ("Seat_Front", 0.68)):
    hw = beam_at(t, bench_z)
    box(n, (hw * 2, 0.30, 0.045), (0, y_at(t), bench_z), M_WOOD, boat_root)
    # vertical support under the bench
    box(n + "_Support", (0.05, 0.26, bench_z - station(t)[2] - 0.05),
        (0, y_at(t), (bench_z + station(t)[2]) / 2 + 0.02), M_WOOD, boat_root)

# floor boards
for k, x in enumerate((-0.30, -0.10, 0.10, 0.30)):
    box(f"Floorboard_{k}", (0.17, 2.6, 0.025), (x, 0.55, -0.13 - abs(x) * 0.0), M_WOOD, boat_root)

# ribs (frames) inside the hull
for k, t in enumerate((0.05, 0.27, 0.55, 0.80)):
    pts = [(x, z) for x, z in profile(t)]
    y = y_at(t)
    rail(f"Rib_{k}", [(x * 0.95, y, z + 0.03) for x, z in pts], 0.022, M_WOOD, boat_root)

# transom reinforcement plate where the motor clamps on
box("Transom_Plate", (0.55, 0.03, 0.35), (0, LEN_STERN + 0.035, 0.32), M_PAINT, boat_root)

# bow eye + cleats
cylinder("Bow_Ring", 0.05, 0.015, (0, LEN_BOW - 0.02, station(1)[1] - 0.12), M_STEEL, boat_root,
         rot=(math.radians(90), 0, 0), seg=6)
for side in (-1, 1):
    box(f"Cleat_{'L' if side < 0 else 'R'}", (0.04, 0.18, 0.04),
        (side * 0.62, y_at(0.06), station(0.06)[1] + 0.05), M_STEEL, boat_root)
    # oar locks
    cylinder(f"Oarlock_{'L' if side < 0 else 'R'}", 0.03, 0.12,
             (side * (station(0.42)[0] + 0.02), y_at(0.42), station(0.42)[1] + 0.07), M_STEEL, boat_root, seg=6)

# --------------------------------------------------------------------------- outboard motor
# Pivot = steering axis. Rotate Motor_Pivot around its local up axis to steer.
motor = empty("Motor_Pivot", (0, LEN_STERN + 0.18, 0.0), boat_root, kind="SINGLE_ARROW", size=0.4)
box("Motor_Clamp", (0.20, 0.14, 0.18), (0, -0.07, 0.55), M_STEEL, motor)
cow = box("Motor_Cowling", (0.36, 0.48, 0.38), (0, 0.10, 0.80), M_PAINT, motor)
m = cow.modifiers.new("bevel", "BEVEL"); m.width = 0.06; m.segments = 1
apply_modifiers(cow)
smart_uv(cow)
box("Motor_Cowling_Band", (0.37, 0.49, 0.05), (0, 0.10, 0.65), M_BLACK, motor)
box("Motor_Midsection", (0.18, 0.24, 0.20), (0, 0.06, 0.52), M_PAINT, motor)
box("Motor_Leg", (0.07, 0.14, 0.78), (0, 0.04, 0.05), M_PAINT, motor)
box("Motor_AntiCav_Plate", (0.26, 0.34, 0.012), (0, 0.06, -0.24), M_STEEL, motor)
cylinder("Motor_Gearcase", 0.055, 0.40, (0, 0.06, -0.34), M_PAINT, motor, rot=(math.radians(90), 0, 0), seg=6)
box("Motor_Skeg", (0.02, 0.10, 0.14), (0, 0.10, -0.43), M_STEEL, motor, rot=(math.radians(-10), 0, 0))
# tiller arm reaching into the boat, the driver holds the grip
box("Motor_Tiller", (0.06, 0.60, 0.05), (0.0, -0.32, 0.72), M_STEEL, motor, rot=(math.radians(-8), 0, 0))
cylinder("Motor_Tiller_Grip", 0.035, 0.22, (0.0, -0.66, 0.66), M_BLACK, motor, rot=(math.radians(90 - 8), 0, 0), seg=6)
cylinder("Motor_Pull_Cord", 0.03, 0.03, (0.0, -0.16, 0.95), M_BLACK, motor, rot=(math.radians(90), 0, 0), seg=6)

# Propeller: separate object, origin on the shaft, spin around local Y (Blender) / Z (Unity).
prop = empty("Propeller", (0, 0.29, -0.34), motor, kind="CIRCLE", size=0.15)
prop.rotation_euler = (math.radians(90), 0, 0)
cylinder("Propeller_Hub", 0.04, 0.12, (0, 0, 0), M_STEEL, prop, seg=6, r2=0.025)
for k in range(3):
    a = k * 2 * math.pi / 3
    bm = bmesh.new()
    # simple twisted blade
    vs = [(0.03, -0.02, 0), (0.13, -0.035, 0.02), (0.15, 0.02, -0.02), (0.03, 0.03, 0)]
    vv = [bm.verts.new(v) for v in vs]
    bm.faces.new(vv)
    sol_b = mesh_obj(f"Propeller_Blade_{k}", bm, M_STEEL, prop)
    sol_b.rotation_euler = (0, 0, a)
    s = sol_b.modifiers.new("t", "SOLIDIFY"); s.thickness = 0.012
    apply_modifiers(sol_b)
    smart_uv(sol_b)

# Exhaust / wake FX socket
empty("FX_Prop_Wash", (0, 0.45, -0.34), motor, kind="CONE", size=0.15)

# --------------------------------------------------------------------------- props (loot-y clutter)
box("Fuel_Can", (0.17, 0.32, 0.36), (0.33, 1.30, 0.065), M_RED, boat_root)
box("Fuel_Can_Handle", (0.04, 0.16, 0.04), (0.33, 1.30, 0.265), M_RED, boat_root)
cylinder("Fuel_Can_Cap", 0.03, 0.05, (0.33, 1.18, 0.265), M_BLACK, boat_root, seg=6)

# --------------------------------------------------------------------------- collider (convex hull)
bm = bmesh.new()
bm.from_mesh(hull.data)
ch = bmesh.ops.convex_hull(bm, input=bm.verts)
bmesh.ops.delete(bm, geom=[g for g in ch["geom_interior"] + ch["geom_unused"] if isinstance(g, bmesh.types.BMVert)],
                 context="VERTS")
bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(8), verts=bm.verts, edges=bm.edges)
bmesh.ops.triangulate(bm, faces=bm.faces)
collider = mesh_obj("Boat_Collider", bm, M_BLACK, boat_root)


# --------------------------------------------------------------------------- flatten for export
# Engines (Roblox especially) misplace parts when an FBX has empties, nested parents and
# rotated pivots, and many ignore multi-material/embedded textures. So every part is exported
# as its own plain top-level mesh (no parents, no rotation), all sharing ONE baked texture.
# Motor_* parts steer together, Propeller_* parts spin together (the game script groups them).
def subtree(root):
    out = []
    for c in root.children:
        out.append(c)
        out += subtree(c)
    return out


prop_pos = prop.matrix_world.translation.copy()
motor_pos = motor.matrix_world.translation.copy()
prop_meshes = [o for o in subtree(prop) if o.type == "MESH"]
motor_meshes = [o for o in subtree(motor) if o.type == "MESH" and o not in prop_meshes]
body_meshes = [o for o in subtree(boat_root) if o.type == "MESH" and o not in prop_meshes + motor_meshes
               and o is not collider]


def select(objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


# bake every transform into the vertices and drop all parents
meshes = body_meshes + motor_meshes + prop_meshes + [collider]
select(meshes)
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.data.objects if o.type == "EMPTY"]:
    bpy.data.objects.remove(o)


# Every part stays its own object (top-level, no rotation) with its origin at its own centre.
for o in meshes:
    o.data.name = o.name
select(body_meshes + motor_meshes + prop_meshes)
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
print("pivots: motor", tuple(round(v, 3) for v in motor_pos), "propeller", tuple(round(v, 3) for v in prop_pos))

# --- bake all materials into one shared atlas texture
parts = body_meshes + motor_meshes + prop_meshes
for o in parts:
    o.data.uv_layers.active = o.data.uv_layers["UVMap"]
    o.data.uv_layers.new(name="UVAtlas").active = True
select(parts)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
bpy.ops.object.mode_set(mode="OBJECT")

ATLAS = 2048
atlas = bpy.data.images.new("T_Boat", ATLAS, ATLAS, alpha=False)
for mat in {s.material for o in parts for s in o.material_slots}:
    nt = mat.node_tree
    uvn = nt.nodes.new("ShaderNodeUVMap")
    uvn.uv_map = "UVMap"  # read the source textures through their original UVs
    for n in nt.nodes:
        if n.type == "TEX_IMAGE":
            nt.links.new(uvn.outputs["UV"], n.inputs["Vector"])
    target = nt.nodes.new("ShaderNodeTexImage")
    target.image = atlas
    nt.nodes.active = target

sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = 1
sc.render.bake.use_pass_direct = False
sc.render.bake.use_pass_indirect = False
sc.render.bake.use_pass_color = True
sc.render.bake.margin = 8
select(parts)
bpy.ops.object.bake(type="DIFFUSE")
atlas.filepath_raw = os.path.join(TEX, "T_Boat.png")
atlas.file_format = "PNG"
atlas.save()

# White base colour: engines multiply it with the texture, so anything darker tints the boat.
M_BOAT = material("M_Boat", (1.0, 1.0, 1.0), image=atlas, rough=0.8)
for o in parts:
    o.data.materials.clear()
    o.data.materials.append(M_BOAT)
    o.data.uv_layers.remove(o.data.uv_layers["UVMap"])
    o.data.uv_layers["UVAtlas"].name = "UVMap"
collider.data.materials.clear()
collider.data.uv_layers.remove(collider.data.uv_layers[0]) if collider.data.uv_layers else None
collider.display_type = "WIRE"
collider.hide_render = True
collider.hide_viewport = True   # it's a solid lid over the boat; keep it out of sight in the .blend
for img in [i for i in bpy.data.images if i is not atlas]:
    os.remove(bpy.path.abspath(img.filepath_raw))  # only the baked atlas ships
    bpy.data.images.remove(img)
for m in [m for m in bpy.data.materials if m is not M_BOAT]:
    bpy.data.materials.remove(m)

# Parts stay top-level (no parenting) so nothing can get offset on import;
# the game script groups the Motor_* / Propeller_* parts under pivots at runtime.

# --------------------------------------------------------------------------- export
os.chdir(OUT)
atlas.pack()  # texture lives inside the .blend, no missing-file pink
# Open the .blend already textured: Material Preview, and Solid mode set to show textures too.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "MATERIAL"
                    space.shading.color_type = "TEXTURE"
                    space.region_3d.view_location = (0, 0.2, 0.2)
                    space.region_3d.view_distance = 7.5
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Boat.blend"), compress=True, check_existing=False)

FBX_OPTS = dict(
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,   # no -90° X rotation on the parts in Unity
    object_types={"MESH"},
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
)
# The visible model. The collider is NOT in here: it's a closed convex hull, so in a viewer
# or engine it shows up as a solid lid covering the inside of the boat.
select(parts)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Boat.fbx"), use_selection=True,
                         path_mode="COPY", embed_textures=True, **FBX_OPTS)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "Boat.glb"), export_format="GLB", use_selection=True)
# Optional low-poly physics hull, in its own file.
collider.hide_viewport = False
select([collider])
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Boat_Collider.fbx"), use_selection=True, **FBX_OPTS)

tris = sum(len(p.vertices) - 2 for o in parts for p in o.data.polygons)
print("objects:", [o.name for o in bpy.data.objects], "tris:", tris)
