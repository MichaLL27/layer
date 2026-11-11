"""Export a .blend to FBX (and GLB) for Unity, headless.

Usage:
  blender.exe -b blender/src/house.blend -P tools/blender_export.py -- [--out blender/export] [--glb]

Exports every top-level collection that is not named with a leading "_" as its own file, so a
kit (walls, roof, windows...) arrives in Unity as separate prefabs. If there are no such
collections, the whole scene is exported as one file named after the .blend.
Conventions: 1 unit = 1 m, Unity-friendly axes (-Z forward, Y up), selected objects only.
"""
import os
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out_dir = "blender/export"
want_glb = "--glb" in argv
if "--out" in argv:
    out_dir = argv[argv.index("--out") + 1]
os.makedirs(out_dir, exist_ok=True)

blend_name = os.path.splitext(os.path.basename(bpy.data.filepath))[0] or "scene"


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]


def export(name, objs):
    select_only(objs)
    fbx = os.path.join(out_dir, f"{name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
    )
    print(f"EXPORTED {fbx} ({len(objs)} objects)")
    if want_glb:
        glb = os.path.join(out_dir, f"{name}.glb")
        bpy.ops.export_scene.gltf(filepath=glb, use_selection=True, export_format="GLB")
        print(f"EXPORTED {glb}")


cols = [c for c in bpy.context.scene.collection.children if not c.name.startswith("_")]
exported = 0
for c in cols:
    objs = [o for o in c.all_objects if o.type in {"MESH", "ARMATURE", "EMPTY", "CURVE"}]
    if objs:
        export(c.name, objs)
        exported += 1
if exported == 0:
    objs = [o for o in bpy.context.scene.objects if o.type in {"MESH", "ARMATURE"}]
    export(blend_name, objs)
print(f"DONE {blend_name}: {max(exported, 1)} file(s) in {out_dir}")
