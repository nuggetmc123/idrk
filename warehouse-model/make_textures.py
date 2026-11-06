#!/usr/bin/env python3
"""Generates the warehouse's realistic, seamlessly tiling textures into textures/.

Needs numpy and Pillow (`pip install numpy pillow`). Run `python3 make_textures.py`,
then `python3 make_warehouse.py` to rebuild the model with them.

Each material gets NAME_color.jpg and NAME_normal.jpg (OpenGL-style normal map, which is
what glTF, Blender and Unity expect). Everything is made from filtered noise in Fourier
space, so every texture tiles with no visible seams.
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures")
rng = np.random.default_rng(7)

# ---- helpers ----------------------------------------------------------------
def noise(n, beta=1.8, sx=1.0, sy=1.0, seed=None):
    """Tileable fractal noise in 0..1. Bigger beta = smoother. sx/sy > 1 stretch it along x/y."""
    r = np.random.default_rng(seed) if seed is not None else rng
    f = np.fft.fft2(r.standard_normal((n, n)))
    fy = np.fft.fftfreq(n)[:, None] * n
    fx = np.fft.fftfreq(n)[None, :] * n
    k = np.sqrt((fx * sx) ** 2 + (fy * sy) ** 2)
    k[0, 0] = 1.0
    out = np.real(np.fft.ifft2(f / k ** beta))
    out -= out.min()
    return out / out.max()

def hexrgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], dtype=float) / 255

def mix(a, b, t):
    """Blends color a -> b by mask t (per pixel)."""
    t = t[..., None]
    return a * (1 - t) + b * t

def smooth(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)

def ribs(n, count, flat=0.35):
    """Trapezoid corrugation profile (0..1) repeating `count` times across n pixels."""
    p = (np.arange(n) / n * count) % 1.0
    tri = 1 - np.abs(2 * p - 1)                  # 0 at the edges, 1 in the middle
    return np.clip((tri - flat / 2) / (1 - flat), 0, 1)

def normal_map(h, strength):
    """Height field (0..1, tiling) -> OpenGL normal map image."""
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2 * strength
    gy = (np.roll(h, 1, 0) - np.roll(h, -1, 0)) / 2 * strength   # rows grow downward
    nrm = np.dstack([-gx, -gy, np.ones_like(h)])
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    return nrm * 0.5 + 0.5

def save(name, color, height, strength):
    os.makedirs(OUT, exist_ok=True)
    rgb = np.clip(color, 0, 1)
    Image.fromarray((rgb * 255 + 0.5).astype(np.uint8)).save(
        os.path.join(OUT, f"{name}_color.jpg"), quality=88, optimize=True)
    Image.fromarray((normal_map(height, strength) * 255 + 0.5).astype(np.uint8)).save(
        os.path.join(OUT, f"{name}_normal.jpg"), quality=92, optimize=True)
    print("wrote", name)

def top(x, fraction, soft=0.05):
    """Soft mask (0..1) that covers roughly the highest `fraction` of the values in x."""
    lo = np.quantile(x, 1 - fraction)
    return smooth(lo - soft / 2, lo + soft / 2, x)

def grime(n, amount, fraction=0.25):
    """Soft, patchy dirt mask covering about `fraction` of the surface."""
    return top(noise(n, 2.0), fraction, 0.15) * amount

# ---- materials ---------------------------------------------------------------
def corrugated_wall(n=1024):
    """Painted steel cladding, vertical ribs, with rain streaks. Tiles every 2 m (10 ribs)."""
    profile = ribs(n, 10)[None, :].repeat(n, 0)
    h = profile + noise(n, 1.4) * 0.02
    base = hexrgb("#5B7286")
    c = np.ones((n, n, 3)) * base
    c *= (0.88 + 0.12 * profile)[..., None]                  # valleys catch less light
    c *= (0.93 + 0.07 * noise(n, 1.6))[..., None]            # paint variation
    streaks = top(noise(n, 1.5, sx=0.06, sy=2.0), 0.3, 0.2)  # long vertical rain streaks
    c = mix(c, c * 0.75, streaks * 0.5)
    c = mix(c, hexrgb("#6A5E4E"), grime(n, 0.18))            # dusty patches
    save("MetalWall", c, h, 18)

def corrugated_roof(n=2048):
    """Galvanized roofing, ribs running down the slope, with a few rust spots. Tiles every 4 m."""
    profile = ribs(n, 20)[:, None].repeat(n, 1)               # ribs vary along v (rows)
    h = profile + noise(n, 1.3) * 0.03
    c = np.ones((n, n, 3)) * hexrgb("#8C9296")
    c *= (0.85 + 0.15 * profile)[..., None]
    c *= (0.9 + 0.1 * noise(n, 1.2))[..., None]              # zinc spangle / mottling
    rust = top(noise(n, 1.3), 0.03, 0.06) * (0.5 + 0.5 * noise(n, 1.0))
    c = mix(c, hexrgb("#7A4524") * (0.8 + 0.4 * noise(n, 1.1))[..., None], rust)
    c = mix(c, c * 0.8, top(noise(n, 1.6, sx=2.0, sy=0.06), 0.3, 0.2) * 0.35)
    save("Roof", c, h + rust * 0.05, 18)

def concrete(n=1024):
    """Sealed warehouse slab with stains, speckle and a saw-cut joint every 4 m."""
    big, mid, fine = noise(n, 2.4), noise(n, 1.6), noise(n, 0.9)
    c = np.ones((n, n, 3)) * hexrgb("#8D8B85")
    c *= (0.9 + 0.08 * big + 0.1 * mid + 0.14 * fine)[..., None]
    stains = top(noise(n, 2.0), 0.15, 0.12) * 0.3
    c = mix(c, hexrgb("#55524B"), stains)
    grit = rng.random((n, n))
    c = mix(c, hexrgb("#3A3936"), (grit > 0.97) * 0.35)    # dark aggregate
    c = mix(c, hexrgb("#B5B2AA"), (grit < 0.02) * 0.4)     # light aggregate
    tire = top(noise(n, 1.9, sx=0.15, sy=2.0), 0.08, 0.1) * 0.25  # scuff marks
    c = mix(c, hexrgb("#2F2E2C"), tire * 0.6)
    joint = np.zeros((n, n))
    w = 3
    joint[:w, :] = 1; joint[-w:, :] = 1; joint[:, :w] = 1; joint[:, -w:] = 1
    c = mix(c, hexrgb("#3B3A37"), joint * 0.8)
    h = fine * 0.15 + mid * 0.1 - joint * 0.6
    save("Concrete", c, h, 6)

def garage_door(n=512):
    """White steel sectional door: a groove every 0.3 m plus fine ribbing. Tiles every 0.6 m."""
    y = np.arange(n) / n
    groove = np.zeros(n)
    for g in (0.0, 0.5):
        d = np.minimum(np.abs(y - g), 1 - np.abs(y - g))
        groove = np.maximum(groove, smooth(0.02, 0.0, d))
    fine_ribs = 0.5 + 0.5 * np.sin(y * 2 * np.pi * 20)
    h = (1 - groove)[:, None] * (0.85 + 0.15 * fine_ribs[:, None]) * np.ones((1, n))
    c = np.ones((n, n, 3)) * hexrgb("#CDCFCB")
    c *= (0.9 + 0.1 * h)[..., None]
    c *= (0.94 + 0.06 * noise(n, 1.5))[..., None]
    c = mix(c, hexrgb("#8A8173"), top(noise(n, 1.5, sx=0.08, sy=2.0), 0.2, 0.2) * 0.2)
    save("GarageDoor", c, h, 10)

def brushed_steel(n=512):
    """Galvanized / brushed steel for the ladder, door tracks and sink tap."""
    brush = noise(n, 1.0, sx=12.0, sy=0.6)                    # long horizontal scratches
    c = np.ones((n, n, 3)) * hexrgb("#7E8489")
    c *= (0.85 + 0.2 * brush + 0.1 * noise(n, 2.0))[..., None]
    c = mix(c, hexrgb("#4A4640"), grime(n, 0.2))
    save("Steel", c, brush * 0.2, 3)

def painted_metal(name, paint, n=512, chips=0.12):
    """Painted steel with worn, chipped edges showing dark metal (shelf frames, trim)."""
    c = np.ones((n, n, 3)) * hexrgb(paint)
    c *= (0.95 + 0.05 * noise(n, 1.4) + 0.05 * noise(n, 0.6))[..., None]
    chip = top(noise(n, 1.2) * 0.7 + noise(n, 0.4) * 0.3, chips * 0.3, 0.02)
    c = mix(c, hexrgb("#3C3B3A") * (0.8 + 0.4 * noise(n, 0.7))[..., None], chip)
    c = mix(c, hexrgb("#55493C"), grime(n, 0.15, 0.2))
    save(name, c, noise(n, 1.0) * 0.08 - chip * 0.3, 4)

def wood(n=512):
    """Plywood / particle board with grain running along u. Tiles every 1 m."""
    warp = noise(n, 2.0, sx=6.0, sy=1.0)
    grain = 0.5 + 0.5 * np.sin((np.arange(n)[:, None] / n * 2 * np.pi * 26) + warp * 14)
    fibers = noise(n, 0.8, sx=10.0, sy=0.5)
    light, dark = hexrgb("#B98C5A"), hexrgb("#7A5530")
    c = mix(np.ones((n, n, 3)) * light, np.ones((n, n, 3)) * dark, grain ** 2.5 * 0.7 + fibers * 0.3)
    c *= (0.9 + 0.12 * noise(n, 2.2))[..., None]
    save("Wood", c, grain * 0.2 + fibers * 0.2, 4)

def drywall(name, paint, n=512):
    """Painted drywall with a faint orange-peel texture and scuffs low down."""
    peel = noise(n, 0.6)
    c = np.ones((n, n, 3)) * hexrgb(paint)
    c *= (0.95 + 0.04 * peel + 0.05 * noise(n, 2.3))[..., None]
    c = mix(c, hexrgb("#6F6A60"), top(noise(n, 1.2), 0.04, 0.05) * 0.08)
    save(name, c, peel * 0.5, 2)

def tiles(name, tile_color, grout, count, n=512, variation=0.06):
    """Square tiles with grout lines; `count` tiles across one texture repeat."""
    px = n // count
    idx = np.arange(n) // px
    per_tile = rng.random((count, count))
    shade = per_tile[idx[:, None], idx[None, :]]
    edge = np.minimum(np.arange(n) % px, px - 1 - np.arange(n) % px)
    gx, gy = edge[None, :], edge[:, None]
    line = smooth(2.5, 1.0, np.minimum(gx, gy).astype(float))
    c = np.ones((n, n, 3)) * hexrgb(tile_color)
    c *= (1 - variation + 2 * variation * shade + 0.03 * noise(n, 1.2))[..., None]
    c = mix(c, hexrgb(grout), line)
    bevel = smooth(0.0, 6.0, np.minimum(gx, gy).astype(float))
    save(name, c, bevel * 0.8, 6)

def carpet(n=512):
    """Commercial loop-pile carpet."""
    fibers = noise(n, 0.3)
    c = np.ones((n, n, 3)) * hexrgb("#434A57")
    c *= (0.8 + 0.3 * fibers + 0.1 * noise(n, 2.0))[..., None]
    save("Carpet", c, fibers * 0.6, 4)

if __name__ == "__main__":
    corrugated_wall()
    corrugated_roof()
    concrete()
    garage_door()
    brushed_steel()
    painted_metal("ShelfFrame", "#C4561A")                # classic orange pallet racking
    painted_metal("Trim", "#D9A70C", chips=0.15)          # worn safety-yellow
    painted_metal("Cabinet", "#9B988C", chips=0.05)       # old beige-grey filing cabinet
    wood()
    drywall("OfficeWall", "#CFCABA")
    drywall("RoomWall", "#D8D5CC")
    tiles("BathroomWall", "#E6EAEA", "#9D9E98", 4)        # 15 cm wall tiles
    tiles("Tile", "#B7B9B4", "#6E6F6A", 2, variation=0.08)  # 30 cm floor tiles
    carpet()
