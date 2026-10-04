import bpy
S = "/tmp/claude-0/-home-user-idrk/5bdecf15-cebb-598e-8d2a-44714c28b2c8/scratchpad/"
bpy.ops.wm.open_mainfile(filepath=S+"glass.blend")
m = bpy.data.materials['Hazmat_Glass']; nt = m.node_tree; p = nt.nodes['Principled BSDF']
for l in list(p.inputs['Alpha'].links): nt.links.remove(l)     # opacity as a plain value importers read
p.inputs['Alpha'].default_value = 0.25
p.inputs['Transmission Weight'].default_value = 1.0
hood = bpy.data.objects['Hazmat_Hood']; vis = bpy.data.objects['Hazmat_Visor']
bpy.ops.object.select_all(action='DESELECT')
vis.select_set(True); hood.select_set(True); bpy.context.view_layer.objects.active = hood
bpy.ops.object.join()
print("HOOD MATS", [s.material.name for s in hood.material_slots])
bpy.ops.wm.save_as_mainfile(filepath=S+"glass2.blend")
bpy.ops.export_scene.fbx(filepath=S+"HazmatSuit_GlassOn.fbx", object_types={'MESH'}, add_leaf_bones=False,
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=S+"HazmatSuit_GlassOn.fbx")
g = bpy.data.materials['Hazmat_Glass']; gp = g.node_tree.nodes['Principled BSDF']
print("REIMPORT objs", sorted(o.name for o in bpy.data.objects))
print("REIMPORT hood mats", [s.material.name for s in bpy.data.objects['Hazmat_Hood'].material_slots], "glass alpha", round(gp.inputs['Alpha'].default_value,3))
