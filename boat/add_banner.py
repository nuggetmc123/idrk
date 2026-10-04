"""
Adds "LOST VR" banners (Banner_L / Banner_R) to the sides of a boat FBX.

    python3 add_banner.py <input.fbx> <output.fbx>

The banners are shrink-wrapped onto the object named "Hull" with ray casts, so they
follow its panels, and sit 1.2 cm off the planks. The banner texture is drawn by
make_banner.py. The FBX is re-exported with all textures embedded.
"""
import os
import sys

import bpy  # noqa: I001
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_banner import make_banner  # noqa: E402

src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

# Re-link the boat texture if the FBX only referenced it by a path that isn't here.
for img in bpy.data.images:
    if not img.has_data:
        local = os.path.join(HERE, "textures", os.path.basename(img.filepath))
        if os.path.exists(local):
            img.filepath = local
            img.reload()

hull = bpy.data.objects["Hull"]
dg = bpy.context.evaluated_depsgraph_get()
bm = bmesh.new()
bm.from_object(hull, dg)
bm.transform(hull.matrix_world)
bvh = BVHTree.FromBMesh(bm)

# Banner area in world space (Blender: bow = -Y, Z up): ~1.5 m long, 0.28 m tall,
# mid-boat, below the gunwale rail.
Y_STERN, Y_BOW = 1.19, -0.33
Z_TOP, Z_BOT = 0.48, 0.20
COLS, ROWS = 24, 4
OFFSET = 0.012

img = bpy.data.images.load(make_banner(os.path.join(HERE, "textures", "T_Banner.png")))
mat = bpy.data.materials.new("M_Banner")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs["Roughness"].default_value = 0.95
tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = img
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

banners = []
for side, name in ((1, "Banner_R"), (-1, "Banner_L")):
    out = bmesh.new()
    uv = out.loops.layers.uv.new("UVMap")
    grid = []
    for i in range(COLS + 1):
        y = Y_STERN + (Y_BOW - Y_STERN) * i / COLS
        col = []
        for j in range(ROWS + 1):
            z = Z_TOP + (Z_BOT - Z_TOP) * j / ROWS
            hit, normal, _, _ = bvh.ray_cast(Vector((side * 3.0, y, z)), Vector((-side, 0, 0)))
            if hit is None:
                raise SystemExit(f"banner ray missed the hull at y={y:.2f} z={z:.2f}")
            if normal.x * side < 0:
                normal = -normal
            col.append(out.verts.new(hit + normal * OFFSET))
        grid.append(col)
    for i in range(COLS):
        for j in range(ROWS):
            f = out.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
            f.normal_update()
            if f.normal.x * side < 0:
                f.normal_flip()
            for loop in f.loops:
                ii = next(a for a, c in enumerate(grid) if loop.vert in c)
                jj = grid[ii].index(loop.vert)
                u = ii / COLS                    # 0 at the stern end
                if side > 0:
                    u = 1 - u                    # text reads left-to-right from either side
                loop[uv].uv = (u, 1 - jj / ROWS)
    me = bpy.data.meshes.new(name)
    out.to_mesh(me)
    out.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    banners.append(o)

for i in bpy.data.images:
    if i.has_data:
        i.pack()

bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects:
    if o.type == "MESH":
        o.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    path_mode="COPY",
    embed_textures=True,
)
print("wrote", dst, "with", [b.name for b in banners])
