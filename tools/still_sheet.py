"""Tile rendered stills into one sheet: python tools/still_sheet.py <dir> <out.png> 0.00 1.80 4.50 ... [--w 320]"""
import argparse
import os
import subprocess

ap = argparse.ArgumentParser()
ap.add_argument("dir")
ap.add_argument("out")
ap.add_argument("times", nargs="+")
ap.add_argument("--w", type=int, default=320)
a = ap.parse_args()
ins = []
for t in a.times:
    ins += ["-i", os.path.join(a.dir, f"still_{float(t):.2f}.png")]
n = len(a.times)
fc = "".join(f"[{i}:v]scale={a.w}:-1[v{i}];" for i in range(n)) + "".join(f"[v{i}]" for i in range(n)) + f"hstack=inputs={n}[o]"
subprocess.run(["ffmpeg", "-v", "error", "-y"] + ins + ["-filter_complex", fc, "-map", "[o]", "-frames:v", "1", a.out], check=True)
print("WROTE", a.out)
