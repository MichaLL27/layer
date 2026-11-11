# car_into_river_v3

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

- Version 3, beside v2. 48.6 s, 1080x1920, 60 fps, channel voice.
- New since v2 (Lazar's notes on v2): the driver acts every beat on its words: hands on the wheel by IK (wheel moved within reach), picks the phone up on "call" and looks at it, puts it on the passenger seat on "leave", buckle, door shoves, window-button presses, breaker strike, windshield hit, pushes the kid out; head looks at each job. Remy's hair uses its opacity map (merged into `Textures/remy/*_DiffuseA.png`), eye bones held still (the eye sat outside the head), elbow IK bends down, softer phone light, 3/4 passenger-seat camera.
- Open: from outside, the right hand on the buckle is hidden behind his body (an inside shot there shows river water in the cabin: the cutaway's open side). The swim out of the window is seen head-on. The steering wheel reads as a flat plate in the close shots.
- Rebuild: Blender `car_assets.py` + `tools/blender_export.py`; `./tools/unity_render.ps1 -UnityArgs @('-wellScene','car','-wellOut','output/car/v3/video.mp4','-wellSS','1.25','-wellFps','60')`; from Bash: `python tools/finish_video.py car --tag eleven --title1 "" --title2 "" --plain --audio output/car/mix.wav --video output/car/v3/video.mp4 --out output/car/v3/car_v3.mp4` (PowerShell drops the empty `""` arguments).
