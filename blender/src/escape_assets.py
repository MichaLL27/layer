"""Models for the Great Escape video (Short 07), built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/escape_assets.py
  blender.exe -b blender/src/escape_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up. The camera side ("front") is -Y; a model's body runs to +Y (into the picture).
Material names are resolved by EscapeScene.Mat. Everything is plain boards and boxes: a wartime barrack.
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


class Kit:
    """One bmesh per material inside a collection."""

    def __init__(self, name):
        self.name = name
        self.col = collection(name)
        self.bms = {}

    def bm(self, key):
        if key not in self.bms:
            self.bms[key] = bmesh.new()
        return self.bms[key]

    def box(self, key, x0, x1, y0, y1, z0, z1, rot=None, pivot=None):
        bm = self.bm(key)
        r = bmesh.ops.create_cube(bm, size=1.0)
        vs = r["verts"]
        sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
        for v in vs:
            v.co = V((v.co.x * sx + (x0 + x1) / 2, v.co.y * sy + (y0 + y1) / 2, v.co.z * sz + (z0 + z1) / 2))
        if rot is not None:
            bmesh.ops.rotate(bm, verts=vs, cent=V(pivot), matrix=rot)
        return vs

    def beam(self, key, a, b, w, h=None):
        """a square beam from point a to point b"""
        h = h or w
        a, b = V(a), V(b)
        d = b - a
        L = d.length
        bm = self.bm(key)
        r = bmesh.ops.create_cube(bm, size=1.0)
        q = d.to_track_quat("Z", "Y")
        for v in r["verts"]:
            p = V((v.co.x * w, v.co.y * h, (v.co.z + 0.5) * L))
            v.co = a + q @ p
        return r["verts"]

    def cyl(self, key, c, r, h, seg=24, axis="Z"):
        bm = self.bm(key)
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=h)
        rot = {"Z": mathutils.Matrix.Identity(3), "X": mathutils.Matrix.Rotation(math.pi / 2, 3, "Y"),
               "Y": mathutils.Matrix.Rotation(math.pi / 2, 3, "X")}[axis]
        for v in res["verts"]:
            v.co = rot @ v.co + V(c)

    def tube(self, key, pts, r, sides=6):
        bm = self.bm(key)
        rings = []
        for i, p in enumerate(pts):
            p = V(p)
            t = (V(pts[min(i + 1, len(pts) - 1)]) - V(pts[max(i - 1, 0)])).normalized()
            n = t.orthogonal().normalized()
            b = t.cross(n)
            rings.append([bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * r)
                          for a in [2 * math.pi * k / sides for k in range(sides)]])
        for i in range(len(rings) - 1):
            for k in range(sides):
                a, b2 = rings[i][k], rings[i][(k + 1) % sides]
                c, d = rings[i + 1][(k + 1) % sides], rings[i + 1][k]
                bm.faces.new((a, b2, c, d))

    def done(self, smooth=()):
        for key, bm in self.bms.items():
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            me = bpy.data.meshes.new(f"{self.name}_{key}")
            bm.to_mesh(me)
            bm.free()
            me.materials.append(mat(key))
            for p in me.polygons:
                p.use_smooth = key in smooth
            ob = bpy.data.objects.new(f"{self.name}_{key}", me)
            self.col.objects.link(ob)


# ---------------------------------------------------------------- the barrack (front open: a cutaway)
HUT_X0, HUT_X1, HUT_D = -6.0, 6.0, 5.0
FLOOR = 0.62           # top of the floor; the hut stands on short stilts
WALL = 2.55            # wall height above the floor
BASE = (1.5, 2.5, 0.0, 1.0)   # the stove's brick base (x0, x1, y0, y1): the floor has a hole for it


def barrack():
    k = Kit("esc_barrack")
    rng = random.Random(4)
    # stilts
    for x in [HUT_X0 + 0.15 + i * 1.95 for i in range(7)]:
        for y in (0.25, 2.5, HUT_D - 0.25):
            k.box("hut_stilt", x - 0.11, x + 0.11, y - 0.11, y + 0.11, -0.3, FLOOR - 0.1)
    # joists under the floor
    for y in (0.25, 2.5, HUT_D - 0.25):
        k.box("hut_stilt", HUT_X0, HUT_X1, y - 0.09, y + 0.09, FLOOR - 0.28, FLOOR - 0.08)
    # floor boards, around the stove base
    x0b, x1b, y0b, y1b = BASE
    w = 0.16
    x = HUT_X0
    while x < HUT_X1 - 1e-3:
        x2 = min(x + w, HUT_X1)
        z1 = FLOOR - rng.random() * 0.006
        if x2 <= x0b or x >= x1b:
            k.box("hut_floor", x + 0.004, x2 - 0.004, 0.0, HUT_D, FLOOR - 0.08, z1)
        else:
            k.box("hut_floor", x + 0.004, x2 - 0.004, y1b, HUT_D, FLOOR - 0.08, z1)
        x = x2
    # walls: outer tarred boards, inner planks; back wall with two windows
    T = 0.12
    zA, zB = FLOOR, FLOOR + WALL
    wins = [(-3.6, -2.4), (2.9, 4.1)]
    wz0, wz1 = FLOOR + 1.0, FLOOR + 2.0
    xs = [HUT_X0] + [v for a in wins for v in a] + [HUT_X1]
    for i in range(0, len(xs) - 1):
        a, b = xs[i], xs[i + 1]
        is_win = i % 2 == 1
        if not is_win:
            k.box("hut_wall", a, b, HUT_D - T, HUT_D, zA, zB)
        else:
            k.box("hut_wall", a, b, HUT_D - T, HUT_D, zA, wz0)
            k.box("hut_wall", a, b, HUT_D - T, HUT_D, wz1, zB)
            # frame + cross bars + glass
            k.box("hut_trim", a - 0.06, b + 0.06, HUT_D - T - 0.03, HUT_D - T + 0.01, wz0 - 0.06, wz0)
            k.box("hut_trim", a - 0.06, b + 0.06, HUT_D - T - 0.03, HUT_D - T + 0.01, wz1, wz1 + 0.06)
            k.box("hut_trim", a - 0.06, a, HUT_D - T - 0.03, HUT_D - T + 0.01, wz0, wz1)
            k.box("hut_trim", b, b + 0.06, HUT_D - T - 0.03, HUT_D - T + 0.01, wz0, wz1)
            m = (a + b) / 2
            k.box("hut_trim", m - 0.025, m + 0.025, HUT_D - T / 2 - 0.02, HUT_D - T / 2 + 0.02, wz0, wz1)
            k.box("hut_trim", a, b, HUT_D - T / 2 - 0.02, HUT_D - T / 2 + 0.02, (wz0 + wz1) / 2 - 0.025, (wz0 + wz1) / 2 + 0.025)
            k.box("hut_glass", a, b, HUT_D - T / 2 - 0.004, HUT_D - T / 2 + 0.004, wz0, wz1)
    # vertical battens on the inside, so the wall reads as boards
    x = HUT_X0 + 0.3
    while x < HUT_X1:
        if not any(a - 0.05 < x < b + 0.05 for a, b in wins):
            k.box("hut_batten", x - 0.022, x + 0.022, HUT_D - T - 0.025, HUT_D - T, zA, zB)
        x += 0.24
    # side walls (gable ends)
    for xw in (HUT_X0, HUT_X1 - T):
        k.box("hut_wall", xw, xw + T, 0.0, HUT_D, zA, zB)
    # wall plate along the open front, so the cut edge has a beam
    k.box("hut_trim", HUT_X0, HUT_X1, 0.0, 0.14, zB - 0.16, zB)
    for x in (HUT_X0 + 0.07, HUT_X1 - 0.07, -2.0, 0.6, 4.6):
        k.box("hut_trim", x - 0.07, x + 0.07, 0.0, 0.14, zA, zB)
    # roof: gable along X, ridge at the middle of the depth; cut open at the front (y = 0)
    ridge_z, eave_z, over = zB + 1.25, zB, 0.45
    yr = HUT_D / 2
    sl = (ridge_z - eave_z) / yr
    def roof_z(y):
        return ridge_z - abs(y - yr) * sl
    # rafters every 0.6 m
    x = HUT_X0 + 0.1
    while x < HUT_X1:
        for (ya, yb) in ((0.0, yr), (yr, HUT_D + over)):
            k.beam("hut_stilt", (x, ya, roof_z(ya) - 0.1), (x, yb, roof_z(yb) - 0.1), 0.07, 0.14)
        x += 0.6
    # tie beams across the room
    for x in (-4.0, 0.0, 4.0):
        k.box("hut_stilt", x - 0.06, x + 0.06, 0.0, HUT_D, zB - 0.1, zB + 0.04)
    # roof slabs + tar paper laps (two slopes; the front slope starts at the cut)
    for (ya, yb) in ((0.0, yr), (yr, HUT_D + over)):
        za, zb = roof_z(ya), roof_z(yb)
        L = math.hypot(yb - ya, zb - za)
        ang = math.atan2(zb - za, yb - ya)
        rot = mathutils.Matrix.Rotation(ang, 3, "X")
        k.box("hut_roof", HUT_X0 - over, HUT_X1 + over, ya, ya + L, za, za + 0.09, rot=rot, pivot=(0, ya, za))
        # tar paper laps: thin raised strips running along the length
        n = int(L / 0.85)
        for i in range(1, n + 1):
            y = ya + i * 0.85
            k.box("hut_lap", HUT_X0 - over, HUT_X1 + over, y - 0.02, y, za + 0.09, za + 0.11, rot=rot, pivot=(0, ya, za))
    # gables as stacked boards
    for xw in (HUT_X0, HUT_X1 - T):
        z = zB
        while z < ridge_z - 0.02:
            z2 = min(z + 0.14, ridge_z)
            half = (ridge_z - (z + z2) / 2) / sl
            k.box("hut_wall", xw, xw + T, max(0.0, yr - half), min(HUT_D, yr + half), z, z2)
            z = z2
    # steps up to the door at the right end
    k.box("hut_stilt", HUT_X1, HUT_X1 + 0.9, 3.4, 4.6, -0.3, 0.2)
    k.box("hut_stilt", HUT_X1, HUT_X1 + 0.5, 3.4, 4.6, 0.2, 0.42)
    k.done()


def stove_base():
    """hollow brick plinth from the ground to the floor; the front is open (we see the shaft inside)"""
    k = Kit("esc_stove_base")
    x0, x1, y0, y1 = -0.5, 0.5, 0.0, 1.0
    t = 0.13
    top = FLOOR
    # courses of bricks: offset every row, so the section reads as brickwork
    h = 0.075
    z = -0.4
    row = 0
    while z < top - 1e-3:
        z2 = min(z + h, top)
        for (a, b, c, d) in ((x0, x0 + t, y0, y1), (x1 - t, x1, y0, y1), (x0 + t, x1 - t, y1 - t, y1)):
            k.box("brick", a, b, c, d, z + 0.006, z2 - 0.006)
        z = z2
        row += 1
    # mortar shell slightly inside the bricks so the joints show
    k.box("mortar", x0 + 0.01, x0 + t - 0.01, y0 + 0.02, y1 - 0.01, -0.4, top)
    k.box("mortar", x1 - t + 0.01, x1 - 0.01, y0 + 0.02, y1 - 0.01, -0.4, top)
    k.box("mortar", x0 + t, x1 - t, y1 - t + 0.01, y1 - 0.01, -0.4, top)
    # tiled rim around the trap door
    for (a, b, c, d) in ((x0, x1, y1 - 0.12, y1), (x0, x0 + 0.12, y0, y1), (x1 - 0.12, x1, y0, y1)):
        k.box("tile", a, b, c, d, top, top + 0.03)
    k.done()


def trapdoor():
    k = Kit("esc_trapdoor")
    n = 3
    s = 0.76 / n
    for i in range(n):
        for j in range(n):
            x0, y0 = -0.38 + i * s, 0.0 + j * s
            k.box("tile", x0 + 0.004, x0 + s - 0.004, y0 + 0.004, y0 + s - 0.004, 0.0, 0.03)
    k.box("mortar", -0.38, 0.38, 0.0, 0.76, -0.04, 0.0)
    k.done()


def stove():
    k = Kit("esc_stove")
    k.box("iron", -0.28, 0.28, 0.12, 0.62, 0.08, 0.72)
    k.box("iron", -0.31, 0.31, 0.09, 0.65, 0.72, 0.77)
    k.box("iron_dark", -0.18, 0.18, 0.115, 0.13, 0.22, 0.48)      # fire door
    for (x, y) in ((-0.24, 0.16), (0.24, 0.16), (-0.24, 0.58), (0.24, 0.58)):
        k.box("iron", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.0, 0.1)
    k.cyl("iron", (0.0, 0.45, 0.77 + 1.6), 0.07, 3.2, seg=20)
    k.done()


def bunk():
    """two-tier bunk, 2.0 long (x) and 0.9 deep (y); the slats are separate (Unity)"""
    k = Kit("esc_bunk")
    for x in (-1.0, 1.0):
        for y in (0.0, 0.9):
            k.box("bunk_wood", x - 0.045, x + 0.045, y - 0.045, y + 0.045, 0.0, 1.85)
    for z in (0.32, 1.27):
        for y in (0.0, 0.9):
            k.box("bunk_wood", -1.0, 1.0, y - 0.04, y + 0.04, z - 0.12, z)
        for x in (-1.0, 1.0):
            k.box("bunk_wood", x - 0.04, x + 0.04, 0.0, 0.9, z - 0.12, z)
    # a ladder rung pair at the end
    for z in (0.62, 0.95):
        k.box("bunk_wood", 1.0, 1.04, 0.1, 0.8, z - 0.02, z + 0.02)
    k.done()


def table():
    k = Kit("esc_table")
    k.box("bunk_wood", -0.7, 0.7, -0.4, 0.4, 0.74, 0.79)
    for (x, y) in ((-0.62, -0.32), (0.62, -0.32), (-0.62, 0.32), (0.62, 0.32)):
        k.box("bunk_wood", x - 0.04, x + 0.04, y - 0.04, y + 0.04, 0.0, 0.74)
    for x in (-1.1, 1.1):
        k.box("bunk_wood", x - 0.2, x + 0.2, -0.2, 0.2, 0.44, 0.48)
        for (dx, dy) in ((-0.16, -0.16), (0.16, -0.16), (-0.16, 0.16), (0.16, 0.16)):
            k.box("bunk_wood", x + dx - 0.025, x + dx + 0.025, dy - 0.025, dy + 0.025, 0.0, 0.44)
    k.done()


def tower():
    k = Kit("esc_tower")
    H = 5.2
    b0, b1 = 1.35, 1.05      # half widths at the foot and the top
    legs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            a, b = (sx * b0, sy * b0, 0.0), (sx * b1, sy * b1, H)
            k.beam("tower_wood", a, b, 0.2)
            legs.append((a, b))
    def at(sx, sy, z):
        u = z / H
        hw = b0 + (b1 - b0) * u
        return (sx * hw, sy * hw, z)
    # braces (X on every side) and rings
    for z0, z1 in ((0.3, 2.6), (2.6, 4.9)):
        for (sa, sb) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            k.beam("tower_wood", at(*sa, z0), at(*sb, z1), 0.09)
            k.beam("tower_wood", at(*sb, z0), at(*sa, z1), 0.09)
            k.beam("tower_wood", at(*sa, z1), at(*sb, z1), 0.1)
    # platform + cabin
    P = 1.6
    k.box("tower_floor", -P, P, -P, P, H, H + 0.16)
    k.box("tower_wood", -P, P, -P, -P + 0.08, H + 0.16, H + 1.15)
    k.box("tower_wood", -P, P, P - 0.08, P, H + 0.16, H + 1.15)
    k.box("tower_wood", -P, -P + 0.08, -P, P, H + 0.16, H + 1.15)
    k.box("tower_wood", P - 0.08, P, -P, P, H + 0.16, H + 1.15)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box("tower_wood", sx * (P - 0.05) - 0.06, sx * (P - 0.05) + 0.06, sy * (P - 0.05) - 0.06, sy * (P - 0.05) + 0.06, H + 1.15, H + 2.25)
    # pyramid roof
    bm = k.bm("tower_roof")
    R, z0, z1 = P + 0.45, H + 2.25, H + 3.05
    c = [bm.verts.new((sx * R, sy * R, z0)) for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    c2 = [bm.verts.new((sx * R, sy * R, z0 - 0.08)) for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    top = bm.verts.new((0, 0, z1))
    for i in range(4):
        bm.faces.new((c[i], c[(i + 1) % 4], top))
        bm.faces.new((c2[i], c2[(i + 1) % 4], c[(i + 1) % 4], c[i]))
    bm.faces.new(tuple(reversed(c2)))
    # searchlight on the rail, facing -Y (the camp)
    k.cyl("lamp_metal", (0.9, -P - 0.05, H + 1.45), 0.24, 0.42, seg=24, axis="Y")
    k.cyl("lamp_glass", (0.9, -P - 0.27, H + 1.45), 0.2, 0.02, seg=24, axis="Y")
    k.box("lamp_metal", 0.86, 0.94, -P - 0.05, -P + 0.05, H + 1.15, H + 1.28)
    # ladder
    for x in (-0.25, 0.25):
        k.beam("tower_wood", (x, -b0 - 0.6, 0.0), (x, -P, H), 0.07)
    for i in range(1, 17):
        z = i * H / 17
        y = -b0 - 0.6 + (-P + b0 + 0.6) * (z / H)
        k.box("tower_wood", -0.25, 0.25, y - 0.03, y + 0.03, z - 0.025, z + 0.025)
    k.done()


def post():
    """fence post with the arm leaning in (toward -X, the camp); wires are strung in Unity"""
    k = Kit("esc_post")
    k.box("post_wood", -0.075, 0.075, -0.075, 0.075, -0.3, 2.9)
    ang = math.radians(45)
    k.beam("post_wood", (0.0, 0.0, 2.75), (-0.55 * math.sin(ang), 0.0, 2.75 + 0.55 * math.cos(ang)), 0.07)
    k.done()


def coil():
    """concertina wire along Y, 10 m"""
    k = Kit("esc_coil")
    pts = []
    R, pitch = 0.42, 0.22
    n = int(10.0 / pitch * 24)
    for i in range(n + 1):
        a = 2 * math.pi * i / 24
        y = -5.0 + 10.0 * i / n
        rr = R * (1 + 0.04 * math.sin(i * 0.37))
        pts.append((rr * math.cos(a), y, R + rr * math.sin(a)))
    k.tube("wire", pts, 0.009, sides=5)
    k.done()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    barrack()
    stove_base()
    trapdoor()
    stove()
    bunk()
    table()
    tower()
    post()
    coil()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blender", "src", "escape_assets.blend"))
    print("SAVED escape_assets.blend")


main()
