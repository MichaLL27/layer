"""Models for the snow cave video, built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/snow_assets.py
  blender.exe -b blender/src/snow_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up. Front of the person is -Y. Material names are resolved by SnowScene.Mat.
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
        MATS[key] = bpy.data.materials.new(key)
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


def smooth_path(points, radii, steps=5):
    """Catmull-Rom through the points, so a limb bends as a curve and its radius changes gently"""
    pts = [V(p) for p in points]
    P, R = [], []
    n = len(pts)
    for i in range(n - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, n - 1)]
        r0, r1, r2, r3 = radii[max(i - 1, 0)], radii[i], radii[i + 1], radii[min(i + 2, n - 1)]
        for s in range(steps):
            t = s / steps
            a, b, c, d = -t**3 + 2 * t**2 - t, 3 * t**3 - 5 * t**2 + 2, -3 * t**3 + 4 * t**2 + t, t**3 - t**2
            P.append((p0 * a + p1 * b + p2 * c + p3 * d) * 0.5)
            R.append(max(0.004, (r0 * a + r1 * b + r2 * c + r3 * d) * 0.5))
    P.append(pts[-1])
    R.append(radii[-1])
    return P, R


def tube(bm, points, radii, sides=18, sy=1.0, ripple=None):
    P, R = smooth_path(points, radii)
    rings = []
    ref = V((0.13, 0.27, 0.95))
    for i, c in enumerate(P):
        a, b = P[max(i - 1, 0)], P[min(i + 1, len(P) - 1)]
        t = (b - a).normalized()
        if abs(t.dot(ref.normalized())) > 0.98:
            ref = V((0.9, 0.1, 0.1))
        side = t.cross(ref).normalized()
        up = side.cross(t).normalized()
        rr = R[i] * (ripple(i, len(P)) if ripple else 1.0)
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            off = side * math.cos(ang) + up * math.sin(ang)
            off = V((off.x, off.y * sy, off.z))
            ring.append(bm.verts.new(c + off * rr))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][n], rings[i + 1][n], rings[i + 1][k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def ball(bm, c, r, sz=1.0):
    res = bmesh.ops.create_uvsphere(bm, u_segments=18, v_segments=12, radius=r)
    for v in res["verts"]:
        v.co = V((c[0] + v.co.x, c[1] + v.co.y, c[2] + v.co.z * sz))


def person():
    col = collection("snow_person")
    # trousers and boots
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.115, 0.0, 0.95), (sgn * 0.13, -0.015, 0.72), (sgn * 0.13, 0.0, 0.5), (sgn * 0.125, 0.015, 0.28), (sgn * 0.12, 0.0, 0.13)], [0.135, 0.125, 0.105, 0.098, 0.09])
    finish(bm, "trousers", col, "snow_pants")
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.12, 0.05, 0.1), (sgn * 0.12, 0.0, 0.085), (sgn * 0.125, -0.09, 0.07), (sgn * 0.125, -0.2, 0.06)], [0.098, 0.105, 0.09, 0.055])
    finish(bm, "boots", col, "snow_boot")
    # the parka: a long, puffy body, baffles round the middle
    bm = bmesh.new()
    def puff(i, n):
        return 1.0 + 0.035 * math.sin(i * 1.05)
    tube(bm, [(0, 0.0, 0.68), (0, 0.0, 0.82), (0, 0.0, 1.05), (0, 0.0, 1.28), (0, 0.0, 1.45), (0, 0.0, 1.55)], [0.265, 0.27, 0.28, 0.29, 0.22, 0.09], sy=0.74, ripple=puff)
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.27, 0.0, 1.43), (sgn * 0.37, 0.0, 1.25), (sgn * 0.42, -0.03, 1.05), (sgn * 0.41, -0.09, 0.86)], [0.115, 0.115, 0.1, 0.09], ripple=puff)
    finish(bm, "parka", col, "snow_parka")
    bm = bmesh.new()
    for sgn in (-1, 1):
        ball(bm, (sgn * 0.405, -0.11, 0.78), 0.083, 1.2)
    finish(bm, "mitts", col, "snow_pants")
    # the hood and a band of fur at its edge
    bm = bmesh.new()
    tube(bm, [(0, 0.03, 1.5), (0, 0.04, 1.62), (0, 0.02, 1.76), (0, 0.0, 1.86), (0, 0.0, 1.91)], [0.13, 0.18, 0.19, 0.14, 0.03], sy=1.08)
    finish(bm, "hood", col, "snow_parka")
    bm = bmesh.new()
    rng = random.Random(4)
    for k in range(46):
        a = 2 * math.pi * k / 46
        c = V((0.145 * math.cos(a), -0.14, 1.71 + 0.155 * math.sin(a)))
        ball(bm, c, 0.04 + rng.random() * 0.018)
    finish(bm, "fur", col, "snow_fur")
    # a rucksack with a rolled mat on top
    bm = bmesh.new()
    tube(bm, [(0, 0.3, 0.86), (0, 0.3, 0.96), (0, 0.3, 1.2), (0, 0.3, 1.4), (0, 0.3, 1.47)], [0.15, 0.2, 0.21, 0.19, 0.09], sy=0.62)
    finish(bm, "pack", col, "snow_pack")
    bm = bmesh.new()
    tube(bm, [(-0.3, 0.3, 1.52), (0, 0.3, 1.52), (0.3, 0.3, 1.52)], [0.08, 0.08, 0.08])
    finish(bm, "mat", col, "snow_mat")
    bm = bmesh.new()
    for x in (-0.11, 0.11):
        tube(bm, [(x, 0.17, 1.46), (x, 0.17, 1.2), (x, 0.17, 0.95)], [0.03, 0.03, 0.03], sides=8, sy=0.5)
    finish(bm, "straps", col, "snow_boot")


def bag():
    """a mummy sleeping bag lying along +x, head at +x, with a sleeper's face in the hood"""
    col = collection("snow_bag")
    bm = bmesh.new()
    def quilt(i, n):
        return 1.0 + 0.06 * math.cos(i * 1.57)
    tube(bm, [(-1.05, 0, 0), (-0.95, 0, 0), (-0.6, 0, 0.0), (-0.1, 0, 0.0), (0.4, 0, 0.0), (0.72, 0, 0.0), (0.88, 0, 0.0)], [0.08, 0.2, 0.28, 0.335, 0.35, 0.3, 0.27], sides=26, ripple=quilt)
    finish(bm, "bag_body", col, "bag_outer")
    bm = bmesh.new()
    tube(bm, [(0.82, 0, 0.0), (0.95, 0, 0.03), (1.12, 0, 0.03), (1.22, 0, 0.02), (1.27, 0, 0.0)], [0.27, 0.27, 0.25, 0.2, 0.11], sides=26)
    finish(bm, "bag_hood", col, "bag_outer")
    bm = bmesh.new()
    ball(bm, (1.11, -0.2, 0.05), 0.125, 1.15)
    finish(bm, "bag_face", col, "snow_face")
    bm = bmesh.new()
    a0 = [V((1.05 + 0.01 * k, -0.16 - 0.0, 0.03)) for k in range(1)]
    tube(bm, [(0.95, -0.27, -0.1), (0.95, -0.27, 0.05), (0.98, -0.27, 0.2), (1.12, -0.26, 0.25), (1.26, -0.25, 0.17), (1.28, -0.25, 0.0), (1.14, -0.26, -0.13), (0.95, -0.27, -0.1)], [0.024] * 8, sides=8)
    finish(bm, "bag_rim", col, "bag_inner")


def pine(name, seed):
    col = collection(name)
    rng = random.Random(seed)
    bm = bmesh.new()
    tube(bm, [(0, 0, 0), (0, 0, 2.0), (0, 0, 5.0), (0, 0, 11.0)], [0.26, 0.22, 0.14, 0.03], sides=8)
    finish(bm, "trunk", col, "pine_trunk")
    H = 12.0
    tiers = 9
    green = bmesh.new()
    white = bmesh.new()
    for t in range(tiers):
        u = t / (tiers - 1)
        z0 = 1.3 + u * (H - 3.0)
        h = 2.4 - 0.9 * u
        R = 3.0 * (1 - u) ** 0.85 + 0.35
        n = 22
        rim, rim2 = [], []
        for k in range(n):
            a = 2 * math.pi * k / n + t * 0.4
            r = R * (0.72 + 0.4 * ((k % 2) * 0.8 + rng.random() * 0.25))
            droop = 0.28 * (k % 2) + rng.random() * 0.1
            rim.append(green.verts.new((r * math.cos(a), r * math.sin(a), z0 - droop)))
            r2 = r * 0.84
            rim2.append(white.verts.new((r2 * math.cos(a), r2 * math.sin(a), z0 - droop + 0.42)))
        tip = green.verts.new((0, 0, z0 + h))
        tip2 = white.verts.new((0, 0, z0 + h + 0.1))
        for k in range(n):
            m = (k + 1) % n
            green.faces.new((rim[k], rim[m], tip))
            white.faces.new((rim2[k], rim2[m], tip2))
        green.faces.new(list(reversed(rim)))
    finish(green, "needles", col, "pine_needle", smooth=False)
    finish(white, "snowcap", col, "pine_snow", smooth=False)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    person()
    bag()
    pine("pine_a", 3)
    pine("pine_b", 8)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blender", "src", "snow_assets.blend"))
    print("SAVED snow_assets.blend")


main()
