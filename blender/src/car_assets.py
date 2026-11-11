"""Car-video models (Short 03), built headless in Blender 5.1, one collection per asset.

  blender.exe -b --python blender/src/car_assets.py
  blender.exe -b blender/src/car_assets.blend -P tools/blender_export.py -- --out WeMadeIt_Unity/Assets/Resources/Models

1 unit = 1 m, Z up, camera on -Y. The car faces -X (nose to the left), so the driver sits on the
camera side. It is a cutaway: the near door panels are left out; what stays on the near side is the
pillars, the roof edge and the window glass (separate objects, so Unity can lower, shatter or
swing them). Pivot of the car: the middle of its floor. Material names are keys of WellLook.ModelMaterial.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tunnel_assets import beam, box, collection, cyl_x, mat  # noqa: E402
from car_shell import shell, xo  # noqa: E402

import bmesh  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
L, W = 2.25, 0.9          # half length, half width


def wheel(c, name, x, y):
    ob = cyl_x(c, name + "_tire", 0.33, -0.11, 0.11, (0, 0), "tire", segs=32)
    ob.rotation_euler = (math.pi / 2, 0, 0)
    ob.location = (x, y, 0.15)
    ob = cyl_x(c, name + "_rim", 0.2, -0.115, 0.115, (0, 0), "steel_dark", segs=24)
    ob.rotation_euler = (math.pi / 2, 0, 0)
    ob.location = (x, y, 0.15)


def steering_wheel(c, centre, R=0.18, r=0.017):
    """A real rim (torus), a hub and three spokes. The face points back and up at the driver (+X, +Z):
    the column's direction. CarScene's grip points use the same centre, radius and 65-degree tilt."""
    bm = bmesh.new()
    NU, NV = 48, 12
    rows = []
    for i in range(NU):
        a = 2 * math.pi * i / NU
        rows.append([bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
                     for b in (2 * math.pi * j / NV for j in range(NV))])
    for i in range(NU):
        for j in range(NV):
            bm.faces.new((rows[i][j], rows[(i + 1) % NU][j], rows[(i + 1) % NU][(j + 1) % NV], rows[i][(j + 1) % NV]))
    me = bpy.data.meshes.new("steering_wheel")
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat("tire"))
    ob = bpy.data.objects.new("steering_wheel", me)
    ob.location = centre
    ob.rotation_euler = (0, math.radians(65), 0)
    c.objects.link(ob)
    # hub and spokes, in the rim's own frame
    hub = cyl_x(c, "steering_hub", 0.055, -0.02, 0.03, (0, 0), "car_interior", segs=24)
    hub.parent = ob
    hub.rotation_euler = (0, 0, 0)            # axis along the rim's axis (local Z)
    hub.location = (0, 0, 0.005)
    for k, a in enumerate((0.0, math.pi, -math.pi / 2)):
        sp = box(c, f"steering_spoke_{k}", (R - 0.05, 0.035, 0.012), ((R / 2 + 0.02) * math.cos(a), (R / 2 + 0.02) * math.sin(a), 0), "car_interior", bevel=0.004)
        sp.rotation_euler = (0, 0, a)
        sp.parent = ob


def car():
    c = collection("car_body")
    P = "car_paint"
    shell(c, L, W)
    # rear window, sloped like the C pillars
    beam(c, "rear_window", (1.4, 0, 0.72), (1.1, 0, 1.36), 0.02, "car_glass")
    bpy.data.objects["rear_window"].scale = (1.0, 2 * W / 0.02 * 0.9, 1.0)
    # floor, sills, bulkheads
    box(c, "floor", (2 * L - 0.5, 2 * W - 0.1, 0.08), (0.1, 0, 0.0), "car_interior", bevel=0.01)
    # front: hood, engine bay, bumper, lights
    for sy in (-0.6, 0.6):
        box(c, f"headlight_{sy}", (0.08, 0.3, 0.09), (xo(L, W, sy) + 0.03, sy, 0.4), "car_light", bevel=0.025)
    box(c, "grille", (0.05, 0.72, 0.12), (-L + 0.01, 0, 0.3), "car_trim", bevel=0.02)
    box(c, "engine", (0.85, 1.2, 0.42), (-L + 0.62, 0, 0.3), "steel_dark", bevel=0.05)
    box(c, "firewall", (0.06, 2 * W - 0.1, 0.5), (-L + 1.2, 0, 0.32), "car_interior", bevel=0.01)
    box(c, "dash", (0.42, 2 * W - 0.15, 0.24), (-L + 1.38, 0, 0.66), "car_interior", bevel=0.04)
    # rear: trunk, bumper, lights
    for sy in (-0.65, 0.65):
        box(c, f"taillight_{sy}", (0.07, 0.28, 0.09), (-xo(L, W, sy) - 0.025, sy, 0.55), "car_tail", bevel=0.025)
    box(c, "rear_shelf", (0.3, 2 * W - 0.15, 0.06), (L - 0.95, 0, 0.66), "car_interior", bevel=0.01)
    # roof and pillars (both sides)
    for sy, nm in ((W - 0.07, "far"), (-W + 0.07, "near")):
        beam(c, f"a_pillar_{nm}", (-L + 1.2, sy, 0.66), (-0.62, sy * 0.97, 1.36), 0.07, P)
        box(c, f"b_pillar_{nm}", (0.09, 0.07, 0.72), (0.3, sy, 1.02), P, bevel=0.02)
        beam(c, f"c_pillar_{nm}", (L - 0.85, sy, 0.66), (1.12, sy * 0.97, 1.36), 0.07, P)
        box(c, f"roof_rail_{nm}", (1.7, 0.07, 0.06), (0.25, sy * 0.97, 1.34), P, bevel=0.025)
    # far side: doors with window glass
    box(c, "window_far", (2.1, 0.03, 0.62), (0.2, W - 0.05, 1.0), "car_glass", bevel=0.0)
    # interior
    for x, nm in ((-0.25, "front"),):
        for sy in (-0.45, 0.45):
            box(c, f"seat_{nm}_{sy}", (0.5, 0.52, 0.12), (x, sy, 0.3), "car_seat", bevel=0.05)
            box(c, f"seat_back_{nm}_{sy}", (0.12, 0.5, 0.62), (x + 0.27, sy, 0.65), "car_seat", bevel=0.05)
            box(c, f"headrest_{nm}_{sy}", (0.1, 0.26, 0.18), (x + 0.3, sy, 1.05), "car_seat", bevel=0.04)
    box(c, "seat_rear", (0.55, 2 * W - 0.25, 0.14), (0.85, 0, 0.3), "car_seat", bevel=0.05)
    box(c, "seat_rear_back", (0.14, 2 * W - 0.25, 0.6), (1.18, 0, 0.62), "car_seat", bevel=0.05)
    beam(c, "steering_column", (-L + 1.38, -0.45, 0.68), (-0.58, -0.45, 0.85), 0.05, "car_interior")
    steering_wheel(c, (-0.56, -0.45, 0.87))
    for x, y in ((-L + 0.7, -W + 0.02), (-L + 0.7, W - 0.02), (L - 0.72, -W + 0.02), (L - 0.72, W - 0.02)):
        wheel(c, f"wheel_{x}_{y}", x, y)
    box(c, "mirror", (0.12, 0.16, 0.1), (-0.65, -W - 0.06, 0.78), P, bevel=0.02)

    # near side glass, each its own file so Unity can move it
    c = collection("car_glass_front")
    box(c, "glass", (0.95, 0.025, 0.6), (0, 0, 0.3), "car_glass", bevel=0.0)       # pivot on its bottom edge
    c = collection("car_glass_rear")
    box(c, "glass", (0.85, 0.025, 0.55), (0, 0, 0.275), "car_glass", bevel=0.0)
    c = collection("car_windshield")
    box(c, "glass", (0.03, 2 * W - 0.18, 0.9), (0, 0, 0.45), "car_glass", bevel=0.0)
    # the near front door: frame only (the panel below the glass is the cutaway), hinged at x = 0
    c = collection("car_door_near")
    box(c, "frame_top", (0.95, 0.06, 0.05), (0.48, 0, 0.72), "car_paint", bevel=0.015)
    box(c, "frame_front", (0.05, 0.06, 0.72), (0.02, 0, 0.36), "car_paint", bevel=0.015)
    box(c, "frame_back", (0.05, 0.06, 0.72), (0.94, 0, 0.36), "car_paint", bevel=0.015)
    box(c, "door_sill", (0.95, 0.06, 0.08), (0.48, 0, 0.0), "car_paint", bevel=0.015)


def bridge():
    """Concrete road bridge, deck along X, top of the road at z = 0, 9 m wide (y), 40 m long."""
    c = collection("bridge")
    Lb = 40.0
    box(c, "deck", (Lb, 9.0, 0.7), (0, 0, -0.35), "bridge_concrete", bevel=0.03)
    box(c, "road", (Lb, 7.0, 0.04), (0, 0, 0.0), "asphalt", bevel=0.0)
    for sy in (-4.2, 4.2):
        box(c, f"kerb_{sy}", (Lb, 0.6, 0.22), (0, sy, 0.09), "bridge_concrete", bevel=0.02)
    for k in range(-10, 11):
        if -1 <= k <= 1:
            continue                       # the gap the car goes through (the rail it breaks is separate)
        x = k * 1.9
        box(c, f"post_n_{k}", (0.1, 0.1, 0.9), (x, -4.35, 0.62), "steel", bevel=0.01)
        box(c, f"post_f_{k}", (0.1, 0.1, 0.9), (x, 4.35, 0.62), "steel", bevel=0.01)
    for seg in ((-19.5, -2.9), (2.9, 19.5)):
        x0, x1 = seg
        box(c, f"rail_n_{x0}", (x1 - x0, 0.08, 0.12), ((x0 + x1) / 2, -4.35, 1.0), "steel", bevel=0.01)
        box(c, f"rail_n2_{x0}", (x1 - x0, 0.06, 0.08), ((x0 + x1) / 2, -4.35, 0.6), "steel", bevel=0.01)
    box(c, "rail_f", (Lb - 1, 0.08, 0.12), (0, 4.35, 1.0), "steel", bevel=0.01)
    for x in (-7.5, 7.5):
        box(c, f"pier_{x}", (1.2, 7.0, 12.0), (x, 0, -6.7), "bridge_concrete", bevel=0.05)
    # the rail section the car breaks, its own piece
    c = collection("bridge_rail_break")
    for k in (-1, 0, 1):
        box(c, f"post_{k}", (0.1, 0.1, 0.9), (k * 1.9, 0, 0.62), "steel", bevel=0.01)
    box(c, "rail", (5.8, 0.08, 0.12), (0, 0, 1.0), "steel", bevel=0.01)
    box(c, "rail2", (5.8, 0.06, 0.08), (0, 0, 0.6), "steel", bevel=0.01)


def phone():
    c = collection("phone")
    box(c, "body", (0.075, 0.15, 0.009), (0, 0, 0.0045), "steel_dark", bevel=0.003)
    box(c, "screen", (0.068, 0.14, 0.002), (0, 0, 0.0095), "phone_screen", bevel=0.0)


def breaker():
    c = collection("window_breaker")
    box(c, "grip", (0.03, 0.09, 0.03), (0, -0.045, 0), "car_tail", bevel=0.008)
    box(c, "head", (0.04, 0.03, 0.045), (0, 0.01, 0), "steel", bevel=0.005)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    car(); bridge(); phone(); breaker()
    out = os.path.join(ROOT, "blender", "src", "car_assets.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print("SAVED", out, "objects:", len(bpy.data.objects))


main()
