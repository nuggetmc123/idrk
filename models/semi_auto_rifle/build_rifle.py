"""Builds a scrappy semi-automatic rifle (Rust SAR / Stray(ed) VR style) and
exports it as FBX + .blend + a preview render.

Run:  python3 build_rifle.py        (needs `pip install bpy`)

Conventions
- Units are meters. The rifle is ~1.0 m long.
- The muzzle points along Blender -Y, which becomes +Z (forward) in Unity /
  Unreal with the FBX export settings used below. Up is +Z in Blender (+Y in Unity).
- Moving parts are separate objects with their pivot where they move from:
  Magazine (top of the mag, at the mag well), Bolt (slides along +Y),
  Trigger (pivot pin), so they can be animated for VR reloads.
- Empties mark useful sockets: Socket_GripRight, Socket_GripLeft,
  Socket_Muzzle, Socket_MagWell, Socket_EjectPort, Socket_RearSight, Socket_FrontSight.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index('--out')+1] if '--out' in sys.argv else HERE

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials
def mat(name, color, metal=0.0, rough=0.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    m.diffuse_color = (*color, 1)
    return m

M_WOOD   = mat('M_Wood',        (0.15, 0.065, 0.022), 0.0, 0.6)
M_WOOD_D = mat('M_WoodDark',    (0.08, 0.035, 0.012), 0.0, 0.7)
M_STEEL  = mat('M_GunSteel',    (0.07, 0.07, 0.075), 1.0, 0.42)
M_WORN   = mat('M_WornSteel',   (0.28, 0.27, 0.26), 1.0, 0.55)
M_RUST   = mat('M_RustyMetal',  (0.16, 0.055, 0.018), 0.4, 0.85)
M_TAPE   = mat('M_DuctTape',    (0.11, 0.11, 0.115), 0.0, 0.9)
M_BRASS  = mat('M_Brass',       (0.62, 0.45, 0.16), 1.0, 0.35)

# ---------------------------------------------------------------- helpers
def link(ob, parent=None):
    scene.collection.objects.link(ob)
    if parent: ob.parent = parent
    return ob

def new_obj(name, bm, material, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    me.materials.append(material)
    return link(bpy.data.objects.new(name, me), parent)

def box(name, size, loc, material, parent=None, bevel=0.003, rot=(0,0,0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = new_obj(name, bm, material, parent)
    ob.location = loc; ob.rotation_euler = rot
    if bevel: add_bevel(ob, bevel)
    return ob

def cyl(name, r, length, loc, material, parent=None, axis='Y', seg=24, bevel=0.0015, r2=None):
    """Cylinder (or cone if r2) centred on loc, along an axis."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=r if r2 is None else r2, depth=length)
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.pi/2, 3, 'X'))
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.pi/2, 3, 'Y'))
    ob = new_obj(name, bm, material, parent)
    ob.location = loc
    if bevel: add_bevel(ob, bevel, segs=1)
    return ob

def torus(name, R, r, loc, material, parent=None, axis='Y', seg=24, mseg=8):
    bm = bmesh.new()
    verts = []
    for i in range(seg):
        a = i/seg*math.tau
        ring = []
        for j in range(mseg):
            b = j/mseg*math.tau
            ring.append(bm.verts.new(((R + r*math.cos(b))*math.cos(a), (R + r*math.cos(b))*math.sin(a), r*math.sin(b))))
        verts.append(ring)
    for i in range(seg):
        for j in range(mseg):
            bm.faces.new((verts[i][j], verts[(i+1)%seg][j], verts[(i+1)%seg][(j+1)%mseg], verts[i][(j+1)%mseg]))
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.pi/2, 3, 'X'))
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, matrix=Matrix.Rotation(math.pi/2, 3, 'Y'))
    ob = new_obj(name, bm, material, parent)
    ob.location = loc
    return ob

def profile(name, pts_yz, width, material, parent=None, bevel=0.004, x=0.0):
    """Extrude a side-profile polygon (list of (y,z), counter-clockwise) across X."""
    bm = bmesh.new()
    L = [bm.verts.new((x - width/2, y, z)) for y, z in pts_yz]
    R = [bm.verts.new((x + width/2, y, z)) for y, z in pts_yz]
    n = len(pts_yz)
    bm.faces.new(list(reversed(L)))
    bm.faces.new(R)
    for i in range(n):
        j = (i+1) % n
        bm.faces.new((L[i], L[j], R[j], R[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = new_obj(name, bm, material, parent)
    if bevel: add_bevel(ob, bevel, segs=3)
    return ob

def add_bevel(ob, w, segs=2):
    m = ob.modifiers.new('Bevel', 'BEVEL')
    m.width = w; m.segments = segs; m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(40)
    m.harden_normals = False

def empty(name, loc, parent, size=0.03):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'ARROWS'; e.empty_display_size = size
    e.location = loc
    return link(e, parent)

def set_origin(ob, world_point):
    """Move an object's pivot to world_point without moving its geometry."""
    off = world_point - ob.location
    ob.data.transform(Matrix.Translation(-off))
    ob.location = world_point

# ================================================================ the rifle
# Y < 0 is forward (muzzle). Receiver spans y = -0.20 .. 0.10, bore axis at z = 0.
root = bpy.data.objects.new('SemiAutoRifle', None)
root.empty_display_type = 'PLAIN_AXES'; root.empty_display_size = 0.1
link(root)

# --- receiver: welded-together steel box with a raised top and a patch plate
recv = profile('Receiver', [(-0.205,-0.035),(0.105,-0.035),(0.105,0.030),(0.07,0.040),
                            (-0.17,0.040),(-0.205,0.028)], 0.046, M_STEEL, root, bevel=0.003)
box('Receiver_DustCover', (0.040, 0.20, 0.010), (0, -0.065, 0.044), M_WORN, root, bevel=0.002)
box('Receiver_PatchPlate', (0.003, 0.085, 0.040), (0.0245, -0.13, -0.002), M_RUST, root, bevel=0.0008,
    rot=(0, 0, 0)).rotation_euler = (math.radians(3), 0, 0)
# weld bead along the patch (row of little bumps)
for i in range(14):
    y = -0.17 + i*0.006
    cyl(f'Weld_{i:02d}', 0.0022, 0.004, (0.026, y, 0.019 + (i%2)*0.0006), M_WORN, root, axis='X', seg=8, bevel=0)
# rivets / screws
for (y, z) in [(-0.185,0.02),(-0.185,-0.022),(0.085,0.02),(0.085,-0.022),(-0.04,-0.024),(0.03,-0.024)]:
    for sx in (-1, 1):
        cyl('Rivet', 0.0035, 0.004, (sx*0.0245, y, z), M_WORN, root, axis='X', seg=10, bevel=0)
# ejection port (dark inset on the right)
box('EjectionPort', (0.004, 0.055, 0.016), (0.022, -0.06, 0.022), M_STEEL, root, bevel=0.001)

# --- bolt / charging handle (separate, slides back along +Y)
bolt = box('Bolt', (0.012, 0.075, 0.014), (0.016, -0.06, 0.022), M_WORN, root, bevel=0.002)
knob = cyl('Bolt_Handle', 0.0045, 0.040, (0.040, -0.035, 0.022), M_WORN, root, axis='X', seg=12)
cyl('Bolt_Knob', 0.0085, 0.012, (0.062, -0.035, 0.022), M_STEEL, root, axis='X', seg=16)
for o in [ob for ob in scene.objects if ob.name.startswith(('Bolt_Handle', 'Bolt_Knob'))]:
    o.parent = None
    o.location -= bolt.location
    o.parent = bolt

# --- barrel, gas tube, muzzle
cyl('Barrel', 0.0115, 0.50, (0, -0.455, 0.004), M_STEEL, root, seg=28)
cyl('Barrel_Chamber', 0.017, 0.05, (0, -0.225, 0.004), M_STEEL, root, seg=28)
cyl('GasTube', 0.0065, 0.29, (0, -0.37, 0.026), M_WORN, root, seg=16)
cyl('GasBlock', 0.016, 0.03, (0, -0.53, 0.012), M_STEEL, root, seg=20)
box('GasBlock_Link', (0.012, 0.03, 0.02), (0, -0.53, 0.024), M_STEEL, root, bevel=0.002)
muz = cyl('Muzzle', 0.016, 0.06, (0, -0.73, 0.004), M_STEEL, root, seg=24)
# muzzle brake slots: thin dark boxes either side
for i in range(3):
    for sx in (-1, 1):
        box('Muzzle_Slot', (0.006, 0.008, 0.018), (sx*0.0135, -0.71 - i*0.016, 0.004), M_RUST, root, bevel=0)
# front sight: post + protective ears
box('FrontSight_Base', (0.018, 0.022, 0.018), (0, -0.655, 0.022), M_STEEL, root, bevel=0.002)
box('FrontSight_Post', (0.003, 0.004, 0.026), (0, -0.655, 0.042), M_STEEL, root, bevel=0)
for sx in (-1, 1):
    box('FrontSight_Ear', (0.003, 0.012, 0.03), (sx*0.0085, -0.655, 0.042), M_STEEL, root, bevel=0.0008)
# rear sight: notched leaf on the receiver
for sx in (-1, 1):
    box('RearSight_Leaf', (0.008, 0.010, 0.020), (sx*0.0075, 0.06, 0.055), M_STEEL, root, bevel=0.001)
box('RearSight_Base', (0.026, 0.024, 0.008), (0, 0.06, 0.044), M_STEEL, root, bevel=0.0015)

# --- wooden handguard with duct-tape wrap and hose clamps (the scrap look)
hg = profile('Handguard', [(-0.215,-0.022),(-0.50,-0.020),(-0.505,0.018),(-0.215,0.020)],
             0.050, M_WOOD, root, bevel=0.010)
hg.location.z = 0.002
torus('Clamp_Rear', 0.028, 0.0025, (0, -0.25, 0.002), M_WORN, root, seg=24)
torus('Clamp_Front', 0.027, 0.0025, (0, -0.48, 0.002), M_WORN, root, seg=24)
cyl('Clamp_Screw', 0.003, 0.012, (0.03, -0.25, 0.002), M_WORN, root, axis='X', seg=8, bevel=0)
cyl('Clamp_Screw', 0.003, 0.012, (0.029, -0.48, 0.002), M_WORN, root, axis='X', seg=8, bevel=0)
tape = cyl('DuctTape_Wrap', 0.031, 0.07, (0, -0.375, 0.002), M_TAPE, root, seg=24, bevel=0.002)
tape.scale = (1.0, 1.0, 0.86)
tape.rotation_euler = (0, math.radians(4), 0)

# --- stock: one piece of wood with a wrist, metal butt plate
stock = profile('Stock', [
    (0.100, 0.028), (0.180, 0.026), (0.420, 0.030), (0.428, 0.010), (0.425, -0.110),
    (0.395, -0.118), (0.250, -0.072), (0.185, -0.068), (0.165, -0.100), (0.145, -0.112),
    (0.120, -0.104), (0.118, -0.060), (0.100, -0.035)], 0.044, M_WOOD, root, bevel=0.009)
box('ButtPlate', (0.046, 0.010, 0.146), (0, 0.430, -0.040), M_RUST, root, bevel=0.003).rotation_euler = (math.radians(-2), 0, 0)
box('Stock_Cheek', (0.046, 0.10, 0.012), (0, 0.33, 0.030), M_WOOD_D, root, bevel=0.004)
# sling swivels
torus('Sling_Rear', 0.010, 0.0018, (0, 0.38, -0.110), M_WORN, root, axis='X', seg=16, mseg=6)
torus('Sling_Front', 0.010, 0.0018, (0, -0.43, -0.030), M_WORN, root, axis='X', seg=16, mseg=6)

# --- trigger guard (bent strip) and trigger (separate, pivots on its pin)
guard = profile('TriggerGuard', [(0.015,-0.034),(0.015,-0.040),(0.035,-0.066),(0.075,-0.068),
                                  (0.098,-0.040),(0.098,-0.034),(0.092,-0.034),(0.092,-0.040),
                                  (0.072,-0.061),(0.038,-0.060),(0.022,-0.040),(0.022,-0.034)],
                0.010, M_STEEL, root, bevel=0.0012)
trig = profile('Trigger', [(0.050,-0.034),(0.058,-0.034),(0.060,-0.045),(0.058,-0.056),
                            (0.053,-0.058),(0.054,-0.047)], 0.006, M_WORN, root, bevel=0.0008)
set_origin(trig, Vector((0, 0.054, -0.034)))

# --- magazine: slightly curved steel box mag (separate, pivot at the mag well)
mb = bmesh.new()
segs = 8
w_, d_ = 0.026, 0.050           # x width, y depth
rows = []
for i in range(segs+1):
    t = i/segs
    z = -0.035 - t*0.165
    yoff = -0.02*t*t             # curves forward as it goes down
    ring = [mb.verts.new((sx*w_/2, -0.08 + yoff + sy*d_/2, z)) for sx, sy in ((-1,-1),(1,-1),(1,1),(-1,1))]
    rows.append(ring)
for i in range(segs):
    a, b = rows[i], rows[i+1]
    for k in range(4):
        mb.faces.new((a[k], a[(k+1)%4], b[(k+1)%4], b[k]))
mb.faces.new(list(reversed(rows[0]))); mb.faces.new(rows[-1])
bmesh.ops.recalc_face_normals(mb, faces=mb.faces)
mag = new_obj('Magazine', mb, M_STEEL, root)
add_bevel(mag, 0.003)
set_origin(mag, Vector((0, -0.08, -0.035)))
box('Magazine_Baseplate', (0.032, 0.058, 0.008), (0, -0.02, -0.168), M_RUST, mag, bevel=0.002)
box('Magazine_Rib_L', (0.003, 0.034, 0.12), (-0.0135, -0.006, -0.07), M_WORN, mag, bevel=0.0008).rotation_euler = (math.radians(-7), 0, 0)
box('Magazine_Rib_R', (0.003, 0.034, 0.12), (0.0135, -0.006, -0.07), M_WORN, mag, bevel=0.0008).rotation_euler = (math.radians(-7), 0, 0)
cyl('Magazine_TopRound', 0.0045, 0.040, (0, 0.0, 0.004), M_BRASS, mag, seg=12)
box('MagWell', (0.034, 0.060, 0.012), (0, -0.08, -0.038), M_STEEL, root, bevel=0.002)
box('MagRelease', (0.012, 0.008, 0.010), (0, -0.044, -0.040), M_WORN, root, bevel=0.001)

# --- sockets for VR / gameplay
empty('Socket_GripRight', (0, 0.17, -0.045), root)
empty('Socket_GripLeft',  (0, -0.36, -0.01), root)
empty('Socket_Muzzle',    (0, -0.765, 0.004), root)
empty('Socket_MagWell',   (0, -0.08, -0.035), root)
empty('Socket_EjectPort', (0.03, -0.06, 0.022), root)
empty('Socket_RearSight', (0, 0.06, 0.06), root)
empty('Socket_FrontSight',(0, -0.655, 0.055), root)

# ================================================================ finalize
# apply modifiers, smooth by angle, simple UVs so it can be textured
for ob in list(scene.objects):
    if ob.type != 'MESH': continue
    bpy.context.view_layer.objects.active = ob
    for o in scene.objects: o.select_set(False)
    ob.select_set(True)
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    # the smooth-by-angle modifier the op adds must be applied for FBX
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)

# merge the many little static bits into a few meshes per material so it is
# cheap in-engine; moving parts (Magazine, Bolt, Trigger) stay separate.
MOVING = {'Magazine', 'Bolt', 'Trigger'}
def is_static(ob):
    p = ob
    while p:
        if p.name in MOVING: return False
        p = p.parent
    return ob.type == 'MESH'
static = [o for o in scene.objects if is_static(o)]
for o in scene.objects: o.select_set(False)
for o in static: o.select_set(True)
bpy.context.view_layer.objects.active = recv
bpy.ops.object.join()
recv.name = 'Rifle_Body'; recv.data.name = 'Rifle_Body'
# join the children of each moving part into it
for name in MOVING:
    parent = scene.objects[name]
    kids = [o for o in parent.children if o.type == 'MESH']
    if not kids: continue
    for o in scene.objects: o.select_set(False)
    for o in kids: o.select_set(True)
    parent.select_set(True)
    bpy.context.view_layer.objects.active = parent
    bpy.ops.object.join()

tris = sum(len(p.vertices) - 2 for o in scene.objects if o.type == 'MESH' for p in o.data.polygons)
print('triangles:', tris)

os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'semi_auto_rifle.blend'), compress=True)
bak = os.path.join(OUT, 'semi_auto_rifle.blend1')
if os.path.exists(bak): os.remove(bak)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, 'semi_auto_rifle.fbx'),
    use_selection=False, object_types={'EMPTY', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
    path_mode='AUTO')

# ================================================================ preview
cam_data = bpy.data.cameras.new('Cam'); cam_data.lens = 42
cam = link(bpy.data.objects.new('PreviewCam', cam_data))
cam.location = (1.75, 0.10, 0.40)
d = Vector((0, -0.16, -0.04)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
for name, loc, e in [('Key', (1.5, 0.8, 1.6), 140), ('Fill', (1.2, -1.4, 0.2), 45), ('Rim', (-1.0, 0.5, 1.2), 90)]:
    ld = bpy.data.lights.new(name, 'AREA'); ld.energy = e; ld.size = 1.2
    lo = link(bpy.data.objects.new(name, ld)); lo.location = loc
    lo.rotation_euler = (Vector((0, -0.2, 0)) - lo.location).to_track_quat('-Z', 'Y').to_euler()
world = bpy.data.worlds.new('W'); scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.05, 0.055, 0.06, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1600, 800
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.render.filepath = os.path.join(OUT, 'preview.png')
if '--no-render' not in sys.argv:
    bpy.ops.render.render(write_still=True)
print('done ->', OUT)
