"""Island-video models, built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/island_assets.py
  blender.exe -b blender/src/island_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up, pivot at the foot of the trunk. Material names are resolved by IslandScene.Mat.
"""
import math
import os
import random

import bmesh
import bpy
import mathutils

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MATS = {}
V = mathutils.Vector


def mat(key):
    if key not in MATS:
        m = bpy.data.materials.new(key)
        m.diffuse_color = (0.8, 0.8, 0.8, 1)
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


def bezier(p0, p1, p2, t):
    return p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t * t


def frame_along(tangent):
    t = tangent.normalized()
    side = t.cross(V((0, 0, 1)))
    if side.length < 1e-4:
        side = V((1, 0, 0))
    return t, side.normalized(), side.cross(t).normalized()


def trunk(col, name, height, lean, segs=48, sides=12, rng=None):
    """Curved tube with the ring scars of a coconut palm, a swollen foot, tapering up. Returns the top and its axis."""
    p0, p1, p2 = V((0, 0, 0)), V((lean * 0.15, 0, height * 0.55)), V((lean, 0, height))
    bm = bmesh.new()
    rings = []
    for i in range(segs + 1):
        t = i / segs
        c = bezier(p0, p1, p2, t)
        tan, side, up = frame_along(bezier(p0, p1, p2, min(1, t + 0.01)) - bezier(p0, p1, p2, max(0, t - 0.01)))
        r = 0.135 + 0.11 * math.exp(-t * 9) - 0.035 * t
        r *= 1.0 + 0.055 * (0.5 + 0.5 * math.sin(t * segs * 2.4))     # the ring scars
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            ring.append(bm.verts.new(c + (side * math.cos(a) + up * math.sin(a)) * r))
        rings.append(ring)
    for i in range(segs):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][n], rings[i + 1][n], rings[i + 1][k]))
    bm.faces.new(rings[-1])
    finish(bm, name + "_trunk", col, "palm_bark")
    return p2, (p2 - bezier(p0, p1, p2, 0.9)).normalized()


def frond(bm, base, azimuth, elev, length, droop, rng):
    """A spine that arcs out and falls, with narrow leaflets on both sides, pointing out and hanging a little."""
    out = V((math.cos(azimuth), math.sin(azimuth), 0))
    p0 = base
    p1 = base + out * (length * 0.55 * math.cos(elev)) + V((0, 0, length * 0.55 * math.sin(elev)))
    p2 = base + out * (length * 0.95) + V((0, 0, length * (0.55 * math.sin(elev) - droop)))
    n = 34
    for i in range(1, n):
        t = i / n
        c = bezier(p0, p1, p2, t)
        tan, side, up = frame_along(bezier(p0, p1, p2, min(1, t + 0.02)) - bezier(p0, p1, p2, max(0, t - 0.02)))
        leaf = (0.16 + 0.82 * math.sin(math.pi * min(1, t * 1.08)) ** 0.7) * length * 0.27
        for s in (-1, 1):
            d = (side * s + tan * 0.45 - up * (0.28 + 0.5 * t)).normalized()
            d += V((rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05)))
            w = 0.028 + 0.012 * math.sin(math.pi * t)
            v = []
            for j in range(4):
                u = j / 3
                q = c + d * (leaf * u) - V((0, 0, 0.14 * leaf * u * u))
                wn = w * (1 - u * u)
                v.append((bm.verts.new(q - tan * wn), bm.verts.new(q + tan * wn)))
            for j in range(3):
                bm.faces.new((v[j][0], v[j][1], v[j + 1][1], v[j + 1][0]))
    # the rib
    prev = None
    for i in range(n + 1):
        t = i / n
        c = bezier(p0, p1, p2, t)
        tan, side, up = frame_along(bezier(p0, p1, p2, min(1, t + 0.02)) - bezier(p0, p1, p2, max(0, t - 0.02)))
        w = 0.022 * (1 - t) + 0.004
        a, b = bm.verts.new(c - side * w), bm.verts.new(c + side * w)
        if prev:
            bm.faces.new((prev[0], prev[1], b, a))
        prev = (a, b)


def palm(name, height, lean, fronds, frond_len, seed):
    col = collection(name)
    rng = random.Random(seed)
    top, axis = trunk(col, name, height, lean, rng=rng)
    bm = bmesh.new()
    for i in range(fronds):
        ring = i % 2
        az = 2 * math.pi * i / fronds + rng.uniform(-0.14, 0.14)
        elev = (0.95 if ring == 0 else 0.42) + rng.uniform(-0.1, 0.1)
        frond(bm, top, az, elev, frond_len * rng.uniform(0.88, 1.08), 0.42 + 0.3 * (1 - ring) * 0.2 + rng.uniform(0, 0.16), rng)
    finish(bm, name + "_leaves", col, "palm_leaf", smooth=False)
    bm = bmesh.new()
    for i in range(6):
        a = rng.uniform(0, 2 * math.pi)
        m = mathutils.Matrix.Translation(top + V((math.cos(a) * 0.22, math.sin(a) * 0.22, -0.2 - rng.uniform(0, 0.1))))
        bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=10, radius=0.135, matrix=m)
    finish(bm, name + "_coconuts", col, "coconut")


def main():
    # start from an empty scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    palm("palm_a", 8.6, 2.4, 16, 3.5, 3)
    palm("palm_b", 6.4, -1.5, 14, 3.1, 7)
    palm("palm_c", 10.2, 1.1, 17, 3.7, 11)
    path = os.path.join(ROOT, "blender", "src", "island_assets.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print("SAVED", path)


main()
