# tunnel_wide_shots

## Upload

Title options (recommended first; the channel adds ` #usa`):

1. `Why Tunnels Under Rivers Don't Flood 🌊`
2. `One Mistake and the River Pours In 😱🌊`
3. `The Machine That Digs Under Rivers 🚇`
4. `How Do You Dig Under a River? 🌊🤯`

Description and tags: empty on a Short.

## Notes (not for upload)

- 46 s, 60 fps, channel voice; `tunnel_wide_shots.mp4` 1080x1920, `tunnel_wide_shots_4k.mp4` 2160x3840 (render: add `-wellScale 2 -wellSS 1`, finish with `--video output/tunnel/video_4k.mp4`). Script and shots: `docs/scenes/tunnel.md`. Replaces nothing yet: `tunnel_problem_chain` waits beside it.
- Rebuild: `./tools/unity_render.ps1 -UnityArgs @('-wellScene','tunnel','-wellOut','output/tunnel/video.mp4','-wellSS','1.5','-wellFps','60')`, `python tools/tunnel_sound.py`, `python tools/finish_video.py tunnel --tag eleven --title1 "" --title2 "" --plain --audio output/tunnel/mix.wav --out output/tunnel/tunnel_wide.mp4`.
- Open: no people yet; the river's break-in is the weakest effect.
