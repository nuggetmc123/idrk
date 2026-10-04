"""Builds a low-poly, flat-shaded hen (Rust-style survival-game wildlife) and exports it as FBX.

Run with Blender's Python module:  pip install bpy && python3 build_chicken.py
Outputs chicken.fbx and chicken.blend next to this script: separate, editable part meshes
skinned to one armature, plus Idle/Walk/Run/Peck/Flap/Death animations.
"""
import math
import os

import bpy  # must be imported before bmesh
import bmesh
from mathutils import Matrix, Quaternion, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# Overall size multiplier, baked into the meshes, the bones and the animation offsets.
# The hen is modelled ~0.55 m tall, so 5.0 gives a ~2.75 m chicken.
SCALE = 5.0

# ---------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = 30

# ---------------------------------------------------------------- materials
PALETTE = {
    "Feather":      (0.36, 0.15, 0.06),  # russet body
    "FeatherWing":  (0.27, 0.11, 0.04),  # slightly darker wing coverts
    "FeatherDark":  (0.06, 0.06, 0.05),  # tail and flight feathers
    "Hackle":       (0.62, 0.34, 0.11),  # golden neck feathers
    "Comb":         (0.66, 0.05, 0.04),
    "Beak":         (0.82, 0.64, 0.30),
    "Leg":          (0.86, 0.62, 0.20),
    "Claw":         (0.30, 0.26, 0.20),
    "Iris":         (0.80, 0.45, 0.05),
    "Pupil":        (0.01, 0.01, 0.01),
}
MATS = {}
for name, rgb in PALETTE.items():
    m = bpy.data.materials.new("Chicken_" + name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.85
    m.diffuse_color = (*rgb, 1.0)
    MATS[name] = m

# ---------------------------------------------------------------- geometry helpers
# The hen faces -Y (Blender front), Z is up, coordinates are metres before SCALE.
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def loft(points, radii, segs=8, jag=0.0, side_hint=X, spin=0.0):
    """Generalised cylinder through `points` with elliptical (rx, rz) rings.

    A station with radius (0, 0) becomes a single tip vertex. `jag` pushes every other
    ring vertex out for a ragged, feathery silhouette.
    """
    pts = [Vector(p) for p in points]

    def make(b):
        rings = []
        for i, (c, (rx, rz)) in enumerate(zip(pts, radii)):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            side = (side_hint - t * side_hint.dot(t)).normalized()
            up = side.cross(t)
            if rx == 0 and rz == 0:
                rings.append([b.verts.new(c)])
                continue
            ring = []
            for k in range(segs):
                a = spin + 2 * math.pi * k / segs
                j = 1.0 + (jag if k % 2 else 0.0)
                ring.append(b.verts.new(c + side * (rx * j * math.cos(a)) + up * (rz * j * math.sin(a))))
            rings.append(ring)
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(segs):
                k1 = (k + 1) % segs
                if len(r0) == 1:
                    b.faces.new((r0[0], r1[k1], r1[k]))
                elif len(r1) == 1:
                    b.faces.new((r0[k], r0[k1], r1[0]))
                else:
                    b.faces.new((r0[k], r0[k1], r1[k1], r1[k]))
        for ring in (rings[0], rings[-1]):
            if len(ring) > 2:
                b.faces.new(ring)
    return make


def sphere(segs, rings, r=1.0):
    return lambda b: bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=rings, radius=r)


def blade(outline, thickness):
    """Flat plate from a 2-D (y, z) outline, extruded along X (comb, feathers)."""
    def make(b):
        front = [b.verts.new((-thickness / 2, y, z)) for y, z in outline]
        back = [b.verts.new((thickness / 2, y, z)) for y, z in outline]
        b.faces.new(front)
        b.faces.new(list(reversed(back)))
        n = len(outline)
        for k in range(n):
            k1 = (k + 1) % n
            b.faces.new((front[k], back[k], back[k1], front[k1]))
        bmesh.ops.triangulate(b, faces=[f for f in b.faces if len(f.verts) > 4])
    return make


def T(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    S = Matrix.Diagonal((*scale, 1.0))
    R = (Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @
         Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @
         Matrix.Rotation(math.radians(rot[0]), 4, 'X'))
    return Matrix.Translation(loc) @ R @ S


def mirror(side, loc):
    return (loc[0] * side, loc[1], loc[2])


def blend(bone_a, bone_b, z0, z1):
    """Weights that fade from bone_a to bone_b between heights z0 and z1."""
    def w(co):
        f = min(1.0, max(0.0, (co.z - z0) / (z1 - z0)))
        return {bone_a: 1.0 - f, bone_b: f}
    return w


# ---------------------------------------------------------------- part registry
# Each named part becomes its own mesh object. Pieces added under the same part name are
# merged into that object. `bone` is a bone name (rigid) or a function co -> {bone: weight}.
PARTS = {}


def add_part(part, make, mat, bone, xform=Matrix.Identity(4)):
    entry = PARTS.setdefault(part, {"bm": bmesh.new(), "mats": [], "weights": []})
    if mat not in entry["mats"]:
        entry["mats"].append(mat)
    tmp = bmesh.new()
    make(tmp)
    bmesh.ops.transform(tmp, matrix=xform, verts=tmp.verts)
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
    bm = entry["bm"]
    mapping = {}
    for v in tmp.verts:
        mapping[v] = bm.verts.new(v.co * SCALE)
        entry["weights"].append(bone(v.co) if callable(bone) else {bone: 1.0})
    for f in tmp.faces:
        nf = bm.faces.new([mapping[v] for v in f.verts])
        nf.material_index = entry["mats"].index(mat)
    tmp.free()


# ---------------------------------------------------------------- body
# Plump, horizontal egg: full low breast at the front, rump sweeping up into the tail.
add_part("Body", loft(
    [(0, -0.205, 0.25), (0, -0.18, 0.245), (0, -0.13, 0.235), (0, -0.06, 0.235), (0, 0.02, 0.24),
     (0, 0.09, 0.255), (0, 0.15, 0.28), (0, 0.195, 0.31), (0, 0.22, 0.335)],
    [(0, 0), (0.07, 0.075), (0.12, 0.12), (0.145, 0.135), (0.145, 0.13),
     (0.13, 0.115), (0.1, 0.09), (0.065, 0.06), (0, 0)],
    segs=10), "Feather", "Body")

# Fluffy vent feathers under the tail.
add_part("Body", loft([(0, 0.1, 0.19), (0, 0.17, 0.23), (0, 0.21, 0.27)],
                      [(0.07, 0.05), (0.055, 0.045), (0, 0)], segs=8, jag=0.25),
         "Feather", "Body")

# ---------------------------------------------------------------- neck and head
# S-curved neck covered in golden hackles, flaring over the shoulders.
add_part("Neck", loft(
    [(0, -0.09, 0.27), (0, -0.13, 0.31), (0, -0.155, 0.36), (0, -0.165, 0.41), (0, -0.165, 0.45)],
    [(0.1, 0.095), (0.08, 0.075), (0.058, 0.055), (0.047, 0.045), (0.04, 0.04)],
    segs=10, jag=0.18), "Hackle", blend("Neck1", "Neck2", 0.31, 0.42))

HEAD_Z = 0.465
add_part("Head", loft(
    [(0, -0.12, HEAD_Z + 0.005), (0, -0.14, HEAD_Z + 0.005), (0, -0.17, HEAD_Z), (0, -0.2, HEAD_Z - 0.003),
     (0, -0.225, HEAD_Z - 0.008), (0, -0.24, HEAD_Z - 0.012)],
    [(0, 0), (0.035, 0.035), (0.046, 0.046), (0.042, 0.04), (0.03, 0.028), (0, 0)],
    segs=8), "Hackle", "Head")

# Upper and lower beak: short, slightly hooked.
add_part("Beak", loft([(0, -0.222, HEAD_Z - 0.004), (0, -0.25, HEAD_Z - 0.01), (0, -0.27, HEAD_Z - 0.022)],
                      [(0.016, 0.012), (0.009, 0.007), (0, 0)], segs=6), "Beak", "Head")
add_part("Beak", loft([(0, -0.222, HEAD_Z - 0.018), (0, -0.248, HEAD_Z - 0.022), (0, -0.258, HEAD_Z - 0.025)],
                      [(0.012, 0.006), (0.006, 0.004), (0, 0)], segs=6), "Beak", "Head")

# Serrated single comb running from the beak back over the crown.
add_part("Comb", blade([
    (-0.225, HEAD_Z + 0.02), (-0.228, HEAD_Z + 0.042), (-0.212, HEAD_Z + 0.034), (-0.205, HEAD_Z + 0.064),
    (-0.19, HEAD_Z + 0.045), (-0.18, HEAD_Z + 0.075), (-0.166, HEAD_Z + 0.05), (-0.153, HEAD_Z + 0.07),
    (-0.142, HEAD_Z + 0.044), (-0.125, HEAD_Z + 0.052), (-0.13, HEAD_Z + 0.03),
    (-0.16, HEAD_Z + 0.03), (-0.2, HEAD_Z + 0.025),
], 0.012), "Comb", "Head")

# Wattles and ear lobes.
for side in (-1, 1):
    add_part("Wattle", sphere(6, 5, 1.0), "Comb", "Head",
             T(mirror(side, (0.011, -0.226, HEAD_Z - 0.045)), scale=(0.011, 0.014, 0.024)))
    add_part("Wattle", sphere(5, 4, 1.0), "Comb", "Head",
             T(mirror(side, (0.04, -0.16, HEAD_Z - 0.018)), scale=(0.006, 0.012, 0.014)))

# Eyes: amber iris with a black pupil.
for side, part in ((-1, "Eye.R"), (1, "Eye.L")):
    add_part(part, sphere(6, 5, 0.011), "Iris", "Head", T(mirror(side, (0.036, -0.198, HEAD_Z + 0.006))))
    add_part(part, sphere(5, 4, 0.006), "Pupil", "Head", T(mirror(side, (0.045, -0.2, HEAD_Z + 0.007))))

# ---------------------------------------------------------------- tail
# Upright fan of dark feathers, the centre ones tallest.
TAIL_BASE = Vector((0, 0.19, 0.31))
for i, fan in enumerate((-40, -25, -10, 10, 25, 40)):
    h = 1.0 - abs(fan) / 110.0
    feather = loft([(0, 0, 0), (0, 0.03, 0.06 * h), (0, 0.06, 0.12 * h), (0, 0.1, 0.16 * h), (0, 0.13, 0.17 * h)],
                   [(0.006, 0.02), (0.006, 0.032), (0.005, 0.034), (0.004, 0.026), (0, 0)], segs=4)
    add_part("Tail", feather, "FeatherDark", "Tail",
             T(TAIL_BASE + Vector((math.sin(math.radians(fan)) * 0.04, 0, 0)), rot=(0, fan, fan * 0.15)))

# ---------------------------------------------------------------- wings
for side, bone in ((-1, "Wing.R"), (1, "Wing.L")):
    # Folded wing: flattened teardrop hugging the flank.
    add_part(bone, loft(
        [(0, -0.09, 0.285), (0, -0.05, 0.28), (0, 0.03, 0.27), (0, 0.11, 0.265), (0, 0.17, 0.27), (0, 0.2, 0.275)],
        [(0, 0), (0.02, 0.05), (0.028, 0.07), (0.025, 0.06), (0.016, 0.035), (0, 0)], segs=8),
        "FeatherWing", bone, T((side * 0.128, 0, 0), rot=(0, 0, side * 6)))
    # Primary flight feathers poking out along the lower back edge.
    for k in range(4):
        add_part(bone, loft([(0, 0, 0), (0, 0.06, -0.005), (0, 0.12, 0.0), (0, 0.15, 0.01)],
                            [(0.004, 0.014), (0.004, 0.017), (0.003, 0.012), (0, 0)], segs=4),
                 "FeatherDark", bone,
                 T((side * (0.135 - k * 0.006), 0.04 + k * 0.012, 0.235 + k * 0.01), rot=(-6, 0, side * (4 + k * 3))))

# ---------------------------------------------------------------- legs
HIP, HOCK, ANKLE = Vector((0, 0.025, 0.17)), Vector((0, 0.04, 0.09)), Vector((0, 0.03, 0.016))
for side, sfx in ((-1, ".R"), (1, ".L")):
    off = Vector((side * 0.055, 0, 0))
    # Feathered drumstick.
    add_part("Thigh" + sfx, loft([HIP + off + Vector((0, 0, 0.02)), HIP + off, (HIP + HOCK) / 2 + off,
                                  HOCK + off + Vector((0, 0, 0.005))],
                                 [(0.04, 0.04), (0.045, 0.048), (0.035, 0.035), (0.016, 0.016)], segs=8, jag=0.15),
             "Feather", "Thigh" + sfx)
    # Scaly yellow shank with a small spur.
    add_part("Shank" + sfx, loft([HOCK + off, (HOCK + ANKLE) / 2 + off, ANKLE + off],
                                 [(0.012, 0.013), (0.01, 0.011), (0.011, 0.012)], segs=6), "Leg", "Shank" + sfx)
    add_part("Shank" + sfx, loft([ANKLE + off + Vector((0, 0.008, 0.03)), ANKLE + off + Vector((0, 0.022, 0.028))],
                                 [(0.004, 0.004), (0, 0)], segs=4), "Claw", "Shank" + sfx)
    # Three forward toes and one back toe, each ending in a claw.
    for ang, length in ((-32, 0.06), (0, 0.072), (32, 0.06), (180, 0.032)):
        d = Vector((math.sin(math.radians(ang)) * side, -math.cos(math.radians(ang)), 0))
        base = ANKLE + off + Vector((0, 0, -0.006))
        tip = base + d * length + Vector((0, 0, -0.004))
        add_part("Foot" + sfx, loft([base, base + d * length * 0.5, tip],
                                    [(0.008, 0.007), (0.006, 0.005), (0.004, 0.004)], segs=5),
                 "Leg", "Foot" + sfx)
        add_part("Foot" + sfx, loft([tip, tip + d * 0.014 + Vector((0, 0, -0.004))],
                                    [(0.004, 0.004), (0, 0)], segs=4), "Claw", "Foot" + sfx)

# ---------------------------------------------------------------- build objects
part_objs = []
for part, entry in PARTS.items():
    bm = entry["bm"]
    # Put each part's origin at its own centre so it is easy to move/scale in Blender.
    center = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    bmesh.ops.translate(bm, vec=-center, verts=bm.verts)
    bm.normal_update()
    mesh = bpy.data.meshes.new(part)
    bm.to_mesh(mesh)
    bm.free()
    for mat in entry["mats"]:
        mesh.materials.append(MATS[mat])
    for poly in mesh.polygons:
        poly.use_smooth = False  # flat shaded, faceted look
    obj = bpy.data.objects.new(part, mesh)
    obj.location = center
    scene.collection.objects.link(obj)
    part_objs.append((obj, entry["weights"]))

# Simple projected UVs so engines that require UVs are happy.
for obj, _ in part_objs:
    obj.select_set(True)
bpy.context.view_layer.objects.active = part_objs[0][0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- armature
arm_data = bpy.data.armatures.new("ChickenRig")
rig = bpy.data.objects.new("ChickenRig", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')

BONES = {  # name: (head, tail, parent)
    "Root":  ((0, 0, 0), (0, 0, 0.08), None),
    "Body":  ((0, 0.06, 0.22), (0, -0.1, 0.25), "Root"),
    "Neck1": ((0, -0.11, 0.29), (0, -0.155, 0.36), "Body"),
    "Neck2": ((0, -0.155, 0.36), (0, -0.165, 0.43), "Neck1"),
    "Head":  ((0, -0.165, 0.43), (0, -0.23, HEAD_Z), "Neck2"),
    "Tail":  ((0, 0.18, 0.3), (0, 0.27, 0.44), "Body"),
}
for side, sfx in ((1, ".L"), (-1, ".R")):
    x = side * 0.055
    BONES["Wing" + sfx] = ((side * 0.13, -0.07, 0.29), (side * 0.13, 0.18, 0.28), "Body")
    BONES["Thigh" + sfx] = ((x, *HIP.yz), (x, *HOCK.yz), "Body")
    BONES["Shank" + sfx] = ((x, *HOCK.yz), (x, *ANKLE.yz), "Thigh" + sfx)
    BONES["Foot" + sfx] = ((x, *ANKLE.yz), (x, -0.04, 0.008), "Shank" + sfx)

for name, (h, t, parent) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail, b.roll = Vector(h) * SCALE, Vector(t) * SCALE, 0.0
    if parent:
        b.parent = arm_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')

# Skin every part. New geometry extruded/duplicated in Edit Mode inherits these groups,
# so edits keep following the rig.
for obj, weights in part_objs:
    for i, wmap in enumerate(weights):
        for bone, w in wmap.items():
            if w <= 0:
                continue
            vg = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            vg.add([i], w, 'REPLACE')
    obj.parent = rig
    obj.modifiers.new("Armature", 'ARMATURE').object = rig

# ---------------------------------------------------------------- animations
# Keys are written in model space so they read naturally:
#   pitch: + tips the bone forward/down,  yaw: + turns toward the hen's left,
#   roll: + tips the top over toward the hen's left (+X),  loc: model-space offset in metres (before SCALE).
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'
rig.animation_data_create()


def pose_quat(bone, pitch=0.0, yaw=0.0, roll=0.0):
    to_local = arm_data.bones[bone].matrix_local.to_3x3().inverted()
    q = Quaternion()
    for axis, deg in ((Z, -yaw), (Y, roll), (X, pitch)):
        if deg:
            q = Quaternion(to_local @ axis, math.radians(deg)) @ q
    return q


def make_action(name, length, keys):
    """keys: {bone: [(frame, {"pitch"/"yaw"/"roll": deg, "loc": (x, y, z)}), ...]}"""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data.action = act
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
    for bone, frames in keys.items():
        pb = rig.pose.bones[bone]
        to_local = arm_data.bones[bone].matrix_local.to_3x3().inverted()
        for f, k in frames:
            pb.rotation_quaternion = pose_quat(bone, k.get("pitch", 0), k.get("yaw", 0), k.get("roll", 0))
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.location = to_local @ (Vector(k.get("loc", (0, 0, 0))) * SCALE)
            pb.keyframe_insert("location", frame=f)
    act.frame_range = (1, length)
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 1, act)
    track.mute = True
    rig.animation_data.action = None


def cycle(length, values):
    """Evenly spaced looping keys over frames 1..length; the last key repeats the first."""
    n = len(values)
    return [(1 + round(i * (length - 1) / n), v) for i, v in enumerate(values)] + [(length, values[0])]


def rotate(values, steps):
    return values[steps:] + values[:steps]


make_action("Idle", 73, {
    "Body":  [(1, {}), (37, {"loc": (0, 0, 0.003)}), (73, {})],
    "Neck1": [(1, {}), (30, {"pitch": -6}), (50, {"pitch": 4}), (73, {})],
    "Head":  [(1, {}), (10, {}), (13, {"yaw": 35, "roll": -10}), (30, {"yaw": 35, "roll": -10}),
              (33, {"yaw": -10}), (40, {"yaw": -10}), (43, {"yaw": -35, "roll": 12}), (60, {"yaw": -35, "roll": 12}),
              (63, {}), (73, {})],
    "Tail":  [(1, {}), (45, {}), (48, {"yaw": 8}), (51, {"yaw": -6}), (54, {}), (73, {})],
    "Wing.L": [(1, {}), (37, {"roll": -3}), (73, {})],
    "Wing.R": [(1, {}), (37, {"roll": 3}), (73, {})],
})


def gait(length, swing, lift, bob, lean, neck, wings):
    """Walk/run cycle in 4 beats. The left leg leads; the right is half a cycle behind."""
    # Per beat: thigh pitch (-forward/+back), shank pitch (- lifts the foot), foot pitch (+ curls toes).
    thigh = [-swing, 0, swing, 0]
    shank = [0, 0, 0, lift]
    foot = [0, 0, -15, 25]
    beat = lambda vals, key: [{key: v} for v in vals]
    return {
        "Body":  cycle(length, [{"pitch": lean}, {"pitch": lean, "loc": (0, 0, bob)}] * 2),
        # Head bob: the neck pumps forward then pulls back once per step, head stays level.
        "Neck1": cycle(length, [{"pitch": neck}, {"pitch": -neck * 0.5}] * 2),
        "Head":  cycle(length, [{"pitch": -neck - lean}, {"pitch": neck * 0.5 - lean}] * 2),
        "Tail":  cycle(length, [{"yaw": 5}, {}, {"yaw": -5}, {}]),
        "Wing.L": cycle(length, [{"roll": -wings}]),
        "Wing.R": cycle(length, [{"roll": wings}]),
        "Thigh.L": cycle(length, beat(thigh, "pitch")),
        "Shank.L": cycle(length, beat(shank, "pitch")),
        "Foot.L":  cycle(length, beat(foot, "pitch")),
        "Thigh.R": cycle(length, beat(rotate(thigh, 2), "pitch")),
        "Shank.R": cycle(length, beat(rotate(shank, 2), "pitch")),
        "Foot.R":  cycle(length, beat(rotate(foot, 2), "pitch")),
    }


make_action("Walk", 25, gait(25, swing=28, lift=-45, bob=0.008, lean=4, neck=12, wings=0))
make_action("Run", 17, gait(17, swing=42, lift=-70, bob=0.015, lean=14, neck=8, wings=25))

make_action("Peck", 31, {
    "Body":  [(1, {}), (8, {"pitch": 18}), (24, {"pitch": 18}), (31, {})],
    "Neck1": [(1, {}), (8, {"pitch": 35}), (11, {"pitch": 50}), (14, {"pitch": 35}), (17, {"pitch": 50}),
              (20, {"pitch": 35}), (24, {"pitch": 35}), (31, {})],
    "Neck2": [(1, {}), (8, {"pitch": 25}), (11, {"pitch": 35}), (14, {"pitch": 25}), (17, {"pitch": 35}),
              (20, {"pitch": 25}), (24, {"pitch": 25}), (31, {})],
    "Head":  [(1, {}), (8, {"pitch": 10}), (24, {"pitch": 10}), (31, {})],
    "Tail":  [(1, {}), (8, {"pitch": -12}), (24, {"pitch": -12}), (31, {})],
})

flap = [(f, {"roll": -80 if i % 2 else -10}) for i, f in enumerate((1, 4, 7, 10, 13, 16, 19))] + [(25, {})]
make_action("Flap", 25, {
    "Body":   [(1, {}), (10, {"loc": (0, 0, 0.06), "pitch": -10}), (19, {"loc": (0, 0, 0.02)}), (25, {})],
    "Wing.L": flap,
    "Wing.R": [(f, {"roll": -k.get("roll", 0)}) for f, k in flap],
    "Tail":   [(1, {}), (10, {"pitch": -20}), (25, {})],
    "Thigh.L": [(1, {}), (10, {"pitch": 25}), (25, {})],
    "Thigh.R": [(1, {}), (10, {"pitch": 25}), (25, {})],
    "Shank.L": [(1, {}), (10, {"pitch": -50}), (25, {})],
    "Shank.R": [(1, {}), (10, {"pitch": -50}), (25, {})],
    "Neck1":  [(1, {}), (10, {"pitch": -15}), (25, {})],
})

def topple(roll, body_h=0.22, rest_h=0.13):
    """Root key that rolls the hen over around its body centre, sinking to lying height."""
    r = math.radians(roll)
    h = body_h + (rest_h - body_h) * (roll / 90.0)  # body centre height while falling
    return {"roll": roll, "loc": (-body_h * math.sin(r), 0, h - body_h * math.cos(r))}


make_action("Death", 41, {
    "Root":   [(1, {})] + [(f, topple(roll)) for f, roll in ((6, 8), (10, 20), (13, 40), (15, 62), (17, 82),
                                                        (18, 90), (22, 84), (41, 88))],
    "Neck1":  [(1, {}), (12, {"pitch": -20}), (20, {"pitch": 40, "roll": 30}), (41, {"pitch": 50, "roll": 35})],
    "Neck2":  [(1, {}), (20, {"pitch": 30}), (41, {"pitch": 35})],
    "Head":   [(1, {}), (20, {"pitch": 20}), (41, {"pitch": 25})],
    "Wing.L": [(1, {}), (10, {"roll": -60}), (18, {"roll": -20}), (41, {"roll": -15})],
    "Wing.R": [(1, {}), (10, {"roll": 60}), (18, {"roll": 20}), (41, {"roll": 15})],
    "Thigh.L": [(1, {}), (18, {"pitch": -40}), (26, {"pitch": -25}), (30, {"pitch": -45}), (41, {"pitch": -40})],
    "Thigh.R": [(1, {}), (18, {"pitch": -30}), (28, {"pitch": -50}), (32, {"pitch": -30}), (41, {"pitch": -35})],
    "Foot.L": [(1, {}), (18, {"pitch": 40}), (41, {"pitch": 50})],
    "Foot.R": [(1, {}), (18, {"pitch": 40}), (41, {"pitch": 50})],
    "Tail":   [(1, {}), (18, {"pitch": 10}), (41, {"pitch": 10})],
})

# Leave the rig in its rest pose (make_action leaves the last keyed pose behind).
for pb in rig.pose.bones:
    pb.location = (0, 0, 0)
    pb.rotation_quaternion = (1, 0, 0, 0)

# ---------------------------------------------------------------- export
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for obj, _ in part_objs:
    obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT_DIR, "chicken.fbx"),
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "chicken.blend"))

tris = 0
for obj, _ in part_objs:
    t = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    tris += t
    print(f"{obj.name:10s} tris {t:4d}  bones {sorted(obj.vertex_groups.keys())}")
print("total tris:", tris)
