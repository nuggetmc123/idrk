"""Procedural clothing builder for the PeeperLeeper player model.

Every garment starts from a copy of the player's own body surface, so it
fits by construction: the body is trimmed to the garment's region with
cutting planes, pushed outward by a gap, given real thickness, and then
decorated with pockets, belts, collars, plates and so on. Skin weights are
transferred from the nearest point on the body, so the clothes follow the
player's 29-bone rig exactly.

All geometry is built in the armature's local space with the rig in its
rest (T) pose. In that space:  +X = character's left, -Y = front, +Z = up,
torso runs z 0.0 (bottom) .. ~0.95 (neck), arms lie along X at z ~0.76.

Run with Blender's Python module (pip install bpy); see build_all.py.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree

# ---------------------------------------------------------------- constants
NECK_Z = 0.925      # crew neckline height
WAIST_Z = 0.38      # top of the bottoms' waistband
HEM_Z = 0.27        # default shirt hem (overlaps the waistband by ~0.1)
LEG_X = 0.12        # leg-tube centre offset from the midline
LEG_Y = 0.045
UPPERARM = (Vector((0.327, 0.028, 0.800)), Vector((1.038, 0.094, 0.761)))
FOREARM = (Vector((1.038, 0.094, 0.761)), Vector((1.909, 0.030, 0.758)))

EXCLUDE_ALWAYS = {'Head', 'Eye.L', 'Eye.R'} | {
    f'{b}.{s}' for s in 'LR' for b in (
        'Hand', 'Pinky1', 'Pinky2', 'Pinky3', 'Pointer1', 'Pointer2',
        'Pointer3', 'Thumb1', 'Thumb2')}
TOP_GROUPS = {'Hips', 'Spine', 'Chest', 'Neck', 'Shoulder.L', 'Shoulder.R',
              'Upperarm.L', 'Upperarm.R'}
LONG_TOP_GROUPS = TOP_GROUPS | {'Forearm.L', 'Forearm.R'}
BOTTOM_GROUPS = {'Hips', 'Spine'}


def V(*a):
    return Vector(a)


def arm_point(bone, t, side=1):
    """Point at parameter t along an arm bone, mirrored for side=-1."""
    h, tl = bone
    p = h.lerp(tl, t)
    d = (tl - h).normalized()
    return V(p.x * side, p.y, p.z), V(d.x * side, d.y, d.z)


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- scene/body
class Ctx:
    """The freshly imported player plus the body data every garment needs."""

    def __init__(self, src):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=src)
        self.arm = bpy.data.objects['Armature']
        self.body = bpy.data.objects['Retopo_Cube.001']
        self.eyes = bpy.data.objects['Cube.001']
        self.names = [g.name for g in self.body.vertex_groups]
        bm = bmesh.new()
        bm.from_mesh(self.body.data)
        bm.transform(self.arm.matrix_world.inverted() @ self.body.matrix_world)
        bm.normal_update()
        bm.faces.ensure_lookup_table()
        bm.verts.ensure_lookup_table()
        self.bm = bm
        self.deform = bm.verts.layers.deform.active
        self.bvh = BVHTree.FromBMesh(bm)
        self.vweights = [dict(v[self.deform]) for v in bm.verts]


# ---------------------------------------------------------------- textures
def _grid(n):
    x = (np.arange(n) + 0.5) / n
    return np.meshgrid(x, x)          # X varies along columns, Y along rows


def fftnoise(n, cutoff, rng):
    """Tileable smooth noise in 0..1; cutoff = features per tile."""
    w = rng.standard_normal((n, n))
    f = np.fft.fftfreq(n) * n
    r = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    a = np.real(np.fft.ifft2(np.fft.fft2(w) * np.exp(-(r / cutoff) ** 2)))
    return (a - a.min()) / (a.max() - a.min() + 1e-9)


def _c(hexstr):
    h = hexstr.lstrip('#')
    srgb = np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])
    return srgb


def make_texture(kind, colors, seed=0, n=512, grime=0.3, wear=1.0):
    """Return an (n, n, 3) sRGB float array for a fabric pattern."""
    rng = np.random.default_rng(seed)
    X, Y = _grid(n)
    C = [_c(c) for c in colors]
    grain = fftnoise(n, 90, rng)
    blotch = fftnoise(n, 4, rng)
    if kind == 'fabric':
        f = 96
        w = (np.abs(np.sin(np.pi * X * f)) * np.abs(np.sin(np.pi * Y * f))) ** 0.5
        img = C[0] * (0.86 + 0.14 * w)[..., None]
    elif kind == 'rib':
        f = 64
        w = np.abs(np.sin(np.pi * X * f)) ** 0.7
        img = C[0] * (0.78 + 0.22 * w)[..., None]
    elif kind == 'burlap':
        f = 28
        cx = (np.floor(X * f) + np.floor(Y * f)) % 2
        tx = np.abs(np.sin(np.pi * Y * f)) ** 0.5
        ty = np.abs(np.sin(np.pi * X * f)) ** 0.5
        th = np.where(cx > 0, tx, ty)
        fiber = fftnoise(n, 160, rng)
        img = C[0] * (0.35 + 0.65 * th * (0.75 + 0.5 * fiber))[..., None]
        img = img * (0.9 + 0.2 * fftnoise(n, 20, rng))[..., None]
    elif kind == 'denim':
        d = 0.5 + 0.5 * np.sin(2 * np.pi * (X * 120 + Y * 120))
        fade = fftnoise(n, 6, rng)
        img = (C[0] * (1 - 0.35 * d)[..., None] + C[1] * (0.35 * d)[..., None])
        img = img * (0.8 + 0.4 * fade)[..., None]
    elif kind == 'camo':
        img = np.broadcast_to(C[0], (n, n, 3)).copy()
        for i, c in enumerate(C[1:]):
            m = fftnoise(n, 5 + i, rng) > 0.56 + 0.03 * i
            img[m] = c
        w = (np.abs(np.sin(np.pi * X * 96)) * np.abs(np.sin(np.pi * Y * 96))) ** 0.5
        img = img * (0.88 + 0.12 * w)[..., None]
    elif kind == 'plaid':
        def bands(t):
            t = (t * 3) % 1.0
            idx = np.zeros_like(t, dtype=int)
            idx[(t > 0.42) & (t < 0.70)] = 1
            idx[(t > 0.78) & (t < 0.82)] = 2
            return idx
        bx, by = bands(X), bands(Y)
        cols = np.array(C)
        img = 0.5 * (cols[bx] + cols[by])
        tw = 0.5 + 0.5 * np.sin(2 * np.pi * (X * 160 - Y * 160))
        img = img * (0.88 + 0.12 * tw)[..., None]
    elif kind == 'waffle':
        f = 40
        gx = np.abs(((X * f) % 1) - 0.5) * 2
        gy = np.abs(((Y * f) % 1) - 0.5) * 2
        cell = 1 - np.maximum(gx, gy) ** 6
        img = C[0] * (0.72 + 0.28 * cell)[..., None]
    elif kind == 'leather':
        cr = fftnoise(n, 60, rng)
        img = C[0] * (0.75 + 0.35 * cr)[..., None]
        grime *= 1.3
    elif kind == 'metal':
        img = np.broadcast_to(C[0], (n, n, 3)).copy()
        img = img * (0.85 + 0.25 * fftnoise(n, 30, rng))[..., None]
        for _ in range(60):                       # scratches
            y0, x0 = rng.integers(0, n, 2)
            ln = rng.integers(20, 120)
            ang = rng.uniform(0, np.pi)
            for k in range(ln):
                yy = int(y0 + k * np.sin(ang)) % n
                xx = int(x0 + k * np.cos(ang)) % n
                img[yy, xx] = np.minimum(img[yy, xx] * 1.35, 1)
        rust = fftnoise(n, 7, rng)
        m = np.clip((rust - 0.55) * 4, 0, 1)[..., None]
        img = img * (1 - m) + _c('#7a3b16') * m * (0.7 + 0.5 * grain[..., None])
    elif kind == 'rope':
        d = np.abs(np.sin(np.pi * (X * 24 + Y * 24)))
        img = C[0] * (0.55 + 0.45 * d)[..., None]
    else:                                          # flat
        img = np.broadcast_to(C[0], (n, n, 3)).copy()
    img = img * (0.9 + 0.2 * grain)[..., None]
    img = img * (1 - grime * blotch ** 2)[..., None]
    if wear and kind not in ('flat', 'metal'):
        # sun-bleached fading toward a dusty grey-beige
        fade = fftnoise(n, 3, rng)[..., None] * 0.45 * wear
        dust = _c('#8a8170')
        img = img * (1 - fade) + (img.mean(axis=2, keepdims=True) * 0.5 + dust * 0.5) * fade
        # dark oily/mud stains with hard-ish edges
        st = fftnoise(n, 6, rng)
        m = np.clip((st - 0.7) * 6, 0, 1)[..., None] * 0.45 * wear
        img = img * (1 - m) + _c('#2e2418') * m
        # rusty-brown splotches
        st2 = fftnoise(n, 14, rng)
        m2 = np.clip((st2 - 0.78) * 8, 0, 1)[..., None] * 0.35 * wear
        img = img * (1 - m2) + _c('#5a2f17') * m2
        # scuffs: fine light streaks
        sc = fftnoise(n, 120, rng)
        img = img * (1 + 0.18 * wear * np.clip((sc - 0.72) * 6, 0, 1))[..., None]
    return np.clip(img, 0, 1)


def save_texture(img, path):
    n = img.shape[0]
    im = bpy.data.images.new(os.path.basename(path), n, n, alpha=False)
    rgba = np.concatenate([img[::-1], np.ones((n, n, 1))], axis=2)
    im.pixels.foreach_set(rgba.astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = 'JPEG'
    bpy.context.scene.render.image_settings.quality = 88
    im.save(quality=88)
    return im


class Mat:
    """One material slot of a garment."""

    def __init__(self, name, kind, colors, tile=0.6, raise_=0.0, rough=0.9,
                 metal=0.0, seed=0, grime=0.38, res=512, stitch=None, wear=1.0):
        self.name, self.kind, self.colors = name, kind, colors
        self.tile, self.raise_, self.rough, self.metal = tile, raise_, rough, metal
        self.seed, self.grime, self.res, self.wear = seed, grime, res, wear
        self.stitch = (kind in ('fabric', 'burlap', 'denim', 'camo', 'plaid', 'waffle',
                                'rib', 'leather')) if stitch is None else stitch

    def build(self, tex_dir, prefix):
        m = bpy.data.materials.new(f'{prefix}_{self.name}')
        nt = m.node_tree
        bsdf = nt.nodes.get('Principled BSDF')
        bsdf.inputs['Roughness'].default_value = self.rough
        bsdf.inputs['Metallic'].default_value = self.metal
        base = _c(self.colors[0])
        lin = [(c / 12.92) if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in base]
        bsdf.inputs['Base Color'].default_value = (*lin, 1)
        m.diffuse_color = (*lin, 1)
        if self.kind != 'flat':
            path = os.path.join(tex_dir, f'{prefix}_{self.name}.jpg')
            img = save_texture(make_texture(self.kind, self.colors, self.seed,
                                            n=self.res, grime=self.grime,
                                            wear=self.wear), path)
            tn = nt.nodes.new('ShaderNodeTexImage')
            tn.image = img
            nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
        return m


# ---------------------------------------------------------------- shells
class Cut:
    """Remove everything on the +normal side of a plane (optionally only
    where region(p) is true). ragged > 0 tears the edge irregularly."""

    def __init__(self, co, no, region=None, ragged=0.0, name=None):
        self.co, self.no = Vector(co), Vector(no).normalized()
        self.region, self.ragged, self.name = region, ragged, name

    def d(self, p):
        return (p - self.co).dot(self.no)


class Region:
    """Faces whose centre satisfies pred get material index mat. planes are
    extra slicing planes (co, no) so the region gets clean straight edges."""

    def __init__(self, mat, pred, planes=()):
        self.mat, self.pred, self.planes = mat, pred, list(planes)


def box_region(mat, x=None, y=None, z=None, mirror=False):
    """Axis-aligned box region with clean edges (x range mirrored if asked)."""
    planes = []
    for axis, rng_ in ((0, x), (1, y), (2, z)):
        if rng_ is None:
            continue
        for val in rng_:
            n = [0, 0, 0]
            n[axis] = 1
            co = [0, 0, 0]
            co[axis] = val
            planes.append((V(*co), V(*n)))
            if mirror and axis == 0:
                co[0] = -val
                planes.append((V(*co), V(*n)))

    def pred(p, x=x, y=y, z=z):
        px = abs(p.x) if mirror else p.x
        return ((x is None or x[0] <= px <= x[1]) and
                (y is None or y[0] <= p.y <= y[1]) and
                (z is None or z[0] <= p.z <= z[1]))
    return Region(mat, pred, planes)


def trim_band(mat, cut, width):
    """Band of material just inside a cut edge (hems, cuffs, collars)."""
    co2 = cut.co - cut.no * width
    return Region(mat, lambda p, c=cut: -width <= c.d(p) <= 0.0,
                  [(co2, cut.no)])


def hole(center, radius, seed=0):
    """A torn hole: deletes faces around center with a ragged outline."""
    c = Vector(center)
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))

    def pred(p):
        r = radius * (0.65 + 0.7 * (noise.noise(p * 18 + off) * 0.5 + 0.5))
        return (p - c).length < r
    return pred


def _islands(bm, min_faces=25):
    seen, kill = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        comp, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        if len(comp) < min_faces:
            kill += comp
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='FACES')


def _bisect(bm, co, no):
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                           dist=1e-6, plane_co=co, plane_no=no)


def _solidify(bm, thickness):
    me = bpy.data.meshes.new('tmp_solid')
    bm.to_mesh(me)
    ob = bpy.data.objects.new('tmp_solid', me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new('s', 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_rim = True
    mod.use_even_offset = False
    mod.thickness_clamp = 1.0     # stops spikes at non-manifold corners
    dg = bpy.context.evaluated_depsgraph_get()
    out = bmesh.new()
    out.from_object(ob, dg)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return out


class Shell:
    """A garment layer cut from the body surface."""

    def __init__(self, ctx, keep, cuts=(), regions=(), holes=(), gap=0.03,
                 gap_arm=None, min_gap=None, min_gap_arm=None, thick=0.012,
                 smooth=0, wrinkle=0.01, wrinkle_freq=8.0, sleeve_folds=0.0,
                 seed=0, mats=None):
        self.loops = {}
        rng = random.Random(seed)
        bm = ctx.bm.copy()
        bm.faces.ensure_lookup_table()
        dl = bm.verts.layers.deform.active
        names = ctx.names
        keep = set(keep) - EXCLUDE_ALWAYS

        def dom(face):
            acc = {}
            for v in face.verts:
                for gi, w in v[dl].items():
                    acc[gi] = acc.get(gi, 0) + w
            return names[max(acc, key=acc.get)] if acc else None
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if dom(f) not in keep],
                         context='FACES')
        bm.verts.layers.deform.remove(dl)

        # trim with planes, tearing ragged ones
        for c in cuts:
            _bisect(bm, c.co, c.no)
            kill = []
            for f in bm.faces:
                p = f.calc_center_median()
                if c.region is not None and not c.region(p):
                    continue
                lim = 0.0
                if c.ragged:
                    n1 = noise.noise(p * 22 + V(seed, 0, 0)) * 0.5 + 0.5
                    lim = -c.ragged * (n1 ** 1.5) * (0.5 + rng.random())
                if c.d(p) > lim:
                    kill.append(f)
            bmesh.ops.delete(bm, geom=kill, context='FACES')
        for h in holes:
            bmesh.ops.delete(bm, geom=[f for f in bm.faces
                                       if h(f.calc_center_median())],
                             context='FACES')
        _islands(bm)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces],
                         context='VERTS')

        # remember clean cut edges (for collars, hoods, drawstrings)
        named = [c for c in cuts if c.name]
        loop_verts = {c.name: [v for v in bm.verts if v.is_boundary and
                               abs(c.d(v.co)) < 1e-4] for c in named}

        # material regions
        for r in regions:
            for co, no in r.planes:
                _bisect(bm, co, no)
        for f in bm.faces:
            f.material_index = 0
            p = f.calc_center_median()
            for r in regions:
                if hasattr(r, 'which'):
                    mi = r.which(p)
                    if mi is not None:
                        f.material_index = mi
                elif r.pred(p):
                    f.material_index = r.mat
            f.smooth = True
        # torn edges to hang fraying threads from
        fray_src = []
        for v in bm.verts:
            if not v.is_boundary:
                continue
            for c in cuts:
                if c.ragged and -c.ragged - 0.01 <= c.d(v.co) <= 1e-4:
                    fray_src.append((v, c.no.copy()))
                    break
        if named:   # re-find loop verts (slicing may have added some)
            loop_verts = {c.name: [v for v in bm.verts if v.is_boundary and
                                   abs(c.d(v.co)) < 1e-4] for c in named}

        # optional relaxing (baggier, less body detail)
        for _ in range(smooth):
            new = {}
            for v in bm.verts:
                if v.is_boundary:
                    continue
                nb = [e.other_vert(v).co for e in v.link_edges]
                avg = sum(nb, Vector()) / len(nb)
                new[v] = v.co.lerp(avg, 0.5)
            for v, co in new.items():
                v.co = co

        # push outward
        bm.normal_update()
        gap_arm = gap * 0.55 if gap_arm is None else gap_arm
        min_gap = gap * 0.8 if min_gap is None else min_gap
        min_gap_arm = gap_arm * 0.8 if min_gap_arm is None else min_gap_arm
        raise_by_mat = [m.raise_ for m in mats] if mats else []
        off = V(seed * 1.3, seed * 0.7, 0)
        moves = []
        for v in bm.verts:
            a = smoothstep(0.33, 0.48, abs(v.co.x))
            g = gap + (gap_arm - gap) * a
            w = noise.noise(v.co * wrinkle_freq + off) * wrinkle
            if sleeve_folds and a > 0.5:
                w += sleeve_folds * math.sin(abs(v.co.x) * 38 + noise.noise(v.co * 4) * 2)
            if raise_by_mat:
                g += max(raise_by_mat[f.material_index] for f in v.link_faces)
            moves.append((v, v.co + v.normal * (g + w), a))
        for v, co, a in moves:
            v.co = co
        for v, co, a in moves:
            loc, nrm, _, _ = ctx.bvh.find_nearest(v.co)
            mg = min_gap + (min_gap_arm - min_gap) * a
            d = (v.co - loc).dot(nrm)
            if d < mg:
                v.co = v.co + nrm * (mg - d)
        bm.normal_update()
        for name, vs in loop_verts.items():
            self.loops[name] = [(v.co.copy(), v.normal.copy()) for v in vs]
        self.fray = [(v.co + v.normal * thick * 0.5, d) for v, d in fray_src]
        self.thick = thick
        self.bm = _solidify(bm, thick)
        bm.free()


# ---------------------------------------------------------------- primitives
def _faces_of(verts):
    return {f for v in verts for f in v.link_faces}


def _set(faces, mat, smooth=True):
    for f in faces:
        f.material_index = mat
        f.smooth = smooth


def _frame(normal, up=V(0, 0, 1)):
    z = Vector(normal).normalized()
    upv = Vector(up)
    if abs(z.dot(upv.normalized())) > 0.95:
        upv = V(0, 1, 0) if abs(z.z) > 0.9 else V(0, 0, 1)
    x = upv.cross(z).normalized()
    y = z.cross(x).normalized()
    return Matrix((x, y, z)).transposed()


def box(bm, center, normal, size, mat, up=V(0, 0, 1), smooth=False):
    R = _frame(normal, up).to_4x4()
    M = Matrix.Translation(center) @ R @ Matrix.Diagonal((*size, 1))
    vs = bmesh.ops.create_cube(bm, size=1.0, matrix=M)['verts']
    _set(_faces_of(vs), mat, smooth)


def ellipsoid(bm, center, normal, size, mat, up=V(0, 0, 1)):
    R = _frame(normal, up).to_4x4()
    M = Matrix.Translation(center) @ R @ Matrix.Diagonal((*size, 1))
    vs = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8,
                                   radius=1.0, matrix=M)['verts']
    _set(_faces_of(vs), mat)


def disc(bm, center, normal, radius, depth, mat, segs=12):
    R = _frame(normal).to_4x4()
    M = Matrix.Translation(center) @ R
    vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False,
                               segments=segs, radius1=radius, radius2=radius,
                               depth=depth, matrix=M)['verts']
    _set(_faces_of(vs), mat)


def sweep(bm, pts, radius, mat, closed=True, sides=8, flat=1.0):
    """Tube along a polyline. radius: float or per-point list."""
    n = len(pts)
    if n < 2:
        return
    rads = radius if isinstance(radius, (list, tuple)) else [radius] * n
    rings = []
    for i, p in enumerate(pts):
        a = pts[(i - 1) % n] if (closed or i > 0) else p
        b = pts[(i + 1) % n] if (closed or i < n - 1) else p
        t = (b - a).normalized()
        ref = V(0, 0, 1) if abs(t.z) < 0.9 else V(1, 0, 0)
        u = t.cross(ref).normalized()
        w = t.cross(u).normalized()
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(p + (u * math.cos(ang) + w * math.sin(ang) * flat) * rads[i]))
        rings.append(ring)
    faces = []
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(sides):
            k1 = (k + 1) % sides
            faces.append(bm.faces.new((r0[k], r0[k1], r1[k1], r1[k])))
    if not closed:
        faces.append(bm.faces.new(rings[0][::-1]))
        faces.append(bm.faces.new(rings[-1]))
    _set(faces, mat)


def ring_on(bvh, center, axis=V(0, 0, 1), n=40, far=0.8):
    """Points around a surface, found by casting rays inward to an axis."""
    axis = Vector(axis).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    out = []
    for i in range(n):
        ang = 2 * math.pi * i / n
        d = u * math.cos(ang) + w * math.sin(ang)
        hit = bvh.ray_cast(Vector(center) + d * far, -d, far * 1.5)
        if hit[0] is not None:
            out.append((hit[0], hit[1]))
    return out


def surf(bvh, origin, direction):
    """First surface hit (point, normal) along a ray, or None."""
    hit = bvh.ray_cast(Vector(origin), Vector(direction).normalized(), 5.0)
    return (hit[0], hit[1]) if hit[0] is not None else None


def ordered_loop(loop, center=V(0, LEG_Y, 0)):
    """Sort boundary points by angle around the vertical axis (front first)."""
    def ang(item):
        p = item[0]
        return math.atan2(p.x - center.x, -(p.y - center.y))
    return sorted(loop, key=ang)


def leg_tube(bm, side, z_top, z_bot, r_top, r_bot, mat, cap_mat, segs=20,
             rings=12, wobble=0.006, ragged=0.0, flare=0.0, blouse=0.0,
             stripe_mat=None, seed=0, panels=None):
    """A trouser leg hanging below the legless body (top end hides inside)."""
    rng = random.Random(seed * 7 + (side > 0))
    cx = LEG_X * side
    ring_vs = []
    zs = [z_top + (z_bot - z_top) * i / (rings - 1) for i in range(rings)]
    for i, z in enumerate(zs):
        t = i / (rings - 1)
        r = r_top + (r_bot - r_top) * t + flare * t ** 2
        if blouse:
            r += blouse * math.exp(-((t - 0.82) / 0.1) ** 2) - blouse * 0.6 * (t > 0.93)
        ring = []
        for k in range(segs):
            ang = 2 * math.pi * k / segs
            p = V(cx + math.cos(ang) * r, LEG_Y + math.sin(ang) * r, z)
            rr = r + noise.noise(p * 9 + V(seed, side, 0)) * wobble
            zz = z
            if ragged and i == rings - 1:
                zz += rng.uniform(0, ragged)
            ring.append(bm.verts.new(V(cx + math.cos(ang) * rr,
                                       LEG_Y + math.sin(ang) * rr, zz)))
        ring_vs.append(ring)
    faces = []
    for i in range(rings - 1):
        for k in range(segs):
            k1 = (k + 1) % segs
            f = bm.faces.new((ring_vs[i][k], ring_vs[i][k1],
                              ring_vs[i + 1][k1], ring_vs[i + 1][k]))
            ang = 2 * math.pi * (k + 0.5) / segs
            outer = math.cos(ang) * side
            f.material_index = stripe_mat if (stripe_mat is not None and outer > 0.93) else mat
            if panels is not None and f.material_index == mat:
                mi = panels.which(f.calc_center_median())
                if mi is not None:
                    f.material_index = mi
            f.smooth = True
            faces.append(f)
    # dark recessed cap so the leg reads as hollow fabric from below
    zc = z_bot + 0.035
    rc = r_bot * 0.92
    cvs = [bm.verts.new(V(cx + math.cos(2 * math.pi * k / segs) * rc,
                          LEG_Y + math.sin(2 * math.pi * k / segs) * rc, zc))
           for k in range(segs)]
    f = bm.faces.new(cvs)
    f.material_index = cap_mat
    # inner wall from rim up to the cap
    for k in range(segs):
        k1 = (k + 1) % segs
        g = bm.faces.new((ring_vs[-1][k1], ring_vs[-1][k], cvs[k], cvs[k1]))
        g.material_index = cap_mat
    return [(v.co.copy()) for v in ring_vs[-1]]


def leg_point(side, z, r, ang_deg):
    a = math.radians(ang_deg)
    n = V(math.cos(a) * side, math.sin(a), 0)
    return V(LEG_X * side, LEG_Y, z) + n * r, n


# ---------------------------------------------------------------- finishing
def box_uvs(bm, mats):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        tile = mats[f.material_index].tile if f.material_index < len(mats) else 0.5
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for lp in f.loops:
            co = lp.vert.co
            lp[uv].uv = (co[a] / tile, co[b] / tile)


def transfer_weights(ctx, ob):
    """Skin each cloth vertex like the nearest point on the body."""
    for name in ctx.names:
        ob.vertex_groups.new(name=name)
    bmf = ctx.bm.faces
    per_group = {}
    for v in ob.data.vertices:
        loc, nrm, fi, _ = ctx.bvh.find_nearest(v.co)
        face = bmf[fi]
        acc = {}
        tot = 0.0
        for bv in face.verts:
            w = 1.0 / ((bv.co - loc).length + 1e-4)
            tot += w
            for gi, gw in ctx.vweights[bv.index].items():
                acc[gi] = acc.get(gi, 0) + gw * w
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
        s = sum(w for _, w in top) or 1.0
        for gi, w in top:
            per_group.setdefault(gi, []).append((v.index, w / s))
    for gi, items in per_group.items():
        vg = ob.vertex_groups[gi]
        for idx, w in items:
            vg.add([idx], w, 'REPLACE')


def stitch_seams(ctx, bm, mats, thread, spacing=0.032):
    """Cross-stitches along every seam where two scrap materials meet."""
    placed = []
    todo = []
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        f1, f2 = e.link_faces
        m1, m2 = f1.material_index, f2.material_index
        if m1 == m2 or not (mats[m1].stitch and mats[m2].stitch):
            continue
        mid = (e.verts[0].co + e.verts[1].co) / 2
        n = (f1.normal + f2.normal).normalized()
        loc, _, _, _ = ctx.bvh.find_nearest(mid)
        if (mid - loc).dot(n) <= 0:          # inner surface: hidden
            continue
        todo.append((mid, n, (e.verts[1].co - e.verts[0].co).normalized()))
    cell = {}
    for mid, n, d in todo:
        key = tuple(int(c // spacing) for c in mid)
        near = [cell.get((key[0] + i, key[1] + j, key[2] + k))
                for i in (-1, 0, 1) for j in (-1, 0, 1) for k in (-1, 0, 1)]
        if any(q is not None and (q - mid).length < spacing for q in near):
            continue
        cell[key] = mid
        across = d.cross(n).normalized()
        box(bm, mid + n * 0.003, n, (0.006, 0.03, 0.005), thread, up=across)


def finish(ctx, name, bm, mats, tex_dir, max_dist=0.6, thread=None):
    stray = 0
    for v in bm.verts:     # safety net: nothing may float far off the body
        loc, nrm, _, d = ctx.bvh.find_nearest(v.co)
        if d > max_dist:
            v.co = loc + nrm * 0.05
            stray += 1
    if stray:
        print(f'WARNING {name}: pulled in {stray} stray vertices')
    if thread is not None:
        stitch_seams(ctx, bm, mats, thread)
    box_uvs(bm, mats)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m.build(tex_dir, name))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = ctx.arm
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)
    transfer_weights(ctx, ob)
    mod = ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = ctx.arm
    return ob


def merge(dst, src):
    me = bpy.data.meshes.new('tmp_merge')
    src.to_mesh(me)
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)
    src.free()


def export(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, bake_anim=False, path_mode='COPY',
        embed_textures=True, mesh_smooth_type='FACE', use_mesh_modifiers=True)


# ---------------------------------------------------------------- wear & tear
SCRAP_PALETTE = [
    ('burlap', ['#9b7f52']), ('leather', ['#5b3a22']), ('fabric', ['#5d6044']),
    ('denim', ['#3d5576', '#c4ccd8']), ('fabric', ['#6e6a62']), ('fabric', ['#7a4b33']),
    ('burlap', ['#7d6a4a']), ('fabric', ['#3f4648']), ('leather', ['#3b2c20']),
]


def _shade(hexstr, k, mix=None, t=0.0):
    c = _c(hexstr) * k
    if mix is not None:
        c = c * (1 - t) + _c(mix) * t
    c = np.clip(c, 0, 1)
    return '#' + ''.join(f'{int(round(x * 255)):02x}' for x in c)


class Panels:
    """Split a garment into mismatched scrap panels with random planes."""

    def __init__(self, planes, pool, weights, seed):
        self.planes = planes
        self.pool, self.weights, self.seed = pool, weights, seed
        self._cache = {}

    def which(self, p):
        key = tuple((p - co).dot(no) > 0 for co, no in self.planes)
        if key not in self._cache:
            r = random.Random(hash(key) ^ (self.seed * 2654435761))
            self._cache[key] = r.choices(self.pool, self.weights)[0]
        return self._cache[key]


class Wear:
    """Turns a clean garment into a beat-up, scavenged one.

    Appends scrap-fabric materials, a thread material and a wrap material to
    the garment's material list, and hands out panel layouts, random tears,
    fraying threads and cloth/tape wraps."""

    def __init__(self, mats, seed, kind, scraps=3, main_weight=0.45):
        self.mats, self.seed, self.kind = mats, seed, kind
        self.rng = random.Random(seed * 31 + 7)
        main = mats[0]
        base = main.colors[0]
        pool = [0]
        # same cloth, re-dyed darker and bleached lighter
        mats.append(Mat('ScrapDark', main.kind, [_shade(base, 0.68, '#3a2c1c', 0.25)] +
                        main.colors[1:], tile=main.tile, raise_=0.004, seed=seed + 101,
                        res=256))
        mats.append(Mat('ScrapFaded', main.kind, [_shade(base, 1.15, '#a39a86', 0.4)] +
                        main.colors[1:], tile=main.tile, raise_=0.006, seed=seed + 102,
                        res=256))
        pool += [len(mats) - 2, len(mats) - 1]
        for i in range(scraps - 2 if scraps > 2 else 0):
            k, cols = self.rng.choice(SCRAP_PALETTE)
            mats.append(Mat(f'Scrap{i}', k, cols, tile=0.4, raise_=0.005 + 0.002 * i,
                            seed=seed + 110 + i, res=256))
            pool.append(len(mats) - 1)
        self.pool = pool
        rest = (1 - main_weight) / (len(pool) - 1)
        self.weights = [main_weight] + [rest] * (len(pool) - 1)
        mats.append(Mat('Thread', 'flat', ['#1c1712'], stitch=False))
        self.thread = len(mats) - 1
        mats.append(Mat('Wrap', 'fabric', ['#b9ae95'], tile=0.25, seed=seed + 120,
                        res=256, stitch=False))
        self.wrap = len(mats) - 1
        self.panels = self._panels()

    def _panels(self):
        r = self.rng
        planes = []
        if self.kind in ('top', 'long'):
            for _ in range(5):          # torso
                a = r.uniform(0, math.pi)
                no = V(math.cos(a), r.uniform(-0.3, 0.3), math.sin(a))
                planes.append((V(r.uniform(-0.25, 0.25), 0, r.uniform(0.35, 0.85)), no))
            reach = (0.5, 0.95) if self.kind == 'top' else (0.5, 1.7)
            for s in (1, -1):           # sleeves
                for _ in range(2 if self.kind == 'long' else 1):
                    planes.append((V(s * r.uniform(*reach), 0, 0.77),
                                   V(1, r.uniform(-0.6, 0.6), r.uniform(-0.6, 0.6))))
        else:
            for _ in range(5):          # hips + legs
                a = r.uniform(0, math.pi)
                no = V(math.cos(a), r.uniform(-0.3, 0.3), math.sin(a) * 1.4)
                planes.append((V(r.uniform(-0.2, 0.2), 0, r.uniform(-0.3, 0.3)), no))
        return Panels(planes, self.pool, self.weights, self.seed)

    def holes(self, ctx, n=3, z=(0.32, 0.85), r=(0.05, 0.08), elbows=False):
        out = []
        for i in range(n):
            a = self.rng.uniform(0, 2 * math.pi)
            zz = self.rng.uniform(*z)
            d = V(math.cos(a), math.sin(a), 0)
            hit = ctx.bvh.ray_cast(V(0, LEG_Y, zz) + d * 0.9, -d, 1.5)
            if hit[0] is not None and abs(hit[0].x) < 0.33:
                out.append(hole(hit[0], self.rng.uniform(*r), self.seed + i))
        if elbows:
            for s in (1, -1):
                if self.rng.random() < 0.75:
                    co, _ = arm_point(FOREARM, 0.03, s)
                    hit = ctx.bvh.ray_cast(co + V(0, 1, 0), V(0, -1, 0), 2)
                    if hit[0] is not None:
                        out.append(hole(hit[0], 0.065, self.seed + 9 + s))
        return out

    def fray(self, bm, src, mat=0, every=2, length=(0.015, 0.05)):
        """Loose threads hanging off torn edges. src: [(point, outward dir)]."""
        r = self.rng
        for i, (p, d) in enumerate(src):
            if i % every or r.random() < 0.35:
                continue
            ln = r.uniform(*length)
            dirn = (d + V(r.uniform(-.4, .4), r.uniform(-.4, .4), r.uniform(-.4, .1))
                    + V(0, 0, -0.5)).normalized()
            mid = p + dirn * ln * 0.5 + V(r.uniform(-.004, .004), r.uniform(-.004, .004), 0)
            sweep(bm, [p, mid, p + dirn * ln + V(0, 0, -ln * 0.3)], 0.0032, mat,
                  closed=False, sides=3)

    def leg_fray(self, bm, rims, z_bot):
        src = [(p, V(0, 0, -1)) for rim in rims for p in rim]
        self.fray(bm, src, every=1)


def wrap_band(bm, ring, axis, width, mat, lift=0.003, thick=0.008, tilt=0.0):
    """A strip of cloth or tape wrapped round a limb. ring: [(point, normal)]."""
    axis = Vector(axis).normalized()
    n = len(ring)
    if n < 3:
        return
    rows = []
    for i, (p, nr) in enumerate(ring):
        sh = axis * (tilt * math.sin(2 * math.pi * i / n))
        a = p + nr * lift + sh
        b = p + nr * (lift + thick) + sh
        rows.append([bm.verts.new(a - axis * width / 2), bm.verts.new(b - axis * width / 2),
                     bm.verts.new(b + axis * width / 2), bm.verts.new(a + axis * width / 2)])
    faces = []
    for i in range(n):
        r0, r1 = rows[i], rows[(i + 1) % n]
        for k in range(4):
            k1 = (k + 1) % 4
            try:
                faces.append(bm.faces.new((r0[k], r0[k1], r1[k1], r1[k])))
            except ValueError:
                pass
    bm.normal_update()
    c = sum((p for p, _ in ring), Vector()) / n
    flip = sum((f.calc_center_median() - c).dot(f.normal) for f in faces) < 0
    for f in faces:
        if flip:
            f.normal_flip()
        f.material_index = mat
        f.smooth = False


def leg_ring(side, z, r, n=20):
    out = []
    for k in range(n):
        a = 2 * math.pi * k / n
        nr = V(math.cos(a), math.sin(a), 0)
        out.append((V(LEG_X * side, LEG_Y, z) + nr * r, nr))
    return out


def limb_ring(bvh, bone, t, side, n=20):
    co, d = arm_point(bone, t, side)
    return ring_on(bvh, co, d, n=n, far=0.35), d
