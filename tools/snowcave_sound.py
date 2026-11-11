"""Sound for the snow cave video: a storm that goes quiet inside, SFX on every action, our tense music bed, under the voice.

  python tools/snowcave_sound.py [--voice sound/voice/snowcave_eleven.mp3] [--out output/snowcave/mix.wav]

Times mirror SnowScene.cs (word lookups). Sources: CC0 samples of the 2D channel and synthesis.
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from island_sound import wind, shovel  # noqa  (also points well_sound at the moved samples folder)
from well_sound import ROOT, SR, Bus, bp, blip, db, env, load, lp, noise, rng, sample, save, stereo, trickle, whoosh  # noqa
from tunnel_sound import boom, music_tense  # noqa


def storm(sec):
    n = int(sec * SR)
    t = np.arange(n) / SR
    m = bp(noise(sec), 300, 4200) * (0.55 + 0.45 * np.sin(2 * np.pi * t / 2.3 + 0.7) ** 2)
    return m / (np.max(np.abs(m)) + 1e-9)


def drip():
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    f = float(rng.uniform(900, 1500))
    m = np.sin(2 * np.pi * (f + 1500 * t) * t) * np.exp(-t * 55)
    return m / (np.max(np.abs(m)) + 1e-9)


def breath(sec):
    n = int(sec * SR)
    m = bp(noise(sec), 500, 2600) * env(n, sec * 0.35, sec * 0.5)
    return m / (np.max(np.abs(m)) + 1e-9)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "snowcave_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "snowcave", "mix.wav"))
    a = ap.parse_args()
    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "snowcave_words.json"), encoding="utf-8"))
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

    # the storm: loud outside, muffled once you are in, gone at dawn
    s = storm(dur)
    n = len(s)
    level = np.ones(n)
    tt = np.arange(n) / SR
    inside = st(3) + 2.2
    level = np.where(tt < inside, 1.0, 0.16)
    k = np.clip((tt - inside) / 0.7, 0, 1)
    level = 1.0 + (0.16 - 1.0) * k
    level *= np.clip(1 - (tt - st(13) + 0.1) / 0.4, 0, 1)
    sfx.add(0.0, stereo(s * level), db(-13))
    sfx.add(0.0, stereo(wind(dur)), db(-25))

    # 1-2: a loud first frame, gusts
    sfx.add(0.0, stereo(boom(1.3, 62, 34)), db(-8))
    sfx.add(0.05, stereo(whoosh(1.6)), db(-11))
    sfx.add(tw(1, "kill"), stereo(boom(1.1, 70, 40)), db(-10))
    sfx.add(tw(2, "wind") - 0.15, stereo(whoosh(1.4, up=False)), db(-11))
    # 3: digging
    a0, a1 = tw(3, "dig"), tw(3, "get") - 0.05
    for j in range(max(int((a1 - a0) / 0.2), 1)):
        sfx.add(a0 + j * 0.2, stereo(shovel()), db(-12))
    # 4: the cold pouring in
    sfx.add(tw(4, "cold"), stereo(whoosh(2.0, up=False)), db(-13))
    # 5: digging again, the bed
    a0, a1 = tw(5, "dig"), tw(5, "high", 0, True)
    for j in range(max(int((a1 - a0) / 0.2), 1)):
        sfx.add(a0 + j * 0.2, stereo(shovel()), db(-13))
    sfx.add(tw(5, "high"), blip(520, 0.16, 0.9), db(-17))
    # 6: heavy air
    sfx.add(tw(6, "sinks"), stereo(boom(1.3, 74, 38)), db(-11))
    sfx.add(tw(6, "sinks"), stereo(whoosh(1.3, up=False)), db(-15))
    # 7: drips
    t0 = tw(7, "melts")
    while t0 < tw(8, "carve"):
        sfx.add(t0, stereo(drip()), db(-16 - float(rng.uniform(0, 5))))
        t0 += float(rng.uniform(0.09, 0.26))
    # 8: carving, the water running away
    a0, a1 = tw(8, "carve"), tw(8, "dome", 0, True)
    for j in range(max(int((a1 - a0) / 0.17), 1)):
        sfx.add(a0 + j * 0.17, stereo(shovel()), db(-15))
    sfx.add(tw(8, "water"), stereo(trickle(1.6, 1.2)), db(-15))
    # 9-10: breathing, the bad air
    t0 = st(9) + 0.2
    while t0 < st(11):
        sfx.add(t0, stereo(breath(1.3)), db(-17))
        t0 += 1.9
    sfx.add(tw(10, "carbon"), stereo(boom(1.7, 58, 30)), db(-9))
    # 11: the pole going through
    sfx.add(tw(11, "poke"), stereo(shovel()), db(-10))
    sfx.add(tw(11, "hole"), sample("soft_thud_cc0.ogg"), db(-12))
    sfx.add(tw(11, "hole", 0, True), stereo(whoosh(1.5)), db(-14))
    # 12: the two numbers
    sfx.add(st(12), stereo(boom(1.1, 80, 42)), db(-12))
    sfx.add(tw(12, "In"), sample("glass_ping_3_cc0.ogg"), db(-13))
    # 13: morning
    sfx.add(st(13) - 0.08, stereo(whoosh(1.3)), db(-15))
    sfx.add(tw(13, "survived"), stereo(boom(2.0, 55, 30)), db(-12))
    sfx.add(tw(13, "survived") + 0.05, sample("glass_ping_3_cc0.ogg"), db(-14))

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
