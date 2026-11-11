# PIPELINE — Blender models -> Unity scene -> video

```
blender/src/*.blend --(tools/blender_export.py)--> blender/export/*.fbx|glb
        --> WeMadeIt_Unity/Assets/Models/  (auto-imported)
        --> Scene: prefabs + Beat runner (C#) + captions + camera
        --> tools/unity_render.ps1 (edit-mode batch, no Recorder): frames -> ffmpeg -> output/<slug>/video.mp4
        --> tools/finish_video.py: word-by-word captions + title + voice (+ music/sfx later)
        --> final/needs-review/shorts/NN_<name>/
```

## Why this split
- Blender is the best place to model; Unity is fast to render (seconds per clip, real-time
  preview), has PhysX, particles and water shaders, and is scriptable.
- Animation is code-driven: a video = a list of beats in C#
  (`Beat(start, duration, action)`: move / rotate / scale / reveal / camera / caption words).
  Same code can be reused and edited by Claude without opening the editor.
- Voice first: the voice track (TTS or recorded) gives word timings; captions are generated
  from them, and beats are timed to the words.

## How it really works (built 2026-10-31 for the well video)
- **Voice first:** `tools/make_voice.py <slug>` (Edge TTS + faster-whisper) -> voice mp3 + `docs/scenes/<slug>_words.json` (every word's start/end).
- **Scene = pure function of time:** `WellScene.Evaluate(t)` sets every object's state at second t, with beat times looked up from the words JSON (`Tw(sentence, "word")`), so changing the narration re-times the scene by itself and any frame can be rendered alone.
- **Render:** `WellRender.Run` builds the scene in EDIT mode (no play mode), renders each frame with `Camera.Render`, pipes raw frames into ffmpeg. 1487 frames 1080x1920 in ~35 s. Stills for checking: `-wellStills 5.5,12.1 -wellScale 0.5`.
- **Built-in render pipeline, Linear colour space.** No URP, Recorder, Timeline, Cinemachine or TextMeshPro are needed (kept out on purpose: fewer moving parts in batch mode).
- **Cutaway trick:** `Assets/Shaders/CutawayGround.shader` discards pixels inside up to 3 world rectangles (hole, trench, riser) so the cut face opens without geometry booleans; dark boxes behind the opening give it depth.
- **Models:** Blender builds them (`blender/src/well_assets.py`, FBX via `tools/blender_export.py`); Unity re-colours by material name from a palette (FBX front lands on +Z: models are rotated 180 degrees on placement).
- **Captions/title** are burned by ffmpeg `ass` (Segoe UI Black from `output/<slug>/fonts`), not by Unity.
- Close any open Unity editor on the project before a batch run.

## Look (house style)
Flat, clean, soft light, ONE sun + sky ambient, few materials, rounded geometry. Soil is a
cross-section "ant farm" block. Our palette and house/character are our own (see REMAKE RULE).

## Folders
| Path | Content |
|---|---|
| `blender/src` | .blend sources (one asset or kit per file) |
| `blender/export` | FBX/GLB for Unity (generated, safe to regenerate) |
| `WeMadeIt_Unity/Assets/{Models,Prefabs,Scenes,Scripts}` | the Unity project |
| `docs/scenes/<slug>.md` | beat sheet per video |
| `docs/uploads/<name>.md` | upload notes per video |
| `final/<stage>/shorts/NN_<name>/` | one folder per video |
| `sound/{music,sfx}` | source audio |
| `research/` | references, format analysis |

## Realistic pass (2026-11-01, Lazar: "too cartoon, make it realistic")
- **HDRP 17.4** project (from the 3d-high-end template); the built-in prototype is kept as `WeMadeIt_Unity_builtin/` (record only).
- **Assets: Poly Haven, CC0** (`tools/fetch_polyhaven.py tex|model|hdri <ids>`), downloaded to `assets/` (git-ignored) and copied into `WeMadeIt_Unity/Assets/Resources/PH/`. Import rules: `Assets/Editor/PHImport.cs` (normal maps, linear masks, HDRI cubemap, foliage alpha coverage). Foliage alpha is composited into `*_diffa_1k.png` before import.
- **Look** (`WellLook.cs`): HDRI sky x12000 (raw cos-weighted irradiance 4.79, measured from the .hdr), sun 85 klux at 48 deg (the HDRI's sun elevation), fixed EV100 13.4, ACES, SSAO, SSR, contact shadows, volumetric fog, light bloom/vignette, +8 white balance. DOF off (soft frames read as low quality on a phone). Textures are triplanar (world for ground, object space for moving models): no UVs needed.
- **Ground** (`CutGround.cs`): real geometry, rebuilt every frame from the layer stack minus cut boxes (hole, trench, riser); 500 m wide and 400 m deep so no edge is ever in frame.
- **Render** (`WellRender.cs`): 1.5x supersampled, SMAA, 24 warm-up renders, BT.709 tagged, light temporal grain. ~70 ms/frame.
- **Labels** are not drawn in 3D: Unity writes their screen position per frame (`output/<slug>/labels.json`), `finish_video.py` draws crisp pills with libass.
- Traps hit: FBX fronts land on +Z (models turned 180 deg); Poly Haven grass/shrub FBXs hold many variants side by side (use a child per clump); fir_tree_01 is 7M triangles and loses its twigs at distance (replaced by broadleaf trees); Barrel_01's texture is an "explosive" barrel (removed).
