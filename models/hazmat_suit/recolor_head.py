import bpy, bmesh, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="up.fbx")

def mat(name, col, rough):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*col, 1); p.inputs['Roughness'].default_value = rough
    m.diffuse_color = (*col, 1)
    return m
FUR   = mat('Fur_BrownBlack', (0.045, 0.026, 0.015), 0.85)
MOUTH = mat('Mouth_Dark',     (0.012, 0.007, 0.005), 0.6)
WHITE = mat('Eye_White',      (0.90, 0.90, 0.88), 0.25)
PUPIL = mat('Eye_Pupil',      (0.005, 0.005, 0.005), 0.15)

body = bpy.data.objects['Retopo_Cube.001']
body.data.materials[0] = FUR; body.data.materials[1] = MOUTH
eyes = bpy.data.objects['Cube.001']
eyes.data.materials.clear(); eyes.data.materials.append(WHITE)

# pupils: black discs projected onto the front of each eyeball
eb = bmesh.new(); eb.from_mesh(eyes.data); eb.transform(eyes.matrix_world)
bvh = BVHTree.FromBMesh(eb)
pb = bmesh.new()
R, RINGS, SEG = 0.034, 6, 32
for side in (-1, 1):
    pts = [v.co for v in eb.verts if v.co.x*side > 0]
    cx = (min(p.x for p in pts) + max(p.x for p in pts))/2
    cz = (min(p.z for p in pts) + max(p.z for p in pts))/2
    def proj(x, z):
        h, n, _, _ = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
        return h + n*0.002
    center = pb.verts.new(proj(cx, cz)); prev = None
    rings = []
    for k in range(1, RINGS+1):
        r = R*k/RINGS
        rings.append([pb.verts.new(proj(cx + r*math.cos(2*math.pi*j/SEG), cz + r*math.sin(2*math.pi*j/SEG))) for j in range(SEG)])
    for j in range(SEG):
        pb.faces.new([center, rings[0][(j+1)%SEG], rings[0][j]])
    for k in range(RINGS-1):
        for j in range(SEG):
            pb.faces.new([rings[k][j], rings[k][(j+1)%SEG], rings[k+1][(j+1)%SEG], rings[k+1][j]])
pb.normal_update()
for f in pb.faces:
    if f.normal.y > 0: f.normal_flip()
me = bpy.data.meshes.new('Pupils'); pb.to_mesh(me); me.materials.append(PUPIL)
for p in me.polygons: p.use_smooth = True
po = bpy.data.objects.new('Pupils', me); bpy.context.scene.collection.objects.link(po)
# keep pupils in the eye object's space so they move with it
po.parent = eyes; po.matrix_parent_inverse = eyes.matrix_world.inverted()

for m in [m for m in bpy.data.materials if m.users == 0]: bpy.data.materials.remove(m)
bpy.ops.wm.save_as_mainfile(filepath="/tmp/claude-0/-home-user-idrk/5bdecf15-cebb-598e-8d2a-44714c28b2c8/scratchpad/recolor.blend")
bpy.ops.export_scene.fbx(filepath="HazmatSuit_BrownHead.fbx", object_types={'MESH'}, add_leaf_bones=False,
    mesh_smooth_type='FACE', path_mode='AUTO', embed_textures=False)
print("DONE")
