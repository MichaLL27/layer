"""Well-video models v2 (realistic pass), built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/well_assets.py
  blender.exe -b blender/src/well_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

Conventions: 1 unit = 1 m, Z up, the FRONT of every asset faces -Y (Unity's camera side; Unity turns
them 180 degrees on placement). Every box is bevelled so edges catch the light. No UVs are needed:
Unity maps textures triplanar in object space. Material names are keys Unity turns into HDRP
materials (WellLook.ModelMaterial).
Pivots: house/rig/hydrant/cap at the base centre; rig_head at its rod axis; pump/casing/screen hang
DOWN from z=0.
"""
import math
import os

import bmesh
import bpy

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
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    ob.rotation_euler = rot
    col.objects.link(ob)
    return ob


def box(col, name, size, loc, matkey, rot=(0, 0, 0), bevel=0.02):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    b = min(bevel, min(size) * 0.45)
    if b > 0.0005:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, segments=2, profile=0.5, affect='EDGES')
    return finish(bm, name, col, matkey, loc, rot)


def cyl(col, name, r, h, loc, matkey, r2=None, segs=32, rot=(0, 0, 0), smooth=True, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                          radius2=r if r2 is None else r2, depth=h)
    if bevel > 0:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and abs(e.link_faces[0].normal.z) > 0.9 or
               (len(e.link_faces) == 2 and abs(e.link_faces[1].normal.z) > 0.9)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, segments=2, profile=0.5, affect='EDGES')
    return finish(bm, name, col, matkey, loc, rot, smooth)


def ball(col, name, r, loc, matkey, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=r)
    for v in bm.verts:
        v.co.x *= scale[0]
        v.co.y *= scale[1]
        v.co.z *= scale[2]
    return finish(bm, name, col, matkey, loc, smooth=True)


def poly_prism(col, name, prof_xz, y0, y1, matkey):
    """Convex polygon in the XZ plane extruded along Y (front -Y)."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in prof_xz]
    b = [bm.verts.new((x, y1, z)) for x, z in prof_xz]
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    n = len(prof_xz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return finish(bm, name, col, matkey)


def beam(col, name, p0, p1, w, matkey):
    """Square beam from p0 to p1 (lattice members)."""
    import mathutils
    v0, v1 = mathutils.Vector(p0), mathutils.Vector(p1)
    d = v1 - v0
    L = d.length
    ob = box(col, name, (w, w, L), (0, 0, 0), matkey, bevel=0.004)
    ob.location = (v0 + v1) / 2
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(d.normalized())
    return ob


def half_shell(col, name, r_out, r_in, z0, z1, matkey, segs=40):
    """Half pipe along Z, material on the BACK half (y >= 0): the open side faces -Y."""
    bm = bmesh.new()
    ang = [math.pi * i / segs for i in range(segs + 1)]

    def ring(r, z):
        return [bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for a in ang]

    oa, ob_, ia, ib = ring(r_out, z0), ring(r_out, z1), ring(r_in, z0), ring(r_in, z1)
    for i in range(segs):
        bm.faces.new((oa[i], oa[i + 1], ob_[i + 1], ob_[i]))
        bm.faces.new((ia[i], ib[i], ib[i + 1], ia[i + 1]))
        bm.faces.new((oa[i], ia[i], ia[i + 1], oa[i + 1]))
        bm.faces.new((ob_[i], ob_[i + 1], ib[i + 1], ib[i]))
    bm.faces.new((oa[0], ob_[0], ib[0], ia[0]))
    bm.faces.new((oa[segs], ia[segs], ib[segs], ob_[segs]))
    return finish(bm, name, col, matkey, smooth=True)


# ======================================================================== assets
def house():
    c = collection("house")
    W, D, H = 8.0, 6.5, 2.9          # walls
    box(c, "plinth", (W + 0.3, D + 0.3, 0.45), (0, 0, 0.225), "house_base", bevel=0.03)
    box(c, "walls", (W, D, H), (0, 0, 0.45 + H / 2), "house_wall", bevel=0.03)
    top = 0.45 + H
    # roof: ridge along X, overhangs, slab thickness via two prisms (outer tiles, inner soffit)
    ov, rh = 0.45, 2.1
    hw, hd = W / 2 + ov, D / 2 + ov
    # gable end walls (triangles) at x = -W/2 and +W/2
    for sx, nm in ((-1, "gable_w"), (1, "gable_e")):
        x = sx * W / 2
        bm = bmesh.new()
        a = bm.verts.new((x, -D / 2, top)); b = bm.verts.new((x, D / 2, top)); t = bm.verts.new((x, 0, top + rh - 0.12))
        x2 = x - sx * 0.25
        a2 = bm.verts.new((x2, -D / 2, top)); b2 = bm.verts.new((x2, D / 2, top)); t2 = bm.verts.new((x2, 0, top + rh - 0.12))
        bm.faces.new((a, b, t)); bm.faces.new((a2, t2, b2))
        bm.faces.new((a, a2, b2, b)); bm.faces.new((b, b2, t2, t)); bm.faces.new((t, t2, a2, a))
        finish(bm, nm, c, "house_wall")
    for side in (-1, 1):                                   # two roof planes, 0.18 thick
        y_eave, y_ridge = side * hd, 0.0
        z_eave, z_ridge = top - 0.25, top + rh
        bm = bmesh.new()
        th = 0.18
        p = [(-hw, y_eave, z_eave), (hw, y_eave, z_eave), (hw, y_ridge, z_ridge), (-hw, y_ridge, z_ridge)]
        q = [(x, y, z - th) for x, y, z in p]
        vp = [bm.verts.new(v) for v in p]; vq = [bm.verts.new(v) for v in q]
        bm.faces.new(vp); bm.faces.new(vq[::-1])
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((vp[i], vq[i], vq[j], vp[j]))
        finish(bm, "roof_" + ("front" if side < 0 else "back"), c, "house_roof")
    box(c, "ridge_cap", (2 * hw + 0.04, 0.26, 0.12), (0, 0, top + rh + 0.02), "house_trim", bevel=0.03)
    for side in (-1, 1):                                   # fascia + gutter along the eaves
        box(c, f"fascia_{side}", (2 * hw, 0.04, 0.22), (0, side * (hd + 0.02), top - 0.33), "house_trim", bevel=0.01)
        cyl(c, f"gutter_{side}", 0.075, 2 * hw, (0, side * (hd + 0.1), top - 0.36), "gutter", segs=16, rot=(0, math.pi / 2, 0))
    cyl(c, "downpipe", 0.045, top - 0.1, (hw - 0.25, -(hd + 0.1), (top - 0.1) / 2), "gutter", segs=12)
    box(c, "chimney", (0.6, 0.6, 1.6), (W / 2 - 1.4, 1.0, top + rh - 0.3), "house_base", bevel=0.02)
    box(c, "chimney_cap", (0.75, 0.75, 0.1), (W / 2 - 1.4, 1.0, top + rh + 0.52), "house_trim", bevel=0.02)

    yF = -D / 2 - 0.001
    def window(name, x, z, w, h):
        box(c, name + "_frame", (w + 0.16, 0.14, h + 0.16), (x, yF - 0.02, z), "house_trim", bevel=0.02)
        box(c, name + "_glass", (w, 0.05, h), (x, yF - 0.07, z), "house_glass", bevel=0.0)
        box(c, name + "_mullion", (0.05, 0.08, h), (x, yF - 0.09, z), "house_trim", bevel=0.01)
        box(c, name + "_transom", (w, 0.08, 0.05), (x, yF - 0.09, z + h * 0.18), "house_trim", bevel=0.01)
        box(c, name + "_sill", (w + 0.3, 0.22, 0.06), (x, yF - 0.1, z - h / 2 - 0.1), "house_base", bevel=0.015)
    window("win_l", -2.6, 1.95, 1.3, 1.35)
    window("win_r", 2.4, 1.95, 1.6, 1.35)
    # door + frame + canopy + step
    box(c, "door_frame", (1.22, 0.14, 2.32), (0, yF - 0.02, 0.45 + 1.15), "house_trim", bevel=0.02)
    box(c, "door", (1.0, 0.08, 2.15), (0, yF - 0.06, 0.45 + 1.08), "house_door", bevel=0.01)
    for k in range(3):
        box(c, f"door_panel_{k}", (0.7, 0.03, 0.5), (0, yF - 0.1, 0.45 + 0.45 + k * 0.65), "house_door", bevel=0.01)
    cyl(c, "door_knob", 0.035, 0.08, (0.36, yF - 0.14, 0.45 + 1.05), "gutter", segs=12, rot=(math.pi / 2, 0, 0))
    box(c, "step", (1.8, 0.7, 0.2), (0, yF - 0.35, 0.1), "house_base", bevel=0.02)
    box(c, "canopy", (1.9, 0.9, 0.08), (0, yF - 0.45, 0.45 + 2.55), "house_roof", bevel=0.01)
    for sx in (-0.8, 0.8):
        box(c, f"canopy_bracket_{sx}", (0.06, 0.8, 0.06), (sx, yF - 0.4, 0.45 + 2.48), "house_trim", bevel=0.01)
    box(c, "outdoor_light", (0.14, 0.12, 0.22), (0.85, yF - 0.06, 0.45 + 1.95), "gutter", bevel=0.02)


def rig():
    """Compact crawler drill rig. Mast over the hole (x=0, y=0); body to the left (-x)."""
    c = collection("rig")
    P, S, K, R = "rig_paint", "steel_dark", "steel", "rubber"
    bx = -2.1
    for sy in (-0.62, 0.62):                               # crawler tracks
        box(c, f"track_{sy}", (2.6, 0.42, 0.5), (bx, sy, 0.25), R, bevel=0.16)
        for k in range(5):
            cyl(c, f"roller_{sy}_{k}", 0.13, 0.46, (bx - 1.0 + k * 0.5, sy, 0.22), S, segs=16, rot=(math.pi / 2, 0, 0))
        box(c, f"track_frame_{sy}", (2.2, 0.2, 0.18), (bx, sy * 0.78, 0.48), S, bevel=0.02)
    box(c, "deck", (3.0, 1.6, 0.22), (bx + 0.2, 0, 0.64), P, bevel=0.04)
    box(c, "engine", (1.4, 1.3, 0.95), (bx - 0.7, 0, 1.23), P, bevel=0.06)
    for k in range(7):
        box(c, f"vent_{k}", (0.9, 0.03, 0.04), (bx - 0.7, -0.66, 1.0 + k * 0.08), S, bevel=0.005)
    cyl(c, "exhaust", 0.05, 0.7, (bx - 1.2, 0.4, 1.95), S, segs=12)
    box(c, "console", (0.5, 0.5, 0.75), (bx + 0.75, -0.45, 1.13), P, bevel=0.04)
    for k in range(4):
        cyl(c, f"lever_{k}", 0.015, 0.25, (bx + 0.6 + k * 0.1, -0.6, 1.6), S, segs=8, rot=(0.3, 0, 0))
        ball(c, f"knob_{k}", 0.03, (bx + 0.6 + k * 0.1, -0.64, 1.72), R)
    cyl(c, "tank", 0.32, 1.0, (bx + 0.3, 0.45, 1.07), K, segs=24, rot=(0, math.pi / 2, 0), bevel=0.02)
    for sy in (-0.78, 0.78):                               # railing
        for k in range(4):
            cyl(c, f"post_{sy}_{k}", 0.018, 0.5, (bx - 1.1 + k * 0.85, sy, 1.0), S, segs=8)
        cyl(c, f"rail_{sy}", 0.018, 2.6, (bx + 0.17, sy, 1.25), S, segs=8, rot=(0, math.pi / 2, 0))
    # mast foot bracket from the deck to the mast
    box(c, "boom", (1.0, 0.5, 0.3), (-0.55, 0.3, 0.7), P, bevel=0.04)
    box(c, "mast_foot", (0.7, 0.5, 0.2), (0, 0.3, 0.15), S, bevel=0.03)
    # lattice mast: two rails BEHIND the rod plane, zig-zag braces
    Hm = 4.6
    for sx in (-0.24, 0.24):
        box(c, f"mast_rail_{sx}", (0.1, 0.12, Hm), (sx, 0.32, 0.25 + Hm / 2), P, bevel=0.015)
        box(c, f"mast_rail_b_{sx}", (0.08, 0.08, Hm), (sx, 0.62, 0.25 + Hm / 2), P, bevel=0.012)
    n = 11
    for k in range(n):
        z0 = 0.35 + k * (Hm - 0.3) / n
        z1 = z0 + (Hm - 0.3) / n
        beam(c, f"brace_s_{k}", (-0.24 if k % 2 == 0 else 0.24, 0.62, z0), (0.24 if k % 2 == 0 else -0.24, 0.62, z1), 0.035, P)
        beam(c, f"brace_l_{k}", (-0.24, 0.32, z0), (-0.24, 0.62, z1), 0.03, P)
        beam(c, f"brace_r_{k}", (0.24, 0.62, z0), (0.24, 0.32, z1), 0.03, P)
    box(c, "crown", (0.8, 0.6, 0.25), (0, 0.45, 0.25 + Hm + 0.1), S, bevel=0.03)
    cyl(c, "sheave", 0.18, 0.08, (0, 0.2, 0.25 + Hm + 0.15), K, segs=24, rot=(0, math.pi / 2, 0))
    beam(c, "mast_stay", (-1.3, 0.3, 0.8), (-0.24, 0.32, 2.4), 0.07, S)
    # pipe rack beside the mast
    for k in range(5):
        cyl(c, f"rack_pipe_{k}", 0.04, 2.2, (-1.2 + k * 0.1, -0.78, 1.7), K, segs=12, rot=(0.15, 0, 0))

    # moving rotary head (its own file): rod axis at x=0,y=0
    c = collection("rig_head")
    box(c, "head_body", (0.56, 0.42, 0.5), (0, 0.3, 0), P, bevel=0.05)
    cyl(c, "head_motor", 0.13, 0.32, (0, 0.05, 0.12), S, segs=20)
    box(c, "head_slide", (0.62, 0.12, 0.62), (0, 0.5, 0), S, bevel=0.02)
    cyl(c, "spindle", 0.06, 0.3, (0, 0, -0.32), K, segs=20)
    for sx in (-0.18, 0.18):
        cyl(c, f"hose_{sx}", 0.025, 0.9, (sx, 0.42, 0.55), R, segs=10, rot=(0.25, 0, 0))


def pump():
    c = collection("pump")
    cyl(c, "neck", 0.045, 0.12, (0, 0, -0.06), "steel_dark", segs=20)
    cyl(c, "discharge", 0.11, 0.12, (0, 0, -0.18), "steel", segs=32, bevel=0.01)
    cyl(c, "body", 0.11, 0.55, (0, 0, -0.52), "steel", segs=32)
    for k in range(6):
        cyl(c, f"rib_{k}", 0.116, 0.025, (0, 0, -0.28 - k * 0.09), "steel_dark", segs=32)
    cyl(c, "intake", 0.1, 0.12, (0, 0, -0.86), "steel_dark", segs=32)
    for k in range(10):
        a = k * math.pi / 5
        box(c, f"intake_slot_{k}", (0.02, 0.02, 0.08), (0.101 * math.cos(a), 0.101 * math.sin(a), -0.86), "rubber", bevel=0.003)
    cyl(c, "motor", 0.1, 0.6, (0, 0, -1.22), "steel", segs=32, bevel=0.01)
    cyl(c, "cable", 0.015, 1.4, (0.13, 0, -0.55), "rubber", segs=8)


def cap():
    c = collection("cap")
    cyl(c, "flange", 0.29, 0.05, (0, 0, 0.025), "steel_dark", segs=40, bevel=0.01)
    cyl(c, "cap_body", 0.27, 0.16, (0, 0, 0.13), "cap", segs=48, bevel=0.02)
    cyl(c, "cap_dome", 0.22, 0.09, (0, 0, 0.255), "cap", r2=0.12, segs=48)
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        cyl(c, f"bolt_{k}", 0.022, 0.06, (0.25 * math.cos(a), 0.25 * math.sin(a), 0.06), "steel", segs=6)
    cyl(c, "vent", 0.028, 0.12, (0.12, 0, 0.35), "steel", segs=16)
    cyl(c, "vent_hood", 0.055, 0.035, (0.12, 0, 0.42), "steel_dark", segs=16, bevel=0.008)


def casing_screen():
    c = collection("casing")
    half_shell(c, "casing_shell", 0.22, 0.2, -1.0, 0.0, "casing")
    c = collection("screen")
    nb, pitch, bh = 26, 1.0 / 26, 0.024
    for k in range(nb):
        zt = -k * pitch
        half_shell(c, f"band_{k}", 0.22, 0.2, zt - bh, zt, "screen", segs=32)
    for i in range(7):
        a = math.pi * i / 6
        box(c, f"rod_{i}", (0.018, 0.018, 1.0), (0.205 * math.cos(a), 0.205 * math.sin(a), -0.5), "steel_dark", bevel=0.002)


def hydrant():
    """Frost-free yard hydrant head + a paver under the spout. Riser pipe comes from Unity."""
    c = collection("hydrant")
    cyl(c, "head", 0.06, 0.22, (0, 0, 0.11), "hydrant", segs=24, bevel=0.01)
    box(c, "lever", (0.04, 0.04, 0.45), (0, 0.03, 0.4), "hydrant", rot=(0.5, 0, 0), bevel=0.01)
    cyl(c, "spout", 0.03, 0.18, (-0.1, 0, 0.12), "steel_dark", segs=16, rot=(0, math.pi / 2, 0))
    cyl(c, "spout_tip", 0.035, 0.06, (-0.19, 0, 0.09), "steel_dark", segs=16)
    c = collection("paver")
    box(c, "paver", (0.6, 0.6, 0.06), (0, 0, 0.03), "house_base", bevel=0.01)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    house(); rig(); pump(); cap(); casing_screen(); hydrant()
    out = os.path.join(ROOT, "blender", "src", "well_assets.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print("SAVED", out, "objects:", len(bpy.data.objects))


main()
