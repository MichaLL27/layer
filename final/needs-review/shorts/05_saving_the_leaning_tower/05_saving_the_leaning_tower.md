# 05_saving_the_leaning_tower

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

- 53.4 s, 1080x1920, 60 fps, channel voice. Made unattended on 2026-11-09, test-clip gate skipped. 4K after approval: add `'-wellScale','2','-wellSS','1'` to the render.
- Script and facts: `docs/scenes/pisa.md`. Scene: `WeMadeIt_Unity/Assets/Scripts/PisaScene.cs`. Model: `blender/src/pisa_assets.py`.
- Drawn larger than life: the 1995 lurch (+0.8 degrees). The 5.5-degree lean and the 18 inches at the top are true size.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','pisa','-wellOut','output/pisa/full/video.mp4','-wellSS','1.25','-wellFps','60')`; copy its `labels.json` to `output/pisa/`; `python tools/pisa_sound.py`; from Bash: `python tools/finish_video.py pisa --tag eleven --title1 "" --title2 "" --plain --audio output/pisa/mix.wav --video output/pisa/full/video.mp4 --out output/pisa/full/pisa_full.mp4`.
- Cover: frame 0, the whole tower against the sky.
