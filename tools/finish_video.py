"""Captions + title + voice onto the Unity render.

  python tools/finish_video.py well [--video output/well/video.mp4] [--out output/well/well_draft.mp4]
                                    [--title1 "HOW A WELL GETS DUG"] [--title2 "(AND STAYS CLEAN)"]

Reads docs/scenes/<slug>_words.json (word times) and sound/voice/<slug>_<tag>.mp3, writes an .ass
(one word at a time, big, centred low, key words in lime), burns it in with the voice muxed.
Captions sit at ~72% of the height: clear of Shorts' bottom UI and of the right-hand buttons.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
KEY = {"river", "tunnel", "wheel", "machine", "pressure", "chamber", "robot", "ring", "concrete", "seals", "watertight", "legs", "grout", "trains",
       "water", "sand", "gravel", "rock", "well", "hole", "casing", "pipe", "clay", "cement", "rain", "screen",
       "slots", "pump", "cap", "chlorine", "germ-free", "clean", "seal", "sealed"}


def ts(x):
    h = int(x // 3600); m = int((x % 3600) // 60); s = x % 60
    return f"{h}:{m:02d}:{s:05.2f}"


PILL = {"w": ("&H00FFFFFF", "&H002A1B0D"), "y": ("&H004DD8FF", "&H002A1B0D"), "g": ("&H004BF2B8", "&H002A1B0D"),
        "b": ("&H00F0B43C", "&H00FFFFFF"), "o": ("&H002A8AFF", "&H00FFFFFF")}


def pill_path(w, h):
    r = h / 2.0
    c = r * 0.45
    return (f"m {r:.0f} 0 l {w - r:.0f} 0 b {w - c:.0f} 0 {w:.0f} {c:.0f} {w:.0f} {r:.0f} "
            f"b {w:.0f} {h - c:.0f} {w - c:.0f} {h:.0f} {w - r:.0f} {h:.0f} l {r:.0f} {h:.0f} "
            f"b {c:.0f} {h:.0f} 0 {h - c:.0f} 0 {r:.0f} b 0 {c:.0f} {c:.0f} 0 {r:.0f} 0")


def label_events(path):
    """One pill + one text event per label per frame, at the screen position Unity measured."""
    if not os.path.exists(path):
        return []
    d = json.load(open(path, encoding="utf-8"))
    fps = d["fps"]
    BS = chr(92)
    ev = []
    for f, labs in enumerate(d["frames"]):
        t0, t1 = ts(f / fps), ts((f + 1) / fps)
        for L in labs:
            fill, ink = PILL.get(L["style"], PILL["w"])
            w = len(L["text"]) * 37 + 56
            h = 80
            x = min(max(L["x"], w / 2 + 24), 1080 - w / 2 - 24)    # keep the pill inside the frame
            L = dict(L, x=x)
            k = max(1, int(round(L["k"] * 100)))
            pos = f"{BS}pos({L['x']:.0f},{L['y']:.0f}){BS}an5{BS}fscx{k}{BS}fscy{k}"
            ev.append(f"Dialogue: 2,{t0},{t1},Pill,,0,0,0,,{{{pos}{BS}1c{fill}{BS}p1}}{pill_path(w, h)}")
            ev.append(f"Dialogue: 3,{t0},{t1},Lab,,0,0,0,,{{{pos}{BS}1c{ink}}}{L['text']}")
    return ev


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("slug")
    ap.add_argument("--tag", default="v1")
    ap.add_argument("--video")
    ap.add_argument("--out")
    ap.add_argument("--title1", default="HOW A WELL GETS DUG")
    ap.add_argument("--title2", default="(AND STAYS CLEAN)")
    ap.add_argument("--title-secs", type=float, default=3.0)
    ap.add_argument("--audio", help="finished mix (wav) to use instead of the bare voice")
    ap.add_argument("--plain", action="store_true", help="plain white captions, no coloured key words")
    a = ap.parse_args()

    work = os.path.join(ROOT, "output", a.slug)
    video = a.video or os.path.join(work, "video.mp4")
    voice = os.path.join(ROOT, "sound", "voice", f"{a.slug}_{a.tag}.mp3")
    if a.audio:
        voice = os.path.abspath(a.audio)
    out = a.out or os.path.join(work, f"{a.slug}_draft.mp4")
    d = json.load(open(os.path.join(ROOT, "docs", "scenes", f"{a.slug}_words.json"), encoding="utf-8"))

    lines = []
    for s in d["sentences"]:
        ws = s["words"]
        for i, w in enumerate(ws):
            start = 0.0 if (s["i"] == 1 and i == 0) else w["start"]    # frame 0 (the cover) already has its word
            nxt = ws[i + 1]["start"] if i + 1 < len(ws) else s["end"] + 0.12
            end = nxt if i + 1 < len(ws) else max(nxt, start + 0.18)   # never overlap the next word
            txt = re.sub(r"[.,;:!?]", "", w["w"]).upper()
            if not txt:
                continue
            color = r"{\c&H004BF2B8&}" if (txt.lower() in KEY and not a.plain) else ""
            pop = r"{\fscx70\fscy70\t(0,110,\fscx100\fscy100)}" if start > 0 else "{}"
            lines.append(f"Dialogue: 0,{ts(start)},{ts(end)},Cap,,0,0,0,,{pop}{color}{txt}")

    t1, t2, ts_ = a.title1, a.title2, a.title_secs
    BS = chr(92)
    title = "Dialogue: 1,%s,%s,Title,,0,0,0,,%s" % (ts(0.0), ts(ts_), "{" + BS + "fad(0,350)}" + t1 + BS + "N{" + BS + "fs58}" + t2)
    ass = f"""[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 2

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Cap,Segoe UI Black,104,&H00FFFFFF,&H00FFFFFF,&H002A1B0D,&H80000000,0,0,0,0,100,100,1,0,1,9,3,5,40,40,0,1
Style: Pill,Arial,20,&H00FFFFFF,&H00FFFFFF,&H00000000,&H64000000,0,0,0,0,100,100,0,0,1,0,3,5,0,0,0,1
Style: Lab,Segoe UI Black,56,&H002A1B0D,&H00FFFFFF,&H00FFFFFF,&H00000000,0,0,0,0,100,100,1,0,1,0,0,5,0,0,0,1
Style: Title,Segoe UI Black,76,&H00FFF4DC,&H00FFFFFF,&H002A1B0D,&H80000000,0,0,0,0,100,100,1,0,1,8,3,8,40,40,230,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    # Cap style is centre-aligned (5); \pos puts it at 72% of the height
    body = []
    for ln in lines:
        body.append(ln.replace(",,0,0,0,,{", ",,0,0,0,,{\pos(540,1385)}{", 1))
    ass += "\n".join(body) + "\n" + (title + "\n" if t1 else "")
    NL = chr(10)
    ass += NL.join(label_events(os.path.join(work, "labels.json"))) + NL
    if a.plain:
        ass = ass.replace("Style: Cap,Segoe UI Black,104,", "Style: Cap,Segoe UI Black,86,").replace(",1,0,1,9,3,5,40,40,0,1", ",1,0,1,6,2,5,40,40,0,1")
    ass_path = os.path.join(work, "captions.ass")
    with open(ass_path, "w", encoding="utf-8") as f:
        f.write(ass)

    fonts = os.path.join(work, "fonts")
    os.makedirs(fonts, exist_ok=True)
    shutil.copyfile(r"C:\Windows\Fonts\seguibl.ttf", os.path.join(fonts, "seguibl.ttf"))
    cmd = ["ffmpeg", "-y", "-v", "error", "-i", os.path.relpath(video, work), "-i", os.path.relpath(voice, work),
           "-vf", "ass=captions.ass:fontsdir=fonts", "-af", "apad", "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-crf", "17",
           "-preset", "medium", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-shortest",
           "-movflags", "+faststart", os.path.relpath(out, work)]
    subprocess.run(cmd, check=True, cwd=work)
    print("WROTE", out)


if __name__ == "__main__":
    sys.exit(main())
