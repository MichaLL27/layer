"""Sound for the Pisa video: wind, SFX on every action + our tense music bed, under the voice.

  python tools/pisa_sound.py [--voice sound/voice/pisa_eleven.mp3] [--out output/pisa/mix.wav]

Times mirror PisaScene.cs (word lookups). Sources: CC0 samples of the 2D channel and synthesis.
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from island_sound import wind  # noqa  (also points well_sound at the moved samples folder)
from well_sound import ROOT, SR, Bus, bp, blip, db, env, load, lp, noise, rng, sample, save, stereo, whoosh  # noqa
from tunnel_sound import boom, music_tense  # noqa


def rumble(sec):
    n = int(sec * SR)
    m = lp(noise(sec), 90) * env(n, 0.3, 0.5)
    return m / (np.max(np.abs(m)) + 1e-9)


def clank():
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    f = float(rng.uniform(160, 260))
    m = (np.sin(2 * np.pi * f * t) + 0.6 * np.sin(2 * np.pi * f * 2.76 * t)) * np.exp(-t * 34) + 0.5 * lp(noise(0.16), 900) * np.exp(-t * 60)
    return m / (np.max(np.abs(m)) + 1e-9)


def hiss(sec):
    n = int(sec * SR)
    m = bp(noise(sec), 3000, 11000) * env(n, 0.25, 0.4)
    return m / (np.max(np.abs(m)) + 1e-9)


def drill(sec):
    n = int(sec * SR)
    t = np.arange(n) / SR
    wob = 1 + 0.02 * np.sin(2 * np.pi * 7 * t)
    m = sum(np.sin(2 * np.pi * 82 * k * t * wob) / k for k in range(1, 9)) + 0.5 * bp(noise(sec), 400, 2400)
    m = m * env(n, 0.15, 0.2)
    return m / (np.max(np.abs(m)) + 1e-9)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "pisa_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "pisa", "mix.wav"))
    a = ap.parse_args()
    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "pisa_words.json"), encoding="utf-8"))
    norm = lambda s: re.sub(r"[^a-z0-9']", "", s.lower())

    def tw(sent, word, occ=0, end=False):
        n = 0
        for w in d["sentences"][sent - 1]["words"]:
            if norm(w["w"]) == norm(word):
                if n == occ:
                    return w["end"] if end else w["start"]
                n += 1
        raise SystemExit(f"tw: {word} not in sentence {sent}")

    def st(i):
        return d["sentences"][i - 1]["start"]

    def en(i):
        return d["sentences"][i - 1]["end"]

    dur = d["duration"] + 1.0
    sfx = Bus(dur)
    sfx.add(0.0, stereo(wind(dur)), db(-24))

    # 1: a loud first frame, the tower groaning, stones coming down
    sfx.add(0.0, stereo(boom(1.3, 62, 34)), db(-8))
    sfx.add(0.0, stereo(rumble(st(2))), db(-11))
    for k in range(9):
        sfx.add(tw(1, "about") + k * 0.27 + rng.uniform(0, 0.1), sample("soft_thud_cc0.ogg"), db(-15 - (k % 3)))
    sfx.add(tw(1, "fall") - 0.1, stereo(whoosh(1.5, up=False)), db(-13))
    # 2: the slice, the weight, the foundation
    sfx.add(st(2) - 0.08, stereo(whoosh(0.9)), db(-12))
    sfx.add(tw(2, "fifteen"), stereo(boom(1.4, 66, 34)), db(-9))
    sfx.add(tw(2, "foundation"), stereo(whoosh(0.8, up=False)), db(-16))
    sfx.add(tw(2, "ten"), blip(420, 0.14, 0.9), db(-16))
    sfx.add(tw(2, "soft"), stereo(whoosh(0.8, up=False)), db(-16))
    # 3: eight hundred years of sinking
    sfx.add(st(3), stereo(rumble(en(3) - st(3) + 0.3)), db(-11))
    for k in range(12):
        sfx.add(st(3) + 0.3 + k * 0.22, blip(600 + 26 * k, 0.05, 0.8), db(-26))
    # 4: lead, block by block, then silence on "stops"
    a1, b1 = tw(4, "stack"), tw(4, "and") - 0.1
    for k in range(0, 48, 2):
        sfx.add(a1 + (b1 - a1) * k / 48 + 0.15, stereo(clank()), db(-15))
    sfx.add(tw(4, "stops"), blip(640, 0.18, 0.9), db(-14))
    # 5: cables going down
    sfx.add(tw(5, "ugly"), sample("soft_thud_cc0.ogg"), db(-14))
    sfx.add(tw(5, "cables"), stereo(whoosh(1.5, up=False)), db(-13))
    sfx.add(tw(5, "underground"), stereo(boom(0.9, 95, 55)), db(-13))
    # 6: liquid nitrogen
    sfx.add(tw(6, "freeze") - 0.1, stereo(hiss(en(6) - tw(6, "freeze") + 0.5)), db(-15))
    # 7: the lurch
    lurch = tw(7, "lurches")
    sfx.add(st(7) - 0.2, stereo(rumble(lurch - st(7) + 0.6)), db(-15))
    sfx.add(lurch, stereo(boom(1.9, 56, 28)), db(-6))
    sfx.add(lurch, stereo(lp(noise(0.5), 1400) * env(int(0.5 * SR), 0.005, 0.4)), db(-11))
    # 8: lead, fast
    a2, b2 = tw(8, "pile"), tw(8, "fast", 0, True)
    for k in range(24):
        sfx.add(a2 + (b2 - a2) * k / 24 + 0.1, stereo(clank()), db(-13))
    # 9: the idea
    sfx.add(tw(9, "opposite"), stereo(whoosh(0.9)), db(-15))
    sfx.add(tw(9, "opposite"), blip(520, 0.18, 0.9), db(-17))
    # 10: across to the high side
    sfx.add(tw(10, "dig") - 0.2, stereo(whoosh(0.9, up=False)), db(-14))
    # 11: the drill
    da, db_ = tw(11, "slides"), en(11) + 0.2
    sfx.add(da, stereo(drill(db_ - da)), db(-16))
    # 12: the hole closing, the tower moving
    sfx.add(tw(12, "hole"), sample("soft_thud_cc0.ogg"), db(-11))
    sfx.add(tw(12, "high"), stereo(rumble(en(12) - tw(12, "high") + 0.3)), db(-11))
    sfx.add(tw(12, "whole") - 0.1, stereo(whoosh(1.4)), db(-14))
    # 13: the dirt, the eighteen inches
    sfx.add(tw(13, "Seventy"), stereo(boom(1.1, 80, 42)), db(-11))
    sfx.add(tw(13, "eighteen"), sample("glass_ping_3_cc0.ogg"), db(-13))
    # 14: the last shot
    sfx.add(st(14) - 0.08, stereo(whoosh(1.2)), db(-15))
    sfx.add(tw(14, "two"), stereo(boom(2.0, 55, 30)), db(-11))
    sfx.add(tw(14, "two") + 0.05, sample("glass_ping_3_cc0.ogg"), db(-14))

    voice = load(a.voice)
    vbus = np.zeros_like(sfx.x)
    vbus[: len(voice)] += voice[: len(vbus)]
    vbus *= 0.9 / (np.max(np.abs(vbus)) + 1e-9)
    venv = lp(np.abs(vbus.mean(axis=1)), 6)
    duckv = np.clip(venv / (np.percentile(venv, 90) + 1e-9), 0, 1)
    mus = music_tense(dur)
    mbus = np.zeros_like(sfx.x)
    mbus[: len(mus)] = mus[: len(mbus)]
    mbus *= (1.0 - 0.5 * duckv)[:, None]
    sfx.x *= (1.0 - 0.5 * duckv)[:, None]
    mix = vbus + sfx.x * 0.9 + mbus * db(-21)
    n = int(dur * SR)
    mix = mix[:n]
    mix[-int(0.25 * SR):] *= np.linspace(1, 0, int(0.25 * SR))[:, None]
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    save(a.out + ".pre.wav", mix * 0.95 / (np.max(np.abs(mix)) + 1e-9))
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.out + ".pre.wav", "-af", "loudnorm=I=-14:TP=-1.5:LRA=11",
                    "-ar", str(SR), "-c:a", "pcm_s24le", a.out], check=True)
    os.remove(a.out + ".pre.wav")
    print("WROTE", a.out, f"{n / SR:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
