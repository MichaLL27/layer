# tunnel_problem_chain

## Upload

Draft, not for upload yet: the voice is the free draft, the channel voice comes next.

Title options (recommended first; the channel adds ` #usa`):

1. `Why Tunnels Under Rivers Don't Flood 🌊`
2. `One Mistake and the River Pours In 😱🌊`
3. `The Machine That Digs Under Rivers 🚇`
4. `How Do You Dig Under a River? 🌊🤯`

Description and tags: empty on a Short.

## Notes (not for upload)

- 49 s, 1080x1920, 60 fps. Script and shots: `docs/scenes/tunnel.md`. Reference for script shape and look: `research/format.md` "What the 29M video does" (70 s, so -30%).
- Rebuild: `python tools/make_voice.py tunnel --engine eleven --tag eleven` (about 762 characters), `./tools/unity_render.ps1 -UnityArgs @('-wellScene','tunnel','-wellOut','output/tunnel/video.mp4','-wellSS','1.5','-wellFps','60')`, `python tools/tunnel_sound.py --voice sound/voice/tunnel_eleven.mp3`, `python tools/finish_video.py tunnel --tag eleven --title1 "" --title2 "" --plain --audio output/tunnel/mix.wav --out output/tunnel/tunnel_draft.mp4`.
- 4K: add `'-wellScale','2','-wellSS','1'` to the render, then `--video` to the finish step.
- Open: no people or dense forest yet; the river's break-in is the weakest effect.
