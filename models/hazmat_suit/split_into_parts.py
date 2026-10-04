import bpy
S = "/tmp/claude-0/-home-user-idrk/5bdecf15-cebb-598e-8d2a-44714c28b2c8/scratchpad/"
bpy.ops.wm.open_mainfile(filepath=S+"glass2.blend")
# (object, material base name) -> (new part name, collection)
NAMES = {
 ('Hazmat_Suit','Hazmat_Yellow'):('Suit_Body','Suit'), ('Hazmat_Suit','Hazmat_Rubber'):('Suit_Gloves','Suit'),
 ('Hazmat_Suit','Hazmat_Tape'):('Suit_CuffTape','Suit'), ('Hazmat_Suit','Hazmat_Zipper'):('Suit_Zipper','Suit'),
 ('Hazmat_Zipper','Hazmat_Zipper'):('Suit_Zipper','Suit'), ('Hazmat_Belt','Hazmat_Rubber'):('Suit_Belt','Suit'),
 ('Hazmat_Hood','Hazmat_Yellow'):('Hood','Hood'), ('Hazmat_Hood','Hazmat_Rubber'):('Hood_VisorRim','Hood'),
 ('Hazmat_Hood','Hazmat_Glass'):('Hood_VisorGlass','Hood'),
 ('Hazmat_Respirator','Hazmat_Rubber'):('Respirator_Mask','Hood'), ('Hazmat_Respirator','Hazmat_Filter'):('Respirator_Filter','Hood'),
 ('Retopo_Cube.001','Fur_BrownBlack'):('Character_Fur','Character'), ('Retopo_Cube.001','Mouth_Dark'):('Character_NoseMouth','Character'),
 ('Cube.001','Eye_White'):('Character_EyeWhites','Character'), ('Pupils','Eye_Pupil'):('Character_Pupils','Character'),
}
COLORS = {'Suit_Body':(0.80,0.55,0.03),'Suit_Gloves':(0.02,0.02,0.02),'Suit_CuffTape':(0.28,0.28,0.27),'Suit_Zipper':(0.06,0.06,0.06),
 'Suit_Belt':(0.02,0.02,0.02),'Hood':(0.80,0.55,0.03),'Hood_VisorRim':(0.02,0.02,0.02),'Respirator_Mask':(0.02,0.02,0.02),
 'Respirator_Filter':(0.22,0.23,0.20)}
base = lambda n: n.split('.')[0]
cols = {}
for c in ('Suit','Hood','Character'):
    cols[c] = bpy.data.collections.new(c); bpy.context.scene.collection.children.link(cols[c])
# unparent pupils keeping position, apply all transforms
for o in list(bpy.data.objects):
    if o.type != 'MESH': continue
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
parts = []
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    src = o.name
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    if len(o.material_slots) > 1: bpy.ops.mesh.separate(type='MATERIAL')
    for p in [x for x in bpy.context.selected_objects]:
        # drop unused material slots
        used = {f.material_index for f in p.data.polygons}
        mat = p.material_slots[list(used)[0]].material
        p.data.materials.clear(); p.data.materials.append(mat)
        for f in p.data.polygons: f.material_index = 0
        name, col = NAMES[(src, base(mat.name))]
        parts.append((p, name, col))
merged = {}
for p, name, col in parts:
    merged.setdefault(name, []).append((p, col))
for name, lst in merged.items():
    objs = [p for p, _ in lst]
    bpy.ops.object.select_all(action='DESELECT')
    for p in objs: p.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = objs[0]; o.name = name; o.data.name = name
    for c in list(o.users_collection): c.objects.unlink(o)
    cols[lst[0][1]].objects.link(o)
    # own unique material per part
    m = o.data.materials[0].copy(); m.name = 'M_' + name; o.data.materials[0] = m
    if name in COLORS:
        m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*COLORS[name], 1)
        m.diffuse_color = (*COLORS[name], 1)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
for m in [m for m in bpy.data.materials if m.users == 0]: bpy.data.materials.remove(m)
for me in [m for m in bpy.data.meshes if m.users == 0]: bpy.data.meshes.remove(me)
for img in bpy.data.images:
    if img.has_data and not img.packed_file: img.pack()
for c in cols.values():
    print(c.name, sorted((o.name, len(o.data.vertices), o.data.materials[0].name) for o in c.objects))
print("loose", [o.name for o in bpy.context.scene.collection.objects])
bpy.ops.wm.save_as_mainfile(filepath=S+"HazmatSuit_Editable.blend")
bpy.ops.export_scene.fbx(filepath=S+"HazmatSuit_Editable.fbx", object_types={'MESH'}, add_leaf_bones=False,
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
