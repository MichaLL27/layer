"""Quick Workbench preview of one collection of a .blend, to check a model's shape before export.

  blender.exe -b blender/src/car_assets.blend -P tools/blender_preview.py -- --col car_body --out output/prev.png [--view side|34|front|top]
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
opt = dict(zip(argv[::2], argv[1::2]))
col = opt.get("--col", "car_body")
out = os.path.abspath(opt["--out"])
view = opt.get("--view", "34")

for c in bpy.data.collections:
    c.hide_render = c.name != col
obs = [o for o in bpy.data.collections[col].all_objects if o.type == 'MESH']
lo = mathutils.Vector((1e9, 1e9, 1e9)); hi = -lo
for o in obs:
    for v in o.bound_box:
        w = o.matrix_world @ mathutils.Vector(v)
        lo = mathutils.Vector(map(min, lo, w)); hi = mathutils.Vector(map(max, hi, w))
centre = (lo + hi) / 2
size = (hi - lo).length
dirs = {"side": (0, -1, 0.15), "34": (-0.8, -1, 0.45), "front": (-1, 0, 0.2), "top": (0, -0.05, 1), "rear34": (0.9, -1, 0.5)}
d = mathutils.Vector(dirs[view]).normalized()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
bpy.context.scene.collection.objects.link(cam)
cam.location = centre + d * size * 1.25
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 50
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'MATERIAL'
sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 1400, 900
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("PREVIEW", out)
