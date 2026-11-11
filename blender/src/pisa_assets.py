"""The tower for the Pisa video, built headless in Blender 5.1, in two halves so it can be sliced like a cake.

  blender.exe -b --python blender/src/pisa_assets.py
  blender.exe -b blender/src/pisa_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up, origin at the middle of the tower at ground level. pisa_a is the half with y > 0, pisa_b y < 0.
Material names are resolved by PisaScene.Mat.
"""
import math
import os

import bmesh
import bpy
import mathutils

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
V = mathutils.Vector
MATS = {}

R_IN = 3.68
F0 = 11.5
FH = 6.6
TIERS = 6
SEG = 72


def mat(key):
    if key not in MATS:
        MATS[key] = bpy.data.materials.new(key)
    return MATS[key]


def finish(bm, name, col, matkey, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(matkey))
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def profile():
    """closed outline (r, z), counter-clockwise: the masonry cut through on one side of the axis"""
    p = [(0.0, -3.0), (7.74, -3.0), (7.74, 0.0), (7.6, 0.6), (7.6, 11.0), (7.95, 11.0), (7.95, 11.5)]
    for i in range(TIERS):
        f = F0 + FH * i
        p += [(6.1, f), (6.1, f + 6.1), (7.95, f + 6.1), (7.95, f + 6.6)]
    p += [(5.0, F0 + FH * TIERS), (5.0, 57.0), (5.45, 57.0), (5.45, 57.5), (0.0, 57.5), (0.0, 56.9), (R_IN, 56.9), (R_IN, 1.2), (0.0, 1.2)]
    # the slab tops were appended as their outer corner; join them back to the wall line
    out = []
    for q in p:
        if not out or abs(q[0] - out[-1][0]) > 1e-6 or abs(q[1] - out[-1][1]) > 1e-6:
            out.append(q)
    return out


def revolve(col, name, prof, a0, a1, matkey="pisa_marble"):
    """one band of faces per outline edge: smooth round the tower, a hard edge at every cornice"""
    bm = bmesh.new()
    n = len(prof)
    for e in range(n):
        p0, p1 = prof[e], prof[(e + 1) % n]
        if p0[0] < 1e-6 and p1[0] < 1e-6:
            continue
        rings = []
        for s in range(SEG + 1):
            th = a0 + (a1 - a0) * s / SEG
            c, si = math.cos(th), math.sin(th)
            rings.append((bm.verts.new((p0[0] * c, p0[0] * si, p0[1])), bm.verts.new((p1[0] * c, p1[0] * si, p1[1]))))
        for s in range(SEG):
            bm.faces.new((rings[s][0], rings[s + 1][0], rings[s + 1][1], rings[s][1]))
    return finish(bm, name, col, matkey)


def caps(col, name, prof):
    """the cut face: the outline drawn on the plane y = 0, on both sides of the axis"""
    bm = bmesh.new()
    n = len(prof)
    zs = sorted({round(p[1], 4) for p in prof})
    for k in range(len(zs) - 1):
        z0, z1 = zs[k], zs[k + 1]
        mid = (z0 + z1) / 2
        hits = []
        for e in range(n):
            a, b = prof[e], prof[(e + 1) % n]
            if abs(a[1] - b[1]) < 1e-6 or not (min(a[1], b[1]) < mid < max(a[1], b[1])):
                continue
            ra = a[0] + (b[0] - a[0]) * (z0 - a[1]) / (b[1] - a[1])
            rb = a[0] + (b[0] - a[0]) * (z1 - a[1]) / (b[1] - a[1])
            hits.append((ra + rb, ra, rb))
        hits.sort()
        for j in range(0, len(hits) - 1, 2):
            _, i0, i1 = hits[j]
            _, o0, o1 = hits[j + 1]
            # the solid between two crossings of the outline, on both sides of the axis; faces look along +y
            right = [(i0, z0), (i1, z1), (o1, z1), (o0, z0)]
            left = [(-o0, z0), (-o1, z1), (-i1, z1), (-i0, z0)]
            for q in (right, left):
                bm.faces.new([bm.verts.new((x, 0.0, z)) for x, z in q])
    return finish(bm, name, col, "pisa_section", smooth=False)


def column(bm, x, y, z0, z1, r, sides=10):
    levels = [(z0, r * 1.45), (z0 + 0.14, r * 1.45), (z0 + 0.2, r * 1.05), (z1 - 0.34, r * 0.86), (z1 - 0.14, r * 1.5), (z1, r * 1.8)]
    rings = []
    for z, rr in levels:
        rings.append([bm.verts.new((x + rr * math.cos(2 * math.pi * k / sides), y + rr * math.sin(2 * math.pi * k / sides), z)) for k in range(sides)])
    for i in range(len(rings) - 1):
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][m], rings[i + 1][m], rings[i + 1][k]))
    bm.faces.new(rings[-1])


def arcade(col, name, a0, a1, bays, offs, R_col, r_col, z_floor, z_spring, z_top, R_o, R_i):
    """columns and the arches over them, for the bays that lie inside the half a0..a1"""
    bm = bmesh.new()
    step = 2 * math.pi / bays
    k = 0
    while k < bays:
        b0 = offs + k * step
        b1 = b0 + step
        k += 1
        mid = (b0 + b1) / 2
        if not (a0 < mid < a1):
            continue
        if a0 < b0 < a1:
            column(bm, R_col * math.cos(b0), R_col * math.sin(b0), z_floor, z_spring, r_col)
        rise = (b1 - b0) * R_col / 2
        N = 14
        def pt(R, th, z):
            return bm.verts.new((R * math.cos(th), R * math.sin(th), z))
        lo = [z_spring + rise * math.sin(math.pi * j / N) for j in range(N + 1)]
        th = [b0 + (b1 - b0) * j / N for j in range(N + 1)]
        for j in range(N):
            bm.faces.new((pt(R_o, th[j], lo[j]), pt(R_o, th[j + 1], lo[j + 1]), pt(R_o, th[j + 1], z_top), pt(R_o, th[j], z_top)))
            bm.faces.new((pt(R_i, th[j + 1], lo[j + 1]), pt(R_i, th[j], lo[j]), pt(R_i, th[j], z_top), pt(R_i, th[j + 1], z_top)))
            bm.faces.new((pt(R_i, th[j], lo[j]), pt(R_i, th[j + 1], lo[j + 1]), pt(R_o, th[j + 1], lo[j + 1]), pt(R_o, th[j], lo[j])))
    return finish(bm, name, col, "pisa_marble")


def half(name, a0, a1):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    prof = profile()
    revolve(col, name + "_body", prof, a0, a1)
    caps(col, name + "_cut", prof)
    # the ground storey: blind arches on half-columns
    arcade(col, name + "_ground", a0, a1, 15, 0.1, 7.78, 0.26, 0.6, 8.2, 11.0, 7.9, 7.62)
    for i in range(TIERS):
        f = F0 + FH * i
        arcade(col, f"{name}_tier{i}", a0, a1, 30, 0.052, 7.38, 0.19, f, f + 5.0, f + 6.1, 7.55, 7.2)
    arcade(col, name + "_bell", a0, a1, 12, 0.13, 5.18, 0.2, F0 + FH * TIERS, F0 + FH * TIERS + 3.0, 57.0, 5.33, 5.02)


def box(bm, c, s):
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]:
        v.co = V((c[0] + v.co.x * s[0], c[1] + v.co.y * s[1], c[2] + v.co.z * s[2]))


def wedge(bm, x0, x1, half_w, z0, z1):
    """a gabled roof along x"""
    a = [bm.verts.new(p) for p in ((x0, -half_w, z0), (x1, -half_w, z0), (x1, 0, z1), (x0, 0, z1))]
    b = [bm.verts.new(p) for p in ((x0, half_w, z0), (x1, half_w, z0), (x1, 0, z1), (x0, 0, z1))]
    bm.faces.new((a[0], a[1], a[2], a[3]))
    bm.faces.new((b[3], b[2], b[1], b[0]))


def wedge_y(bm, y0, y1, cx, half_w, z0, z1):
    """a gabled roof along y"""
    a = [bm.verts.new(p) for p in ((cx - half_w, y0, z0), (cx - half_w, y1, z0), (cx, y1, z1), (cx, y0, z1))]
    b = [bm.verts.new(p) for p in ((cx + half_w, y0, z0), (cx + half_w, y1, z0), (cx, y1, z1), (cx, y0, z1))]
    bm.faces.new((a[3], a[2], a[1], a[0]))
    bm.faces.new((b[0], b[1], b[2], b[3]))


def duomo():
    """the cathedral across the lawn: seen far off, so the masses and the striped marble matter, not the detail"""
    col = bpy.data.collections.new("pisa_duomo")
    bpy.context.scene.collection.children.link(col)
    bm = bmesh.new()
    box(bm, (0, 0, 16), (90, 18, 32))
    box(bm, (0, 13.5, 9.5), (90, 9, 19))
    box(bm, (0, -13.5, 9.5), (90, 9, 19))
    box(bm, (8, 0, 14), (20, 72, 28))
    finish(bm, "duomo_walls", col, "pisa_stripe", smooth=False)
    bm = bmesh.new()
    wedge(bm, -45, 45, 9.4, 32, 39)
    wedge_y(bm, -36, 36, 8, 10.4, 28, 35)
    box(bm, (0, 13.5, 19.3), (90.6, 9.6, 0.7))
    box(bm, (0, -13.5, 19.3), (90.6, 9.6, 0.7))
    finish(bm, "duomo_roof", col, "pisa_tile", smooth=False)
    prof = [(0.0, 30.0), (9.0, 30.0), (9.0, 38.0), (9.6, 38.0), (9.6, 39.0)]
    for k in range(1, 15):
        a = k / 14 * math.pi / 2
        prof.append((8.8 * math.cos(a), 39.0 + 9.5 * math.sin(a)))
    ob = revolve(col, "duomo_dome", prof, 0.0, 2.0 * math.pi, "pisa_stripe")
    ob.location = (8, 0, 0)
    # the west front: storeys of little columns
    bm = bmesh.new()
    for lv, (z0, z1, ymax, n) in enumerate(((1.0, 13.0, 16.5, 7), (15.0, 20.0, 16.5, 19), (21.5, 26.5, 8.5, 11), (28.0, 33.0, 8.5, 11))):
        for j in range(n):
            y = -ymax + 2 * ymax * j / (n - 1)
            column(bm, -45.55, y, z0, z1, 0.46 if lv == 0 else 0.26)
    finish(bm, "duomo_columns", col, "pisa_marble")
    bm = bmesh.new()
    for z in (14.3, 20.9, 27.4, 33.4):
        box(bm, (-45.6, 0, z), (1.3, 36.6 if z < 21 else 19.0, 0.7))
    finish(bm, "duomo_ledges", col, "pisa_marble", smooth=False)


def cypress():
    col = bpy.data.collections.new("pisa_cypress")
    bpy.context.scene.collection.children.link(col)
    prof = [(0.0, 0.0), (0.3, 0.0), (0.3, 0.9), (1.05, 1.5)]
    for k in range(1, 18):
        u = k / 18
        prof.append((1.05 + 0.5 * math.sin(u * math.pi * 0.72) ** 2 - 1.55 * u ** 3.2 + 0.06 * math.sin(k * 2.7), 1.5 + 13.0 * u))
    prof.append((0.0, 15.0))
    revolve(col, "cypress_body", prof, 0.0, 2.0 * math.pi, "pisa_cypress")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    half("pisa_a", 0.0, math.pi)
    half("pisa_b", math.pi, 2.0 * math.pi)
    duomo()
    cypress()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blender", "src", "pisa_assets.blend"))
    print("SAVED pisa_assets.blend")


main()
