"""
IRONHOLD VAULT 07 - procedural loot-vault room.

Builds the whole scene with Blender's Python API and exports it as FBX (plus GLB
and a .blend source file). Units are meters, 1 Blender unit = 1 m.

Run with Blender:   blender -b -P build_vault.py -- <out_dir> [--render]
or the bpy module:  python3 build_vault.py <out_dir> [--render]
"""
import math
import os
import random
import sys

import bpy  # must come before bmesh when running as the bpy module
import bmesh
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
OUT_DIR = os.path.abspath(next((a for a in argv if not a.startswith("--")), "."))
DO_RENDER = "--render" in argv
# Strayed-style vault tiers: the keycard colour, signage and loot all scale with level.
LEVEL = int(next((a.split("=")[1] for a in argv if a.startswith("--level=")), "1"))
CARD, CARD_RGB = {1: ("GREEN", (0.1, 1.0, 0.25)), 2: ("BLUE", (0.1, 0.45, 1.0)), 3: ("RED", (1.0, 0.08, 0.04))}[LEVEL]
NAME = f"IronholdVault_L{LEVEL}"
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

PI = math.pi
RAD = math.radians
rng = random.Random(7)


# --------------------------------------------------------------------------
# Materials
# --------------------------------------------------------------------------
def material(name, color, metal=0.0, rough=0.6, emit=None, strength=0.0, alpha=1.0):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Transmission Weight"].default_value = 1.0
    m.diffuse_color = (*color, alpha)
    m.metallic = metal
    m.roughness = rough
    return m


M = {
    "concrete":      material("Concrete",      (0.24, 0.235, 0.22), rough=0.92),
    "concrete_dark": material("ConcreteDark",  (0.11, 0.11, 0.11), rough=0.9),
    "floor":         material("FloorConcrete", (0.17, 0.17, 0.165), rough=0.8),
    "steel":         material("Steel",         (0.56, 0.57, 0.59), metal=1.0, rough=0.35),
    "steel_dark":    material("SteelDark",     (0.16, 0.17, 0.18), metal=0.9, rough=0.45),
    "rust":          material("RustedSteel",   (0.34, 0.18, 0.10), metal=0.5, rough=0.75),
    "vault_paint":   material("VaultTeal",     (0.07, 0.19, 0.21), metal=0.6, rough=0.45),
    "orange":        material("AccentOrange",  (0.90, 0.33, 0.04), rough=0.5),
    "yellow":        material("HazardYellow",  (0.95, 0.68, 0.04), rough=0.6),
    "black":         material("HazardBlack",   (0.025, 0.025, 0.025), rough=0.7),
    "gold":          material("Gold",          (1.00, 0.72, 0.26), metal=1.0, rough=0.18),
    "crate_green":   material("CrateOlive",    (0.15, 0.21, 0.11), rough=0.65),
    "crate_elite":   material("CrateElite",    (0.06, 0.06, 0.07), metal=0.4, rough=0.45),
    "wood":          material("Wood",          (0.46, 0.30, 0.16), rough=0.85),
    "cash":          material("Cash",          (0.36, 0.50, 0.32), rough=0.85),
    "paper":         material("PaperBand",     (0.88, 0.86, 0.80), rough=0.9),
    "med_white":     material("MedWhite",      (0.85, 0.85, 0.82), rough=0.5),
    "med_red":       material("MedRed",        (0.70, 0.04, 0.04), rough=0.5),
    "rubber":        material("Rubber",        (0.02, 0.02, 0.02), rough=0.95),
    "barrel_blue":   material("BarrelBlue",    (0.04, 0.16, 0.45), metal=0.3, rough=0.55),
    "barrel_red":    material("BarrelRed",     (0.50, 0.06, 0.03), metal=0.3, rough=0.55),
    "glass":         material("Glass",         (0.80, 0.92, 1.00), rough=0.03, alpha=0.2),
    "light":         material("LightPanel",    (1.0, 0.95, 0.85), emit=(1.0, 0.93, 0.8), strength=14.0),
    "laser":         material("LaserRed",      (1.0, 0.05, 0.02), emit=(1.0, 0.04, 0.02), strength=25.0),
    "core":          material("CoreCyan",      (0.10, 0.90, 1.00), emit=(0.1, 0.85, 1.0), strength=35.0),
    "screen":        material("ScreenGreen",   (0.05, 0.60, 0.20), emit=(0.1, 1.0, 0.35), strength=5.0),
    "keycard":       material(f"Keycard{CARD.title()}", CARD_RGB, emit=CARD_RGB, strength=10.0),
    "sign":          material("SignOrange",    (1.0, 0.45, 0.08), emit=(1.0, 0.4, 0.05), strength=8.0),
}


# --------------------------------------------------------------------------
# Mesh helpers (everything is built with bmesh, no operators)
# --------------------------------------------------------------------------
def _link(obj, parent):
    scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def group(name, loc=(0, 0, 0), rot=(0, 0, 0), parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.3
    e.location = loc
    e.rotation_euler = rot
    return _link(e, parent)


def finish(name, bm, mat, loc=(0, 0, 0), rot=(0, 0, 0), parent=None, bevel=0.0, smooth=35):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    # world-scale box-projected UVs so tiling textures just work
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            c = l.vert.co
            l[uv].uv = (c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else (c.x, c.y)
    # smoothing: soft curved surfaces, hard edges above `smooth` degrees
    flat = bevel > 0 or smooth is None
    for f in bm.faces:
        f.smooth = not flat
    if not flat:
        lim = RAD(smooth)
        for e in bm.edges:
            if not e.is_manifold or e.calc_face_angle(PI) > lim:
                e.smooth = False
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    if bevel > 0:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 1
        b.limit_method = "ANGLE"
    return _link(o, parent)


def box(name, c, s, mat, rot=(0, 0, 0), parent=None, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=s, verts=bm.verts)
    return finish(name, bm, mat, c, rot, parent, bevel, smooth=None)


def cyl(name, c, r, depth, mat, rot=(0, 0, 0), parent=None, seg=24, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=r if r2 is None else r2, depth=depth)
    return finish(name, bm, mat, c, rot, parent)


def sphere(name, c, r, mat, parent=None, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    return finish(name, bm, mat, c, (0, 0, 0), parent, smooth=None)


def ring(name, c, r_out, r_in, depth, mat, rot=(0, 0, 0), parent=None, seg=48):
    """Hollow tube along local Z."""
    bm = bmesh.new()
    rows = []
    for i in range(seg):
        a = 2 * PI * i / seg
        ca, sa = math.cos(a), math.sin(a)
        rows.append([bm.verts.new((r_out * ca, r_out * sa, -depth / 2)),
                     bm.verts.new((r_out * ca, r_out * sa, depth / 2)),
                     bm.verts.new((r_in * ca, r_in * sa, depth / 2)),
                     bm.verts.new((r_in * ca, r_in * sa, -depth / 2))])
    for i in range(seg):
        a, b = rows[i], rows[(i + 1) % seg]
        for k in range(4):
            k2 = (k + 1) % 4
            bm.faces.new((a[k], b[k], b[k2], a[k2]))
    return finish(name, bm, mat, c, rot, parent, smooth=50)


def torus(name, c, R, r, mat, rot=(0, 0, 0), parent=None, seg=40, seg2=10):
    bm = bmesh.new()
    rows = []
    for i in range(seg):
        a = 2 * PI * i / seg
        row = []
        for j in range(seg2):
            b = 2 * PI * j / seg2
            d = R + r * math.cos(b)
            row.append(bm.verts.new((d * math.cos(a), d * math.sin(a), r * math.sin(b))))
        rows.append(row)
    for i in range(seg):
        A, B = rows[i], rows[(i + 1) % seg]
        for j in range(seg2):
            j2 = (j + 1) % seg2
            bm.faces.new((A[j], B[j], B[j2], A[j2]))
    return finish(name, bm, mat, c, rot, parent, smooth=60)


def frustum(name, c, bottom, top, h, mat, rot=(0, 0, 0), parent=None):
    """Tapered box (gold bar shape)."""
    bm = bmesh.new()
    bx, by = bottom[0] / 2, bottom[1] / 2
    tx, ty = top[0] / 2, top[1] / 2
    v = [bm.verts.new(p) for p in (
        (-bx, -by, -h / 2), (bx, -by, -h / 2), (bx, by, -h / 2), (-bx, by, -h / 2),
        (-tx, -ty, h / 2), (tx, -ty, h / 2), (tx, ty, h / 2), (-tx, ty, h / 2))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f])
    return finish(name, bm, mat, c, rot, parent, smooth=None)


def text(name, body, c, size, mat, rot, parent=None, extrude=0.015):
    cu = bpy.data.curves.new(name + "_curve", "FONT")
    cu.body = body
    cu.size = size
    cu.extrude = extrude
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    tmp = bpy.data.objects.new(name + "_tmp", cu)
    scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    me.name = name
    me.materials.clear()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    o.location = c
    o.rotation_euler = rot
    return _link(o, parent)


def boolean_cut(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    mod.solver = "EXACT"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter)


def between(name, p0, p1, thick, mat, parent=None):
    """Thin box stretched between two points (lasers, rods, straps)."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    o = box(name, (p0 + p1) / 2, (thick, thick, d.length), mat, parent=parent)
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return o


# --------------------------------------------------------------------------
# Layout constants
#   Room interior: x -7..7, y 0..12, z 0..4.5. Vault door in the y=0 wall,
#   entry tunnel runs out to y=-4.
# --------------------------------------------------------------------------
ROOM_X, ROOM_Y, ROOM_H = 7.0, 12.0, 4.5
DOOR_C = Vector((0.0, -0.3, 2.0))
DOOR_R = 1.66
HOLE_R = 1.70
Y90 = (RAD(90), 0, 0)  # rotate a Z-axis primitive to point along Y

root = group(NAME)

# ---------------------------------------------------------------- structure
g_struct = group("Structure", parent=root)
box("Floor_Room", (0, 5.95, -0.15), (15, 13.1, 0.3), M["floor"], parent=g_struct)
box("Floor_Tunnel", (0, -2.3, -0.15), (7, 3.4, 0.3), M["floor"], parent=g_struct)
box("Ceiling_Room", (0, 5.95, ROOM_H + 0.15), (15, 13.1, 0.3), M["concrete_dark"], parent=g_struct)
box("Ceiling_Tunnel", (0, -2.3, ROOM_H + 0.15), (7, 3.4, 0.3), M["concrete_dark"], parent=g_struct)
box("Wall_Left", (-7.25, 6, 2.25), (0.5, 12, 4.5), M["concrete"], parent=g_struct)
box("Wall_Right", (7.25, 6, 2.25), (0.5, 12, 4.5), M["concrete"], parent=g_struct)
box("Wall_Back", (0, 12.25, 2.25), (15, 0.5, 4.5), M["concrete"], parent=g_struct)
box("Tunnel_Wall_L", (-3.25, -2.3, 2.25), (0.5, 3.4, 4.5), M["concrete"], parent=g_struct)
box("Tunnel_Wall_R", (3.25, -2.3, 2.25), (0.5, 3.4, 4.5), M["concrete"], parent=g_struct)

front = box("Wall_Front", (0, -0.3, 2.25), (15, 0.6, 4.5), M["concrete"], parent=g_struct)
cutter = cyl("cutter", (0, -0.3, DOOR_C.z), HOLE_R + 0.02, 1.0, M["steel"], rot=Y90, seg=64)
boolean_cut(front, cutter)

# floor seams every 2 m
for i in range(-3, 4):
    box(f"Seam_X{i}", (i * 2, 6, 0.002), (0.025, 12, 0.004), M["concrete_dark"], parent=g_struct)
for j in range(1, 6):
    box(f"Seam_Y{j}", (0, j * 2, 0.002), (14, 0.025, 0.004), M["concrete_dark"], parent=g_struct)

# steel wainscot + orange band around the walls
g_trim = group("WallTrim", parent=g_struct)
for side, x in (("L", -6.97), ("R", 6.97)):
    box(f"Wainscot_{side}", (x, 6, 0.6), (0.06, 12, 1.2), M["steel_dark"], parent=g_trim)
    box(f"Band_{side}", (x, 6, 1.26), (0.08, 12, 0.12), M["orange"], parent=g_trim)
box("Wainscot_B", (0, 11.97, 0.6), (14, 0.06, 1.2), M["steel_dark"], parent=g_trim)
box("Band_B", (0, 11.96, 1.26), (14, 0.08, 0.12), M["orange"], parent=g_trim)
for side, x0, x1 in (("FL", -7, -2.3), ("FR", 2.9, 7)):
    box(f"Wainscot_{side}", ((x0 + x1) / 2, 0.03, 0.6), (x1 - x0, 0.06, 1.2), M["steel_dark"], parent=g_trim)
    box(f"Band_{side}", ((x0 + x1) / 2, 0.04, 1.26), (x1 - x0, 0.08, 0.12), M["orange"], parent=g_trim)

# portal frames: wall columns + ceiling beams
g_frames = group("SteelFrames", parent=g_struct)
for y in (4.0, 8.0):
    for side, x in (("L", -6.82), ("R", 6.82)):
        box(f"Column_{side}_{int(y)}", (x, y, 2.25), (0.36, 0.36, 4.5), M["steel_dark"], parent=g_frames, bevel=0.015)
        box(f"ColumnPlate_{side}_{int(y)}", (x, y, 0.1), (0.5, 0.5, 0.2), M["steel"], parent=g_frames, bevel=0.01)
    box(f"Beam_{int(y)}", (0, y, 4.28), (13.3, 0.3, 0.44), M["steel_dark"], parent=g_frames, bevel=0.015)
    box(f"BeamFlange_{int(y)}", (0, y, 4.07), (13.3, 0.42, 0.04), M["steel"], parent=g_frames)

# ceiling light panels
g_lights = group("CeilingLights", parent=g_struct)
for x in (-3.2, 3.2):
    for y in (2.0, 6.0, 10.0):
        n = f"{'L' if x < 0 else 'R'}{int(y)}"
        box(f"LightHousing_{n}", (x, y, 4.45), (0.5, 1.9, 0.1), M["steel_dark"], parent=g_lights, bevel=0.01)
        box(f"LightPanel_{n}", (x, y, 4.395), (0.4, 1.78, 0.02), M["light"], parent=g_lights)
for x in (-2.0, 2.0):
    box(f"TunnelLight_{'L' if x < 0 else 'R'}", (x * 1.4, -2.3, 3.2), (0.08, 1.2, 0.12), M["light"], parent=g_lights)

# pipes (left wall) + cable tray (right wall)
g_pipes = group("Pipes", parent=g_struct)
cyl("Pipe_Big", (-6.4, 6, 3.85), 0.12, 12, M["rust"], rot=Y90, parent=g_pipes, seg=20)
cyl("Pipe_Small", (-6.4, 6, 3.52), 0.07, 12, M["steel"], rot=Y90, parent=g_pipes, seg=16)
for k, y in enumerate((1.0, 3.0, 5.5, 7.0, 9.5, 11.0)):
    box(f"PipeBracket_{k}", (-6.55, y, 3.68), (0.9, 0.06, 0.55), M["steel_dark"], parent=g_pipes)
    ring(f"PipeCollar_{k}", (-6.4, y + 0.12, 3.85), 0.15, 0.12, 0.08, M["steel_dark"], rot=Y90, parent=g_pipes, seg=20)
box("CableTray", (6.35, 6, 3.95), (0.45, 12, 0.04), M["steel"], parent=g_pipes)
for k in range(4):
    cyl(f"Cable_{k}", (6.2 + k * 0.1, 6, 4.0), 0.03, 12, M["rubber"], rot=Y90, parent=g_pipes, seg=10)

# tunnel sign
g_sign = group("EntrySign", parent=g_struct)
box("Sign_Plate", (0, -3.3, 3.7), (3.4, 0.08, 0.8), M["steel_dark"], parent=g_sign, bevel=0.01)
for x in (-1.2, 1.2):
    box(f"Sign_Chain_{x:+.0f}", (x, -3.3, 4.3), (0.03, 0.03, 0.4), M["steel"], parent=g_sign)
text("Sign_Text", f"LEVEL {LEVEL} VAULT", (0, -3.35, 3.85), 0.3, M["keycard"], (RAD(90), 0, 0), parent=g_sign)
text("Sign_Sub", f"{CARD} KEYCARD REQUIRED", (0, -3.35, 3.5), 0.15, M["sign"], (RAD(90), 0, 0), parent=g_sign)

# hazard stripes at the threshold, inside and outside
g_haz = group("HazardStripes", parent=g_struct)
for side, y in (("In", 0.35), ("Out", -0.95)):
    for i in range(14):
        x = -2.1 + i * 0.3 + 0.15
        box(f"Hazard_{side}_{i}", (x, y, 0.004), (0.3, 0.5, 0.008),
            M["yellow"] if i % 2 == 0 else M["black"], parent=g_haz)

# back wall lettering
text("Wall_Title", "IRONHOLD RESERVE", (0, 11.93, 3.45), 0.46, M["orange"], (RAD(90), 0, 0), parent=g_struct, extrude=0.02)
text("Wall_Sub", f"LEVEL {LEVEL} CLEARANCE  -  {CARD} KEYCARD", (0, 11.93, 2.98), 0.16, M["steel"], (RAD(90), 0, 0), parent=g_struct)


# ---------------------------------------------------------------- vault door
g_door = group("VaultDoor", parent=root)
ring("DoorFrame", DOOR_C + Vector((0, -0.05, 0)), 2.15, HOLE_R, 1.0, M["steel_dark"], rot=Y90, parent=g_door, seg=64)
ring("DoorFrame_Lip", DOOR_C + Vector((0, -0.57, 0)), 2.3, 1.95, 0.08, M["orange"], rot=Y90, parent=g_door, seg=64)
# status light ring around the frame, lit in the vault's keycard colour
ring("DoorFrame_StatusLight", DOOR_C + Vector((0, -0.62, 0)), 2.27, 2.21, 0.03, M["keycard"], rot=Y90, parent=g_door, seg=64)
ring("DoorFrame_StatusLightIn", DOOR_C + Vector((0, 0.47, 0)), 2.2, 2.14, 0.03, M["keycard"], rot=Y90, parent=g_door, seg=64)
for i in range(16):
    a = 2 * PI * i / 16
    cyl(f"FrameBolt_{i}", DOOR_C + Vector((2.05 * math.cos(a), -0.57, 2.05 * math.sin(a))), 0.05, 0.12,
        M["steel"], rot=Y90, parent=g_door, seg=8)

# hinge (static part fixed to the wall)
HINGE = Vector((2.35, 0.45, DOOR_C.z))
cyl("Hinge_Barrel", HINGE, 0.2, 3.3, M["steel"], parent=g_door, seg=24)
for z in (0.55, 3.45):
    box(f"Hinge_Mount_{z}", (2.62, 0.22, z), (0.55, 0.45, 0.25), M["steel_dark"], parent=g_door, bevel=0.01)
    cyl(f"Hinge_Cap_{z}", (HINGE.x, HINGE.y, z), 0.24, 0.25, M["steel_dark"], parent=g_door, seg=24)

# the swinging leaf: pivot empty at the hinge axis, parts modelled relative to it
leaf = group("VaultDoor_Leaf", loc=HINGE, parent=g_door)
C = DOOR_C - HINGE  # door center in pivot space


def dp(x, y, z):
    return C + Vector((x, y, z))


cyl("Door_Body", dp(0, 0, 0), DOOR_R, 0.7, M["vault_paint"], rot=Y90, parent=leaf, seg=64)
ring("Door_FrontRim", dp(0, -0.39, 0), DOOR_R, 1.32, 0.1, M["steel"], rot=Y90, parent=leaf, seg=64)
ring("Door_FrontStripe", dp(0, -0.37, 0), 1.05, 0.93, 0.06, M["orange"], rot=Y90, parent=leaf, seg=48)
cyl("Door_Hub", dp(0, -0.48, 0), 0.32, 0.26, M["steel"], rot=Y90, parent=leaf, seg=32)
cyl("Door_HubCap", dp(0, -0.63, 0), 0.18, 0.06, M["steel_dark"], rot=Y90, parent=leaf, seg=24)
torus("Door_Wheel", dp(0, -0.68, 0), 0.75, 0.055, M["steel"], rot=Y90, parent=leaf)
for k in range(3):
    a = RAD(90 + 120 * k)
    ca, sa = math.cos(a), math.sin(a)
    box(f"Door_Spoke_{k}", dp(0.4 * ca, -0.66, 0.4 * sa), (0.72, 0.07, 0.07), M["steel"], rot=(0, -a, 0), parent=leaf)
    cyl(f"Door_Grip_{k}", dp(0.75 * ca, -0.8, 0.75 * sa), 0.045, 0.24, M["rubber"], rot=Y90, parent=leaf, seg=12)
# locking bolts (two rows, poking out of the rim)
for row, y in enumerate((-0.18, 0.18)):
    for i in range(14):
        a = 2 * PI * (i + 0.5 * row) / 14
        cyl(f"Door_Bolt_{row}_{i}", dp(1.72 * math.cos(a), y, 1.72 * math.sin(a)), 0.08, 0.36, M["steel"],
            rot=(0, PI / 2 - a, 0), parent=leaf, seg=12)
# exposed mechanism on the inner face
ring("Door_InnerRim", dp(0, 0.39, 0), DOOR_R, 1.45, 0.08, M["steel"], rot=Y90, parent=leaf, seg=64)
cyl("Door_Gear", dp(0, 0.42, 0), 0.5, 0.12, M["steel"], rot=Y90, parent=leaf, seg=32)
cyl("Door_GearAxle", dp(0, 0.5, 0), 0.14, 0.14, M["steel_dark"], rot=Y90, parent=leaf, seg=16)
for i in range(18):
    a = 2 * PI * i / 18
    box(f"Door_GearTooth_{i}", dp(0.55 * math.cos(a), 0.42, 0.55 * math.sin(a)), (0.12, 0.11, 0.08),
        M["steel"], rot=(0, -a, 0), parent=leaf)
for i in range(8):
    a = 2 * PI * (i + 0.5) / 8
    ca, sa = math.cos(a), math.sin(a)
    box(f"Door_Rod_{i}", dp(1.0 * ca, 0.43, 1.0 * sa), (0.9, 0.06, 0.08), M["steel_dark"], rot=(0, -a, 0), parent=leaf)
    box(f"Door_RodGuide_{i}", dp(1.2 * ca, 0.43, 1.2 * sa), (0.1, 0.1, 0.16), M["orange"], rot=(0, -a, 0), parent=leaf)
text("Door_Number", "07", dp(0, 0.4, 1.12), 0.34, M["orange"], (RAD(90), 0, PI), parent=leaf, extrude=0.02)
# hinge arms joining the leaf to the barrel
for z in (-0.9, 0.9):
    box(f"Door_HingeBracket_{z:+.0f}", dp(1.2, 0.47, z), (0.4, 0.25, 0.3), M["steel_dark"], parent=leaf, bevel=0.01)
    box(f"Door_HingeArm_{z:+.0f}", (-0.5, -0.05, C.z + z), (1.2, 0.2, 0.26), M["steel_dark"], parent=leaf, bevel=0.01)
    cyl(f"Door_HingeKnuckle_{z:+.0f}", (0, 0, C.z + z), 0.25, 0.3, M["steel"], parent=leaf, seg=24)
leaf.rotation_euler = (0, 0, RAD(-97))  # swung open into the room

# keycard readers + fuse box
g_sec = group("Security", parent=root)
for side, x, y, rz in (("Out", 2.7, -0.66, 0.0), ("In", -2.7, 0.06, PI)):
    rd = group(f"KeycardReader_{side}", loc=(x, y, 1.35), rot=(0, 0, rz), parent=g_sec)
    box("Reader_Body", (0, 0, 0), (0.26, 0.1, 0.42), M["steel_dark"], parent=rd, bevel=0.008)
    box("Reader_Slot", (0, -0.052, -0.08), (0.16, 0.01, 0.03), M["keycard"], parent=rd)
    box("Reader_Screen", (0, -0.052, 0.09), (0.18, 0.01, 0.12), M["keycard"], parent=rd)
    box("Reader_Stripe", (0, -0.051, 0.19), (0.26, 0.01, 0.03), M["orange"], parent=rd)

# open/close countdown screens: vaults open on a timer and lock again
for side, x, y, rz in (("Out", 2.7, -0.64, 0.0), ("In", -2.7, 0.04, PI)):
    ts = group(f"VaultTimer_{side}", loc=(x, y, 2.35), rot=(0, 0, rz), parent=g_sec)
    box("Timer_Body", (0, 0, 0), (0.9, 0.08, 0.5), M["steel_dark"], parent=ts, bevel=0.01)
    box("Timer_Face", (0, -0.042, 0), (0.8, 0.01, 0.4), M["black"], parent=ts)
    text("Timer_Label", "VAULT OPEN", (0, -0.05, 0.1), 0.1, M["keycard"], (RAD(90), 0, 0), parent=ts, extrude=0.004)
    text("Timer_Digits", "04:59", (0, -0.05, -0.07), 0.17, M["keycard"], (RAD(90), 0, 0), parent=ts, extrude=0.004)

fb = group("FuseBox", loc=(-4.2, 0.12, 1.55), parent=g_sec)
box("Fuse_Body", (0, 0, 0), (0.7, 0.24, 0.9), M["steel_dark"], parent=fb, bevel=0.01)
box("Fuse_Door", (0, 0.125, 0), (0.62, 0.02, 0.82), M["vault_paint"], parent=fb)
box("Fuse_Hazard", (0, 0.137, 0.3), (0.5, 0.01, 0.1), M["yellow"], parent=fb)
box("Fuse_LeverBase", (0.42, 0.05, 0), (0.12, 0.14, 0.3), M["steel"], parent=fb)
box("Fuse_Lever", (0.5, 0.05, 0.12), (0.04, 0.04, 0.3), M["med_red"], rot=(0, RAD(-25), 0), parent=fb)
for i in range(3):
    box(f"Fuse_Lamp_{i}", (-0.2 + i * 0.2, 0.14, -0.28), (0.07, 0.02, 0.07),
        M["screen"] if i < 2 else M["laser"], parent=fb)

# alarm beacons + cameras
for k, (x, y) in enumerate(((0, 0.25), (-6.5, 11.7), (6.5, 11.7))):
    box(f"Beacon_Mount_{k}", (x, y - 0.1 if k == 0 else y, 4.33), (0.2, 0.3 if k == 0 else 0.2, 0.06), M["steel_dark"], parent=g_sec)
    cyl(f"Beacon_Base_{k}", (x, y, 4.22), 0.11, 0.1, M["steel_dark"], parent=g_sec, seg=16)
    cyl(f"Beacon_Lamp_{k}", (x, y, 4.09), 0.09, 0.16, M["laser"], parent=g_sec, seg=16)
for k, (x, y, rz) in enumerate(((-6.6, 0.45, RAD(-40)), (6.6, 11.55, RAD(140)))):
    cam = group(f"SecurityCamera_{k}", loc=(x, y, 4.05), rot=(0, 0, rz), parent=g_sec)
    box("Cam_Mount", (0, 0, 0.2), (0.08, 0.08, 0.4), M["steel_dark"], parent=cam)
    box("Cam_Body", (0, 0.18, 0), (0.18, 0.42, 0.18), M["med_white"], rot=(RAD(-20), 0, 0), parent=cam, bevel=0.01)
    cyl("Cam_Lens", (0, 0.4, -0.08), 0.06, 0.06, M["black"], rot=(RAD(70), 0, 0), parent=cam, seg=16)
    box("Cam_LED", (0.06, 0.38, 0.0), (0.02, 0.02, 0.02), M["laser"], parent=cam)


# ---------------------------------------------------------------- prop kits
def military_crate(name, loc, rz=0.0, parent=None, size=(1.2, 0.7, 0.6), elite=False):
    g = group(name, loc=loc, rot=(0, 0, rz), parent=parent)
    sx, sy, sz = size
    body_m = M["crate_elite"] if elite else M["crate_green"]
    box("Crate_Body", (0, 0, sz * 0.43), (sx, sy, sz * 0.86), body_m, parent=g, bevel=0.02)
    box("Crate_Lid", (0, 0, sz * 0.93), (sx + 0.04, sy + 0.04, sz * 0.14), body_m, parent=g, bevel=0.02)
    for x in (-sx / 2 + 0.08, sx / 2 - 0.08):
        box("Crate_Corner", (x, 0, sz * 0.5), (0.1, sy + 0.03, sz * 0.98), M["steel_dark"], parent=g)
    for x in (-sx * 0.25, sx * 0.25):
        box("Crate_Latch", (x, -sy / 2 - 0.03, sz * 0.82), (0.1, 0.04, 0.14), M["steel"], parent=g)
    for x in (-sx / 2 - 0.02, sx / 2 + 0.02):
        box("Crate_Handle", (x, 0, sz * 0.6), (0.04, 0.3, 0.05), M["steel"], parent=g)
    if elite:
        box("Crate_GlowStrip", (0, -sy / 2 - 0.006, sz * 0.62), (sx * 0.62, 0.01, 0.025), M["core"], parent=g)
        box("Crate_GlowStripLid", (0, 0, sz + 0.001), (sx * 0.62, 0.025, 0.01), M["core"], parent=g)
        box("Crate_Screen", (0, -sy / 2 - 0.012, sz * 0.36), (0.36, 0.02, 0.16), M["black"], parent=g)
        text("Crate_ScreenText", "LOCKED 15:00", (0, -sy / 2 - 0.025, sz * 0.36), 0.05, M["laser"], (RAD(90), 0, 0), parent=g, extrude=0.003)
    else:
        box("Crate_Band", (0, -sy / 2 - 0.004, sz * 0.5), (sx * 0.5, 0.01, 0.08), M["orange"], parent=g)
    return g


def gold_bar(name, loc, rz, parent):
    return frustum(name, loc, (0.26, 0.11), (0.22, 0.08), 0.06, M["gold"], rot=(0, 0, rz), parent=parent)


def pallet(name, parent, w=1.2, d=1.0):
    for i, y in enumerate((-d / 2 + 0.05, 0, d / 2 - 0.05)):
        box(f"{name}_Runner_{i}", (0, y, 0.05), (w, 0.1, 0.1), M["wood"], parent=parent)
    for i in range(6):
        x = -w / 2 + 0.07 + i * (w - 0.14) / 5
        box(f"{name}_Board_{i}", (x, 0, 0.12), (0.14, d, 0.03), M["wood"], parent=parent)


def gold_pallet(name, loc, rz, parent, layers=4):
    g = group(name, loc=loc, rot=(0, 0, rz), parent=parent)
    pallet("Pallet", g)
    z = 0.135 + 0.03
    for L in range(layers):
        cols, rows = 4 - L % 2, 8 - L
        for i in range(cols):
            for j in range(rows):
                x = (i - (cols - 1) / 2) * 0.27
                y = (j - (rows - 1) / 2) * 0.115
                gold_bar(f"GoldBar_{L}_{i}_{j}", (x, y, z), 0.0, g)
        z += 0.06
    # a steel strap over the stack
    for x in (-0.3, 0.3):
        box(f"Strap_{x:+.1f}", (x, 0, 0.135 + 0.03 * 2 * layers / 2 + 0.005), (0.03, 0.96, 0.06 * layers + 0.02), M["steel_dark"], parent=g)
    return g


def shelf_unit(name, loc, rz, parent, length=2.4, depth=0.6, levels=(0.15, 0.8, 1.45, 2.1)):
    g = group(name, loc=loc, rot=(0, 0, rz), parent=parent)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Shelf_Post", (sx * (length / 2 - 0.03), sy * (depth / 2 - 0.03), 1.2), (0.05, 0.05, 2.4), M["steel_dark"], parent=g)
    for k, z in enumerate(levels):
        box(f"Shelf_Board_{k}", (0, 0, z), (length, depth, 0.035), M["steel"], parent=g)
        box(f"Shelf_Lip_{k}", (0, -depth / 2, z + 0.03), (length, 0.02, 0.06), M["orange"], parent=g)
        x = -length / 2 + 0.1
        while True:
            kind = rng.choice(["ammo", "ammo", "med", "parts", "gold", "cash", "gap"])
            w = {"ammo": 0.3, "med": 0.34, "parts": 0.45, "gold": 0.3, "cash": 0.22, "gap": 0.2}[kind]
            if x + w > length / 2 - 0.05:
                break
            cx, zz = x + w / 2, z + 0.0175
            if kind == "ammo":
                for s in range(rng.randint(1, 3)):
                    box("AmmoCan", (cx, 0.02, zz + 0.09 + s * 0.18), (0.28, 0.16, 0.18), M["crate_green"], parent=g, bevel=0.008)
                    box("AmmoCan_Handle", (cx, 0.02, zz + 0.185 + s * 0.18), (0.12, 0.03, 0.015), M["steel"], parent=g)
            elif kind == "med":
                box("MedKit", (cx, 0, zz + 0.08), (0.32, 0.22, 0.16), M["med_white"], parent=g, bevel=0.01)
                box("MedKit_CrossA", (cx, -0.111, zz + 0.08), (0.1, 0.005, 0.03), M["med_red"], parent=g)
                box("MedKit_CrossB", (cx, -0.111, zz + 0.08), (0.03, 0.005, 0.1), M["med_red"], parent=g)
            elif kind == "parts":
                box("PartsBox", (cx, 0, zz + 0.13), (0.43, 0.34, 0.26), M["wood"], parent=g, bevel=0.01)
                box("PartsBox_Label", (cx, -0.171, zz + 0.16), (0.2, 0.005, 0.08), M["paper"], parent=g)
            elif kind == "gold":
                for s in range(rng.randint(2, 4)):
                    gold_bar("ShelfGold", (cx, (s % 2) * 0.12 - 0.06, zz + 0.03 + (s // 2) * 0.06), (s // 2) * PI / 2 * 0, g)
            elif kind == "cash":
                for s in range(rng.randint(2, 5)):
                    box("CashBrick", (cx, 0, zz + 0.02 + s * 0.04), (0.17, 0.08, 0.04), M["cash"], parent=g)
                    box("CashBand", (cx, 0, zz + 0.02 + s * 0.04), (0.03, 0.082, 0.042), M["paper"], parent=g)
            x += w + 0.04
    return g


def rifle(name, loc, parent, rz=0.0):
    g = group(name, loc=loc, rot=(0, 0, rz), parent=parent)
    box("Rifle_Stock", (0, -0.38, -0.02), (0.05, 0.24, 0.1), M["black"], parent=g)
    box("Rifle_Receiver", (0, -0.12, 0), (0.06, 0.3, 0.1), M["steel_dark"], parent=g)
    box("Rifle_Handguard", (0, 0.13, 0.005), (0.06, 0.22, 0.07), M["crate_green"], parent=g)
    cyl("Rifle_Barrel", (0, 0.36, 0.01), 0.013, 0.28, M["steel_dark"], rot=Y90, parent=g, seg=10)
    box("Rifle_Mag", (0, -0.06, -0.1), (0.04, 0.07, 0.14), M["black"], rot=(RAD(-12), 0, 0), parent=g)
    box("Rifle_Grip", (0, -0.2, -0.08), (0.04, 0.05, 0.1), M["black"], rot=(RAD(20), 0, 0), parent=g)
    box("Rifle_Optic", (0, -0.1, 0.07), (0.035, 0.12, 0.04), M["steel_dark"], parent=g)
    return g


def barrel(name, loc, mat, parent, tipped=False, rz=0.0):
    rot = (RAD(90), 0, rz) if tipped else (0, 0, rz)
    g = group(name, loc=loc, rot=rot, parent=parent)
    cyl("Barrel_Body", (0, 0, 0.45), 0.29, 0.9, mat, parent=g, seg=24)
    for z in (0.02, 0.3, 0.6, 0.88):
        ring("Barrel_Rib", (0, 0, z), 0.305, 0.28, 0.035, mat, parent=g, seg=24)
    cyl("Barrel_Cap", (0.14, 0, 0.905), 0.04, 0.02, M["steel"], parent=g, seg=10)
    return g


# ---------------------------------------------------------------- loot
g_loot = group("Loot", parent=root)

g_shelves = group("Shelving", parent=g_loot)
shelf_unit("Shelf_L1", (-6.62, 2.4, 0), RAD(-90), g_shelves)
shelf_unit("Shelf_L2", (-6.62, 6.0, 0), RAD(-90), g_shelves)

if LEVEL >= 2:
    gold_pallet("GoldPallet_B", (3.7, 5.2, 0), RAD(-12), g_loot, layers=LEVEL)
if LEVEL >= 3:
    gold_pallet("GoldPallet_A", (-3.4, 5.6, 0), RAD(8), g_loot)
for k in range(2 + 2 * LEVEL):
    gold_bar(f"LooseGold_{k}", (3.0 + rng.uniform(-0.4, 0.4), 4.3 + rng.uniform(-0.4, 0.4), 0.03), rng.uniform(0, PI), g_loot)

if LEVEL >= 2:
    military_crate("EliteCrate", (0, 6.3, 0), RAD(6), g_loot, size=(1.4, 0.8, 0.7), elite=True)
else:
    military_crate("MilitaryCrate_Center", (0, 6.3, 0), RAD(6), g_loot)
military_crate("MilitaryCrate_A", (-5.95, 9.6, 0), RAD(90), g_loot)
if LEVEL >= 2:
    military_crate("MilitaryCrate_B", (-5.95, 9.6, 0.6), RAD(84), g_loot)
military_crate("MilitaryCrate_C", (3.9, 8.7, 0), RAD(-18), g_loot)
military_crate("MilitaryCrate_D", (-4.6, 2.6, 0), RAD(12), g_loot)

# cash table
tbl = group("CashTable", loc=(-3.7, 9.4, 0), rot=(0, 0, RAD(4)), parent=g_loot)
box("Table_Top", (0, 0, 0.88), (1.5, 0.75, 0.04), M["steel"], parent=tbl, bevel=0.005)
for sx in (-1, 1):
    for sy in (-1, 1):
        box("Table_Leg", (sx * 0.7, sy * 0.33, 0.43), (0.05, 0.05, 0.86), M["steel_dark"], parent=tbl)
box("Table_Shelf", (0, 0, 0.25), (1.4, 0.65, 0.03), M["steel_dark"], parent=tbl)
for i in range(4):
    for j in range(2):
        for s in range(rng.randint(3, 7)):
            p = (-0.55 + i * 0.26, -0.15 + j * 0.3, 0.92 + s * 0.04)
            box("CashBrick", p, (0.17, 0.08, 0.04), M["cash"], rot=(0, 0, rng.uniform(-0.1, 0.1)), parent=tbl)
            box("CashBand", p, (0.03, 0.082, 0.042), M["paper"], rot=(0, 0, 0), parent=tbl)
for k in range(3):
    gold_bar("TableGold", (0.5, -0.2 + k * 0.13, 0.93), 0.0, tbl)

# weapon rack on the right wall
wr = group("WeaponRack", loc=(6.9, 6.0, 0), parent=g_loot)
box("Rack_Board", (0, 0, 1.55), (0.06, 2.9, 1.9), M["steel_dark"], parent=wr, bevel=0.01)
box("Rack_Header", (-0.03, 0, 2.55), (0.1, 2.9, 0.12), M["orange"], parent=wr)
box("Rack_Shelf", (-0.25, 0, 0.55), (0.5, 2.9, 0.04), M["steel"], parent=wr)
for sy in (-1, 1):
    box("Rack_Leg", (-0.47, sy * 1.4, 0.27), (0.04, 0.04, 0.54), M["steel_dark"], parent=wr)
for r, z in enumerate((0.95, 1.5, 2.05)[:LEVEL]):
    for c, y in enumerate((-0.72, 0.72)):
        for py in (-0.2, 0.25):
            box("Rack_Peg", (-0.08, y + py, z - 0.07), (0.1, 0.03, 0.03), M["steel"], parent=wr)
        rifle(f"Rifle_{r}_{c}", (-0.08, y, z), wr)
lau = group("Launcher", loc=(-0.12, 0, 2.35), parent=wr) if LEVEL >= 3 else None
if lau: cyl("Launcher_Tube", (0, 0, 0), 0.075, 1.1, M["crate_green"], rot=Y90, parent=lau, seg=16)
if lau: cyl("Launcher_Muzzle", (0, 0.55, 0), 0.09, 0.08, M["steel_dark"], rot=Y90, parent=lau, seg=16)
if lau: box("Launcher_Grip", (0, -0.1, -0.11), (0.04, 0.06, 0.12), M["black"], parent=lau)
for i in range(4):
    for s in range(2):
        box("RackAmmo", (-0.25, -1.1 + i * 0.7, 0.66 + s * 0.18), (0.16, 0.28, 0.18), M["crate_green"], parent=wr, bevel=0.008)

# barrels
g_barrels = group("Barrels", parent=g_loot)
barrel("Barrel_0", (6.35, 9.0, 0), M["barrel_blue"], g_barrels)
barrel("Barrel_1", (6.35, 9.65, 0), M["barrel_red"], g_barrels, rz=0.6)
barrel("Barrel_2", (5.75, 9.3, 0), M["barrel_blue"], g_barrels, rz=1.2)
barrel("Barrel_3", (6.35, 10.6, 0), M["barrel_blue"], g_barrels)
barrel("Barrel_4", (5.1, 10.9, 0.29), M["barrel_red"], g_barrels, tipped=True, rz=RAD(70))

# terminal desk
td = group("Terminal", loc=(6.25, 2.3, 0), parent=g_loot)
box("Desk_Top", (0, 0, 0.9), (0.8, 1.6, 0.05), M["steel"], parent=td, bevel=0.005)
for sy in (-1, 1):
    box("Desk_Side", (0, sy * 0.76, 0.44), (0.76, 0.05, 0.88), M["steel_dark"], parent=td)
box("Monitor", (0.22, 0, 1.2), (0.08, 0.64, 0.42), M["black"], parent=td, bevel=0.01)
box("Monitor_Stand", (0.25, 0, 0.98), (0.06, 0.12, 0.12), M["black"], parent=td)
box("Monitor_Screen", (0.175, 0, 1.2), (0.01, 0.58, 0.36), M["screen"], parent=td)
box("Keyboard", (-0.05, 0, 0.935), (0.18, 0.5, 0.02), M["black"], parent=td)
text("Screen_Text", f"LEVEL {LEVEL} VAULT\nSTATUS: OPEN", (0.168, 0, 1.2), 0.06, M["black"], (RAD(90), 0, RAD(-90)), parent=td, extrude=0.002)

# safe-deposit locker walls
g_lock = group("DepositLockers", parent=g_loot)
open_cells = set(list([(1, 2), (4, 0), (7, 4), (2, 5), (8, 1), (5, 3), (0, 4)])[:1 + 2 * LEVEL])
for side, x0 in (("L", -6.6), ("R", 2.1)):
    cols, rows, cw, ch = 10, 6, 0.45, 0.4
    width = cols * cw
    cab = group(f"Lockers_{side}", loc=(x0 + width / 2, 11.7, 0), parent=g_lock)
    box("Locker_Cabinet", (0, 0, 1.35), (width + 0.1, 0.5, 2.7), M["steel_dark"], parent=cab, bevel=0.01)
    box("Locker_Plinth", (0, -0.02, 0.1), (width + 0.1, 0.5, 0.2), M["black"], parent=cab)
    for i in range(cols):
        for j in range(rows):
            x = -width / 2 + cw / 2 + i * cw
            z = 0.2 + ch / 2 + j * ch + 0.05
            key = (i, j) if side == "L" else (cols - 1 - i, j)
            if key in open_cells:
                box("Locker_Cavity", (x, -0.252, z), (cw - 0.05, 0.005, ch - 0.05), M["black"], parent=cab)
                hinge_x = x - cw / 2 + 0.02
                d = group("Locker_OpenDoor", loc=(hinge_x, -0.26, z), rot=(0, 0, RAD(-105)), parent=cab)
                box("Locker_Door", (cw / 2 - 0.02, -0.015, 0), (cw - 0.04, 0.03, ch - 0.04), M["steel"], parent=d)
                if (i + j) % 2:
                    gold_bar("Locker_Gold", (x, -0.3, z - ch / 2 + 0.06), 0.0, cab)
                else:
                    box("Locker_Cash", (x, -0.29, z - ch / 2 + 0.06), (0.17, 0.08, 0.04), M["cash"], parent=cab)
            else:
                box("Locker_Door", (x, -0.265, z), (cw - 0.04, 0.03, ch - 0.04), M["steel"], parent=cab)
                cyl("Locker_Keyhole", (x + cw / 2 - 0.08, -0.285, z), 0.022, 0.015, M["steel_dark"], rot=Y90, parent=cab, seg=10)
                box("Locker_Tag", (x - 0.06, -0.282, z + ch / 2 - 0.07), (0.12, 0.005, 0.04), M["paper"], parent=cab)

# ---------------------------------------------------------------- the core (centerpiece)
g_core = group("PrismCore" if LEVEL >= 3 else "Dais", parent=root)
box("Dais", (0, 10.3, 0.15), (4.0, 3.2, 0.3), M["concrete_dark"], parent=g_core, bevel=0.02)
box("Dais_Trim", (0, 8.7, 0.29), (4.02, 0.06, 0.02), M["steel"], parent=g_core)
box("Dais_Step", (0, 8.45, 0.075), (2.0, 0.5, 0.15), M["concrete_dark"], parent=g_core, bevel=0.01)
for i in range(13):
    x = -1.95 + i * 0.3 + 0.15
    box(f"Dais_Hazard_{i}", (x, 8.69, 0.2), (0.3, 0.012, 0.12), M["yellow"] if i % 2 == 0 else M["black"], parent=g_core)
if LEVEL < 3:
    military_crate("DaisCrate", (0, 10.3, 0.3), 0.0, g_core, size=(1.4, 0.8, 0.7), elite=LEVEL == 2)
if LEVEL >= 3:
    cyl("Pedestal", (0, 10.3, 0.8), 0.42, 1.0, M["steel_dark"], parent=g_core, seg=32)
    ring("Pedestal_Band", (0, 10.3, 0.9), 0.44, 0.4, 0.08, M["orange"], parent=g_core, seg=32)
    cyl("Pedestal_Top", (0, 10.3, 1.33), 0.55, 0.06, M["steel"], parent=g_core, seg=32)
    case_c = Vector((0, 10.3, 1.76))
    box("Case_Glass", case_c, (0.8, 0.8, 0.8), M["glass"], parent=g_core)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Case_Post", case_c + Vector((sx * 0.4, sy * 0.4, 0)), (0.04, 0.04, 0.82), M["steel"], parent=g_core)
    box("Case_Lid", case_c + Vector((0, 0, 0.42)), (0.86, 0.86, 0.04), M["steel"], parent=g_core)
    sphere("Core_Prism", case_c, 0.17, M["core"], parent=g_core, subdiv=1)
    torus("Core_Orbit_A", case_c, 0.28, 0.012, M["steel"], rot=(RAD(70), 0, RAD(20)), parent=g_core, seg=32, seg2=6)
    torus("Core_Orbit_B", case_c, 0.3, 0.012, M["steel"], rot=(RAD(-60), RAD(30), 0), parent=g_core, seg=32, seg2=6)
    # laser fence around the dais
    posts = [(-1.8, 9.0), (1.8, 9.0), (1.8, 11.5), (-1.8, 11.5)]
    for k, (x, y) in enumerate(posts):
        box(f"LaserPost_{k}", (x, y, 0.9), (0.12, 0.12, 1.2), M["steel_dark"], parent=g_core, bevel=0.01)
        box(f"LaserPost_Tip_{k}", (x, y, 1.53), (0.13, 0.13, 0.06), M["laser"], parent=g_core)
    for a, b in ((0, 1), (1, 2), (3, 0)):
        for z in (0.6, 0.9, 1.2):
            (x0, y0), (x1, y1) = posts[a], posts[b]
            between(f"Laser_{a}{b}_{z}", (x0, y0, z), (x1, y1, z), 0.012, M["laser"], parent=g_core)


# --------------------------------------------------------------------------
# Consolidate: merge the loose primitives under each group into one mesh so the
# FBX has ~100 tidy objects instead of ~1300. A group that ends up holding just
# that one mesh is replaced by it (keeping the group's pivot, e.g. door hinges).
# --------------------------------------------------------------------------
def depth(o):
    d = 0
    while o.parent:
        d, o = d + 1, o.parent
    return d


def consolidate():
    merged = set()
    empties = sorted((o for o in scene.objects if o.type == "EMPTY"), key=depth, reverse=True)
    for E in empties:
        kids = [c for c in E.children if c.type == "MESH" and c.name not in merged]
        if not kids:
            continue
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        inv = E.matrix_world.inverted()
        bm, mats = bmesh.new(), []
        for c in kids:
            me = bpy.data.meshes.new_from_object(c.evaluated_get(dg))  # bakes bevels
            me.transform(inv @ c.matrix_world)
            remap = []
            for m in me.materials:
                if m not in mats:
                    mats.append(m)
                remap.append(mats.index(m))
            for poly in me.polygons:
                poly.material_index = remap[poly.material_index] if remap else 0
            bm.from_mesh(me)
            bpy.data.meshes.remove(me)
        for c in kids:
            bpy.data.objects.remove(c)
        name = E.name
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for m in mats:
            me.materials.append(m)
        if not E.children and E.parent is not None:
            E.name = name + "_old"
            o = bpy.data.objects.new(name, me)
            scene.collection.objects.link(o)
            o.parent = E.parent
            o.matrix_parent_inverse = E.matrix_parent_inverse.copy()
            o.location, o.rotation_euler, o.scale = E.location, E.rotation_euler, E.scale
            bpy.data.objects.remove(E)
            merged.add(o.name)
        else:
            o = bpy.data.objects.new(name + "_Mesh", me)
            scene.collection.objects.link(o)
            o.parent = E
            merged.add(o.name)


consolidate()

# --------------------------------------------------------------------------
# Export
# --------------------------------------------------------------------------
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
tris = sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons)
           for o in scene.objects if o.type == "MESH")
meshes = sum(1 for o in scene.objects if o.type == "MESH")
print(f"[vault] {meshes} mesh objects, ~{tris} triangles")

bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT_DIR, f"{NAME}.fbx"),
    object_types={"EMPTY", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    apply_scale_options="FBX_SCALE_UNITS",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
)
bpy.ops.export_scene.gltf(
    filepath=os.path.join(OUT_DIR, f"{NAME}.glb"),
    export_format="GLB",
    export_apply=True,
)


# --------------------------------------------------------------------------
# Preview renders (lights/cameras are only for the renders, not exported)
# --------------------------------------------------------------------------
def add_light(name, kind, loc, energy, size=1.0, color=(1, 1, 1), rot=(0, 0, 0)):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == "AREA":
        ld.size = size
    elif kind in ("POINT", "SPOT"):
        ld.shadow_soft_size = size
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    o.rotation_euler = rot
    scene.collection.objects.link(o)
    return o


def add_camera(name, loc, target, lens):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    o = bpy.data.objects.new(name, cd)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(o)
    return o


for x in (-3.2, 3.2):
    for y in (2.0, 6.0, 10.0):
        add_light(f"L_{x}_{y}", "AREA", (x, y, 4.3), 170, size=1.2, color=(1.0, 0.92, 0.8))
add_light("L_core", "POINT", (0, 10.3, 1.76), 60, size=0.1, color=(0.2, 0.85, 1.0))
add_light("L_tunnel", "AREA", (0, -2.3, 4.2), 180, size=2.0, color=(1.0, 0.85, 0.7))
add_light("L_alarm", "POINT", (0, 0.4, 3.9), 25, size=0.1, color=(1.0, 0.1, 0.05))

world = bpy.data.worlds.new("World")
scene.world = world
try:
    world.use_nodes = True
except AttributeError:
    pass
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.02, 0.022, 0.025, 1)
bg.inputs["Strength"].default_value = 1.0

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, f"{NAME}.blend"), check_existing=False, compress=True)
if os.path.exists(os.path.join(OUT_DIR, f"{NAME}.blend1")):
    os.remove(os.path.join(OUT_DIR, f"{NAME}.blend1"))

if DO_RENDER:
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = int(os.environ.get("VAULT_SAMPLES", "64"))
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    shots = [
        ("preview_entrance", (-0.9, -3.7, 1.75), (0.6, 8.0, 1.2), 22),
        ("preview_interior", (-6.3, 0.7, 3.3), (1.2, 8.5, 0.6), 16),
        ("preview_core", (4.6, 7.2, 2.1), (-0.8, 10.8, 1.2), 20),
        ("preview_door", (-3.6, 5.8, 2.0), (1.6, 2.2, 1.9), 20),
    ]
    only = os.environ.get("VAULT_SHOTS")
    for name, loc, target, lens in shots:
        if only and name not in only.split(","):
            continue
        scene.camera = add_camera("Cam_" + name, loc, target, lens)
        scene.render.filepath = os.path.join(OUT_DIR, f"{name}_L{LEVEL}.png")
        bpy.ops.render.render(write_still=True)
        print(f"[vault] rendered {name}")
