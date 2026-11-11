# tunnel_under_a_river_test

## Upload

Test clip, not for upload yet. Title options for the full video (recommended first; the channel adds ` #usa`):

1. `Why Tunnels Under Rivers Don't Flood 🌊`
2. `One Mistake and the River Pours In 😱🌊`
3. `The Machine That Digs Under Rivers 🚇`
4. `How Do You Dig Under a River? 🌊🤯`

Description and tags: empty on a Short.

## Notes (not for upload)

- First 37.2 s of the real timeline (sentences 1-9 of `docs/scenes/tunnel.md`), channel voice, sound, captions. Still to build after a yes: sentences 10-12 (grout, breakthrough, train), then 4K.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','tunnel','-wellOut','output/tunnel/video.mp4','-wellSS','1.5','-wellTo','1116')`, `python tools/tunnel_sound.py --end 37.2`, `python tools/finish_video.py tunnel --tag eleven --title1 "" --title2 "" --audio output/tunnel/mix.wav --out output/tunnel/tunnel_test.mp4`.
- Cover: frame 0 (the river breaking in).
