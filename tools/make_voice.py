"""Voice first: narration -> audio + exact word timings, so the scene can be built on the voice.

  python tools/make_voice.py well                      # docs/scenes/well.md "## Narration" -> sound/voice/well_v1.mp3
  python tools/make_voice.py well --voice en-US-GuyNeural --rate -4% --gap 0.35 --tag v2
  python tools/make_voice.py well --audio my_recording.wav --tag mine   # own recording: only timings

Narration = the numbered lines under "## Narration" in docs/scenes/<slug>.md (one sentence group
per number). Each number is synthesised on its own and padded with --gap seconds of silence, so a
beat can start exactly on a sentence. Word times come from faster-whisper (base.en) on each
sentence and are aligned to the script's own words (the script text is what captions show).

Outputs:
  sound/voice/<slug>_<tag>.mp3            the full voice track
  docs/scenes/<slug>_words.json           {"duration", "sentences":[{"i","text","start","end","words":[{"w","start","end"}]}]}
Needs: pip install edge-tts faster-whisper; ffmpeg on PATH.
"""
import argparse
import asyncio
import difflib
import json
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


def read_narration(slug):
    path = os.path.join(ROOT, "docs", "scenes", f"{slug}.md")
    lines, on = [], False
    for ln in open(path, encoding="utf-8"):
        if ln.startswith("## "):
            on = ln.strip().lower().startswith("## narration")
            continue
        m = re.match(r"\s*(\d+)\.\s+(.*\S)\s*$", ln)
        if on and m:
            lines.append(m.group(2))
    if not lines:
        sys.exit(f"No numbered lines under '## Narration' in {path}")
    return lines


def norm(w):
    return re.sub(r"[^a-z0-9']", "", w.lower())


def probe(path):
    out = subprocess.check_output(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path])
    return float(out.decode().strip())


async def synth(text, voice, rate, path):
    import edge_tts
    # the draft service rejects some sentences for some voices (NoAudioReceived, 2026-11-03): try the next voice
    for v in (voice, "en-US-GuyNeural", "en-US-ChristopherNeural", "en-US-BrianNeural"):
        try:
            await edge_tts.Communicate(text, v, rate=rate).save(path)
            return
        except edge_tts.exceptions.NoAudioReceived:
            print("edge: no audio from", v, "for:", text[:50])
    raise SystemExit("edge: every voice refused: " + text)


def align(script_words, heard):
    """script_words: list[str]; heard: list[(word,start,end)] -> list[(start,end)] per script word."""
    a = [norm(w) for w in script_words]
    b = [norm(h[0]) for h in heard]
    sm = difflib.SequenceMatcher(a=a, b=b, autojunk=False)
    times = [None] * len(a)
    for blk in sm.get_matching_blocks():
        for k in range(blk.size):
            times[blk.a + k] = (heard[blk.b + k][1], heard[blk.b + k][2])
    # fill gaps by interpolation between known neighbours
    known = [i for i, t in enumerate(times) if t]
    if not known:
        return None
    first, last = known[0], known[-1]
    for i in range(first):
        times[i] = (times[first][0], times[first][0])
    for i in range(last + 1, len(a)):
        times[i] = (times[last][1], times[last][1])
    i = first
    while i <= last:
        if times[i]:
            i += 1
            continue
        j = i
        while not times[j]:
            j += 1
        lo, hi = times[i - 1][1], times[j][0]
        n = j - i
        for k in range(n):
            times[i + k] = (lo + (hi - lo) * k / n, lo + (hi - lo) * (k + 1) / n)
        i = j
    return times


CHANNEL_VOICE_ID = "F635typDjR3WIF0qJAdL"     # "Layer by Layer Narrator", designed 2026-11-01 (sound/voice/design/previews.json)


def eleven_key():
    """ELEVENLABS_API_KEY from the environment, this project's .env, or the 3D channel's .env (shared account)."""
    if os.environ.get("ELEVENLABS_API_KEY"):
        return os.environ["ELEVENLABS_API_KEY"]
    for env in (os.path.join(ROOT, ".env"), os.path.join(ROOT, "..", "satisfying-3d-sims", ".env")):
        if os.path.exists(env):
            for line in open(env, encoding="utf-8"):
                if line.startswith("ELEVENLABS_API_KEY="):
                    return line.split("=", 1)[1].strip().strip("\"'")
    sys.exit("no ELEVENLABS_API_KEY found")


def eleven(args, sents, work, voice_dir):
    """The whole narration in ONE request (one continuous read, no stitched sentences); ElevenLabs returns
    per-character times, folded here into the same words JSON the scene reads."""
    import base64
    import urllib.error
    import urllib.request
    text = " ".join(sents)
    body = json.dumps({"text": text, "model_id": args.model,
                       "voice_settings": {"stability": 0.45, "similarity_boost": 0.8, "style": 0.25, "use_speaker_boost": True}}).encode()
    # mp3_44100_128: the 192 kbps format answered 403 on this (pay-as-you-go) account, 2026-11-01
    req = urllib.request.Request(f"https://api.elevenlabs.io/v1/text-to-speech/{args.voice_id}/with-timestamps?output_format=mp3_44100_128",
                                 data=body, headers={"xi-api-key": eleven_key(), "Content-Type": "application/json"})
    try:
        res = json.load(urllib.request.urlopen(req, timeout=600))
    except urllib.error.HTTPError as e:
        sys.exit(f"ElevenLabs HTTP {e.code}: {e.read().decode()[:400]}")
    out_mp3 = os.path.join(voice_dir, f"{args.slug}_{args.tag}.mp3")
    with open(out_mp3, "wb") as f:
        f.write(base64.b64decode(res["audio_base64"]))
    al = res.get("normalized_alignment") or res["alignment"]
    chars, st, en = al["characters"], al["character_start_times_seconds"], al["character_end_times_seconds"]
    spoken = "".join(chars)
    if spoken != text:
        print("note: alignment text differs from the script; matching by position")
    # walk the script's words over the character stream
    report, ci = [], 0
    for i, s in enumerate(sents, 1):
        words = []
        for w in s.split():
            k = spoken.find(w, ci)
            if k < 0:
                k = ci
            a, b = k, min(len(st) - 1, k + len(w) - 1)
            words.append({"w": w, "start": round(st[a], 3), "end": round(en[b], 3)})
            ci = k + len(w)
        report.append({"i": i, "text": s, "start": words[0]["start"], "end": words[-1]["end"], "words": words})
    total = probe(out_mp3)
    out_json = os.path.join(work if args.test_text else os.path.join(ROOT, "docs", "scenes"), f"{args.slug}_words.json")
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump({"voice": "eleven:" + args.voice_id, "model": args.model, "duration": round(total, 3),
                   "speech_end": report[-1]["end"], "sentences": report}, f, indent=1, ensure_ascii=False)
    nwords = sum(len(s["words"]) for s in report)
    print(f"WROTE {out_mp3}  ({total:.1f} s, speech ends {report[-1]['end']:.1f} s, {nwords} words, {len(text)} characters billed)")
    print(f"WROTE {out_json}")
    for s in report:
        print(f"  {s['i']}: {s['start']:6.2f}-{s['end']:6.2f}  {s['text'][:70]}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("slug")
    ap.add_argument("--voice", default="en-US-AndrewNeural")
    ap.add_argument("--rate", default="+0%")
    ap.add_argument("--gap", type=float, default=0.30, help="silence after each sentence, seconds")
    ap.add_argument("--tag", default="v1")
    ap.add_argument("--audio", help="use this recording instead of synthesising (whole narration in one file)")
    ap.add_argument("--engine", choices=["edge", "eleven"], default="edge",
                    help="edge = free draft; eleven = the channel voice (ElevenLabs, costs ~1 credit per character)")
    ap.add_argument("--voice-id", default=CHANNEL_VOICE_ID, help="ElevenLabs voice (default: 'Layer by Layer Narrator')")
    ap.add_argument("--model", default="eleven_multilingual_v2")
    ap.add_argument("--test-text", help="eleven only: synthesise this text into output/voice_test/ instead of the narration")
    args = ap.parse_args()

    sents = [args.test_text] if args.test_text else read_narration(args.slug)
    work = os.path.join(ROOT, "output", "voice_test" if args.test_text else args.slug)
    os.makedirs(work, exist_ok=True)
    voice_dir = work if args.test_text else os.path.join(ROOT, "sound", "voice")
    os.makedirs(voice_dir, exist_ok=True)
    if args.audio:
        sys.exit("--audio (own recording, one file) is not implemented yet: record sentence by sentence into output/<slug>/s<N>.wav")
    if args.engine == "eleven":
        return eleven(args, sents, work, voice_dir)

    from faster_whisper import WhisperModel
    model = WhisperModel("base.en", device="cpu", compute_type="int8")

    parts, report, offset = [], [], 0.0
    for i, text in enumerate(sents, 1):
        mp3 = os.path.join(work, f"s{i:02d}.mp3")
        wav = os.path.join(work, f"s{i:02d}.wav")
        asyncio.run(synth(text, args.voice, args.rate, mp3))
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", mp3, "-ar", "44100", "-ac", "1",
                        "-af", f"apad=pad_dur={args.gap}", wav], check=True)
        raw = probe(mp3)
        segs, _ = model.transcribe(mp3, word_timestamps=True, language="en")
        heard = [(w.word, w.start, w.end) for s in segs for w in (s.words or [])]
        words = text.split()
        times = align(words, heard)
        if times is None:
            sys.exit(f"Sentence {i}: whisper heard nothing usable")
        report.append({
            "i": i, "text": text, "start": round(offset, 3), "end": round(offset + raw, 3),
            "words": [{"w": w, "start": round(offset + t[0], 3), "end": round(offset + t[1], 3)} for w, t in zip(words, times)],
        })
        parts.append(wav)
        offset += probe(wav)

    lst = os.path.join(work, "concat.txt")
    with open(lst, "w", encoding="utf-8") as f:
        for p in parts:
            f.write("file '" + p.replace("\\", "/") + "'\n")
    out_mp3 = os.path.join(voice_dir, f"{args.slug}_{args.tag}.mp3")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", lst,
                    "-c:a", "libmp3lame", "-b:a", "192k", out_mp3], check=True)
    total = probe(out_mp3)
    # the last sentence needs no trailing gap in the finished video
    out_json = os.path.join(ROOT, "docs", "scenes", f"{args.slug}_words.json")
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump({"voice": args.voice, "rate": args.rate, "gap": args.gap, "duration": round(total, 3),
                   "speech_end": report[-1]["end"], "sentences": report}, f, indent=1, ensure_ascii=False)
    nwords = sum(len(s["words"]) for s in report)
    print(f"WROTE {out_mp3}  ({total:.1f} s incl. trailing gap, speech ends {report[-1]['end']:.1f} s, {nwords} words, {nwords / report[-1]['end']:.2f} words/s)")
    print(f"WROTE {out_json}")
    for s in report:
        print(f"  {s['i']}: {s['start']:6.2f}-{s['end']:6.2f}  {s['text'][:70]}")


if __name__ == "__main__":
    main()
