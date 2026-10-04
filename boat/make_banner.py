"""Draws textures/T_Banner.png, the "LOST VR" banner for the sides of the boat (needs Pillow)."""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures", "T_Banner.png")
W, H = 2048, 384
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
NAVY = (18, 30, 42)
ORANGE = (255, 122, 26)
CREAM = (240, 232, 214)


def make_banner(path=OUT):
    rng = np.random.default_rng(3)
    # canvas cloth: navy with a fine weave and soft blotches
    y, x = np.mgrid[0:H, 0:W]
    weave = (np.sin(x * 1.3) * np.sin(y * 1.3)) * 4
    blotch = np.array(Image.fromarray((rng.random((H // 32, W // 32)) * 255).astype(np.uint8))
                      .resize((W, H), Image.BICUBIC), dtype=float) / 255 - 0.5
    base = np.array(NAVY, dtype=float)[None, None, :] + (weave + blotch * 14)[..., None]
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)

    # hemmed border stripes
    for yy in (22, H - 34):
        d.rectangle([0, yy, W, yy + 12], fill=ORANGE)
    for yy in (40, H - 46):
        d.rectangle([0, yy, W, yy + 4], fill=CREAM)

    # grommets in the corners / ends where it's tied to the boat
    for gx in (46, W - 46):
        for gy in (H // 2 - 110, H // 2 + 110):
            d.ellipse([gx - 22, gy - 22, gx + 22, gy + 22], fill=(150, 150, 150), outline=(70, 70, 70), width=4)
            d.ellipse([gx - 10, gy - 10, gx + 10, gy + 10], fill=(12, 16, 20))

    # chevrons either side of the text
    def chevron(cx, flip):
        s = -1 if flip else 1
        for k in range(3):
            ox = cx + s * k * 34
            d.polygon([(ox, H // 2 - 46), (ox + s * 26, H // 2), (ox, H // 2 + 46),
                       (ox - s * 14, H // 2 + 46), (ox + s * 12, H // 2), (ox - s * 14, H // 2 - 46)], fill=ORANGE)

    # title
    font = ImageFont.truetype(FONT, 230)
    parts = [("LOST", CREAM), (" ", CREAM), ("VR", ORANGE)]
    spacing = 18
    widths = []
    for text, _ in parts:
        widths.append(sum(font.getlength(c) + spacing for c in text) - spacing)
    total = sum(widths) + spacing * 2
    x0 = (W - total) / 2
    top = H // 2 - 128

    # drop shadow layer
    shadow = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(shadow)
    cx = x0
    for (text, _), w in zip(parts, widths):
        for c in text:
            sd.text((cx + 10, top + 12), c, font=font, fill=200)
            cx += font.getlength(c) + spacing
        cx += spacing
    img.paste((5, 8, 12), mask=shadow.filter(ImageFilter.GaussianBlur(6)))

    cx = x0
    for (text, col), w in zip(parts, widths):
        for c in text:
            d.text((cx, top), c, font=font, fill=col, stroke_width=9, stroke_fill=(8, 10, 14))
            cx += font.getlength(c) + spacing
        cx += spacing
    chevron(int(x0 - 150), True)
    chevron(int(x0 + total + 150), False)

    # wear: faded scuffs and a darker water line along the bottom edge
    arr = np.array(img, dtype=float)
    scuff = np.array(Image.fromarray((rng.random((H // 48, W // 48)) * 255).astype(np.uint8))
                     .resize((W, H), Image.BICUBIC), dtype=float) / 255
    fade = np.clip((scuff - 0.6) * 0.6, 0, 0.15)[..., None]
    arr = arr * (1 - fade) + np.array([120, 120, 115]) * fade
    arr *= (1 - np.clip((y - (H - 70)) / 70, 0, 1) * 0.25)[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


if __name__ == "__main__":
    print(make_banner())
