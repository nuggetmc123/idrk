#!/usr/bin/env python3
"""Pack KartVR/Assets/KartVR into KartVR.unitypackage (no Unity needed).

A .unitypackage is a gzipped tar with one folder per asset, named by the
asset's GUID, holding `asset` (the file), `asset.meta` and `pathname`.
GUIDs are derived from the asset path so re-packing keeps them stable and
importing a newer package updates files in place instead of duplicating them.
"""
import hashlib, io, os, sys, tarfile, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # .../KartVR
SRC = os.path.join(ROOT, "Assets", "KartVR")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "KartVR.unitypackage")

def guid(path):
    return hashlib.md5(("kartvr:" + path).encode()).hexdigest()

def meta(path, is_dir):
    g = guid(path)
    if is_dir:
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif path.endswith(".cs"):
        body = ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
                "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        body = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return "fileFormatVersion: 2\nguid: %s\n%s" % (g, body)

def add(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = int(time.time())
    tar.addfile(info, io.BytesIO(data))

entries = []
for dirpath, dirnames, filenames in os.walk(SRC):
    dirnames.sort()
    rel = os.path.relpath(dirpath, os.path.join(ROOT)).replace(os.sep, "/")  # Assets/KartVR/...
    entries.append((rel, None))
    for f in sorted(filenames):
        if f.endswith(".meta"):
            continue
        entries.append((rel + "/" + f, os.path.join(dirpath, f)))

with tarfile.open(OUT, "w:gz") as tar:
    for path, src in entries:
        g = guid(path)
        add(tar, g + "/pathname", path.encode())
        add(tar, g + "/asset.meta", meta(path, src is None).encode())
        if src:
            with open(src, "rb") as fh:
                add(tar, g + "/asset", fh.read())
print("wrote", OUT, "with", len(entries), "entries")
