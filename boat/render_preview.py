"""Renders preview.png by importing the exported Boat.fbx (python3 render_preview.py [all])."""
import math
import os
import sys

import bpy
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "Boat.fbx"))
for o in bpy.data.objects:
    if o.name.startswith("Boat_Collider"):
        o.hide_render = True
    print("imported", o.name, tuple(round(v, 2) for v in o.location), tuple(round(v, 2) for v in o.rotation_euler), [m.name for m in o.data.materials])
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 48
sc.cycles.device = "CPU"
sc.render.resolution_x, sc.render.resolution_y = 1280, 720

water = bpy.data.materials.new("Water")
water.use_nodes = True
b = water.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.03, 0.12, 0.14, 1)
b.inputs["Roughness"].default_value = 0.15
b.inputs["Alpha"].default_value = 0.85
bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.17))  # just under the floorboards
bpy.context.object.data.materials.append(water)

world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.62, 0.68, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(sun)

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 40


def shot(name, loc, target=(0, 0, 0.2)):
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)


views = sys.argv[1:] or ["preview"]
shot("preview.png", (4.2, 4.6, 2.6))
if "all" in views:
    shot("preview_front.png", (-3.5, -5.0, 2.0))
    shot("preview_top.png", (0.01, 0.0, 8.0), (0, 0, 0))
