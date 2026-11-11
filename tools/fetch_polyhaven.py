"""Download Poly Haven CC0 assets (textures, models, HDRIs) into assets/polyhaven/<kind>/<id>/.

Poly Haven (https://polyhaven.com): CC0, no login, no attribution needed, safe for monetised video.

  python tools/fetch_polyhaven.py tex excavated_soil_wall sand_01 --res 2k
  python tools/fetch_polyhaven.py model tree_small_02 grass_medium_01 --res 1k
  python tools/fetch_polyhaven.py hdri kloofendal_48d_partly_cloudy_puresky --res 4k

Textures: Diffuse, nor_gl (OpenGL normal = Unity's convention), Rough, AO, then an HDRP MaskMap is
composed: R metallic (0), G AO, B detail mask (1), A smoothness (1 - roughness).
Models: the FBX and every texture it references, at their relative paths.
"""
import argparse
import json
import os
import sys
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UA = {"User-Agent": "Mozilla/5.0 (we-made-it pipeline)"}


def get_json(url):
    return json.load(urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60))


def download(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r, open(path + ".part", "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            f.write(b)
    os.replace(path + ".part", path)
    return True


def pick(node, res):
    """node = {res: {...}}; take the requested resolution or the nearest smaller one."""
    order = ["8k", "4k", "2k", "1k"]
    for r in order[order.index(res):] + order[:order.index(res)][::-1]:
        if r in node:
            return node[r]
    return None


def fetch_tex(aid, res):
    files = get_json(f"https://api.polyhaven.com/files/{aid}")
    dest = os.path.join(ROOT, "assets", "polyhaven", "tex", aid)
    got = {}
    for m in ("Diffuse", "nor_gl", "Rough", "AO"):
        node = files.get(m)
        if not node:
            continue
        r = pick(node, res)
        fmt = r.get("jpg") or r.get("png")
        p = os.path.join(dest, f"{aid}_{m}.{'jpg' if r.get('jpg') else 'png'}")
        download(fmt["url"], p)
        got[m] = p
    # HDRP MaskMap
    from PIL import Image
    import numpy as np
    base = Image.open(got["Diffuse"])
    w, h = base.size
    rough = np.asarray(Image.open(got["Rough"]).convert("L").resize((w, h)), dtype=np.float32) if "Rough" in got else np.full((h, w), 200, np.float32)
    ao = np.asarray(Image.open(got["AO"]).convert("L").resize((w, h)), dtype=np.float32) if "AO" in got else np.full((h, w), 255, np.float32)
    mask = np.zeros((h, w, 4), np.uint8)
    mask[..., 0] = 0
    mask[..., 1] = ao.astype(np.uint8)
    mask[..., 2] = 255
    mask[..., 3] = (255 - rough).clip(0, 255).astype(np.uint8)
    Image.fromarray(mask, "RGBA").save(os.path.join(dest, f"{aid}_Mask.png"))
    print("TEX", aid, sorted(got))


def composite_alpha(dest):
    """Foliage: the colour map with its alpha map as a real alpha channel (*_diffa_*), for HDRP alpha cutoff."""
    import glob
    from PIL import Image
    for a in glob.glob(os.path.join(dest, "textures", "*_alpha_*.png")):
        pre, res = a.rsplit("_alpha_", 1)
        res = os.path.splitext(res)[0]
        d = [p for p in (f"{pre}_diff_{res}.png", f"{pre}_diff_{res}.jpg") if os.path.exists(p)]
        if d:
            im = Image.open(d[0]).convert("RGB")
            im.putalpha(Image.open(a).convert("L").resize(im.size))
            im.save(f"{pre}_diffa_{res}.png")


def fetch_model(aid, res):
    files = get_json(f"https://api.polyhaven.com/files/{aid}")
    dest = os.path.join(ROOT, "assets", "polyhaven", "model", aid)
    node = files.get("fbx")
    if not node:
        print("MODEL", aid, "has no fbx; formats:", list(files))
        return
    r = pick(node, res)["fbx"]
    download(r["url"], os.path.join(dest, os.path.basename(r["url"].split("?")[0])))
    for rel, inc in (r.get("include") or {}).items():
        download(inc["url"], os.path.join(dest, rel))
    composite_alpha(dest)
    print("MODEL", aid, "files:", 1 + len(r.get("include") or {}))


def fetch_hdri(aid, res):
    files = get_json(f"https://api.polyhaven.com/files/{aid}")
    r = pick(files["hdri"], res)
    u = r["hdr"]["url"]
    p = os.path.join(ROOT, "assets", "polyhaven", "hdri", aid, os.path.basename(u))
    download(u, p)
    print("HDRI", aid, p)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["tex", "model", "hdri"])
    ap.add_argument("ids", nargs="+")
    ap.add_argument("--res", default="2k")
    a = ap.parse_args()
    fn = {"tex": fetch_tex, "model": fetch_model, "hdri": fetch_hdri}[a.kind]
    for i in a.ids:
        try:
            fn(i, a.res)
        except Exception as e:
            print("FAILED", a.kind, i, repr(e))


if __name__ == "__main__":
    sys.exit(main())
