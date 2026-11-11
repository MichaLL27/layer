"""Frames from a finished video, tiled in rows: python tools/frame_sheet.py <video> <out_prefix> [--every 2] [--cols 7] [--w 270] [--from 0] [--to end]"""
import argparse
import os
import shutil
import subprocess
import tempfile

ap = argparse.ArgumentParser()
ap.add_argument("video")
ap.add_argument("prefix")
ap.add_argument("--every", type=float, default=2.0)
ap.add_argument("--cols", type=int, default=7)
ap.add_argument("--w", type=int, default=270)
ap.add_argument("--from", dest="t0", type=float, default=0.0)
ap.add_argument("--to", type=float)
a = ap.parse_args()
dur = float(subprocess.check_output(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", a.video]).decode())
end = a.to if a.to else dur
times = []
t = a.t0
while t < end - 0.02:
    times.append(t)
    t += a.every
tmp = tempfile.mkdtemp()
files = []
for i, t in enumerate(times):
    p = os.path.join(tmp, f"{i:03d}.png")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", f"{t:.3f}", "-i", a.video, "-frames:v", "1", "-vf", f"scale={a.w}:-1", p], check=True)
    files.append(p)
for r in range(0, len(files), a.cols):
    row = files[r:r + a.cols]
    out = f"{a.prefix}_{r // a.cols}.png"
    if len(row) == 1:
        shutil.copyfile(row[0], out)
    else:
        args = []
        for f in row:
            args += ["-i", f]
        subprocess.run(["ffmpeg", "-v", "error", "-y"] + args + ["-filter_complex", f"hstack=inputs={len(row)}", out], check=True)
    print(out, [f"{times[i]:.1f}" for i in range(r, min(r + a.cols, len(times)))])
shutil.rmtree(tmp)
