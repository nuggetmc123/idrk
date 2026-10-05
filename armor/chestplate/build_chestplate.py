"""
Scrap-metal chest plate generator for the PeeperLeeper player model.

Builds a rusty, bolted-together chest rig (breastplate, back plate, belly lames,
gorget, pauldrons, straps, buckles, scrap patch, welds, rivets) that is fitted to
the player's body by ray-casting against the real mesh, skinned to the player's
own armature, UV-unwrapped into one shared texture atlas, and exported as FBX.

Every part is its own object, so you can edit any of them by hand in Blender
afterwards. For bigger changes, edit the SETTINGS block below and re-run:

    blender --background --python build_chestplate.py
    # or, with the `bpy` pip module:
    python3 build_chestplate.py

Outputs (next to this script):
    ChestPlate.fbx            - rigged armor, one object per part
    ChestPlate_Merged.fbx     - same armor merged into a single skinned mesh
    ChestPlate.blend          - editable scene with the player model for reference
    textures/                 - BaseColor / Roughness / Metallic / Normal atlas,
                                plus a Unity-style MetallicSmoothness map
    previews/                 - renders of the result

Coordinates are Blender world space with the model as imported: Z is up, the
character faces -Y, and its left side is +X.
"""

import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Matrix, noise, kdtree
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------------------
# SETTINGS - tweak these and re-run the script
# ---------------------------------------------------------------------------
PLAYER_FBX = os.path.join(HERE, "..", "PeeperLeeper.fbx")
OUT_DIR = HERE
SEED = 7                    # change for a different set of dents / rust / weld blobs

CLEARANCE = 0.010           # gap between the skin and the inside of a plate
THICKNESS = 0.007           # sheet metal thickness
LIFT = 0.009                # how far an overlapping edge rides over the plate below
BEVEL = 0.0022              # edge rounding on every plate
HAMMER = 0.0012             # uneven, hand-hammered wobble on every plate
DENTS = 6                   # dents per big plate
DENT_DEPTH = 0.0045
DENT_RADIUS = 0.028
RIVET_RADIUS = 0.0048
RIVET_SPACING = 0.050
RIM_RADIUS = 0.0045         # rolled / welded-on edge bars
FBX_METALLIC = 0.35          # metal / roughness values on the exported material
FBX_ROUGHNESS = 0.5
GRIME = 0.0                 # 0 = clean finish, 1 = paint chips, dirt in the gaps, blotchy tone
USE_TEXTURES = False        # False = plain colour materials (imports reliably anywhere);
                            # True = bake a texture atlas (needed for RUST / GRIME detail)
FLAT_MATERIALS = {          # name, base colour (linear RGB), metallic, roughness - edit freely
    'rust':       ("Steel",          (0.13, 0.127, 0.124), 0.35, 0.50),
    'rust_dark':  ("Steel_Dark",     (0.07, 0.069, 0.067), 0.35, 0.50),
    'galvanized': ("Steel_Light",    (0.22, 0.224, 0.228), 0.35, 0.45),
    'steel':      ("Steel_Hardware", (0.08, 0.079, 0.077), 0.40, 0.45),
    'weld':       ("Weld",           (0.07, 0.065, 0.07), 0.30, 0.60),
    'paint':      ("Paint_Teal",     (0.06, 0.17, 0.18), 0.0, 0.60),
    'hazard':     ("Paint_Yellow",   (0.62, 0.43, 0.05), 0.0, 0.60),
    'leather':    ("Leather",        (0.115, 0.058, 0.026), 0.0, 0.75),
}
RUST = 0.0                  # 0 = clean steel, 1 = fully rusted scrap (scales every rust patch)

# Torso axis (the body is a rounded pod centred here, measured from the mesh).
AXIS_Y = 0.015

BREASTPLATE = dict(
    theta=80,                                  # half-width in degrees around the body
    top=[(0.0, 0.040), (0.42, 0.082), (0.78, 0.050), (1.0, -0.020)],   # neckline -> armpit
    bottom=-0.080, bottom_side_rise=0.020,
    ridge=0.007, ridge_width=16,               # raised centre keel
)
BACKPLATE = dict(
    theta=74,
    top=[(0.0, 0.088), (0.5, 0.078), (0.8, 0.045), (1.0, -0.020)],
    bottom=-0.215, bottom_side_rise=0.020,
)
BELLY_LAMES = dict(count=3, top=-0.065, bottom=-0.235, overlap=0.014, theta=[72, 68, 62])
GORGET = dict(bottom=0.048, top=0.086, front_drop=0.020, flare=0.010, gap=0.002, max_radius=0.168)
PAULDRON = dict(
    cap=(0.020, 0.125),          # start/end distance along the upper-arm bone
    cap_angle=(85, 118),         # half-angle around the arm at the inner / outer end
    dome=0.016,                  # how much the cap bulges
    lames=2, lame_len=0.040, lame_overlap=0.012, lame_angle=105,
)
STRAPS = dict(heights=[-0.092, -0.180], width=0.018, theta=(62, 120), thickness=0.0035)
SCRAP_PATCH = dict(enabled=False, theta=34, z=0.010, half_theta=17, half_z=0.032, skew=0.010)
CRACK_WELD = dict(enabled=False, points=[(-26, 0.060), (-30, 0.030), (-24, 0.005), (-31, -0.030), (-27, -0.060)])

ATLAS_SIZE = 1024
BAKE_SAMPLES = 16
RENDER_PREVIEWS = True
# ---------------------------------------------------------------------------

rng = random.Random(SEED)
ARM = BODY = None
BODY_BVH = None
LAYER_BVH = None            # body + everything built so far (for stacking)
BUILT = []                  # (object, material key, allowed bones)
BODY_POLYS = None


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def curve(points, t):
    """Piecewise smooth curve through (t, value) pairs."""
    t = max(points[0][0], min(points[-1][0], t))
    for (t0, v0), (t1, v1) in zip(points, points[1:]):
        if t <= t1:
            return lerp(v0, v1, smoothstep(0, 1, (t - t0) / (t1 - t0)))
    return points[-1][1]


# ---------------------------------------------------------------------------
# Scene / player setup
# ---------------------------------------------------------------------------
def load_player():
    global ARM, BODY, BODY_BVH, LAYER_BVH, BODY_POLYS
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(PLAYER_FBX))
    ARM = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    BODY = max(meshes, key=lambda o: len(o.data.vertices))
    ARM.data.pose_position = 'REST'         # fit everything to the T-pose rest shape
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = BODY.evaluated_get(dg)
    me = ev.to_mesh()
    verts = [BODY.matrix_world @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    BODY_POLYS = (verts, polys)
    BODY_BVH = BVHTree.FromPolygons(verts, polys)
    LAYER_BVH = BODY_BVH
    col = bpy.data.collections.new("ChestPlate")
    bpy.context.scene.collection.children.link(col)
    return col


def rebuild_layer_bvh():
    """BVH of the body plus every armor part built so far."""
    global LAYER_BVH
    verts, polys = list(BODY_POLYS[0]), list(BODY_POLYS[1])
    dg = bpy.context.evaluated_depsgraph_get()
    for obj, _, _ in BUILT:
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        off = len(verts)
        verts += [obj.matrix_world @ v.co for v in me.vertices]
        polys += [tuple(i + off for i in p.vertices) for p in me.polygons]
        ev.to_mesh_clear()
    LAYER_BVH = BVHTree.FromPolygons(verts, polys)


def outer_hit(origin, d, dmax, bvh=None):
    """Distance to the outermost surface along a ray from inside the body."""
    bvh = bvh or LAYER_BVH
    last, travelled, o = None, 0.0, origin.copy()
    for _ in range(12):
        hit = bvh.ray_cast(o, d, dmax - travelled)
        if hit[0] is None:
            break
        travelled += hit[3] + 1e-5
        last = travelled
        o = origin + d * travelled
    return last


def first_hit(origin, d, dmax):
    hit = BODY_BVH.ray_cast(origin, d, dmax)
    return hit[3] if hit[0] is not None else None


# ---------------------------------------------------------------------------
# Shell grids: a grid of points fitted to the body, then dented and pushed clear
# ---------------------------------------------------------------------------
def torso_dir(theta_deg):
    t = math.radians(theta_deg)
    return Vector((math.sin(t), -math.cos(t), 0.0))


def torso_point(theta_deg, z, extra, layered=False, rmax=0.19):
    o = Vector((0.0, AXIS_Y, z))
    d = torso_dir(theta_deg)
    r = outer_hit(o, d, 0.35) if layered else first_hit(o, d, 0.35)
    if r is None or r > rmax:            # ray ran down an arm: clamp, push-out fixes it
        r = rmax if r is None else min(r, rmax)
    return o + d * (r + extra)


def grid_normals(P, cyclic=False):
    nu, nv = len(P), len(P[0])
    N = [[None] * nv for _ in range(nu)]
    for i in range(nu):
        for j in range(nv):
            if cyclic:
                du = P[(i + 1) % nu][j] - P[(i - 1) % nu][j]
            else:
                du = P[min(i + 1, nu - 1)][j] - P[max(i - 1, 0)][j]
            dv = P[i][min(j + 1, nv - 1)] - P[i][max(j - 1, 0)]
            n = du.cross(dv)
            N[i][j] = n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))
    # orient away from the body
    votes = 0
    for i in range(0, nu, max(1, nu // 6)):
        for j in range(0, nv, max(1, nv // 4)):
            loc, nrm, _, _ = BODY_BVH.find_nearest(P[i][j])
            votes += 1 if (P[i][j] - loc).dot(N[i][j]) > 0 else -1
    if votes < 0:
        N = [[-n for n in row] for row in N]
    return N, votes < 0


def smooth_grid(P, iters, amount=0.5, cyclic=False, keep_edges=False):
    nu, nv = len(P), len(P[0])
    for _ in range(iters):
        Q = [row[:] for row in P]
        for i in range(nu):
            for j in range(nv):
                edge_u = not cyclic and (i == 0 or i == nu - 1)
                edge_v = j == 0 or j == nv - 1
                if keep_edges and (edge_u or edge_v):
                    continue
                nb = []
                if cyclic or i > 0: nb.append(P[(i - 1) % nu][j])
                if cyclic or i < nu - 1: nb.append(P[(i + 1) % nu][j])
                if j > 0: nb.append(P[i][j - 1])
                if j < nv - 1: nb.append(P[i][j + 1])
                avg = sum(nb, Vector()) / len(nb)
                Q[i][j] = P[i][j].lerp(avg, amount)
        P = Q
    return P


def push_clear(P, min_clear):
    """Make sure no point sits closer than min_clear to (or inside) the body."""
    for _ in range(2):
        for row in P:
            for j, p in enumerate(row):
                loc, nrm, _, dist = BODY_BVH.find_nearest(p)
                if loc is None:
                    continue
                side = (p - loc).dot(nrm)
                if side < 0 or dist < min_clear:
                    row[j] = loc + nrm * min_clear
    return P


def weather(P, dents=0, cyclic=False):
    """Hammered wobble + a few dents, so the plates look hand-made."""
    N, _ = grid_normals(P, cyclic)
    seed = Vector((rng.random() * 50, rng.random() * 50, rng.random() * 50))
    for i, row in enumerate(P):
        for j, p in enumerate(row):
            row[j] = p + N[i][j] * HAMMER * noise.noise(p * 38 + seed)
    nu, nv = len(P), len(P[0])
    for _ in range(dents):
        c = P[rng.randrange(nu)][rng.randrange(1, max(2, nv - 1))].copy()
        rad = DENT_RADIUS * rng.uniform(0.5, 1.1)
        depth = DENT_DEPTH * rng.uniform(0.5, 1.0)
        for i, row in enumerate(P):
            for j, p in enumerate(row):
                d = (p - c).length / rad
                if d < 1:
                    row[j] = p - N[i][j] * depth * (1 - d * d) ** 2
    return P


def grid_to_object(name, P, col, cyclic=False, thickness=THICKNESS, bevel=BEVEL):
    N, flipped = grid_normals(P, cyclic)
    nu, nv = len(P), len(P[0])
    bm = bmesh.new()
    V = [[bm.verts.new(p) for p in row] for row in P]
    for i in range(nu if cyclic else nu - 1):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            q = (V[i][j], V[i2][j], V[i2][j + 1], V[i][j + 1])
            bm.faces.new(q[::-1] if flipped else q)
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = True
    if thickness > 0:
        s = obj.modifiers.new("Thickness", 'SOLIDIFY')
        s.thickness = thickness
        s.offset = 1.0
        s.use_even_offset = True
        s.use_rim = True
    if bevel > 0:
        b = obj.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 2
        b.limit_method = 'ANGLE'
        b.angle_limit = math.radians(35)
        b.harden_normals = True
    apply_modifiers(obj)
    return obj, N


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.view_layer.objects:
        o.select_set(o == obj)
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def mesh_object(name, bm, col, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = smooth
    return obj


# ---------------------------------------------------------------------------
# Small hardware: rivets, bolts, tubes (rolled rims / weld beads), buckles
# ---------------------------------------------------------------------------
def frame_from_normal(n):
    n = n.normalized()
    ref = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
    x = ref.cross(n).normalized()
    y = n.cross(x)
    return Matrix((x, y, n)).transposed()


def add_rivet(bm, pos, n, r=RIVET_RADIUS):
    res = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=r)
    rot = frame_from_normal(n)
    for v in res['verts']:
        c = v.co
        c.z = max(c.z, -r * 0.15) * 0.55                 # flattened dome head
        v.co = pos + rot @ c


def add_bolt(bm, pos, n, r=RIVET_RADIUS * 1.25):
    rot = frame_from_normal(n)
    spin = Matrix.Rotation(rng.uniform(0, math.pi), 3, 'Z')
    washer = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=r * 1.5, radius2=r * 1.5, depth=r * 0.25)
    for v in washer['verts']:
        v.co = pos + rot @ (v.co + Vector((0, 0, r * 0.12)))
    head = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r, radius2=r, depth=r * 0.8)
    for v in head['verts']:
        v.co = pos + rot @ (spin @ v.co + Vector((0, 0, r * 0.65)))


def add_tube(bm, pts, nrms, radius_fn, sides=8, closed=False):
    rings = []
    n = len(pts)
    for k, p in enumerate(pts):
        a = pts[(k - 1) % n] if closed or k > 0 else p
        b = pts[(k + 1) % n] if closed or k < n - 1 else p
        t = (b - a).normalized()
        up = (nrms[k] - t * nrms[k].dot(t)).normalized()
        side = t.cross(up)
        r = radius_fn(k / max(1, n - 1))
        rings.append([bm.verts.new(p + (up * math.cos(s / sides * math.tau) + side * math.sin(s / sides * math.tau)) * r)
                      for s in range(sides)])
    for k in range(n if closed else n - 1):
        r0, r1 = rings[k], rings[(k + 1) % n]
        for s in range(sides):
            bm.faces.new((r0[s], r0[(s + 1) % sides], r1[(s + 1) % sides], r1[s]))
    if not closed:
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])


def add_box(bm, center, axes, half):
    res = bmesh.ops.create_cube(bm, size=2.0)
    m = Matrix((axes[0] * half[0], axes[1] * half[1], axes[2] * half[2])).transposed()
    for v in res['verts']:
        v.co = center + m @ v.co


def resample(pts, nrms, spacing):
    """Points every `spacing` along a polyline (with interpolated normals)."""
    out, out_n, acc = [pts[0]], [nrms[0]], 0.0
    for a, b, na, nb in zip(pts, pts[1:], nrms, nrms[1:]):
        seg = (b - a).length
        while acc + seg >= spacing:
            t = (spacing - acc) / seg
            a, na = a.lerp(b, t), na.lerp(nb, t).normalized()
            seg = (b - a).length
            out.append(a)
            out_n.append(na)
            acc = 0.0
        acc += seg
    return out, out_n


def rivet_line(bm, P, N, idx, spacing=RIVET_SPACING, outset=THICKNESS, bolts=False):
    pts = [P[i][j] + N[i][j] * outset for i, j in idx]
    nrm = [N[i][j] for i, j in idx]
    rp, rn = resample(pts, nrm, spacing)
    if len(rp) > 2:          # keep a margin at both ends
        rp, rn = rp[1:-1], rn[1:-1]
    for p, n in zip(rp, rn):
        (add_bolt if bolts else add_rivet)(bm, p, n)


def rim_along(bm, P, N, idx, radius=RIM_RADIUS, outset=THICKNESS * 0.5):
    pts = [P[i][j] + N[i][j] * outset for i, j in idx]
    nrm = [N[i][j] for i, j in idx]
    add_tube(bm, pts, nrm, lambda t: radius * (0.9 + 0.1 * math.sin(t * 40)), sides=8)


def weld_along(bm, pts, nrms, radius=0.0032):
    pts, nrms = resample(pts, nrms, 0.0025)
    ph = rng.random() * 10
    add_tube(bm, pts, nrms,
             lambda t: radius * (0.75 + 0.35 * abs(math.sin(t * len(pts) * 0.9 + ph))
                                 + 0.25 * noise.noise(Vector((t * 30 + ph, 0, 0)))),
             sides=7)


# ---------------------------------------------------------------------------
# Armor parts
# ---------------------------------------------------------------------------
def register(obj, mat, bones):
    BUILT.append((obj, mat, bones))
    return obj


TORSO = ["Hips", "Spine", "Chest"]


def build_breastplate(col):
    S = BREASTPLATE
    nu, nv = 34, 16
    P = []
    for i in range(nu):
        u = i / (nu - 1)
        th = lerp(-S['theta'], S['theta'], u)
        t = abs(th) / S['theta']
        z0 = S['bottom'] + S['bottom_side_rise'] * t * t
        z1 = curve(S['top'], t)
        row = []
        for j in range(nv):
            v = j / (nv - 1)
            z = lerp(z0, z1, v)
            ridge = S['ridge'] * max(0.0, 1 - abs(th) / S['ridge_width']) ** 2 * math.sin(math.pi * min(1, v * 1.15)) ** 0.5
            lift = LIFT * (1 - smoothstep(0, BELLY_LAMES['overlap'] / max(1e-6, z1 - z0) * 1.3, v))
            row.append(torso_point(th, z, CLEARANCE + ridge + lift))
        P.append(row)
    P = smooth_grid(P, 3, 0.4)
    P = weather(P, DENTS)
    P = push_clear(P, CLEARANCE * 0.8)
    obj, N = grid_to_object("Breastplate", P, col)
    register(obj, 'rust', TORSO)
    bm = bmesh.new()
    rivet_line(bm, P, N, [(i, nv - 2) for i in range(1, nu - 1)])                 # along neckline
    rivet_line(bm, P, N, [(i, 1) for i in range(1, nu - 1)])                      # bottom edge
    register(mesh_object("Rivets_Breastplate", bm, col), 'steel', TORSO)
    bm = bmesh.new()
    rim_along(bm, P, N, [(i, nv - 1) for i in range(nu)])
    register(mesh_object("Rim_Breastplate", bm, col), 'rust_dark', TORSO)
    return P, N


def build_backplate(col):
    S = BACKPLATE
    nu, nv = 30, 18
    P = []
    for i in range(nu):
        u = i / (nu - 1)
        th = 180 + lerp(-S['theta'], S['theta'], u)
        t = abs(th - 180) / S['theta']
        z0 = S['bottom'] + S['bottom_side_rise'] * t * t
        z1 = curve(S['top'], t)
        row = [torso_point(th, lerp(z0, z1, j / (nv - 1)), CLEARANCE) for j in range(nv)]
        P.append(row)
    P = smooth_grid(P, 3, 0.4)
    P = weather(P, DENTS)
    P = push_clear(P, CLEARANCE * 0.8)
    obj, N = grid_to_object("Backplate", P, col)
    register(obj, 'rust', TORSO)
    bm = bmesh.new()
    rivet_line(bm, P, N, [(i, nv - 2) for i in range(1, nu - 1)])
    rivet_line(bm, P, N, [(i, 1) for i in range(1, nu - 1)])
    # a vertical repair seam down the middle, bolted down one side
    mid = nu // 2
    rivet_line(bm, P, N, [(mid - 2, j) for j in range(1, nv - 1)], spacing=0.045, bolts=True)
    register(mesh_object("Bolts_Backplate", bm, col), 'steel', TORSO)
    bm = bmesh.new()
    rim_along(bm, P, N, [(i, nv - 1) for i in range(nu)])
    weld_along(bm, [P[mid][j] + N[mid][j] * THICKNESS for j in range(1, nv - 1)],
               [N[mid][j] for j in range(1, nv - 1)])
    register(mesh_object("Rim_Backplate", bm, col), 'rust_dark', TORSO)


def build_belly_lames(col):
    S = BELLY_LAMES
    n = S['count']
    h = (S['top'] - S['bottom']) / n
    for k in range(n):
        z1 = S['top'] - k * h + S['overlap']
        z0 = S['top'] - (k + 1) * h
        half = S['theta'][k]
        nu, nv = 28, 7
        P = []
        for i in range(nu):
            th = lerp(-half, half, i / (nu - 1))
            t = abs(th) / half
            row = []
            for j in range(nv):
                v = j / (nv - 1)
                lift = LIFT * (1 - smoothstep(0, 0.45, v)) if k < n - 1 else 0.0
                row.append(torso_point(th, lerp(z0 + 0.010 * t * t, z1, v), CLEARANCE + lift))
            P.append(row)
        P = smooth_grid(P, 2, 0.4)
        P = weather(P, 2)
        P = push_clear(P, CLEARANCE * 0.8)
        obj, N = grid_to_object(f"BellyLame_{k + 1}", P, col)
        register(obj, 'galvanized' if k == 1 else 'rust', TORSO)
        bm = bmesh.new()
        rivet_line(bm, P, N, [(i, nv // 2) for i in range(1, nu - 1)], spacing=0.065)
        register(mesh_object(f"Rivets_BellyLame_{k + 1}", bm, col), 'steel', TORSO)


def build_straps(col):
    S = STRAPS
    for side, sx in (("L", 1), ("R", -1)):
        for k, zc in enumerate(S['heights']):
            nu, nv = 26, 3
            raw = []
            for i in range(nu):
                th = sx * lerp(S['theta'][0], S['theta'][1], i / (nu - 1))
                raw.append([(th, lerp(zc - S['width'] / 2, zc + S['width'] / 2, j / (nv - 1))) for j in range(nv)])
            # radius from the outermost surface, then bridge the gap between plates tautly
            rad = [[outer_hit(Vector((0, AXIS_Y, z)), torso_dir(th), 0.35) or 0.15 for th, z in row] for row in raw]
            col_r = [max(r) for r in rad]
            taut = []
            for i in range(nu):
                w = col_r[max(0, i - 4):i + 5]
                taut.append(max(col_r[i], sum(w) / len(w)))
            for _ in range(3):
                taut = [taut[0]] + [(taut[i - 1] + taut[i] * 2 + taut[i + 1]) / 4 for i in range(1, nu - 1)] + [taut[-1]]
            P = [[Vector((0, AXIS_Y, z)) + torso_dir(th) * (max(taut[i], col_r[i]) + 0.0012) for th, z in raw[i]]
                 for i in range(nu)]
            obj, N = grid_to_object(f"Strap_{k + 1}.{side}", P, col, thickness=S['thickness'], bevel=0.0008)
            register(obj, 'leather', TORSO)
            # buckle in the gap between front and back plates
            bm = bmesh.new()
            i = nu // 2
            c = P[i][1] + N[i][1] * (S['thickness'] + 0.0015)
            tng = (P[i + 1][1] - P[i - 1][1]).normalized()
            n = N[i][1]
            up = n.cross(tng).normalized()
            bw, bh, bar = 0.011, S['width'] * 0.75, 0.0018
            for off, hx, hy in ((Vector(), bar, bh), (tng * bw * 2, bar, bh)):
                add_box(bm, c + off - tng * bw, (tng, up, n), (hx, hy, bar))
            for s in (-1, 1):
                add_box(bm, c + up * s * bh, (tng, up, n), (bw + bar, bar, bar))
            add_box(bm, c, (tng, up, n), (bw, bar * 0.6, bar * 0.7))           # prong
            for j in (2, nu - 3):                                                # strap rivets
                add_rivet(bm, P[j][1] + N[j][1] * S['thickness'], N[j][1], RIVET_RADIUS * 0.8)
            register(mesh_object(f"Buckle_{k + 1}.{side}", bm, col), 'steel', TORSO)


def build_scrap_patch(col):
    S = SCRAP_PATCH
    nu, nv = 14, 12
    P = []
    for i in range(nu):
        u = i / (nu - 1)
        th = S['theta'] + lerp(-S['half_theta'], S['half_theta'], u)
        row = []
        for j in range(nv):
            v = j / (nv - 1)
            z = S['z'] + lerp(-S['half_z'], S['half_z'], v) + S['skew'] * (u - 0.5) * 2
            o = Vector((0, AXIS_Y, z))
            d = torso_dir(th)
            r = outer_hit(o, d, 0.35) or 0.15
            row.append(o + d * (r + 0.0012))
        P.append(row)
    P = smooth_grid(P, 4, 0.5)
    # patch is a flat-ish sheet: blend toward a plane so it bridges the plate curve
    c = sum((p for row in P for p in row), Vector()) / (nu * nv)
    nrm = (P[-1][nv // 2] - P[0][nv // 2]).cross(P[nu // 2][-1] - P[nu // 2][0]).normalized()
    if nrm.dot(c - Vector((0, AXIS_Y, c.z))) < 0:
        nrm = -nrm
    for row in P:
        for j, p in enumerate(row):
            flat = p - nrm * (p - c).dot(nrm)
            q = p.lerp(flat, 0.45)
            if (q - c).dot(nrm) < (p - c).dot(nrm):
                q = p                                  # never sink into the plate
            row[j] = q
    P = weather(P, 2)
    obj, N = grid_to_object("ScrapPatch", P, col, thickness=THICKNESS * 0.7)
    register(obj, 'hazard', TORSO)
    bm = bmesh.new()
    for i, j in ((1, 1), (nu - 2, 1), (1, nv - 2), (nu - 2, nv - 2), (nu // 2, nv - 2)):
        add_bolt(bm, P[i][j] + N[i][j] * THICKNESS * 0.7, N[i][j])
    register(mesh_object("Bolts_ScrapPatch", bm, col), 'steel', TORSO)
    bm = bmesh.new()
    edge = [(0, j) for j in range(nv)] + [(i, 0) for i in range(1, nu)]
    weld_along(bm, [P[i][j] for i, j in edge], [N[i][j] for i, j in edge])
    register(mesh_object("Weld_ScrapPatch", bm, col), 'weld', TORSO)


def build_crack_weld(col):
    pts, nrms = [], []
    S = CRACK_WELD['points']
    for k in range(len(S) - 1):
        for s in range(8):
            t = s / 8
            th = lerp(S[k][0], S[k + 1][0], t)
            z = lerp(S[k][1], S[k + 1][1], t)
            o = Vector((0, AXIS_Y, z))
            d = torso_dir(th)
            r = outer_hit(o, d, 0.35) or 0.15
            pts.append(o + d * r)
            nrms.append(d)
    bm = bmesh.new()
    weld_along(bm, pts, nrms, radius=0.0028)
    register(mesh_object("Weld_Crack", bm, col), 'weld', TORSO)


def build_gorget(col):
    S = GORGET
    nu, nv = 48, 6
    zs, R = [], []
    for i in range(nu):
        th = i / nu * 360.0
        front = max(0.0, math.cos(math.radians(th)))
        z1 = S['top'] - S['front_drop'] * front
        zr, rr = [], []
        for j in range(nv):
            v = j / (nv - 1)
            z = lerp(S['bottom'] - S['front_drop'] * front * 0.5, z1, v)
            r = outer_hit(Vector((0, AXIS_Y, z)), torso_dir(th), 0.30) or S['max_radius']
            zr.append(z)
            rr.append(min(r, S['max_radius']))     # rays toward the arms run long: cap them
        zs.append(zr)
        R.append(rr)
    for _ in range(6):                             # smooth the radius around the ring
        R = [[(R[i - 1][j] + 2 * R[i][j] + R[(i + 1) % nu][j]) / 4 for j in range(nv)] for i in range(nu)]
    P = [[Vector((0, AXIS_Y, zs[i][j])) + torso_dir(i / nu * 360.0) * (R[i][j] + S['gap'] + S['flare'] * (1 - j / (nv - 1)) ** 2)
          for j in range(nv)] for i in range(nu)]
    P = smooth_grid(P, 4, 0.5, cyclic=True)
    P = weather(P, 1, cyclic=True)
    P = push_clear(P, CLEARANCE)
    obj, N = grid_to_object("Gorget", P, col, cyclic=True)
    register(obj, 'rust_dark', ["Chest"])
    bm = bmesh.new()
    rim_along(bm, P, N, [(i % nu, nv - 1) for i in range(nu + 1)], radius=RIM_RADIUS * 0.9)
    rivet_line(bm, P, N, [(i % nu, 1) for i in range(nu + 1)], spacing=0.065)
    register(mesh_object("Rim_Gorget", bm, col), 'steel', ["Chest"])


def arm_frame(sx):
    bone = ARM.data.bones[f"Upperarm.{'L' if sx > 0 else 'R'}"]
    h = ARM.matrix_world @ bone.head_local
    t = ARM.matrix_world @ bone.tail_local
    a = (t - h).normalized()
    up = (Vector((0, 0, 1)) - a * a.z).normalized()
    fwd = a.cross(up) * sx                     # toward -Y (front) on both sides
    if fwd.y > 0:
        fwd = -fwd
    return h, a, up, fwd


def arm_point(sx, t, phi_deg, extra, dmax=0.12):
    h, a, up, fwd = arm_frame(sx)
    o = h + a * t
    p = math.radians(phi_deg)
    d = up * math.cos(p) + fwd * math.sin(p)
    r = outer_hit(o, d, dmax)
    if r is None:
        r = 0.06
    return o + d * (r + extra), d


def build_pauldron(col, sx):
    S = PAULDRON
    side = "L" if sx > 0 else "R"
    bones = [f"Shoulder.{side}", f"Upperarm.{side}", "Chest"]
    # lames first (they tuck under the cap)
    t_start = S['cap'][1] - S['lame_overlap']
    for k in range(S['lames']):
        t0 = t_start + k * (S['lame_len'] - S['lame_overlap'])
        t1 = t0 + S['lame_len']
        nu, nv = 22, 6
        P = []
        for i in range(nu):
            phi = lerp(-S['lame_angle'], S['lame_angle'], i / (nu - 1))
            row = []
            for j in range(nv):
                v = j / (nv - 1)                      # 0 = toward shoulder, 1 = toward elbow
                lift = LIFT * smoothstep(0.55, 1.0, v) if k < S['lames'] - 1 else 0.0
                row.append(arm_point(sx, lerp(t0, t1, v), phi, CLEARANCE + lift)[0])
            P.append(row)
        P = smooth_grid(P, 2, 0.4)
        P = weather(P, 1)
        P = push_clear(P, CLEARANCE * 0.8)
        obj, N = grid_to_object(f"PauldronLame_{k + 1}.{side}", P, col)
        register(obj, 'rust', bones)
    rebuild_layer_bvh()
    # dome cap over everything below it
    nu, nv = 30, 14
    P = []
    for i in range(nu):
        u = i / (nu - 1)
        row = []
        for j in range(nv):
            v = j / (nv - 1)
            ang = lerp(S['cap_angle'][0], S['cap_angle'][1], v)
            phi = lerp(-ang, ang, u)
            dome = S['dome'] * math.sin(math.pi * min(1.0, v * 0.85 + 0.1)) * math.cos(math.radians(phi) * 0.5)
            row.append(arm_point(sx, lerp(S['cap'][0], S['cap'][1], v), phi, 0.002 + dome)[0])
        P.append(row)
    P = smooth_grid(P, 4, 0.45)
    P = weather(P, 3)
    P = push_clear(P, CLEARANCE)
    obj, N = grid_to_object(f"PauldronCap.{side}", P, col)
    register(obj, 'paint', bones)
    bm = bmesh.new()
    rim_along(bm, P, N, [(i, nv - 1) for i in range(nu)])
    rivet_line(bm, P, N, [(i, nv - 3) for i in range(1, nu - 1)], spacing=0.042)
    register(mesh_object(f"Rim_Pauldron.{side}", bm, col), 'steel', bones)


# ---------------------------------------------------------------------------
# Materials (procedural, then baked into one atlas for the FBX)
# ---------------------------------------------------------------------------
class NT:
    def __init__(self, mat):
        self.nt = mat.node_tree
        self.n = self.nt.nodes
        self.l = self.nt.links
        self.x = -1600

    def node(self, kind, **props):
        nd = self.n.new(kind)
        nd.location = (self.x, 0)
        self.x += 120
        for k, v in props.items():
            setattr(nd, k, v)
        return nd

    def link(self, src, dst):
        if isinstance(src, (int, float)):
            dst.default_value = src
        elif isinstance(src, tuple):
            dst.default_value = src if len(src) == 4 else (*src, 1.0)
        else:
            self.l.new(src, dst)

    def noise(self, vec, scale, detail=4, rough=0.55, distort=0.0):
        nd = self.node('ShaderNodeTexNoise')
        self.link(vec, nd.inputs['Vector'])
        nd.inputs['Scale'].default_value = scale
        nd.inputs['Detail'].default_value = detail
        nd.inputs['Roughness'].default_value = rough
        nd.inputs['Distortion'].default_value = distort
        return nd.outputs['Fac']

    def ramp(self, fac, a, b, interp='EASE'):
        nd = self.node('ShaderNodeMapRange')
        nd.interpolation_type = 'SMOOTHSTEP' if interp == 'EASE' else 'LINEAR'
        self.link(fac, nd.inputs['Value'])
        nd.inputs['From Min'].default_value = a
        nd.inputs['From Max'].default_value = b
        return nd.outputs['Result']

    def math(self, op, a, b=0.0, clamp=True):
        nd = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.link(a, nd.inputs[0])
        self.link(b, nd.inputs[1])
        return nd.outputs[0]

    def mixc(self, fac, a, b):
        nd = self.node('ShaderNodeMix', data_type='RGBA')
        self.link(fac, nd.inputs[0])
        self.link(a, nd.inputs[6])
        self.link(b, nd.inputs[7])
        return nd.outputs[2]

    def mixf(self, fac, a, b):
        nd = self.node('ShaderNodeMix', data_type='FLOAT')
        self.link(fac, nd.inputs[0])
        self.link(a, nd.inputs[2])
        self.link(b, nd.inputs[3])
        return nd.outputs[0]

    def value(self, v):
        nd = self.node('ShaderNodeValue')
        nd.outputs[0].default_value = v
        return nd.outputs[0]


def rust_layers(t, steel, steel_dark, rust_amount, seed):
    """Shared steel look (rust scaled by RUST): returns (color, metallic, roughness, height, coords)."""
    co = t.node('ShaderNodeTexCoord').outputs['Object']
    off = t.node('ShaderNodeVectorMath', operation='ADD')
    t.link(co, off.inputs[0])
    off.inputs[1].default_value = (seed, seed * 1.7, seed * 0.3)
    co = off.outputs[0]
    stretch = t.node('ShaderNodeVectorMath', operation='MULTIPLY')
    t.link(co, stretch.inputs[0])
    stretch.inputs[1].default_value = (9.0, 9.0, 1.0)      # vertical drip streaks
    big = t.noise(co, 9, 6, 0.62, 0.35)
    pits = t.noise(co, 95, 3, 0.5)
    tint = t.mixf(GRIME, 0.5, t.noise(co, 4, 2, 0.5))       # blotchy tone only with grime
    streak = t.noise(stretch.outputs[0], 7, 3, 0.5)
    ao = t.node('ShaderNodeAmbientOcclusion', samples=8)
    ao.inputs['Distance'].default_value = 0.02
    cav = t.math('SUBTRACT', 1.0, ao.outputs['AO'])
    m = t.math('ADD', big, t.math('MULTIPLY', pits, 0.35, False), False)
    m = t.math('ADD', m, t.math('MULTIPLY', cav, 0.5, False), False)
    m = t.math('ADD', m, t.math('MULTIPLY', t.ramp(streak, 0.55, 0.75), 0.25, False), False)
    rust = t.ramp(m, 1.02 - rust_amount * 0.5, 1.10 - rust_amount * 0.5)
    rust = t.math('MULTIPLY', rust, RUST)
    rust_col = t.mixc(t.ramp(pits, 0.35, 0.7), (0.30, 0.10, 0.03), (0.55, 0.22, 0.07))
    rust_col = t.mixc(t.ramp(big, 0.55, 0.8), rust_col, (0.16, 0.06, 0.025))
    metal_col = t.mixc(tint, steel_dark, steel)
    col = t.mixc(rust, metal_col, rust_col)
    dirt = t.mixc(t.math('MULTIPLY', t.ramp(cav, 0.1, 0.8), GRIME), (1, 1, 1), (0.45, 0.42, 0.4))
    col = t.node('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
    t.link(1.0, col.inputs[0])
    t.link(t.mixc(rust, metal_col, rust_col), col.inputs[6])
    t.link(dirt, col.inputs[7])
    col = col.outputs[2]
    metal = t.mixf(rust, 0.65, 0.05)
    rough = t.mixf(rust, t.mixf(tint, 0.38, 0.55), 0.92)
    height = t.math('ADD', t.math('MULTIPLY', rust, 0.6, False), t.math('MULTIPLY', pits, 0.4 * RUST + 0.05 * GRIME, False), False)
    return col, metal, rough, height, co


def finish(t, col, metal, rough, height, bump=0.35):
    bsdf = t.n.get('Principled BSDF') or t.node('ShaderNodeBsdfPrincipled')
    bsdf.location = (t.x + 200, 0)
    out = t.n.get('Material Output') or t.node('ShaderNodeOutputMaterial')
    out.location = (t.x + 500, 0)
    t.link(col, bsdf.inputs['Base Color'])
    t.link(metal if not isinstance(metal, float) else t.value(metal), bsdf.inputs['Metallic'])
    t.link(rough if not isinstance(rough, float) else t.value(rough), bsdf.inputs['Roughness'])
    b = t.node('ShaderNodeBump')
    b.inputs['Strength'].default_value = bump
    b.inputs['Distance'].default_value = 0.002
    t.link(height, b.inputs['Height'])
    t.link(b.outputs['Normal'], bsdf.inputs['Normal'])
    t.l.new(bsdf.outputs[0], out.inputs['Surface'])


def make_material(key):
    mat = bpy.data.materials.new(f"Proc_{key}")
    mat.use_nodes = True
    t = NT(mat)
    seed = {'rust': 1.3, 'rust_dark': 4.1, 'galvanized': 7.7, 'steel': 2.2, 'weld': 9.1,
            'paint': 5.5, 'hazard': 3.3, 'leather': 6.6}[key]
    if key in ('rust', 'rust_dark', 'galvanized', 'steel', 'weld'):
        steel, dark, amount = {
            'rust': ((0.48, 0.47, 0.45), (0.26, 0.25, 0.24), 0.62),
            'rust_dark': ((0.32, 0.31, 0.30), (0.16, 0.15, 0.15), 0.55),
            'galvanized': ((0.66, 0.67, 0.68), (0.45, 0.46, 0.47), 0.30),
            'steel': ((0.34, 0.33, 0.32), (0.18, 0.18, 0.18), 0.40),
            'weld': ((0.10, 0.09, 0.10), (0.04, 0.035, 0.04), 0.55),
        }[key]
        col, metal, rough, height, _ = rust_layers(t, steel, dark, amount, seed)
        finish(t, col, metal, rough, height, 0.6 if key == 'weld' else 0.35)
    elif key in ('paint', 'hazard'):
        col, metal, rough, height, co = rust_layers(t, (0.33, 0.32, 0.31), (0.12, 0.12, 0.12), 0.7, seed)
        chip = t.ramp(t.math('ADD', t.noise(co, 14, 6, 0.65, 0.2), t.math('MULTIPLY', t.noise(co, 70, 3), 0.3, False), False), 0.80, 0.84)
        chip = t.math('MULTIPLY', chip, GRIME)
        if key == 'hazard':
            w = t.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction='DIAGONAL')
            t.link(co, w.inputs['Vector'])
            w.inputs['Scale'].default_value = 9.0
            w.inputs['Distortion'].default_value = 0.6
            stripe = t.ramp(w.outputs['Fac'], 0.48, 0.52, 'LINEAR')
            paint = t.mixc(stripe, (0.62, 0.43, 0.05), (0.035, 0.03, 0.03))
        else:
            paint = t.mixc(t.mixf(GRIME, 0.5, t.noise(co, 6, 2)), (0.10, 0.24, 0.26), (0.14, 0.30, 0.30))   # teal
        fade = t.mixc(t.ramp(t.noise(co, 3, 2), 0.3, 0.7), paint, (0.45, 0.42, 0.36))
        paint = t.mixc(0.35 * GRIME, paint, fade)
        col = t.mixc(chip, paint, col)
        metal = t.mixf(chip, 0.0, metal)
        rough = t.mixf(chip, 0.68, rough)
        height = t.math('ADD', height, t.math('MULTIPLY', t.math('SUBTRACT', 1.0, chip), 0.4, False), False)
        finish(t, col, metal, rough, height, 0.35)
    elif key == 'leather':
        co = t.node('ShaderNodeTexCoord').outputs['Object']
        n1 = t.noise(co, 30, 4, 0.6)
        grain = t.noise(co, 400, 2, 0.5)
        col = t.mixc(n1, (0.07, 0.035, 0.018), (0.16, 0.08, 0.035))
        col = t.mixc(t.ramp(grain, 0.6, 0.8), col, (0.04, 0.02, 0.01))
        finish(t, col, 0.0, t.mixf(n1, 0.62, 0.82), grain, 0.15)
    return mat


# ---------------------------------------------------------------------------
# Skinning: copy weights from the nearest body vertices, limited per part
# ---------------------------------------------------------------------------
def skin_all():
    me = BODY.data
    groups = {g.index: g.name for g in BODY.vertex_groups}
    rest = [BODY.matrix_world @ v.co for v in me.vertices]
    kd = kdtree.KDTree(len(rest))
    for i, p in enumerate(rest):
        kd.insert(p, i)
    kd.balance()
    for obj, _, allowed in BUILT:
        for name in allowed:
            if name not in obj.vertex_groups:
                obj.vertex_groups.new(name=name)
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            acc = {}
            for co, idx, dist in kd.find_n(p, 6):
                w0 = 1.0 / max(dist, 1e-4) ** 2
                for g in me.vertices[idx].groups:
                    nm = groups[g.group]
                    if nm in allowed:
                        acc[nm] = acc.get(nm, 0.0) + g.weight * w0
            tot = sum(acc.values())
            if tot <= 1e-9:
                acc, tot = {allowed[0]: 1.0}, 1.0
            for nm, w in acc.items():
                obj.vertex_groups[nm].add([v.index], w / tot, 'REPLACE')
        obj.parent = ARM
        obj.matrix_parent_inverse = ARM.matrix_world.inverted()
        m = obj.modifiers.new("Armature", 'ARMATURE')
        m.object = ARM


# ---------------------------------------------------------------------------
# UVs + texture atlas bake
# ---------------------------------------------------------------------------
def select_only(objs, active):
    for o in bpy.context.view_layer.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = active


def unwrap_all(objs):
    for o in objs:
        if not o.data.uv_layers:
            o.data.uv_layers.new(name="UVMap")
    select_only(objs, objs[0])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')


def flat_materials():
    """One plain material per part type - no textures, so nothing for an importer to misread."""
    mats = {}
    for o, key, _ in BUILT:
        if key not in mats:
            name, colour, metal, rough = FLAT_MATERIALS[key]
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            b = m.node_tree.nodes['Principled BSDF']
            b.inputs['Base Color'].default_value = (*colour, 1.0)
            b.inputs['Metallic'].default_value = metal
            b.inputs['Roughness'].default_value = rough
            m.diffuse_color = (*colour, 1.0)        # solid-mode viewport colour
            mats[key] = m
        o.data.materials.clear()
        o.data.materials.append(mats[key])
    unwrap_all([o for o, _, _ in BUILT])           # UVs kept so the parts can be textured later


def bake_atlas(tex_dir):
    os.makedirs(tex_dir, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = BAKE_SAMPLES
    sc.render.bake.margin = 6
    objs = [o for o, _, _ in BUILT]
    mats = {}
    for o, key, _ in BUILT:
        mats.setdefault(key, make_material(key))
        o.data.materials.clear()
        o.data.materials.append(mats[key])
    unwrap_all(objs)
    images = {}
    for name, nonc in (("BaseColor", False), ("Roughness", True), ("Metallic", True), ("Normal", True)):
        img = bpy.data.images.new(f"ChestPlate_{name}", ATLAS_SIZE, ATLAS_SIZE, alpha=False)
        if nonc:
            img.colorspace_settings.name = 'Non-Color'
        images[name] = img
    select_only(objs, objs[0])

    def target(img):
        for m in mats.values():
            nd = m.node_tree.nodes.get("BakeTarget") or m.node_tree.nodes.new('ShaderNodeTexImage')
            nd.name = "BakeTarget"
            nd.image = img
            m.node_tree.nodes.active = nd

    target(images["BaseColor"])
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True, margin=6)
    target(images["Roughness"])
    bpy.ops.object.bake(type='ROUGHNESS', use_clear=True, margin=6)
    target(images["Normal"])
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_clear=True, margin=6)
    # metallic: route it through an emission shader for one bake
    saved = {}
    for k, m in mats.items():
        nt = m.node_tree
        bsdf = nt.nodes['Principled BSDF']
        out = nt.nodes['Material Output']
        src = bsdf.inputs['Metallic'].links[0].from_socket
        em = nt.nodes.new('ShaderNodeEmission')
        nt.links.new(src, em.inputs['Color'])
        nt.links.new(em.outputs[0], out.inputs['Surface'])
        saved[k] = (em, bsdf, out)
    target(images["Metallic"])
    bpy.ops.object.bake(type='EMIT', use_clear=True, margin=6)
    for em, bsdf, out in saved.values():
        bsdf.id_data.links.new(bsdf.outputs[0], out.inputs['Surface'])
        bsdf.id_data.nodes.remove(em)

    paths = {}
    for name, img in images.items():
        p = os.path.join(tex_dir, f"ChestPlate_{name}.png")
        img.filepath_raw = p
        img.file_format = 'PNG'
        img.save()
        img.filepath = p
        paths[name] = p
    # Unity standard shader map: R = metallic, A = smoothness (1 - roughness)
    import numpy as np
    n = ATLAS_SIZE * ATLAS_SIZE
    met = np.empty(n * 4, dtype=np.float32); images["Metallic"].pixels.foreach_get(met)
    rgh = np.empty(n * 4, dtype=np.float32); images["Roughness"].pixels.foreach_get(rgh)
    ms = np.zeros((n, 4), dtype=np.float32)
    ms[:, 0] = ms[:, 1] = ms[:, 2] = met.reshape(n, 4)[:, 0]
    ms[:, 3] = 1.0 - rgh.reshape(n, 4)[:, 0]
    img = bpy.data.images.new("ChestPlate_MetallicSmoothness", ATLAS_SIZE, ATLAS_SIZE, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(ms.ravel())
    img.filepath_raw = os.path.join(tex_dir, "ChestPlate_MetallicSmoothness.png")
    img.file_format = 'PNG'
    img.save()

    # final, export-friendly material that uses the atlas
    fm = bpy.data.materials.new("ChestPlate_Atlas")
    fm.use_nodes = True
    nt = fm.node_tree
    bsdf = nt.nodes['Principled BSDF']

    def tex(name, y):
        nd = nt.nodes.new('ShaderNodeTexImage')
        nd.image = images[name]
        nd.location = (-500, y)
        return nd

    # Only the colour map is wired in. FBX has no real slots for roughness / metallic / normal maps,
    # and importers (including different Blender versions) hook them up differently, which can turn
    # the steel into black chrome or blotchy shading. Fixed values import the same everywhere; the
    # full map set is still in textures/ for setting up the material by hand in a game engine.
    nt.links.new(tex("BaseColor", 300).outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Metallic'].default_value = FBX_METALLIC
    bsdf.inputs['Roughness'].default_value = FBX_ROUGHNESS
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(fm)
    for m in mats.values():
        m.use_fake_user = True          # keep the procedural sources in the .blend


# ---------------------------------------------------------------------------
# Export + previews
# ---------------------------------------------------------------------------
def export(col):
    parts = [o for o, _, _ in BUILT]
    ARM.data.pose_position = 'POSE'
    select_only(parts + [ARM], ARM)
    common = dict(use_selection=True, object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=True,
                  add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True,
                  mesh_smooth_type='FACE', use_tspace=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "ChestPlate.fbx"), **common)
    # armor together with the player model, sharing one armature
    player = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == ARM and o not in parts]
    select_only(parts + player + [ARM], ARM)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "ChestPlate_WithPlayer.fbx"), **common)
    # single merged skinned mesh
    copies = []
    for o in parts:
        c = o.copy()
        c.data = o.data.copy()
        col.objects.link(c)
        copies.append(c)
    select_only(copies, copies[0])
    bpy.ops.object.join()
    merged = copies[0]
    merged.name = merged.data.name = "ChestPlate_Merged"
    select_only([merged, ARM], ARM)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "ChestPlate_Merged.fbx"), **common)
    bpy.data.objects.remove(merged, do_unlink=True)


def render_previews():
    from math import radians, sin, cos
    d = os.path.join(OUT_DIR, "previews")
    os.makedirs(d, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = 900
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("Preview")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.42, 0.45, 0.5, 1)
    w.node_tree.nodes['Background'].inputs[1].default_value = 0.7
    for name, energy, rot in (("Key", 4.0, (50, -15, -35)), ("Rim", 2.5, (60, 0, 160))):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.rotation_euler = [radians(a) for a in rot]
    cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 70
    sc.camera = cam
    for pose in ('POSE', 'REST'):
        ARM.data.pose_position = pose
        views = {"front": (0, 8), "threequarter": (38, 14), "side": (90, 6), "back": (180, 10)}
        if pose == 'REST':
            views = {"front": (0, 8), "threequarter": (38, 14)}
        for name, (az, el) in views.items():
            tgt = Vector((0, 0, -0.02))
            dist = 2.4 if pose == 'REST' else 1.7
            dv = Vector((sin(radians(az)) * cos(radians(el)), -cos(radians(az)) * cos(radians(el)), sin(radians(el))))
            cam.location = tgt + dv * dist
            cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(d, f"{pose.lower()}_{name}.png")
            bpy.ops.render.render(write_still=True)
    ARM.data.pose_position = 'POSE'


def main():
    col = load_player()
    build_breastplate(col)
    build_backplate(col)
    build_belly_lames(col)
    rebuild_layer_bvh()
    if SCRAP_PATCH['enabled']:
        build_scrap_patch(col)
    if CRACK_WELD['enabled']:
        build_crack_weld(col)
    build_straps(col)
    rebuild_layer_bvh()
    build_gorget(col)
    rebuild_layer_bvh()
    build_pauldron(col, 1)
    build_pauldron(col, -1)
    for o, _, _ in BUILT:
        o.data.name = o.name
        bm = bmesh.new()                 # quads/tris only, so tangents export cleanly
        bm.from_mesh(o.data)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
        bm.to_mesh(o.data)
        bm.free()
    if USE_TEXTURES:
        bake_atlas(os.path.join(OUT_DIR, "textures"))
    else:
        flat_materials()
    skin_all()
    export(col)
    ARM.data.pose_position = 'POSE'
    if RENDER_PREVIEWS:
        render_previews()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "ChestPlate.blend"), compress=True)
    print("Built", len(BUILT), "parts:", ", ".join(o.name for o, _, _ in BUILT))


if __name__ == "__main__":
    main()
