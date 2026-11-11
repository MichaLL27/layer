# 06_snow_cave_in_a_blizzard

## Upload

**Title** (recommended first; the channel adds ` #usa`):

1. `How a Pile of Snow Saves Your Life 🏔️❄️`
2. `-40° Outside, 32° In Here ❄️🤯`
3. `Don't Fall Asleep in a Snow Cave 💀❄️`
4. `The Night You Sleep Inside a Snowdrift 🌨️😱`

**Description:** empty on a Short.

**Pinned comment:** not chosen yet for this channel.

**Tags:** empty on a Short.

## Notes (not for upload)

- 46.8 s, 1080x1920, 60 fps, channel voice. Made unattended on 2026-11-09, test-clip gate skipped. 4K after approval: add `'-wellScale','2','-wellSS','1'` to the render.
- Script and facts: `docs/scenes/snowcave.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/SnowScene.cs` (no Blender models; the person is built from primitives).
- The 32 F inside is what field reports and manuals give, not a lab measurement.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','snowcave','-wellOut','output/snowcave/full/video.mp4','-wellSS','1.25','-wellFps','60')`; copy its `labels.json` to `output/snowcave/`; `python tools/snowcave_sound.py`; from Bash: `python tools/finish_video.py snowcave --tag eleven --title1 "" --title2 "" --plain --audio output/snowcave/mix.wav --video output/snowcave/full/video.mp4 --out output/snowcave/full/snowcave_full.mp4`.
- Cover: frame 0, the person in the storm with the -40°F tag.
