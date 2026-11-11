"""Sound design for the well video: SFX on every on-screen action + our own synthesised music bed,
mixed under the voice (ducked) and normalised for YouTube.

  python tools/well_sound.py [--voice sound/voice/well_eleven.mp3] [--out output/well/mix.wav]

Event times come from the SAME sources as the picture: docs/scenes/well_words.json (the scene's
Tw(sentence, word) lookups, mirrored here) and output/well/labels.json (each label's first frame).
Sources (shared rules, "Music, sound and reused content"): CC0 samples from the 2D channel's sfx/
(only files marked _cc0), everything else synthesised here. No Freesound (_fs) files: no licence record.
"""
import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SR = 48000
SFX2D = os.path.abspath(os.path.join(ROOT, "..", "2d-physics", "sfx"))
rng = np.random.default_rng(7)


# ----------------------------------------------------------------- io
def load(path):
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"])
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()


def sample(name):
    return load(os.path.join(SFX2D, name))


def save(path, x):
    x = np.clip(x, -1, 1).astype(np.float32)
    p = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-c:a", "pcm_s24le", path],
                         stdin=subprocess.PIPE)
    p.communicate(x.tobytes())


# ----------------------------------------------------------------- dsp helpers
def bp(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return signal.lfilter(b, a, x, axis=0)


def lp(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2), "low")
    return signal.lfilter(b, a, x, axis=0)


def hp(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2), "high")
    return signal.lfilter(b, a, x, axis=0)


def env(n, a, r):
    """attack / release in seconds over n samples"""
    e = np.ones(n)
    na, nr = int(a * SR), int(r * SR)
    if na > 0: e[:na] = np.linspace(0, 1, na)
    if nr > 0: e[-nr:] *= np.linspace(1, 0, nr)
    return e


def stereo(m, pan=0.0):
    l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
    return np.stack([m * l, m * r], axis=1)


def noise(sec):
    return rng.standard_normal(int(sec * SR))


class Bus:
    def __init__(self, sec):
        self.x = np.zeros((int(sec * SR) + SR, 2))

    def add(self, t, sig, gain=1.0):
        if sig.ndim == 1:
            sig = stereo(sig)
        i = int(max(0, t) * SR)
        j = min(len(self.x), i + len(sig))
        if j > i:
            self.x[i:j] += sig[: j - i] * gain


def db(g):
    return 10 ** (g / 20)


# ----------------------------------------------------------------- synthesised sounds
def engine(sec, rpm_curve, load_curve):
    """diesel rig: firing pulses + harmonics, rough; rpm_curve/load_curve: arrays (0..1) over sec"""
    n = int(sec * SR)
    t = np.arange(n) / SR
    f0 = 28 + 22 * np.interp(t, np.linspace(0, sec, len(rpm_curve)), rpm_curve)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    tone = sum((0.9 / k) * np.sin(k * ph + rng.uniform(0, 6)) for k in range(1, 9))
    pulse = (0.5 + 0.5 * np.sin(ph * 2)) ** 3
    rough = lp(noise(sec), 900) * 0.6
    ld = np.interp(t, np.linspace(0, sec, len(load_curve)), load_curve)
    m = (tone * 0.5 + rough * pulse) * (0.6 + 0.4 * ld)
    m = lp(m, 1800)
    return m / (np.max(np.abs(m)) + 1e-9)


def grind(sec):
    """drill bit chewing soil: band noise with jittery amplitude"""
    m = bp(noise(sec), 300, 2600)
    am = lp(np.abs(noise(sec)), 18)
    m = m * (0.4 + 2.0 * am)
    return m / (np.max(np.abs(m)) + 1e-9) * env(len(m), 0.15, 0.4)


def whoosh(sec, up=True):
    n = int(sec * SR)
    x = noise(sec)
    out = np.zeros(n)
    seg, zi = 512, np.zeros(4)                 # filter state carried across segments: no zipper clicks
    for i in range(0, n, seg):
        k = i / n
        f = 300 + 3500 * (k if up else 1 - k)
        b, a = signal.butter(2, [max(80, f * 0.6) / (SR / 2), min(SR / 2 - 100, f * 1.4) / (SR / 2)], "band")
        out[i:i + seg], zi = signal.lfilter(b, a, x[i:i + seg], zi=zi)
    e = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    return out * e / (np.max(np.abs(out * e)) + 1e-9)


def trickle(sec, bright=1.0):
    """running water: band noise + many tiny bubble blips"""
    n = int(sec * SR)
    m = bp(noise(sec), 500 * bright, 5000 * bright) * 0.25
    for _ in range(int(sec * 40)):
        m += blip(rng.uniform(600, 2200) * bright, rng.uniform(0.01, 0.03), rng.uniform(0.2, 0.6), n, rng.integers(0, n))
    return m / (np.max(np.abs(m)) + 1e-9) * env(n, 0.2, 0.4)


def blip(f, dur, amp, n=None, at=0):
    """a bubble: sine with a fast upward chirp and decay"""
    k = int(dur * SR)
    t = np.arange(k) / SR
    s = np.sin(2 * np.pi * (f * t + f * 2.5 * t * t / dur)) * np.exp(-t / (dur / 3)) * amp
    if n is None:
        return s
    out = np.zeros(n)
    j = min(n, at + k)
    out[at:j] = s[: j - at]
    return out


def rain(sec):
    n = int(sec * SR)
    m = lp(hp(noise(sec), 400), 7000) * 0.35
    for _ in range(int(sec * 60)):
        i = rng.integers(0, n - 2000)
        m[i:i + 600] += hp(noise(600 / SR), 2000) * np.exp(-np.arange(600) / 90) * rng.uniform(0.3, 1.0)
    return m / (np.max(np.abs(m)) + 1e-9) * env(n, 0.6, 0.6)


def slurry(sec):
    """cement pumped through a hose: low gurgle + wet noise"""
    n = int(sec * SR)
    m = lp(noise(sec), 500) * 0.8
    for _ in range(int(sec * 14)):
        m += blip(rng.uniform(120, 320), rng.uniform(0.05, 0.12), rng.uniform(0.4, 0.9), n, rng.integers(0, n))
    return m / (np.max(np.abs(m)) + 1e-9) * env(n, 0.3, 0.5)


def hum(sec, f=100):
    n = int(sec * SR)
    t = np.arange(n) / SR
    m = 0.6 * np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t) + 0.15 * np.sin(2 * np.pi * 3 * f * t)
    m += bp(noise(sec), 200, 1200) * 0.1
    return m * env(n, 0.4, 0.6) / 1.2


def glass_fill(sec):
    """pouring into a glass: resonance rising as it fills"""
    n = int(sec * SR)
    x = noise(sec)
    out = np.zeros(n)
    seg, zi = 1024, np.zeros(4)
    for i in range(0, n, seg):
        k = i / n
        f = 500 + 1400 * k
        b, a = signal.butter(2, [f * 0.85 / (SR / 2), f * 1.15 / (SR / 2)], "band")
        out[i:i + seg], zi = signal.lfilter(b, a, x[i:i + seg], zi=zi)
    out += bp(noise(sec), 2000, 8000) * 0.15
    return out / (np.max(np.abs(out)) + 1e-9) * env(n, 0.1, 0.3)


def bird(n_chirps=3):
    out = []
    for _ in range(n_chirps):
        d = rng.uniform(0.05, 0.12)
        t = np.arange(int(d * SR)) / SR
        f0, f1 = rng.uniform(2800, 3800), rng.uniform(4000, 5600)
        s = np.sin(2 * np.pi * (f0 * t + (f1 - f0) * t * t / (2 * d))) * np.sin(np.pi * t / d) ** 2
        out.append(s)
        out.append(np.zeros(int(rng.uniform(0.04, 0.1) * SR)))
    return np.concatenate(out)


def music(sec, bpm=88):
    """our own bed: warm pad + soft plucked arpeggio, I-V-vi-IV in D major; quiet by design"""
    n = int(sec * SR)
    t = np.arange(n) / SR
    beat = 60 / bpm
    bar = 4 * beat
    prog = [[62, 66, 69], [57, 61, 64], [59, 62, 66], [55, 59, 62]]   # D, A, Bm, G (midi)
    hz = lambda m: 440 * 2 ** ((m - 69) / 12)
    pad = np.zeros(n)
    pl = np.zeros(n)
    nb = int(np.ceil(sec / bar))
    for b in range(nb):
        ch = prog[b % 4]
        i0, i1 = int(b * bar * SR), min(n, int((b + 1) * bar * SR) + int(0.3 * SR))
        tt = np.arange(i1 - i0) / SR
        e = np.minimum(1, tt / 0.6) * np.minimum(1, (len(tt) / SR - tt) / 0.6 + 0.0)
        for m in ch:
            for det in (-0.08, 0.08):
                pad[i0:i1] += np.sin(2 * np.pi * hz(m - 12) * (1 + det / 100) * tt) * e * 0.12
        # arpeggio: eighth notes over the chord, an octave up
        notes = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12]
        for k in range(8):
            j0 = int((b * bar + k * beat / 2) * SR)
            if j0 >= n: break
            dur = int(1.2 * SR)
            j1 = min(n, j0 + dur)
            tk = np.arange(j1 - j0) / SR
            f = hz(notes[k % 4])
            s = (np.sin(2 * np.pi * f * tk) + 0.35 * np.sin(4 * np.pi * f * tk) + 0.12 * np.sin(6 * np.pi * f * tk)) * np.exp(-tk * 4.5)
            pl[j0:j1] += s * (0.16 if k % 2 == 0 else 0.11)
    pad = lp(pad, 1500)
    m = pad + pl
    m = m / (np.max(np.abs(m)) + 1e-9)
    # gentle stereo: delay the plucks a little on the right
    d = int(0.012 * SR)
    st = np.stack([m, np.concatenate([np.zeros(d), m[:-d]])], axis=1)
    return st * env(n, 1.5, 2.5)[:, None]


# ----------------------------------------------------------------- the score
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=os.path.join(ROOT, "sound", "voice", "well_eleven.mp3"))
    ap.add_argument("--out", default=os.path.join(ROOT, "output", "well", "mix.wav"))
    a = ap.parse_args()

    d = json.load(open(os.path.join(ROOT, "docs", "scenes", "well_words.json"), encoding="utf-8"))
    norm = lambda s: re.sub(r"[^a-z0-9']", "", s.lower())

    def tw(sent, word, occ=0, end=False):
        n = 0
        for w in d["sentences"][sent - 1]["words"]:
            if norm(w["w"]) == norm(word):
                if n == occ:
                    return w["end"] if end else w["start"]
                n += 1
        raise SystemExit(f"tw: {word} not in sentence {sent}")

    TAIL = 1.2
    dur = d["duration"] + TAIL
    sfx = Bus(dur)

    # ambience: soft wind + a few birds the whole time
    wind = lp(noise(dur + 1), 450) * 0.5 + bp(noise(dur + 1), 800, 2500) * 0.05
    wind *= 0.7 + 0.3 * lp(np.abs(noise(dur + 1)), 0.4) * 3
    sfx.add(0, stereo(wind / np.max(np.abs(wind))), db(-30))
    for k, t0 in enumerate(np.arange(0.6, dur - 1, 4.3)):
        sfx.add(t0 + rng.uniform(0, 1.5), stereo(bird(rng.integers(2, 5)), rng.uniform(-0.7, 0.7)), db(-31))

    # hook -> dive into the ground
    sfx.add(tw(1, "Let's") - 0.1, stereo(whoosh(1.4)), db(-16))
    # water fills the gravel
    sfx.add(tw(2, "water") - 0.1, stereo(trickle(1.8, 1.0)), db(-22))

    # drill rig: drives in, drills, leaves
    r0, r1 = tw(3, "First") - 0.25, tw(5, "Now") + 1.6
    dA, dB = tw(3, "bores"), tw(3, "feet", 0, True) + 0.1
    L = r1 - r0
    rpm = np.interp(np.linspace(r0, r1, 400), [r0, r0 + 0.8, dA, dA + 0.4, dB, dB + 0.6, r1 - 1.6, r1], [0.3, 0.5, 0.45, 1.0, 1.0, 0.5, 0.5, 0.8])
    load_c = np.interp(np.linspace(r0, r1, 400), [r0, dA, dA + 0.3, dB, dB + 0.3, r1], [0.2, 0.2, 1, 1, 0.2, 0.3])
    eng = engine(L, rpm, load_c) * env(int(L * SR), 0.7, 1.4)
    sfx.add(r0, stereo(eng, -0.3), db(-20))
    sfx.add(dA, stereo(grind(dB - dA + 0.3)), db(-23))
    chips = [f"pebble_click_{i}_cc0.ogg" for i in range(1, 9)]
    for t0 in np.arange(dA, dB, 0.09):
        sfx.add(t0 + rng.uniform(0, 0.06), sample(chips[rng.integers(0, len(chips))]), db(-27 + rng.uniform(-4, 2)))
    sfx.add(dA - 0.05, sample("press_slam_hyd_cc0.ogg"), db(-20))

    # cave-in thuds + rumble
    cave = tw(4, "cave")
    for k in range(5):
        sfx.add(cave - 0.05 + k * 0.11, sample("soft_thud_cc0.ogg"), db(-12 - k * 1.5))
    sfx.add(cave - 0.1, stereo(lp(noise(1.0), 160) * env(SR, 0.05, 0.8) * 2.5), db(-16))
    # casing lowered by the rig's hydraulics, lands with a soft clunk
    sfx.add(tw(4, "lowered") - 0.3, sample("press_slam_hyd_cc0.ogg"), db(-18))
    sfx.add(tw(4, "walls", 0, True) + 0.35, sample("soft_thud_cc0.ogg"), db(-14))
    sfx.add(tw(4, "walls", 0, True) + 0.38, sample("metal_clink_cc0.ogg"), db(-24))

    # seal: bentonite chips rattle down, then grout is pumped up the hoses
    clayA, clayB, cemB = tw(5, "filled") + 0.05, tw(5, "and") + 0.05, tw(5, "so") + 0.3
    for t0 in np.arange(clayA - 0.3, clayB, 0.05):
        sfx.add(t0 + rng.uniform(0, 0.04), sample(chips[rng.integers(0, len(chips))]), db(-25 + rng.uniform(-5, 1)))
    sfx.add(clayB - 0.1, stereo(slurry(cemB - clayB + 0.5)), db(-17))
    sfx.add(clayB - 0.15, stereo(hum(cemB - clayB + 0.6, 70)), db(-27))
    # dirty rain, stopped by the seal
    sfx.add(tw(5, "dirty") - 0.15, stereo(rain(tw(6, "At") - tw(5, "dirty") + 0.3)), db(-20))
    sfx.add(tw(5, "outside"), sample("glass_ping_3_cc0.ogg"), db(-18))

    # screen: water squeezes in, bubbles, sand ticks off the slots
    sfx.add(tw(6, "Water") - 0.1, stereo(trickle(3.0, 1.3)), db(-18))
    for t0 in np.arange(tw(6, "Water"), tw(7, "A"), 0.13):
        sfx.add(t0 + rng.uniform(0, 0.1), blip(rng.uniform(500, 1400), 0.025, 0.5), db(-24))
    for t0 in np.arange(tw(6, "Sand"), tw(7, "A"), 0.16):
        sfx.add(t0 + rng.uniform(0, 0.1), sample(chips[rng.integers(0, len(chips))]), db(-30))

    # pump drops, then hums; water runs through the pipe to the house
    sfx.add(tw(7, "drops") - 0.2, stereo(whoosh(0.9, up=False)), db(-24))
    push = tw(7, "pushes")
    sfx.add(push - 0.1, stereo(hum(dur - push - 0.2, 120)), db(-29))
    sfx.add(push, stereo(trickle(tw(7, "home", 0, True) - push + 0.8, 0.8)), db(-21))

    # cap lands, ground slopes, puddle runs off
    sfx.add(tw(8, "cap") + 0.1, sample("metal_clink_cc0.ogg"), db(-13))
    sfx.add(tw(8, "cap") + 0.12, sample("soft_thud_cc0.ogg"), db(-18))
    sfx.add(tw(8, "puddles"), stereo(trickle(1.6, 0.7)), db(-23))

    # chlorine: cap off (clink), glugs down the casing, germs pop with tiny pings
    sfx.add(tw(9, "flushed") - 0.1, sample("metal_clink_cc0.ogg"), db(-19))
    chA = tw(9, "chlorine")
    for k in range(6):
        sfx.add(chA - 0.1 + k * 0.17, blip(rng.uniform(180, 300), 0.12, 0.9), db(-15))
    sfx.add(chA, stereo(trickle(1.4, 0.9)), db(-20))
    for k in range(6):
        sfx.add(chA + 0.25 + k * 0.17, sample(f"glass_ping_{k + 4}_cc0.ogg" if k < 4 else "glass_ping_1_cc0.ogg"), db(-27))
    # the glass fills, final sparkle
    gA, gB = tw(9, "glass") - 0.25, tw(9, "germ-free", 0, True) + 0.2
    sfx.add(gA, stereo(glass_fill(gB - gA + 0.2)), db(-18))
    sfx.add(tw(9, "germ-free") + 0.05, sample("glass_ping_10_cc0.wav"), db(-14))
    sfx.add(tw(9, "germ-free") + 0.12, sample("bell_hit_cc0.ogg"), db(-22))

    # label pops: each label's first frame
    lj = os.path.join(ROOT, "output", "well", "labels.json")
    if os.path.exists(lj):
        L = json.load(open(lj, encoding="utf-8"))
        seen = set()
        for f, labs in enumerate(L["frames"]):
            for lab in labs:
                if lab["text"] not in seen:
                    seen.add(lab["text"])
                    sfx.add(f / L["fps"], sample("chip_click_cc0.ogg"), db(-20))

    # music bed, ducked under the voice
    mus = music(dur)
    voice = load(a.voice)
    vbus = np.zeros_like(sfx.x)
    vbus[: len(voice)] += voice[: len(vbus)]
    venv = lp(np.abs(vbus.mean(axis=1)), 6)
    duck = 1.0 - 0.55 * np.clip(venv / (np.percentile(venv, 90) + 1e-9), 0, 1)
    mbus = np.zeros_like(sfx.x)
    mbus[: len(mus)] = mus[: len(mbus)]
    mbus *= duck[:, None]

    # loudness: voice ~ -1 dBFS peaks, the rest set by the dB values above
    vbus *= 0.9 / (np.max(np.abs(vbus)) + 1e-9)
    mix = vbus + sfx.x * 0.9 + mbus * db(-22)
    mix = mix[: int(dur * SR)]
    save(a.out + ".pre.wav", mix)
    # YouTube: -14 LUFS integrated, -1 dBTP
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", a.out + ".pre.wav", "-af", "loudnorm=I=-14:TP=-1.0:LRA=9",
                    "-ar", str(SR), "-c:a", "pcm_s24le", a.out], check=True)
    os.remove(a.out + ".pre.wav")
    stems = os.path.join(os.path.dirname(a.out), "stems")
    os.makedirs(stems, exist_ok=True)
    save(os.path.join(stems, "sfx.wav"), sfx.x[: int(dur * SR)])
    save(os.path.join(stems, "music.wav"), (mbus * db(-22))[: int(dur * SR)])
    print("WROTE", a.out, f"{dur:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
