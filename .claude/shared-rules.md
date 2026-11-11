> **Copy of the shared rules**, synced version `76b4ea8f136d`. The master is
> `_hq/shared-rules.md` in YouTube HQ, on Luka's PC. A rule for EVERY channel may be
> changed right here: `_hq/tools/sync_rules.py` merges the change into the master and every
> other channel (on Luka's PC by itself; from another PC once this repo is pushed and Luka
> pulls it). A rule only this channel needs goes in its own `CLAUDE.md`, not here.

# Shared rules: every channel

The rules every YouTube HQ channel follows: Satisfying 2D Sims, Satisfying 3D Sims, Particle
Factory, Bot League, Toy Tempo (folder `kids-cars`), and every channel started later.

- **One master, a copy in each channel.** The master is `_hq/shared-rules.md` in the hub; each
  channel loads its copy, `.claude/shared-rules.md`, through the line `@.claude/shared-rules.md`
  in its `CLAUDE.md`, so the copy travels with the channel's git repo to every PC and person.
- **A shared rule can be changed from anywhere** (Luka, 2026-10-06): the master or any copy.
  `_hq/tools/sync_rules.py` merges it into the master and every channel (on Luka's PC by itself;
  from another PC once the repo is pushed and Luka pulls). Write it as a dated rule for EVERY
  channel; what one channel alone needs goes in its `CLAUDE.md`.
- **A channel's own `CLAUDE.md` holds what is that channel's alone.** It may add detail or be
  stricter; where it differs on purpose (ratios, lengths, audience, 2D's and 3D's older
  layouts) it says so, and that wins for that channel.
- Most rules below are how Luka wants to work; Anka and Luka's brother can set their own
  working preferences in their channel's `CLAUDE.md`.

## Who's who

- **Luka** owns the channels, reads the analytics and gives direction. He runs Particle
  Factory, Bot League and Toy Tempo himself.
- **Anka** (Luka's girlfriend, git `AnkaTsutskiridze`, branch `anka`) makes Satisfying 2D Sims.
- **Luka's brother** (git `MichaLL27`) makes Satisfying 3D Sims.
- Every project is worked on from more than one PC.

## Answering

- **Link every file you hand over** (Luka, 2026-10-06): a video, clip, sheet, audio, `.md`
  note or report is a clickable markdown link, never a bare path or a path in backticks:
  `[03_red_fire_truck.mp4](final/ready-to-upload/shorts/from_01_colour_dip/03_red_fire_truck/03_red_fire_truck.mp4)`.
  Relative to the folder the session was opened in (in the hub, starting with the channel
  folder); main deliverable first, one link per file plus their folder; in the last lines of
  the reply; where the file is now, after any move.
- **Every question he asks is answered in the LAST reply** (Luka, 2026-10-07). He reads only the
  last reply, not the session. Answer a question as soon as you know the answer, AND answer it
  again, plainly, in the final reply of that turn: first, before anything else, and again in the
  overview. A question asked mid-session gets the same treatment. A question still open (waiting
  on him, or on work) is named in the final reply too. An answer that sits only in the middle of
  the session is a missed answer.
- **End with a short overview** (Luka, 2026-09-21): any length the answer needs, then a few
  closing lines that stand alone (the answers to his questions, what changed, what's next). He
  often reads only the end; in a hurry, give only that.
- **Read a brief for intent.** Luka dictates long briefs in non-native English and
  speech-to-text swaps words. Act on what he means; ask only when two readings lead to
  different work.
- **Correct him when he's wrong.** He wants it ("if am wrong tell me"): say so plainly, with the
  number or the frame that shows it, then do what he decides.
- **Unsure? Say so and measure.** Probe the pixel, file or log before theorising; re-measure a
  limit rather than quote an old doc.
- **Say a limit once.** You can't listen to audio: say so once, measure what can be measured,
  never suggest the audio may be bad on that basis. A pasted image can't be saved as a file:
  when you need the file, ask for it in your first reply.

## Doing the work

- **Make what was asked, exactly.** One video when he says "make X", or the count he names; no
  unasked variants. Options belong to titles and thumbnails: give several, he picks.
- **Ask only what blocks the next step.** If the answer doesn't change what gets built next,
  decide, and park the question in the channel's plan.
- **Change only what was asked.** One named defect = one minimal change; everything else stays
  identical. A nearby improvement is a one-line suggestion, never folded in. Unclear wording:
  the narrow reading.
- **Never remove a visible feature as part of a fix: ask first**, with the tradeoff; prefer the
  fix that keeps it.
- **Luka overrides any rule.** Rules are compressed past evidence, not law. When he asks against
  one, flag it in ONE line (the rule and what it says), then build it fully: no hedging, no
  re-arguing, no re-raising. A per-video override applies to that video only (note it in its
  `.md`). A rule changes only when he says so; then change it where it lives, with the date.
- **Once he decides, build lean.** Correct a wrong fact in one line, with the newest number and
  its date; no risk lists, no re-opening a settled point.
- **A new version never replaces the current one.** Build it beside it, own folder and files;
  the current one stays untouched until he picks.
- **New rules are for new work.** A published video can't be replaced: never re-render or
  rebuild uploaded videos for a new rule without asking.
- **A background or batch agent never exceeds the scope it was given.**
- **One home per fact** (Luka, 2026-10-01: the same thing in several files is "duplication, too
  much work"). Check a file doesn't already say it before writing it. Where a video is = its
  folder.
- **"Uploaded" is one move plus clearing its scratch, nothing else** (Luka, 2026-10-01 in Bot
  League; every channel 2026-10-07). When Luka says a video is uploaded (or to move it): move it
  from `ready-to-upload/` to `uploaded/` (the whole folder; a Short of a `from_NN_<long>/` group
  into that group's folder there), delete its own files in `output/` (renders, frames, audio,
  JSON, by its stem, label or seed; what can't be told apart waits for the clean-up), answer in
  one line with its new link. Nothing else changes: not its `.md`, `PLAN.md`, `CLAUDE.md`, a
  log, a memory, the hub roster or a sheet; don't ask which title or thumbnail he used. The
  `uploaded/` folder IS the list. An upload through a channel's upload skill: the one ledger row
  its tool writes is the only record.
- **A rule change cleans up after itself** (Luka, 2026-10-07): when a rule is added or changed,
  remove or boil down the leftovers it makes stale, repeated or contradicted (the channel's
  `CLAUDE.md`, `PLAN.md`, `docs/`, skills, memories; the hub's roster and sheets) in the same
  change. Every line in a file Claude loads costs tokens in every session.
- **Several sessions share one checkout.** Edit a shared file with small exact-string patches,
  never a whole-file write from an older read. A build broken by files you didn't touch: don't
  fix them; wait, or say so.

## When Luka is away (overnight, a batch)

- **A night session runs by the shared `overnight` skill** (Luka, 2026-10-07): questions only
  before START (15 minutes, then the defaults), a night log to resume from, an hourly reminder,
  shutdown only when his prompt asks for it.
- A request for several videos at once is unattended too: no questions, choose yourself, finish
  each video to the end before counting it (a channel's batch skill may bake the next one while
  one renders), every decision (and every rule it overrode) logged under `docs/`, one report at
  the end with a link to every file.
- Everything made unattended is provisional: it waits in the review folder for his verdict.

## Before you hand a video over

- **Watch it yourself first.** Frames across the whole video (a sheet, one every ~2 s), full
  size and, for a Short, in the 9:16 crop: subject in frame throughout, nothing hidden or
  broken. Frame 0 of a Short is its cover.
- **Verify what the video is ABOUT**, at the right scale and frames, out of the encoded file:
  zoom 1:1 on the busiest region, step to an event's exact frame. Detail is lost in space and
  in time; check both.
- **A check that shares the builder's assumption is not a check**, and a check never seen to
  fail isn't known to work.
- **A change to a batch of finished videos: re-render ~3 first**, picked to stress different
  cases.
- **Stage it, then present it.** A video awaiting a verdict sits in the review folder
  (`final/needs-review/` on the new channels), never only in `output/`.
- **A loop ships single.** A 2x or 3x repeat is only a private seam check.
- **One metadata file per video, written when the video is staged** (Luka, 2026-10-07): exactly
  ONE `.md` beside it, named like it (`NN_<name>.md`), written as it enters a review folder, so
  nothing reaches `ready-to-upload/` without it. The title options also go in the reply. Two
  parts, in order:
  1. **Upload**: only what Luka pastes into YouTube. **Title**: 3-4 options of different kinds,
     recommended first, none offered twice in the channel; a Short's carry emoji (next rule).
     **Description**: in a code block. **Pinned comment**. **Tags**. A field the channel leaves
     empty on purpose is one line saying so (2D's and 3D's Shorts: no description, no tags;
     Particle Factory: no tags).
  2. **Notes (not for upload)**: only what a later session needs and no other file says: the
     rebuild command, seed, version, the cover frame, an open problem. A few lines.

  Not in it: what the channel's `CLAUDE.md` says once (Studio settings, audience rule, licence
  warnings), check results (a failing video isn't staged), drafts, history, "after upload"
  to-dos. A Short's file is about 20-30 lines.
- **Every Short title carries 1-3 emoji, never zero** (Luka, 2026-10-07; proven on 2D and 3D),
  on all 3-4 options. Ones the video earns, from its subject and what happens (💀 a chaotic
  ending, 🤯 a number nobody expects, 🎵 the tune, the subject's own emoji), at the end or
  beside the word they amplify; never a decorative string or one that contradicts the video.
  Long-form takes emoji only when the title calls for one; a channel may keep long-form titles
  emoji-free (search phrases: 2D's and 3D's megamixes, Particle Factory's long-form).
- **Flashing is measured, never eyeballed.** A photosensitivity warning goes only where a
  measurement found flashing, never as a blanket line.

## Music, sound and reused content

- **Music is ours to use.** A public-domain composition (composer dead 100+ years, or clearly
  traditional), synthesis, or a track we made, always in OUR OWN recording, never anyone else's
  recording of a free song. Sound effects: CC0 or ours. A non-public-domain tune Luka names
  himself is built without a warning and stays out of long-form.
- **Never the same video twice**, on one channel or across channels: vary the subject, the set,
  the song. Never reuse another channel's footage or import its code (patterns may be copied).
- **Never reuse a title, or a title shape, close together.** Check the last ~6 public titles.
  A reused one collapsed impressions once on 2D, and 3D was demonetised once (appeal won by
  showing the work, 2026-09-19): YouTube's "inauthentic content" policy targets templated,
  repetitive uploads.
- **Shorts cut from one video go up on different days with different titles.**
- **A reference is an example, not a target**: take the mechanic, make the visuals ours (Toy
  Tempo stays closer: very close, never a copy). Download a reference with yt-dlp (on Luka's PC
  `python -m yt_dlp`: the exe on PATH is older and 403s), never web-fetch it; read it as frames.
  A competitor's brand words never go on screen.

## YouTube: Studio, Shorts, thumbnails, channel art

- **Check the channel before any Studio action.** Luka has several: match the channel ID.
- **Use this PC's own automation browser**, proven local (Claude in Chrome also lists Luka's
  other PC); never log in anywhere. Pulling analytics is read-only on YouTube.
- **The bottom of a Short belongs to YouTube.** Title, channel name and buttons cover about the
  bottom fifth; the like/comment/share column the right edge from mid-height down. Keep text and
  action clear of both. A Short's title shows about 40 characters.
- **Thumbnails:** every element 60+ px inside the edge of a 1920x1080 master; nothing important
  in the bottom-right ~200x110 px (the time badge); a number on it is read off a frame, never
  computed. Many variants on one sheet: Luka compares and A/B-tests the best on YouTube.
- **Channel art:** an avatar is 800x800, shown as a circle, must read at 98 px; only the centre
  1546x423 of a banner shows on every device. Luka uploads channel art himself.

## Analytics

- **Analytics before opinions.** Every claim about a channel cites a dated number (a Studio
  export in `reports/exports/<date>/`, a report in `reports/`, or vidIQ); write the date next to
  every figure; check for a newer one before citing an old one.
- **Rates are evidence, counts aren't.** Never target a raw count or argue from one video's
  views: compare medians over a real number of videos.
- **No theory without data from his own channel.** A guess, his or yours, is a prediction until
  a number backs it.

## What every channel has learned (2D and 3D agree)

- **Long-form earns, Shorts grow.** Shorts RPM ~$0.07; megamixes ~$1.7-2.4, marathons
  ~$5-6.6. On 2D, long-form was 90.5% of lifetime revenue from 26.4% of the views (2026-09-09).
- **Retention is the lever.** On 2D, stayed-to-watch predicts Shorts reach (r=+0.59) and every
  breakout was above 65% stay. The mechanic must fire by second 5.
- **Sound and the first frame matter more than titles.** The audience is global and mostly
  doesn't read English. On 3D, recognisable melodies drive the channel.
- **Escalation formats work:** a count that climbs, bigger each clip.

## Git, files and PCs

- **No git commit or push unless asked, and no AI co-author trailer** (no `Co-Authored-By`
  line). Leave finished work in the working tree and say what's ready.
- **No absolute paths in shared files.** Repos live on several PCs: scripts find their root from
  their own location. Ask before rewriting a path.
- **Knowledge lives in the repo.** Memory is a one-PC cache: a durable rule goes in the
  channel's `CLAUDE.md`, `docs/` or a skill; a rule for every channel in the hub's master of
  this file.
- **Shared skills** (Luka, 2026-10-07): a skill whose steps are the same in every channel lives
  once in the hub (`_hq/skills/<name>/`) and is synced into every channel's `.claude/skills/`
  both ways, like these rules (`.claude/skills/.shared-sync.json` records the synced versions:
  never edit it). Shared: overnight, youtube-upload, youtube-analytics, crosspost,
  comment-video. A skill that depends on how a channel builds videos stays its own (make-video,
  megamix, marathon, redesign, reladder, ship-clip). Per-channel facts (IDs, accounts, paths,
  which skill makes one video) come from the `## Channel facts` section of the channel's
  `CLAUDE.md`; a missing one is asked for once and written there.
- **API keys live in a git-ignored `.env`**, never printed, never committed.
- **`output/` is scratch: nothing important lives there** (Luka, 2026-10-07): only temporary
  data of a video being made (renders, frames, audio, JSON, sim dumps), so it can be wiped
  anytime without loss; whatever must survive lives where it belongs (`final/`, `sound/`,
  `devlog/`, `tools/`, the docs). An upload clears that video's files (the upload rule). **On
  Luka's call** ("clean output"): delete what belongs to uploaded videos plus junk (old tests,
  broken or replaced renders), keep what belongs to a video in review or ready to upload, report
  the space freed. Never unasked. A video's master stays until that video is uploaded
  (re-captioning or re-cropping needs it; a sim may not re-run identically), then goes too.

## Renders and the PC

- **Luka uses the PC while it renders.** Every build runs BelowNormal and leaves threads free,
  counted from the PC, never hardcoded (8+ threads: 2 free; 4-7: 1). All threads only when Luka
  says the run is overnight; a session resumed after he wakes drops back.
- **One GPU render at a time across all channels** (Luka, 2026-09-21): every Unity/engine call
  is wrapped in `tools/gpu_lock.ps1`, the named mutex `Global\YouTubeGpuRender`, so another
  channel's render waits its turn. Each repo keeps its own copy of the script; the name is the
  contract. A render gets the whole GPU (Luka, 2026-10-05).
- **Unity renders on its own Windows desktop** (`tools/desktop_isolate.ps1`), or the editor
  steals Luka's caret. The cost: an unexpected modal dialog is invisible and hangs the build.
- **Two renders writing the same output path corrupt the mp4**, and ffmpeg still exits 0: one
  render per output name at a time.

## Windows, PowerShell and ffmpeg traps

- **PowerShell:** an unquoted comma list becomes an array (quote it, or `[string[]]`); a
  trailing empty-string argument can vanish (`@('--flag','')`); never reuse a parameter's name
  in any case; no non-ASCII inside `.ps1` strings; never `Get-Content -Raw | Set-Content` a
  source file; never merge native stderr (`2>&1`, `*>`) under `$ErrorActionPreference='Stop'`,
  and `2>$null` hides real crashes.
- **Bash tool:** a heredoc mangles quotes and doubled backslashes: write the script with the
  Write tool, then run it. Run a `.ps1` with the PowerShell tool.
- **Timeouts:** a background command dies at its timeout; give a 4K render 2,400,000 ms or more.
- **Line endings:** working copies are CRLF, so a byte-level patch that assumes `\n` misses.
- **ffmpeg:** map streams explicitly when re-muxing (`-map 0:v:0 -map 1:a:0`), drop `-shortest`,
  then extract the audio and compare; yuv420p needs even dimensions; `overlay` can drop frames
  at a junction (`-fps_mode cfr`). Loudness is measured on the muxed mp4, in stereo.

## Standard layout for new channels (Luka, 2026-09-27)

Every channel started after the two sims channels: Particle Factory, Bot League, Toy Tempo,
every new one. **Satisfying 2D Sims and 3D Sims keep their older layouts on purpose** (skills,
scripts and Anka's and his brother's habits depend on them): don't restructure them; their
differences (3D's `need-review/`, `concepts/`, masters in `output/` until upload, loose scripts
in their roots) are known and stay.

```
<channel>/
  CLAUDE.md  PLAN.md  .gitignore        the only files in the top folder, plus the one or two
  run.ps1 (or the channel's entry script)  entry scripts that were always there. Nothing new:
                                        new scripts go in tools/, notes and logs in docs/
  .claude/skills/  .claude/shared-rules.md (this file's copy)
  <Name>_Unity/  lib/  scenes/  tools/  code
  channel/
    avatar/     every profile picture ever made, one file each, + current.png = what is live
    banner/     every banner, + current.png. Older versions of a picked design: not-picked/
    README.md   which is live, since when
  docs/         how things work, scene notes (scenes/), work logs, upload notes
  final/
    needs-review/     longform/ shorts/   a new video waits here for Luka's verdict
    ready-to-upload/  longform/ shorts/   approved
    uploaded/         longform/ shorts/   on YouTube
    new-concepts/     tests of unproven ideas, awaiting a verdict
    megamix/ marathon/                    only a channel that makes them
  devlog/       proof of authorship (rule below)
  sound/        music/ sfx/: the source audio
  research/     competitors, reference videos, thumbnails/, comment-ideas.md
  reports/      analytics (reports/exports/<date>/), created by the first pull
  output/       scratch, gitignored, safe to wipe
```

- **One folder per video**, in every stage: the mp4(s), the `.md`, and `thumbnail/` for
  long-form. A video moves as a whole folder.
- **Video names: `NN_<what-it-is>`** (Luka, 2026-09-28: handy at 100+ videos). Two digits, the
  NUMBER FIRST so folders sort in upload order, counted separately for long-form and Shorts;
  files inside named like the folder (`03_star_wheels/03_star_wheels.mp4`, `.md`; two ratios add
  `_16x9` / `_9x16`). The number is given when the video is made (the next free one across all
  stages) and never changes; a test in `new-concepts/` has none until made for real. No "short"
  in a Short's name, no pipeline or engine in a name; the part after the number is the
  channel's own (Bot League keeps the fight's seed: `03_nibbles_vs_glare_s3081`). **A tool
  names it, never a hand**: Bot League `tools/make_short.ps1` / `make_tournament.ps1` /
  `make_royale.ps1`, Particle Factory `tools/stage_video.py`, Toy Tempo `tools/make_dip.ps1` /
  `make_dip_short.ps1`.
- **A small data file that records the video** may sit next to it (Bot League's `_events.json`:
  every hit, on its frame). Never audio, sim dumps or render leftovers: those live in `output/`.
- **`longform/` and `shorts/`** once a channel publishes both.
- **Shorts cut from one long video share a folder** (Luka, 2026-10-03):
  `shorts/from_<NN>_<long>/`, e.g. `final/ready-to-upload/shorts/from_01_colour_dip/03_red_fire_truck/`.
  The Shorts inside keep their own numbers among the Shorts; a Short made on its own sits
  directly in `shorts/`. The group moves between stages as its Shorts do.
- A channel without Shorts, a megamix or a sound folder simply doesn't have that folder; what
  does exist has these names.
- **Check it:** `python _hq/tools/check_layout.py [channel]` in the hub (`python
  ../_hq/tools/check_layout.py <channel>` from a channel) lists misnamed videos, loose files,
  audio in `final/`, reused numbers, missing channel-art folders and oversized devlog clips.
  Run it after staging or moving videos; a new channel gets a line in its `CHANNELS` table.

## devlog/: proof of authorship, not storage (Luka, 2026-09-27)

If YouTube ever asks whether the videos are ours (a reused-content review, a monetisation
appeal), `devlog/` plus the openable engine project plus the git history show how the channel
was made.

- **Keep:** the comparisons Luka chose between (looks, materials, machines, HUDs, sounds,
  channel-art directions): the sheet of options, the compare video, the before and after, a few
  steps that show how a look was built. Copy it in BEFORE the next change replaces it: a render
  can rarely be made again on a later sim version.
- **Don't keep:** full videos or their old versions (the upload is in `final/uploaded/`), every
  render of a step, broken renders, or files whose home is elsewhere (avatars and banners in
  `channel/`, rejected ones too; thumbnails in the video's `final/` folder; thumbnail research
  in `research/thumbnails/`).
- **Copy in, never move out.** A file that belongs to `channel/` or `final/` stays there; devlog
  gets a copy or a sheet of it.
- **Video size:** a clip showing ONE option is saved at 1080p
  (`ffmpeg -vf scale=1920:1080 -c:v libx264 -preset slow -crf 23 -c:a copy`, ~25 MB for 21 s);
  a compare tiling several options in one frame stays 4K, so each tile stays readable.
- One folder per step, `YYYY-MM-DD_what/`; `devlog/README.md` gets one entry per folder: what
  was compared, what Luka picked, the date, a line per file.
- `*.mp4` / `*.wav` are not in git and exist only on the PC that made them: back `devlog/` up
  if it is to serve as proof.
