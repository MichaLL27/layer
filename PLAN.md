# PLAN — We Made It

## Goal
YouTube Shorts channel of 3D cutaway explainers ("How a Hole in Your Yard Becomes a Basement"
style). Built with Blender (models) + Unity (scene, animation, render). Fully scriptable, so
Claude can build a video from a beat sheet.

## State (2026-10-31)
- [x] 4 reference videos downloaded and analysed (`research/`)
- [x] Folder layout + CLAUDE.md + `/make-video` skill
- [x] Channel name: **Layer by Layer 3D** (handle `@LayerByLayer3D` is TAKEN: pick another in YouTube's form)
- [x] Avatar, banner, watermark v1 (`channel/`, rebuild `python tools/make_brand.py`)
- [x] Skills copied: youtube-upload, youtube-analytics, crosspost, overnight, comment-video
- [x] `tools/env_check.ps1` passes on this PC
- [x] Unity project `WeMadeIt_Unity/` created (6000.4.1f1); Built-in pipeline + Linear, no extra packages needed
- [x] Video 01 = **well** ("How a Well Gets Dug (and Stays Clean)"): narration v1 + draft voice (Edge TTS, 49.3 s) + word timings -> `docs/scenes/well.md`, `well_words.json`
- [x] Well: text + draft voice approved by Lazar (2026-10-31)
- [x] Well: Blender assets (house, tree, rig, pump, cap, casing, screen), Unity scene `WellScene.cs`, renderer, captions -> DRAFT v0 in `final/needs-review/shorts/01_how_a_well_gets_dug/` (49.5 s)
- [x] Channel voice designed and saved: "Layer by Layer Narrator" (ElevenLabs `F635typDjR3WIF0qJAdL`)
- [ ] Well: final read with the channel voice after the ElevenLabs reset (~2026-11-04): `python tools/make_voice.py well --engine eleven`, re-render, finish
- [x] Well: music + SFX, 4K, UPLOADED 2026-11-01: https://youtube.com/shorts/ldiB5iTqr4g -> `final/uploaded/shorts/01_how_a_well_gets_dug/`
- [ ] Shared asset library: pull reusable parts (layers, house, tree, pipes) out of WellScene.cs for video 02
- [x] Unity: scene as function of time + own batch renderer + ffmpeg captions (replaces beat runner / Recorder idea)
- [ ] Island video (drinking water on a desert island), full 53.4 s: `final/ready-to-upload/shorts/04_fresh_water_on_an_island/`, approved by Lazar, 4K (2026-11-08)
- [ ] Night 2026-11-09 (made unattended, waiting for Lazar's verdict, 4K after approval): polished versions `final/needs-review/shorts/05_saving_the_leaning_tower_v2/`, `final/needs-review/shorts/06_snow_cave_in_a_blizzard_v3/` (earlier versions beside them)
- [ ] Short 07 Great Escape (topic picked 2026-11-11): test clip (sentences 1-6) in `final/new-concepts/great_escape_test/`, quality pass v2 beside it (`great_escape_test_v2/`), waiting for the verdict; then the full video
- [ ] Create the YouTube channel (name, handle, upload avatar + banner + watermark); fill `unknown` in CLAUDE.md Channel facts
- [ ] House/character design (OWN, not the source's)

## Decisions to make with Lazar
- YouTube handle (the first choice is taken); create the channel and upload channel/ art.
- Verdict on the well draft; final voice (ElevenLabs key lives in the 3D project's .env: ask before using, or Lazar's own voice).
- Video 02 topic (candidates: tunnel under a river, skyscraper foundation).
