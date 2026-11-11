"""Sound for the tunnel video: SFX on every action + our own tense music bed, under the voice.

  python tools/tunnel_sound.py [--end 37.2] [--out output/tunnel/mix.wav]

The very first frame is loud on purpose (stay-to-watch: docs/scenes/tunnel.md). Times mirror
TunnelScene.cs (word lookups, SegTime, Lunge0, Again0); --voice picks the voice file and output/tunnel/labels.json.
Sources: CC0 samples of the 2D channel (only *_cc0*) and synthesis (tools/well_sound.py's helpers).
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from well_sound import (ROOT, SR, Bus, blip, bp, db, engine, env, grind, hp, hum, load, lp, noise, rain, rng, sample,  # noqa
                        save, slurry, stereo, trickle, whoosh)


def roar(sec):
    """a river breaking in: broad, rolling water noise"""
    m = bp(noise(sec), 120, 5000) * 0.8 + lp(noise(sec), 220) * 1.2
    am = 0.75 + 0.25 * np.sin(2 * np.pi * 7.3 * np.arange(len(m)) / SR) * lp(np.abs(noise(sec)), 5) * 4
    m = m * am
    return m / (np.max(np.abs(m)) + 1e-9)


def boom(sec=1.6, f0=70.0, f1=32.0):
    """a heavy hit: falling sub + noise burst"""
    n = int(sec * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t * 7)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 3.2)
    b = lp(noise(sec), 900) * np.exp(-t * 16) * 0.7
    return s + b


def tape_stop(sec=0.7):
    """time snapping backwards: rising sweep with tremble"""
    w = whoosh(sec, up=True)
    t = np.arange(len(w)) / SR
    return w * (0.8 + 0.2 * np.sin(2 * np.pi * 30 * t))


def creak(sec):
    """the weight of the river: a slow low groan"""
    n = int(sec * SR)
    t = np.arange(n) / SR
    ph = 2 * np.pi * np.cumsum(38 + 6 * np.sin(2 * np.pi * 0.7 * t) + 3 * lp(noise(sec), 3) * 8) / SR
    m = sum(np.sin(k * ph) / k for k in range(1, 7))
    return m / (np.max(np.abs(m)) + 1e-9) * env(n, 0.5, 0.6)


def hiss(sec, lo=1800, hi=9000):
    return bp(noise(sec), lo, hi) * env(int(sec * SR), 0.03, 0.15) / 0.6


def scrape(sec, rate=4.3):
    n = int(sec * SR)
    t = np.arange(n) / SR
    m = bp(noise(sec), 180, 1500) * (0.45 + 0.55 * (0.5 + 0.5 * np.sin(2 * np.pi * rate * t)) ** 2)
    return m / (np.max(np.abs(m)) + 1e-9) * env(n, 0.2, 0.3)


def squeak(sec=0.5):
    n = int(sec * SR)
    t = np.arange(n) / SR
    f = 420 + 340 * t / sec + 60 * np.sin(2 * np.pi * 21 * t)
    m = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.4 * np.sin(4 * np.pi * np.cumsum(f) / SR)
    return m * env(n, 0.04, 0.12) * 0.6


def music_tense(sec, bpm=104):
    """our own bed: pulsing bass, dark pad, a repeated rising figure in D minor, builds over time"""
    n = int(sec * SR)
    beat = 60 / bpm
    bar = 4 * beat
    hz = lambda m: 440 * 2 ** ((m - 69) / 12)
    prog = [[50, 53, 57], [46, 50, 53], [48, 52, 55], [45, 49, 52]]   # Dm, Bb, C, A
    pad, bass, pl = np.zeros(n), np.zeros(n), np.zeros(n)
    for b in range(int(np.ceil(sec / bar))):
        ch = prog[b % 4]
        i0, i1 = int(b * bar * SR), min(n, int((b + 1) * bar * SR) + int(0.2 * SR))
        tt = np.arange(i1 - i0) / SR
        e = np.minimum(1, tt / 0.3) * np.minimum(1, (len(tt) / SR - tt) / 0.3)
        for m in ch:
            for det in (-0.1, 0.1):
                pad[i0:i1] += np.sin(2 * np.pi * hz(m) * (1 + det / 100) * tt) * e * 0.1
        for k in range(16):
            j0 = int((b * bar + k * beat / 4) * SR)
            if j0 >= n:
                break
            j1 = min(n, j0 + int(0.16 * SR))
            tk = np.arange(j1 - j0) / SR
            bass[j0:j1] += np.sin(2 * np.pi * hz(ch[0] - 12) * tk) * np.exp(-tk * 15) * (1.0 if k % 4 == 0 else 0.6)
        notes = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 24]
        for k in range(8):
            j0 = int((b * bar + k * beat / 2) * SR)
            if j0 >= n:
                break
            j1 = min(n, j0 + int(0.5 * SR))
            tk = np.arange(j1 - j0) / SR
            f = hz(notes[k % 4])
            pl[j0:j1] += (np.sin(2 * np.pi * f * tk) + 0.3 * np.sin(4 * np.pi * f * tk)) * np.exp(-tk * 9) * 0.1
    grow = np.linspace(0.65, 1.0, n)
    m = (lp(pad, 1100) + bass * 0.55 + pl) * grow
    m = m / (np.max(np.abs(m)) + 1e-9)
    d = int(0.011 * SR)
    return np.stack([m, np.concatenate([np.zeros(d), m[:-d]])], axis=1) * env(n, 0.05, 1.5)[:, None]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "tunnel_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "tunnel", "mix.wav"))
    ap.add_argument("--end", type=float, help="cut the mix here (test clip)")
    a = ap.parse_args()

    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "tunnel_words.json"), encoding="utf-8"))
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
    chips = [f"pebble_click_{i}_cc0.ogg" for i in range(1, 9)]

    # --- the finished tunnel, first and last picture: a train passing under the river
    def train_pass(t0, sec):
        m = lp(noise(sec), 260) * 1.2 + bp(noise(sec), 800, 3000) * 0.12
        sfx.add(t0, stereo(m / (np.max(np.abs(m)) + 1e-9) * env(len(m), 0.25, 0.4)), db(-19))
        for tc in np.arange(t0 + 0.2, t0 + sec - 0.2, 0.42):
            sfx.add(tc, sample("metal_clink_cc0.ogg"), db(-34))

    train_pass(0.0, tw(2, "If"))
    train_pass(tw(13, "Congratulations"), dur - tw(13, "Congratulations"))

    # --- 2: drips and a dull lamp hum, the roof gives way, the river breaks in
    so = tw(3, "So")
    roof, gives = tw(2, "roof"), tw(2, "gives")
    for t0 in np.arange(tw(2, "If"), roof, 0.55):
        sfx.add(t0, blip(rng.uniform(500, 900), 0.05, 0.8), db(-22))
    sfx.add(roof - 0.1, stereo(creak(gives - roof + 0.5)), db(-13))
    sfx.add(gives - 0.05, stereo(boom(1.8)), db(-5))
    r = roar(so - gives + 0.2)
    sfx.add(gives, stereo(r * env(len(r), 0.1, 0.25)), db(-11))
    for t0 in np.arange(gives, so, 0.32):
        sfx.add(t0 + rng.uniform(0, 0.1), sample("soft_thud_cc0.ogg"), db(-13))
    sfx.add(so - 0.1, stereo(tape_stop(0.75)), db(-9))

    # --- 2: the steel shield slides in
    steel = tw(3, "steel")
    sfx.add(steel - 0.05, stereo(scrape(tw(3, "up") - steel + 0.2, 2.2)), db(-16))
    sfx.add(tw(3, "up") + 0.05, stereo(boom(0.9, 70, 40)), db(-13))
    sfx.add(tw(3, "up") + 0.05, sample("metal_clink_cc0.ogg"), db(-20))

    # --- 3: a spade, then the wheel swings in, bolts on and starts
    but3, bolt = tw(4, "But"), tw(4, "bolt")
    for t0 in np.arange(but3, bolt - 0.15, 0.46):
        sfx.add(t0, sample(chips[rng.integers(0, len(chips))]), db(-16))
        sfx.add(t0 + 0.02, sample("soft_thud_cc0.ogg"), db(-22))
    sfx.add(bolt - 0.2, stereo(whoosh(tw(4, "wheel") - bolt + 0.2)), db(-13))
    sfx.add(tw(4, "wheel"), stereo(boom(1.3, 85, 36)), db(-9))
    sfx.add(tw(4, "wheel"), sample("metal_clink_cc0.ogg"), db(-13))

    # --- the machine runs: engine from the wheel on, grinding whenever the wheel turns
    spin = tw(4, "spinning")
    L = tw(13, "Congratulations") - spin
    eng = engine(L, np.full(50, 0.7), np.full(50, 0.6))
    sfx.add(spin, stereo(eng * env(len(eng), 0.4, 1.0)), db(-28))
    stop = tw(8, "nowhere")
    g = grind(stop - spin)
    sfx.add(spin + 0.2, stereo(g), db(-23))
    for t0 in np.arange(spin + 0.2, stop, 0.08):
        sfx.add(t0 + rng.uniform(0, 0.05), sample(chips[rng.integers(0, len(chips))]), db(-30 + rng.uniform(-4, 2)))

    # --- 5: mud, then the dirt and the squeeze
    mud = tw(6, "pushes")
    sfx.add(mud - 0.1, stereo(slurry(tw(6, "pack") - mud + 0.2)), db(-13))
    sfx.add(tw(6, "pack"), stereo(whoosh(0.5, up=False)), db(-20))
    sfx.add(tw(6, "dirt"), sample("soft_thud_cc0.ogg"), db(-13))
    sq = tw(6, "squeeze")
    sfx.add(sq - 0.05, stereo(boom(0.9, 60, 40)), db(-12))
    sfx.add(sq, stereo(hiss(1.0, 500, 3500)), db(-24))

    # --- 6: the weight of the river, held
    sfx.add(tw(7, "Now") - 0.2, stereo(creak(tw(8, "But") - tw(7, "Now") + 0.3)), db(-17))

    # --- 7: the wheel jams, the screw slides in, everything starts again
    sfx.add(stop, stereo(creak(0.9)), db(-14))
    run = tw(8, "run")
    sfx.add(run - 0.1, stereo(scrape(tw(8, "wall", 0, True) - run + 0.1, 3.0)), db(-17))
    sfx.add(tw(8, "wall"), sample("metal_clink_cc0.ogg"), db(-17))
    sfx.add(tw(8, "wall"), stereo(boom(0.7, 70, 45)), db(-16))
    sfx.add(tw(8, "wall", 0, True), stereo(grind(tw(9, "robot") - tw(8, "wall", 0, True))), db(-24))
    sfx.add(tw(8, "wall", 0, True), stereo(scrape(tw(9, "robot") - tw(8, "wall", 0, True))), db(-23))

    # --- 8: the bare tunnel falls, the arm sets four segments, rings run down the tunnel
    hole, robot, rings = tw(9, "hole"), tw(9, "robot"), tw(9, "rings")
    sfx.add(hole - 0.1, stereo(lp(noise(robot - hole + 0.6), 300) * 1.4 * env(int((robot - hole + 0.6) * SR), 0.1, 0.4)), db(-14))
    for t0 in np.arange(hole, robot + 0.4, 0.17):
        sfx.add(t0 + rng.uniform(0, 0.08), sample("soft_thud_cc0.ogg"), db(-16 + rng.uniform(-4, 2)))
    ra, rb = robot - 0.05, rings + 0.1
    seg = (rb - ra) / 4
    for k in range(4):
        s0 = ra + k * seg
        sfx.add(s0, stereo(hiss(seg * 0.55)), db(-28))
        sfx.add(s0 + seg * 0.78, sample("soft_thud_cc0.ogg"), db(-11))
        sfx.add(s0 + seg * 0.79, sample("metal_clink_cc0.ogg"), db(-22))
    for k in range(14):
        sfx.add(rings + 0.1 + k * 0.055, sample("soft_thud_cc0.ogg"), db(-13 - k * 0.8))

    # --- 9: water through the joint, then the rubber closes it
    sfx.add(tw(10, "water") - 0.1, stereo(trickle(tw(10, "rubber") - tw(10, "water") + 0.2, 1.2)), db(-19))
    sfx.add(tw(10, "rubber") - 0.05, stereo(squeak(0.55)), db(-19))
    sfx.add(tw(10, "seal", 0, True), sample("glass_ping_3_cc0.ogg"), db(-15))

    # --- 10-11: the rams reach out, the shove, ring after ring
    sfx.add(tw(11, "forward"), stereo(hiss(0.9, 700, 5000)), db(-24))
    lunge = tw(12, "shove") - 0.05
    sfx.add(lunge - 0.05, stereo(whoosh(0.4)), db(-19))
    sfx.add(lunge + 0.32, stereo(boom(1.3, 75, 34)), db(-8))
    sfx.add(lunge + 0.32, sample("soft_thud_cc0.ogg"), db(-10))
    ag = tw(12, "built", 0, True) + 0.02
    for i in range(3):
        sfx.add(ag + i * 0.42 + 0.36, stereo(boom(0.7, 70, 40)), db(-15))
        sfx.add(ag + i * 0.42, stereo(hiss(0.25, 900, 5000)), db(-27))

    # --- 12: quiet river over the finished tunnel
    sfx.add(tw(13, "You"), stereo(trickle(dur - tw(13, "You"), 0.5) * 0.6), db(-24))

    # --- label clicks
    lj = os.path.join(ROOT, "output", "tunnel", "labels.json")
    if os.path.exists(lj):
        LB = json.load(open(lj, encoding="utf-8"))
        seen = set()
        for f, labs in enumerate(LB["frames"]):
            for lab in labs:
                if lab["text"] not in seen:
                    seen.add(lab["text"])
                    if f > 0:
                        sfx.add(f / LB["fps"], sample("chip_click_cc0.ogg"), db(-19))

    # --- mix: music starts on the question, ducked under the voice
    voice = load(a.voice)
    vbus = np.zeros_like(sfx.x)
    vbus[: len(voice)] += voice[: len(vbus)]
    vbus *= 0.9 / (np.max(np.abs(vbus)) + 1e-9)
    venv = lp(np.abs(vbus.mean(axis=1)), 6)
    duck = 1.0 - 0.5 * np.clip(venv / (np.percentile(venv, 90) + 1e-9), 0, 1)
    mus = music_tense(dur)
    mbus = np.zeros_like(sfx.x)
    i0 = 0
    mbus[i0:i0 + len(mus)] = mus[: len(mbus) - i0]
    mbus *= duck[:, None]
    # every word must be understood: effects step down under the voice too (Lazar 2026-11-05)
    sduck = 1.0 - 0.55 * np.clip(venv / (np.percentile(venv, 90) + 1e-9), 0, 1)
    sfx.x *= sduck[:, None]
    mix = vbus + sfx.x * 0.9 + mbus * db(-20)

    n = int((a.end or dur) * SR)
    mix = mix[:n]
    mix[-int(0.25 * SR):] *= np.linspace(1, 0, int(0.25 * SR))[:, None]
    # keep the transient at frame 0: limiter-free peak normalisation first, then the loudness target
    save(a.out + ".pre.wav", mix * 0.95 / (np.max(np.abs(mix)) + 1e-9))
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.out + ".pre.wav", "-af", "loudnorm=I=-14:TP=-1.0:LRA=11",
                    "-ar", str(SR), "-c:a", "pcm_s24le", a.out], check=True)
    os.remove(a.out + ".pre.wav")
    stems = os.path.join(os.path.dirname(a.out), "stems")
    os.makedirs(stems, exist_ok=True)
    save(os.path.join(stems, "sfx.wav"), (sfx.x * 0.9)[:n])
    save(os.path.join(stems, "music.wav"), (mbus * db(-20))[:n])
    print("WROTE", a.out, f"{n / SR:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
