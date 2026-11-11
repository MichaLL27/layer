# 05_saving_the_leaning_tower_v2

## Upload

**Title** (recommended first; the channel adds ` #usa`):

1. `They Saved This Tower by Digging Under It 🏗️🤯`
2. `Why the Leaning Tower Didn't Fall 🇮🇹😱`
3. `The Night the Leaning Tower Almost Fell 🌙💀`
4. `900 Tons of Lead Couldn't Fix This Tower 🏗️🤯`

**Description:** empty on a Short.

**Pinned comment:** not chosen yet for this channel.

**Tags:** empty on a Short.

## Notes (not for upload)

- 53.4 s, 1080x1920, 60 fps, channel voice. 4K after approval: `'-wellScale','2','-wellSS','1'` in the render.
- New since v1 (Lazar: refine the quality): cut-stone marble with joints and stains, masonry in section, earth with grains and stones, paving, a cathedral and cypresses behind (`blender/src/pisa_assets.py`), 1.5x supersampling. Same voice, timing and sound.
- Script and facts: `docs/scenes/pisa.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/PisaScene.cs` (now builds this version, not v1).
- Drawn larger than life: the 1995 lurch (+0.8 degrees). The 5.5-degree lean and the 18 inches at the top are true size.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','pisa','-wellOut','output/pisa/v2/video.mp4','-wellSS','1.5','-wellFps','60')`; copy its `labels.json` to `output/pisa/`; `python tools/pisa_sound.py`; from Bash: `python tools/finish_video.py pisa --tag eleven --title1 "" --title2 "" --plain --audio output/pisa/mix.wav --video output/pisa/v2/video.mp4 --out output/pisa/v2/pisa_v2.mp4`.
- Cover: frame 0, the whole tower against the sky.
