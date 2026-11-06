#!/usr/bin/env python3
"""Generates a walk-in warehouse as warehouse.glb and warehouse.obj (+ .mtl).
Every piece is its own named object (Floor, GarageDoor, Shelf_LeftWall_1, Light_1, ...).

No dependencies: run `python3 make_warehouse.py`. Change the numbers below to resize it.
Units are meters, +Y is up, the floor's top surface is at y = 0 and the building is
centered on x = 0, z = 0. The garage door faces +Z.
"""
import json, math, os, struct

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
SHELF_ROWS_X = [(-8.6, -7.6, "LeftWall"), (-3.4, -2.2, "MiddleLeft"),
                (2.2, 3.4, "MiddleRight"), (7.6, 8.6, "RightWall")]   # x range of each row
SHELF_Z = (-11.0, 9.0)    # shelf rows run from the back toward the door, stopping here
SHELF_UNIT_LEN = 2.5
SHELF_LEVELS = [0.15, 1.05, 1.95, 2.85]   # height of each shelf board
LIGHTS_X = [-5.5, 0.0, 5.5]
LIGHTS_Z = [-8.0, -1.0, 6.0]
LAMP_Y = 4.6              # height of the lamp shades
LAMP_POWER_W = 350        # how bright each light is in Blender (watts)
TILE = 2.0                # texture repeat size for UVs

MATERIALS = {
    #  name        base color (linear RGB)  metallic roughness  emission
    "Concrete":   ((0.42, 0.42, 0.40), 0.0, 0.95, None),
    "MetalWall":  ((0.20, 0.30, 0.40), 0.6, 0.55, None),
    "Roof":       ((0.30, 0.30, 0.32), 0.7, 0.50, None),
    "Trim":       ((0.80, 0.55, 0.08), 0.3, 0.60, None),
    "GarageDoor": ((0.55, 0.57, 0.58), 0.7, 0.45, None),
    "Steel":      ((0.12, 0.12, 0.13), 0.8, 0.40, None),
    "ShelfFrame": ((0.08, 0.20, 0.55), 0.5, 0.50, None),
    "Wood":       ((0.45, 0.28, 0.12), 0.0, 0.80, None),
    "LampBulb":   ((1.00, 0.92, 0.75), 0.0, 0.50, (1.00, 0.85, 0.60)),
}

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
    def __init__(self): self.pos, self.nrm, self.uv, self.idx = [], [], [], []
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
            self.uv.append((p[u_ax]/TILE, -p[v_ax]/TILE))
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
        mid = centroid([(x, y, (z0+z1)/2) for x, y in poly])
        self.face([(x, y, z1) for x, y in poly], mid)
        self.face([(x, y, z0) for x, y in poly], mid)
        for i in range(len(poly)):
            (ax, ay), (bx, by) = poly[i], poly[(i+1) % len(poly)]
            self.face([(ax, ay, z1), (ax, ay, z0), (bx, by, z0), (bx, by, z1)], mid)

# ---- build the warehouse ----------------------------------------------------
hw, hd, t = WIDTH/2, DEPTH/2, WALL_T
objects = {}      # object name -> {material: Mesh}; an object can use several materials
lights = []       # (object name, light position)

def obj(name, material):
    return objects.setdefault(name, {}).setdefault(material, Mesh())

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
for prefix, x0, x1 in [("LeftWall", -hw, -hw+t), ("RightWall", hw-t, hw)]:
    obj(f"{prefix}_BelowWindows", "MetalWall").box(x0, x1, 0, wy, za, zb)
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
def roof_y(x): return WALL_H + ROOF_RISE*(1 - abs(x)/hw)   # underside of the roof
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

# ---- stairs with side rails up to a platform at the roof edge (left wall) -----
sx1 = -hw - OVERHANG                             # inner edge (by the roof edge)
sx0 = sx1 - 1.2                                  # outer edge
pz0, pz1 = -hd + 1.0, -hd + 2.8                  # the platform's z range
platform = obj("RoofPlatform", "Steel")
platform.box(sx0, sx1 + 0.05, eave_y - 0.15, eave_y, pz0, pz1)
for x in (sx0 + 0.05, sx1 - 0.05):              # support legs
    for z in (pz0 + 0.05, pz1 - 0.05):
        platform.box(x-0.05, x+0.05, 0, eave_y - 0.15, z-0.05, z+0.05)
platform.beam((sx1 - 0.05, 0, pz0 + 0.05), (sx1 - 0.05, eave_y - 0.15, pz1 - 0.05), 0.06, 0.06)  # cross brace

n_steps = math.ceil(eave_y / 0.2)
riser, tread = eave_y / n_steps, 0.28
stair_bottom = pz1 + n_steps*tread               # where the stairs reach the ground
stairs = obj("Stairs", "Steel")
for i in range(1, n_steps):                      # the last step is the platform itself
    z = pz1 + (n_steps - i)*tread
    stairs.box(sx0 + 0.06, sx1 - 0.06, i*riser - 0.05, i*riser, z - tread, z)
for x in (sx0 + 0.03, sx1 - 0.03):               # sloped side beams (stringers)
    stairs.beam((x, 0, stair_bottom), (x, eave_y - 0.15, pz1), 0.06, 0.25)

rail_h = 1.0
rail = obj("Stairs_Railing", "Trim")
for x in (sx0 + 0.03, sx1 - 0.03):               # a rail on both sides of the stairs
    posts = [stair_bottom - k*(stair_bottom - pz1)/5 for k in range(6)]
    for z in posts:
        y = eave_y * (stair_bottom - z) / (stair_bottom - pz1)
        rail.box(x-0.03, x+0.03, y, y+rail_h, z-0.03, z+0.03)
    for h in (rail_h, rail_h/2):                 # handrail and mid rail
        rail.beam((x, h, stair_bottom), (x, eave_y + h, pz1), 0.05, 0.05)

prail = obj("RoofPlatform_Railing", "Trim")      # outer side and back end; roof side stays open
for x, z in ((sx0 + 0.03, pz1 - 0.03), (sx0 + 0.03, pz0 + 0.03), (sx1 - 0.03, pz0 + 0.03)):
    prail.box(x-0.03, x+0.03, eave_y, eave_y + rail_h, z-0.03, z+0.03)
for h in (rail_h, rail_h/2):
    prail.box(sx0, sx0 + 0.06, eave_y + h - 0.025, eave_y + h + 0.025, pz0, pz1)
    prail.box(sx0, sx1, eave_y + h - 0.025, eave_y + h + 0.025, pz0, pz0 + 0.06)

# ---- shelves: rows of tall shelving units, with aisles between them -----------
n_units = round((SHELF_Z[1] - SHELF_Z[0]) / SHELF_UNIT_LEN)
for x0, x1, row in SHELF_ROWS_X:
    for u in range(n_units):
        z0 = SHELF_Z[0] + u*SHELF_UNIT_LEN
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

# ---- hanging lights ----------------------------------------------------------
k = 0
for z in LIGHTS_Z:
    for x in LIGHTS_X:
        k += 1
        name = f"Light_{k}"
        steel, bulb = obj(name, "Steel"), obj(name, "LampBulb")
        steel.box(x-0.015, x+0.015, LAMP_Y + 0.25, roof_y(x), z-0.015, z+0.015)   # cord
        r0, r1 = 0.12, 0.35                                                # shade: cone-ish
        ring = lambda r, y: [(x-r, y, z-r), (x+r, y, z-r), (x+r, y, z+r), (x-r, y, z+r)]
        steel.hexa(ring(r1, LAMP_Y) + ring(r0, LAMP_Y + 0.25))
        bulb.box(x-0.1, x+0.1, LAMP_Y - 0.12, LAMP_Y, z-0.1, z+0.1)
        lights.append((name, (x, LAMP_Y - 0.15, z)))

# ---- writers ----------------------------------------------------------------
here = os.path.dirname(os.path.abspath(__file__))

def write_obj():
    with open(os.path.join(here, "warehouse.mtl"), "w") as m:
        for name, (rgb, metal, rough, emit) in MATERIALS.items():
            m.write(f"newmtl {name}\nKd {rgb[0]:.3f} {rgb[1]:.3f} {rgb[2]:.3f}\nKs 0.1 0.1 0.1\nNs 20\nd 1\nillum 2\n")
            if emit: m.write(f"Ke {emit[0]:.3f} {emit[1]:.3f} {emit[2]:.3f}\n")
            m.write("\n")
    with open(os.path.join(here, "warehouse.obj"), "w") as o:
        o.write("# Walk-in warehouse. Meters, Y up, floor top at y=0. (OBJ has no lights; see the .glb/.fbx/.blend)\n"
                "mtllib warehouse.mtl\n")
        off = 1
        for name, parts in objects.items():
            o.write(f"o {name}\n")
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
    mat_index = {}
    for name, (rgb, metal, rough, emit) in MATERIALS.items():
        mat_index[name] = len(mats)
        m = {"name": name, "pbrMetallicRoughness": {
             "baseColorFactor": [*rgb, 1.0], "metallicFactor": metal, "roughnessFactor": rough}}
        if emit:
            m["emissiveFactor"] = list(emit)
            m["extensions"] = {"KHR_materials_emissive_strength": {"emissiveStrength": 6.0}}
        mats.append(m)
    light_defs, light_at = [], dict(lights)
    # Blender reads glTF light intensity (candela) back as watts * 683 / (4 * pi)
    candela = LAMP_POWER_W * 683 / (4 * math.pi)
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
        gl_meshes.append({"name": name, "primitives": prims})
        node = {"name": name, "mesh": len(gl_meshes)-1, "translation": ctr}
        if name in light_at:                     # a real light source, as a child of the lamp
            light_defs.append({"name": f"{name}_Lamp", "type": "point", "intensity": candela,
                               "color": [1.0, 0.88, 0.7], "range": 25.0})
            nodes.append({"name": f"{name}_Lamp", "translation": [a-b for a, b in zip(light_at[name], ctr)],
                          "extensions": {"KHR_lights_punctual": {"light": len(light_defs)-1}}})
            node["children"] = [len(nodes)-1]
        nodes.append(node)
    while len(bin_) % 4: bin_.append(0)
    top_level = [i for i, n in enumerate(nodes) if "mesh" in n]
    doc = {"asset": {"version": "2.0", "generator": "make_warehouse.py"},
           "extensionsUsed": ["KHR_lights_punctual", "KHR_materials_emissive_strength"],
           "extensions": {"KHR_lights_punctual": {"lights": light_defs}},
           "scene": 0, "scenes": [{"name": "Warehouse", "nodes": [len(nodes)]}],
           "nodes": nodes + [{"name": "Warehouse", "children": top_level}],
           "meshes": gl_meshes, "materials": mats, "accessors": accs,
           "bufferViews": views, "buffers": [{"byteLength": len(bin_)}]}
    js = json.dumps(doc, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    with open(os.path.join(here, "warehouse.glb"), "wb") as g:
        g.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)))
        g.write(struct.pack("<II", len(js), 0x4E4F534A)); g.write(js)
        g.write(struct.pack("<II", len(bin_), 0x004E4942)); g.write(bin_)

write_obj()
write_glb()
print(f"wrote warehouse.glb, warehouse.obj, warehouse.mtl: {len(objects)} objects, {len(lights)} lights")
for name, parts in objects.items():
    print(f"  {name:28s} {', '.join(parts):22s} {sum(len(m.idx) for m in parts.values())//3} tris")
