"""
Rust-style welded scrap-metal helmet (full face cover), fitted to the PeeperLeeper player model.

Every piece is its own named object, so you can select / move / scale / recolor /
delete each one in Blender or Unity. Tweak the numbers in P below and re-run.
The plates are shaped by ray-casting the player's head, so they always clear the
muzzle and eyes (the FIT CHECK prints the minimum clearance).

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
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
flags = {a for a in argv if a.startswith('--')}
args = [a for a in argv if not a.startswith('--')]
PLAYER = args[0]
OUT = os.path.abspath(args[1] if len(args) > 1 else os.path.dirname(os.path.abspath(__file__)))

# ---------------------------------------------------------------------------
# Parameters (meters / degrees, Blender world space: Z up, face looks toward -Y,
# azimuth 0 = straight ahead, +90 = player's left side).
# ---------------------------------------------------------------------------
P = dict(
    CY=-0.004,                     # head vertical axis (x=0, y=CY)
    CLEAR=0.012,                   # gap between head and inside of plates
    FIT_WIN_DEG=8, FIT_WIN_Z=0.02, # how far around each vertex the head is checked
    R_MIN=0.165,                   # plates never closer to the axis than this
    THICK=0.008,                   # sheet thickness (Solidify modifier)
    DENT=0.0025,                   # random outward hammer dents on plates
    SEAM_Z=0.288, TOP_Z=0.405,     # dome starts at SEAM_Z, peaks at TOP_Z
    SIDE_DEG=95,                   # face plates cover -SIDE..+SIDE, back plate the rest
    # eye slits: between NOSE_DEG and SLIT_OUT_DEG, from SLIT_BOT_Z up to the brow edge
    NOSE_DEG=7, SLIT_OUT_DEG=38, SLIT_BOT_Z=0.208,
    BROW_INNER_Z=0.242, BROW_OUTER_Z=0.258,   # angled brow (lower at the nose = angry)
    JAW_BOT_Z=0.115, BACK_BOT_Z=0.150,
    BAND_H=0.022, BAND_OUT=0.006,  # strap band covering the dome seam
    WELD_R=0.0045, BOLT_R=0.0075, HOLE_R=0.0055,
    STEEL=(0.46, 0.46, 0.47, 1), RUST=(0.42, 0.29, 0.20, 1), DARK=(0.13, 0.13, 0.14, 1),
    WELD=(0.20, 0.18, 0.16, 1), BLACK=(0.01, 0.01, 0.01, 1),
)
globals().update(P)
CENTER = Vector((0, CY, 0.24))
random.seed(7)


def smooth(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


def slit_top(a_deg):
    """Bottom edge of the brow plate (top of the eye slit)."""
    t = (abs(a_deg) - NOSE_DEG) / (SLIT_OUT_DEG - NOSE_DEG)
    return BROW_INNER_Z + (BROW_OUTER_Z - BROW_INNER_Z) * max(0.0, min(1.0, t))


def dirv(a_deg):
    a = math.radians(a_deg); return Vector((math.sin(a), -math.cos(a), 0))


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

    def grid(self, fn, ni, nj):
        """fn(u, v) -> point; u along i (0..1), v along j (0..1)."""
        ids = [[self.add(fn(i / ni, j / nj)) for j in range(nj + 1)] for i in range(ni + 1)]
        for i in range(ni):
            for j in range(nj):
                self.face([ids[i][j], ids[i + 1][j], ids[i + 1][j + 1], ids[i][j + 1]],
                          [(i / ni, j / nj), ((i + 1) / ni, j / nj), ((i + 1) / ni, (j + 1) / nj), (i / ni, (j + 1) / nj)])

    def tube(self, pts, normals, radii, seg=8, closed=False):
        n = len(pts); rings = []
        for k in range(n):
            t = (pts[(k + 1) % n] - pts[k - 1]) if closed else (pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)])
            t.normalize(); nn = (normals[k] - t * normals[k].dot(t)).normalized(); b = t.cross(nn)
            r = radii[k]
            rings.append([self.add(pts[k] + r * (math.cos(2 * math.pi * s / seg) * nn + math.sin(2 * math.pi * s / seg) * b))
                          for s in range(seg)])
        for k in range(n if closed else n - 1):
            a, b2 = rings[k], rings[(k + 1) % n]
            for s in range(seg):
                self.face([a[s], a[(s + 1) % seg], b2[(s + 1) % seg], b2[s]],
                          [(k / n, s / seg), (k / n, (s + 1) / seg), ((k + 1) / n, (s + 1) / seg), ((k + 1) / n, s / seg)])
        if not closed:
            for ring, rev in ((rings[0], True), (rings[-1], False)):
                c = self.add(sum((self.v[i] for i in ring), Vector()) / seg)
                for s in range(seg):
                    tri = [ring[s], ring[(s + 1) % seg], c]
                    self.face(tri[::-1] if rev else tri)

    def cylinder(self, c, axis, r, h, seg=6, twist=0.0):
        """Capped prism (seg=6 -> hex bolt head) standing on c along axis."""
        axis = axis.normalized(); u = axis.orthogonal().normalized(); w = axis.cross(u)
        bot, top = [], []
        for s in range(seg):
            ang = 2 * math.pi * s / seg + twist
            d = r * (math.cos(ang) * u + math.sin(ang) * w)
            bot.append(self.add(c + d)); top.append(self.add(c + d + axis * h))
        for s in range(seg):
            s2 = (s + 1) % seg
            self.face([bot[s], bot[s2], top[s2], top[s]])
        self.face(bot[::-1]); self.face(top)

    def build(self, name, mat, closed_manifold, coll, flat=True):
        bm = bmesh.new()
        bv = [bm.verts.new(c) for c in self.v]
        uv = bm.loops.layers.uv.new("UVMap")
        for idx, uvs in self.f:
            fc = bm.faces.new([bv[i] for i in idx])
            fc.smooth = not flat
            for loop, t in zip(fc.loops, uvs): loop[uv].uv = t
        bm.normal_update()
        if closed_manifold:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        else:  # open sheet: make normals face away from the head
            score = sum(fc.normal.dot(fc.calc_center_median() - CENTER) for fc in bm.faces)
            if score < 0:
                bmesh.ops.reverse_faces(bm, faces=bm.faces)
        origin = sum(self.v, Vector()) / len(self.v)
        bmesh.ops.translate(bm, verts=bm.verts, vec=-origin)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        me.materials.append(mat)
        ob = bpy.data.objects.new(name, me); ob.location = origin
        coll.objects.link(ob)
        return ob


def material(name, rgba, metal, rough):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = rgba
    return m


def add_solidify(ob, thick=None):
    s = ob.modifiers.new("Thickness", 'SOLIDIFY'); s.thickness = thick or THICK; s.offset = 1.0
    s.use_even_offset = True
    bv = ob.modifiers.new("EdgeBevel", 'BEVEL'); bv.width = (thick or THICK) * 0.25; bv.segments = 1
    bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(60)


# ---------------------------------------------------------------------------
# Scene + player
# ---------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=PLAYER)
scene = bpy.context.scene
player_coll = bpy.data.collections.new("Player"); scene.collection.children.link(player_coll)
for o in list(scene.collection.objects):
    scene.collection.objects.unlink(o); player_coll.objects.link(o)
arm = next(o for o in player_coll.objects if o.type == 'ARMATURE')

dg = bpy.context.evaluated_depsgraph_get()
def bvh_of(ob):
    ev = ob.evaluated_get(dg); me = ev.to_mesh()
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(ob.matrix_world); t = BVHTree.FromBMesh(bm)
    closed = all(e.is_manifold for e in bm.edges)   # inside test only valid on closed meshes
    bm.free(); ev.to_mesh_clear(); return t, closed
body = [bvh_of(o) for o in player_coll.objects if o.type == 'MESH']

# Head radius table: outermost player surface along each horizontal ray from the axis.
A_STEP, Z_STEP, Z0, Z1 = 2, 0.005, 0.09, 0.42
head_r = {}
for ai in range(-180, 180, A_STEP):
    d = dirv(ai)
    for zi in range(int(round((Z1 - Z0) / Z_STEP)) + 1):
        z = Z0 + zi * Z_STEP; best = 0.0
        for t, _ in body:
            hit = t.ray_cast(Vector((0, CY, z)) + d * 0.6, -d, 0.6)[0]
            if hit is not None:
                best = max(best, (hit - Vector((0, CY, z))).length)
        head_r[ai, zi] = best


def R(a_deg, z, zlo=Z0):
    """Plate radius: head radius (max over a window around the point) + clearance."""
    best = 0.0
    a0 = int(math.floor((a_deg - FIT_WIN_DEG) / A_STEP)) * A_STEP
    for ai in range(a0, int(a_deg + FIT_WIN_DEG) + A_STEP, A_STEP):
        aw = ((ai + 180) % 360) - 180
        for zi in range(int((max(zlo, z - FIT_WIN_Z) - Z0) / Z_STEP), int((z + FIT_WIN_Z - Z0) / Z_STEP) + 1):
            best = max(best, head_r.get((aw, zi), 0.0))
    return max(R_MIN, best + CLEAR)


def plate_pt(a_deg, z, extra=0.0, dent=True, zlo=Z0):
    r = R(a_deg, z, zlo) + extra + (random.random() * DENT if dent else 0.0)
    return Vector((0, CY, z)) + dirv(a_deg) * r


def outward(a_deg, z):
    return dirv(a_deg)


hcoll = bpy.data.collections.new("MetalHelmet"); scene.collection.children.link(hcoll)
STEEL_M = material("Helmet_Steel", STEEL, 0.9, 0.55)
RUST_M = material("Helmet_RustSteel", RUST, 0.55, 0.75)
DARK_M = material("Helmet_DarkSteel", DARK, 0.9, 0.5)
WELD_M = material("Helmet_Weld", WELD, 0.7, 0.85)
BLACK_M = material("Helmet_Holes", BLACK, 0.0, 1.0)
parts = []


def panel(name, mat, a0, a1, zbot, ztop, ni, nj, extra=0.0, zlo=Z0):
    """Curved sheet between azimuths a0..a1; zbot/ztop may be functions of azimuth."""
    zb = zbot if callable(zbot) else (lambda a: zbot)
    zt = ztop if callable(ztop) else (lambda a: ztop)
    def fn(u, v):
        a = a0 + (a1 - a0) * u
        return plate_pt(a, zb(a) + (zt(a) - zb(a)) * v, extra, zlo=zlo)
    b = Builder(); b.grid(fn, ni, nj)
    ob = b.build(name, mat, False, hcoll); add_solidify(ob); parts.append(ob); return ob


# ---------------------------------------------------------------------------
# Plates
# ---------------------------------------------------------------------------
# Brow plate: across the forehead, bottom edge forms the top of both eye slits
panel("Helmet_BrowPlate", STEEL_M, -SIDE_DEG, SIDE_DEG, slit_top, SEAM_Z + 0.004, 16, 3)
# Nose bridge between the slits (sits a hair proud, welded on)
panel("Helmet_NoseBridge", RUST_M, -NOSE_DEG, NOSE_DEG, SLIT_BOT_Z - 0.004, BROW_INNER_Z + 0.004, 2, 3, extra=0.003)
# Cheek plates outside the slits
panel("Helmet_CheekPlate_L", RUST_M, SLIT_OUT_DEG, SIDE_DEG, SLIT_BOT_Z - 0.004, lambda a: slit_top(a) + 0.004, 6, 2, extra=0.002)
panel("Helmet_CheekPlate_R", STEEL_M, -SIDE_DEG, -SLIT_OUT_DEG, SLIT_BOT_Z - 0.004, lambda a: slit_top(a) + 0.004, 6, 2, extra=0.002)
# Jaw plate wraps the muzzle down to the chin
panel("Helmet_JawPlate", STEEL_M, -SIDE_DEG, SIDE_DEG, JAW_BOT_Z, SLIT_BOT_Z, 14, 4, zlo=JAW_BOT_Z - 0.01)
# Back plate
panel("Helmet_BackPlate", RUST_M, SIDE_DEG - 3, 360 - SIDE_DEG + 3, BACK_BOT_Z, SEAM_Z + 0.004, 14, 4, extra=0.001,
      zlo=BACK_BOT_Z - 0.01)

# Dome: ring at SEAM_Z (matching the plates) closing to a point at TOP_Z
NA, NR = 20, 6
ring = [R(360 * i / NA, SEAM_Z) for i in range(NA)]
b = Builder(); cols = []
for i in range(NA):
    a = 360 * i / NA; col = []
    for j in range(NR):
        t = j / NR; z = SEAM_Z - 0.004 + (TOP_Z - SEAM_Z) * math.sin(t * math.pi / 2)
        # superellipse profile: flat-ish crown with rounded shoulders (bucket helm look)
        f = max(0.0, 1 - ((z - SEAM_Z) / (TOP_Z - SEAM_Z)) ** 4) ** 0.25 if z > SEAM_Z else 1.0
        r = ring[i] * f + random.random() * DENT
        col.append(b.add(Vector((0, CY, z)) + dirv(a) * r))
    cols.append(col)
pole = b.add((0, CY, TOP_Z))
for i in range(NA):
    i2 = (i + 1) % NA
    for j in range(NR - 1):
        b.face([cols[i][j], cols[i2][j], cols[i2][j + 1], cols[i][j + 1]],
               [(i / NA, j / NR), ((i + 1) / NA, j / NR), ((i + 1) / NA, (j + 1) / NR), (i / NA, (j + 1) / NR)])
    b.face([cols[i][NR - 1], cols[i2][NR - 1], pole], [(i / NA, (NR - 1) / NR), ((i + 1) / NA, (NR - 1) / NR), (i / NA, 1)])
dome = b.build("Helmet_Dome", STEEL_M, False, hcoll); add_solidify(dome); parts.append(dome)

# Strap band over the dome/plate seam
def band_fn(u, v):
    a = 360 * u; z = SEAM_Z - BAND_H / 2 + BAND_H * v
    return Vector((0, CY, z)) + dirv(a) * (R(a, SEAM_Z) + DENT + THICK + BAND_OUT * 0.3)
b = Builder(); b.grid(band_fn, 48, 1)
band = b.build("Helmet_Band", DARK_M, False, hcoll); add_solidify(band, 0.004); parts.append(band)

# ---------------------------------------------------------------------------
# Welds, bolts, breathing holes
# ---------------------------------------------------------------------------
def weld_line(b, pts_az_z, extra):
    pts = [plate_pt(a, z, THICK + DENT + extra, dent=False) for a, z in pts_az_z]
    nrm = [outward(a, z) for a, z in pts_az_z]
    radii = [WELD_R * (0.8 + 0.6 * abs(math.sin(k * 1.7))) for k in range(len(pts))]
    b.tube(pts, nrm, radii, seg=6)

b = Builder()
for side in (1, -1):
    # jaw-to-cheek seam and jaw-to-nose seam
    weld_line(b, [(side * (SLIT_OUT_DEG + (SIDE_DEG - SLIT_OUT_DEG) * k / 14), SLIT_BOT_Z) for k in range(29)], 0.0)
    # vertical seam between face plates and back plate
    weld_line(b, [(side * SIDE_DEG, JAW_BOT_Z + 0.03 + (SEAM_Z - JAW_BOT_Z - 0.03) * k / 24) for k in range(25)], 0.002)
weld_line(b, [(-NOSE_DEG - 1 + (2 * NOSE_DEG + 2) * k / 5, SLIT_BOT_Z) for k in range(6)], 0.003)
parts.append(b.build("Helmet_Welds", WELD_M, False, hcoll, flat=False))

b = Builder()
bolt_spots = [(a, SEAM_Z, THICK + DENT + 0.004 + BAND_OUT * 0.3) for a in range(0, 360, 30) if a not in (0,)]
bolt_spots += [(s * 70, (SLIT_BOT_Z + BROW_OUTER_Z) / 2, THICK + DENT + 0.002) for s in (1, -1)]
bolt_spots += [(s * 78, JAW_BOT_Z + 0.03, THICK + DENT) for s in (1, -1)]
bolt_spots += [(s * 160, BACK_BOT_Z + 0.03, THICK + DENT + 0.001) for s in (1, -1)]
for a, z, off in bolt_spots:
    p = plate_pt(a, z, off - 0.002, dent=False)
    b.cylinder(p, outward(a, z), BOLT_R, 0.006, seg=6, twist=random.random())
parts.append(b.build("Helmet_Bolts", DARK_M, True, hcoll))

b = Builder()
for row, z in enumerate((0.128, 0.146, 0.164)):
    for k in range(-2, 3):
        a = k * 9 + (4.5 if row == 1 else 0)
        if row == 1 and k == 2: continue
        p = plate_pt(a, z, THICK + DENT - 0.001, dent=False, zlo=JAW_BOT_Z - 0.01)
        b.cylinder(p, outward(a, z), HOLE_R, 0.0018, seg=10)
parts.append(b.build("Helmet_BreathingHoles", BLACK_M, True, hcoll))

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
def is_inside(t, p):
    """Ray-parity test: odd number of surface crossings = inside a closed mesh."""
    n, o, d = 0, p.copy(), Vector((0.0123, 0.0071, 1.0)).normalized()
    while True:
        hit = t.ray_cast(o, d)[0]
        if hit is None: return n % 2 == 1
        n += 1; o = hit + d * 1e-5


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
            if closed and is_inside(t, p): inside += 1; dist = -dist
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
    target = Vector((0, CY, 0.24))
    shots = (("front", 0, 5, 1.0), ("front_3q", -35, 12, 1.0), ("side", 90, 5, 1.0),
             ("back_3q", 145, 20, 1.0), ("full_body", -25, 10, 2.6))
    for name, az, el, dist in shots:
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
