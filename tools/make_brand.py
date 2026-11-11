"""Builds the channel art (avatar, banner, watermark) for "Layer by Layer 3D", design 02 "sunny cutaway".

SVG/HTML sources are written to channel/src/, PNGs are rasterised with headless Edge.
  python tools/make_brand.py                                   # everything
  python tools/make_brand.py --line1 "X" --line2 "Y" --tagline "..."   # change the banner text

Outputs (design 01, the navy layer-block, stays in the folders as the older version):
  channel/avatar/02_sunny_cutaway.png    800x800   (+ current.png copy)
  channel/banner/02_sunny_cutaway.png    2560x1440 (+ current.png copy); title + slab sit inside
                                         YouTube's 1546x423 safe area (x 507..2053, y 508..932)
  channel/watermark.png                  150x150, transparent
"""
import argparse
import os
import shutil
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CH = os.path.join(ROOT, "channel")
SRC = os.path.join(CH, "src")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
DESIGN = "02_sunny_cutaway"

NAVY = "#0D1B2A"
CORAL = "#E8553D"
SKY_TOP, SKY_BOT = "#FFE873", "#FFB347"

# (top, left, right), listed TOP layer first
LAYERS = [
    ("#A8EA54", "#6CB02E", "#4F8A1F"),  # grass
    ("#B97A4A", "#8E5530", "#6E3E22"),  # soil
    ("#E5853F", "#BC6428", "#8F4719"),  # clay
    ("#6CC7FF", "#3A9BE0", "#2A78B8"),  # water
    ("#A3AEC0", "#728095", "#525E70"),  # stone
]


def pts(points):
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in points)


def block(cx, top_y, s=1.0, lift=26, a=100, b=58, t=34, g=10, house=True):
    """Isometric stack of layers, top layer lifted, optional house on top.
    cx = centre x, top_y = top of the layers (the house stands above it).
    Returns (svg, y_of_bottom)."""
    a, b, t, g, lift = a * s, b * s, t * s, g * s, lift * s
    n = len(LAYERS)
    ys, y = [], top_y + b
    for i in range(n):
        ys.append(y)
        y += t + g + (lift if i == 0 else 0)
    out = []
    for i in reversed(range(n)):
        top, left, right = LAYERS[i]
        ty = ys[i]
        out.append(f'<polygon points="{pts([(cx - a, ty), (cx, ty + b), (cx, ty + b + t), (cx - a, ty + t)])}" fill="{left}"/>')
        out.append(f'<polygon points="{pts([(cx, ty + b), (cx + a, ty), (cx + a, ty + t), (cx, ty + b + t)])}" fill="{right}"/>')
        out.append(f'<polygon points="{pts([(cx, ty - b), (cx + a, ty), (cx, ty + b), (cx - a, ty)])}" fill="{top}"/>')
    if house:
        out.append(iso_house(cx, ys[0], a, b, s))
    return "\n".join(out), ys[-1] + b + t


def iso_house(cx, ty, a, b, s):
    """Small gable-roof house on the top face (centre cx,ty). Unit square [-1,1]^2 = the rhombus."""
    def P(x, y, z):
        return (cx + (x - y) * a / 2, ty + (x + y) * b / 2 - z * s)

    def poly(ps, fill):
        return f'<polygon points="{pts([P(*p) for p in ps])}" fill="{fill}"/>'

    xm, ym, ov = 0.55, 0.42, 0.09
    wall, rh = 62, 48
    o = [
        poly([(-xm, ym, 0), (xm, ym, 0), (xm, ym, wall), (-xm, ym, wall)], "#FFF3DA"),      # left wall
        poly([(xm, -ym, 0), (xm, ym, 0), (xm, ym, wall), (xm, -ym, wall)], "#E6CFA3"),      # right wall
        poly([(xm + .001, -0.12, 0), (xm + .001, 0.14, 0), (xm + .001, 0.14, wall * .58), (xm + .001, -0.12, wall * .58)], "#F08A3C"),  # door
    ]
    for x0, x1 in ((-0.42, -0.14), (0.14, 0.42)):                                          # windows
        o.append(poly([(x0, ym + .001, wall * .36), (x1, ym + .001, wall * .36), (x1, ym + .001, wall * .8), (x0, ym + .001, wall * .8)], "#8FD9FF"))
    zt = wall
    o += [
        poly([(-xm - ov, -ym - ov, zt), (xm + ov, -ym - ov, zt), (xm + ov, 0, zt + rh), (-xm - ov, 0, zt + rh)], "#1B7F74"),   # far slope
        poly([(xm + ov, -ym, zt), (xm + ov, ym, zt), (xm + ov, 0, zt + rh)], "#E6CFA3"),                                      # gable end
        poly([(-xm - ov, ym + ov, zt), (xm + ov, ym + ov, zt), (xm + ov, 0, zt + rh), (-xm - ov, 0, zt + rh)], "#2AA396"),     # near slope
    ]
    return "".join(o)


def shadow(cx, y, s):
    return f'<ellipse cx="{cx}" cy="{y}" rx="{150 * s}" ry="{34 * s}" fill="#000" opacity="0.22"/>'


def sky_defs(size_w, size_h):
    return (f'<defs><linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{SKY_TOP}"/>'
            f'<stop offset="1" stop-color="{SKY_BOT}"/></linearGradient></defs>'
            f'<rect width="{size_w}" height="{size_h}" fill="url(#sky)"/>')


def cloud(x, y, k=1.0, op=0.85):
    c = [(0, 0, 46), (52, -16, 56), (110, 0, 44), (58, 12, 52)]
    return (f'<g opacity="{op}" fill="#fff">' +
            "".join(f'<circle cx="{x + dx * k}" cy="{y + dy * k}" r="{r * k}"/>' for dx, dy, r in c) +
            f'<rect x="{x - 40 * k}" y="{y}" width="{190 * k}" height="{50 * k}" rx="{25 * k}"/></g>')


def mark_svg(size, scale, bg=True, house=True):
    _, h = block(0, 0, scale, house=False)
    house_h = 112 * scale if house else 0                 # wall + roof stand above the top face
    top = (size - (h + house_h)) / 2 + house_h
    g, h2 = block(size / 2, top, scale, house=house)
    bgs = ""
    sh = ""
    if bg:
        bgs = sky_defs(size, size) + cloud(size * .08, size * .2, size / 1000) + cloud(size * .68, size * .12, size / 1300, 0.7)
        sh = shadow(size / 2, h2 + 8 * scale, scale)
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}">{bgs}{sh}{g}</svg>'


def slab():
    """Front-view cutaway slab with a house, a bunker, a pool and a tree. Spans x 1230..2060, y 548..930."""
    x0, x1, yg, base = 1230, 2030, 760, 930
    d = 30                                            # depth of the 3D edge
    L = []
    L.append('<clipPath id="slabclip"><rect x="%d" y="%d" width="%d" height="%d" rx="18"/></clipPath>' % (x0, yg, x1 - x0, base - yg))
    layers = [(yg, 18, "#6CB02E"), (yg + 18, 56, "#8E5530"), (yg + 74, 42, "#BC6428"), (yg + 116, 30, "#3A9BE0"), (yg + 146, 24, "#728095")]
    L.append('<g clip-path="url(#slabclip)">')
    for y, h, c in layers:
        L.append(f'<rect x="{x0}" y="{y}" width="{x1 - x0}" height="{h}" fill="{c}"/>')
    # pebbles in the soil
    for px, py, r in ((1260, 800, 5), (1590, 818, 6), (1700, 790, 4), (1900, 805, 6), (1980, 830, 5), (1560, 840, 4),
                      (1290, 862, 6), (1640, 868, 5), (1850, 858, 4), (1980, 880, 6)):
        L.append(f'<ellipse cx="{px}" cy="{py}" rx="{r * 1.4}" ry="{r}" fill="#000" opacity="0.16"/>')
    L.append('</g>')
    # 3D edge: top surface + right side
    L.append(f'<polygon points="{pts([(x0, yg), (x0 + d, yg - 16), (x1 + d, yg - 16), (x1, yg)])}" fill="#8BDD43"/>')
    L.append(f'<polygon points="{pts([(x1, yg), (x1 + d, yg - 16), (x1 + d, base - 22), (x1, base)])}" fill="#4A2F1E"/>')
    # bunker + shaft (cut into the soil)
    L.append('<rect x="1432" y="760" width="26" height="42" fill="#BFD0E0"/>')
    for ly in (770, 780, 790):
        L.append(f'<line x1="1432" y1="{ly}" x2="1458" y2="{ly}" stroke="#7A8CA0" stroke-width="3"/>')
    L.append('<rect x="1330" y="798" width="230" height="86" rx="26" fill="#EEF3F8"/>')
    L.append('<rect x="1342" y="808" width="206" height="68" rx="18" fill="#C8D8E8"/>')
    L.append('<circle cx="1445" cy="822" r="7" fill="#FFE873"/>')
    L.append('<rect x="1470" y="840" width="62" height="10" fill="#8E5530"/><rect x="1470" y="862" width="62" height="10" fill="#8E5530"/>'
             '<rect x="1470" y="840" width="6" height="36" fill="#6E3E22"/><rect x="1526" y="840" width="6" height="36" fill="#6E3E22"/>')
    L.append('<circle cx="1385" cy="846" r="9" fill="#F2C7A0"/><rect x="1375" y="855" width="20" height="21" rx="6" fill="#35506B"/>')
    # pool (cut into the ground, white rim)
    L.append('<rect x="1636" y="760" width="228" height="64" rx="10" fill="#fff"/>')
    L.append('<rect x="1646" y="772" width="208" height="46" rx="8" fill="#59C9F5"/>')
    L.append('<rect x="1646" y="772" width="208" height="8" fill="#9BE3FF"/>')
    L.append('<path d="M1670 796 q12 -8 24 0 t24 0 t24 0" stroke="#fff" stroke-width="3" fill="none" opacity=".7"/>')
    L.append('<g stroke="#C9D3DD" stroke-width="5" fill="none"><path d="M1836 732 v54 M1850 732 v54"/></g>'
             '<g stroke="#C9D3DD" stroke-width="4"><line x1="1836" y1="746" x2="1850" y2="746"/><line x1="1836" y1="760" x2="1850" y2="760"/></g>')
    # house (front cutaway), standing on the top surface
    hb = 752
    L.append(f'<polygon points="{pts([(1286, 648), (1432, 556), (1578, 648)])}" fill="#1B7F74"/>')
    L.append(f'<polygon points="{pts([(1286, 648), (1432, 556), (1432, 566), (1306, 648)])}" fill="#2AA396"/>')
    L.append(f'<rect x="1300" y="640" width="264" height="{hb - 640}" fill="#FFF3DA"/>')
    L.append(f'<rect x="1314" y="654" width="236" height="{hb - 654 - 8}" fill="#FFE2A8"/>')
    L.append(f'<rect x="1300" y="{hb - 8}" width="264" height="8" fill="#D9BE8A"/>')
    L.append('<rect x="1338" y="676" width="46" height="40" rx="3" fill="#8FD9FF" stroke="#fff" stroke-width="5"/>')
    L.append('<rect x="1480" y="676" width="46" height="40" rx="3" fill="#8FD9FF" stroke="#fff" stroke-width="5"/>')
    L.append(f'<rect x="1400" y="{hb - 40}" width="74" height="32" rx="8" fill="{CORAL}"/>'
             f'<rect x="1394" y="{hb - 52}" width="86" height="16" rx="8" fill="#F27B65"/>')
    L.append('<line x1="1500" y1="654" x2="1500" y2="688" stroke="#8E5530" stroke-width="3"/><circle cx="1500" cy="692" r="9" fill="#FFE873"/>'
             f'<rect x="1506" y="{hb - 38}" width="20" height="30" fill="#F08A3C"/>')
    # tree
    L.append('<rect x="1940" y="700" width="16" height="52" fill="#8E5530"/>')
    for cxx, cyy, r in ((1948, 668, 40), (1920, 692, 28), (1978, 692, 28)):
        L.append(f'<circle cx="{cxx}" cy="{cyy}" r="{r}" fill="#4DB04A"/>')
    L.append('<circle cx="1936" cy="656" r="12" fill="#7CD264" opacity=".8"/>')
    return "".join(L)


def banner_svg(line1, line2, tagline):
    W, H = 2560, 1440
    p = [sky_defs(W, H)]
    p.append('<circle cx="2300" cy="230" r="170" fill="#FFF6B0" opacity=".55"/>')
    for x, y, k in ((300, 260, 1.5), (780, 360, 1.0), (1900, 330, 1.2), (2250, 640, 0.9), (200, 1020, 1.2), (2150, 1150, 1.5)):
        p.append(cloud(x, y, k))
    # floating mini blocks outside the safe area
    for cx, ty, sc in ((260, 560, 0.5), (2350, 880, 0.55), (600, 1130, 0.42), (2380, 330, 0.4)):
        g, _ = block(cx, ty, sc, house=True)
        p.append(f'<g opacity=".95">{g}</g>')
    # text (left), safe area x>=507
    tx = 548
    p.append(f'<text x="{tx}" y="650" font-family="Segoe UI Black, Arial Black, sans-serif" font-size="116" fill="{NAVY}">{line1}</text>')
    l2a, _, l2b = line2.rpartition(" ")
    p.append(f'<text x="{tx}" y="768" font-family="Segoe UI Black, Arial Black, sans-serif" font-size="116" fill="{NAVY}">{l2a} <tspan fill="{CORAL}">{l2b}</tspan></text>')
    p.append(f'<rect x="{tx + 2}" y="802" width="150" height="9" rx="4" fill="{CORAL}"/>')
    p.append(f'<text x="{tx + 2}" y="858" font-family="Segoe UI Semibold, Segoe UI, sans-serif" font-size="37" font-weight="700" fill="{NAVY}" letter-spacing="3">{tagline}</text>')
    p.append(slab())
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">{"".join(p)}</svg>'


def shot(svg_path, png_path, w, h, transparent=False):
    html = os.path.splitext(svg_path)[0] + ".html"
    with open(html, "w", encoding="utf-8") as f:
        f.write(f'<!doctype html><meta charset="utf-8"><style>html,body{{margin:0;padding:0;background:transparent;overflow:hidden}}'
                f'svg{{display:block}}</style>{open(svg_path, encoding="utf-8").read()}')
    bg = "00000000" if transparent else "ffffffff"
    cmd = [EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--default-background-color={bg}",
           f"--window-size={w},{h}", f"--screenshot={png_path}", "file:///" + html.replace("\\", "/")]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    os.remove(html)


def write(path, text):
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--line1", default="LAYER BY")
    ap.add_argument("--line2", default="LAYER 3D")
    ap.add_argument("--tagline", default="HOW EVERYTHING GETS BUILT")
    args = ap.parse_args()
    for d in (SRC, os.path.join(CH, "avatar"), os.path.join(CH, "banner")):
        os.makedirs(d, exist_ok=True)

    write(os.path.join(SRC, "avatar.svg"), mark_svg(800, 1.05))
    write(os.path.join(SRC, "banner.svg"), banner_svg(args.line1, args.line2, args.tagline))
    write(os.path.join(SRC, "watermark.svg"), mark_svg(150, 0.3, bg=False))

    av = os.path.join(CH, "avatar", DESIGN + ".png")
    bn = os.path.join(CH, "banner", DESIGN + ".png")
    wm = os.path.join(CH, "watermark.png")
    shot(os.path.join(SRC, "avatar.svg"), av, 800, 800)
    shot(os.path.join(SRC, "banner.svg"), bn, 2560, 1440)
    shot(os.path.join(SRC, "watermark.svg"), wm, 150, 150, transparent=True)
    shutil.copyfile(av, os.path.join(CH, "avatar", "current.png"))
    shutil.copyfile(bn, os.path.join(CH, "banner", "current.png"))
    for p in (av, bn, wm):
        print("WROTE", p)


if __name__ == "__main__":
    sys.exit(main())
