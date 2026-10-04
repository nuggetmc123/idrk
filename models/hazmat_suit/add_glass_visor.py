import bpy, bmesh, math, numpy as np
from mathutils import Vector, Matrix
S = "/tmp/claude-0/-home-user-idrk/5bdecf15-cebb-598e-8d2a-44714c28b2c8/scratchpad/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=S+"up2.fbx")
hood = bpy.data.objects['Hazmat_Hood']
W = hood.matrix_world
P = np.array([tuple(W @ v.co) for v in hood.data.vertices])

# fit axis-aligned ellipsoid to the upper hood (above the drape): Ax^2+By^2+Cz^2+Dx+Ey+Fz = 1
zmid = (P[:,2].min() + P[:,2].max())/2
U = P[P[:,2] > zmid]
A = np.c_[U[:,0]**2, U[:,1]**2, U[:,2]**2, U[:,0], U[:,1], U[:,2]]
a,b,c,d,e,f = np.linalg.lstsq(A, np.ones(len(U)), rcond=None)[0]
cen = np.array([-d/(2*a), -e/(2*b), -f/(2*c)])
g = 1 + a*cen[0]**2 + b*cen[1]**2 + c*cen[2]**2
semi = np.sqrt(g/np.array([a,b,c]))
print("center", cen, "semi", semi)

FWD = Vector((0, -1, 0.06)).normalized(); SIDE = Vector((1,0,0)); UP = FWD.cross(SIDE).normalized()
if UP.z < 0: UP = -UP
# per-azimuth opening angle = smallest polar angle (from FWD, in normalized ellipsoid space) of any hood vertex
NB = 72; thb = [math.pi]*NB
for p in P:
    q = Vector((p - cen)/semi)
    if q.length < 1e-6: continue
    q.normalize()
    th = math.acos(max(-1, min(1, q.dot(FWD))))
    phi = math.atan2(q.dot(UP), q.dot(SIDE)) % (2*math.pi)
    k = int(phi/(2*math.pi)*NB) % NB
    thb[k] = min(thb[k], th)
# smooth the boundary a bit, then overlap under the rubber rim
thb = [ (thb[(k-1)%NB] + 2*thb[k] + thb[(k+1)%NB])/4 for k in range(NB) ]
print("opening half-angles deg: min %.1f max %.1f" % (math.degrees(min(thb)), math.degrees(max(thb))))

def on_ell(th, phi, scale=1.004):
    dvec = FWD*math.cos(th) + (SIDE*math.cos(phi) + UP*math.sin(phi))*math.sin(th)
    # scale so the point lies on the ellipsoid along direction (in normalized space)
    return Vector(cen) + Vector((dvec.x*semi[0], dvec.y*semi[1], dvec.z*semi[2]))*scale

bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
RINGS = 14
centerv = bm.verts.new(on_ell(0, 0))
rings = []
for r in range(1, RINGS+1):
    row = []
    for k in range(NB):
        phi = (k+0.5)/NB*2*math.pi
        row.append(bm.verts.new(on_ell((thb[k] + 0.06)*r/RINGS, phi)))
    rings.append(row)
for k in range(NB):
    bm.faces.new([centerv, rings[0][k], rings[0][(k+1)%NB]])
for r in range(RINGS-1):
    for k in range(NB):
        bm.faces.new([rings[r][k], rings[r+1][k], rings[r+1][(k+1)%NB], rings[r][(k+1)%NB]])
bm.normal_update()
for fc in bm.faces:   # face outward (toward -Y / away from center)
    if fc.normal.dot(fc.calc_center_median() - Vector(cen)) < 0: fc.normal_flip()
xs = [v.co.x for v in bm.verts]; zs = [v.co.z for v in bm.verts]
for fc in bm.faces:
    for l in fc.loops:
        l[uv].uv = ((l.vert.co.x-min(xs))/(max(xs)-min(xs)), (l.vert.co.z-min(zs))/(max(zs)-min(zs)))
# store in hood's object space so it shares its origin/scale
bm.transform(W.inverted())
me = bpy.data.meshes.new('Hazmat_Visor'); bm.to_mesh(me)
for p in me.polygons: p.use_smooth = True
vis = bpy.data.objects.new('Hazmat_Visor', me); bpy.context.scene.collection.objects.link(vis)
vis.matrix_world = W.copy()

# glass texture: light blue-green tint, mostly transparent, with soft diagonal reflection streaks
N = 512
yy, xx = np.mgrid[0:N, 0:N] / (N-1)
t = (xx + yy)
streak = np.exp(-((t-0.55)/0.035)**2)*0.55 + np.exp(-((t-0.68)/0.015)**2)*0.35 + np.exp(-((t-1.35)/0.05)**2)*0.15
edge = np.clip((np.hypot(xx-0.5, yy-0.5)-0.38)/0.14, 0, 1)**2*0.25      # slightly cloudier toward the rim
alpha = np.clip(0.16 + streak + edge, 0, 0.9)
rgb = np.stack([0.72+0.28*streak, 0.88+0.12*streak, 0.92+0.08*streak], -1).clip(0, 1)
img = bpy.data.images.new('Hazmat_Glass', N, N, alpha=True)
img.pixels.foreach_set(np.concatenate([rgb, alpha[...,None]], -1).astype(np.float32).ravel())
img.filepath_raw = S+"Hazmat_Glass.png"; img.file_format = 'PNG'; img.save()

m = bpy.data.materials.new('Hazmat_Glass'); m.use_nodes = True
nt = m.node_tree; p = nt.nodes['Principled BSDF']
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img
nt.links.new(tex.outputs['Color'], p.inputs['Base Color'])
nt.links.new(tex.outputs['Alpha'], p.inputs['Alpha'])
p.inputs['Roughness'].default_value = 0.04
p.inputs['Specular IOR Level'].default_value = 0.8
m.surface_render_method = 'BLENDED'; m.use_backface_culling = False
m.diffuse_color = (0.75, 0.9, 0.95, 0.25)
me.materials.append(m)

bpy.ops.wm.save_as_mainfile(filepath=S+"glass.blend")
bpy.ops.export_scene.fbx(filepath=S+"HazmatSuit_Glass.fbx", object_types={'MESH'}, add_leaf_bones=False,
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
print("DONE")
