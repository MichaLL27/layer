"""Sound for the island video: surf and wind, SFX on every action + our tense music bed, under the voice.

  python tools/island_sound.py [--voice sound/voice/island_v1.mp3] [--out output/island/mix.wav]

Times mirror IslandScene.cs (word lookups). Sources: CC0 samples of the 2D channel (only *_cc0*) and
synthesis (helpers of tools/well_sound.py and tools/tunnel_sound.py).
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from well_sound import ROOT, SR, Bus, bp, blip, db, env, load, lp, noise, rain, rng, sample, save, stereo, trickle, whoosh  # noqa
from tunnel_sound import boom, music_tense  # noqa

# the 2D channel's folder moved into the hub: look there when its old place is gone
import well_sound  # noqa
if not os.path.isdir(well_sound.SFX2D):
    well_sound.SFX2D = os.path.abspath(os.path.join(ROOT, "..", "youtube-hub", "satisfying-2d-sims", "sfx"))


def surf(sec):
    """waves on sand: broad noise that swells and falls about every 6 s"""
    n = int(sec * SR)
    t = np.arange(n) / SR
    body = bp(noise(sec), 180, 3200)
    swell = 0.35 + 0.65 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 5.9 - 1.2)) ** 2
    m = body * swell
    return m / (np.max(np.abs(m)) + 1e-9)


def shovel():
    """one handful of sand"""
    n = int(0.2 * SR)
    m = bp(noise(0.2), 300, 2600) * env(n, 0.02, 0.12)
    return m / (np.max(np.abs(m)) + 1e-9)


def wind(sec):
    n = int(sec * SR)
    t = np.arange(n) / SR
    m = bp(noise(sec), 250, 900) * (0.6 + 0.4 * np.sin(2 * np.pi * t / 3.7 + 0.5))
    return m / (np.max(np.abs(m)) + 1e-9)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "island_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "island", "mix.wav"))
    a = ap.parse_args()
    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "island_words.json"), encoding="utf-8"))
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

    dur = d["duration"] + 1.0
    sfx = Bus(dur)
    sfx.add(0.0, stereo(surf(dur)), db(-17))
    sfx.add(0.0, stereo(wind(dur)), db(-29))

    # 1: a loud first frame, a splash of water out of the cupped hands
    sfx.add(0.0, stereo(boom(1.3, 62, 34)), db(-8))
    sfx.add(0.1, stereo(trickle(0.9, 1.1)), db(-16))
    # 2: the camera tears away from the beach
    sfx.add(st(2) - 0.05, stereo(whoosh(2.0)), db(-13))
    sfx.add(tw(2, "three"), stereo(boom(1.2, 70, 42)), db(-11))
    # 3: digging, then the water seeping in
    dig1, fill1 = tw(3, "dig"), tw(3, "fills")
    n = int((fill1 - dig1) / 0.24)
    for k in range(max(n, 1)):
        sfx.add(dig1 + k * 0.24, stereo(shovel()), db(-13))
    sfx.add(fill1 - 0.05, stereo(trickle(1.4, 1.0)), db(-14))
    # 4: the bad news, the sea pushing in
    sfx.add(tw(4, "salt"), stereo(boom(0.9, 110, 60)), db(-10))
    sfx.add(tw(4, "sea"), stereo(whoosh(1.5, up=False)), db(-17))
    # 5: footsteps in sand, digging again
    w0, w1 = st(5) + 0.05, tw(5, "dig") - 0.05
    k, t0 = 0, w0
    while t0 < w1:
        sfx.add(t0, stereo(lp(noise(0.13), 700) * env(int(0.13 * SR), 0.01, 0.1) * 2), db(-15))
        t0 += (w1 - w0) / 11
    dig2, fill2 = tw(5, "dig"), st(6)
    for k in range(max(int((fill2 - dig2) / 0.24), 1)):
        sfx.add(dig2 + k * 0.24, stereo(shovel()), db(-13))
    # 6: clear water, a bright ping on "fresh"
    sfx.add(fill2, stereo(trickle(1.5, 1.4)), db(-13))
    sfx.add(tw(6, "fresh"), sample("glass_ping_3_cc0.ogg"), db(-13))
    # 7: rain
    ra, rb = tw(7, "rain"), tw(7, "floats", 0, True) + 0.7
    rr = rain(rb - ra + 0.4)
    sfx.add(ra - 0.2, stereo(rr * env(len(rr), 0.3, 0.6)), db(-15))
    # 8: the reveal
    sfx.add(st(8), stereo(boom(1.8, 58, 30)), db(-10))
    for k in range(10):
        sfx.add(st(8) + 0.2 + k * 0.42 + rng.uniform(0, 0.15), blip(rng.uniform(220, 520), 0.11, 0.8), db(-26))

    # 9: one foot up, then the long drop
    sfx.add(tw(9, "foot"), blip(420, 0.14, 0.9), db(-16))
    sfx.add(tw(9, "pushes") - 0.1, stereo(whoosh(1.1, up=False)), db(-13))
    sfx.add(tw(9, "forty"), stereo(boom(1.5, 66, 32)), db(-9))
    # 10: digging down through the thin layer, then the salt breaks in
    d0, d1 = tw(10, "Dig"), tw(10, "punch", 0, True) + 0.1
    for k in range(6):
        sfx.add(d0 + (d1 - d0) * k / 6, stereo(shovel()), db(-12))
    sfx.add(tw(10, "punch"), stereo(boom(0.8, 95, 50)), db(-10))
    sfx.add(tw(10, "punch") + 0.05, stereo(trickle(1.2, 0.8)), db(-13))
    # 11: stop; the logs go in, one by one
    sfx.add(tw(11, "stop"), blip(640, 0.16, 0.9), db(-15))
    for k in range(11):
        sfx.add(tw(11, "line") + k * 0.15 + 0.3, sample("soft_thud_cc0.ogg"), db(-13 - (k % 3)))
    # 12: a slow scoop; then the salt coming up
    sfx.add(st(12) + 0.2, stereo(trickle(0.8, 1.1)), db(-16))
    sfx.add(tw(12, "take") + 0.02, stereo(whoosh(1.7)), db(-14))
    sfx.add(tw(12, "meet"), stereo(boom(1.4, 72, 36)), db(-9))
    # 13: the fire, then the village
    ppl = tw(13, "People")
    for k in range(int((ppl - st(13)) / 0.07)):
        t0 = st(13) + k * 0.07 + rng.uniform(0, 0.05)
        sfx.add(t0, stereo(bp(noise(0.03), 800, 5000) * env(int(0.03 * SR), 0.003, 0.02) * float(rng.uniform(0.2, 1.0))), db(-20))
    sfx.add(st(13), stereo(lp(noise(ppl - st(13)), 450) * env(int((ppl - st(13)) * SR), 0.1, 0.25)), db(-23))
    sfx.add(tw(13, "weeks"), sample("glass_ping_3_cc0.ogg"), db(-14))
    sfx.add(ppl - 0.05, stereo(whoosh(1.2, up=True)), db(-15))
    sfx.add(tw(13, "thousands"), stereo(boom(2.0, 55, 30)), db(-10))

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
