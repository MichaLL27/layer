# 06_snow_cave_in_a_blizzard_v3

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

- 46.8 s, 60 fps, channel voice. `06_snow_cave_in_a_blizzard_v3_4k.mp4` = 2160x3840, the one to upload (render: `'-wellScale','2','-wellSS','1'`, into `output/snowcave/v3_4k/`); the plain mp4 = 1080x1920.
- New since v2 (Lazar: refine it a lot): real candle and cold-air point lights with shadows on lit snow walls, overcast storm and a low golden sun at dawn, wind-carved snow, branch-by-branch pines, far mountains, folds in the clothes, a huddled figure in the first hole, mist instead of blobs, snow torn off the crest. Models: `blender/src/snow_assets_v3.py`. Same voice, timing and sound.
- `WeMadeIt_Unity/Assets/Scripts/SnowScene.cs` now builds v3 only. Script and facts: `docs/scenes/snowcave.md`.
- The 32 F inside is what field reports and manuals give, not a lab measurement. The candle is not in the narration.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','snowcave','-wellOut','output/snowcave/v3/video.mp4','-wellSS','1.5','-wellFps','60')`; copy its `labels.json` to `output/snowcave/`; `python tools/snowcave_sound.py`; from Bash: `python tools/finish_video.py snowcave --tag eleven --title1 "" --title2 "" --plain --audio output/snowcave/mix.wav --video output/snowcave/v3/video.mp4 --out output/snowcave/v3/snowcave_v3.mp4`.
- Cover: frame 0, the person in the storm with the -40°F tag.
