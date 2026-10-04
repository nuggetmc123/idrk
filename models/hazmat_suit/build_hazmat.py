import bpy, bmesh, math, mathutils
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

import sys
bpy.ops.wm.open_mainfile(filepath=sys.argv[-1] if sys.argv[-1].endswith(".blend") else "peeper.blend")
arm = bpy.data.objects['Armature']
body = bpy.data.objects['Retopo_Cube.001']
orig_pose = arm.data.pose_position
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
bme = body.evaluated_get(dg).to_mesh()
M = body.matrix_world.copy()
BONES = [b.name for b in arm.data.bones]
HAND = {n for n in BONES if any(n.startswith(p) for p in ('Hand','Pinky','Pointer','Thumb'))}

# ---------- materials ----------
def mat(name, col, rough=0.6, metal=0.0, alpha=1.0, trans=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*col, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    p.inputs['Alpha'].default_value = alpha
    if trans: p.inputs['Transmission Weight'].default_value = trans
    m.diffuse_color = (*col, alpha)
    if alpha < 1:
        m.surface_render_method = 'BLENDED'
    return m
M_SUIT  = mat('Hazmat_Yellow', (0.80, 0.55, 0.03), 0.65)
M_RUB   = mat('Hazmat_Rubber', (0.02, 0.02, 0.02), 0.45)
M_TAPE  = mat('Hazmat_Tape',   (0.28, 0.28, 0.27), 0.85)
M_ZIP   = mat('Hazmat_Zipper', (0.06, 0.06, 0.06), 0.35, 0.6)
M_GLASS = mat('Hazmat_Visor',  (0.55, 0.70, 0.65), 0.05, 0.0, 0.35, 0.9)
M_FILT  = mat('Hazmat_Filter', (0.22, 0.23, 0.20), 0.4, 0.7)
M_DECAL = mat('Hazmat_Decal',  (0.01, 0.01, 0.01), 0.7)

def new_obj(name, bm, mats):
    for l in list(bm.verts.layers.deform.values()): bm.verts.layers.deform.remove(l)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o

def finish(o, weights):
    """parent to armature (keep world placement), add groups + armature modifier"""
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    for b in BONES: o.vertex_groups.new(name=b)
    for i, w in enumerate(weights):
        for b, val in w.items():
            if val > 1e-4: o.vertex_groups[b].add([i], val, 'REPLACE')
    md = o.modifiers.new('Armature', 'ARMATURE'); md.object = arm

# body weights per vertex
gname = {g.index: g.name for g in body.vertex_groups}
bw = []
for v in body.data.vertices:
    d = {gname[g.group]: g.weight for g in v.groups if g.weight > 0}
    s = sum(d.values()) or 1
    bw.append({k: x/s for k, x in d.items()})

# ---------- head / hood ellipsoid ----------
head_pts = [M @ bme.vertices[i].co for i, w in enumerate(bw) if w and max(w, key=w.get) == 'Head']
hc = Vector((0, sum(p.y for p in head_pts)/len(head_pts), 0.0))
hc.z = (min(p.z for p in head_pts) + max(p.z for p in head_pts))/2 + 0.01
ax = Vector((1.0, 1.05, 0.95))
need = max(math.sqrt(((p.x-hc.x)/ax.x)**2+((p.y-hc.y)/ax.y)**2+((p.z-hc.z)/ax.z)**2) for p in head_pts)
R = need * 1.07
HA = Vector((ax.x*R, ax.y*R, ax.z*R))
print("hood center", hc, "semi-axes", HA)
def ell(p):  # normalized ellipsoid radius
    q = p - hc; return math.sqrt((q.x/HA.x)**2 + (q.y/HA.y)**2 + (q.z/HA.z)**2)

# ---------- suit (coverall + gloves) from body topology ----------
bm = bmesh.new(); bm.from_mesh(bme); bm.transform(M)
IDX = bm.verts.layers.int.new('oidx')
for v in bm.verts: v[IDX] = v.index
bm.verts.ensure_lookup_table(); bm.normal_update()
def handw(i): return sum(x for k, x in bw[i].items() if k in HAND)
off = []
for v in bm.verts:
    ax_ = abs(v.co.x)
    if ax_ > 0.675:   o_ = 0.007          # glove
    elif ax_ > 0.66:  o_ = 0.016          # gauntlet lip
    elif ax_ > 0.635: o_ = 0.015          # tape band
    else:             o_ = 0.012          # baggy suit
    # wrinkles at elbows / armpits / waist
    if 0.32 < ax_ < 0.50: o_ += 0.0035*math.sin(ax_*2*math.pi/0.03)**2
    if 0.12 < ax_ < 0.20: o_ += 0.0025*math.sin(ax_*2*math.pi/0.025)**2
    if ax_ < 0.15 and -0.20 < v.co.z < -0.12: o_ += 0.003*math.sin(v.co.z*2*math.pi/0.028)**2
    off.append(o_)
disp = [v.normal * off[i] for i, v in enumerate(bm.verts)]
for _ in range(4):  # smooth the displacement field, not the shape
    nd = []
    for v in bm.verts:
        nb = [e.other_vert(v) for e in v.link_edges]
        a = sum((disp[n.index] for n in nb), Vector())/max(len(nb), 1)
        nd.append(disp[v.index]*0.5 + a*0.5)
    disp = nd
for v in bm.verts: v.co += disp[v.index]
# drop faces fully hidden inside the hood
kill = [f for f in bm.faces if any(bw[v[IDX]].get('Head',0) > 0.5 for v in f.verts) or all(ell(v.co) < 0.93 for v in f.verts)]
bmesh.ops.delete(bm, geom=kill, context='FACES')
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
for f in bm.faces:
    c = f.calc_center_median(); a = abs(c.x)
    if a > 0.66: f.material_index = 1
    elif a > 0.635: f.material_index = 2
suit_w = [bw[v[IDX]] for v in bm.verts]
suit = new_obj('Hazmat_Suit', bm, [M_SUIT, M_RUB, M_TAPE, M_ZIP])
finish(suit, suit_w)

# BVH of the suit for conforming other parts
sbm = bmesh.new(); sbm.from_mesh(suit.data); sbm.transform(suit.matrix_world)
suit_bvh = BVHTree.FromBMesh(sbm)
suit_kd = KDTree(len(sbm.verts))
for v in sbm.verts: suit_kd.insert(v.co, v.index)
suit_kd.balance()
def conform(p, gap=0.004):
    loc, n, _, _ = suit_bvh.find_nearest(p)
    if loc is None: return p
    d = (p - loc).dot(n)
    return loc + n*gap + (p - loc - n*d) if d < gap else p

# ---------- hood ----------
# lat-long sphere whose pole points at the face, so the visor opening is a clean ring
FWD = Vector((0, -1, 0.06)).normalized()
SIDE = Vector((1, 0, 0)); UP = FWD.cross(SIDE).normalized()
if UP.z < 0: UP = -UP
NU, NV = 72, 44
TH0 = 0.36   # ring index fraction where the visor ends
def win_angle(phi):  # elliptical visor half-angle: wide, short
    a_, b_ = 0.78, 0.44
    return 1/math.sqrt((math.cos(phi)/a_)**2 + (math.sin(phi)/b_)**2)
hb = bmesh.new(); grid = []
K_GLASS, K_RIM = 12, 15
for k in range(1, NV):
    row = []
    for j in range(NU):
        phi = 2*math.pi*j/NU
        tw = win_angle(phi); tr = tw + 0.10
        if k <= K_GLASS: th = tw*k/K_GLASS
        elif k <= K_RIM: th = tw + (tr-tw)*(k-K_GLASS)/(K_RIM-K_GLASS)
        else: th = tr + (math.pi-tr)*(k-K_RIM)/(NV-K_RIM)
        d = FWD*math.cos(th) + (SIDE*math.cos(phi) + UP*math.sin(phi))*math.sin(th)
        row.append(hb.verts.new(d))
    grid.append(row)
front = hb.verts.new(FWD); back = hb.verts.new(-FWD)
faces_k = []
for j in range(NU):
    faces_k.append((0, hb.faces.new([front, grid[0][j], grid[0][(j+1)%NU]][::-1])))
for k in range(NV-2):
    for j in range(NU):
        faces_k.append((k+1, hb.faces.new([grid[k][j], grid[k+1][j], grid[k+1][(j+1)%NU], grid[k][(j+1)%NU]][::-1])))
for j in range(NU):
    faces_k.append((NV, hb.faces.new([back, grid[NV-2][(j+1)%NU], grid[NV-2][j]][::-1])))
glass_faces = [f for k, f in faces_k if k < K_GLASS]
for k, f in faces_k:
    if K_GLASS <= k < K_RIM: f.material_index = 1
vis = bmesh.new(); vmap = {}
for f in glass_faces:
    vs = []
    for v in f.verts:
        if v not in vmap: vmap[v] = vis.verts.new(v.co.copy())
        vs.append(vmap[v])
    vis.faces.new(vs)
bmesh.ops.delete(hb, geom=glass_faces, context='FACES')
rimv = {grid[k][j] for k in range(K_GLASS-1, K_RIM) for j in range(NU)}
for v in hb.verts:
    n = v.co.copy()
    bump = 1.03 if v in rimv else 1.0
    v.co = hc + Vector((n.x*HA.x, n.y*HA.y, n.z*HA.z))*bump
# the lower part of the hood drapes over neck & shoulders
for _ in range(3):
    for v in hb.verts:
        if v.co.z < hc.z - 0.15*HA.z and v not in rimv: v.co = conform(v.co, 0.006)
    for v in hb.verts:   # relax draped area
        if v.co.z < hc.z - 0.15*HA.z and v not in rimv and not v.is_boundary:
            nb = [e.other_vert(v).co for e in v.link_edges]
            v.co = v.co*0.5 + sum(nb, Vector())/len(nb)*0.5
for v in hb.verts:
    if v.co.z < hc.z: v.co = conform(v.co, 0.006)
hb.normal_update()
hood = new_obj('Hazmat_Hood', hb, [M_SUIT, M_RUB])
md = hood.modifiers.new('Thickness', 'SOLIDIFY'); md.thickness = 0.003; md.offset = -1
hood_w = []
for v in hood.data.vertices:
    p = hood.matrix_world @ v.co
    _, idx, _ = suit_kd.find(p)
    t = max(0.0, min(1.0, (p.z - 0.05)/0.08))
    w = {k: x*(1-t) for k, x in suit_w[idx].items()}
    w['Head'] = w.get('Head', 0) + t
    hood_w.append(w)
finish(hood, hood_w)
for v in vis.verts:
    n = v.co.copy(); v.co = hc + Vector((n.x*HA.x, n.y*HA.y, n.z*HA.z))*1.012
vis.normal_update()
visor = new_obj('Hazmat_Visor', vis, [M_GLASS])
finish(visor, [{'Head': 1.0}]*len(visor.data.vertices))

# ---------- conforming straps: rubber belt + chest zipper ----------
def strip(rows):
    """rows: list of lists of surface points -> quad strip bmesh"""
    sb = bmesh.new(); vv = [[sb.verts.new(p) for p in r] for r in rows]
    for i in range(len(vv)-1):
        for j in range(len(vv[i])-1):
            sb.faces.new([vv[i][j], vv[i][j+1], vv[i+1][j+1], vv[i+1][j]])
    return sb
def hit_toward(origin, target, lift):
    d = (target-origin).normalized()
    h, n, _, _ = suit_bvh.ray_cast(origin, d)
    return h + n*lift if h else None
YC = 0.013
belt_rows = []
for zz in (-0.200, -0.190, -0.180, -0.170):
    lift = 0.0045 if zz in (-0.19, -0.18) else 0.0025
    r = []
    for j in range(97):
        ph = 2*math.pi*j/96
        o_ = Vector((0.6*math.cos(ph), YC + 0.6*math.sin(ph), zz))
        r.append(hit_toward(o_, Vector((0, YC, zz)), lift))
    belt_rows.append(r)
belt = new_obj('Hazmat_Belt', strip(belt_rows), [M_RUB])
zip_rows = []
for i in range(40):
    zz = -0.168 + (0.03+0.168)*i/39
    zip_rows.append([hit_toward(Vector((xx, -0.6, zz)), Vector((xx, 0.5, zz)), l) for xx, l in ((-0.008, 0.0015), (-0.003, 0.003), (0.003, 0.003), (0.008, 0.0015))])
zipper = new_obj('Hazmat_Zipper', strip(list(zip(*zip_rows))), [M_ZIP])
for o in (belt, zipper):
    finish(o, [suit_w[suit_kd.find(o.matrix_world @ v.co)[1]] for v in o.data.vertices])

# ---------- respirator (mask + canister) ----------
rdir = Vector((0, -0.70, -0.71)).normalized()
rp = hc + Vector((rdir.x*HA.x, rdir.y*HA.y, rdir.z*HA.z))
rn = Vector((rdir.x/HA.x, rdir.y/HA.y, rdir.z/HA.z)).normalized()  # ellipsoid normal
rot = rn.to_track_quat('Z', 'Y').to_matrix().to_4x4()
rb = bmesh.new()
s = HA.x/0.19
bmesh.ops.create_cone(rb, cap_ends=True, segments=32, radius1=0.050*s, radius2=0.040*s, depth=0.022*s,
                      matrix=Matrix.Translation((0, 0, 0.004*s)))               # rubber mask body
n0 = len(rb.faces)
bmesh.ops.create_cone(rb, cap_ends=True, segments=32, radius1=0.036*s, radius2=0.036*s, depth=0.040*s,
                      matrix=Matrix.Translation((0, 0, 0.035*s)))               # filter canister
for i in range(3):                                                              # canister ribs
    bmesh.ops.create_cone(rb, cap_ends=False, segments=32, radius1=0.039*s, radius2=0.039*s, depth=0.004*s,
                          matrix=Matrix.Translation((0, 0, (0.022+0.012*i)*s)))
rb.faces.ensure_lookup_table()
for f in rb.faces[n0:]: f.material_index = 1
rb.transform(Matrix.Translation(rp) @ rot)
resp = new_obj('Hazmat_Respirator', rb, [M_RUB, M_FILT])
resp.data.set_sharp_from_angle(angle=0.6)
finish(resp, [{'Head': 1.0}]*len(resp.data.vertices))

# ---------- radiation trefoil decal on the chest ----------
db = bmesh.new()
def fan(r0, r1, a0, a1, seg=10):
    pts = [(r1*math.cos(a0+(a1-a0)*k/seg), r1*math.sin(a0+(a1-a0)*k/seg)) for k in range(seg+1)]
    pts += [(r0*math.cos(a1-(a1-a0)*k/seg), r0*math.sin(a1-(a1-a0)*k/seg)) for k in range(seg+1)]
    db.faces.new([db.verts.new((x, y, 0)) for x, y in pts])
for k in range(3):
    a = math.radians(90 + 120*k)
    fan(0.30, 1.0, a-math.radians(30), a+math.radians(30))
c = [db.verts.new((0.2*math.cos(t*2*math.pi/16), 0.2*math.sin(t*2*math.pi/16), 0)) for t in range(16)]
db.faces.new(c)
ring_o = [(1.25*math.cos(t*2*math.pi/48), 1.25*math.sin(t*2*math.pi/48)) for t in range(48)]
ring_i = [(1.12*math.cos(t*2*math.pi/48), 1.12*math.sin(t*2*math.pi/48)) for t in range(48)]
ro = [db.verts.new((x, y, 0)) for x, y in ring_o]; ri = [db.verts.new((x, y, 0)) for x, y in ring_i]
for t in range(48): db.faces.new([ro[t], ro[(t+1)%48], ri[(t+1)%48], ri[t]])
bmesh.ops.subdivide_edges(db, edges=db.edges[:], cuts=2, use_grid_fill=True)
size = 0.030
dc = Vector((0.062, -1.0, -0.055))
for v in db.verts:
    x, y = v.co.x*size, v.co.y*size
    origin = Vector((dc.x + x, -0.6, dc.z + y))
    hit, n, _, _ = suit_bvh.ray_cast(origin, Vector((0, 1, 0)))
    v.co = hit + n*0.0012 if hit else origin
db.normal_update()
for f in db.faces:
    if f.normal.y > 0: f.normal_flip()
decal = new_obj('Hazmat_Decal', db, [M_DECAL])
finish(decal, [suit_w[suit_kd.find(decal.matrix_world @ v.co)[1]] for v in decal.data.vertices])

parts = [suit, hood, visor, resp, belt, zipper, decal]
arm.data.pose_position = orig_pose
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=bpy.path.abspath("//PeeperLeeper_Hazmat.blend"), copy=True)

def export(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)
export(bpy.path.abspath("//HazmatSuit.fbx"), [arm] + parts)
export(bpy.path.abspath("//PeeperLeeper_WithHazmat.fbx"), [arm, body, bpy.data.objects['Cube.001']] + parts)
print("DONE", [(o.name, len(o.data.vertices)) for o in parts])
