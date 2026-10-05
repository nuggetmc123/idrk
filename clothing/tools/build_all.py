"""Build all 20 clothing pieces for PeeperLeeper.fbx.

    pip install bpy
    python3 build_all.py <PeeperLeeper.fbx> <output clothing dir> [only-name ...]

For each garment this writes, under <out>/<Category>/<NN_Name>/:
    <Name>_WithPlayer.fbx   player body + eyes + rig + garment, skinned
    <Name>_ClothOnly.fbx    rig + garment only (drop onto the same rig)
    <Name>_preview.jpg      render of the garment worn in the model's pose
Textures are embedded in the FBX and also saved to <out>/Textures/.
"""
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from clothing_lib import *  # noqa: E402,F401,F403
from clothing_lib import (BOTTOM_GROUPS, FOREARM, HEM_Z, LEG_X, LEG_Y,  # noqa: E402
                          LONG_TOP_GROUPS, NECK_Z, TOP_GROUPS, UPPERARM, V,
                          WAIST_Z, Ctx, Cut, Mat, Region, Shell, arm_point,
                          box, box_region, disc, ellipsoid, export, finish,
                          hole, leg_point, leg_tube, merge, ordered_loop,
                          ring_on, smoothstep, surf, sweep, trim_band,
                          Wear, wrap_band, leg_ring, limb_ring)

FRONT = V(0, 1, 0)       # ray direction that hits the front of the body
BACK = V(0, -1, 0)


# ---------------------------------------------------------------- wear wrappers
_W = None     # the Wear of the garment currently being built


def wear(m, seed, kind):
    global _W
    _W = Wear(m, seed, kind)
    return _W


def shell(ctx, keep, cuts=(), regions=(), worn=True, n_holes=3, elbows=False, **k):
    """Shell + scrap panels, random tears and fraying threads."""
    if worn and _W:
        regions = [_W.panels] + list(regions)
        z = (0.06, 0.3) if _W.kind == 'bottom' else (0.34, 0.85)
        r = (0.035, 0.055) if _W.kind == 'bottom' else (0.05, 0.08)
        k['holes'] = list(k.get('holes', [])) + _W.holes(ctx, n=n_holes, z=z, r=r,
                                                         elbows=elbows)
    sh = Shell(ctx, keep, cuts, regions, **k)
    if worn and _W:
        _W.fray(sh.bm, sh.fray)
    return sh


def done(ctx, name, bm, m):
    """Final scavenger touches (wraps), seam stitches, then build the object."""
    w = _W
    r = w.rng
    bvh = bvh_of(bm)
    if w.kind == 'long':
        s = r.choice((1, -1))
        ring, d = limb_ring(bvh, FOREARM, r.uniform(0.35, 0.65), s)
        wrap_band(bm, ring, d, r.uniform(0.05, 0.08), w.wrap, tilt=0.02)
        if r.random() < 0.5:
            ring, d = limb_ring(bvh, UPPERARM, r.uniform(0.6, 0.8), -s)
            wrap_band(bm, ring, d, 0.04, w.wrap, tilt=0.015)
    elif w.kind == 'top' and r.random() < 0.6:
        s = r.choice((1, -1))
        ring, d = limb_ring(bvh, UPPERARM, 0.62, s)
        wrap_band(bm, ring, d, 0.04, w.wrap, tilt=0.012)
    elif w.kind == 'bottom' and getattr(w, 'leg_info', None):
        z_top, z_bot = w.leg_info
        if z_bot < -0.2:
            for s in ((1, -1) if r.random() < 0.4 else (r.choice((1, -1)),)):
                z = z_bot + r.uniform(0.08, 0.16)
                h = surf(bvh, (LEG_X * s + 0.6 * s, LEG_Y, z), V(-s, 0, 0))
                rad = abs(h[0].x - LEG_X * s) if h else 0.12
                wrap_band(bm, leg_ring(s, z, rad), V(0, 0, 1), r.uniform(0.05, 0.08),
                          w.wrap, tilt=0.02)
    return finish(ctx, name, bm, m, TEX, thread=w.thread)


# ---------------------------------------------------------------- helpers
def top_cuts(sleeve, hem=HEM_Z, neck=NECK_Z, ragged=0.035, neck_tilt=0.0):
    """Neck, hem and both sleeve cuts for a top. sleeve=('short', t) or ('long', t).
    Order matters to callers: [neck, hem, sleeve L, sleeve R]."""
    cuts = [Cut((0, 0, neck), (0, -neck_tilt, 1), region=lambda p: abs(p.x) < 0.3,
                name='neck'),
            Cut((0, 0, hem), (0, 0, -1), ragged=ragged, name='hem')]
    bone = UPPERARM if sleeve[0] == 'short' else FOREARM
    for s in (1, -1):
        co, d = arm_point(bone, sleeve[1], s)
        cuts.append(Cut(co, d, ragged=ragged, name=f'sleeve{s}'))
    return cuts


def waist_cut(z=WAIST_Z, ragged=0.0):
    return Cut((0, 0, z), (0, 0, 1), ragged=ragged, name='waist')


def leg_r(z, z_top, z_bot, r_top, r_bot):
    t = (z - z_top) / (z_bot - z_top)
    return r_top + (r_bot - r_top) * t


def on_front(bvh, x, z, lift=0.0):
    h = surf(bvh, (x, -2, z), FRONT)
    return (h[0] + h[1] * lift, h[1]) if h else (None, None)


def on_back(bvh, x, z, lift=0.0):
    h = surf(bvh, (x, 2, z), BACK)
    return (h[0] + h[1] * lift, h[1]) if h else (None, None)


def buttons(bm, bvh, x, zs, mat, r=0.016):
    for z in zs:
        p, n = on_front(bvh, x, z, 0.004)
        if p is not None:
            disc(bm, p, n, r, 0.008, mat)


def belt_loops(bm, bvh, z, mat, n=10, skip_front=30):
    for p, nrm in ring_on(bvh, (0, LEG_Y, z), n=n):
        ang = math.degrees(math.atan2(p.x, -(p.y - LEG_Y)))
        if abs(ang) < skip_front:
            continue
        box(bm, p + nrm * 0.004, nrm, (0.016, 0.06, 0.009), mat)


def buckle(bm, bvh, z, mat, size=(0.075, 0.055, 0.012)):
    p, n = on_front(bvh, 0.0, z, 0.006)
    if p is not None:
        box(bm, p, n, size, mat)
        box(bm, p + n * 0.006, n, (size[0] * 0.55, size[1] * 0.45, 0.008), mat)


def rope_belt(bm, bvh, z, mat, r=0.017, knot=True):
    ring = ring_on(bvh, (0, LEG_Y, z), n=48)
    pts = [p + n * (r * 0.7) for p, n in ring]
    sweep(bm, pts, r, mat, closed=True, sides=7)
    if knot:
        p, n = on_front(bvh, 0.05, z, r)
        if p is not None:
            ellipsoid(bm, p, n, (0.03, 0.025, 0.02), mat)
            for dx, ln in ((-0.012, 0.13), (0.02, 0.10)):
                q = p + V(dx, 0, 0)
                pts = [q + V(dx * 0.6 * i, -0.012 * i, -ln * i / 4) for i in range(5)]
                sweep(bm, pts, r * 0.75, mat, closed=False, sides=6)


def drawstrings(bm, bvh, z, mat, tip_mat, dx=0.05, length=0.15):
    for s in (1, -1):
        p, n = on_front(bvh, s * dx, z, 0.006)
        if p is None:
            continue
        pts = [p + V(s * 0.004 * i, -0.006 * i, -length * i / 5) for i in range(6)]
        sweep(bm, pts, 0.007, mat, closed=False, sides=6)
        disc(bm, pts[-1] + V(0, 0, -0.012), V(0, 0, 1), 0.009, 0.026, tip_mat, segs=8)


def collar(bm, loop, thick, mat, height=0.065, spread=0.05, front_gap_deg=16):
    """Fold-down shirt collar along the neckline, open at the front."""
    from clothing_lib import _solidify
    import bmesh
    pts = []
    for p, n in ordered_loop(loop, V(0, 0.03, 0)):
        ang = math.degrees(math.atan2(p.x, -(p.y - 0.03)))
        if abs(ang) < front_gap_deg:
            continue
        pts.append((ang, p))
    pts.sort()
    if len(pts) < 4:
        return
    sb = bmesh.new()
    row0, row1 = [], []
    for ang, p in pts:
        radial = V(p.x, p.y - 0.03, 0).normalized()
        frontness = max(0.0, math.cos(math.radians(ang)))
        row0.append(sb.verts.new(p + radial * thick + V(0, 0, 0.012)))
        row1.append(sb.verts.new(p + radial * (thick + spread + 0.02 * frontness)
                                 - V(0, 0, height * (1 + 0.5 * frontness))))
    for i in range(len(row0) - 1):
        sb.faces.new((row0[i], row0[i + 1], row1[i + 1], row1[i]))
    sb.normal_update()
    if sum(f.normal.dot(V(f.calc_center_median().x, f.calc_center_median().y - 0.03, 0))
           for f in sb.faces) < 0:
        for f in sb.faces:
            f.normal_flip()
    solid = _solidify(sb, 0.012)
    sb.free()
    for f in solid.faces:
        f.material_index = mat
        f.smooth = True
    merge(bm, solid)


def hood(bm, loop, thick, mat):
    """A hood lying down around the neck: thin at the front, bunched behind."""
    pts, rads = [], []
    for p, n in ordered_loop(loop, V(0, 0.03, 0)):
        back = smoothstep(-0.15, 0.25, p.y)
        r = 0.03 + 0.085 * back
        radial = V(p.x, p.y - 0.03, 0).normalized()
        pts.append(p + radial * (thick + r * 0.8) + V(0, 0.02 * back, -0.02 * back))
        rads.append(r)
    sweep(bm, pts, rads, mat, closed=True, sides=10, flat=0.8)
    ellipsoid(bm, V(0, 0.36, 0.80), V(0, 1, 0.25), (0.2, 0.17, 0.05), mat)


def pocket_flap(bm, bvh, x, z, mat, btn_mat, w=0.12):
    p, n = on_front(bvh, x, z, 0.012)
    if p is None:
        return
    box(bm, p, n, (w, 0.04, 0.016), mat)
    disc(bm, p + n * 0.009 - V(0, 0, 0.005), n, 0.011, 0.007, btn_mat)


def cargo_pocket(bm, side, z, r, mat, btn_mat, size=(0.11, 0.13)):
    p, n = leg_point(side, z, r, 0)
    box(bm, p + n * 0.012, n, (size[0], size[1], 0.026), mat)
    q, _ = leg_point(side, z + size[1] / 2 + 0.005, r, 0)
    box(bm, q + n * 0.026, n, (size[0] + 0.012, 0.04, 0.018), mat)
    disc(bm, q + n * 0.036 - V(0, 0, 0.006), n, 0.01, 0.007, btn_mat)


def legs(bm, mats_idx, z_top, z_bot, r_top, r_bot, cuff=None, **kw):
    main, cap = mats_idx
    kw.setdefault('ragged', 0.0 if cuff else 0.035)
    if _W:
        kw.setdefault('panels', _W.panels)
        _W.leg_info = (z_top, z_bot)
    bottoms = []
    for s in (1, -1):
        bottoms.append(leg_tube(bm, s, z_top, z_bot, r_top, r_bot, main, cap, seed=s, **kw))
    if cuff:
        mat, rad, lift = cuff
        for s in (1, -1):
            pts = []
            for k in range(20):
                a = 2 * math.pi * k / 20
                rr = r_bot + rad * 0.6
                pts.append(V(LEG_X * s + math.cos(a) * rr, LEG_Y + math.sin(a) * rr,
                             z_bot + lift))
            sweep(bm, pts, rad, mat, closed=True, sides=8)
    if _W and kw['ragged']:
        _W.leg_fray(bm, bottoms, z_bot)
    return bottoms


def bvh_of(bm):
    return BVHTree.FromBMesh(bm)


# ---------------------------------------------------------------- PANTS
def cargo_pants(ctx):
    m = [Mat('Fabric', 'fabric', ['#5b6236'], seed=1),
         Mat('Pocket', 'fabric', ['#50562f'], raise_=0.007, seed=2),
         Mat('Belt', 'fabric', ['#1f1f1f'], raise_=0.011, tile=0.3, seed=3),
         Mat('Metal', 'metal', ['#8d8d86'], metal=0.8, rough=0.45, seed=4),
         Mat('Inner', 'flat', ['#15160f'])]
    wear(m, 17, 'bottom')
    regs = [box_region(2, z=(WAIST_Z - 0.06, WAIST_Z - 0.012)),
            box_region(1, x=(0.05, 0.2), y=(0.0, 1), z=(0.13, 0.27), mirror=True)]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.014, min_gap=0.01,
               thick=0.01, mats=m, seed=1)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.15, -0.36, 0.125, 0.115
    legs(bm, (0, 4), zt, zb, rt, rb, cuff=(0, 0.014, 0.012), wobble=0.009)
    for s in (1, -1):
        cargo_pocket(bm, s, -0.14, leg_r(-0.14, zt, zb, rt, rb), 1, 3)
    belt_loops(bm, bvh, WAIST_Z - 0.036, 2)
    buckle(bm, bvh, WAIST_Z - 0.036, 3)
    return done(ctx, 'CargoPants', bm, m)


def burlap_trousers(ctx):
    m = [Mat('Burlap', 'burlap', ['#a88a5a'], tile=0.35, seed=5, grime=0.45),
         Mat('PatchA', 'burlap', ['#6f5a3a'], tile=0.3, raise_=0.006, seed=6),
         Mat('PatchB', 'fabric', ['#5d6b78'], raise_=0.006, seed=7),
         Mat('Rope', 'rope', ['#c2a468'], tile=0.12, seed=8),
         Mat('Inner', 'flat', ['#20190f'])]
    wear(m, 34, 'bottom')
    regs = [box_region(1, x=(-0.22, -0.06), y=(-1, 0), z=(0.12, 0.25)),
            box_region(2, x=(0.08, 0.2), y=(0, 1), z=(0.18, 0.3))]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut(ragged=0.015)], regs, gap=0.016,
               min_gap=0.01, thick=0.012, wrinkle=0.009, mats=m, seed=2)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.15, -0.34, 0.13, 0.125
    legs(bm, (0, 4), zt, zb, rt, rb, wobble=0.014, ragged=0.04)
    p, n = leg_point(1, -0.17, leg_r(-0.17, zt, zb, rt, rb), -60)
    box(bm, p + n * 0.006, n, (0.09, 0.1, 0.012), 1, smooth=True)
    p, n = leg_point(-1, -0.08, leg_r(-0.08, zt, zb, rt, rb), -100)
    box(bm, p + n * 0.006, n, (0.08, 0.07, 0.012), 2, smooth=True)
    rope_belt(bm, bvh, WAIST_Z - 0.04, 3)
    return done(ctx, 'BurlapTrousers', bm, m)


def camo_pants(ctx):
    m = [Mat('Camo', 'camo', ['#6b6f4a', '#3f4a2c', '#8a7f5a', '#2a2a22'], tile=0.7, seed=9),
         Mat('Pad', 'leather', ['#2b2b2b'], tile=0.3, seed=10),
         Mat('Belt', 'fabric', ['#3d3a2a'], raise_=0.011, tile=0.3, seed=11),
         Mat('Metal', 'metal', ['#5c5c58'], metal=0.8, rough=0.5, seed=12),
         Mat('Inner', 'flat', ['#16170f'])]
    wear(m, 51, 'bottom')
    regs = [box_region(2, z=(WAIST_Z - 0.06, WAIST_Z - 0.012))]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.016, min_gap=0.01,
               thick=0.01, mats=m, seed=3)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.15, -0.36, 0.128, 0.11
    legs(bm, (0, 4), zt, zb, rt, rb, wobble=0.01, blouse=0.022)
    for s in (1, -1):
        p, n = leg_point(s, -0.13, leg_r(-0.13, zt, zb, rt, rb), -90)
        ellipsoid(bm, p + n * 0.012, n, (0.07, 0.08, 0.025), 1)
        p, n = leg_point(s, -0.06, leg_r(-0.06, zt, zb, rt, rb), 0)
        box(bm, p + n * 0.01, n, (0.09, 0.1, 0.022), 0, smooth=True)
    belt_loops(bm, bvh, WAIST_Z - 0.036, 2)
    buckle(bm, bvh, WAIST_Z - 0.036, 3, size=(0.08, 0.045, 0.014))
    return done(ctx, 'CamoPants', bm, m)


def denim_jeans(ctx):
    m = [Mat('Denim', 'denim', ['#2c4a78', '#c9d4e6'], tile=0.5, seed=13),
         Mat('Pocket', 'denim', ['#2a456f', '#c9d4e6'], tile=0.5, raise_=0.006, seed=14),
         Mat('Belt', 'leather', ['#5a3a1e'], raise_=0.011, tile=0.3, seed=15),
         Mat('Metal', 'metal', ['#b8a46a'], metal=0.9, rough=0.35, seed=16),
         Mat('Inner', 'flat', ['#101a2a']),
         Mat('Cuff', 'denim', ['#4b6a99', '#e3e9f3'], tile=0.5, seed=17)]
    wear(m, 68, 'bottom')
    regs = [box_region(2, z=(WAIST_Z - 0.055, WAIST_Z - 0.012)),
            box_region(1, x=(0.05, 0.19), y=(0.0, 1), z=(0.12, 0.26), mirror=True)]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.012, min_gap=0.009,
               thick=0.01, mats=m, seed=4)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.15, -0.36, 0.122, 0.112
    legs(bm, (0, 4), zt, zb, rt, rb, cuff=(5, 0.02, 0.02), wobble=0.006)
    belt_loops(bm, bvh, WAIST_Z - 0.033, 2, n=9)
    buckle(bm, bvh, WAIST_Z - 0.033, 3, size=(0.07, 0.05, 0.012))
    for s in (1, -1):    # rivets at front pocket corners
        p, n = on_front(bvh, s * 0.2, 0.28, 0.003)
        if p is not None:
            disc(bm, p, n, 0.008, 0.006, 3, segs=8)
    return done(ctx, 'DenimJeans', bm, m)


def track_pants(ctx):
    m = [Mat('Fabric', 'fabric', ['#2b2d33'], seed=18, grime=0.15),
         Mat('Stripe', 'flat', ['#e8e8e8'], raise_=0.002),
         Mat('Waistband', 'rib', ['#1d1e22'], raise_=0.008, tile=0.3, seed=19),
         Mat('Cord', 'flat', ['#e8e8e8']),
         Mat('Inner', 'flat', ['#0f0f12'])]
    wear(m, 85, 'bottom')
    stripe = Region(1, lambda p: abs(p.y - LEG_Y) < 0.025 and abs(p.x) > 0.15,
                    [(V(0, LEG_Y - 0.025, 0), V(0, 1, 0)), (V(0, LEG_Y + 0.025, 0), V(0, 1, 0))])
    regs = [stripe, box_region(2, z=(WAIST_Z - 0.06, WAIST_Z))]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.016, min_gap=0.01,
               thick=0.01, mats=m, seed=5)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    legs(bm, (0, 4), 0.15, -0.34, 0.128, 0.1, cuff=(2, 0.02, 0.018), wobble=0.012,
         stripe_mat=1, segs=24)
    drawstrings(bm, bvh, WAIST_Z - 0.035, 3, 3, dx=0.04, length=0.12)
    return done(ctx, 'TrackPants', bm, m)


# ---------------------------------------------------------------- SHORTS
def cargo_shorts(ctx):
    m = [Mat('Fabric', 'fabric', ['#b39d72'], seed=20),
         Mat('Pocket', 'fabric', ['#a48f65'], raise_=0.007, seed=21),
         Mat('Belt', 'fabric', ['#4a3b2a'], raise_=0.011, tile=0.3, seed=22),
         Mat('Metal', 'metal', ['#8d8d86'], metal=0.8, rough=0.45, seed=23),
         Mat('Inner', 'flat', ['#2a2416'])]
    wear(m, 102, 'bottom')
    regs = [box_region(2, z=(WAIST_Z - 0.055, WAIST_Z - 0.012)),
            box_region(1, x=(0.05, 0.19), y=(0.0, 1), z=(0.13, 0.26), mirror=True)]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.015, min_gap=0.01,
               thick=0.01, mats=m, seed=6)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.14, -0.09, 0.128, 0.138
    legs(bm, (0, 4), zt, zb, rt, rb, cuff=(0, 0.012, 0.01), rings=6)
    for s in (1, -1):
        cargo_pocket(bm, s, -0.03, leg_r(-0.03, zt, zb, rt, rb), 1, 3, size=(0.09, 0.08))
    belt_loops(bm, bvh, WAIST_Z - 0.034, 2)
    buckle(bm, bvh, WAIST_Z - 0.034, 3)
    return done(ctx, 'CargoShorts', bm, m)


def ripped_jorts(ctx):
    m = [Mat('Denim', 'denim', ['#3f6596', '#dbe3ef'], tile=0.5, seed=24, grime=0.2),
         Mat('Pocket', 'denim', ['#3a5e8c', '#dbe3ef'], tile=0.5, raise_=0.006, seed=25),
         Mat('Fray', 'flat', ['#dfe6ee']),
         Mat('Metal', 'metal', ['#b8a46a'], metal=0.9, rough=0.35, seed=26),
         Mat('Inner', 'flat', ['#13202f']),
         Mat('Band', 'denim', ['#355886', '#dbe3ef'], tile=0.5, raise_=0.007, seed=27)]
    wear(m, 119, 'bottom')
    regs = [box_region(5, z=(WAIST_Z - 0.05, WAIST_Z)),
            box_region(1, x=(0.05, 0.19), y=(0.0, 1), z=(0.12, 0.25), mirror=True)]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs,
               holes=[hole((0.17, -0.27, 0.12), 0.05, 1)], gap=0.012, min_gap=0.009,
               thick=0.01, mats=m, seed=7)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.14, -0.1, 0.126, 0.135
    rims = legs(bm, (0, 4), zt, zb, rt, rb, rings=6, ragged=0.03, wobble=0.008)
    import random as _r
    rr = _r.Random(3)
    for rim in rims:            # dangling frayed threads
        for p in rim[::2]:
            ln = rr.uniform(0.015, 0.045)
            sweep(bm, [p, p + V(rr.uniform(-.006, .006), rr.uniform(-.006, .006), -ln)],
                  0.0035, 2, closed=False, sides=4)
    p, n = on_front(bvh, 0.0, WAIST_Z - 0.025, 0.006)
    if p is not None:
        disc(bm, p, n, 0.014, 0.008, 3)
    return done(ctx, 'RippedJorts', bm, m)


def athletic_shorts(ctx):
    m = [Mat('Fabric', 'fabric', ['#b52a2a'], seed=28, grime=0.12),
         Mat('Stripe', 'flat', ['#f2f2f2'], raise_=0.002),
         Mat('Waistband', 'rib', ['#8f1f1f'], raise_=0.008, tile=0.3, seed=29),
         Mat('Cord', 'flat', ['#f2f2f2']),
         Mat('Inner', 'flat', ['#2a0c0c'])]
    wear(m, 136, 'bottom')
    stripe = Region(1, lambda p: abs(p.y - LEG_Y) < 0.02 and abs(p.x) > 0.15,
                    [(V(0, LEG_Y - 0.02, 0), V(0, 1, 0)), (V(0, LEG_Y + 0.02, 0), V(0, 1, 0))])
    regs = [stripe, box_region(2, z=(WAIST_Z - 0.06, WAIST_Z))]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.016, min_gap=0.01,
               thick=0.009, mats=m, seed=8)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    legs(bm, (0, 4), 0.14, -0.06, 0.13, 0.15, cuff=(1, 0.008, 0.006), rings=5,
         stripe_mat=1, segs=24, wobble=0.004)
    drawstrings(bm, bvh, WAIST_Z - 0.035, 3, 3, dx=0.035, length=0.1)
    return done(ctx, 'AthleticShorts', bm, m)


def burlap_shorts(ctx):
    m = [Mat('Burlap', 'burlap', ['#9c7f50'], tile=0.35, seed=30, grime=0.5),
         Mat('Patch', 'fabric', ['#6d3b2e'], raise_=0.006, seed=31),
         Mat('Stitch', 'flat', ['#2b2218']),
         Mat('Rope', 'rope', ['#b8995e'], tile=0.12, seed=32),
         Mat('Inner', 'flat', ['#21190e'])]
    wear(m, 153, 'bottom')
    regs = [box_region(1, x=(0.04, 0.2), y=(-1, 0), z=(0.1, 0.24))]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut(ragged=0.02)], regs, gap=0.016,
               min_gap=0.01, thick=0.013, wrinkle=0.01, mats=m, seed=9)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    legs(bm, (0, 4), 0.14, -0.08, 0.13, 0.145, rings=6, ragged=0.04, wobble=0.014)
    # cross-stitches across the patch edges
    for x in (0.04, 0.2):
        for z in (0.13, 0.17, 0.21):
            p, n = on_front(bvh, x, z, 0.004)
            if p is not None:
                box(bm, p, n, (0.03, 0.006, 0.006), 2, up=V(0, 0, 1))
    rope_belt(bm, bvh, WAIST_Z - 0.04, 3)
    return done(ctx, 'BurlapShorts', bm, m)


def camo_shorts(ctx):
    m = [Mat('Camo', 'camo', ['#7d7a5f', '#4c5236', '#a39a76', '#36352a'], tile=0.6, seed=33),
         Mat('Pocket', 'camo', ['#7d7a5f', '#4c5236', '#a39a76', '#36352a'], tile=0.6,
             raise_=0.007, seed=34),
         Mat('Belt', 'fabric', ['#2a2a24'], raise_=0.011, tile=0.3, seed=35),
         Mat('Metal', 'metal', ['#5c5c58'], metal=0.8, rough=0.5, seed=36),
         Mat('Inner', 'flat', ['#18180f'])]
    wear(m, 170, 'bottom')
    regs = [box_region(2, z=(WAIST_Z - 0.06, WAIST_Z - 0.012)),
            box_region(1, x=(0.12, 0.25), y=(-1, -0.05), z=(0.14, 0.27), mirror=True)]
    sh = shell(ctx, BOTTOM_GROUPS, [waist_cut()], regs, gap=0.016, min_gap=0.01,
               thick=0.01, mats=m, seed=10)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    zt, zb, rt, rb = 0.14, -0.12, 0.13, 0.14
    legs(bm, (0, 4), zt, zb, rt, rb, rings=6, wobble=0.008)
    for s in (1, -1):    # rolled-up hem
        pts = [V(LEG_X * s + math.cos(a) * (rb + 0.014), LEG_Y + math.sin(a) * (rb + 0.014),
                 zb + 0.022) for a in [2 * math.pi * k / 22 for k in range(22)]]
        sweep(bm, pts, 0.022, 0, closed=True, sides=8, flat=0.6)
    belt_loops(bm, bvh, WAIST_Z - 0.036, 2)
    buckle(bm, bvh, WAIST_Z - 0.036, 3, size=(0.08, 0.045, 0.014))
    return done(ctx, 'CamoShorts', bm, m)


# ---------------------------------------------------------------- T-SHIRTS
def basic_tee(ctx):
    m = [Mat('Cotton', 'fabric', ['#d9d4c7'], seed=40, grime=0.4),
         Mat('Rib', 'rib', ['#cbc5b6'], raise_=0.005, tile=0.25, seed=41)]
    wear(m, 187, 'top')
    cuts = top_cuts(('short', 0.42))
    regs = [trim_band(1, cuts[0], 0.035), trim_band(1, cuts[1], 0.03)]
    sh = shell(ctx, TOP_GROUPS, cuts, regs, gap=0.05, gap_arm=0.03, min_gap=0.044,
               min_gap_arm=0.022, thick=0.012, smooth=1, sleeve_folds=0.004,
               mats=m, seed=11)
    return done(ctx, 'BasicTee', sh.bm, m)


def striped_tee(ctx):
    m = [Mat('Navy', 'fabric', ['#22304f'], seed=42, grime=0.2),
         Mat('Cream', 'fabric', ['#e9e2cf'], seed=43, grime=0.2),
         Mat('Ringer', 'rib', ['#a8322b'], raise_=0.006, tile=0.25, seed=44)]
    wear(m, 204, 'top')
    cuts = top_cuts(('short', 0.4))
    zs = [HEM_Z + 0.075 * k for k in range(10)]
    stripes = Region(1, lambda p: abs(p.x) < 0.36 and int((p.z - HEM_Z) / 0.075) % 2 == 1,
                     [(V(0, 0, z), V(0, 0, 1)) for z in zs] +
                     [(V(0.36, 0, 0), V(1, 0, 0)), (V(-0.36, 0, 0), V(1, 0, 0))])
    regs = [stripes, trim_band(2, cuts[0], 0.035)] + [trim_band(2, c, 0.04) for c in cuts[2:]]
    sh = shell(ctx, TOP_GROUPS, cuts, regs, gap=0.048, gap_arm=0.028, min_gap=0.044,
               min_gap_arm=0.02, thick=0.011, smooth=1, mats=m, seed=12)
    return done(ctx, 'StripedTee', sh.bm, m)


def survivor_tee(ctx):
    m = [Mat('Cotton', 'fabric', ['#5f6b4e'], seed=45, grime=0.55),
         Mat('PatchA', 'fabric', ['#7b4a35'], raise_=0.005, seed=46),
         Mat('PatchB', 'burlap', ['#9c845a'], raise_=0.005, tile=0.3, seed=47),
         Mat('Stitch', 'flat', ['#1f1a14'])]
    wear(m, 221, 'top')
    cuts = top_cuts(('short', 0.36), ragged=0.045)
    regs = [box_region(1, x=(0.06, 0.2), y=(-1, 0), z=(0.42, 0.56)),
            box_region(2, x=(-0.24, -0.1), y=(0, 1), z=(0.55, 0.7))]
    holes = [hole((-0.16, -0.3, 0.62), 0.045, 1), hole((0.2, 0.3, 0.4), 0.04, 2),
             hole((0.55, -0.05, 0.86), 0.03, 3)]
    sh = shell(ctx, TOP_GROUPS, cuts, regs, holes=holes, gap=0.052, gap_arm=0.032,
               min_gap=0.044, min_gap_arm=0.022, thick=0.012, smooth=1, wrinkle=0.01,
               sleeve_folds=0.005, mats=m, seed=13)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    for z in (0.44, 0.49, 0.54):
        for x in (0.06, 0.2):
            p, n = on_front(bvh, x, z, 0.004)
            if p is not None:
                box(bm, p, n, (0.028, 0.006, 0.006), 3)
    return done(ctx, 'SurvivorTee', bm, m)


def polo_shirt(ctx):
    m = [Mat('Pique', 'waffle', ['#2f5d8a'], tile=0.25, seed=48, grime=0.2),
         Mat('Placket', 'fabric', ['#2a5580'], raise_=0.005, seed=49),
         Mat('Button', 'flat', ['#ece8de'], rough=0.3),
         Mat('Rib', 'rib', ['#264d75'], raise_=0.005, tile=0.25, seed=50),
         Mat('Logo', 'flat', ['#d8b545'], raise_=0.003)]
    wear(m, 238, 'top')
    cuts = top_cuts(('short', 0.44))
    regs = [box_region(1, x=(-0.03, 0.03), y=(-1, 0), z=(0.68, 0.95)),
            box_region(4, x=(0.12, 0.18), y=(-1, 0), z=(0.72, 0.77))] + \
        [trim_band(3, c, 0.04) for c in cuts[2:]]
    sh = shell(ctx, TOP_GROUPS, cuts, regs, gap=0.048, gap_arm=0.028, min_gap=0.044,
               min_gap_arm=0.02, thick=0.012, smooth=1, mats=m, seed=14)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    collar(bm, sh.loops['neck'], sh.thick, 0, height=0.06, spread=0.045)
    buttons(bm, bvh, 0.0, (0.73, 0.8, 0.87), 2, r=0.013)
    return done(ctx, 'PoloShirt', bm, m)


def scrap_armor_tee(ctx):
    m = [Mat('Cotton', 'fabric', ['#6b6863'], seed=51, grime=0.5),
         Mat('Strap', 'leather', ['#4a3220'], tile=0.3, seed=52),
         Mat('Rivet', 'metal', ['#a9a49a'], metal=0.9, rough=0.3, seed=53),
         Mat('Plate', 'metal', ['#7d8186'], metal=0.85, rough=0.55, tile=0.4, seed=54)]
    wear(m, 255, 'top')
    cuts = top_cuts(('short', 0.4), ragged=0.02)
    tee = shell(ctx, TOP_GROUPS, cuts, [], gap=0.045, gap_arm=0.028, min_gap=0.04,
                min_gap_arm=0.02, thick=0.011, smooth=1, mats=m, seed=15)
    bm = tee.bm
    plate = shell(ctx, {'Chest', 'Spine'}, worn=False, cuts=
                  [Cut((0.21, 0, 0), (1, 0, 0)), Cut((-0.21, 0, 0), (-1, 0, 0)),
                   Cut((0, 0, 0.46), (0, 0, -1)), Cut((0, 0, 0.83), (0, 0, 1)),
                   Cut((0, -0.05, 0), (0, 1, 0), ragged=0.0)],
                  regions=[Region(3, lambda p: True)], gap=0.08, min_gap=0.075, thick=0.028,
                  smooth=3, wrinkle=0.0, mats=m, seed=16)
    pbvh = bvh_of(plate.bm)
    merge(bm, plate.bm)
    straps = shell(ctx, TOP_GROUPS, worn=False, cuts=
                   [Cut((0.2, 0, 0), (1, 0, 0)), Cut((-0.2, 0, 0), (-1, 0, 0)),
                    Cut((0.13, 0, 0), (-1, 0, 0), region=lambda p: p.x >= 0),
                    Cut((-0.13, 0, 0), (1, 0, 0), region=lambda p: p.x < 0),
                    Cut((0, 0, 0.5), (0, 0, -1))],
                   regions=[Region(1, lambda p: True)], gap=0.062, min_gap=0.058, thick=0.012,
                   wrinkle=0.0, mats=m, seed=17)
    merge(bm, straps.bm)
    for x in (-0.17, 0.17):
        for z in (0.5, 0.79):
            p, n = on_front(pbvh, x, z, 0.004)
            if p is not None:
                ellipsoid(bm, p, n, (0.014, 0.014, 0.009), 2)
    return done(ctx, 'ScrapArmorTee', bm, m)


# ---------------------------------------------------------------- LONG SLEEVES
def hoodie(ctx):
    m = [Mat('Fleece', 'fabric', ['#4b4f57'], seed=60, grime=0.25),
         Mat('Rib', 'rib', ['#3f434a'], raise_=0.007, tile=0.25, seed=61),
         Mat('Pocket', 'fabric', ['#454951'], raise_=0.008, seed=62),
         Mat('Cord', 'flat', ['#d8d8d8']),
         Mat('Tip', 'metal', ['#a0a0a0'], metal=0.9, rough=0.35, seed=63)]
    wear(m, 272, 'long')
    cuts = top_cuts(('long', 0.9), hem=HEM_Z - 0.01)
    regs = [box_region(2, x=(-0.19, 0.19), y=(-1, -0.05), z=(0.33, 0.52)),
            trim_band(1, cuts[1], 0.05)] + [trim_band(1, c, 0.07) for c in cuts[2:]]
    sh = shell(ctx, LONG_TOP_GROUPS, cuts, regs, elbows=True, gap=0.058, gap_arm=0.034,
               min_gap=0.05, min_gap_arm=0.024, thick=0.014, smooth=2,
               sleeve_folds=0.006, wrinkle=0.008, mats=m, seed=18)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    hood(bm, sh.loops['neck'], sh.thick, 0)
    drawstrings(bm, bvh, NECK_Z - 0.04, 3, 4, dx=0.06, length=0.17)
    return done(ctx, 'Hoodie', bm, m)


def flannel_shirt(ctx):
    m = [Mat('Plaid', 'plaid', ['#9c2424', '#1c1c1c', '#d9c9a0'], tile=0.55, seed=64),
         Mat('Placket', 'plaid', ['#9c2424', '#1c1c1c', '#d9c9a0'], tile=0.55,
             raise_=0.005, seed=65),
         Mat('Button', 'flat', ['#e8e2d0'], rough=0.3)]
    wear(m, 289, 'long')
    cuts = top_cuts(('long', 0.92))
    regs = [box_region(1, x=(-0.03, 0.03), y=(-1, 0), z=(HEM_Z, 0.95)),
            box_region(1, x=(0.09, 0.21), y=(-1, 0), z=(0.6, 0.72), mirror=True)] + \
        [trim_band(1, c, 0.06) for c in cuts[2:]]
    sh = shell(ctx, LONG_TOP_GROUPS, cuts, regs, elbows=True, gap=0.05, gap_arm=0.03, min_gap=0.044,
               min_gap_arm=0.022, thick=0.012, smooth=1, sleeve_folds=0.005,
               mats=m, seed=19)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    collar(bm, sh.loops['neck'], sh.thick, 0, height=0.065, spread=0.05)
    buttons(bm, bvh, 0.0, (0.34, 0.45, 0.56, 0.67, 0.78, 0.88), 2, r=0.013)
    for s in (1, -1):
        pocket_flap(bm, bvh, s * 0.15, 0.73, 1, 2)
    return done(ctx, 'FlannelShirt', bm, m)


def burlap_shirt(ctx):
    m = [Mat('Burlap', 'burlap', ['#ad9061'], tile=0.35, seed=66, grime=0.5),
         Mat('Patch', 'fabric', ['#4d5a3f'], raise_=0.006, seed=67),
         Mat('Rope', 'rope', ['#c4a66a'], tile=0.12, seed=68),
         Mat('Lace', 'flat', ['#3a2a1a'])]
    wear(m, 306, 'long')
    cuts = top_cuts(('long', 0.86), ragged=0.04, neck_tilt=0.25)
    regs = [box_region(1, x=(0.6, 0.8), y=None, z=(0.82, 1.0)),
            box_region(1, x=(-0.25, -0.1), y=(-1, 0), z=(0.36, 0.5))]
    sh = shell(ctx, LONG_TOP_GROUPS, cuts, regs, elbows=True, gap=0.055, gap_arm=0.034,
               min_gap=0.046, min_gap_arm=0.024, thick=0.014, smooth=1, wrinkle=0.011,
               sleeve_folds=0.006, mats=m, seed=20)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    rope_belt(bm, bvh, 0.34, 2)
    for i, z in enumerate((0.72, 0.77, 0.82)):    # laced neck
        a, na = on_front(bvh, -0.035, z, 0.006)
        b, nb = on_front(bvh, 0.035, z + 0.04, 0.006)
        if a is not None and b is not None:
            sweep(bm, [a, b], 0.005, 3, closed=False, sides=5)
        a, na = on_front(bvh, 0.035, z, 0.006)
        b, nb = on_front(bvh, -0.035, z + 0.04, 0.006)
        if a is not None and b is not None:
            sweep(bm, [a, b], 0.005, 3, closed=False, sides=5)
    return done(ctx, 'BurlapShirt', bm, m)


def field_jacket(ctx):
    m = [Mat('Canvas', 'fabric', ['#55593a'], seed=69, grime=0.35),
         Mat('Pocket', 'fabric', ['#4c5034'], raise_=0.008, seed=70),
         Mat('Zip', 'metal', ['#6f6f6a'], raise_=0.004, metal=0.9, rough=0.4, seed=71),
         Mat('Snap', 'metal', ['#3b3b38'], metal=0.9, rough=0.4, seed=72),
         Mat('Cuff', 'fabric', ['#4a4e33'], raise_=0.006, seed=73)]
    wear(m, 323, 'long')
    cuts = top_cuts(('long', 0.9), hem=HEM_Z - 0.02)
    regs = [box_region(2, x=(-0.012, 0.012), y=(-1, 0), z=(HEM_Z - 0.03, 0.95)),
            box_region(1, x=(0.07, 0.21), y=(-1, 0), z=(0.6, 0.73), mirror=True),
            box_region(1, x=(0.08, 0.24), y=(-1, 0), z=(0.3, 0.45), mirror=True),
            trim_band(4, cuts[1], 0.04)] + [trim_band(4, c, 0.06) for c in cuts[2:]]
    sh = shell(ctx, LONG_TOP_GROUPS, cuts, regs, elbows=True, gap=0.06, gap_arm=0.036, min_gap=0.052,
               min_gap_arm=0.026, thick=0.015, smooth=1, sleeve_folds=0.006,
               mats=m, seed=21)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    collar(bm, sh.loops['neck'], sh.thick, 0, height=0.07, spread=0.06, front_gap_deg=10)
    for s in (1, -1):
        pocket_flap(bm, bvh, s * 0.14, 0.745, 1, 3, w=0.15)
        pocket_flap(bm, bvh, s * 0.16, 0.465, 1, 3, w=0.17)
        h = surf(bvh, (s * 0.27, 0.03, 2.0), V(0, 0, -1))
        if h:
            box(bm, h[0] + h[1] * 0.008, h[1], (0.05, 0.16, 0.012), 0, up=V(1, 0, 0))
            disc(bm, h[0] + h[1] * 0.016 + V(-s * 0.05, 0, 0), h[1], 0.01, 0.006, 3)
    p, n = on_front(bvh, 0.0, 0.9, 0.012)
    if p is not None:
        box(bm, p - V(0, 0, 0.02), n, (0.02, 0.04, 0.008), 2)
    return done(ctx, 'FieldJacket', bm, m)


def thermal_henley(ctx):
    m = [Mat('Waffle', 'waffle', ['#d8ccb4'], tile=0.2, seed=74, grime=0.3),
         Mat('Sleeve', 'waffle', ['#3a3d42'], tile=0.2, seed=75, grime=0.25),
         Mat('Placket', 'waffle', ['#cfc2a9'], tile=0.2, raise_=0.005, seed=76),
         Mat('Button', 'flat', ['#5a4632'], rough=0.4),
         Mat('Rib', 'rib', ['#33363b'], raise_=0.007, tile=0.25, seed=77)]
    wear(m, 340, 'long')
    cuts = top_cuts(('long', 0.9))
    raglan = Region(1, lambda p: abs(p.x) > 0.36 - 0.6 * max(0.0, p.z - 0.8),
                    [(V(0.36, 0, 0), V(1, 0, 0)), (V(-0.36, 0, 0), V(1, 0, 0))])
    regs = [raglan, box_region(2, x=(-0.028, 0.028), y=(-1, 0), z=(0.7, 0.95)),
            trim_band(4, cuts[0], 0.03)] + [trim_band(4, c, 0.07) for c in cuts[2:]]
    sh = shell(ctx, LONG_TOP_GROUPS, cuts, regs, elbows=True, gap=0.046, gap_arm=0.026, min_gap=0.042,
               min_gap_arm=0.02, thick=0.011, smooth=1, sleeve_folds=0.004,
               mats=m, seed=22)
    bm, bvh = sh.bm, bvh_of(sh.bm)
    buttons(bm, bvh, 0.0, (0.75, 0.81, 0.87), 3, r=0.012)
    return done(ctx, 'ThermalHenley', bm, m)


GARMENTS = [
    ('Pants', '01_CargoPants', cargo_pants),
    ('Pants', '02_BurlapTrousers', burlap_trousers),
    ('Pants', '03_CamoPants', camo_pants),
    ('Pants', '04_DenimJeans', denim_jeans),
    ('Pants', '05_TrackPants', track_pants),
    ('Shorts', '01_CargoShorts', cargo_shorts),
    ('Shorts', '02_RippedJorts', ripped_jorts),
    ('Shorts', '03_AthleticShorts', athletic_shorts),
    ('Shorts', '04_BurlapShorts', burlap_shorts),
    ('Shorts', '05_CamoShorts', camo_shorts),
    ('Shirts', '01_BasicTee', basic_tee),
    ('Shirts', '02_StripedTee', striped_tee),
    ('Shirts', '03_SurvivorTee', survivor_tee),
    ('Shirts', '04_PoloShirt', polo_shirt),
    ('Shirts', '05_ScrapArmorTee', scrap_armor_tee),
    ('LongSleeves', '01_Hoodie', hoodie),
    ('LongSleeves', '02_FlannelShirt', flannel_shirt),
    ('LongSleeves', '03_BurlapShirt', burlap_shirt),
    ('LongSleeves', '04_FieldJacket', field_jacket),
    ('LongSleeves', '05_ThermalHenley', thermal_henley),
]


# ---------------------------------------------------------------- preview
def render_preview(path, res=460):
    """Front-three-quarter and back-three-quarter views side by side."""
    import numpy as np
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 24
    sc.cycles.device = 'CPU'
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass
    sc.render.resolution_x = res
    sc.render.resolution_y = res
    w = bpy.data.worlds.new('w')
    bg = w.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.32, 0.34, 0.38, 1)
    bg.inputs[1].default_value = 0.9
    sc.world = w
    for name, rot, e in (('key', (0.9, 0.2, -0.6), 3.5), ('rim', (1.2, 0, 2.6), 2.0)):
        L = bpy.data.lights.new(name, 'SUN')
        L.energy = e
        o = bpy.data.objects.new(name, L)
        o.rotation_euler = rot
        sc.collection.objects.link(o)
    cam = bpy.data.cameras.new('cam')
    cam.type = 'ORTHO'
    cam.ortho_scale = 1.2
    co = bpy.data.objects.new('cam', cam)
    sc.collection.objects.link(co)
    sc.camera = co
    target = Vector((0, 0, -0.07))
    halves = []
    for i, loc in enumerate((Vector((0.9, -1.9, 0.3)), Vector((-0.9, 1.9, 0.3)))):
        co.location = loc
        co.rotation_euler = (target - loc).to_track_quat('-Z', 'Y').to_euler()
        tmp = path + f'.{i}.png'
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(tmp)
        halves.append(np.array(im.pixels[:], dtype=np.float32).reshape(res, res, 4))
        bpy.data.images.remove(im)
        os.remove(tmp)
    both = np.concatenate(halves, axis=1)
    out = bpy.data.images.new('preview', res * 2, res, alpha=False)
    out.pixels.foreach_set(both.ravel())
    out.filepath_raw = path
    out.file_format = 'JPEG'
    out.save(quality=90)


def main():
    global TEX
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    src, out = argv[0], argv[1]
    only = set(argv[2:])
    TEX = os.path.join(out, 'Textures')
    os.makedirs(TEX, exist_ok=True)
    for cat, folder, fn in GARMENTS:
        if only and folder not in only and fn.__name__ not in only:
            continue
        ctx = Ctx(src)
        ob = fn(ctx)
        name = ob.name
        d = os.path.join(out, cat, folder)
        os.makedirs(d, exist_ok=True)
        export(os.path.join(d, f'{name}_WithPlayer.fbx'), [ctx.arm, ctx.body, ctx.eyes, ob])
        export(os.path.join(d, f'{name}_ClothOnly.fbx'), [ctx.arm, ob])
        render_preview(os.path.join(d, f'{name}_preview.jpg'))
        print(f'BUILT {cat}/{folder}: {len(ob.data.vertices)} verts, '
              f'{len(ob.data.polygons)} faces, {len(ob.data.materials)} materials')


TEX = None
if __name__ == '__main__':
    main()
