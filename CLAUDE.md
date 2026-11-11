# CLAUDE.md — "Layer by Layer 3D": 3D cutaway explainer Shorts

(Channel name chosen 2026-10-31. Handle `@LayerByLayer3D` is TAKEN (YouTube's own form, 2026-10-31); a handle is only confirmed by typing it into YouTube's form: an HTTP 404 on youtube.com/@x does NOT mean free. Folder name
`we made it!` stays; the Unity project is `WeMadeIt_Unity`.)

Pipeline for 9:16 Shorts in the "How a X Becomes a Y" cutaway style (side-on cross-section of
soil, house, bunker, raft...; word-by-word captions; ~60-70 s). Format reference:
`research/reference-youmadeit/` (4 videos of @youmadeit3d, analysis in `research/format.md`).

**RULES ONLY.** Reasoning lives in `docs/`. Keep this file short.

@.claude/shared-rules.md

The shared rules apply, EXCEPT where this file says otherwise. This channel follows the
"Standard layout for new channels" in them (the tree is already created).

## Channel facts (read by the shared skills)

Channel not created on YouTube yet: every `unknown` is asked once by the skill that needs it,
then written here.

- **Upload:**
  - Channel: `Layer by Layer 3D`, `unknown`, `unknown` (not chosen yet; `@LayerByLayer3D` is taken).
  - Scope: Shorts only, the 9:16.
  - Schedule: unknown.
  - Made for kids: unknown.
  - Collaborator: none.
  - Settings: Category: Education, Type: Concept overview; Academic system: None (not Georgia: the audience is US); Problems/Level empty (Lazar, 2026-11-01).
  - Text: title + `#usa`, its only hashtag; description and tags empty on a Short (same house rule as the sims channels; revisit after the first analytics).
  - Pinned comment: unknown.
  - Ledger: `docs/uploads/YOUTUBE-UPLOADS.md`.
  - Video: folder: `final/ready-to-upload/shorts/NN_<name>/` with `NN_<name>.mp4` + `NN_<name>.md` (`## Upload` = title options, recommended first; template `docs/uploads/TEMPLATE.md`).
  - Uploaded: `final/uploaded/shorts/`.
  - Order: unknown.
- **Analytics:** Channel `Layer by Layer 3D`, id unknown; families unknown (define after ~10 videos); ledger `docs/uploads/YOUTUBE-UPLOADS.md`; outcomes `docs/uploads/<name>.md`.
- **Crosspost:** unknown (no TikTok or Facebook account yet).
- **Comment video:** episode: `/make-video` from the existing video plus the change.

## Skills here (copied from `satisfying-3d-sims`, 2026-10-31)

`/make-video` (ours, adapted) · `youtube-upload` · `youtube-analytics` · `crosspost` ·
`overnight` · `comment-video`. NOT copied because they are about physics-sim families:
`batch`, `megamix`, `marathon`, `reladder`, `redesign-video`. Revisit `batch` once there are
proven video types to repeat.

## Engine: Blender = models, Unity = everything else (Lazar, 2026-10-31)

- **Blender 5.1** (`C:\Program Files\Blender Foundation\Blender 5.1\blender.exe`, not on PATH)
  builds MODELS only (house, soil blocks, concrete, pipes, props, character). Sources in
  `blender/src/*.blend`, exports (FBX/GLB) in `blender/export/`, then imported into Unity.
- **Unity 6000.4.1f1** (Hub editor at `C:\Program Files\Unity\Hub\Editor\6000.4.1f1`) assembles
  the scene, animates (C# scripts, scene = function of time) and RENDERS (own edit-mode batch renderer, 1080x1920; no Recorder/Timeline). **HDRP + realistic look** since 2026-11-01 (Lazar: "too cartoon"): Poly Haven CC0 textures/models/HDRI, see `docs/PIPELINE.md` "Realistic pass".
  Project: `WeMadeIt_Unity/` (create with `tools/unity_new_project.ps1`, once).
- Animation is **code-driven** (C# beats from a beat sheet), not hand-keyed: reproducible and
  Claude-editable. Details and why: `docs/PIPELINE.md`.
- One Unity editor per project: close it before a batch build.

## Channel voice (Lazar, 2026-11-01)

- **"Layer by Layer Narrator"**, ElevenLabs voice `F635typDjR3WIF0qJAdL`, designed for this channel
  (Voice Design, preview A; description + previews in `sound/voice/design/`). Every video's final
  voice is this one: `python tools/make_voice.py <slug> --engine eleven` (one continuous read,
  ElevenLabs word timings). Edge TTS (`--engine edge`, default) is only the free draft.
- The ElevenLabs account is the 3D channel's (shared, pay-as-you-go, ~10k characters a month; the
  key is read from `../satisfying-3d-sims/.env`, never copied). Check the balance before a full read;
  ask Lazar before going over the monthly limit.
- **Every video, two musts** (Lazar, 2026-11-08): it always opens on a hook that stops the viewer, and everything we show must read clearly.
- **Goal: raise stayed-to-watch** (Lazar, 2026-11-03; video 01: 54.7%). The first words state the problem;
  a visible change or a new shot every 2-3 s. Script shape and look: `research/format.md` "What the 29M video
  does". Opening in motion at frame 0 is an untested idea (that 29M video opens calm): test it, don't assume it.

## REMAKE RULE (Lazar's global rule, applies here)

A video made "in our style" from a reference must NOT be matchable to its source:
- own look, own watermark, own house design, own character, own palette;
- duration never the same: at least ±20%;
- different camera, composition, object count, order of beats; recolour + new text is NOT enough;
- cover test before staging: our frames vs the source's side by side
  (`tools/contact_sheet.ps1`).
- A topic may be shared ("hole becomes a basement"); the telling must be ours.

## Where things are documented

| Read this | Before you |
|---|---|
| `docs/PIPELINE.md` | build or change anything in the Blender -> Unity -> video chain |
| `research/format.md` | decide beats, pacing, captions, framing of a new video |
| `.claude/skills/make-video/SKILL.md` | make ANY new video (`/make-video`) |
| `PLAN.md` | pick what to build next / see the state of the project |
| `channel/README.md` | touch the avatar, banner or watermark (`py tools/make_brand.py` rebuilds all three) |
| `docs/uploads/TEMPLATE.md` | write a video's `.md` (title options, pinned comment, outcome) |

## Environment (fixed)

- Windows 11. ffmpeg + yt-dlp on PATH (winget). Check everything: `./tools/env_check.ps1`.
- Scratch goes in `output/` (git-ignored, safe to wipe). Finished videos in `final/` stages;
  one folder per video, `NN_<what-it-is>`.
- Reference downloads: `yt-dlp -f "bv*[height<=1080]+ba/b" --merge-output-format mp4` into
  `research/` (NOT with `--print`: it disables the download).

## Session end

Append the summary to `C:\Users\lazar\Desktop\second brain\personal\daily\YYYY-MM-DD.md`
(format in the global CLAUDE.md).
