"""Builds racer.glb: a low-poly motorcycle racer, ~1.8 m tall.

glTF conventions: metres, +Y up, the model faces +Z, feet on y = 0.
Every body part is its own named node so it can be posed or rigged later.

    pip install trimesh numpy
    python3 models/build_racer.py
"""
import os
import numpy as np
import trimesh
from trimesh.visual.material import PBRMaterial

def mat(hex_, metal=0.0, rough=0.6):
    h = hex_.lstrip('#')
    # glTF colour factors are linear, so convert from the sRGB hex
    lin = [((c / 255 + 0.055) / 1.055) ** 2.4 if c / 255 > 0.04045 else c / 255 / 12.92
           for c in (int(h[i:i+2], 16) for i in (0, 2, 4))]
    return PBRMaterial(baseColorFactor=lin + [1.0], metallicFactor=metal, roughnessFactor=rough)

ORANGE  = mat('#ff6a1a', rough=0.45)          # leathers / helmet paint
BLACK   = mat('#1b1a22', rough=0.55)          # leather panels, boots, gloves
WHITE   = mat('#f2f0f6', rough=0.45)
GREY    = mat('#9a97a6', metal=0.6, rough=0.35)  # sliders, buckles
VISOR   = mat('#0c0c14', metal=0.9, rough=0.08)  # tinted, glossy
GLARE   = mat('#ffb27a', metal=0.2, rough=0.1)

scene = trimesh.Scene()

def add(name, mesh, material):
    mesh.visual = trimesh.visual.TextureVisuals(material=material)
    scene.add_geometry(mesh, node_name=name, geom_name=name)

def limb(p0, p1, r, sections=20):
    """A capsule running from p0 to p1."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    v = p1 - p0
    h = np.linalg.norm(v)
    m = trimesh.creation.capsule(height=h, radius=r, count=[sections, sections])
    m.apply_translation([0, 0, h / 2])                      # capsule spans z 0..h
    m.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], v / h))
    m.apply_translation(p0)
    return m

def ellipsoid(center, radii, sub=3):
    m = trimesh.creation.icosphere(subdivisions=sub)
    m.apply_scale(radii)
    m.apply_translation(center)
    return m

def shell(center, radii, pole):
    """A UV-sphere ellipsoid whose rings run around `pole`, so slicing it
    across that axis leaves a clean edge instead of a jagged one."""
    m = trimesh.creation.uv_sphere(radius=1.0, count=[96, 96])
    m.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], pole))
    m.apply_scale(radii)
    m.apply_translation(center)
    return m

def box(center, size, rot=None):
    m = trimesh.creation.box(extents=size)
    if rot is not None:
        m.apply_transform(rot)
    m.apply_translation(center)
    return m

def slice_mesh_plane(m, normal, origin):
    """Keep the faces on the +normal side of a plane (open shells only)."""
    d = (m.vertices - np.asarray(origin, float)) @ np.asarray(normal, float)
    keep = (d[m.faces] >= 0).all(axis=1)
    m = m.copy()
    m.update_faces(keep)
    m.remove_unreferenced_vertices()
    return m

def rx(a): return trimesh.transformations.rotation_matrix(a, [1, 0, 0])
def rz(a): return trimesh.transformations.rotation_matrix(a, [0, 0, 1])

# ---------- legs ----------
for s, side in ((1, 'L'), (-1, 'R')):
    x = 0.10 * s
    hip, knee, ankle = [x, 0.92, 0], [x * 1.05, 0.50, 0.02], [x * 1.05, 0.12, 0]
    add(f'Thigh_{side}', limb(hip, knee, 0.075), ORANGE)
    add(f'Shin_{side}',  limb(knee, ankle, 0.058), BLACK)
    # the white racing stripe down the outside of each leg
    add(f'LegStripe_{side}', limb([x + 0.07 * s, 0.88, 0], [x * 1.05 + 0.065 * s, 0.53, 0.02], 0.018), WHITE)
    # knee slider: the puck racers drag on the tarmac
    add(f'KneeSlider_{side}', box([x * 1.05 + 0.03 * s, 0.50, 0.075], [0.07, 0.10, 0.03], rx(-0.15)), GREY)
    # race boot with a toe slider
    boot = trimesh.util.concatenate([
        limb([x * 1.05, 0.30, -0.01], [x * 1.05, 0.08, -0.01], 0.065),
        box([x * 1.05, 0.045, 0.05], [0.11, 0.09, 0.26]),
    ])
    add(f'Boot_{side}', boot, BLACK)
    add(f'ToeSlider_{side}', box([x * 1.05 + 0.05 * s, 0.04, 0.12], [0.02, 0.05, 0.08]), GREY)
    add(f'BootStrap_{side}', box([x * 1.05, 0.22, 0.0], [0.14, 0.03, 0.14]), ORANGE)

# ---------- torso ----------
add('Hips',  ellipsoid([0, 0.95, 0], [0.18, 0.12, 0.12]), BLACK)
add('Torso', ellipsoid([0, 1.22, 0], [0.20, 0.27, 0.13]), ORANGE)
stripe = slice_mesh_plane(shell([0, 1.22, 0], [0.203, 0.273, 0.133], [1, 0, 0]), [1, 0, 0], [-0.03, 0, 0])
stripe = slice_mesh_plane(stripe, [-1, 0, 0], [0.03, 0, 0])
add('SuitStripe', stripe, WHITE)
add('Belt', ellipsoid([0, 1.02, 0], [0.185, 0.04, 0.125]), BLACK)
# the aero hump racing suits have on the back
add('AeroHump', ellipsoid([0, 1.40, -0.12], [0.11, 0.15, 0.09]), ORANGE)
add('HumpStripe', ellipsoid([0, 1.40, -0.125], [0.03, 0.155, 0.093]), WHITE)
# number plate on the chest: a white disc, the "46" is up to a texture
disc = trimesh.creation.cylinder(radius=0.055, height=0.03, sections=32)
disc.apply_transform(trimesh.transformations.rotation_matrix(0.45, [0, 1, 0]))
disc.apply_translation([0.095, 1.29, 0.112])
add('ChestNumberDisc', disc, WHITE)

# ---------- arms (relaxed A-pose) ----------
for s, side in ((1, 'L'), (-1, 'R')):
    sh = [0.22 * s, 1.43, 0]
    el = [0.33 * s, 1.15, 0.02]
    wr = [0.40 * s, 0.90, 0.06]
    add(f'Shoulder_{side}', ellipsoid(sh, [0.085, 0.075, 0.085]), BLACK)
    add(f'UpperArm_{side}', limb(sh, el, 0.058), ORANGE)
    add(f'Forearm_{side}',  limb(el, wr, 0.048), ORANGE)
    add(f'ElbowSlider_{side}', ellipsoid([el[0] + 0.02 * s, el[1], el[2] - 0.02], [0.04, 0.05, 0.04], 2), GREY)
    add(f'GloveCuff_{side}', limb([0.38 * s, 0.96, 0.05], [0.41 * s, 0.88, 0.065], 0.058), BLACK)
    add(f'Glove_{side}', ellipsoid([0.42 * s, 0.82, 0.07], [0.045, 0.075, 0.055], 2), BLACK)
    add(f'Knuckles_{side}', box([0.43 * s, 0.82, 0.12], [0.07, 0.06, 0.02]), GREY)

# ---------- neck + helmet ----------
add('Neck', limb([0, 1.45, 0], [0, 1.56, 0.01], 0.06), BLACK)
HC = np.array([0, 1.68, 0.01])              # helmet centre
HR = np.array([0.145, 0.15, 0.165])         # a bit longer front-to-back
add('Helmet', ellipsoid(HC, HR, 4), ORANGE)

# white stripe over the crown, front to back
shell_ = shell(HC, HR * 1.012, [1, 0, 0])
stripe = slice_mesh_plane(shell_, [1, 0, 0], [-0.025, 0, 0])
stripe = slice_mesh_plane(stripe, [-1, 0, 0], [0.025, 0, 0])
stripe = slice_mesh_plane(stripe, [0, 1, 0], [0, HC[1] + 0.02, 0])
add('HelmetStripe', stripe, WHITE)

# tinted visor: a band across the front of the shell
vis = shell(HC, HR * 1.02, [0, 1, 0])
vis = slice_mesh_plane(vis, [0, 0, 1], [0, 0, HC[2] + 0.06])
vis = slice_mesh_plane(vis, [0, 1, 0], [0, HC[1] - 0.045, 0])
vis = slice_mesh_plane(vis, [0, -1, 0], [0, HC[1] + 0.05, 0])
add('Visor', vis, VISOR)
glare = shell(HC, HR * 1.028, [0, 1, 0])
glare = slice_mesh_plane(glare, [0, 0, 1], [0, 0, HC[2] + 0.1])
glare = slice_mesh_plane(glare, [0, 1, 0], [0, HC[1] + 0.025, 0])
glare = slice_mesh_plane(glare, [0, -1, 0], [0, HC[1] + 0.035, 0])
glare = slice_mesh_plane(glare, [1, 0, 0], [-0.07, 0, 0])
glare = slice_mesh_plane(glare, [-1, 0, 0], [0.02, 0, 0])
add('VisorGlare', glare, GLARE)

# chin bar with vents, and the spoiler on the back
add('ChinBar', ellipsoid([0, HC[1] - 0.095, HC[2] + 0.07], [0.115, 0.055, 0.1]), BLACK)
for vx in (-0.035, 0, 0.035):
    add(f'ChinVent_{"LCR"[int(round(vx / 0.035)) + 1]}',
        box([vx, HC[1] - 0.09, HC[2] + 0.168], [0.018, 0.035, 0.01], rx(0.5)), GREY)
add('HelmetSpoiler', box([0, HC[1] + 0.07, HC[2] - 0.15], [0.16, 0.025, 0.08], rx(-0.45)), BLACK)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'racer.glb')
scene.export(out)
b = scene.bounds
print(f'wrote {out}: {len(scene.geometry)} parts, '
      f'{sum(len(g.faces) for g in scene.geometry.values())} triangles, '
      f'height {b[1][1] - b[0][1]:.2f} m')
