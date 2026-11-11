"""Sound for the car video (Short 03): SFX on every action + our tense music bed, under the voice.

  python tools/car_sound.py [--out output/car/mix.wav]

Times mirror CarScene.cs (word lookups). Sources: CC0 samples of the 2D channel (only *_cc0*) and
synthesis (helpers of tools/well_sound.py and tools/tunnel_sound.py). Under water everything is low-passed.
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from well_sound import ROOT, SR, Bus, blip, bp, db, engine, env, lp, load, noise, rng, sample, save, stereo, trickle, whoosh  # noqa
from tunnel_sound import boom, creak, hiss, music_tense, roar, scrape, squeak  # noqa


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "car_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "car", "mix.wav"))
    a = ap.parse_args()
    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "car_words.json"), encoding="utf-8"))
    norm = lambda s: re.sub(r"[^a-z0-9']", "", s.lower())

    def tw(sent, word, occ=0, end=False):
        n = 0
        for w in d["sentences"][sent - 1]["words"]:
            if norm(w["w"]) == norm(word):
                if n == occ:
                    return w["end"] if end else w["start"]
                n += 1
        raise SystemExit(f"tw: {word} not in sentence {sent}")

    dur = d["duration"] + 1.0
    sfx = Bus(dur)
    under = Bus(dur)                 # these get the underwater low-pass
    hit, land = tw(1, "bridge") - 0.1, tw(1, "river") + 0.05

    # 1: engine and tyres, the rail, the fall, the splash
    eng = engine(hit + 0.2, np.full(30, 0.8), np.full(30, 0.7))
    sfx.add(0.0, stereo(eng * env(len(eng), 0.05, 0.1)), db(-16))
    sfx.add(hit - 0.45, stereo(bp(noise(0.5), 700, 3500) * env(int(0.5 * SR), 0.05, 0.1)), db(-18))   # tyre squeal
    sfx.add(hit, sample("metal_clink_cc0.ogg"), db(-6))
    sfx.add(hit, stereo(boom(0.9, 90, 50)), db(-10))
    sfx.add(hit + 0.05, sample("press_slam_crunch_1_cc0.ogg"), db(-10))
    sfx.add(hit + 0.1, stereo(whoosh(land - hit)), db(-18))
    sfx.add(land, stereo(boom(1.8, 60, 30)), db(-5))
    r = roar(1.6)
    sfx.add(land, stereo(r * env(len(r), 0.0, 1.0)), db(-9))
    sfx.add(land + 0.2, stereo(trickle(2.0, 0.8)), db(-17))
    sfx.add(land + 0.4, stereo(creak(tw(2, "Your") - land)), db(-24))

    # inside the floating car: muffled water against the doors, creaks, the phone
    sfx.add(tw(2, "Your"), stereo(lp(noise(tw(8, "pours") - tw(2, "Your")), 300) * 0.5), db(-30))
    for t0 in (tw(2, "thought"), tw(3, "holds"), tw(5, "power"), tw(7, "Never")):
        sfx.add(t0, stereo(creak(1.2)), db(-26))
    sfx.add(tw(2, "leave") + 0.3, sample("chip_click_cc0.ogg"), db(-16))   # the phone lands on the seat
    # 3: the seatbelt clicks open and runs back
    sfx.add(tw(3, "off") - 0.05, sample("metal_clink_cc0.ogg"), db(-14))
    sfx.add(tw(3, "off"), stereo(scrape(0.5, 18)), db(-24))
    # 4: the door will not move
    for k in range(3):
        sfx.add(tw(4, "push") + k * 0.35, sample("soft_thud_cc0.ogg"), db(-12))
    sfx.add(tw(4, "river"), stereo(creak(1.6)), db(-16))
    # 5-6: the window motor, then it dies; the breaker, the glass
    sfx.add(tw(5, "open"), stereo(squeak(1.2) * 0.6), db(-22))
    sfx.add(tw(6, "button"), stereo(hiss(0.25, 300, 900)), db(-24))
    sfx.add(tw(6, "breaker") - 0.05, sample("metal_clink_cc0.ogg"), db(-10))
    sfx.add(tw(6, "breaker"), sample("glass_shatter_cc0.ogg"), db(-5))
    # 7: the windshield takes the hit and holds
    sfx.add(tw(7, "laminated") - 0.1, sample("soft_thud_cc0.ogg"), db(-8))
    sfx.add(tw(7, "laminated") - 0.08, sample("thin_snap_2_cc0.wav"), db(-14))
    # 8: the river pours in, the car tips
    pours = tw(8, "pours")
    rr = roar(tw(9, "Kids") - pours + 0.5)
    sfx.add(pours - 0.1, stereo(rr * env(len(rr), 0.2, 0.6)), db(-12))
    sfx.add(tw(8, "tips"), stereo(creak(1.6)), db(-12))
    for k in range(18):
        sfx.add(pours + 0.3 + k * 0.45 + rng.uniform(0, 0.2), blip(rng.uniform(120, 300), 0.12, 0.9), db(-18))

    # under water from "Kids" on: muffled, bubbles, the door, the swim
    for k in range(40):
        under.add(tw(9, "Kids") + k * 0.3 + rng.uniform(0, 0.2), blip(rng.uniform(150, 500), 0.1, 0.8), db(-16))
    under.add(tw(10, "door") - 0.1, stereo(creak(1.2)), db(-8))
    under.add(tw(10, "opens") - 0.1, sample("soft_thud_cc0.ogg"), db(-8))
    for k in range(6):
        under.add(tw(11, "push") + k * 0.4, stereo(lp(noise(0.35), 400) * env(int(0.35 * SR), 0.05, 0.2) * 2), db(-14))
    under.x[:, 0] = lp(under.x[:, 0], 650); under.x[:, 1] = lp(under.x[:, 1], 650)

    # 12: breaking the surface, air, the river again
    up = tw(12, "Congratulations") + 0.3
    sfx.add(up - 0.1, stereo(trickle(1.2, 1.2)), db(-12))
    sfx.add(up, stereo(bp(noise(0.6), 300, 3000) * env(int(0.6 * SR), 0.02, 0.4)), db(-16))      # the gasp of air
    sfx.add(up + 0.4, stereo(trickle(dur - up - 0.4, 0.6) * 0.6), db(-22))
    sfx.x += under.x

    # music: from the first frame, ducked under the voice; effects step down under the voice too
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
    sfx.x *= (1.0 - 0.55 * duckv)[:, None]
    mix = vbus + sfx.x * 0.9 + mbus * db(-20)
    n = int(dur * SR)
    mix = mix[:n]
    mix[-int(0.25 * SR):] *= np.linspace(1, 0, int(0.25 * SR))[:, None]
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    save(a.out + ".pre.wav", mix * 0.95 / (np.max(np.abs(mix)) + 1e-9))
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.out + ".pre.wav", "-af", "loudnorm=I=-14:TP=-1.5:LRA=11",
                    "-ar", str(SR), "-c:a", "pcm_s24le", a.out], check=True)
    os.remove(a.out + ".pre.wav")
    stems = os.path.join(os.path.dirname(a.out), "stems")
    os.makedirs(stems, exist_ok=True)
    save(os.path.join(stems, "sfx.wav"), (sfx.x * 0.9)[:n])
    save(os.path.join(stems, "music.wav"), (mbus * db(-20))[:n])
    print("WROTE", a.out, f"{n / SR:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
