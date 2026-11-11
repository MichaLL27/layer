# island_water_test

## Upload

Test, not for upload yet: sentences 1-8 of 13, draft voice.

**Title** (recommended first; the channel adds ` #usa`):

1. `Never Drink This on a Desert Island 🏝️💀`
2. `The Hidden Water Under an Island 🏝️💧`
3. `3 Days to Find Water on This Island ⏱️💧`
4. `Stranded? Don't Dig on the Beach 🏝️⛏️`

**Description:** empty on a Short.

**Pinned comment:** not chosen yet for this channel.

**Tags:** empty on a Short.

## Notes (not for upload)

- 32.5 s of a planned ~60 s, 1080x1920, 60 fps. Script, facts: `docs/scenes/island.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/IslandScene.cs`. Palms: `blender/src/island_assets.py`.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','island','-wellOut','output/island/test/video.mp4','-wellSS','1.25','-wellFps','60','-wellTo','1950')`; copy its `labels.json` to `output/island/`; `python tools/island_sound.py`; `python tools/finish_video.py island --tag v1 --title1 "" --title2 "" --plain --audio output/island/mix.wav --video output/island/test/video.mp4 --out output/island/test/island_test.mp4`.
- Still to build on green light: sentences 9-13 (the ×40 number, digging too deep, the driftwood lining, slow scooping, boiling), channel voice (`--engine eleven`), watermark if the channel uses one.
- The figure's poses are procedural (IK from the bind pose): there is no digging or walking clip on this PC.
