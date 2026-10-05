"""Tile every <Name>_preview.jpg into one overview image.

    python3 contact_sheet.py <clothing dir> <out.png> [front|both]
"""
import glob
import os
import sys

import bpy
import numpy as np

root, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
mode = sys.argv[3] if len(sys.argv) > 3 else 'front'
cats = ['Pants', 'Shorts', 'Shirts', 'LongSleeves']
rows = []
for c in cats:
    tiles = []
    for p in sorted(glob.glob(os.path.join(root, c, '*', '*_preview.jpg'))):
        im = bpy.data.images.load(p)
        w, h = im.size
        a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        if mode == 'front':
            a = a[:, :w // 2]
        step = 2
        tiles.append(a[::step, ::step])
        bpy.data.images.remove(im)
    rows.append(np.concatenate(tiles, axis=1))
sheet = np.concatenate(rows[::-1], axis=0)   # pixel rows are bottom-up
H, W = sheet.shape[:2]
img = bpy.data.images.new('sheet', W, H, alpha=False)
img.pixels.foreach_set(sheet.ravel())
img.filepath_raw = out
img.file_format = 'JPEG' if out.lower().endswith('.jpg') else 'PNG'
img.save()
print('sheet', W, H)
