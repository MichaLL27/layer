---
name: crosspost
description: >
  Post a finished video of any YouTube HQ channel to TikTok and Facebook through this PC's
  automation Edge (Claude-in-Chrome): pick the clip (a path Luka gives, or the next one in the
  channel's crosspost order), drive the real Windows file picker, write the caption, set a
  climax cover frame, post or schedule, share Reels to the
  Page's story, record one ledger row. Accounts, ledger, pick order and caption style come from
  the Crosspost entry of the channel's CLAUDE.md `## Channel facts`. Use whenever Luka says
  "upload this to tiktok/facebook", "post the next one", "crosspost", "sync with youtube", or
  hands over a clip path for the socials.
---

# Crosspost: TikTok and Facebook

YouTube leads; the socials get the channel's videos later, one clip at a time, and catch up over
time. Nothing in `final/` moves: the ledger row is the only record.

## What differs per channel: one entry in `## Channel facts`

Read it in the channel's `CLAUDE.md` first:

```
- **Crosspost:** yes | no | unknown.
  - TikTok: account <@handle>; <what goes there>.
  - Facebook: Page <name>, id <id>; <Shorts as Reels; long-form as a Page video, or never>.
  - Ledger: <path>.
  - Pick: <which clips, in what order, what is skipped, which file of a video>.
  - Cadence: <posts a day, spacing>.
  - Caption: <template, hashtags>.
  - AI label: on | off.
```

- No entry, `no`, or `unknown`: say so in one line and stop.
- An account, or any other part, missing or `unknown`: say which, ask Luka for it, and stop.
  Write his answer into the entry before posting. A platform the entry doesn't name gets
  nothing.

The scripts sit beside this file; the commands below run from the channel root (from the hub:
`<channel>/.claude/skills/crosspost/`).

## 0. Browser (every session)

1. **This PC's Edge, proven local. Never ask which browser; never log in anywhere.** Luka's other
   PC's Edge is connected to the same extension account and also reports `isLocal:true`, and
   display names drift ("Browser 1", "Browser 2", "Automation"): neither is proof.
   a. `Get-Process msedge` empty: `Start-Process msedge` and wait ~8 s BEFORE listing, so the
      other PC is never the only choice.
   b. The channel's `.claude/browser.local` (per PC, gitignored; lines `deviceId=<id>`,
      `computer=<$env:COMPUTERNAME>`, `proven=<date>`). Its `computer` is this PC:
      `list_connected_browsers` → `select_browser` that deviceId, whatever its name (not listed:
      wait, list again). Missing, or another PC's: try each listed browser (known IDs
      `5106415a-4c67-41ec-bbf5-4c2ee6f56cb0`, `7eb920bc-e2a8-4c92-a8d8-75f7032354d9`) with step c
      and write the one that passes into the file (and `.claude/browser.local` into the
      channel's `.gitignore` if it isn't there). Never use a deviceId that hasn't passed step c
      this session.
   c. **Proof:** `navigate` the tab to any https page (a `chrome://` page can't run JS),
      `javascript_tool` → `document.title='LOCAL-<random>'`, then
      `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/verify_local_browser.ps1 -Marker LOCAL-<random>`
      → `LOCAL`. Its `NOT_LOCAL` can be false (a window title shows only its active tab); then
      settle it with localhost: serve a random-named file from the scratchpad
      (`python -m http.server <port> --bind 127.0.0.1`, in the background), `navigate` to
      `http://127.0.0.1:<port>/<file>`, and `get_page_text` must show its content (localhost
      resolves only on the browser's own PC; `file://` is refused). Neither passes: wrong
      browser, select another.
   d. The extension switches instances mid-session (a new tab logged out, tab IDs from another
      range): `select_browser` the SAME proven deviceId again.
2. `tabs_context_mcp createIfEmpty:true`, then work in ONE tab, `navigate`-ing it between TikTok
   and Facebook. It must be the ACTIVE tab of the Edge window (a screenshot of an inactive tab
   times out): `tabs_create_mcp` never activates a tab, so run
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/activate_edge_tab.ps1 -TitleMatch "<words of the tab's title>"`
   and check `ACTIVE:` or `ALREADY_ACTIVE:` (`FOREGROUND_FAILED`: a real mouse click on
   Edge's title bar, as in §3, then run it again).
3. The extension clicks and types without focus; the file picker (§3) foregrounds Edge for ~2 s.

## 1. Pick the clip

- A path from Luka: use it.
- Otherwise the entry's **Pick** order. **Re-read the ledger right before every pick, even
  mid-session**: a session that picked from its own earlier copy posted one clip to TikTok
  twice. A clip goes up once per platform.
- A Short: its 9:16 file (`*_9x16.mp4`, or the folder's only mp4). Long-form only where the
  entry allows it.
- Two clips with the same on-screen caption or the same title shape never go up the same day:
  in the Page's grid they read as one video posted twice.

## 2. Caption and cover

- **Caption:** the entry's template, the same text on both platforms. The first line is the
  title the video went live with on YouTube (the proven hook: the newest Studio content export,
  or the channel's Shorts tab, matched by duration) with its own emoji; don't add more. Not on
  YouTube yet: the recommended title in its `.md`. Every number must be true of the video (its
  `.md`, its scene doc). A reply to a viewer's comment says "A viewer asked", never the
  YouTube @handle.
- **Cover:** the frame the video's `.md` names, else the CLIMAX, ~75-90 % in (the biggest
  number on screen), not the burst after the payoff. A video that empties as it goes (an
  elimination, a cleared field, a closing arena) is near-empty there: take the densest frame
  that still shows progress. The same frame on both platforms:
  `ffmpeg -ss <s> -i <clip> -frames:v 1 <scratchpad>/cover.png`; over 10 MB, add
  `-vf scale=1080:-2`.

## 3. Get the video into the page

The extension's `file_upload` caps at 10 MB, its own synthetic click never opens the OS picker,
and a page-side `fetch` from a local server is blocked. What works on both sites:

1. `find` the page's upload button and keep its `ref`: TikTok `Select video`, Facebook
   `Add video or drag and drop`.
2. Start the picker in the BACKGROUND; it polls for the dialog:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/pick_file_dialog.ps1 -Path "<full path>" -TimeoutS 30`
   (`$env:PICK_DEBUG_DIR = "<scratchpad>"` saves desktop screenshots of each step).
3. `computer left_click` the ref: a trusted gesture, it opens the real dialog, and the script
   fills it with real clicks and presses Open. The script must be polling BEFORE the click.
4. Its output ends `DIALOG_CLOSED`.

When it fails:
- `NO_DIALOG within 30s`, page still on its select-video screen: the first click after a
  `navigate` often does nothing (the ref went stale while the page settled). `find` again,
  restart the script, click the fresh ref. Facebook's `Add video` can ignore the ref twice: a
  coordinate click on the box centre works.
- `FOREGROUND_FAILED` with the dialog open (the taskbar or another app holds focus): take a
  desktop screenshot, make a REAL mouse click (`SetCursorPos` + `mouse_event`) on the dialog's
  title bar, then run the script again with the same `-Path`: it finds the open dialog and
  finishes.
- A rejected or garbled path ("The file name is not valid."), or a dialog left open by a failed
  try: `pick_file_dialog.ps1 -CloseDialogs`, reload the page, start again. The extension's
  screenshot can't see OS dialogs: check them on a desktop screenshot.
- In the dialog never Ctrl+A (keyboard-layout dependent: a Georgian layout typed `a`), never
  SendKeys Alt+N or Enter (keys landing on the file list start a rename), never UIA
  `ValuePattern.SetValue` (times out).
- TikTok offering "Continue editing?" on the upload page holds a draft from an earlier try: if
  it is this clip, continue it instead of uploading again.

**The cover image needs none of this:** it is under 10 MB, so `find` the cover dialog's
`input[type=file]` and give the PNG to `file_upload` directly.

### Clicking in the page

- `find` + click by `ref`. Screenshot coordinates are SCALED from CSS pixels and modals move
  between renders: a stale coordinate typed a caption into nothing and picked 11:55 PM from a
  time list. When a coordinate is unavoidable, compute it fresh,
  `rect * (screenshot width / window.innerWidth)` via `javascript_tool`, and click in the same
  turn.
- Check a click landed (`document.activeElement`) before typing.
- Never Ctrl+A in a page: on TikTok's description and Facebook's time field it selects the
  whole page and the typing goes nowhere.

## 4. TikTok Studio

1. `navigate` to `tiktok.com/tiktokstudio` FIRST, then `/tiktokstudio/upload`: the direct URL
   goes through a login redirect that throws the tab out of the extension's group (leave the
   stray tab open).
2. **Account:** the account TikTok Studio shows (avatar menu) must be the entry's. Another one:
   switch in the avatar menu if offered, else stop.
3. Picker (§3) → wait for "Uploaded (NN MB)". Dismiss "New editing features" (Got it).
4. **Description** (a Draft.js editor): click the box by ref, `End` → `shift+Home` → `Delete`,
   then type. Hashtags one at a time, each WITH a trailing space (`"#asmr "`), `Escape` after
   each: an open suggestion list sits over the Cover box (the next click turned `#asmr` into
   `#asmreating`), and Backspace deletes a whole hashtag. Read back
   `document.querySelector('.public-DraftEditor-content').innerText`: the only proof the
   caption landed (a missed click leaves the file name as the caption).
5. **Cover:** the `input[type=file]` inside "Upload cover" → `file_upload` the PNG → Save. (The
   frame strip expands after the first drag, so drag twice if it is ever needed.)
6. **Settings:** Now (or §6) · Everyone · High-quality ON · comments and reuse allowed ·
   Disclose post content OFF · AI-generated label as the entry says.
7. Checks: music copyright should read "No issues"; a content check still "in progress" doesn't
   block posting.
8. Post → the Posts list. The new row shows "Only me" with a timestamp while TikTok transcodes
   and flips to Everyone by itself; still "Only me" after 15 min: open its privacy dropdown.

## 5. Facebook

**Identity before every post:** the facebook.com composer reads "What's on your mind, <Page>?"
and the top-right avatar is the Page's. A personal profile or another Page: switch to the
entry's Page in the avatar menu; if it isn't offered, stop.

Reel (a Short), `facebook.com/reels/create`:
1. Picker (§3) on `Add video or drag and drop`.
2. Next (the edit screen needs nothing) → Next → Reel settings.
3. Description box → the caption. Public · AI label as the entry says · Publish now (or §6).
4. **Share to story** (Luka, 2026-09-27: every Reel also goes to the Page's story): the row must
   read "Sharing this and future reels". Otherwise Share to story → "Always share reels to
   story" → Save; Facebook keeps it as the Page's default.
5. **Cover:** click the thumbnail's "Edit" ONCE and wait: "Edit thumbnail" takes up to ~25 s
   behind a spinner. Poll with `find` for its file input → `file_upload` the PNG → Save. A
   second click stacks a second dialog (`find` returns two "Edit thumbnail" sections): nothing
   saves after that, so reload `reels/create` and start the Reel again.
6. "Your reel is safe to publish!" → Post. The composer closing to the Reels feed = published;
   check it on `facebook.com/profile.php?id=<Page id>&sk=videos`.

"Edit", "Scheduling options" and the final Post or Schedule button often ignore the first click
while the panel settles: click the ref again, then a coordinate computed fresh.

Long-form, only where the entry allows it: a Page video post ("Upload Video"), not a Reel (a
Reel caps at 3 min). Thumbnail: the video's YouTube thumbnail. Description: its upload text,
with a photosensitivity line only where the build measured flashing.

## 6. Scheduling instead of posting now

The entry's cadence is kept by scheduling; never mass-post a backlog.

- **TikTok:** "When to post" → Schedule → "Allow your video to be saved for scheduled
  posting?" → Allow. The time field is read-only and ignores the wheel: open its dropdown and
  click through the DOM, `span.tiktok-timepicker-option-text.tiktok-timepicker-left` (hour) /
  `.tiktok-timepicker-right` (minute), `scrollIntoView()` + `.click()` on the span AND its
  parent, then Escape. The date defaults to today. **Read the time back off a screenshot**, not
  the DOM (an input scan returns the value from before the click). The button becomes
  Schedule; the Posts list shows the row with a date badge.
- **Facebook:** Reel settings → Scheduling options → Time: `triple_click` the field's ref, type
  `8:30 AM`, click the single `[role="option"]` at coordinates computed fresh from its
  `getBoundingClientRect()` → Schedule for later. The row reads "Today at 8:30 AM" and Post
  becomes Schedule. The toast "Your post was saved as a draft" after a scheduled Reel is wrong:
  it is scheduled. Verify in the Planner, `facebook.com/professional_dashboard/content_calendar/`
  → Month (the Reel's cover and time; a spinner on it = still processing). The Content
  Library's Scheduled tab stays empty, don't use it; its Published tab
  (`professional_dashboard/content/content_library`) gives Facebook views for the ledger.

## 7. After posting

- **One ledger row per clip**, both platforms, newest on top:
  `| # | date | dur | title | TikTok | Facebook | source |`. The running number; when it went
  live (`<date time> (SCHEDULED)` until it has, updated next session); the duration; the
  caption's first line and the cover time; TikTok views with the date read; Facebook views or
  the Reel URL; the file's path under `final/` and its YouTube views. A new ledger starts with
  that header.
- Close the tabs you opened. Never delete a post (Luka's call): flag a weak one instead.

## 8. Facebook Page engagement: replies and text/image posts (Luka, 2026-10-09)

Facebook monetisation has no posting rule, but a Page that talks to its audience reads as a real
one and raises engagement: so besides Reels, the Page replies to comments and posts now and then.
Runs when Luka says "reply to comments", "post something on facebook", or after a crosspost
session. Same Page identity check as §5; the entry's **Page posts** line (if any) sets the topics.

**Replies.** Open the Page's recent Reels/posts (Content Library, Published tab) and reply to the
unanswered comments. Same voice as the YouTube replies (`youtube-upload` §1b): 1-4 words or emoji
only, never the official voice, a question gets a 2-3 word answer (engine, tune), match their
energy, vary loosely, **never promise a video**, skip spam/hate/arguments. React (like) to every
comment we reply to. A video idea in a comment goes to the channel's `research/comment-ideas.md`
as on YouTube (link = the comment's permalink).

**Posts** (Page feed, "What's on your mind"), about 1 a day at most, never on the minute of a Reel:
- Kinds, rotated: a question to the audience ("What's your favourite sim? 👇", "Drop a sim idea
  and it might be next"), a short fact about simulations, physics or maths (true, one or two
  lines, no claim we can't stand behind), a still from one of our own videos with a one-line hook.
- **An image goes with it where it helps:** our own frame (`ffmpeg -ss <s> -i <clip> -frames:v 1`,
  from a clip already posted) or a simple graphic we draw ourselves. Never someone else's image.
  Upload through the composer's photo button with `file_upload` (images are under 10 MB).
- No hashtags beyond 1-2, no links to other platforms in the first line, no engagement bait
  ("like if", "share to win").
- **Never the same post twice:** re-read the ledger's Page-posts table before writing one, and
  add a row (`| date | kind | first line | image |`) after posting.
- Questions we ask are answered by §8 replies in the next session.
