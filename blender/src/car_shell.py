"""Smooth sedan shell for car_assets.py: one plan outline (a superellipse), a wall swept around it,
hood and trunk lids that meet the wall's top edge, a crowned roof, wrap-around bumpers.
Same frame as car_assets: nose to -X, camera on -Y, floor at z = 0, wheels at z = 0.15.
The near side (y < 0) is cut away between the A and C pillars above the sill.
"""
import math

import bmesh
import bpy

from tunnel_assets import mat

A_EXP = 5.0                  # superellipse exponent: rounded-rectangle plan
WHEELS = (-1.55, 1.53)
ARCH_R = 0.41


def outline(L, W, th):
    c, s = math.cos(th), math.sin(th)
    return (L * math.copysign(abs(c) ** (2 / A_EXP), c), W * math.copysign(abs(s) ** (2 / A_EXP), s))


def theta_for_x(L, x):
    """Angle in (0, pi/2] whose outline point has |x| = |x|."""
    return math.acos(min(1.0, (abs(x) / L) ** (A_EXP / 2)))


def half_width(L, W, x):
    return W * max(0.0, 1 - abs(x / L) ** A_EXP) ** (1 / A_EXP)


def smoothstep(a, b, x):
    k = min(1.0, max(0.0, (x - a) / (b - a)))
    return k * k * (3 - 2 * k)


def belt(x):
    """Top edge of the body side (hood/trunk edge at the front and back)."""
    z = 0.47 + 0.19 * smoothstep(-2.25, -1.0, x)          # nose 0.47 -> cowl 0.66
    z += 0.05 * smoothstep(-1.0, 1.2, x)                  # wedge: beltline rises toward the back
    z -= 0.06 * smoothstep(1.9, 2.25, x)                  # tail rounds down
    return z


def finish_smooth(bm, name, col, matkey, thickness=0.0):
    if thickness > 0:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=thickness)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(matkey))
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def grid(col, name, pts, matkey, thickness=0.0, keep=None, closed_u=False):
    """pts[i][j] = (x, y, z); quads between neighbours; keep(i, j) -> False drops that quad."""
    bm = bmesh.new()
    vs = [[bm.verts.new(p) for p in row] for row in pts]
    nu, nv = len(pts), len(pts[0])
    for i in range(nu if closed_u else nu - 1):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            if keep is not None and not keep(i, j):
                continue
            bm.faces.new((vs[i][j], vs[i2][j], vs[i2][j + 1], vs[i][j + 1]))
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    return finish_smooth(bm, name, col, matkey, thickness)


def shell(c, L, W, paint="car_paint", trim="car_trim"):
    N, NV = 200, 14
    ths = [2 * math.pi * i / N for i in range(N)]

    # ---- the wall, around the whole plan, from the sill (or the wheel arch) up to the beltline
    wall, meta = [], []
    for th in ths:
        x, y = outline(L, W, th)
        th2 = th + 1e-3
        x2, y2 = outline(L, W, th2)
        tx, ty = x2 - x, y2 - y
        tl = math.hypot(tx, ty) or 1.0
        nx, ny = ty / tl, -tx / tl                    # outward for a counter-clockwise outline
        zb = 0.06
        if abs(y) > 0.55 * W:
            for xw in WHEELS:
                if abs(x - xw) < ARCH_R:
                    zb = max(zb, 0.15 + math.sqrt(ARCH_R ** 2 - (x - xw) ** 2))
        zt = belt(x)
        col = []
        for j in range(NV):
            v = j / (NV - 1)
            z = zb + (zt - zb) * v
            off = 0.045 * math.sin(math.pi * v) ** 0.8          # the side bulges, both edges tuck in
            col.append((x + nx * off, y + ny * off, z))
        wall.append(col)
        meta.append((x, y))

    def keep_wall(i, j):
        x, y = meta[i]
        z = wall[i][j][2]
        if y < -0.3 and -0.8 < x < 1.2 and z > 0.22:
            return False                                       # the cutaway: near doors gone, sill stays
        return True

    grid(c, "body_wall", wall, paint, thickness=0.025, keep=keep_wall, closed_u=True)

    # ---- hood and trunk: lids whose outer edge is the wall's top edge
    def lid(name, x_back, sign, crown):
        ta = theta_for_x(L, x_back)              # outline points with |x| >= |x_back|: |theta - centre| <= ta
        centre = math.pi if sign < 0 else 0.0
        yb = half_width(L, W, x_back)
        NU, NL = 40, 18
        rows = []
        for i in range(NU + 1):
            s_ = -1 + 2 * i / NU
            xf, yf = outline(L, W, centre + s_ * ta)
            ybk = math.copysign(abs(s_) * yb, yf) if abs(yf) > 1e-9 else 0.0
            row = []
            for j in range(NL + 1):
                v = j / NL
                x = x_back + (xf - x_back) * v
                y = ybk + (yf - ybk) * v
                yo = max(1e-4, half_width(L, W, x))
                q = min(1.0, abs(y) / yo)
                row.append((x, y, belt(x) + crown * (1 - q * q)))
            rows.append(row)
        return grid(c, name, rows, paint, thickness=0.02)

    lid("hood", -0.98, -1, 0.05)
    lid("trunk", 1.22, +1, 0.04)

    # ---- roof: crowned, edges rolled down onto the roof rails
    rows = []
    for i in range(31):
        x = -0.6 + 1.7 * i / 30
        row = []
        for j in range(25):
            y = -0.81 + 1.62 * j / 24
            q = abs(y) / 0.81
            z = 1.37 + 0.05 * (1 - q * q) - 0.03 * ((x - 0.25) / 0.85) ** 2 - 0.035 * q ** 8
            row.append((x, y, z))
        rows.append(row)
    grid(c, "roof", rows, paint, thickness=0.025)

    # ---- bumpers: bands around the front and back arcs, a little proud of the wall
    for nm, th0, th1 in (("bumper_f", math.radians(118), math.radians(242)), ("bumper_r", math.radians(-60), math.radians(60))):
        rows = []
        for i in range(61):
            th = th0 + (th1 - th0) * i / 60
            x, y = outline(L, W, th)
            x2, y2 = outline(L, W, th + 1e-3)
            tl = math.hypot(x2 - x, y2 - y) or 1.0
            nx, ny = (y2 - y) / tl, -(x2 - x) / tl
            col = []
            for j in range(7):
                v = j / 6
                off = 0.05 + 0.03 * math.sin(math.pi * v)
                col.append((x + nx * off, y + ny * off, 0.04 + 0.24 * v))
            rows.append(col)
        grid(c, nm, rows, trim, thickness=0.03)


def xo(L, W, y):
    """x of the outline's front (negative) for a given y."""
    return -L * max(0.0, 1 - abs(y / W) ** A_EXP) ** (1 / A_EXP)
