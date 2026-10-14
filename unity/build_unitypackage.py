#!/usr/bin/env python3
"""Build unity/KingdomChaos.unitypackage from unity/KingdomChaos/.

A .unitypackage is a gzipped tar with one directory per asset, named by the asset's GUID,
holding `asset` (the file itself, omitted for folders), `asset.meta` and `pathname` (where
it lands inside the project). This writes any missing .meta files first, with GUIDs derived
from the asset path so they stay stable between builds, then packs everything up.

    python3 unity/build_unitypackage.py
"""
import hashlib
import io
import os
import tarfile

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "KingdomChaos")
OUT = os.path.join(HERE, "KingdomChaos.unitypackage")
PROJECT_ROOT = "Assets/KingdomChaos"

TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid_for(project_path):
    return hashlib.md5(("kingdom-chaos:" + project_path).encode()).hexdigest()


def meta_for(project_path, is_dir):
    guid = guid_for(project_path)
    head = f"fileFormatVersion: 2\nguid: {guid}\n"
    if is_dir:
        return head + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + TAIL
    if project_path.endswith(".cs"):
        return head + ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
                       "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n") + TAIL
    if project_path.endswith((".md", ".txt")):
        return head + "TextScriptImporter:\n  externalObjects: {}\n" + TAIL
    return head + "DefaultImporter:\n  externalObjects: {}\n" + TAIL


def entries():
    """Yield (disk_path, project_path, is_dir) for the root folder and everything under it."""
    yield SRC, PROJECT_ROOT, True
    for root, dirs, files in os.walk(SRC):
        dirs.sort()
        rel_root = os.path.relpath(root, SRC)
        for d in dirs:
            rel = os.path.normpath(os.path.join(rel_root, d)).replace(os.sep, "/")
            yield os.path.join(root, d), f"{PROJECT_ROOT}/{rel}", True
        for f in sorted(files):
            if f.endswith(".meta"):
                continue
            rel = os.path.normpath(os.path.join(rel_root, f)).replace(os.sep, "/")
            yield os.path.join(root, f), f"{PROJECT_ROOT}/{rel}", False


def add_bytes(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = 0
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def main():
    items = list(entries())
    for disk, proj, is_dir in items:
        meta = disk + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w", newline="\n") as fh:
                fh.write(meta_for(proj, is_dir))

    with tarfile.open(OUT, "w:gz", format=tarfile.GNU_FORMAT) as tar:
        for disk, proj, is_dir in items:
            with open(disk + ".meta", "rb") as fh:
                meta = fh.read()
            guid = next(l.split(":", 1)[1].strip() for l in meta.decode().splitlines() if l.startswith("guid:"))
            add_bytes(tar, f"{guid}/pathname", proj.encode())
            add_bytes(tar, f"{guid}/asset.meta", meta)
            if not is_dir:
                with open(disk, "rb") as fh:
                    add_bytes(tar, f"{guid}/asset", fh.read())
    print(f"wrote {os.path.relpath(OUT)} ({len(items)} assets)")


if __name__ == "__main__":
    main()
