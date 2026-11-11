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

- 53.4 s, 60 fps, channel voice. `04_fresh_water_on_an_island.mp4` = 2160x3840, the one to upload; `_1080p` = 1080x1920. This is version 2; version 1 is in `final/needs-review/shorts/04_fresh_water_on_an_island/`. Script and facts: `docs/scenes/island.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/IslandScene.cs`. Models: `blender/src/island_assets.py`, `island_props.py`. Walk: `assets/mixamo/walking.fbx`.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','island','-wellOut','output/island/full3/video.mp4','-wellSS','1.25','-wellFps','60')`; copy its `labels.json` to `output/island/`; `python tools/island_sound.py`; `python tools/finish_video.py island --tag eleven --title1 "" --title2 "" --plain --audio output/island/mix.wav --video output/island/full3/video.mp4 --out output/island/full3/island_full3.mp4`.
- 4K: same commands with `'-wellScale','2','-wellSS','1'` in the render, into `output/island/4k/`.
- Cover: frame 0, the figure kneeling at the edge of the sea.
- New since v1 (Lazar's notes): nobody drinks or walks close up. The hook is a figure kneeling at the shore; the walk is a line of footprints over the dune; the holes, the lining and the fire are shot alone; "scoop" is the tin let down on a rope. The walk clip only moves the tiny villagers in the last shot.
