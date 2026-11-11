"""Models for the snow cave video, version 3, built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/snow_assets_v3.py
  blender.exe -b blender/src/snow_assets_v3.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up. Front of the person is -Y. Material names are resolved by SnowScene.Mat.
"""
import math
import os
import random

import bmesh
import bpy
import mathutils
from mathutils import noise

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


def smooth_path(points, radii, steps):
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


def tube(bm, points, radii, sides=30, sy=1.0, ripple=None, wrinkle=0.0, steps=10, freq=11.0):
    """a smooth limb; 'wrinkle' pushes the cloth in and out with noise, stretched along the limb so it reads as folds"""
    P, R = smooth_path(points, radii, steps)
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
            d = 1.0
            if wrinkle > 0.0:
                q = c * freq + off * 2.4
                d += wrinkle * (noise.noise(V((q.x, q.y, q.z * 0.45))) + 0.5 * noise.noise(q * 2.3))
            ring.append(bm.verts.new(c + off * rr * d))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][n], rings[i + 1][n], rings[i + 1][k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def ball(bm, c, r, sz=1.0, lump=0.0, seed=0.0):
    res = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=r)
    for v in res["verts"]:
        d = 1.0 + lump * noise.noise(v.co * (9.0 / r * 0.1) + V((seed, seed * 1.7, 0.0)))
        v.co = V((c[0] + v.co.x * d, c[1] + v.co.y * d, c[2] + v.co.z * d * sz))


def puff(i, n):
    return 1.0 + 0.03 * math.sin(i * 0.52)


def pack(bm, ox=0.0, oy=0.3, oz=0.0):
    tube(bm, [(ox, oy, oz + 0.86), (ox, oy, oz + 0.96), (ox, oy, oz + 1.2), (ox, oy, oz + 1.4), (ox, oy, oz + 1.47)], [0.15, 0.2, 0.21, 0.19, 0.09], sy=0.62, wrinkle=0.05, freq=8.0)


def person():
    col = collection("snow_person")
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.115, 0.0, 0.95), (sgn * 0.13, -0.015, 0.72), (sgn * 0.13, 0.0, 0.5), (sgn * 0.125, 0.015, 0.28), (sgn * 0.12, 0.0, 0.13)], [0.135, 0.125, 0.105, 0.098, 0.09], wrinkle=0.07)
    finish(bm, "trousers", col, "snow_pants")
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.12, 0.05, 0.1), (sgn * 0.12, 0.0, 0.085), (sgn * 0.125, -0.09, 0.07), (sgn * 0.125, -0.2, 0.06)], [0.098, 0.105, 0.09, 0.055], wrinkle=0.02)
    finish(bm, "boots", col, "snow_boot")
    bm = bmesh.new()
    tube(bm, [(0, 0.0, 0.68), (0, 0.0, 0.82), (0, 0.0, 1.05), (0, 0.0, 1.28), (0, 0.0, 1.45), (0, 0.0, 1.55)], [0.265, 0.27, 0.28, 0.29, 0.22, 0.09], sy=0.74, ripple=puff, wrinkle=0.09, sides=48, freq=9.0)
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.27, 0.0, 1.43), (sgn * 0.37, 0.0, 1.25), (sgn * 0.42, -0.03, 1.05), (sgn * 0.41, -0.09, 0.86)], [0.115, 0.115, 0.1, 0.09], ripple=puff, wrinkle=0.1, freq=13.0)
    finish(bm, "parka", col, "snow_parka")
    bm = bmesh.new()
    for sgn in (-1, 1):
        ball(bm, (sgn * 0.405, -0.11, 0.78), 0.083, 1.2)
    finish(bm, "mitts", col, "snow_pants")
    bm = bmesh.new()
    tube(bm, [(0, 0.03, 1.5), (0, 0.06, 1.62), (0, 0.03, 1.74), (0, -0.04, 1.84), (0, -0.07, 1.9)], [0.13, 0.18, 0.19, 0.14, 0.03], sy=1.08, wrinkle=0.08, freq=9.0)
    finish(bm, "hood", col, "snow_parka")
    bm = bmesh.new()
    rng = random.Random(4)
    for k in range(70):
        a = 2 * math.pi * k / 70
        c = V((0.14 * math.cos(a), -0.17, 1.68 + 0.14 * math.sin(a)))
        ball(bm, c, 0.032 + rng.random() * 0.02, lump=0.5, seed=k)
    finish(bm, "fur", col, "snow_fur")
    bm = bmesh.new()
    pack(bm)
    finish(bm, "pack", col, "snow_pack")
    bm = bmesh.new()
    tube(bm, [(-0.3, 0.3, 1.52), (0, 0.3, 1.52), (0.3, 0.3, 1.52)], [0.08, 0.08, 0.08], wrinkle=0.04)
    finish(bm, "mat", col, "snow_mat")
    bm = bmesh.new()
    for x in (-0.11, 0.11):
        tube(bm, [(x, 0.17, 1.46), (x, 0.17, 1.2), (x, 0.17, 0.95)], [0.03, 0.03, 0.03], sides=10, sy=0.5)
    finish(bm, "straps", col, "snow_boot")
    # snow that has settled on him
    bm = bmesh.new()
    rng = random.Random(7)
    spots = [((sgn * 0.26, 0.02, 1.53), 0.09) for sgn in (-1, 1)] + [((0.0, 0.0, 1.9), 0.08), ((-0.15, 0.3, 1.6), 0.09), ((0.16, 0.3, 1.6), 0.08), ((0.02, 0.32, 1.48), 0.1), ((-0.4, 0.0, 1.32), 0.06), ((0.4, 0.0, 1.32), 0.06)]
    for k, (c, r) in enumerate(spots):
        ball(bm, c, r, 0.5, lump=0.8, seed=k * 3.1)
    finish(bm, "settled", col, "pine_snow")


def person_sit():
    """huddled on the floor of the first hole, arms round the knees, seen from the side"""
    col = collection("snow_person_sit")
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.12, 0.0, 0.2), (sgn * 0.13, -0.2, 0.38), (sgn * 0.14, -0.4, 0.52), (sgn * 0.14, -0.46, 0.3), (sgn * 0.14, -0.47, 0.12)], [0.13, 0.125, 0.105, 0.095, 0.085], wrinkle=0.07)
    finish(bm, "trousers", col, "snow_pants")
    bm = bmesh.new()
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.14, -0.45, 0.1), (sgn * 0.14, -0.52, 0.08), (sgn * 0.14, -0.66, 0.07)], [0.1, 0.095, 0.055], wrinkle=0.02)
    finish(bm, "boots", col, "snow_boot")
    bm = bmesh.new()
    tube(bm, [(0, 0.04, 0.12), (0, 0.03, 0.24), (0, -0.04, 0.46), (0, -0.14, 0.66), (0, -0.2, 0.76)], [0.25, 0.27, 0.28, 0.25, 0.1], sy=0.8, ripple=puff, wrinkle=0.09, sides=48, freq=9.0)
    for sgn in (-1, 1):
        tube(bm, [(sgn * 0.26, -0.12, 0.66), (sgn * 0.34, -0.26, 0.5), (sgn * 0.24, -0.42, 0.42), (sgn * 0.06, -0.54, 0.4)], [0.11, 0.105, 0.09, 0.08], ripple=puff, wrinkle=0.1, freq=13.0)
    finish(bm, "parka", col, "snow_parka")
    bm = bmesh.new()
    tube(bm, [(0, -0.16, 0.72), (0, -0.25, 0.82), (0, -0.33, 0.92), (0, -0.37, 1.0), (0, -0.37, 1.05)], [0.13, 0.18, 0.19, 0.14, 0.03], sy=1.08, wrinkle=0.08, freq=9.0)
    finish(bm, "hood", col, "snow_parka")


def bag():
    col = collection("snow_bag")
    bm = bmesh.new()
    def quilt(i, n):
        return 1.0 + 0.055 * math.cos(i * 0.71)
    tube(bm, [(-1.05, 0, 0), (-0.95, 0, 0), (-0.6, 0, 0.0), (-0.1, 0, 0.0), (0.4, 0, 0.0), (0.72, 0, 0.0), (0.88, 0, 0.0)], [0.08, 0.2, 0.28, 0.335, 0.35, 0.3, 0.27], sides=44, ripple=quilt, wrinkle=0.05, steps=14, freq=7.0)
    finish(bm, "bag_body", col, "bag_outer")
    bm = bmesh.new()
    tube(bm, [(0.82, 0, 0.0), (0.95, 0, 0.03), (1.12, 0, 0.03), (1.22, 0, 0.02), (1.27, 0, 0.0)], [0.27, 0.27, 0.25, 0.2, 0.11], sides=44, wrinkle=0.05)
    finish(bm, "bag_hood", col, "bag_outer")
    bm = bmesh.new()
    ball(bm, (1.11, -0.2, 0.05), 0.125, 1.15)
    finish(bm, "bag_face", col, "snow_face")
    bm = bmesh.new()
    tube(bm, [(0.95, -0.27, -0.1), (0.95, -0.27, 0.05), (0.98, -0.27, 0.2), (1.12, -0.26, 0.25), (1.26, -0.25, 0.17), (1.28, -0.25, 0.0), (1.14, -0.26, -0.13), (0.95, -0.27, -0.1)], [0.024] * 8, sides=10, steps=6)
    finish(bm, "bag_rim", col, "bag_inner")


def gear():
    """the rucksack, standing on the bed by the sleeper's head"""
    col = collection("snow_gear")
    bm = bmesh.new()
    pack(bm, 0.0, 0.0, -0.86)
    finish(bm, "pack", col, "snow_pack")
    bm = bmesh.new()
    tube(bm, [(-0.3, 0.0, 0.66), (0, 0.0, 0.66), (0.3, 0.0, 0.66)], [0.08, 0.08, 0.08], wrinkle=0.04)
    finish(bm, "mat", col, "snow_mat")


def blade(g, w, origin, az, L, width, droop, rng, twigs):
    """one branch: a drooping, ragged sheet of needles with a lumpy strip of snow lying on it"""
    d = V((math.cos(az), math.sin(az), 0.0))
    side = V((-math.sin(az), math.cos(az), 0.0))
    N = 7
    def spine(s):
        return origin + d * (L * s) + V((0, 0, 0.12 * L * s - droop * s * s))
    def wid(s):
        return width * math.sin(math.pi * min(s * 1.04 + 0.04, 1.0)) ** 0.7
    left, mid, right = [], [], []
    for k in range(N):
        s = k / (N - 1)
        c = spine(s)
        jag = 0.75 + 0.45 * (k % 2) + rng.random() * 0.15
        wd = wid(s) * jag
        mid.append(g.verts.new(c + V((0, 0, 0.05 * L * (1 - s)))))
        left.append(g.verts.new(c + side * wd - V((0, 0, wd * 0.45))))
        right.append(g.verts.new(c - side * wd - V((0, 0, wd * 0.45))))
    for k in range(N - 1):
        g.faces.new((mid[k], left[k], left[k + 1], mid[k + 1]))
        g.faces.new((mid[k], mid[k + 1], right[k + 1], right[k]))
    a, b, c_ = [], [], []
    for k in range(N):
        s = k / (N - 1) * 0.86
        c = spine(s)
        wd = wid(s) * (0.55 + 0.25 * rng.random())
        lift = 0.1 + 0.1 * rng.random()
        c_.append(w.verts.new(c + V((0, 0, 0.05 * L * (1 - s) + lift + 0.05))))
        a.append(w.verts.new(c + side * wd + V((0, 0, -wd * 0.3 + 0.04))))
        b.append(w.verts.new(c - side * wd + V((0, 0, -wd * 0.3 + 0.04))))
    for k in range(N - 1):
        w.faces.new((c_[k], a[k], a[k + 1], c_[k + 1]))
        w.faces.new((c_[k], c_[k + 1], b[k + 1], b[k]))
    if twigs:
        for sgn in (-1, 1):
            for at in (0.42, 0.68):
                blade(g, w, spine(at), az + sgn * 0.75, L * 0.42 * (1.2 - at), width * 0.55, droop * 0.3, rng, False)


def pine(name, seed):
    col = collection(name)
    rng = random.Random(seed)
    H = 13.0
    bm = bmesh.new()
    tube(bm, [(0, 0, 0), (0, 0, 2.0), (0, 0, 6.0), (0.02, 0, H)], [0.27, 0.22, 0.15, 0.02], sides=10, steps=4)
    finish(bm, "trunk", col, "pine_trunk")
    g, w = bmesh.new(), bmesh.new()
    tiers = 22
    for t in range(tiers):
        u = t / (tiers - 1)
        z = 1.5 + u * (H - 1.9)
        L = 3.3 * (1 - u) ** 0.9 + 0.32
        n = int(5 + 7 * (1 - u))
        for k in range(n):
            az = 2 * math.pi * k / n + rng.random() * 0.5 + t * 1.1
            LL = L * (0.8 + 0.35 * rng.random())
            blade(g, w, V((0, 0, z + rng.random() * 0.2)), az, LL, LL * 0.2 + 0.08, LL * (0.34 + 0.22 * (1 - u)), rng, LL > 1.1)
    finish(g, "needles", col, "pine_needle", smooth=False)
    finish(w, "snowcap", col, "pine_snow", smooth=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    person()
    person_sit()
    bag()
    gear()
    pine("pine_a", 3)
    pine("pine_b", 8)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blender", "src", "snow_assets_v3.blend"))
    print("SAVED snow_assets_v3.blend")


main()
