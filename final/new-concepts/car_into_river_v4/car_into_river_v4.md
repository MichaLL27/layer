# car_into_river_v4

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

- Version 4, beside v3. 48.6 s, 1080x1920, 60 fps, channel voice.
- New since v3 (Lazar's notes on v3): hair no longer flickers (the cards are lifted 4 mm off the painted scalp they z-fought with); the phone comes out of the right trouser pocket on "Your first thought" (the inside shot starts there); a real 3-point seatbelt strap that follows the body, which he unlatches on "off", carries to the door and lets retract up the pillar; a real steering wheel (rim, hub, spokes, tilted toward him) with the hands gripping outside the rim; the eyes follow the head (on this rig they are not its children).
- Open: the swim out of the window is seen head-on; from outside, the buckle hand is on his far side.
- Rebuild: Blender `car_assets.py` + `tools/blender_export.py`; `./tools/unity_render.ps1 -UnityArgs @('-wellScene','car','-wellOut','output/car/v4/video.mp4','-wellSS','1.25','-wellFps','60')`; from Bash: `python tools/finish_video.py car --tag eleven --title1 "" --title2 "" --plain --audio output/car/mix.wav --video output/car/v4/video.mp4 --out output/car/v4/car_v4.mp4`.
