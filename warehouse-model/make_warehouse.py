#!/usr/bin/env python3
"""Generates a simple, empty, walk-in warehouse as warehouse.glb and warehouse.obj (+ .mtl).
Every piece is its own named object (Floor, FrontWall_AboveDoor, Roof_Left, ...).

No dependencies: run `python3 make_warehouse.py`. Change the numbers below to resize it.
Units are meters, +Y is up, the floor's top surface is at y = 0 and the building is
centered on x = 0, z = 0. The big roll-up door opening faces +Z.
"""
import json, math, os, struct

# ---- size settings (meters) -------------------------------------------------
WIDTH = 24.0          # along X
DEPTH = 36.0          # along Z
WALL_H = 8.0          # wall height (eaves)
ROOF_RISE = 2.0       # how much higher the ridge is than the eaves
WALL_T = 0.3          # wall thickness
FLOOR_T = 0.3         # floor slab thickness (top at y = 0)
ROOF_T = 0.2          # roof sheet thickness
OVERHANG = 0.6        # roof overhang past the walls
BIG_DOOR = (6.0, 5.0)     # width, height, front wall (+Z)
SMALL_DOOR = (1.4, 2.6)   # width, height, back wall (-Z), off to one side
WINDOW = (2.4, 1.2, 5.6)  # width, height, sill height: strip of open windows on the side walls
WINDOWS_PER_SIDE = 6
TILE = 2.0            # texture repeat size for UVs

MATERIALS = {
    #  name        base color (linear RGB)     metallic roughness
    "Concrete":  ((0.42, 0.42, 0.40), 0.0, 0.95),
    "MetalWall": ((0.20, 0.30, 0.40), 0.6, 0.55),
    "Roof":      ((0.30, 0.30, 0.32), 0.7, 0.50),
    "Trim":      ((0.80, 0.55, 0.08), 0.3, 0.60),
}

# ---- geometry helpers -------------------------------------------------------
def sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(v):
    l = math.sqrt(sum(c*c for c in v)) or 1.0
    return (v[0]/l, v[1]/l, v[2]/l)

class Mesh:
    def __init__(self): self.pos, self.nrm, self.uv, self.idx = [], [], [], []
    def face(self, pts):
        """Adds a flat convex polygon; pts are counter-clockwise seen from outside."""
        n = norm(cross(sub(pts[1], pts[0]), sub(pts[2], pts[0])))
        ax = max(range(3), key=lambda i: abs(n[i]))     # planar UV projection
        u_ax, v_ax = [(2, 1), (0, 2), (0, 1)][ax]
        base = len(self.pos)
        for p in pts:
            self.pos.append(p); self.nrm.append(n)
            self.uv.append((p[u_ax]/TILE, -p[v_ax]/TILE))
        for i in range(1, len(pts)-1):
            self.idx += [base, base+i, base+i+1]

    def hexa(self, c):
        """8 corners: bottom 0-3 then top 4-7, each ring CCW seen from above."""
        self.face([c[0], c[1], c[2], c[3]][::-1])   # bottom
        self.face([c[4], c[5], c[6], c[7]])         # top
        for i in range(4):
            j = (i+1) % 4
            self.face([c[i], c[j], c[j+4], c[i+4]])

    def box(self, x0, x1, y0, y1, z0, z1):
        self.hexa([(x0,y0,z1),(x1,y0,z1),(x1,y0,z0),(x0,y0,z0),
                   (x0,y1,z1),(x1,y1,z1),(x1,y1,z0),(x0,y1,z0)])

    def prism_z(self, poly, z0, z1):
        """Extrudes a CCW (x, y) polygon from z0 to z1 (z1 > z0)."""
        self.face([(x, y, z1) for x, y in poly])
        self.face([(x, y, z0) for x, y in poly][::-1])
        for i in range(len(poly)):
            (ax, ay), (bx, by) = poly[i], poly[(i+1) % len(poly)]
            self.face([(ax, ay, z1), (ax, ay, z0), (bx, by, z0), (bx, by, z1)])

# ---- build the warehouse ----------------------------------------------------
hw, hd, t = WIDTH/2, DEPTH/2, WALL_T
objects = []                                     # (object name, material, Mesh)

def obj(name, material):
    m = Mesh(); objects.append((name, material, m)); return m

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

# Front wall (+Z) with the big door; back wall (-Z) with the small door near the +X corner.
bw, bh = BIG_DOOR
sw, sh = SMALL_DOOR
wall_with_door("FrontWall", hd-t, hd, 0.0, bw, bh)
wall_with_door("BackWall", -hd, -hd+t, hw-4.0, sw, sh, facing_back=True)

# Side walls run between the front and back walls, with a strip of open windows.
ww, wh, wy = WINDOW
za, zb = -hd+t, hd-t
spacing = (zb - za) / WINDOWS_PER_SIDE
gaps = [(za + spacing*(i+0.5) - ww/2, za + spacing*(i+0.5) + ww/2) for i in range(WINDOWS_PER_SIDE)]
# LeftWall/RightWall are on your left/right when you stand outside facing the big door.
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
ze0, ze1 = -hd-OVERHANG, hd+OVERHANG
for side, name in ((-1, "Roof_Left"), (1, "Roof_Right")):
    xe = side*(hw+OVERHANG)                      # eave edge
    ye = WALL_H - OVERHANG*slope
    yr = WALL_H + ROOF_RISE
    lift = ROOF_T / math.cos(math.atan(slope))   # vertical thickness of the sheet
    lo = [(xe, ye, ze1), (0.0, yr, ze1), (0.0, yr, ze0), (xe, ye, ze0)]
    if side > 0:                                 # keep rings CCW seen from above
        lo = [lo[1], lo[0], lo[3], lo[2]]
    obj(name, "Roof").hexa(lo + [(x, y+lift, z) for x, y, z in lo])

# Yellow frame around the big door so the entrance is easy to spot.
f = 0.25
for name, (x0, x1, y0, y1) in [("DoorFrame_Left", (-bw/2-f, -bw/2, 0, bh+f)),
                               ("DoorFrame_Right", (bw/2, bw/2+f, 0, bh+f)),
                               ("DoorFrame_Top", (-bw/2, bw/2, bh, bh+f))]:
    obj(name, "Trim").box(x0, x1, y0, y1, hd, hd+0.08)

# ---- writers ----------------------------------------------------------------
here = os.path.dirname(os.path.abspath(__file__))

def write_obj():
    with open(os.path.join(here, "warehouse.mtl"), "w") as m:
        for name, (rgb, metal, rough) in MATERIALS.items():
            m.write(f"newmtl {name}\nKd {rgb[0]:.3f} {rgb[1]:.3f} {rgb[2]:.3f}\nKs 0.1 0.1 0.1\nNs 20\nd 1\nillum 2\n\n")
    with open(os.path.join(here, "warehouse.obj"), "w") as o:
        o.write("# Empty walk-in warehouse. Meters, Y up, floor top at y=0.\nmtllib warehouse.mtl\n")
        off = 1
        for name, material, mesh in objects:
            o.write(f"o {name}\nusemtl {material}\n")
            o.writelines(f"v {p[0]:.4f} {p[1]:.4f} {p[2]:.4f}\n" for p in mesh.pos)
            o.writelines(f"vt {u:.4f} {v:.4f}\n" for u, v in mesh.uv)
            o.writelines(f"vn {n[0]:.4f} {n[1]:.4f} {n[2]:.4f}\n" for n in mesh.nrm)
            for i in range(0, len(mesh.idx), 3):
                a, b, c = (k + off for k in mesh.idx[i:i+3])
                o.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")
            off += len(mesh.pos)

def write_glb():
    bin_, views, accs, gl_meshes, nodes, mats = bytearray(), [], [], [], [], []
    def add(data, fmt, comp, typ, target, minmax=None):
        while len(bin_) % 4: bin_.append(0)
        views.append({"buffer": 0, "byteOffset": len(bin_), "byteLength": len(data)*struct.calcsize(fmt), "target": target})
        bin_.extend(struct.pack(f"<{len(data)}{fmt}", *data))
        acc = {"bufferView": len(views)-1, "componentType": comp,
               "count": len(data) // {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[typ], "type": typ}
        if minmax: acc["min"], acc["max"] = minmax
        accs.append(acc)
        return len(accs)-1
    mat_index = {}
    for name, (rgb, metal, rough) in MATERIALS.items():
        mat_index[name] = len(mats)
        mats.append({"name": name, "pbrMetallicRoughness": {
            "baseColorFactor": [*rgb, 1.0], "metallicFactor": metal, "roughnessFactor": rough}})
    for name, material, mesh in objects:
        mn = [min(p[i] for p in mesh.pos) for i in range(3)]
        mx = [max(p[i] for p in mesh.pos) for i in range(3)]
        ctr = [(a+b)/2 for a, b in zip(mn, mx)]  # object origin = center of the piece
        local = [c - ctr[i % 3] for k, p in enumerate(mesh.pos) for i, c in enumerate(p)]
        prim = {"attributes": {
                    "POSITION": add(local, "f", 5126, "VEC3", 34962,
                                    ([a-c for a, c in zip(mn, ctr)], [b-c for b, c in zip(mx, ctr)])),
                    "NORMAL": add([c for n in mesh.nrm for c in n], "f", 5126, "VEC3", 34962),
                    "TEXCOORD_0": add([c for uv in mesh.uv for c in uv], "f", 5126, "VEC2", 34962)},
                "indices": add(mesh.idx, "I", 5125, "SCALAR", 34963),
                "material": mat_index[material]}
        gl_meshes.append({"name": name, "primitives": [prim]})
        nodes.append({"name": name, "mesh": len(gl_meshes)-1, "translation": ctr})
    while len(bin_) % 4: bin_.append(0)
    doc = {"asset": {"version": "2.0", "generator": "make_warehouse.py"},
           "scene": 0, "scenes": [{"name": "Warehouse", "nodes": [len(nodes)]}],
           "nodes": nodes + [{"name": "Warehouse", "children": list(range(len(nodes)))}],
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
print(f"wrote warehouse.glb, warehouse.obj, warehouse.mtl: {len(objects)} objects")
for name, material, mesh in objects:
    print(f"  {name:28s} {material:10s} {len(mesh.idx)//3} tris")
