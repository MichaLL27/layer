# car_into_river_v6

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

- Version 6, beside v4. 50.7 s, 1080x1920, 60 fps, channel voice.
- New since v4 (Lazar's notes): opens straight on "Your car just went off a bridge into a river." (new ElevenLabs read: `sound/voice/car_eleven2.mp3`, timings in `docs/scenes/car_words.json`; v1-v5 used `car_eleven.mp3` + `car_words_v1.json`); not a realistic person: a blue-suit figure with a white helmet on the same skeleton and acting; the phone comes out of the pocket and the hand is on the buckle and the console window switch in the shots (the skinned meshes are baked every frame: before, they lagged the skeleton in a back-to-back render, which also made the hair slide); a door handle: pulled on "push", and in "if nothing breaks" he is back in the seat, pulls it, pushes the door open and swims out of it.
- `car_into_river_v6_4k.mp4` = 2160x3840 (render: `-wellScale 2 -wellSS 1`, finish with `--video output/car/v6/video_4k.mp4`).
- Open: the swim out of the window is seen head-on.
- Rebuild: Blender `car_assets.py` + `tools/blender_export.py`; `./tools/unity_render.ps1 -UnityArgs @('-wellScene','car','-wellOut','output/car/v6/video.mp4','-wellSS','1.25','-wellFps','60')`; `python tools/car_sound.py --voice sound/voice/car_eleven2.mp3`; from Bash: `python tools/finish_video.py car --tag eleven2 --title1 "" --title2 "" --plain --audio output/car/mix.wav --video output/car/v6/video.mp4 --out output/car/v6/car_v6.mp4`.
