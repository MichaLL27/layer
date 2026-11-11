"""Tunnel-video models, built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/tunnel_assets.py
  blender.exe -b blender/src/tunnel_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up, the machine's axis along X (it drives toward +X), camera on the -Y side.
Half shells keep their material on the BACK half (y >= 0): the section is open to the camera.
Pivots on the tunnel axis (cutter, erector, ring_seg, screw) so Unity only has to rotate them.
Material names are keys of WellLook.ModelMaterial.
"""
import math
import os

import bmesh
import bpy
import mathutils

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MATS = {}


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


def finish(bm, name, col, matkey, loc=(0, 0, 0), rot=(0, 0, 0), smooth=False):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat(matkey))
    for p in me.polygons:
        # caps and ring ends stay flat, or short cylinders shade like balls
        p.use_smooth = smooth and len(p.vertices) <= 4 and abs(p.normal.x) < 0.9
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    ob.rotation_euler = rot
    col.objects.link(ob)
    return ob


def box(col, name, size, loc, matkey, rot=(0, 0, 0), bevel=0.02):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
    b = min(bevel, min(size) * 0.45)
    if b > 0.0005:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, segments=2, profile=0.5, affect='EDGES')
    return finish(bm, name, col, matkey, loc, rot)


def cyl_x(col, name, r, x0, x1, loc_yz, matkey, r2=None, segs=48):
    """Solid cylinder along X from x0 to x1 at (y, z)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                          radius2=r if r2 is None else r2, depth=abs(x1 - x0))
    return finish(bm, name, col, matkey, ((x0 + x1) / 2, loc_yz[0], loc_yz[1]), (0, math.pi / 2, 0), smooth=True)


def beam(col, name, p0, p1, w, matkey):
    v0, v1 = mathutils.Vector(p0), mathutils.Vector(p1)
    d = v1 - v0
    ob = box(col, name, (w, w, d.length), (0, 0, 0), matkey, bevel=0.006)
    ob.location = (v0 + v1) / 2
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(d.normalized())
    return ob


def arc_shell(col, name, r_out, r_in, x0, x1, a0, a1, matkey, segs=48, smooth=True):
    """Ring sector along X. Angle 0 = up (+Z), growing toward the back (+Y). Closed solid."""
    bm = bmesh.new()
    ang = [a0 + (a1 - a0) * i / segs for i in range(segs + 1)]

    def ring(r, x):
        return [bm.verts.new((x, r * math.sin(a), r * math.cos(a))) for a in ang]

    oa, ob_, ia, ib = ring(r_out, x0), ring(r_out, x1), ring(r_in, x0), ring(r_in, x1)
    for i in range(segs):
        bm.faces.new((oa[i], oa[i + 1], ob_[i + 1], ob_[i]))
        bm.faces.new((ia[i], ib[i], ib[i + 1], ia[i + 1]))
        bm.faces.new((oa[i], ia[i], ia[i + 1], oa[i + 1]))
        bm.faces.new((ob_[i], ob_[i + 1], ib[i + 1], ib[i]))
    bm.faces.new((oa[0], ob_[0], ib[0], ia[0]))
    bm.faces.new((oa[segs], ia[segs], ib[segs], ob_[segs]))
    return finish(bm, name, col, matkey, smooth=smooth)


def half_x(col, name, r_out, r_in, x0, x1, matkey, segs=64):
    return arc_shell(col, name, r_out, r_in, x0, x1, 0.0, math.pi, matkey, segs)


def full_ring_x(col, name, r_out, r_in, x0, x1, matkey, segs=96):
    return arc_shell(col, name, r_out, r_in, x0, x1, 0.0, 2 * math.pi - 1e-4, matkey, segs)


# ======================================================================== the machine
R = 2.1          # shield radius; the tunnel cut in Unity matches it


def cutter():
    """Spoked cutting wheel, axis on the origin, turns about X. Its face points +X."""
    c = collection("cutter")
    full_ring_x(c, "rim", R - 0.02, R - 0.26, -0.28, 0.14, "tbm_yellow")
    full_ring_x(c, "rim_lip", R - 0.02, R - 0.1, 0.14, 0.26, "tbm_yellow_dark")
    cyl_x(c, "hub", 0.55, -0.42, 0.22, (0, 0), "tbm_yellow")
    cyl_x(c, "nose", 0.55, 0.22, 0.62, (0, 0), "tbm_yellow_dark", r2=0.14)
    n = 6
    for k in range(n):
        a = k * 2 * math.pi / n
        ca, sa = math.cos(a), math.sin(a)
        rm = (0.5 + R - 0.15) / 2
        L = R - 0.65
        # spoke: a deep box arm from the hub to the rim
        box(c, f"spoke_{k}", (0.44, 0.34, L), (-0.02, rm * sa, rm * ca), "tbm_yellow", rot=(-a, 0, 0), bevel=0.03)
        box(c, f"spoke_edge_{k}", (0.12, 0.2, L), (0.26, rm * sa, rm * ca), "tbm_yellow_dark", rot=(-a, 0, 0), bevel=0.02)
        # disc cutters along the spoke (steel rollers proud of the face)
        for j, r0 in enumerate((0.72, 1.08, 1.44, 1.78)):
            off = 0.13 if j % 2 == 0 else -0.13
            y, z = r0 * sa + off * ca, r0 * ca - off * sa
            cyl_x(c, f"disc_seat_{k}_{j}", 0.1, 0.2, 0.32, (y, z), "steel_dark", segs=20)
            cyl_x(c, f"disc_{k}_{j}", 0.17, 0.3, 0.38, (y, z), "disc_steel", segs=28)
        # scraper teeth on the rim
        for j in range(3):
            aa = a + (j + 0.5) * 2 * math.pi / n / 3
            box(c, f"tooth_{k}_{j}", (0.16, 0.14, 0.2), (0.3, (R - 0.12) * math.sin(aa), (R - 0.12) * math.cos(aa)), "steel_dark", rot=(-aa, 0, 0), bevel=0.02)


def shield():
    """Half shells and everything fixed inside. x = 0 is the bulkhead; the wheel sits ahead of it."""
    c = collection("tbm_shield")
    half_x(c, "front_shield", R, R - 0.1, -1.6, 1.0, "tbm_paint")
    half_x(c, "mid_shield", R - 0.02, R - 0.12, -4.0, -1.6, "tbm_paint")
    half_x(c, "tail_shield", R - 0.04, R - 0.12, -7.2, -4.0, "tbm_paint")
    for k, x in enumerate((-1.6, -4.0)):
        half_x(c, f"joint_{k}", R + 0.005, R - 0.15, x - 0.09, x + 0.09, "steel_dark")
    for k, x in enumerate((-0.9, -2.7, -3.4, -5.2, -6.2)):      # stiffener rings inside
        half_x(c, f"rib_{k}", R - 0.1, R - 0.27, x - 0.07, x + 0.07, "tbm_yellow")
    half_x(c, "bulkhead", R - 0.08, 0.0, -0.1, 0.06, "tbm_paint")
    half_x(c, "bulk_ring", R - 0.1, R - 0.42, -0.26, -0.1, "steel_dark")
    cyl_x(c, "main_bearing", 1.15, -1.2, -0.1, (0.02, 0), "drum")
    cyl_x(c, "main_drive", 0.92, -1.75, -1.2, (0.02, 0), "tbm_yellow")
    for k in range(6):                                           # drive motors around the bearing
        a = k * math.pi / 3 + 0.3
        cyl_x(c, f"motor_{k}", 0.17, -2.3, -1.25, (0.8 * math.sin(a) + 0.02, 0.8 * math.cos(a)), "steel", segs=24)
    # walkway, handrail, cabinets (scale + life)
    box(c, "deck", (4.2, 1.5, 0.08), (-3.6, 0.7, -1.2), "steel_dark", bevel=0.01)
    for k in range(7):
        x = -5.5 + k * 0.65
        beam(c, f"rail_post_{k}", (x, 0.02, -1.16), (x, 0.02, -0.5), 0.035, "tbm_yellow")
    beam(c, "rail", (-5.5, 0.02, -0.5), (-1.6, 0.02, -0.5), 0.04, "tbm_yellow")
    box(c, "cabinet_a", (0.8, 0.5, 0.95), (-3.0, 1.15, -0.68), "tbm_cabinet", bevel=0.03)
    box(c, "cabinet_b", (0.55, 0.5, 0.75), (-3.9, 1.2, -0.78), "tbm_cabinet", bevel=0.03)
    # hoses along the crown
    for k in range(4):
        y = 0.8 + k * 0.12
        z = math.sqrt(max(0.0, (R - 0.35) ** 2 - y * y))
        beam(c, f"hose_{k}", (-6.8, y, z), (-0.4, y, z), 0.05, "rubber")


def screw():
    """Screw conveyor: unit length 6 along -X from its intake at the origin, turns about its own X axis."""
    c = collection("screw_flight")
    L, r, rs, pitch = 6.0, 0.3, 0.075, 0.42
    n = int(L / pitch * 28)
    bm = bmesh.new()
    prev = None
    for i in range(n + 1):
        x = -L * i / n
        a = 2 * math.pi * (x / pitch)
        q = (bm.verts.new((x, rs * math.sin(a), rs * math.cos(a))), bm.verts.new((x, r * math.sin(a), r * math.cos(a))),
             bm.verts.new((x - 0.02, r * math.sin(a), r * math.cos(a))), bm.verts.new((x - 0.02, rs * math.sin(a), rs * math.cos(a))))
        if prev:
            for k in range(4):
                bm.faces.new((prev[k], q[k], q[(k + 1) % 4], prev[(k + 1) % 4]))
        prev = q
    finish(bm, "flight", c, "steel", smooth=True)
    cyl_x(c, "shaft", rs, -L, 0, (0, 0), "steel_dark", segs=16)
    c = collection("screw_tube")
    half_x(c, "tube", 0.36, 0.33, -6.0, 0.0, "tbm_yellow", segs=32)
    for k in range(9):
        x = -0.3 - k * 0.68
        half_x(c, f"band_{k}", 0.375, 0.33, x - 0.03, x + 0.03, "steel_dark", segs=32)


def erector():
    """Robot arm: pivots on the axis, about X. Rest position: arm straight up (+Z). Gripper at r = 1.5."""
    c = collection("erector")
    full_ring_x(c, "slew_ring", 0.72, 0.5, -0.16, 0.16, "steel_dark", segs=64)
    full_ring_x(c, "slew_gear", 0.82, 0.7, -0.06, 0.06, "steel", segs=64)
    box(c, "arm_a", (0.2, 0.26, 1.15), (0.14, 0.0, 0.95), "tbm_yellow", bevel=0.03)
    box(c, "arm_b", (0.2, 0.26, 1.15), (-0.14, 0.0, 0.95), "tbm_yellow", bevel=0.03)
    cyl_x(c, "ram_a", 0.05, -0.05, 0.05, (0.2, 1.0), "steel", segs=16)
    box(c, "gripper_plate", (0.95, 0.75, 0.1), (0, 0, 1.55), "steel_dark", bevel=0.02)
    box(c, "gripper_pin", (0.16, 0.16, 0.14), (0, 0, 1.66), "steel", bevel=0.02)
    for sx in (-0.34, 0.34):
        box(c, f"gripper_foot_{sx}", (0.2, 0.55, 0.08), (sx, 0, 1.63), "rubber", bevel=0.015)
    box(c, "counterweight", (0.5, 0.36, 0.3), (0, 0, -0.9), "steel_dark", bevel=0.04)


def segments():
    """One precast segment: a 45 degree sector of a 1.4 m wide ring, rubber gasket on its edges. Angle 0 = up."""
    c = collection("ring_seg")
    Ro, Ri, W = 1.96, 1.66, 1.4
    a = math.radians(45)
    gap = math.radians(0.5)
    arc_shell(c, "seg_body", Ro, Ri, -W / 2 + 0.008, W / 2 - 0.008, gap, a - gap, "concrete_seg", segs=20)
    # gasket grooves: dark strips on all four outer edges
    arc_shell(c, "gasket_a", Ro - 0.07, Ro - 0.11, W / 2 - 0.008, W / 2, gap, a - gap, "gasket", segs=20)
    arc_shell(c, "gasket_b", Ro - 0.07, Ro - 0.11, -W / 2, -W / 2 + 0.008, gap, a - gap, "gasket", segs=20)
    # bolt pockets on the inner face
    for s in (-0.42, 0.42):
        for t in (0.25, 0.75):
            ang = a * t
            box(c, f"pocket_{s}_{t}", (0.14, 0.07, 0.03), (s, (Ri - 0.004) * math.sin(ang), (Ri - 0.004) * math.cos(ang)), "gasket", rot=(-ang, 0, 0), bevel=0.005)


def grout():
    """Grout shell filling the gap outside the rings: unit length along X from the origin toward -X."""
    c = collection("grout_shell")
    half_x(c, "grout", 2.09, 1.96, -1.0, 0.0, "grout")


def gantry():
    """Back-up car trailing the machine, 5 m long, runs on rails, carries the belt."""
    c = collection("gantry")
    L, h = 5.0, 2.5
    for sx in (-L / 2 + 0.3, L / 2 - 0.3):
        for sy in (-0.45, 1.0):
            box(c, f"leg_{sx}_{sy}", (0.14, 0.14, h), (sx, sy, -1.72 + h / 2 - 0.05), "tbm_paint", bevel=0.02)
            cyl_x(c, f"wheel_{sx}_{sy}", 0.14, sx - 0.06, sx + 0.06, (sy, -1.72), "steel_dark", segs=20).rotation_euler = (math.pi / 2, 0, 0)
    for sy in (-0.45, 1.0):
        box(c, f"beam_top_{sy}", (L, 0.14, 0.16), (0, sy, 0.68), "tbm_paint", bevel=0.02)
        box(c, f"beam_mid_{sy}", (L, 0.1, 0.1), (0, sy, -0.55), "tbm_paint", bevel=0.015)
    for k in range(5):
        x = -L / 2 + 0.5 + k * (L - 1) / 4
        box(c, f"cross_{k}", (0.1, 1.55, 0.1), (x, 0.28, 0.68), "tbm_paint", bevel=0.015)
    box(c, "tank_saddle", (1.5, 0.8, 0.1), (-0.9, 0.42, -0.5), "steel_dark", bevel=0.01)
    cyl_x(c, "tank", 0.34, -1.6, -0.2, (0.42, -0.12), "tbm_cabinet", segs=28)
    box(c, "pump_unit", (1.0, 0.7, 0.65), (1.1, 0.42, -0.2), "tbm_cabinet", bevel=0.03)
    for k in range(4):
        box(c, f"vent_{k}", (0.8, 0.02, 0.03), (1.1, 0.06, -0.38 + k * 0.1), "steel_dark", bevel=0.003)
    # belt conveyor up high (the moving dirt comes from Unity)
    box(c, "belt_frame", (L, 0.5, 0.06), (0, 0.15, 0.42), "steel_dark", bevel=0.01)
    box(c, "belt", (L, 0.42, 0.02), (0, 0.15, 0.46), "rubber", bevel=0.0)


def rails():
    c = collection("rail_piece")
    for sy in (-0.45, 1.0):
        box(c, f"rail_{sy}", (5.0, 0.07, 0.08), (0, sy, 0), "steel", bevel=0.008)
    for k in range(9):
        box(c, f"sleeper_{k}", (0.16, 1.9, 0.08), (-2.2 + k * 0.55, 0.28, -0.07), "tbm_cabinet", bevel=0.01)


def barge():
    """Container barge: 14 m long, waterline at z = 0, bow toward +X."""
    c = collection("barge")
    bm = bmesh.new()
    L, W, H, B = 7.0, 1.7, 0.9, 0.5
    prof = [(-L, -W), (L - 1.2, -W), (L, -W * 0.5), (L, W * 0.5), (L - 1.2, W), (-L, W)]
    a = [bm.verts.new((x, y, -B)) for x, y in prof]
    t = [bm.verts.new((x, y, H)) for x, y in prof]
    bm.faces.new(a[::-1]); bm.faces.new(t)
    for i in range(len(prof)):
        j = (i + 1) % len(prof)
        bm.faces.new((a[i], a[j], t[j], t[i]))
    finish(bm, "hull", c, "barge_hull")
    box(c, "deck_line", (2 * L - 0.6, 2 * W - 0.1, 0.06), (-0.3, 0, H + 0.01), "barge_deck", bevel=0.0)
    box(c, "cabin", (1.5, 1.8, 1.0), (-L + 1.1, 0, H + 0.5), "wall_white", bevel=0.04)
    box(c, "bridge", (1.1, 1.4, 0.7), (-L + 1.1, 0, H + 1.35), "wall_white", bevel=0.04)
    box(c, "bridge_glass", (0.05, 1.2, 0.3), (-L + 1.67, 0, H + 1.42), "house_glass", bevel=0.0)
    box(c, "funnel", (0.3, 0.3, 0.6), (-L + 0.6, 0, H + 1.9), "container_r", bevel=0.03)
    cols = ["container_b", "container_r", "container_y", "container_b", "container_g", "container_r"]
    for i in range(5):
        for j, y in enumerate((-0.62, 0.62)):
            for k in range(2 if (i + j) % 3 else 1):
                col = cols[(i * 2 + j + k * 3) % len(cols)]
                x = -3.9 + i * 2.05
                box(c, f"cont_{i}_{j}_{k}", (1.95, 1.15, 0.92), (x, y, H + 0.5 + k * 0.94), col, bevel=0.015)


def seal_arc():
    """Rubber gasket seen from inside: half a ring on the rings' inner face, width 0.12 along X."""
    c = collection("seal_arc")
    half_x(c, "seal", 1.67, 1.61, -0.06, 0.06, "gasket", segs=64)


def train():
    """One metro car, 6.4 m long, wheels on z = 0, side windows on -Y (the camera side)."""
    c = collection("train_car")
    L, W = 3.2, 0.75
    box(c, "body", (2 * L, 2 * W, 1.45), (0, 0, 0.945), "train_body", bevel=0.09)
    box(c, "roof", (2 * L - 0.2, 2 * W - 0.2, 0.14), (0, 0, 1.72), "train_roof", bevel=0.05)
    box(c, "skirt", (2 * L - 0.1, 2 * W - 0.04, 0.22), (0, 0, 0.32), "steel_dark", bevel=0.03)
    box(c, "stripe", (2 * L - 0.1, 0.02, 0.13), (0, -W - 0.004, 0.72), "train_stripe", bevel=0.0)
    for x in (-2.6, -0.85, 0.0, 0.85, 2.6):
        box(c, f"window_{x}", (0.72, 0.03, 0.55), (x, -W - 0.006, 1.27), "train_window", bevel=0.0)
    for x in (-1.72, 1.72):
        box(c, f"door_{x}", (0.7, 0.03, 1.22), (x, -W - 0.006, 1.03), "train_door", bevel=0.0)
        box(c, f"door_win_{x}", (0.5, 0.035, 0.5), (x, -W - 0.008, 1.27), "train_window", bevel=0.0)
    for x in (-2.15, 2.15):
        box(c, f"bogie_{x}", (1.5, 1.2, 0.16), (x, 0, 0.13), "steel_dark", bevel=0.02)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cutter(); shield(); screw(); erector(); segments(); grout(); gantry(); rails(); barge(); seal_arc(); train()
    out = os.path.join(ROOT, "blender", "src", "tunnel_assets.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print("SAVED", out, "objects:", len(bpy.data.objects))


if __name__ == "__main__":      # car_assets.py imports the helpers
    main()
