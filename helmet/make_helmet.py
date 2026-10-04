"""
Metal helmet generator, fitted to the PeeperLeeper player model.

Every helmet piece is its own named object, so you can select / move / scale /
recolor / delete each one in Blender or Unity. Tweak the numbers in P below and
re-run to regenerate everything.

Run with Blender:
    blender --background --python make_helmet.py -- /path/to/PeeperLeeper.fbx [out_dir] [--render]
or with the bpy pip module (pip install bpy==4.2.0):
    python3 make_helmet.py /path/to/PeeperLeeper.fbx [out_dir] [--render]

Outputs (in out_dir, default = this folder):
    MetalHelmet.fbx                - helmet only (all parts under a "MetalHelmet" root)
    PeeperLeeper_MetalHelmet.fbx   - player + helmet already attached to the Head bone
    MetalHelmet.blend              - editable source (modifiers kept live)
    previews/*.png                 - renders (with --render)
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
flags = {a for a in argv if a.startswith('--')}
args = [a for a in argv if not a.startswith('--')]
PLAYER = args[0]
OUT = os.path.abspath(args[1] if len(args) > 1 else os.path.dirname(os.path.abspath(__file__)))

# ---------------------------------------------------------------------------
# Parameters (meters, Blender world space: Z up, face looks toward -Y).
# Measured from the PeeperLeeper head: x +-0.166, y -0.153..0.145, top z 0.371.
# ---------------------------------------------------------------------------
P = dict(
    CY=-0.004, CZ=0.255,              # dome center
    RX=0.182, RY=0.168, RZ=0.135,     # dome inner radii (side, front/back, height)
    THICK=0.010,                      # shell thickness (Solidify modifier)
    RIM_FRONT_Z=0.275,                # brim height over the eyes
    RIM_BACK_Z=0.200,                 # brim height at sides / back
    FRONT_DEG=45, BLEND_END_DEG=85,   # front opening width / transition
    FLARE=0.04,                       # outward flare of the lower wall
    RIM_RADIUS=0.0075,                # rolled rim tube
    CREST_BASE_H=0.016, CREST_PEAK_H=0.018, CREST_HALF_W=0.0065,
    NOSE_TOP_W=0.017, NOSE_BOT_W=0.013, NOSE_DEPTH=0.008,
    CHEEK_DEG=(75, 118), CHEEK_TOP_Z=0.235, CHEEK_BOT_Z=0.135, CHEEK_TUCK=0.93,
    NECK_DEG=(145, 215), NECK_TOP_Z=0.225, NECK_BOT_Z=0.150, NECK_FLARE=1.14,
    PLATE_THICK=0.007,
    RIVET_R=0.0055,
    STEEL=(0.62, 0.63, 0.66, 1), DARK=(0.20, 0.20, 0.22, 1), BRASS=(0.80, 0.58, 0.26, 1),
)
globals().update(P)
CENTER = Vector((0, CY, CZ))


def smooth(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


def rim_z(a):
    """Bottom edge height of the dome at azimuth a (radians, 0 = front)."""
    d = abs(math.degrees(math.atan2(math.sin(a), math.cos(a))))
    return RIM_FRONT_Z + (RIM_BACK_Z - RIM_FRONT_Z) * smooth((d - FRONT_DEG) / (BLEND_END_DEG - FRONT_DEG))


def surf_f(z):
    if z >= CZ:
        return math.sqrt(max(0.0, 1 - ((z - CZ) / RZ) ** 2))
    return 1 + FLARE * (CZ - z) / (CZ - RIM_BACK_Z)


def normal_at(a, z):
    f = max(surf_f(z), 1e-4)
    x, y = RX * f * math.sin(a), -RY * f * math.cos(a)
    nz = (z - CZ) / RZ ** 2 if z >= CZ else 0.0
    return Vector((x / RX ** 2, y / RY ** 2, nz)).normalized()


def surf_pt(a, z, off=0.0, f=None):
    f = surf_f(z) if f is None else f
    p = Vector((RX * f * math.sin(a), CY - RY * f * math.cos(a), z))
    return p + normal_at(a, z) * off


def phi_start(a):
    zb = rim_z(a)
    return math.asin((zb - CZ) / RZ) if zb >= CZ else (zb - CZ) / RZ


def dome_pt(a, phi, off=0.0):
    z = CZ + RZ * math.sin(phi) if phi >= 0 else CZ + RZ * phi
    return surf_pt(a, z, off)


# ---------------------------------------------------------------------------
# Mesh helpers
# ---------------------------------------------------------------------------
class Builder:
    def __init__(self):
        self.v, self.f = [], []          # faces: (indices, uvs)

    def add(self, co):
        self.v.append(Vector(co)); return len(self.v) - 1

    def face(self, idx, uvs=None):
        self.f.append((idx, uvs or [(0, 0)] * len(idx)))

    def grid(self, fn, ni, nj, wrap_i=False):
        """fn(u, v) -> point; u along i (0..1), v along j (0..1)."""
        cols = ni if wrap_i else ni + 1
        ids = [[self.add(fn(i / ni, j / nj)) for j in range(nj + 1)] for i in range(cols)]
        for i in range(ni):
            i2 = (i + 1) % cols
            for j in range(nj):
                self.face([ids[i][j], ids[i2][j], ids[i2][j + 1], ids[i][j + 1]],
                          [(i / ni, j / nj), ((i + 1) / ni, j / nj), ((i + 1) / ni, (j + 1) / nj), (i / ni, (j + 1) / nj)])
        return ids

    def sweep_rect(self, stations, closed=False):
        """stations: list of (p, side, out, half_w, d_in, d_out) -> box tube with caps."""
        rings = []
        for p, side, out, hw, din, dout in stations:
            rings.append([self.add(p - side * hw - out * din), self.add(p + side * hw - out * din),
                          self.add(p + side * hw + out * dout), self.add(p - side * hw + out * dout)])
        n = len(rings)
        for k in range(n if closed else n - 1):
            a, b = rings[k], rings[(k + 1) % n]
            for s in range(4):
                self.face([a[s], a[(s + 1) % 4], b[(s + 1) % 4], b[s]],
                          [(k / n, s / 4), (k / n, (s + 1) / 4), ((k + 1) / n, (s + 1) / 4), ((k + 1) / n, s / 4)])
        if not closed:
            self.face(rings[0][::-1]); self.face(rings[-1])

    def tube(self, pts, normals, r, seg=10, closed=True):
        n = len(pts); rings = []
        for k in range(n):
            t = (pts[(k + 1) % n] - pts[k - 1]) if closed else (pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)])
            t.normalize(); nn = (normals[k] - t * normals[k].dot(t)).normalized(); b = t.cross(nn)
            rings.append([self.add(pts[k] + r * (math.cos(2 * math.pi * s / seg) * nn + math.sin(2 * math.pi * s / seg) * b))
                          for s in range(seg)])
        for k in range(n if closed else n - 1):
            a, b2 = rings[k], rings[(k + 1) % n]
            for s in range(seg):
                self.face([a[s], a[(s + 1) % seg], b2[(s + 1) % seg], b2[s]],
                          [(k / n, s / seg), (k / n, (s + 1) / seg), ((k + 1) / n, (s + 1) / seg), ((k + 1) / n, s / seg)])

    def sphere(self, c, r, squash=1.0, up=Vector((0, 0, 1)), seg=10, rings=6):
        rot = Vector((0, 0, 1)).rotation_difference(up).to_matrix()
        top = self.add(c + rot @ Vector((0, 0, r * squash))); bot = self.add(c - rot @ Vector((0, 0, r * squash)))
        ids = []
        for j in range(1, rings):
            th = math.pi * j / rings
            ids.append([self.add(c + rot @ Vector((r * math.sin(th) * math.cos(2 * math.pi * s / seg),
                                                     r * math.sin(th) * math.sin(2 * math.pi * s / seg),
                                                     r * squash * math.cos(th)))) for s in range(seg)])
        for s in range(seg):
            s2 = (s + 1) % seg
            self.face([top, ids[0][s], ids[0][s2]])
            self.face([bot, ids[-1][s2], ids[-1][s]])
            for j in range(len(ids) - 1):
                self.face([ids[j][s], ids[j + 1][s], ids[j + 1][s2], ids[j][s2]])

    def build(self, name, mat, closed_manifold, coll, origin=None):
        bm = bmesh.new()
        bv = [bm.verts.new(c) for c in self.v]
        uv = bm.loops.layers.uv.new("UVMap")
        for idx, uvs in self.f:
            fc = bm.faces.new([bv[i] for i in idx])
            fc.smooth = True
            for loop, t in zip(fc.loops, uvs): loop[uv].uv = t
        bm.normal_update()
        if closed_manifold:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        else:  # open shell: make normals face away from the head center
            score = sum(fc.normal.dot(fc.calc_center_median() - CENTER) for fc in bm.faces)
            if score < 0:
                bmesh.ops.reverse_faces(bm, faces=bm.faces)
        origin = origin if origin is not None else sum(self.v, Vector()) / len(self.v)
        bmesh.ops.translate(bm, verts=bm.verts, vec=-origin)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        me.materials.append(mat)
        ob = bpy.data.objects.new(name, me); ob.location = origin
        coll.objects.link(ob)
        return ob


def material(name, rgba, rough):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Metallic"].default_value = 1.0
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = rgba
    return m


def add_solidify(ob, thick, offset=1.0):
    s = ob.modifiers.new("Thickness", 'SOLIDIFY'); s.thickness = thick; s.offset = offset
    s.use_even_offset = True; s.use_quality_normals = True
    bv = ob.modifiers.new("EdgeBevel", 'BEVEL'); bv.width = thick * 0.3; bv.segments = 2
    bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(50)


# ---------------------------------------------------------------------------
# Scene
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=PLAYER)
scene = bpy.context.scene
player_coll = bpy.data.collections.new("Player"); scene.collection.children.link(player_coll)
for o in list(scene.collection.objects):
    scene.collection.objects.unlink(o); player_coll.objects.link(o)
arm = next(o for o in player_coll.objects if o.type == 'ARMATURE')

hcoll = bpy.data.collections.new("MetalHelmet"); scene.collection.children.link(hcoll)
STEEL_M = material("Helmet_Steel", STEEL, 0.30)
DARK_M = material("Helmet_DarkSteel", DARK, 0.45)
BRASS_M = material("Helmet_Brass", BRASS, 0.35)
parts = []

# 1. Dome shell ---------------------------------------------------------------
NA, NR = 72, 22
b = Builder()
cols = []
for i in range(NA):
    a = 2 * math.pi * i / NA; p0 = phi_start(a)
    cols.append([b.add(dome_pt(a, p0 + (math.pi / 2 - p0) * j / NR)) for j in range(NR)])
pole = b.add((0, CY, CZ + RZ))
for i in range(NA):
    i2 = (i + 1) % NA
    for j in range(NR - 1):
        b.face([cols[i][j], cols[i2][j], cols[i2][j + 1], cols[i][j + 1]],
               [(i / NA, j / NR), ((i + 1) / NA, j / NR), ((i + 1) / NA, (j + 1) / NR), (i / NA, (j + 1) / NR)])
    b.face([cols[i][NR - 1], cols[i2][NR - 1], pole], [(i / NA, (NR - 1) / NR), ((i + 1) / NA, (NR - 1) / NR), (i / NA, 1)])
dome = b.build("Helmet_Dome", STEEL_M, False, hcoll, origin=CENTER.copy()); add_solidify(dome, THICK); parts.append(dome)

# 2. Rolled rim --------------------------------------------------------------
b = Builder(); pts, nrm = [], []
for i in range(120):
    a = 2 * math.pi * i / 120; z = rim_z(a)
    pts.append(surf_pt(a, z, THICK * 0.5) + Vector((0, 0, RIM_RADIUS * 0.3))); nrm.append(normal_at(a, z))
b.tube(pts, nrm, RIM_RADIUS, seg=10)
parts.append(b.build("Helmet_Rim", DARK_M, True, hcoll))

# 3. Crest ridge (front rim -> top -> back rim) ------------------------------
b = Builder(); st = []
front = [(0.0, phi_start(0) + (math.pi / 2 - phi_start(0)) * k / 20) for k in range(21)]
back = [(math.pi, math.pi / 2 - (math.pi / 2 - phi_start(math.pi)) * k / 20) for k in range(1, 21)]
path = front + back
for k, (a, phi) in enumerate(path):
    z = CZ + RZ * math.sin(phi) if phi >= 0 else CZ + RZ * phi
    s = k / (len(path) - 1)
    n = normal_at(a, z) if phi < math.pi / 2 - 1e-6 else Vector((0, 0, 1))
    st.append((dome_pt(a, phi, THICK), Vector((1, 0, 0)), n, CREST_HALF_W, 0.003,
               CREST_BASE_H + CREST_PEAK_H * math.sin(math.pi * s)))
b.sweep_rect(st)
parts.append(b.build("Helmet_Crest", DARK_M, True, hcoll))

# 4. Nose guard ---------------------------------------------------------------
b = Builder(); st = []
zs = [0.300, 0.275, 0.250, 0.228, 0.205]
for k, z in enumerate(zs):
    t = k / (len(zs) - 1)
    if z >= RIM_FRONT_Z:
        p = surf_pt(0, z, THICK + 0.001); out = normal_at(0, z)
    else:   # hang straight down in front of the face, slight forward tilt
        p = surf_pt(0, RIM_FRONT_Z, THICK + 0.001) + Vector((0, -0.006 * (RIM_FRONT_Z - z) / 0.07 - 0.002, z - RIM_FRONT_Z))
        out = Vector((0, -1, 0))
    hw = NOSE_TOP_W + (NOSE_BOT_W - NOSE_TOP_W) * t
    st.append((p, Vector((1, 0, 0)), out, hw if k < len(zs) - 1 else hw * 1.25, 0.0, NOSE_DEPTH))
b.sweep_rect(st)
parts.append(b.build("Helmet_NoseGuard", STEEL_M, True, hcoll))

# 5/6. Cheek guards -----------------------------------------------------------
def cheek(side):
    a0, a1 = (math.radians(d) for d in CHEEK_DEG)
    def fn(u, v):
        a = side * (a0 + (a1 - a0) * u)
        bot = CHEEK_BOT_Z + 0.025 * (2 * u - 1) ** 2          # rounded lower edge
        z = CHEEK_TOP_Z + (bot - CHEEK_TOP_Z) * v
        f_top = surf_f(CHEEK_TOP_Z) + (THICK + 0.002) / RX
        f = f_top + (CHEEK_TUCK - f_top) * smooth(v)
        return surf_pt(a, z, f=f)
    b = Builder(); b.grid(fn, 10, 10)
    ob = b.build("Helmet_CheekGuard_" + ("L" if side > 0 else "R"), STEEL_M, False, hcoll)
    add_solidify(ob, PLATE_THICK); return ob
parts += [cheek(1), cheek(-1)]

# 7. Neck guard ---------------------------------------------------------------
def neck_fn(u, v):
    a = math.radians(NECK_DEG[0] + (NECK_DEG[1] - NECK_DEG[0]) * u)
    z = NECK_TOP_Z + (NECK_BOT_Z - NECK_TOP_Z) * v
    f_top = surf_f(NECK_TOP_Z) + (THICK + 0.002) / RY
    return surf_pt(a, z, f=f_top + (NECK_FLARE - f_top) * v * v)
b = Builder(); b.grid(neck_fn, 16, 8)
ng = b.build("Helmet_NeckGuard", STEEL_M, False, hcoll); add_solidify(ng, PLATE_THICK); parts.append(ng)

# 8. Rivets -------------------------------------------------------------------
b = Builder()
for d in (-150, -120, -60, -30, 30, 60, 120, 150, 180):
    a = math.radians(d); z = rim_z(a) + 0.020
    b.sphere(surf_pt(a, z, THICK), RIVET_R, 0.6, normal_at(a, z))
for side in (1, -1):        # pin each cheek guard
    for d in (82, 110):
        a = side * math.radians(d); z = CHEEK_TOP_Z - 0.012
        f = surf_f(CHEEK_TOP_Z) + (THICK + 0.002 + PLATE_THICK) / RX
        b.sphere(surf_pt(a, z, f=f), RIVET_R, 0.6, normal_at(a, z))
parts.append(b.build("Helmet_Rivets", BRASS_M, True, hcoll))

# ---------------------------------------------------------------------------
# Hierarchy: MetalHelmet (empty) -> parts, MetalHelmet bone-parented to Head
# ---------------------------------------------------------------------------
root = bpy.data.objects.new("MetalHelmet", None); root.empty_display_type = 'SPHERE'
root.empty_display_size = 0.05; root.location = CENTER; hcoll.objects.link(root)
for ob in parts:
    ob.parent = root; ob.location = ob.location - CENTER
bpy.context.view_layer.update()
root.parent = arm; root.parent_type = 'BONE'; root.parent_bone = 'Head'
bpy.context.view_layer.update()
root.matrix_world = Matrix.Translation(CENTER)
bpy.context.view_layer.update()

# ---------------------------------------------------------------------------
# Fit check: no helmet vertex inside the player, report min clearance
# ---------------------------------------------------------------------------
dg = bpy.context.evaluated_depsgraph_get()
def bvh_of(ob):
    ev = ob.evaluated_get(dg); me = ev.to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(ob.matrix_world); t = BVHTree.FromBMesh(bm)
    closed = all(e.is_manifold for e in bm.edges)   # inside test only valid on closed meshes
    bm.free(); ev.to_mesh_clear(); return t, closed
body = [bvh_of(o) for o in player_coll.objects if o.type == 'MESH']
print("\nFIT CHECK (clearance to player surface, meters)")
worst_all = 1e9
for ob in parts:
    ev = ob.evaluated_get(dg); me = ev.to_mesh(); mw = ob.matrix_world
    worst, inside = 1e9, 0
    for v in me.vertices:
        p = mw @ v.co
        for t, closed in body:
            hit, nrm, _, dist = t.find_nearest(p)
            if hit is None: continue
            if closed and (p - hit).dot(nrm) < 0: inside += 1; dist = -dist
            worst = min(worst, dist)
    ev.to_mesh_clear(); worst_all = min(worst_all, worst)
    print(f"  {ob.name:24s} verts={len(ob.data.vertices):5d}  min clearance={worst:+.4f}  inside={inside}")
print(f"  overall min clearance = {worst_all:+.4f}\n")

# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
os.makedirs(OUT, exist_ok=True)
fbx = dict(use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, use_mesh_modifiers=True,
           add_leaf_bones=False, mesh_smooth_type='FACE', bake_anim=False, path_mode='AUTO')
def select(objs):
    for o in scene.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
select([root] + parts)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "MetalHelmet.fbx"), **fbx)
select(list(player_coll.objects) + [root] + parts)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PeeperLeeper_MetalHelmet.fbx"), **fbx)
for o in scene.objects: o.select_set(False)

if '--render' in flags:
    world = bpy.data.worlds.new("World"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.68, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35)); scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam)
    cam.data.lens = 60; scene.camera = cam
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.device = 'CPU'
    scene.render.resolution_x = scene.render.resolution_y = 640
    target = Vector((0, CY, 0.22))
    for name, az, el, dist in (("front_3q", -35, 12, 1.0), ("side", 90, 5, 1.0), ("back_3q", 145, 20, 1.0), ("full_body", -25, 10, 2.6)):
        tgt = target if name != "full_body" else Vector((0, 0, 0.0))
        d = Vector((math.sin(math.radians(az)) * math.cos(math.radians(el)),
                    -math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
        cam.location = tgt + d * dist
        cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(OUT, "previews", name + ".png")
        bpy.ops.render.render(write_still=True)
    for o in (sun, cam): bpy.data.objects.remove(o)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MetalHelmet.blend"), compress=True)
print("Wrote:", OUT)
