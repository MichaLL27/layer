---
name: make-video
description: >
  Own the end-to-end creation of a new 3D cutaway explainer Short ("How a X Becomes a Y")
  with Blender models + Unity render. Use whenever Lazar asks to make / create / build a new
  video, from a REFERENCE URL ("make this in our style") or from an IDEA. Hard rule: build ONE
  short test clip first, self-verify it, hand it over for a verdict, and only then build the
  full ~60 s video.
user-invocable: true
---

# /make-video — beat sheet, test clip first, verdict, then full video

Read `CLAUDE.md` (engine split + REMAKE RULE) and `docs/PIPELINE.md` before step 1.

## Step 0 — Input mode

### Mode A — reference URL
1. Download into `research/` (`yt-dlp -f "bv*[height<=1080]+ba/b" --merge-output-format mp4`;
   never `--print`, it disables the download).
2. Contact sheet: `./tools/contact_sheet.ps1 <video> -Every 5` and Read the PNG; extract more
   frames where the mechanism is unclear.
3. Write the **reference beat sheet** in `research/<slug>.md`: per beat, what is on screen,
   the camera, the caption words, the mechanism. This is analysis, NOT the spec to copy.
4. Apply the **REMAKE RULE** (CLAUDE.md) to turn it into OUR beat sheet: own house/character,
   different duration (>= ±20%), different camera and beat order.

### Mode B — idea
Write our beat sheet directly from `research/format.md` (pacing, caption rhythm, framing).

## Step 1 — Narration script (VOICE FIRST: the scene is built on the voice, Lazar 2026-10-31)

Nothing is modelled or animated before the voice exists. Imagine someone speaking: every
picture serves the sentence being said at that second.

`docs/scenes/<slug>.md`: title, then `## Narration` = numbered lines (one beat of speech each,
8-10 numbers for ~50 s; target ~2.7-3 words/s), then a short `## Scene idea`. Facts must be
accurate (check them). Hook in sentence 1; one mechanism; a payoff near the end. Duration rule:
>= ±20% from the source (Mode A).

## Step 2 — Voice and word timings

`python tools/make_voice.py <slug> [--voice en-US-GuyNeural --rate -4% --gap 0.35 --tag v2]`
-> `sound/voice/<slug>_<tag>.mp3` and `docs/scenes/<slug>_words.json` (per sentence and per word:
start/end seconds). Edge TTS is the free draft voice; the final voice is the channel voice (`--engine eleven`,
CLAUDE.md "Channel voice"), same JSON out. Any text change = re-run (timings change).
Frame 0 is ALWAYS in motion and the first seconds say what the video explains (CLAUDE.md "Channel voice").

## Step 3 — Beat list timed to the words (the spec)

In the same `.md`: a table
`# | start s | end s | spoken words | what happens on screen | camera | assets needed`
where start/end come from `<slug>_words.json` (a picture changes ON the word that names it, a
visible change every 2-4 s). Captions = the words JSON (one word at a time; give each word a
minimum on-screen time, a few words are near-zero in whisper's timing).
List the **new assets** against `blender/src/` (reuse first).

## Step 4 — Assets (Blender)

Build missing models in `blender/src/<asset>.blend` (Blender 5.1, headless script or Python
via `blender.exe -b`), export with `tools/blender_export.py` to `blender/export/`, import into
Unity. Origins at the logical pivot, 1 unit = 1 m, +Y up on export. Keep the style flat and
clean: few materials, no heavy textures.

## Step 5 — ONE complete test clip (20-30 s, NOT a stub)

Unity scene `Assets/Scenes/<Slug>.unity`, beats from the C# beat list (times from the words
JSON), captions, framing and colours final; voice may be the TTS draft. The test clip is the
first N seconds of the real timeline (e.g. sentences 1-3), so nothing is re-timed later. Render with Recorder at 1080x1920 (or half-res for
speed). Self-verify (loop, Lazar not involved yet):
- `./tools/contact_sheet.ps1 <clip> -Every 2` and Read it; nothing clipping, subject in frame,
  captions readable in the Shorts safe area (not under the UI), each beat reads without sound.
- Cover test vs the source (Mode A): our contact sheet next to theirs must look like a
  different channel.
- ffprobe the file: 1080x1920, 30 fps (or 60), duration inside the plan.

## Step 6 — HARD STOP

Show Lazar the test clip: what it is, what the full video adds. Do NOT build the full video
before an explicit green light.

## Step 7 — On green light

Full beat list, final voice, music/SFX, render, ffprobe, stage in
`final/needs-review/shorts/NN_<what-it-is>/` (mp4 + `NN_<what-it-is>.md` with title options,
pinned comment, rebuild command). Number = next free across all `final/` stages.

## Guardrails
- Never skip the test-clip gate.
- Never copy a source's house, character, palette, caption style 1:1 or its exact duration.
- Visual change = re-render; audio change = re-mux only.
