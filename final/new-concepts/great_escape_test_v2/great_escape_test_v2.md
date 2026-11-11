# great_escape_test_v2

Quality pass on `great_escape_test` (Lazar 2026-11-11: "a bit better, the quality"). Same timeline (sentences 1-6, 25.5 s), waiting for the verdict. Script and facts: `docs/scenes/escape.md`.

## Notes (not for upload)

- Changed vs v1:
  - The figure uses its own skin, face and hand textures (no longer a blue mannequin). The pullover is RAF blue-grey, the trousers dark, the boots brown, and he wears a wool cap.
  - The sand cut is a calm, fine-grain, layered yellow sand (procedural `SandFaceTex`, Poly Haven grain only as a soft normal). It replaces the noisy orange `sand_01`.
  - The trees sit further back and are smaller, so the hook shows sky.
  - The tunnel shots are about 20% tighter and follow the figure.
- Still the draft Edge voice, with fallback voices mid-clip; no music or SFX yet.
- Rebuild:
  1. `./tools/unity_render.ps1 -UnityArgs @('-wellScene','escape','-wellOut','output/escape/test2/video.mp4','-wellFps','60','-wellSS','1.5','-wellFrom','0','-wellTo','1530')`
  2. Copy `output/escape/test2/labels.json` to `output/escape/`.
  3. `python tools/finish_video.py escape --tag v1 "--title1=" "--title2=" --plain --video output/escape/test2/video.mp4 --out output/escape/test2/escape_test2.mp4`
- Cover: frame 0, a prisoner from behind walking toward the wire, "1944".
