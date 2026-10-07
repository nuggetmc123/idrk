"""
Builds a clean (no rust / no stains) Rust-style recycler and exports it as FBX.

Every part is its own named object with its own pivot, so it can be moved,
recoloured or replaced on its own after importing (Blender, Unity, Roblox, ...).

Run with Blender:   blender -b -P build_recycler.py
or with the bpy pip module:   python3 build_recycler.py
Outputs (next to this script): recycler.fbx, recycler.blend, recycler_preview.png
"""
import math
import os
import sys

import bpy  # noqa: I001  (bpy must be imported before bmesh/mathutils)
import bmesh
from mathutils import Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
RENDER_PREVIEW = "--no-render" not in sys.argv

# ---------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

# ------------------------------------------------------------------ materials
def make_mat(name, color, metallic=0.0, roughness=0.5, emission=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    m.diffuse_color = (*color, 1.0)
    return m

MAT = {
    "green":   make_mat("Paint_Green",   (0.060, 0.260, 0.110), 0.0, 0.45),
    "yellow":  make_mat("Paint_Yellow",  (0.950, 0.600, 0.020), 0.0, 0.40),
    "black":   make_mat("Hazard_Black",  (0.020, 0.020, 0.020), 0.0, 0.50),
    "steel":   make_mat("Steel_Dark",    (0.180, 0.185, 0.190), 0.9, 0.35),
    "chrome":  make_mat("Steel_Bright",  (0.700, 0.700, 0.720), 1.0, 0.20),
    "rubber":  make_mat("Rubber_Black",  (0.012, 0.012, 0.012), 0.0, 0.85),
    "red":     make_mat("Button_Red",    (0.700, 0.020, 0.020), 0.0, 0.30),
    "btngrn":  make_mat("Button_Green",  (0.030, 0.550, 0.050), 0.0, 0.30),
    "light":   make_mat("Light_Lens",    (0.100, 1.000, 0.150), 0.0, 0.10,
                        emission=(0.10, 1.0, 0.15), strength=4.0),
    "white":   make_mat("Plate_White",   (0.850, 0.850, 0.820), 0.0, 0.50),
    "hole":    make_mat("Slot_Dark",     (0.005, 0.005, 0.005), 0.0, 0.90),
}

# ----------------------------------------------------------------- collections
root_col = bpy.data.collections.new("Recycler")
scene.collection.children.link(root_col)
COLS = {}
def col(name):
    if name not in COLS:
        c = bpy.data.collections.new(name)
        root_col.children.link(c)
        COLS[name] = c
    return COLS[name]

root = bpy.data.objects.new("Recycler", None)
root.empty_display_type = "PLAIN_AXES"
root_col.objects.link(root)

# ------------------------------------------------------------- mesh builders
class Part:
    """Collects geometry into one bmesh, then becomes one object."""

    def __init__(self):
        self.bm = bmesh.new()

    def _mark(self, before):
        return [v for v in self.bm.verts if v.index == -1 or v not in before]

    def box(self, size, center, rot=None):
        mat = Matrix.Translation(center) @ (rot or Matrix()) @ Matrix.Diagonal((*size, 1.0))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=mat, calc_uvs=False)
        return self

    def cyl(self, radius, depth, center, axis="Z", seg=32, radius2=None, smooth=True):
        rot = {"Z": Matrix(), "X": Matrix.Rotation(math.pi / 2, 4, "Y"),
               "Y": Matrix.Rotation(math.pi / 2, 4, "X")}[axis]
        mat = Matrix.Translation(center) @ rot
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg,
                                    radius1=radius, radius2=radius if radius2 is None else radius2,
                                    depth=depth, matrix=mat, calc_uvs=False)
        if smooth:
            self._smooth_sides(res["verts"], mat.to_3x3() @ Vector((0, 0, 1)))
        return res

    def sphere(self, radius, center, seg=24, rings=12):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=radius,
                                        matrix=Matrix.Translation(center), calc_uvs=False)
        faces = {f for v in res["verts"] for f in v.link_faces}
        for f in faces:
            f.smooth = True
        return res

    def quad_prism(self, pts, thickness, normal):
        """Extrude a flat polygon (list of 3D points) along `normal` by `thickness`."""
        verts = [self.bm.verts.new(p) for p in pts]
        face = self.bm.faces.new(verts)
        n = Vector(normal).normalized() * thickness
        ext = bmesh.ops.extrude_face_region(self.bm, geom=[face])
        new_verts = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(self.bm, vec=n, verts=new_verts)
        bmesh.ops.recalc_face_normals(self.bm, faces=list(self.bm.faces))
        return self

    def _smooth_sides(self, verts, axis):
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.normal_update()
            side = abs(f.normal.dot(axis)) < 0.5
            f.smooth = side
        for f in faces:
            if not f.smooth:
                for e in f.edges:
                    e.smooth = False

    def build(self, name, mat, collection, origin=(0, 0, 0), bevel=0.0, parent=None):
        """Create the object. `origin` is the world-space pivot of the part."""
        o = Vector(origin)
        bmesh.ops.translate(self.bm, vec=-o, verts=list(self.bm.verts))
        bmesh.ops.remove_doubles(self.bm, verts=list(self.bm.verts), dist=1e-6)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(name, me)
        ob.location = o
        mats = mat if isinstance(mat, (list, tuple)) else [mat]
        for m in mats:
            me.materials.append(MAT[m])
        col(collection).objects.link(ob)
        if bevel > 0:
            mod = ob.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = 2
            mod.limit_method = "ANGLE"
            mod.angle_limit = math.radians(40)
            mod.harden_normals = False
        par = parent or root
        ob.parent = par
        ob.matrix_parent_inverse = par.matrix_world.inverted()
        return ob


def box_obj(name, size, center, mat, collection, bevel=0.008, origin=None, parent=None):
    return Part().box(size, center).build(name, mat, collection,
                                          origin=origin or center, bevel=bevel, parent=parent)


def cyl_obj(name, radius, depth, center, mat, collection, axis="Z", seg=32, origin=None,
            radius2=None, parent=None):
    p = Part()
    p.cyl(radius, depth, center, axis=axis, seg=seg, radius2=radius2)
    return p.build(name, mat, collection, origin=origin or center, parent=parent)


# ============================================================== DIMENSIONS
W, D = 1.80, 1.40                 # body width (X) and depth (Y)
SKID_H = 0.14
BODY_Z0, BODY_Z1 = SKID_H, 1.30   # body bottom / top
FRONT = -D / 2                    # front face is -Y
BACK = D / 2
RIM = 0.10                        # top rim width around the crusher opening
PIT = 0.42                        # crusher pit depth

# ============================================================== BASE / SKIDS
for side, x in (("L", -0.66), ("R", 0.66)):
    p = Part()
    p.box((0.16, D + 0.18, 0.025), (x, 0, 0.0125))            # bottom flange
    p.box((0.025, D + 0.18, SKID_H), (x, 0, SKID_H / 2))       # web
    p.box((0.16, D + 0.18, 0.025), (x, 0, SKID_H - 0.0125))   # top flange
    p.build(f"Base_Skid_{side}", "steel", "Base", origin=(x, 0, 0), bevel=0.004)

for i, y in enumerate((-0.45, 0.45)):
    box_obj(f"Base_Crossbar_{i + 1}", (1.50, 0.10, 0.06), (0, y, SKID_H - 0.03), "steel", "Base")

for sx in (-1, 1):
    for sy in (-1, 1):
        n = ("F" if sy < 0 else "B") + ("L" if sx < 0 else "R")
        box_obj(f"Base_Foot_{n}", (0.22, 0.22, 0.02), (sx * 0.66, sy * (D / 2 + 0.03), 0.01),
                "rubber", "Base", bevel=0.004)

# ============================================================== MAIN BODY
# Box with a recessed crusher pit in the top.
p = Part()
p.box((W, D, BODY_Z1 - BODY_Z0), (0, 0, (BODY_Z0 + BODY_Z1) / 2))
top = max(p.bm.faces, key=lambda f: f.calc_center_median().z)
ins = bmesh.ops.inset_individual(p.bm, faces=[top], thickness=RIM, depth=0.0)
ext = bmesh.ops.extrude_face_region(p.bm, geom=[top])
bmesh.ops.translate(p.bm, vec=(0, 0, -PIT),
                    verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)])
bmesh.ops.delete(p.bm, geom=[top], context="FACES_ONLY")
bmesh.ops.recalc_face_normals(p.bm, faces=list(p.bm.faces))
body = p.build("Body_Main", "green", "Body", origin=(0, 0, BODY_Z0), bevel=0.012)

# raised panels on front / back / sides
box_obj("Body_Panel_Front", (1.10, 0.025, 0.55), (-0.18, FRONT - 0.0125, 0.92), "green", "Body")
box_obj("Body_Panel_Back", (1.50, 0.025, 0.75), (0, BACK + 0.0125, 0.80), "green", "Body")
box_obj("Body_Panel_Left", (0.025, 1.10, 0.75), (-W / 2 - 0.0125, 0, 0.80), "green", "Body")
box_obj("Body_Panel_Right", (0.025, 1.10, 0.75), (W / 2 + 0.0125, 0, 0.80), "green", "Body")

# steel corner guards
for sx in (-1, 1):
    for sy in (-1, 1):
        n = ("F" if sy < 0 else "B") + ("L" if sx < 0 else "R")
        p = Part()
        cx, cy = sx * (W / 2 + 0.005), sy * (D / 2 + 0.005)
        h = BODY_Z1 - BODY_Z0
        p.box((0.012, 0.14, h), (cx, cy - sy * 0.064, (BODY_Z0 + BODY_Z1) / 2))
        p.box((0.14, 0.012, h), (cx - sx * 0.064, cy, (BODY_Z0 + BODY_Z1) / 2))
        p.build(f"Body_CornerGuard_{n}", "steel", "Body", origin=(cx, cy, BODY_Z0), bevel=0.003)

# top rim cap (steel frame around the pit)
p = Part()
ow, od, iw, idp = W + 0.02, D + 0.02, W - 2 * RIM + 0.02, D - 2 * RIM + 0.02
t = 0.03
p.box((ow, (od - idp) / 2, t), (0, -(od + idp) / 4, BODY_Z1 + t / 2))
p.box((ow, (od - idp) / 2, t), (0, (od + idp) / 4, BODY_Z1 + t / 2))
p.box(((ow - iw) / 2, idp, t), (-(ow + iw) / 4, 0, BODY_Z1 + t / 2))
p.box(((ow - iw) / 2, idp, t), ((ow + iw) / 4, 0, BODY_Z1 + t / 2))
p.build("Body_TopRim", "steel", "Body", origin=(0, 0, BODY_Z1), bevel=0.004)
RIM_TOP = BODY_Z1 + t

# hazard stripes around the bottom of the body (yellow band + black diagonal stripes)
BAND_Z0, BAND_Z1 = BODY_Z0 + 0.02, BODY_Z0 + 0.14
band = Part()
band.box((W + 0.01, 0.012, BAND_Z1 - BAND_Z0), (0, FRONT - 0.006, (BAND_Z0 + BAND_Z1) / 2))
band.box((W + 0.01, 0.012, BAND_Z1 - BAND_Z0), (0, BACK + 0.006, (BAND_Z0 + BAND_Z1) / 2))
band.build("Hazard_Band_Yellow", "yellow", "Body", origin=(0, 0, BAND_Z0), bevel=0.0)

stripes = Part()
bh = BAND_Z1 - BAND_Z0
sw, gap = 0.07, 0.14
for face_y, nrm in ((FRONT - 0.012, (0, -1, 0)), (BACK + 0.012, (0, 1, 0))):
    x = -W / 2 + 0.02
    while x + sw + bh < W / 2 - 0.02:
        pts = [(x, face_y, BAND_Z0), (x + sw, face_y, BAND_Z0),
               (x + sw + bh, face_y, BAND_Z1), (x + bh, face_y, BAND_Z1)]
        if nrm[1] > 0:
            pts = pts[::-1]
        stripes.quad_prism(pts, 0.003, nrm)
        x += gap
stripes.build("Hazard_Stripes_Black", "black", "Body", origin=(0, 0, BAND_Z0))

# ============================================================== HOPPER
HOP_Z0, HOP_Z1 = RIM_TOP, RIM_TOP + 0.42
bx, by = W / 2 - RIM + 0.03, D / 2 - RIM + 0.03       # bottom half extents
tx, ty = W / 2 + 0.12, D / 2 + 0.12                    # top half extents
p = Part()
bot = [(-bx, -by, HOP_Z0), (bx, -by, HOP_Z0), (bx, by, HOP_Z0), (-bx, by, HOP_Z0)]
tp = [(-tx, -ty, HOP_Z1), (tx, -ty, HOP_Z1), (tx, ty, HOP_Z1), (-tx, ty, HOP_Z1)]
vb = [p.bm.verts.new(v) for v in bot]
vt = [p.bm.verts.new(v) for v in tp]
for i in range(4):
    j = (i + 1) % 4
    p.bm.faces.new((vb[i], vb[j], vt[j], vt[i]))
bmesh.ops.recalc_face_normals(p.bm, faces=list(p.bm.faces))
hopper = p.build("Hopper", "green", "Hopper", origin=(0, 0, HOP_Z0))
sol = hopper.modifiers.new("Thickness", "SOLIDIFY")
sol.thickness = 0.03
sol.offset = 1.0
hb = hopper.modifiers.new("Bevel", "BEVEL")
hb.width, hb.segments, hb.limit_method = 0.006, 2, "ANGLE"

# hopper top lip (rolled edge)
p = Part()
lip = 0.05
for (a, b) in ((tp[0], tp[1]), (tp[1], tp[2]), (tp[2], tp[3]), (tp[3], tp[0])):
    a, b = Vector(a), Vector(b)
    mid = (a + b) / 2
    length = (b - a).length + lip
    axis = "X" if abs(b.x - a.x) > 1e-6 else "Y"
    p.cyl(0.025, length, mid + Vector((0, 0, 0.01)), axis=axis, seg=16)
p.build("Hopper_Lip", "yellow", "Hopper", origin=(0, 0, HOP_Z1))

# hopper side ribs
p = Part()
for sx in (-1, 1):
    for f in (0.33, 0.66):
        y0 = -by + (2 * by) * f
        y1 = -ty + (2 * ty) * f
        pts = [(sx * bx, y0, HOP_Z0 + 0.02), (sx * tx, y1, HOP_Z1 - 0.02)]
        a, b = Vector(pts[0]), Vector(pts[1])
        d = b - a
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        mat = Matrix.Translation((a + b) / 2 + Vector((sx * 0.03, 0, 0))) @ rot
        bmesh.ops.create_cube(p.bm, size=1.0,
                              matrix=mat @ Matrix.Diagonal((0.03, 0.05, d.length, 1)))
p.build("Hopper_Ribs", "green", "Hopper", origin=(0, 0, HOP_Z0), bevel=0.004)

# ============================================================== CRUSHER
PIT_FLOOR = BODY_Z1 - PIT
ROLL_R = 0.15
ROLL_Z = PIT_FLOOR + ROLL_R + 0.03
ROLL_LEN = W - 2 * RIM - 0.10


def roller(name, y, spin):
    p = Part()
    res = p.cyl(ROLL_R, ROLL_LEN, (0, y, ROLL_Z), axis="X", seg=32)
    # teeth: rings of blocks around the drum
    n_rings, n_teeth = 7, 8
    for r in range(n_rings):
        x = -ROLL_LEN / 2 + 0.12 + r * (ROLL_LEN - 0.24) / (n_rings - 1)
        for k in range(n_teeth):
            ang = 2 * math.pi * k / n_teeth + spin + (r % 2) * math.pi / n_teeth
            rot = Matrix.Rotation(ang, 4, "X")
            off = rot @ Vector((0, 0, ROLL_R + 0.03))
            mat = Matrix.Translation(Vector((x, y, ROLL_Z)) + off) @ rot
            bmesh.ops.create_cube(p.bm, size=1.0, matrix=mat @ Matrix.Diagonal((0.05, 0.06, 0.07, 1)))
    ob = p.build(name, "chrome", "Crusher", origin=(0, y, ROLL_Z))
    return ob


roller("Crusher_Roller_Front", -0.17, 0.0)
roller("Crusher_Roller_Back", 0.17, math.pi / 8)
p = Part()
for y in (-0.17, 0.17):
    p.cyl(0.045, W - 2 * RIM + 0.02, (0, y, ROLL_Z), axis="X", seg=20)
p.build("Crusher_Axles", "steel", "Crusher", origin=(0, 0, ROLL_Z))

# grate below rollers
p = Part()
for i in range(9):
    x = -0.6 + i * 0.15
    p.box((0.03, D - 2 * RIM - 0.02, 0.02), (x, 0, PIT_FLOOR + 0.012))
p.build("Crusher_Grate", "steel", "Crusher", origin=(0, 0, PIT_FLOOR))

# gear cover on the right side
GEAR_C = Vector((W / 2 + 0.06, 0.0, ROLL_Z - 0.05))
p = Part()
p.cyl(0.36, 0.10, GEAR_C, axis="X", seg=48)
p.build("Gear_Cover", "yellow", "Crusher", origin=GEAR_C, parent=None)
p = Part()
p.cyl(0.12, 0.05, GEAR_C + Vector((0.07, 0, 0)), axis="X", seg=32)
p.build("Gear_Hub", "chrome", "Crusher", origin=GEAR_C + Vector((0.07, 0, 0)))
p = Part()
for k in range(8):
    a = 2 * math.pi * k / 8
    c = GEAR_C + Vector((0.056, math.cos(a) * 0.29, math.sin(a) * 0.29))
    p.cyl(0.018, 0.02, c, axis="X", seg=12)
p.build("Gear_Cover_Bolts", "chrome", "Crusher", origin=GEAR_C)

# ============================================================== CONTROLS
CB = Vector((0.52, FRONT - 0.09, 0.98))           # control box centre
box_obj("Control_Box", (0.46, 0.16, 0.38), CB, "steel", "Controls", bevel=0.01)
box_obj("Control_Faceplate", (0.40, 0.01, 0.32), CB + Vector((0, -0.085, 0)), "black", "Controls",
        bevel=0.002)

for name, mat, x in (("Button_Start", "btngrn", -0.10), ("Button_Stop", "red", 0.10)):
    c = CB + Vector((x, -0.09, 0.05))
    p = Part()
    p.cyl(0.050, 0.02, c + Vector((0, 0.0, 0)), axis="Y", seg=32)          # collar
    p.build(name + "_Collar", "chrome", "Controls", origin=c)
    p = Part()
    p.cyl(0.036, 0.035, c + Vector((0, -0.02, 0)), axis="Y", seg=32)
    p.build(name, mat, "Controls", origin=c)

# big power lever (pivot at the hinge so it can be rotated)
LEV = CB + Vector((0.0, -0.10, -0.10))
p = Part()
p.box((0.16, 0.03, 0.07), LEV + Vector((0, 0.01, 0)))
p.build("Lever_Base", "chrome", "Controls", origin=LEV, bevel=0.004)
p = Part()
p.cyl(0.014, 0.20, LEV + Vector((0, -0.03, 0.10)), axis="Z", seg=16)
p.build("Lever_Arm", "chrome", "Controls", origin=LEV + Vector((0, -0.03, 0)))
p = Part()
p.cyl(0.028, 0.08, LEV + Vector((0, -0.03, 0.22)), axis="Z", seg=24)
p.build("Lever_Grip", "red", "Controls", origin=LEV + Vector((0, -0.03, 0)))
for n in ("Lever_Arm", "Lever_Grip"):
    bpy.data.objects[n].rotation_euler.x = math.radians(-25)

# status light on top of the control box
LC = CB + Vector((0.12, 0.0, 0.19))
cyl_obj("Status_Light_Base", 0.055, 0.04, LC + Vector((0, 0, 0.02)), "steel", "Controls", origin=LC)
p = Part()
p.cyl(0.045, 0.07, LC + Vector((0, 0, 0.075)), axis="Z", seg=32)
p.sphere(0.045, LC + Vector((0, 0, 0.11)), seg=32, rings=16)
p.build("Status_Light_Lens", "light", "Controls", origin=LC)
p = Part()
p.cyl(0.05, 0.012, LC + Vector((0, 0, 0.16)), axis="Z", seg=32)
p.build("Status_Light_Cap", "steel", "Controls", origin=LC)

# ============================================================== OUTPUT
OUT_Z0, OUT_Z1 = 0.36, 0.66
box_obj("Output_Frame", (1.00, 0.05, OUT_Z1 - OUT_Z0 + 0.10),
        (-0.30, FRONT - 0.025, (OUT_Z0 + OUT_Z1) / 2), "steel", "Output", bevel=0.006)
box_obj("Output_Slot", (0.88, 0.01, OUT_Z1 - OUT_Z0), (-0.30, FRONT - 0.052, (OUT_Z0 + OUT_Z1) / 2),
        "hole", "Output", bevel=0.0)

# angled chute / tray (pivot at the hinge on the body)
HINGE = Vector((-0.30, FRONT - 0.05, OUT_Z0))
p = Part()
p.box((0.92, 0.42, 0.025), HINGE + Vector((0, -0.21, 0)))                     # floor
p.box((0.025, 0.42, 0.10), HINGE + Vector((-0.4475, -0.21, 0.05)))            # side L
p.box((0.025, 0.42, 0.10), HINGE + Vector((0.4475, -0.21, 0.05)))             # side R
p.box((0.92, 0.025, 0.08), HINGE + Vector((0, -0.4075, 0.04)))                # front lip
tray = p.build("Output_Tray", "yellow", "Output", origin=HINGE, bevel=0.004)
tray.rotation_euler.x = math.radians(-12)
# tray support legs
p = Part()
for x in (-0.36, 0.36):
    p.box((0.04, 0.04, 0.24), Vector((-0.30 + x, FRONT - 0.40, 0.25)))
p.build("Output_Tray_Legs", "steel", "Output", origin=(-0.30, FRONT - 0.40, BODY_Z0), bevel=0.003)

# ============================================================== BACK: MOTOR + EXHAUST
MC = Vector((0.40, BACK + 0.17, 0.62))
box_obj("Motor_Housing", (0.62, 0.30, 0.46), MC, "steel", "Motor", bevel=0.015)
p = Part()
for i in range(7):
    p.box((0.50, 0.012, 0.025), MC + Vector((0, 0.153, -0.15 + i * 0.05)))
p.build("Motor_Vent_Slats", "black", "Motor", origin=MC + Vector((0, 0.15, 0)), bevel=0.0)

EX = Vector((-0.55, BACK + 0.24, 0.0))
p = Part()
p.cyl(0.07, 1.55, EX + Vector((0, 0, 1.05)), axis="Z", seg=32)
p.build("Exhaust_Pipe", "steel", "Motor", origin=EX + Vector((0, 0, 0.28)))
cyl_obj("Exhaust_Collar", 0.09, 0.06, EX + Vector((0, 0, 1.70)), "chrome", "Motor")
p = Part()
p.cyl(0.10, 0.02, EX + Vector((0, 0, 1.845)), axis="Z", seg=32)
cap = p.build("Exhaust_RainCap", "steel", "Motor", origin=EX + Vector((-0.09, 0, 1.835)))
cap.rotation_euler.y = math.radians(-12)
p = Part()
for z in (0.55, 1.15):
    p.box((0.06, 0.25, 0.04), EX + Vector((0, -0.13, z)))
    p.cyl(0.078, 0.04, EX + Vector((0, 0, z)), axis="Z", seg=24)
p.build("Exhaust_Brackets", "chrome", "Motor", origin=EX + Vector((0, 0, 0.55)), parent=None)
# base of exhaust bend into motor
p = Part()
p.cyl(0.07, 0.30, EX + Vector((0.15, 0, 0.28)), axis="X", seg=24)
p.cyl(0.06, 0.14, EX + Vector((0.30, -0.07, 0.28)), axis="Y", seg=24)
p.build("Exhaust_Elbow", "steel", "Motor", origin=EX + Vector((0, 0, 0.28)))

# ============================================================== SIDES: VENTS, HANDLES
VC = Vector((-W / 2 - 0.03, 0.0, 0.75))
box_obj("Vent_Frame", (0.03, 0.80, 0.42), VC, "steel", "Details", bevel=0.005)
p = Part()
for i in range(8):
    rot = Matrix.Rotation(math.radians(35), 4, "Y")
    mat = Matrix.Translation(VC + Vector((-0.02, 0, -0.17 + i * 0.048))) @ rot
    bmesh.ops.create_cube(p.bm, size=1.0, matrix=mat @ Matrix.Diagonal((0.006, 0.74, 0.05, 1)))
p.build("Vent_Louvers", "black", "Details", origin=VC)

for side, sx in (("L", -1), ("R", 1)):
    for idx, y in enumerate((-0.48, 0.48)):
        hc = Vector((sx * (W / 2 + 0.02), y, 1.12))
        if sx > 0 and abs(y) < 0.5 and abs(hc.z - GEAR_C.z) < 0.45 and abs(y - GEAR_C.y) < 0.5:
            hc.z = 0.42
        p = Part()
        p.box((0.05, 0.03, 0.03), hc + Vector((sx * 0.025, -0.11, 0)))
        p.box((0.05, 0.03, 0.03), hc + Vector((sx * 0.025, 0.11, 0)))
        p.cyl(0.016, 0.25, hc + Vector((sx * 0.055, 0, 0)), axis="Y", seg=16)
        p.build(f"Handle_{side}{idx + 1}", "chrome", "Details", origin=hc)

# warning sign on the front-left
SC = Vector((-0.30, FRONT - 0.03, 1.02))
box_obj("Warning_Plate", (0.30, 0.008, 0.20), SC, "white", "Details", bevel=0.002)
p = Part()
p.quad_prism([SC + Vector((-0.08, -0.005, -0.065)), SC + Vector((0.08, -0.005, -0.065)),
              SC + Vector((0.0, -0.005, 0.075))], 0.003, (0, -1, 0))
p.build("Warning_Triangle", "yellow", "Details", origin=SC)
p = Part()
p.box((0.016, 0.004, 0.065), SC + Vector((0, -0.009, 0.0)))
p.box((0.016, 0.004, 0.016), SC + Vector((0, -0.009, -0.045)))
p.build("Warning_Mark", "black", "Details", origin=SC)

# bolts along the top rim
p = Part()
for i in range(10):
    x = -W / 2 + 0.08 + i * (W - 0.16) / 9
    for y in (FRONT - 0.002, BACK + 0.002):
        p.cyl(0.014, 0.012, Vector((x, y * 0.965, RIM_TOP + 0.006)), axis="Z", seg=12)
for i in range(6):
    y = -D / 2 + 0.15 + i * (D - 0.30) / 5
    for x in (-W / 2, W / 2):
        p.cyl(0.014, 0.012, Vector((x * 0.97, y, RIM_TOP + 0.006)), axis="Z", seg=12)
p.build("Bolts_TopRim", "chrome", "Details", origin=(0, 0, RIM_TOP))

p = Part()
for (cx, cz, w, h) in ((-0.18, 0.92, 1.10, 0.55), ):
    for fx in (-1, 1):
        for fz in (-1, 1):
            p.cyl(0.012, 0.012, Vector((cx + fx * (w / 2 - 0.04), FRONT - 0.03,
                                        cz + fz * (h / 2 - 0.04))), axis="Y", seg=12)
p.build("Bolts_FrontPanel", "chrome", "Details", origin=(0, FRONT, 0.92))

# ============================================================== UVs + shading
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
for o in meshes:
    o.data.uv_layers.new(name="UVMap")
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT")

# ============================================================== EXPORT
fbx_path = os.path.join(OUT_DIR, "recycler.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=False,
    object_types={"EMPTY", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    path_mode="AUTO",
)
print("wrote", fbx_path)

# ------------------------------------------------ preview render (optional)
if RENDER_PREVIEW:
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.60, 0.66, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world

    pm = Part()
    pm.box((12, 12, 0.01), (0, 0, -0.005))
    floor = pm.build("_PreviewFloor", "white", "Preview")
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.5
    sun.angle = math.radians(8)
    so = bpy.data.objects.new("_PreviewSun", sun)
    so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    col("Preview").objects.link(so)

    cam = bpy.data.cameras.new("Cam")
    cam.lens = 45
    co = bpy.data.objects.new("_PreviewCam", cam)
    col("Preview").objects.link(co)
    co.location = (3.0, -4.3, 2.75)
    target = Vector((0.0, 0.0, 0.85))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 960
    scene.render.filepath = os.path.join(OUT_DIR, "recycler_preview.png")
    scene.view_settings.view_transform = "AgX"
    bpy.ops.render.render(write_still=True)

    # second angle from the back
    co.location = (-3.4, 3.9, 2.6)
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT_DIR, "recycler_preview_back.png")
    bpy.ops.render.render(write_still=True)

    # don't ship preview helpers inside the .blend
    for o in list(col("Preview").objects):
        bpy.data.objects.remove(o, do_unlink=True)
    root_col.children.unlink(col("Preview"))

blend_path = os.path.join(OUT_DIR, "recycler.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
print("wrote", blend_path)
