# car_into_river_v2

## Upload

**Title** (recommended first; the channel adds ` #usa`):

1. `How to Escape a Car Sinking in a River 🚗🌊`
2. `Your Car Hits the River. You Have 60 Seconds ⏱️🌊`
3. `Why You Should Never Break the Windshield 🚗💥`
4. `The Window, Not the Door 🚗🌊`

**Description:** empty on a Short.

**Pinned comment:** not chosen yet for this channel.

**Tags:** empty on a Short.

## Notes (not for upload)

- Version 2 of the car Short, beside `car_into_river_draft` (dummies). 48.6 s, 1080x1920, 60 fps, channel voice.
- New: Mixamo people (Remy = driver, Sophie = kid; `assets/mixamo/`), sampled clips + hand IK; smooth sedan shell (`blender/src/car_shell.py`); passenger-seat shots from inside (phone call, window button); splash as fading crown + streaks.
- Open: the swim out of the window is seen head-on and reads like sitting (34-36 s, 43-45 s); a side-on camera there would fix it. The near window glass shows as a pale sheet in close shots.
- Rebuild: Blender `car_assets.py` + `tools/blender_export.py`; `./tools/unity_render.ps1 -UnityArgs @('-wellScene','car','-wellOut','output/car/v2/video.mp4','-wellSS','1.25','-wellFps','60')`; `python tools/finish_video.py car --tag eleven --title1 "" --title2 "" --plain --audio output/car/mix.wav --video output/car/v2/video.mp4 --out output/car/v2/car_v2.mp4`.
