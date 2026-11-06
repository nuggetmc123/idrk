#!/usr/bin/env python3
"""Generates a walk-in warehouse as warehouse.glb and warehouse.obj (+ .mtl), using the
textures in textures/ (made by make_textures.py).
Every piece is its own object, named after its shape (Cube, Cube.001, ..., Triangle, Triangle.001).

No dependencies: run `python3 make_warehouse.py`. Change the numbers below to resize it.
Units are meters, +Y is up, the floor's top surface is at y = 0 and the building is
centered on x = 0, z = 0. The garage door faces +Z.
"""
import json, math, os, struct
from texture_names import color_file, normal_file

# ---- size settings (meters) -------------------------------------------------
WIDTH = 18.0          # along X
DEPTH = 26.0          # along Z
WALL_H = 6.0          # wall height (eaves)
ROOF_RISE = 1.6       # how much higher the ridge is than the eaves
WALL_T = 0.3          # wall thickness
FLOOR_T = 0.3         # floor slab thickness (top at y = 0)
ROOF_T = 0.2          # roof sheet thickness
OVERHANG = 0.6        # roof overhang past the walls
BIG_DOOR = (5.0, 4.2)     # width, height of the garage door opening, front wall (+Z)
GARAGE_OPEN = 2.4         # how high the garage door is rolled up (0 = closed)
SMALL_DOOR = (1.2, 2.4)   # width, height, back wall (-Z), near the right corner
WINDOW = (2.0, 0.9, 4.3)  # width, height, sill height: open windows high on the side walls
WINDOWS_PER_SIDE = 6
SIDE_OPENING = (-1.5, 7.5)  # z range of the huge open doorway in the left wall (its top is the window sill)
ROOMS = (-1.7, -7.2)        # office + bathroom block in the back-left corner: its right edge (x), front edge (z)
ROOM_H = 3.0                # height of the room walls; the block has a flat ceiling you can climb onto
# shelf rows: x range, row name, z range (each row is cut into SHELF_UNIT_LEN units)
SHELF_ROWS = [(7.6, 8.6, "RightWall", -10.0, 2.5), (1.6, 2.8, "Middle", -8.0, 2.0)]
SHELF_UNIT_LEN = 2.5
SHELF_LEVELS = [0.15, 1.05, 1.95, 2.85]   # height of each shelf board

# Realistic look: most materials use a tiling photo-style texture from textures/
# (made by make_textures.py) with a matching normal map for bumps and ridges.
# tint multiplies the texture (#FFFFFF = the texture's own colors); tile = meters per repeat.
MATERIALS = {
    #  name           tint      metallic roughness textured tile
    "Concrete":     ("#FFFFFF", 0.0, 0.85, True,  4.0),   # sealed concrete slab, joint every 4 m
    "MetalWall":    ("#FFFFFF", 0.3, 0.55, True,  2.0),   # corrugated steel cladding
    "Roof":         ("#FFFFFF", 0.4, 0.50, True,  4.0),   # galvanized corrugated roofing
    "Trim":         ("#FFFFFF", 0.1, 0.55, True,  1.0),   # worn safety-yellow paint
    "GarageDoor":   ("#FFFFFF", 0.2, 0.45, True,  0.6),   # white steel sectional door
    "Steel":        ("#FFFFFF", 0.6, 0.45, True,  1.0),   # galvanized steel: ladder, tracks, tap
    "ShelfFrame":   ("#FFFFFF", 0.1, 0.50, True,  1.0),   # orange pallet-rack paint
    "Wood":         ("#FFFFFF", 0.0, 0.75, True,  1.0),   # shelf boards, desk
    "RoomWall":     ("#FFFFFF", 0.0, 0.90, True,  2.0),   # ceiling of the office/bathroom block
    "OfficeWall":   ("#FFFFFF", 0.0, 0.90, True,  2.0),   # painted drywall
    "BathroomWall": ("#FFFFFF", 0.0, 0.25, True,  0.6),   # white 15 cm wall tiles
    "Carpet":       ("#FFFFFF", 0.0, 1.00, True,  1.0),   # office carpet
    "Tile":         ("#FFFFFF", 0.0, 0.30, True,  0.6),   # 30 cm floor tiles
    "Cabinet":      ("#FFFFFF", 0.3, 0.50, True,  1.0),   # old beige-grey filing cabinet
    "Porcelain":    ("#EDEDE8", 0.0, 0.12, False, 1.0),   # toilet, sink
    "Plastic":      ("#1E1F22", 0.0, 0.55, False, 1.0),   # chair, computer
    "Mirror":       ("#E8EEF2", 1.0, 0.03, False, 1.0),   # bathroom mirror
}

def linear(hex_color):
    """'#RRGGBB' (sRGB, as color pickers show it) -> linear RGB, which the file formats store."""
    def ch(c):
        c = int(c, 16) / 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(ch(hex_color[i:i+2]) for i in (1, 3, 5))

# ---- geometry helpers -------------------------------------------------------
def add(a, b): return (a[0]+b[0], a[1]+b[1], a[2]+b[2])
def sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def mul(a, s): return (a[0]*s, a[1]*s, a[2]*s)
def dot(a, b): return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(v):
    l = math.sqrt(dot(v, v)) or 1.0
    return (v[0]/l, v[1]/l, v[2]/l)
def centroid(pts): return mul(tuple(map(sum, zip(*pts))), 1/len(pts))

class Mesh:
    def __init__(self, tile=1.0):
        self.pos, self.nrm, self.uv, self.idx, self.tile = [], [], [], [], tile
        self.shape = "Cube"                      # what the piece gets named in the files
    def face(self, pts, inside):
        """Adds a flat convex polygon, wound so it faces away from the point `inside`."""
        n = norm(cross(sub(pts[1], pts[0]), sub(pts[2], pts[0])))
        if dot(n, sub(centroid(pts), inside)) < 0:
            pts, n = pts[::-1], mul(n, -1)
        ax = max(range(3), key=lambda i: abs(n[i]))     # planar UV projection
        u_ax, v_ax = [(2, 1), (0, 2), (0, 1)][ax]
        base = len(self.pos)
        for p in pts:
            self.pos.append(p); self.nrm.append(n)
            self.uv.append((p[u_ax]/self.tile, -p[v_ax]/self.tile))
        for i in range(1, len(pts)-1):
            self.idx += [base, base+i, base+i+1]

    def hexa(self, c):
        """A closed 6-sided solid from 8 corners: one end 0-3, the other 4-7 (same order)."""
        mid = centroid(c)
        self.face(c[0:4], mid); self.face(c[4:8], mid)
        for i in range(4):
            j = (i+1) % 4
            self.face([c[i], c[j], c[j+4], c[i+4]], mid)

    def box(self, x0, x1, y0, y1, z0, z1):
        self.hexa([(x0,y0,z0),(x1,y0,z0),(x1,y0,z1),(x0,y0,z1),
                   (x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)])

    def beam(self, p0, p1, w, h):
        """A w x h square bar from point p0 to point p1 (any direction)."""
        d = norm(sub(p1, p0))
        side = norm(cross(d, (0, 1, 0))) if abs(d[1]) < 0.99 else (1, 0, 0)
        up = cross(side, d)
        ring = [add(mul(side, sx*w/2), mul(up, sy*h/2)) for sx, sy in ((-1,-1),(1,-1),(1,1),(-1,1))]
        self.hexa([add(p0, r) for r in ring] + [add(p1, r) for r in ring])

    def prism_z(self, poly, z0, z1):
        """Extrudes a convex (x, y) polygon from z0 to z1."""
        if len(poly) == 3: self.shape = "Triangle"
        mid = centroid([(x, y, (z0+z1)/2) for x, y in poly])
        self.face([(x, y, z1) for x, y in poly], mid)
        self.face([(x, y, z0) for x, y in poly], mid)
        for i in range(len(poly)):
            (ax, ay), (bx, by) = poly[i], poly[(i+1) % len(poly)]
            self.face([(ax, ay, z1), (ax, ay, z0), (bx, by, z0), (bx, by, z1)], mid)

# ---- build the warehouse ----------------------------------------------------
hw, hd, t = WIDTH/2, DEPTH/2, WALL_T
objects = {}      # object name -> {material: Mesh}; an object can use several materials

def obj(name, material):
    parts = objects.setdefault(name, {})
    if material not in parts:
        parts[material] = Mesh(MATERIALS[material][4])
    return parts[material]

# Floor slab, a little bigger than the building so the walls sit on it.
obj("Floor", "Concrete").box(-hw-0.5, hw+0.5, -FLOOR_T, 0.0, -hd-0.5, hd+0.5)

def wall_with_door(prefix, z0, z1, door_x, door_w, door_h, facing_back=False):
    """A wall along X, split into the pieces left of, right of, and above a door.
    Left/right are as seen standing outside, facing that wall."""
    a, b = door_x - door_w/2, door_x + door_w/2
    left, right = ("RightOfDoor", "LeftOfDoor") if facing_back else ("LeftOfDoor", "RightOfDoor")
    obj(f"{prefix}_{left}", "MetalWall").box(-hw, a, 0, WALL_H, z0, z1)
    obj(f"{prefix}_{right}", "MetalWall").box(b, hw, 0, WALL_H, z0, z1)
    obj(f"{prefix}_AboveDoor", "MetalWall").box(a, b, door_h, WALL_H, z0, z1)

bw, bh = BIG_DOOR
sw, sh = SMALL_DOOR
wall_with_door("FrontWall", hd-t, hd, 0.0, bw, bh)
wall_with_door("BackWall", -hd, -hd+t, hw-3.0, sw, sh, facing_back=True)

# Side walls run between the front and back walls, with a strip of open windows.
# LeftWall/RightWall are on your left/right when you stand outside facing the garage door.
ww, wh, wy = WINDOW
za, zb = -hd+t, hd-t
spacing = (zb - za) / WINDOWS_PER_SIDE
gaps = [(za + spacing*(i+0.5) - ww/2, za + spacing*(i+0.5) + ww/2) for i in range(WINDOWS_PER_SIDE)]
# The left wall has a huge open doorway below its windows instead of a solid lower part.
# (Seen from outside facing the left wall, the back of the building is on your left.)
oz0, oz1 = SIDE_OPENING
obj("LeftWall_LeftOfOpening", "MetalWall").box(-hw, -hw+t, 0, wy, za, oz0)
obj("LeftWall_RightOfOpening", "MetalWall").box(-hw, -hw+t, 0, wy, oz1, zb)
obj("RightWall_BelowWindows", "MetalWall").box(hw-t, hw, 0, wy, za, zb)
for prefix, x0, x1 in [("LeftWall", -hw, -hw+t), ("RightWall", hw-t, hw)]:
    obj(f"{prefix}_AboveWindows", "MetalWall").box(x0, x1, wy+wh, WALL_H, za, zb)
    edges = [za] + [c for g in gaps for c in g] + [zb]
    for i in range(0, len(edges), 2):            # the posts between (and beside) windows
        obj(f"{prefix}_WindowPost_{i//2 + 1}", "MetalWall").box(x0, x1, wy, wy+wh, edges[i], edges[i+1])

# Gable triangles on top of the front and back walls.
gable = [(-hw, WALL_H), (hw, WALL_H), (0.0, WALL_H+ROOF_RISE)]
obj("FrontWall_Gable", "MetalWall").prism_z(gable, hd-t, hd)
obj("BackWall_Gable", "MetalWall").prism_z(gable, -hd, -hd+t)

# Two sloped roof sheets meeting at the ridge (sitting on top of the walls).
slope = ROOF_RISE / hw
eave_y = WALL_H - OVERHANG*slope                 # height of the roof's outer edge
lift = ROOF_T / math.cos(math.atan(slope))       # vertical thickness of a roof sheet
for side, name in ((-1, "Roof_Left"), (1, "Roof_Right")):
    xe, yr = side*(hw+OVERHANG), WALL_H + ROOF_RISE
    lo = [(xe, eave_y, -hd-OVERHANG), (0.0, yr, -hd-OVERHANG), (0.0, yr, hd+OVERHANG), (xe, eave_y, hd+OVERHANG)]
    obj(name, "Roof").hexa(lo + [(x, y+lift, z) for x, y, z in lo])

# Yellow frame around the garage door opening, on the outside.
f = 0.25
for name, (x0, x1, y0, y1) in [("DoorFrame_Left", (-bw/2-f, -bw/2, 0, bh+f)),
                               ("DoorFrame_Right", (bw/2, bw/2+f, 0, bh+f)),
                               ("DoorFrame_Top", (-bw/2, bw/2, bh, bh+f))]:
    obj(name, "Trim").box(x0, x1, y0, y1, hd, hd+0.08)

f = 0.25                                         # yellow frame around the side opening
for name, (z0, z1, y0, y1) in [("SideOpeningFrame_Left", (oz0-f, oz0, 0, wy)),
                               ("SideOpeningFrame_Right", (oz1, oz1+f, 0, wy)),
                               ("SideOpeningFrame_Top", (oz0-f, oz1+f, wy-f, wy))]:
    obj(name, "Trim").box(-hw-0.08, -hw, y0, y1, z0, z1)

# ---- garage door: ribbed roll-up door just inside the opening ----------------
gz = hd - t                                      # inside face of the front wall
slat = 0.3
door = obj("GarageDoor", "GarageDoor")
n_slats = round((bh - GARAGE_OPEN) / slat)
for i in range(n_slats):                         # alternate slat depth so it looks ribbed
    y0 = bh - (i+1)*slat
    door.box(-bw/2-0.05, bw/2+0.05, y0, y0+slat, gz-0.16, gz-(0.06 if i % 2 else 0.09))
door.box(-bw/2-0.05, bw/2+0.05, bh-n_slats*slat-0.08, bh-n_slats*slat, gz-0.18, gz-0.05)  # bottom bar
for name, x in (("GarageDoor_Track_Left", -bw/2-0.12), ("GarageDoor_Track_Right", bw/2+0.12)):
    obj(name, "Steel").box(x-0.08, x+0.08, 0, bh+0.1, gz-0.22, gz)
obj("GarageDoor_RollBox", "Steel").box(-bw/2-0.3, bw/2+0.3, bh, bh+0.6, gz-0.6, gz)

# ---- ladder up to the roof, outside the right wall ----------------------------
lx = hw + OVERHANG + 0.15                        # just past the roof's edge
lz = -hd + 3.5
ladder = obj("RoofLadder", "Steel")
top = eave_y + 1.1                               # rails stick up past the roof edge to grab
for dz in (-0.25, 0.25):
    ladder.box(lx-0.03, lx+0.03, 0, top, lz+dz-0.03, lz+dz+0.03)
y = 0.3
while y < top - 0.1:
    ladder.box(lx-0.02, lx+0.02, y-0.02, y+0.02, lz-0.25, lz+0.25)
    y += 0.3
for y in (1.5, 3.5, eave_y - 0.3):               # brackets holding it to the wall
    for dz in (-0.25, 0.25):
        ladder.box(hw, lx, y-0.03, y+0.03, lz+dz-0.03, lz+dz+0.03)

# ---- shelves: rows of tall shelving units, with aisles between them -----------
for x0, x1, row, rz0, rz1 in SHELF_ROWS:
    for u in range(round((rz1 - rz0) / SHELF_UNIT_LEN)):
        z0 = rz0 + u*SHELF_UNIT_LEN
        z1 = z0 + SHELF_UNIT_LEN
        name = f"Shelf_{row}_{u+1}"
        frame, wood = obj(name, "ShelfFrame"), obj(name, "Wood")
        top = SHELF_LEVELS[-1] + 0.25
        for x in (x0 + 0.04, x1 - 0.04):
            for z in (z0 + 0.04, z1 - 0.04):
                frame.box(x-0.04, x+0.04, 0, top, z-0.04, z+0.04)       # uprights
        for y in SHELF_LEVELS:
            for z in (z0 + 0.04, z1 - 0.04):                            # cross beams
                frame.box(x0, x1, y-0.08, y, z-0.04, z+0.04)
            wood.box(x0 + 0.02, x1 - 0.02, y, y+0.04, z0 + 0.08, z1 - 0.08)  # board

# ---- office + bathroom block in the back-left corner -------------------------
rx0, rx1 = -hw + t, ROOMS[0]                     # inside face of the left wall .. block's right edge
rz0, rz1 = -hd + t, ROOMS[1]                     # inside face of the back wall .. block's front edge
it = 0.15                                        # interior wall thickness
mx = rx0 + (rx1 - rx0) * 0.62                    # center of the office/bathroom divider wall
fz0, fz1 = rz1 - it, rz1                         # front wall's z range

def front_wall(prefix, a, b, door_x, window_x=None):
    wall = f"{prefix}Wall"                       # OfficeWall / BathroomWall material
    """Front wall of a room from x=a to x=b, with a doorway (0.9 x 2.1) and maybe a window."""
    holes = [("Door", door_x - 0.45, door_x + 0.45, 0.0, 2.1)]
    if window_x is not None:
        holes.append(("Window", window_x - 0.8, window_x + 0.8, 1.0, 2.0))
    holes.sort(key=lambda h: h[1])
    x, k = a, 0
    for label, h0, h1, y0, y1 in holes:
        k += 1
        obj(f"{prefix}_FrontWall_Section_{k}", wall).box(x, h0, 0, ROOM_H, fz0, fz1)
        obj(f"{prefix}_FrontWall_Above{label}", wall).box(h0, h1, y1, ROOM_H, fz0, fz1)
        if y0 > 0:
            obj(f"{prefix}_FrontWall_Below{label}", wall).box(h0, h1, 0, y0, fz0, fz1)
        x = h1
    obj(f"{prefix}_FrontWall_Section_{k+1}", wall).box(x, b, 0, ROOM_H, fz0, fz1)

front_wall("Office", rx0, mx, mx - 1.0, window_x=rx0 + 1.3)
front_wall("Bathroom", mx, rx1, (mx + rx1) / 2)
obj("Office_Bathroom_Wall", "OfficeWall").box(mx - it/2, mx, 0, ROOM_H, rz0, fz0)   # green on the office side
obj("Office_Bathroom_Wall", "BathroomWall").box(mx, mx + it/2, 0, ROOM_H, rz0, fz0) # blue on the bathroom side
obj("Bathroom_SideWall", "BathroomWall").box(rx1 - it, rx1, 0, ROOM_H, rz0, fz0)
obj("Rooms_Ceiling", "RoomWall").box(rx0, rx1, ROOM_H, ROOM_H + 0.15, rz0, rz1)
obj("Office_Floor", "Carpet").box(rx0, mx - it/2, 0, 0.02, rz0, fz0)
obj("Bathroom_Floor", "Tile").box(mx + it/2, rx1 - it, 0, 0.02, rz0, fz0)

# Office: desk against the back wall, a computer on it, a chair, and a filing cabinet.
dx = (rx0 + mx) / 2
desk = obj("Office_Desk", "Wood")
desk.box(dx - 0.8, dx + 0.8, 0.72, 0.76, rz0 + 0.1, rz0 + 0.85)
for x in (dx - 0.75, dx + 0.75):
    for z in (rz0 + 0.15, rz0 + 0.8):
        desk.box(x - 0.03, x + 0.03, 0, 0.72, z - 0.03, z + 0.03)
pc = obj("Office_Computer", "Plastic")
pc.box(dx - 0.3, dx + 0.3, 0.92, 1.28, rz0 + 0.3, rz0 + 0.34)       # monitor
pc.box(dx - 0.04, dx + 0.04, 0.76, 0.92, rz0 + 0.33, rz0 + 0.37)    # stand
pc.box(dx - 0.12, dx + 0.12, 0.76, 0.78, rz0 + 0.27, rz0 + 0.43)    # base
pc.box(dx - 0.25, dx + 0.25, 0.76, 0.78, rz0 + 0.5, rz0 + 0.66)     # keyboard
cz = rz0 + 1.3
chair = obj("Office_Chair", "Plastic")
chair.box(dx - 0.25, dx + 0.25, 0.45, 0.52, cz - 0.25, cz + 0.25)  # seat
chair.box(dx - 0.25, dx + 0.25, 0.52, 1.05, cz + 0.2, cz + 0.26)   # back
chair.box(dx - 0.03, dx + 0.03, 0.08, 0.45, cz - 0.03, cz + 0.03)  # post
chair.box(dx - 0.28, dx + 0.28, 0.0, 0.08, cz - 0.03, cz + 0.03)   # feet
chair.box(dx - 0.03, dx + 0.03, 0.0, 0.08, cz - 0.28, cz + 0.28)
cab = obj("Office_FilingCabinet", "Cabinet")
cab.box(rx0 + 0.05, rx0 + 0.6, 0, 1.3, rz0 + 2.3, rz0 + 2.8)
for y in (0.35, 0.75, 1.15):                     # drawer handles
    obj("Office_FilingCabinet", "Trim").box(rx0 + 0.6, rx0 + 0.63, y, y + 0.03, rz0 + 2.45, rz0 + 2.65)

# Bathroom: toilet against the back wall, sink and mirror on the side wall.
tx = mx + it/2 + 0.6
toilet = obj("Bathroom_Toilet", "Porcelain")
toilet.box(tx - 0.25, tx + 0.25, 0.4, 0.8, rz0, rz0 + 0.2)                 # tank
toilet.box(tx - 0.15, tx + 0.15, 0, 0.36, rz0 + 0.2, rz0 + 0.7)            # base
toilet.box(tx - 0.2, tx + 0.2, 0.36, 0.42, rz0 + 0.2, rz0 + 0.75)          # seat
sz = rz0 + 2.3
sink = obj("Bathroom_Sink", "Porcelain")
sink.box(rx1 - it - 0.55, rx1 - it, 0.8, 0.9, sz - 0.3, sz + 0.3)           # basin
sink.box(rx1 - it - 0.3, rx1 - it - 0.1, 0, 0.8, sz - 0.1, sz + 0.1)        # pedestal
obj("Bathroom_Sink", "Steel").box(rx1 - it - 0.12, rx1 - it, 0.9, 1.1, sz - 0.03, sz + 0.03)  # tap
obj("Bathroom_Mirror", "Mirror").box(rx1 - it - 0.02, rx1 - it, 1.2, 1.9, sz - 0.3, sz + 0.3)

# ---- writers ----------------------------------------------------------------
here = os.path.dirname(os.path.abspath(__file__))

# In the exported files every object is named after its shape, the way Blender names new
# objects: Cube, Cube.001, Cube.002, ... and Triangle, Triangle.001 for the gable ends.
# (The descriptive names above are only used inside this script and in the README's list.)
export_name, _count = {}, {}
for name, parts in objects.items():
    shape = "Triangle" if any(m.shape == "Triangle" for m in parts.values()) else "Cube"
    k = _count.get(shape, 0)
    export_name[name] = shape if k == 0 else f"{shape}.{k:03d}"
    _count[shape] = k + 1
ROOT_NAME = "Empty"                              # the parent object that holds everything

def write_obj():
    with open(os.path.join(here, "warehouse.mtl"), "w") as m:
        for name, (color, metal, rough, textured, tile) in MATERIALS.items():
            rgb = linear(color)
            m.write(f"newmtl {name}\nKd {rgb[0]:.3f} {rgb[1]:.3f} {rgb[2]:.3f}\nKs 0.1 0.1 0.1\nNs 20\nd 1\nillum 2\n"
                    f"Pr {rough:.2f}\nPm {metal:.2f}\n")
            if textured:
                m.write(f"map_Kd {color_file(name)}\nmap_Bump -bm 1.0 {normal_file(name)}\n")
            m.write("\n")
    with open(os.path.join(here, "warehouse.obj"), "w") as o:
        o.write("# Walk-in warehouse. Meters, Y up, floor top at y=0.\n"
                "mtllib warehouse.mtl\n")
        off = 1
        for name, parts in objects.items():
            o.write(f"o {export_name[name]}\n")
            for material, mesh in parts.items():
                o.write(f"usemtl {material}\n")
                o.writelines(f"v {p[0]:.4f} {p[1]:.4f} {p[2]:.4f}\n" for p in mesh.pos)
                o.writelines(f"vt {u:.4f} {v:.4f}\n" for u, v in mesh.uv)
                o.writelines(f"vn {n[0]:.4f} {n[1]:.4f} {n[2]:.4f}\n" for n in mesh.nrm)
                for i in range(0, len(mesh.idx), 3):
                    a, b, c = (k + off for k in mesh.idx[i:i+3])
                    o.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")
                off += len(mesh.pos)

def write_glb():
    bin_, views, accs, gl_meshes, nodes, mats = bytearray(), [], [], [], [], []
    def acc(data, fmt, comp, typ, target, minmax=None):
        while len(bin_) % 4: bin_.append(0)
        views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data)*struct.calcsize(fmt), "target": target})
        bin_.extend(struct.pack(f"<{len(data)}{fmt}", *data))
        a = {"bufferView": len(views)-1, "componentType": comp,
             "count": len(data) // {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[typ], "type": typ}
        if minmax: a["min"], a["max"] = minmax
        accs.append(a)
        return len(accs)-1
    images, textures = [], []
    def texture(path):
        """Embeds an image file in the .glb and returns its texture index."""
        with open(os.path.join(here, path), "rb") as f:
            data = f.read()
        while len(bin_) % 4: bin_.append(0)
        views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data)})
        bin_.extend(data)
        images.append({"name": os.path.basename(path), "bufferView": len(views)-1, "mimeType": "image/jpeg"})
        textures.append({"sampler": 0, "source": len(images)-1})
        return len(textures)-1
    mat_index = {}
    for name, (color, metal, rough, textured, tile) in MATERIALS.items():
        rgb = linear(color)
        mat_index[name] = len(mats)
        m = {"name": name, "pbrMetallicRoughness": {
             "baseColorFactor": [*rgb, 1.0], "metallicFactor": metal, "roughnessFactor": rough}}
        if textured:
            m["pbrMetallicRoughness"]["baseColorTexture"] = {"index": texture(color_file(name))}
            m["normalTexture"] = {"index": texture(normal_file(name))}
        mats.append(m)
    for name, parts in objects.items():
        pts = [p for mesh in parts.values() for p in mesh.pos]
        mn = [min(p[i] for p in pts) for i in range(3)]
        mx = [max(p[i] for p in pts) for i in range(3)]
        ctr = [(a+b)/2 for a, b in zip(mn, mx)]  # object origin = center of the piece
        prims = []
        for material, mesh in parts.items():
            local = [c - ctr[i] for p in mesh.pos for i, c in enumerate(p)]
            lmn = [min(local[i::3]) for i in range(3)]
            lmx = [max(local[i::3]) for i in range(3)]
            prims.append({"attributes": {
                    "POSITION": acc(local, "f", 5126, "VEC3", 34962, (lmn, lmx)),
                    "NORMAL": acc([c for n in mesh.nrm for c in n], "f", 5126, "VEC3", 34962),
                    "TEXCOORD_0": acc([c for uv in mesh.uv for c in uv], "f", 5126, "VEC2", 34962)},
                "indices": acc(mesh.idx, "I", 5125, "SCALAR", 34963),
                "material": mat_index[material]})
        gl_meshes.append({"name": export_name[name], "primitives": prims})
        node = {"name": export_name[name], "mesh": len(gl_meshes)-1, "translation": ctr}
        nodes.append(node)
    while len(bin_) % 4: bin_.append(0)
    doc = {"asset": {"version": "2.0", "generator": "make_warehouse.py"},
           "scene": 0, "scenes": [{"name": "Warehouse", "nodes": [len(nodes)]}],
           "nodes": nodes + [{"name": ROOT_NAME, "children": list(range(len(nodes)))}],
           "meshes": gl_meshes, "materials": mats, "accessors": accs,
           "images": images, "textures": textures,
           "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}],
           "bufferViews": views, "buffers": [{"byteLength": len(bin_)}]}
    js = json.dumps(doc, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    with open(os.path.join(here, "warehouse.glb"), "wb") as g:
        g.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)))
        g.write(struct.pack("<II", len(js), 0x4E4F534A)); g.write(js)
        g.write(struct.pack("<II", len(bin_), 0x004E4942)); g.write(bin_)

write_obj()
write_glb()
print(f"wrote warehouse.glb, warehouse.obj, warehouse.mtl: {len(objects)} objects")
for name, parts in objects.items():
    print(f"  {export_name[name]:14s} {name:28s} {', '.join(parts)}")
