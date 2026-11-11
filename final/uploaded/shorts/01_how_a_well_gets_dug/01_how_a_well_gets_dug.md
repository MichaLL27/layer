# 01_how_a_well_gets_dug

Status: UPLOADED 2026-11-01 by Lazar (Studio, by hand). Category Education, type Concept overview. URL: https://youtube.com/shorts/ldiB5iTqr4g

## Upload

Title options (recommended first; the upload tool appends `#usa`):
1. **Recommended** — `How a Well Gets Dug (and Stays Clean) 💧`
2. **Alt** — `The Hidden Seal That Keeps Well Water Clean 💧`

Pinned comment: unknown (to be set in CLAUDE.md `## Channel facts`)

## Facts
- Length: 50.1 s (reference Shorts of the source channel run 69-71 s: about -28%, REMAKE RULE needs >= 20%)
- Beat sheet and narration: `docs/scenes/well.md`; word times: `docs/scenes/well_words.json`
- Scene: `WeMadeIt_Unity/Assets/Scripts/WellScene.cs` (pure function of time) + `WellLook.cs` (HDRP look) + `CutGround.cs` (real cut geometry); models `blender/src/well_assets.py` + Poly Haven CC0 (`tools/fetch_polyhaven.py`)
- Rebuild: `python tools/make_voice.py well --engine eleven --tag eleven` (costs ~811 characters; the timings JSON is kept, so only re-run on a text change) -> `./tools/unity_render.ps1 -UnityArgs @('-wellOut','output/well/video.mp4')` -> `python tools/finish_video.py well --tag eleven --title-secs 3.8`
- Render: HDRP, 1487 frames at 1620x2880 -> 1080x1920 in ~100 s (69 ms/frame, RX 5700 XT).

## Cover test
Not a copy of any source video (own topic: well, own house/rig, yellow palette, own beats). Format reference only.

## Outcome (filled by youtube-analytics)

## Sound
- Voice: `sound/voice/well_eleven.mp3` (ElevenLabs "Layer by Layer Narrator").
- SFX + music: `python tools/well_sound.py` -> `output/well/mix.wav` (stems in `output/well/stems/`). CC0 samples from `../2d-physics/sfx/*_cc0*` only; engine, grind, whoosh, water, rain, cement slurry, pump hum, glass fill, wind, birds and the music bed are synthesised (ours). Music ducked under the voice; measured: music 11-17 dB and SFX >= 7 dB under the voice in every spoken second.
- 4K rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellOut','output/well/video_4k.mp4','-wellScale','2','-wellSS','1')` (~5 min) then `python tools/finish_video.py well --tag eleven --title-secs 3.8 --audio output/well/mix.wav --video output/well/video_4k.mp4 --out output/well/well_4k.mp4`.
