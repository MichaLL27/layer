"""More island-video models (the second half of the video), built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/island_props.py
  blender.exe -b blender/src/island_props.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up. Material names are resolved by IslandScene.Mat.
"""
import math
import os
import random

import bmesh
import bpy
import mathutils

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
V = mathutils.Vector
MATS = {}


def mat(key):
    if key not in MATS:
        m = bpy.data.materials.new(key)
        MATS[key] = m
    return MATS[key]


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def finish(bm, name, col, matkey, smooth=True):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(matkey))
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def tube(bm, points, radii, sides=10):
    rings = []
    for i, c in enumerate(points):
        a = points[max(i - 1, 0)]; b = points[min(i + 1, len(points) - 1)]
        t = (b - a).normalized()
        side = t.cross(V((0.13, 0.27, 0.95))).normalized()
        up = side.cross(t).normalized()
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(c + (side * math.cos(ang) + up * math.sin(ang)) * radii[i]))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][n], rings[i + 1][n], rings[i + 1][k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def driftwood(name, length, r0, seed):
    """A bleached, slightly bent log along +Z, pivot at its lower end."""
    col = collection(name)
    rng = random.Random(seed)
    n = 12
    bend = (rng.uniform(-0.07, 0.07), rng.uniform(-0.07, 0.07))
    pts, rad = [], []
    for i in range(n + 1):
        t = i / n
        q = math.sin(t * math.pi)
        pts.append(V((bend[0] * q + 0.012 * math.sin(t * 19 + seed), bend[1] * q + 0.012 * math.cos(t * 17 + seed), t * length)))
        rad.append(r0 * (1.0 - 0.28 * t) * (1 + 0.08 * math.sin(t * 23 + seed * 2)))
    bm = bmesh.new()
    tube(bm, pts, rad)
    finish(bm, name + "_log", col, "driftwood")


def cook_pot():
    """A dented open tin with a wire handle, water in it; pivot at the centre of its base."""
    col = collection("cook_pot")
    r, h = 0.16, 0.24
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=32, radius1=r, radius2=r * 1.02, depth=h,
                          matrix=mathutils.Matrix.Translation((0, 0, h / 2)))
    finish(bm, "pot_body", col, "pot_metal")
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=32, radius=r * 0.97, matrix=mathutils.Matrix.Translation((0, 0, h * 0.86)))
    finish(bm, "pot_water", col, "pot_water", smooth=False)
    bm = bmesh.new()
    pts = []
    for i in range(25):
        a = math.pi * i / 24
        pts.append(V((r * 1.02 * math.cos(a), 0, h + r * 0.95 * math.sin(a))))
    tube(bm, pts, [0.006] * len(pts), sides=6)
    finish(bm, "pot_wire", col, "pot_metal", smooth=False)


def hut(name, r, wall, roof, seed):
    """A round hut: posts and palm-leaf walls, a shaggy cone of thatch. Pivot on the ground at the centre."""
    col = collection(name)
    rng = random.Random(seed)
    bm = bmesh.new()
    segs = 40
    for i in range(segs):
        a0, a1 = 2 * math.pi * i / segs, 2 * math.pi * (i + 0.86) / segs
        if abs(a0 - 4.71) < 0.32:              # the doorway, toward the camera
            continue
        w = wall * rng.uniform(0.96, 1.04)
        v = [bm.verts.new((r * math.cos(a0), r * math.sin(a0), 0)), bm.verts.new((r * math.cos(a1), r * math.sin(a1), 0)),
             bm.verts.new((r * math.cos(a1), r * math.sin(a1), w)), bm.verts.new((r * math.cos(a0), r * math.sin(a0), w))]
        bm.faces.new(v)
    finish(bm, name + "_wall", col, "hut_wall", smooth=False)
    bm = bmesh.new()
    layers = 6
    for L in range(layers):
        t0, t1 = L / layers, (L + 1.5) / layers
        count = 46 - L * 4
        for i in range(count):
            a = 2 * math.pi * (i + rng.uniform(-0.3, 0.3)) / count
            da = 2 * math.pi / count * 0.72
            over = 1.28 - 1.28 * t0
            ro = (r + 0.32) * (1 - t0) + 0.03
            ri = (r + 0.32) * (1 - min(t1, 1)) * (1 - 0.0) + 0.02
            zo = wall - 0.1 + roof * t0 * 0.98
            zi = wall - 0.1 + roof * min(t1, 1) * 0.98
            droop = rng.uniform(0, 0.06)
            v = [bm.verts.new((ro * math.cos(a - da), ro * math.sin(a - da), zo - droop)),
                 bm.verts.new((ro * math.cos(a + da), ro * math.sin(a + da), zo - droop)),
                 bm.verts.new((ri * math.cos(a + da * 0.6), ri * math.sin(a + da * 0.6), zi)),
                 bm.verts.new((ri * math.cos(a - da * 0.6), ri * math.sin(a - da * 0.6), zi))]
            bm.faces.new(v)
            _ = over
    finish(bm, name + "_roof", col, "hut_roof", smooth=False)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    driftwood("driftwood_a", 1.25, 0.085, 2)
    driftwood("driftwood_b", 1.32, 0.075, 5)
    driftwood("driftwood_c", 1.18, 0.09, 9)
    cook_pot()
    hut("hut_a", 1.55, 1.25, 2.1, 4)
    hut("hut_b", 1.25, 1.1, 1.85, 8)
    path = os.path.join(ROOT, "blender", "src", "island_props.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print("SAVED", path)


main()
