#!/usr/bin/env python3
"""Pack Veylix/Assets/Veylix into Veylix.unitypackage (Assets > Import Package > Custom Package).
A fallback for when Unity Hub won't add the project folder: make a new Unity 6 project, import this.
Usage: Veylix/Tools/make_unitypackage.py [output.unitypackage]"""
import hashlib, io, os, sys, tarfile

proj = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(proj), "Veylix.unitypackage"))

def guid(path):
    return hashlib.md5(("veylix:" + path).encode()).hexdigest()

def add(tar, name, data):
    info = tarfile.TarInfo(name); info.size = len(data); info.mode = 0o644; info.mtime = 0
    tar.addfile(info, io.BytesIO(data))

with tarfile.open(out, "w:gz") as tar:
    root = os.path.join(proj, "Assets", "Veylix")
    for dirpath, dirs, files in os.walk(root):
        dirs.sort()
        rel_dir = os.path.relpath(dirpath, proj).replace(os.sep, "/")
        g = guid(rel_dir)
        add(tar, g + "/pathname", rel_dir.encode())
        add(tar, g + "/asset.meta", ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % g).encode())
        for f in sorted(files):
            if f.endswith(".meta"): continue
            rel = rel_dir + "/" + f
            g = guid(rel)
            add(tar, g + "/pathname", rel.encode())
            add(tar, g + "/asset.meta", ("fileFormatVersion: 2\nguid: %s\n" % g).encode())
            with open(os.path.join(dirpath, f), "rb") as fh: add(tar, g + "/asset", fh.read())
print("wrote " + out)
