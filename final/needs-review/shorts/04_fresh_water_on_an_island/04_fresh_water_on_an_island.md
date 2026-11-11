# 04_fresh_water_on_an_island

## Upload

**Title** (recommended first; the channel adds ` #usa`):

1. `Never Drink This on a Desert Island 🏝️💀`
2. `The Hidden Water Under an Island 🏝️💧`
3. `3 Days to Find Water on This Island ⏱️💧`
4. `Stranded? Don't Dig on the Beach 🏝️⛏️`

**Description:** empty on a Short.

**Pinned comment:** not chosen yet for this channel.

**Tags:** empty on a Short.

## Notes (not for upload)

- 53.4 s, 1080x1920, 60 fps, channel voice. 4K to be rendered once approved. Script and facts: `docs/scenes/island.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/IslandScene.cs`. Models: `blender/src/island_assets.py`, `island_props.py`. Walk: `assets/mixamo/walking.fbx`.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','island','-wellOut','output/island/full/video.mp4','-wellSS','1.25','-wellFps','60')`; copy its `labels.json` to `output/island/`; `python tools/island_sound.py`; `python tools/finish_video.py island --tag eleven --title1 "" --title2 "" --plain --audio output/island/mix.wav --video output/island/full/video.mp4 --out output/island/full/island_full.mp4`.
- 4K: add `'-wellScale','2','-wellSS','1'` to the render.
- Cover: frame 0, the figure reaching for the sea at the shoreline.
