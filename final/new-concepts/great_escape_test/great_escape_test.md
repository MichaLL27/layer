# great_escape_test

Test clip of the Great Escape Short (sentences 1-6 of 13, 25.5 s of the planned ~57 s), waiting for Lazar's verdict.
Script, facts and beats: `docs/scenes/escape.md`.

## Notes (not for upload)

- Draft voice (Edge TTS; the service dropped some lines to fallback voices, so the voice changes mid-clip). The final read is the channel voice: `python tools/make_voice.py escape --engine eleven` (~830 characters), then re-render: the scene re-times itself from `escape_words.json`.
- No music or SFX yet.
- Models: `blender/src/escape_assets.py` (barrack, stove + brick base, trap door, bunk, table, guard tower, fence post, wire coil). Scene: `WeMadeIt_Unity/Assets/Scripts/EscapeScene.cs`.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','escape','-wellOut','output/escape/test/video.mp4','-wellFps','60','-wellSS','1.5','-wellFrom','0','-wellTo','1530')`; copy `output/escape/test/labels.json` to `output/escape/`; `python tools/finish_video.py escape --tag v1 --title1 "" --title2 "" --plain --video output/escape/test/video.mp4 --out output/escape/test/escape_test.mp4`.
- Known weak points: the walk is stiff (the walk clip's arms are pinned by IK); the sand texture repeats on big areas.
- Cover: frame 0, a prisoner from behind walking toward the wire, "1944".
