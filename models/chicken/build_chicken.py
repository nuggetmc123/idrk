"""Builds a low-poly, flat-shaded chicken (survival-game style) and exports it as FBX.

Run with Blender's Python module:  python3 build_chicken.py
Outputs chicken.fbx (mesh + armature + Idle/Walk/Peck/Flap animations) next to this script.
"""
import math
import os

import bpy  # must be imported before bmesh
import bmesh
from mathutils import Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30

# ---------------------------------------------------------------- materials
PALETTE = {
    "Feather":     (0.42, 0.20, 0.08),   # russet hen body
    "FeatherDark": (0.20, 0.09, 0.04),   # tail / wing tips
    "FeatherLight": (0.62, 0.36, 0.15),  # neck hackles
    "Comb":        (0.70, 0.05, 0.04),
    "Beak":        (0.85, 0.62, 0.18),
    "Leg":         (0.88, 0.66, 0.22),
    "Eye":         (0.02, 0.02, 0.02),
}
MATS = {}
for name, rgb in PALETTE.items():
    m = bpy.data.materials.new("Chicken_" + name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    m.diffuse_color = (*rgb, 1.0)
    MATS[name] = m
MAT_INDEX = {n: i for i, n in enumerate(MATS)}

# ---------------------------------------------------------------- mesh helpers
# Every part is added to one bmesh; each face records its material and each
# vertex the bone that rigidly owns it.
bm = bmesh.new()
vert_bone = {}


def add_part(make, mat, bone, xform=Matrix.Identity(4), deform=None):
    tmp = bmesh.new()
    make(tmp)
    if deform:
        for v in tmp.verts:
            v.co = deform(v.co.copy())
    bmesh.ops.transform(tmp, matrix=xform, verts=tmp.verts)
    mapping = {}
    for v in tmp.verts:
        nv = bm.verts.new(v.co)
        mapping[v] = nv
        vert_bone[nv] = bone
    for f in tmp.faces:
        nf = bm.faces.new([mapping[v] for v in f.verts])
        nf.material_index = MAT_INDEX[mat]
    tmp.free()


def sphere(segs, rings, r=1.0):
    return lambda b: bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=rings, radius=r)


def cone(segs, r1, r2, depth):
    return lambda b: bmesh.ops.create_cone(b, cap_ends=True, segments=segs,
                                           radius1=r1, radius2=r2, depth=depth)


def cube(size=1.0):
    return lambda b: bmesh.ops.create_cube(b, size=size)


def T(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    S = Matrix.Diagonal((*scale, 1.0))
    R = (Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @
         Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @
         Matrix.Rotation(math.radians(rot[0]), 4, 'X'))
    return Matrix.Translation(loc) @ R @ S


# Chicken faces -Y (Blender front); Z up; units are metres (~0.45 m tall).

# Body: plump egg shape, chest forward, rump lifted toward the tail.
def body_deform(co):
    x, y, z = co
    if y < 0:            # chest: fuller and slightly lower
        x *= 1.05
        z -= 0.10 * -y
    else:                # rump: narrower, swept up
        x *= 1.0 - 0.25 * y
        z += 0.35 * y * y
    return Vector((x, y, z))


add_part(sphere(8, 6), "Feather", "Body",
         T((0, 0.02, 0.22), scale=(0.15, 0.21, 0.14)), body_deform)

# Neck (hackle feathers) and head.
add_part(sphere(7, 5), "FeatherLight", "Neck",
         T((0, -0.15, 0.33), rot=(-30, 0, 0), scale=(0.075, 0.075, 0.1)))
add_part(sphere(6, 5, 0.07), "FeatherLight", "Head",
         T((0, -0.19, 0.42), scale=(0.85, 1.0, 1.0)))

# Beak: 4-sided cone pointing forward and a touch down.
add_part(cone(4, 0.024, 0.0, 0.07), "Beak", "Head",
         T((0, -0.275, 0.415), rot=(100, 0, 45), scale=(1.0, 0.75, 1.0)))

# Comb: three jagged spikes along the top of the head.
for i, (dy, h) in enumerate(((-0.03, 0.04), (0.0, 0.05), (0.03, 0.04))):
    add_part(cone(4, 0.028, 0.004, h), "Comb", "Head",
             T((0, -0.19 + dy, 0.465 + h * 0.4), rot=(-15 + i * 15, 0, 45), scale=(0.4, 1.0, 1.0)))

# Wattle under the beak.
add_part(sphere(5, 4, 0.022), "Comb", "Head",
         T((0, -0.245, 0.37), scale=(0.6, 0.8, 1.4)))

# Eyes.
for side in (-1, 1):
    add_part(sphere(5, 4, 0.013), "Eye", "Head",
             T((side * 0.056, -0.215, 0.43)))

# Tail: fan of dark feathers angled up and back.
for ang in (-36, -18, 0, 18, 36):
    add_part(sphere(6, 4), "FeatherDark", "Tail",
             T((math.sin(math.radians(ang)) * 0.03, 0.2, 0.33),
               rot=(-35, ang, 0), scale=(0.018, 0.045, 0.11)),
             lambda co: co + Vector((0, 0, 0.09)))

# Wings: flattened, tapered teardrops tucked along the sides.
def wing_deform(co):
    x, y, z = co
    taper = 1.0 - 0.45 * max(0.0, y)  # narrower toward the tip (rear)
    return Vector((x, y, z * taper + 0.25 * max(0.0, y)))


for side, bone in ((-1, "Wing.R"), (1, "Wing.L")):
    add_part(sphere(6, 4), "Feather", bone,
             T((side * 0.13, 0.04, 0.25), rot=(-12, side * -8, side * 6), scale=(0.035, 0.15, 0.08)),
             wing_deform)

# Legs and feet.
for side, bone in ((-1, "Leg.R"), (1, "Leg.L")):
    x = side * 0.06
    add_part(cone(6, 0.035, 0.03, 0.06), "Feather", bone,      # thigh feathers
             T((x, 0.02, 0.12)))
    add_part(cone(5, 0.012, 0.011, 0.11), "Leg", bone,         # shank
             T((x, 0.02, 0.055)))
    for ang in (-30, 0, 30):                                    # three front toes
        a = math.radians(ang)
        add_part(cube(1.0), "Leg", bone,
                 T((x + math.sin(a) * 0.035, 0.02 - math.cos(a) * 0.035, 0.006),
                   rot=(0, 0, -ang), scale=(0.012, 0.07, 0.01)))
    add_part(cube(1.0), "Leg", bone,                            # back toe
             T((x, 0.045, 0.006), scale=(0.012, 0.04, 0.01)))

bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.normal_update()

mesh = bpy.data.meshes.new("ChickenMesh")
bones_of_verts = [vert_bone.get(v) for v in bm.verts]
bm.to_mesh(mesh)
bm.free()
for m in MATS.values():
    mesh.materials.append(m)
for p in mesh.polygons:
    p.use_smooth = False  # flat shaded, faceted look

chicken = bpy.data.objects.new("Chicken", mesh)
scene.collection.objects.link(chicken)

# Simple box-projected UVs so engines that require UVs are happy.
bpy.context.view_layer.objects.active = chicken
chicken.select_set(True)
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
eb = arm_data.edit_bones

BONES = {  # name: (head, tail, parent)
    "Root":   ((0, 0, 0),         (0, 0, 0.08),        None),
    "Body":   ((0, 0.0, 0.18),    (0, 0.0, 0.28),      "Root"),
    "Neck":   ((0, -0.13, 0.28),  (0, -0.18, 0.38),    "Body"),
    "Head":   ((0, -0.18, 0.38),  (0, -0.18, 0.48),    "Neck"),
    "Tail":   ((0, 0.17, 0.28),   (0, 0.25, 0.36),     "Body"),
    "Wing.L": ((0.12, -0.06, 0.27), (0.12, 0.14, 0.27), "Body"),
    "Wing.R": ((-0.12, -0.06, 0.27), (-0.12, 0.14, 0.27), "Body"),
    "Leg.L":  ((0.06, 0.02, 0.15), (0.06, 0.02, 0.0),  "Body"),
    "Leg.R":  ((-0.06, 0.02, 0.15), (-0.06, 0.02, 0.0), "Body"),
}
for name, (h, t, parent) in BONES.items():
    b = eb.new(name)
    b.head, b.tail, b.roll = h, t, 0.0
    if parent:
        b.parent = eb[parent]
bpy.ops.object.mode_set(mode='OBJECT')

for name in BONES:
    if name != "Root":
        chicken.vertex_groups.new(name=name)
for i, bone in enumerate(bones_of_verts):
    chicken.vertex_groups[bone].add([i], 1.0, 'REPLACE')

chicken.parent = rig
mod = chicken.modifiers.new("Armature", 'ARMATURE')
mod.object = rig

# ---------------------------------------------------------------- animations
for pb in rig.pose.bones:
    pb.rotation_mode = 'XYZ'

rig.animation_data_create()


def make_action(name, length, keys):
    """keys: {bone: {"rot": [(frame, (x,y,z) degrees)], "loc": [(frame, (x,y,z))]}}"""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data.action = act
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
    for bone, chans in keys.items():
        pb = rig.pose.bones[bone]
        for f, r in chans.get("rot", []):
            pb.rotation_euler = [math.radians(a) for a in r]
            pb.keyframe_insert("rotation_euler", frame=f)
        for f, l in chans.get("loc", []):
            pb.location = l
            pb.keyframe_insert("location", frame=f)
    act.frame_range = (1, length)
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 1, act)
    track.mute = True
    rig.animation_data.action = None
    return act


make_action("Idle", 61, {
    "Body": {"loc": [(1, (0, 0, 0)), (31, (0, 0.004, 0)), (61, (0, 0, 0))]},
    "Neck": {"rot": [(1, (0, 0, 0)), (20, (0, 0, 0)), (24, (0, 0, 25)), (40, (0, 0, 25)),
                     (44, (0, 0, -15)), (56, (0, 0, -15)), (61, (0, 0, 0))]},
    "Head": {"rot": [(1, (0, 0, 0)), (24, (0, 0, 0)), (27, (0, 12, 0)), (38, (0, 12, 0)),
                     (41, (0, 0, 0)), (61, (0, 0, 0))]},
    "Tail": {"rot": [(1, (0, 0, 0)), (31, (6, 0, 0)), (61, (0, 0, 0))]},
})

make_action("Walk", 25, {
    "Body": {"loc": [(1, (0, 0.012, 0)), (7, (0, 0, 0)), (13, (0, 0.012, 0)), (19, (0, 0, 0)), (25, (0, 0.012, 0))],
             "rot": [(1, (0, 4, 0)), (13, (0, -4, 0)), (25, (0, 4, 0))]},
    "Leg.L": {"rot": [(1, (30, 0, 0)), (13, (-30, 0, 0)), (25, (30, 0, 0))]},
    "Leg.R": {"rot": [(1, (-30, 0, 0)), (13, (30, 0, 0)), (25, (-30, 0, 0))]},
    "Neck":  {"rot": [(1, (-15, 0, 0)), (7, (10, 0, 0)), (13, (-15, 0, 0)), (19, (10, 0, 0)), (25, (-15, 0, 0))]},
    "Tail":  {"rot": [(1, (0, 0, 6)), (13, (0, 0, -6)), (25, (0, 0, 6))]},
})

make_action("Peck", 31, {
    "Body": {"rot": [(1, (0, 0, 0)), (8, (20, 0, 0)), (24, (20, 0, 0)), (31, (0, 0, 0))]},
    "Neck": {"rot": [(1, (0, 0, 0)), (8, (55, 0, 0)), (11, (75, 0, 0)), (14, (55, 0, 0)),
                     (17, (75, 0, 0)), (20, (55, 0, 0)), (24, (55, 0, 0)), (31, (0, 0, 0))]},
    "Tail": {"rot": [(1, (0, 0, 0)), (8, (-10, 0, 0)), (24, (-10, 0, 0)), (31, (0, 0, 0))]},
})

make_action("Flap", 21, {
    "Body":   {"loc": [(1, (0, 0, 0)), (11, (0, 0.05, 0)), (21, (0, 0, 0))]},
    "Wing.L": {"rot": [(1, (0, 0, 0)), (4, (0, 70, 0)), (7, (0, 0, 0)), (10, (0, 70, 0)),
                       (13, (0, 0, 0)), (16, (0, 70, 0)), (21, (0, 0, 0))]},
    "Wing.R": {"rot": [(1, (0, 0, 0)), (4, (0, -70, 0)), (7, (0, 0, 0)), (10, (0, -70, 0)),
                       (13, (0, 0, 0)), (16, (0, -70, 0)), (21, (0, 0, 0))]},
    "Leg.L":  {"rot": [(1, (0, 0, 0)), (11, (25, 0, 0)), (21, (0, 0, 0))]},
    "Leg.R":  {"rot": [(1, (0, 0, 0)), (11, (25, 0, 0)), (21, (0, 0, 0))]},
})

# ---------------------------------------------------------------- export
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
chicken.select_set(True)
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
    path_mode='AUTO',
)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "chicken.blend"))
print("verts:", len(mesh.vertices), "tris:", sum(len(p.vertices) - 2 for p in mesh.polygons))
