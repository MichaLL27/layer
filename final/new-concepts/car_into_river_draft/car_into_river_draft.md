# car_into_river_draft

## Upload

Layout draft, not for upload: crash-test dummies stand in for the people until the Mixamo characters arrive.

Title options (recommended first; the channel adds ` #usa`):

1. `How to Escape a Car Sinking in a River 🚗🌊`
2. `Your Car Hits the River. You Have 60 Seconds ⏱️🌊`
3. `Why You Should Never Break the Windshield 🚗💥`
4. `The Window, Not the Door 🚗🌊`

Description and tags: empty on a Short.

## Notes (not for upload)

- 48.6 s, 1080x1920, 60 fps, channel voice. Script, facts, shots: `docs/scenes/car.md`; scene `WeMadeIt_Unity/Assets/Scripts/CarScene.cs`; models `blender/src/car_assets.py`; sound `tools/car_sound.py`.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','car','-wellOut','output/car/video.mp4','-wellSS','1.5','-wellFps','60')`, `python tools/car_sound.py`, `python tools/finish_video.py car --tag eleven --title1 "" --title2 "" --plain --audio output/car/mix.wav --out output/car/car_draft.mp4`.
- Open: people (Mixamo, in `assets/mixamo/`), a better car model.
