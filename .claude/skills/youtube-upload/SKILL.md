---
name: youtube-upload
description: >
  Upload finished Shorts of any YouTube HQ channel to YouTube through this PC's automation Edge
  (Claude-in-Chrome) and SCHEDULE them into the channel's window around whatever is already
  scheduled or published: the Studio upload form end to end (title, the channel's description
  and tags rule, audience, ad-suitability rating, related video, schedule), the collaborator
  channel invited and accepted, then the pinned first comment once each goes live (plus short
  casual hearted replies to viewer comments, saving every video idea viewers drop into the
  channel's ideas file), one ledger row and the move to final/uploaded/.
  Channel, schedule, audience, collaborator, pinned comment, ledger and folders come from the
  Upload entry of the channel's CLAUDE.md `## Channel facts`. Use whenever Luka says "upload
  these", "upload 5 videos", "schedule these shorts", "put these on YouTube", "pin the
  comments", "reply to comments", or hands over a list of clips for YouTube.
---

# YouTube upload: scheduled Shorts

The browser part is this file; everything mechanical (metadata, order, slots, ledger, the move)
goes through `yt_upload_prep.py` beside it, so it comes out identical every run. `UP` below =
`py -3 .claude/skills/youtube-upload/yt_upload_prep.py` run from the channel root (`python`
works too; it finds its channel from its own location). Studio quirks below were found on
Satisfying 2D Sims and 3D Sims from 2026-09-15 on; they hold on every channel until a run says
otherwise.

## What differs per channel: one entry in `## Channel facts`

Read it in the channel's `CLAUDE.md` first; `UP facts` prints it as the tool reads it.

```
- **Upload:**
  - Channel: `<name>`, `<UC... id>`, `<@handle>`.
  - Scope: <what this skill uploads, e.g. Shorts only, the 9:16>.
  - Schedule: `<HH:MM HH:MM ...>` <Area/City> (UTC+N)[, <N> a day][, <M> min apart] | unknown.
    The pace is the channel's own and grows with it (a new channel 1 a day, later 3, later 5):
    when Luka changes it, rewrite this line: the times, and N a day (default: as many as the
    times listed; the gap defaults to 45 min).
  - Made for kids: no, the channel default (don't click) | no, set on every upload | decided per upload | unknown.
  - Collaborator: `<name>`, `<UC... id>`, `<@handle>` | none | unknown.
  - Text: title + `#<tag>`, its only hashtag; description and tags empty on a Short | as the video's .md writes it.
  - Pinned comment: `<line 1>` + `<line 2>` (fixed: each backticked part is one line) | the video's .md.
  - Settings: <other fields set on every upload> (optional).
  - Ledger: `<path>` | unknown.
  - Video: pair | compilation | folder: <where a video and its metadata sit>.
  - Uploaded: `<final/uploaded/...>`; `<folder>/` -> `<dest>`; non-PD music (`<guard.py>`) -> `<dest>` (both optional).
  - Queues: `<folder>/` (<its rule>), ...; these stay when emptied (optional).
  - Order: family | machine-subject | unknown: <how look-alikes are kept apart>.
```

No entry, or Channel id `unknown`: ask Luka once (confirm the channel in Studio with him), write
it in, go on. A part `unknown` that a step needs (Schedule for `slots`, Ledger for `record`,
Made for kids, Collaborator, Scope): ask Luka once and write his answer into the entry (shared
rules, "Shared skills"); the tool refuses rather than guess.

**Video layouts** (`UP meta` resolves a path, the `.md`, or for compilation/folder the name):
- **pair** (2D): `<stem>_9x16.mp4` + `<stem>_16x9.mp4` in `final/ready-to-upload/` or a
  subfolder, `<stem>.md` beside them; its titles come as `1. **Type** — <title in backticks>` or
  `1. title (type)`. `record` moves the pair and deletes the `.md`.
- **compilation** (3D): `final/ready-to-upload/compilation_<name>_9x16.mp4`; metadata
  `docs/uploads/compilation_<name>.md`, the record, never deleted, parsed by the channel's own
  `tools/make_upload_txt.py` so the title typed is the staged `.txt`'s (fix a bad parse THERE and
  both follow); the option marked *recommended* is flagged; a `_noloop`/`_hook` variant uses its
  parent's `.md`. `record` moves the pair and deletes the staged `compilation_<name>.txt`.
- **folder** (the standard layout): one folder per video,
  `final/ready-to-upload/<longform|shorts>/[from_NN_<long>/]NN_<name>/` with `NN_<name>.mp4` (or
  `_9x16`/`_16x9`) and `NN_<name>.md`, whose `## Upload` part holds the title options
  (recommended first), description, pinned comment and tags. `record` moves the whole folder to
  the same place under `final/uploaded/`.

**Scope.** Shorts only means the 9:16 (the 16:9 is never uploaded standalone). Long-form has not
been run through this skill yet: where an entry's Scope first includes it, do the first one with
Luka watching and write its steps here.

## 0. Browser and channel (every session)

1. **This PC's Edge, proven local:** `.claude/skills/crosspost/SKILL.md` §0 step 1 (a-d)
   exactly; for the proof, navigate the tab to `https://studio.youtube.com` (a `chrome://` page
   can't run JS). Never ask which browser: `isLocal:true` is not proof (Luka's other PC reports
   it too and was picked by mistake once, opening Studio on the wrong machine). Then
   `tabs_context_mcp createIfEmpty:true`.
2. **Keep the working tab ACTIVE.** The extension's new tabs are not active, and an inactive tab
   breaks things silently: its screenshots time out (CDP 30 s), comments don't render, the
   renderer stalls, and the OS file picker reaches only the active tab (a picker run on the wrong
   one still "succeeds", into a tab the extension can't drive). Before every picker call and
   after any screenshot timeout: `powershell -NoProfile -ExecutionPolicy Bypass -File
   .claude/skills/crosspost/activate_edge_tab.ps1 -TitleMatch "<words of the tab's title>"` →
   `ACTIVE:` or `ALREADY_ACTIVE:` (`FOREGROUND_FAILED`: crosspost §0 step 2; after the extension
   navigates a tab, Edge's title can still read "New tab"). It cycles Ctrl+Tab every 600 ms,
   which missed a match once (1.5 s per step was reliable): `NO_TAB_MATCHING` with the tab open
   → run it again. A tab once came back with a new tabId: `tabs_context_mcp` again.
3. **Confirm the channel** (Luka has several). Open
   `https://studio.youtube.com/channel/<ID>/videos/short` and check BOTH: the URL still carries
   `<ID>` after load (Studio redirects to the active channel, or shows "Oops, you don't have
   permission to view this page") AND the sidebar / avatar menu shows the entry's name. Wrong
   channel (avatar → Switch account, or directly):
   `https://www.youtube.com/channel_switcher?next=%2Faccount` ("All channels"; wait
   ~7 s; plain `youtube.com/account` did not list them) → `find` the entry's channel
   (`ytd-account-item-renderer`, "<name> (<handle>)") → click → `navigate` to Studio on its own
   (inside `browser_batch` right after the switch it was refused "domain not allowed";
   navigating before the switch lands gives "Oops") → check again. No password: the channels are
   brand channels of one Google account. Ask Luka first if another channel's session may be using
   this browser right now. The same identity check on youtube.com before posting a comment (§1).
4. **Extension site permissions are per install:** a fresh install answers "Permission denied for
   this action on this domain" until Luka allows studio.youtube.com and youtube.com in it.

## 1. Pinned-comment pass: FIRST, every session

YouTube refuses comments on a scheduled video until it is live, so the pin comes later. `UP due`
lists the ledger rows now live whose comment is still pending (a row whose pin isn't the entry's
fixed comment carries its own `comment`). For each, one video at a time:

1. Open `https://www.youtube.com/shorts/<ID>` → the Short's comments button (`find` "View
   comments", or `button[aria-label^="View "][aria-label$=" comments"]`) → the panel lists the
   threads (`ytd-comment-thread-renderer` in the older layout; in the newer one read the expanded
   `ytd-engagement-panel-section-list-renderer` text). Closing the panel re-centres the video and
   moves the button: `find` it again rather than reuse coordinates.
2. **Existing pin first:** a thread carrying `#pinned-comment-badge` ("Pinned by ...") is pinned,
   and pinning ours REPLACES it (the dialog says so). Ours already pinned → skip, just mark it.
   Ours already posted but not pinned (an earlier pin that failed) → pin THAT one, never post a
   second. Any OTHER pinned comment → leave it and ask Luka. The panel can show a COUNT with ZERO
   threads (no spinner; reopening doesn't help): settle it in Studio,
   `studio.youtube.com/video/<ID>/comments` with its default "Response status: Unresponded"
   filter removed ("No comments found" = nothing to replace); the panel still takes our comment.
3. **Identity:** the comment goes out as the entry's channel. Older layout: click `#avatar-btn` →
   `ytd-active-account-header-renderer` must read "<name> / <handle>" (Escape doesn't close that
   menu, clicking the avatar again does; it doesn't block the next steps). Newer layout
   (`#avatar-btn` gone): `button[aria-label="Account menu"]`, read the identity from the popup's
   text.
4. Click `#simplebox-placeholder` → type line 1 → `shift+Return` → line 2 (and so on). The text:
   the row's own `comment` from `due` when it has one, else the entry's fixed Pinned comment (a
   `## Pinned comment` in a 2D or 3D video's `.md` is NOT used), EXCEPT on a comment-reply video
   (shared skill comment-video; its `.md` Notes say so): there pin the `.md`'s own comment, which
   @-mentions the viewer (Luka, 2026-10-07). `record --own-comment` puts that comment in the
   ledger at upload time, so `due` hands it over even after the `.md` is gone (2D deletes it). On
   a channel whose Pinned comment is "the video's .md", every row carries its own.
   Read back `#contenteditable-root`: its innerText dropped the emoji in the older layout and kept
   it in the newer one, so check the emoji on a zoomed screenshot. Submit: click
   `ytd-commentbox #submit-button button` (a JS click works with the menu open), or in the newer
   layout the button whose text is `Comment`.
5. The new thread lands on top, author = the handle → `find` its "Action menu" (⋮) → click →
   `find` the **Pin** menuitem (the popup reads Pin / Edit / Delete) and click it BY REF. **Delete
   sits right under Pin:** never click by coordinates, and a JS `menu item` query matched the
   SIDEBAR instead of the popup.
6. Dialog "Pin this comment? If you already pinned a comment, this will replace it" → `find` its
   Pin button → click → a "Comment pinned" toast and "Pinned by <handle>" (`#pinned-comment-badge`).
7. `UP pinned --url <url>` (ledger rows only).

**The first Pin click often doesn't register.** On 2 of 3 videos, clicking Pin 1 s after opening
the ⋮ menu reported success but opened no dialog (the menu stayed open); the same ref clicked
again brought the dialog both times (another run: first click, 1 of 1). Always `find` the dialog
before assuming the pin happened. Once, in the newer layout, Pin never opened it: the ref twice
and a scoped JS click (`ytd-menu-navigation-item-renderer`, role menuitem) only closed the menu.
Then stop (no blind coordinate clicks, Delete is right below); untried: a real coordinate click on
Pin after a `zoom` on the popup. The comment stays posted, the row stays pending; next pass pins
that comment (step 2).

**One tab at a time.** Comments don't render in a background tab (skeleton rows until it is
screenshotted into view), and typing or clicking in a second tab mid-flow can close the first
tab's menu. Finish one video's post + pin before touching the next.

**Claude Code's auto-mode classifier can refuse posting the comment** (an external write) when the
request was only "upload": ask Luka to confirm the pin pass explicitly. Also run this pass on its
own when Luka says "pin the comments".

## 1b. Replying to viewer comments: casual, short, hearted

DEFAULT (Luka, 2026-09-26): with a Short's comment panel open (after its pin, or on its own when
Luka says "reply to comments"), reply to viewer comments the way a real creator does. **NEVER the
official voice**: no "Thank you so much for watching! We're glad you enjoyed…", no multi-sentence
answers.

- **1-4 words, or emoji only.** "😂😂", "right?? 💀", "it never ends 🔥", "haha exactly".
- **A question gets a 2-3 word answer**, not an explanation: "how is this made?" → the channel's
  engine ("Python code 🙂" on 2D, "Unity physics 🙂" on 3D); "what song?" → the tune ("Mountain
  King 🎵", "Ode to Joy 🎵").
- **Match their energy and their emoji:** a joke gets a joke or 😂, "wow" gets 🔥/🙌. If the
  comment calls for 🔥, use 🔥 even if the last reply did.
- **Vary loosely, not by rule.** Don't paste one reply under every comment (reads as a bot), but
  never avoid an emoji just because it was used before.
- **HEART EVERY COMMENT WE REPLY TO** (the heart button under the viewer's comment; confirm it
  turned red on a screenshot).
- **Skip** spam, hate, and anything that would need an argument: no reply, no heart.
- **Never promise a video** ("coming soon 👀"). A video request is an idea: save it (below); Luka
  may pick it for a comment-reply video (shared skill comment-video).
- The identity check of §1 step 3 before the first reply.

**Ideas from the comments: save every good one** (Luka, 2026-10-07). Read every comment you see
in §1/§1b (and in any other comment work) for a VIDEO IDEA: a concrete mechanic, setting, twist or
request that could become a video on this channel ("do it with 1,000 balls", "try a square
arena", "what if the crusher was lava"). **Most sit as replies under our pinned "Drop a simulated
idea 👇" comment:** open those replies on every video whose panel is open, and include replies to
our comment in the inbox stop. Not ideas: praise, jokes, plain questions, spam, hate, impossible
or off-channel asks. For each good idea:
- **Save it** in the channel's ideas file: the Upload entry's `Ideas:` line, else
  `research/comment-ideas.md` (create it with the heading `# Video ideas from comments` and the
  line "Newest first. Luka picks; a picked one becomes a comment-reply video."). Newest first,
  one entry per idea:
  ```
  ## YYYY-MM-DD · @handle · on "<video title>" (<video link>)
  > <the comment, word for word>
  - Link: https://www.youtube.com/watch?v=<video id>&lc=<comment id>
  - Idea: <one line: what the video would be or change>
  ```
  The comment's own link comes from its timestamp (on youtube.com) or its ⋮ menu (in Studio);
  without the comment id, the video link plus the handle. Already in the file (same commenter,
  same idea): don't add it again.
- **Heart it and reply** in a few words like any comment ("ooh nice 👀"), never a promise.
- **Report it:** the session's reply lists the ideas saved (handle, the idea in a few words, the
  link).

**Two stops, in this order, every session:**

1. **The videos §1 pins:** reply to their comments while the panel is open.
2. **Then the Studio comments inbox: engage 20 comments.** Open
   `https://studio.youtube.com/channel/<ID>/comments/inbox` (channel check as §0), filter to
   comments we have NOT responded to, and reply + heart 20 of them, newest first. Studio shows the
   video next to each comment, so a question about it ("what song?") can be answered: look it up
   (title → ledger → the video's `.md` or docs) when it is quick; if the answer isn't findable,
   skip that comment. Skipped ones don't count toward the 20.

## 2. Queue, order, metadata

1. **Videos:** Luka gives the list (paths, names, or "these 5"). A video he names gets the
   not-already-live check too: Luka uploads by hand and a file can stay staged after it is public,
   so match its title against the Studio Shorts list; already live → ask him (never two public
   uploads of one video).
   **No list given ("upload new video", "upload 3") → PICK THEM YOURSELF. NEVER ask which**
   (Luka, 2026-09-15: no named clip means the choice is delegated). No count given → fill today's
   free slots, else the next day's. Pick from `final/ready-to-upload/` (subfolders and the entry's
   Queues too) in this priority:
   1. **Not already live** (above).
   2. **Absent from the channel the LONGEST:** the family, machine or series, by the ledger,
      `final/uploaded/` AND the Studio Shorts list (manual uploads count). One that went up in
      the last few days goes to the back.
   3. **The channel's own direction:** the ranking or recommendations its `CLAUDE.md` or latest
      `reports/*_channel_analysis.md` gives (the entry's Order line points to it; 2D: "MECHANIC
      FAMILY RANKING", Pac-Man and press/crush ahead of escape/door and beat/rhythm).
   4. **Waited longest in the queue** (oldest file mtime) breaks ties.
   5. **Never two of one family in a batch** when another is available; `order` spaces the rest.

   **Queues** (the entry's): a day-bound queue (2D `monday/`) goes ONLY into that upload day's
   slots (Mon 14:00 → Tue 06:00 Tbilisi), and that day takes from it FIRST (the priority above
   orders them); never pick from it for another day unless Luka names the clip. `great/` holds
   Luka's hand-picked best; it is picked like any other and `record` files it apart. State the
   picks with a one-line reason each in the §3 plan and the report, not as a question beforehand.
2. **Order = the order Luka WROTE them in** (Luka, 2026-09-15). The first video in his message
   takes the first free slot, the second the next, and so on: **a list he gives is never shuffled
   or reordered**, not even for sequencing. A clash in his order (two look-alikes back to back, a
   `## Sequencing` objection) goes into the §3 plan as a note, and his order stands unless he
   changes it. **Random ONLY when he didn't list them himself** ("upload 5 from ready-to-upload",
   "any order"): `UP order <videos…> [--after <latest upload's name> ...]`, random, keeping the
   entry's look-alikes apart (family: the stem's first word, `sort_*`, `double_*`;
   machine-subject: never back to back the same machine, the same subject, or a pair whose
   `## Sequencing` names the other, with `--after` = the channel's latest upload(s) from the ledger
   / `final/uploaded/`, oldest first, so the first one is checked too). The tool matches exact
   names only: also read each video's `sequencing` lines from `meta` and any house notes (3D:
   `final/ready-to-upload/README.md` "Sequencing rules": "the coin cluster", "every melody
   episode") and reorder a random order by hand if they object.
3. **No photosensitivity line on a Short, ever,** even when the flash check fails (the warning is
   long-form only). No flash check for an upload; `meta` drops the line from a Short's
   description if a `.md` still carries it.
4. **Metadata:** `UP meta <videos…>` → per video: the file to upload (`short`, or `file` on a
   folder channel), `title_options` (the entry's suffix appended, ≤ 100 characters checked; on
   compilation and folder channels the recommended one flagged and the 1-3 emoji rule checked),
   `description`, `tags`, `comment`, `problems`, plus `sequencing` (3D), `title_note`,
   `tags_if_made_for_kids` and `thumbnails` (folder). Where the entry's Text says description
   and tags are empty on a Short (2D, 3D: Luka 2026-09-17, tested: no tags performs better), the
   suffix (`#usa`) is the ONLY hashtag, at the end of the title; the description box stays EMPTY
   and the Tags field stays EMPTY; `meta` returns both as `""`: never fill them from the `.md`
   (3D's `## Description` is archive, not upload text). Check `title_options` by eye anyway. A
   non-empty `problems`: on 2D write the title by hand (1-3 emoji + the suffix); on 3D fix the
   docs `.md` (the record; edits go md → txt), re-run `py -3 tools/make_upload_txt.py <name>`,
   re-run `meta`; on a folder channel fix the video's `.md`.
5. **Title:** Luka's pick if he named one; otherwise the `.md`'s recommended one (3D marks it,
   Luka 2026-09-15: "titles in metadata files are good, apply it": they follow the house style in
   `final/ready-to-upload/README.md` and were count-checked against the finale at staging, rule
   15g; a folder `.md` lists it first,
   and its `title_note` may tie the choice to the audience setting). A 2D `.md` recommends none:
   choose by its `CLAUDE.md` "Channel direction" (a question the sim answers, or a countdown /
   number the viewer watches converge, beats a bare reaction). **Never a guess/predict title**
   ("Guess who wins", "Pick your column", "Can you spot…": 2D's worst stayed-to-watch): if the
   recommended one is that shape, take the next that isn't and say so in the plan. Never a title
   the channel published in the last few weeks (ledger + Studio list). On 3D check the title
   against the staged `.txt` before typing it. The title is typed WITH the suffix: on 2D and 3D
   `#usa` is not optional and no other hashtag is allowed.
6. **Tags** as the entry's Text says. Empty: don't open "Show more" for them, don't add any.

## 3. Schedule slots

1. In Studio **Content → Shorts**, read every Short that is **Scheduled** (the row shows date +
   time) and every one **published in the last ~24 h** (Studio shows only a date: open its
   youtube.com page for "N hours ago"; an exact time is `youtube.com/watch?v=<ID>` →
   `meta[itemprop=datePublished]`, Pacific time: +11 h to Tbilisi in PDT, +12 h in PST). Manual
   uploads by Luka count exactly like ours; long-form doesn't count. Row IDs: `a[href*="/video/"]`
   inside each `ytcp-video-row`. Page 1 (30 rows) covered 7 days; a JS loop clicking
   `#navigate-after` 7 times timed out (CDP 45 s) and left the list on the last page, and a
   `?filter=[{"name":"TITLE",...}]` URL did not filter. A Short published today needs its exact
   time only when it could sit within the entry's gap of a candidate time.
2. `UP slots --count N --existing "YYYY-MM-DD HH:MM" …` (times in the entry's timezone). The
   entry's slots, at most its count a day (a day runs from the first slot to just before it:
   14:00 → 13:59 on 2D and 3D). **A slot is a TARGET, not a gate** (Luka, 2026-09-15: "18:00
   means around 18:00"): **a MISSED slot is never skipped**: minutes or hours late, it goes out as
   soon as possible (now+10 min, next 15-minute mark), because every slot of the day is meant to go
   out. **The ONE strict rule: the entry's gap (45 min on 2D and 3D) between any two Shorts:** the
   tool pushes a time forward in 15-minute steps until it clears every other Short; gaps around
   manual uploads are filled. Assign times in upload order. **Always run the tool; never pick or
   skip a time by hand** (3D, 2026-09-15: 18:00 was skipped to 22:00 on a hand call that it was
   "too close" to the 14:00 upload; the gap was 4 h, and Luka was rightly angry). If the form runs
   past the time the tool gave, re-run `slots` right before Schedule and use the new time.
3. **"Publish now"** for a video → `--immediate`: now+10 min (next 5-minute mark) as a SCHEDULE,
   never the Publish button; the rest continue from the slots.
4. State the plan (video → time → title, the picks' reasons, any clash note) in chat, then upload.

## 4. The upload form: one video at a time

Open `https://studio.youtube.com/channel/<ID>/videos/upload?d=ud` (or Create → Upload videos),
wait ~5 s, then mark the tab with the channel's folder name AND lock the mark: Studio RESETS
`document.title` on its own route changes (its filter redirect put it back to "Channel content -
YouTube Studio", and `activate_edge_tab.ps1` then cycled every tab and found nothing). Redo this
after EVERY navigate to the upload URL:

```js
const T = 'UPLOAD-<channel folder> YouTube Studio'; document.title = T;
new MutationObserver(() => { if (document.title !== T) document.title = T })
  .observe(document.querySelector('title'), {childList: true, characterData: true, subtree: true});
```

The marker keeps the picker off another channel's upload tab, another "YouTube Studio" tab and
other browsers (`-WindowTitleMatch "YouTube Studio"` once bound an Opera window). Then the file
through the real OS picker:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/activate_edge_tab.ps1 -TitleMatch "UPLOAD-<channel folder>"
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/pick_file_dialog.ps1 `
  -Path "<full path of the file meta gave>" -InvokeButton "Select files" -WindowTitleMatch "UPLOAD-<channel folder>" -TimeoutS 40
```

- The button is `"Select files"` in an English UI; check its label first (a Georgian UI says
  `ფაილების არჩევა`, and the English name then gives `NO_BUTTON`).
- `NO_BUTTON` right after navigating is a11y lag: take a screenshot (it wakes the a11y tree),
  wait ~3 s, run it again. Still `NO_BUTTON`: crosspost §3's route (the script WITHOUT
  `-InvokeButton`, in the background, then a `computer left_click` on the button's ref).
- A failed run can leave its Open dialog alive, and the next run then APPENDS the path to the old
  text ("The file name is not valid"): `pick_file_dialog.ps1 -CloseDialogs` before retrying.
- The Edge window title stays "Channel content - YouTube Studio" under the upload dialog (`?d=ud`
  is an overlay on the same tab).

A ~27 s 4K Short uploads in seconds; Studio prefills the title with the file name. The "uploading
a Short… ad revenue sharing" banner is normal.

**NEVER press Escape inside the upload dialog: it CLOSES the dialog** (the upload survives as a
draft: Content → Shorts → Edit draft reopens it with the title kept). Close every popup on the
form with its own X / Save / Cancel.

**Details: four actions**
1. **Title:** focus the box BY JS (`document.querySelectorAll('[contenteditable="true"]')[0]
   .focus()`; a ref click + Ctrl+A once put the whole description into the TITLE), Ctrl+A (the
   prefilled file name), type the chosen title. With a hashtag suffix, type a trailing space after
   it so the hashtag autocomplete closes (`#usa` once became `#usanewstoday`); never close that
   popup with Escape. A long title wraps to 2 lines and shifts the layout: never click Details
   fields by coordinates.
2. **Audience**, as the entry's Made for kids says: "the channel default" → don't click it; "set
   on every upload" → click "No, it's not made for kids"; "decided per upload" → ask Luka for this
   video first (a folder `.md`'s `title_note` and `tags_if_made_for_kids` depend on it). Then read
   it back: `document.querySelector('tp-yt-paper-radio-group').selected` is
   `VIDEO_MADE_FOR_KIDS_NOT_MFK` for "no" (the buttons' `aria-checked` stays "false" either way;
   the radio once ignored the first click, by ref AND by coordinate).
3. **Collaborator** (only when the entry names one): **Show more** → Collaboration → Invite a
   collaborator, exactly §4b's invite steps (the same inside the upload form). Wait for "Changes
   saved" and the button reading **Manage collaborators**; the upload form stays open underneath.
4. **Description and tags**, as the entry's Text says: empty → leave the description EMPTY and
   don't touch Tags (also under "Show more"); from the `.md` → the `description` and `tags` from
   `meta` (plus `tags_if_made_for_kids` on a video set made for kids). Other fields only where the
   entry's Settings names them; otherwise don't touch the AI-use question that sits above
   Collaboration. Read back before Next:
   `[...document.querySelectorAll('[contenteditable="true"]')].map(b=>b.innerText)` shows the
   title (a trimmed trailing space is fine) and the description (EMPTY where the entry says so).
   → Next.

**Ad suitability, 2 actions:** tick **None of the above** (verify it is ticked: the rating is
final) → **Submit rating** → Next. Before submitting, the side panel reads **Safe for ads**.
**The proof it saved is a SCREENSHOT where Submit rating and every option went GREY**
(2026-10-03: the first click didn't register and Next silently stayed on Ad suitability; a JS
`/Rating saved/` check read true off the PREVIOUS upload's toast, and often no toast shows at
all). Still white → click it again.

**Video elements:** Related video → Add → the "Choose specific video" grid (it opened directly on
3D, through a "Choose specific video" step on 2D). Its search box didn't take focus on a click
(the typing went to a tab control, harmlessly): `find` the tile by title in the newest-first grid
and click its ref (once only a coordinate click took) → the dialog text names it under `Related
video` → Next. The pick: the **previous upload** = the Short whose publish time is directly before
this video's slot (Studio list + this session's ledger rows, including videos scheduled minutes
ago), or the one a folder `.md`'s Notes name. A Short still Scheduled is never offered: take the
newest PUBLIC Short the picker offers (in a batch, every video after the first) and note the swap
in the ledger's related column.

**Initial check:** nothing → Next.

**Visibility** opens on *Save or publish* = **Unlisted** with a **Save** button where Next was:
never press Save, never re-click Next's coordinates. Expand **Schedule** ("Click to expand"); it
defaults to tomorrow 12:00 AM. The layout SHIFTS when "Checks complete" replaces the checking
banner (the Schedule row moved ~40 px and coordinates from an earlier screenshot missed
silently): screenshot right before each click below.
1. **Time zone** button → the list's TOP entry `(GMT+0400) Local Time` is Tbilisi (Georgian UI:
   `(GMT+0400) ადგილობრივი დრო`) → click it; anything else → the GMT+04:00 Tbilisi entry (a
   channel in another timezone: its own). The option by ref once left the list open with no zone
   set; a coordinate click on the top entry took.
2. **Date** button → calendar → the slot's day (`studio_date`); a tomorrow slot is already set.
   The date button once ignored two ref clicks; a coordinate click opened it.
3. **Time:** typing `2:00 PM` does NOT stick (the field stays 12:00 AM: "Select a time in the
   future"). Click the field (the first click after picking the time zone only closes the zone
   menu, so click again; once it took 3 clicks), `find` the option `studio_time` in its 15-minute
   dropdown, `scroll_to` + click by ref. The labels hold a narrow no-break space (U+202F) before
   AM/PM, so an exact `'2:00 PM'` match finds nothing: compare after `.replace(/\s/g,' ')`. For a
   coordinate click: normalise, `scrollIntoView({block:'center'})`, take `getBoundingClientRect()`'s
   centre × (screenshot width / `window.innerWidth`). `scroll_to` can leave the list at 12:00 AM
   so the click misses: re-read the input. A Georgian UI lists 24 h times (`14:45`). An off-grid
   time (a now+10 min slot): type it and read back; if it won't stick, take the next 15-minute
   option and record that time.
4. **Read back before scheduling:** the Schedule block shows the date and `(GMT+0400) Local Time`,
   the time input's value is `studio_time`, the Schedule button is enabled, no "Select a time in
   the future". **Copy the video's URL now:**
   `[...document.querySelectorAll('ytcp-uploads-dialog ytcp-video-info a')].map(a=>a.href)`
   (2026-10-05: 2 of 5 ended on a dialog with no link).

→ **Schedule.** While Studio's content check still runs (~20% a minute after upload) a **"We're
still checking your content"** notice comes first: advisory, **Got it**. Then the **"Video
scheduled"** dialog ("will be set to public on <date> at <time>": read it against the slot) with
the `youtube.com/shorts/<ID>` link → Close. It can instead say "Video published" with no date
line (2026-09-28) or "Video processing ... will be set to public on <date> at <time> after the SD
finishes processing" with no link (2026-10-05); both were scheduled correctly: confirm in Content
→ Shorts (visibility "Scheduled", the hover tooltip "scheduled to become public on <date> at
<time>").

Don't start the next video until this one's page says the upload finished (closing mid-upload
loses it).

**Record** (the ledger row is the only record of the upload, and the tool creates the ledger on
its first row; the move follows the entry):

```
UP record --clip <the video> --when "YYYY-MM-DD HH:MM" --title "<title as uploaded>" --url <url> --related "<related title>"
```

- 2D: the mp4 pair to `final/uploaded/` (from `great/` to `final/uploaded/great/`; non-PD music,
  flagged by `tools/longform_audio_guard.py`, to `final/uploaded/shorts-only/` so it never
  enters the megamix pool), the `.md` deleted, an emptied subfolder removed (never a stage folder
  or a Queues folder).
- 3D: the pair to `final/uploaded/`, the staged `.txt` deleted; the docs `.md` is never touched.
- A folder channel: the whole folder to the same place under `final/uploaded/` (a Short of a
  `from_NN_<long>/` group into that group's folder; the emptied group folder goes).
- It refuses a video whose file isn't there. `--no-move` records one that stays where it is (a
  manual upload by Luka). `--own-comment`: a comment-reply video (§1 step 4).
- Then the other half of the shared upload rule: delete this video's own files in `output/`
  (named by its stem, label or seed; what can't be told apart waits for Luka's clean-up).

## 4b. Collaborator (when the entry names one)

Every Short goes out as a collaboration with the entry's collaborator (2D and 3D invited each
other from 2026-10-05; paused by Luka 2026-10-17). Two halves: INVITE from this channel on each
video, then ACCEPT from the collaborator. Both sit on the same Google account, so accepting is
only a channel switch.

- **Invite at upload time (the default):** §4 Details step 3, inside the upload form, before
  Schedule. Tested 2026-10-05 on a draft: the invite saved while the video was still a private
  draft, and closing the link popup did NOT close the upload form.
- **Accept once, after the whole batch is scheduled:** one switch accepts every pending invite.
  Invites stay "Pending" until accepted, wherever they were added.
- **Fallback:** a video that went up without the invite (form trouble, a manual upload) gets it
  from its Details page, `https://studio.youtube.com/video/<ID>/edit`.

**Invite** (per video; the same in the upload form or on the Details page): **Show more** ("Paid
promotion, collaboration, and more") → **Collaboration → Invite a collaborator** → focus the
search box BY JS (`document.querySelector('input[placeholder="Search for a channel"]').focus()`:
after a ref or coordinate click the typing was lost) → type the collaborator's name, check its
handle appears → pick that row → **Allow analytics viewing** ON (`button[aria-label="Allow
analytics viewing"]`, check `aria-checked="true"`; no revenue access) → **Create link** → the
"Copy and share your invite link" popup → close it with its X → the list reads "<collaborator> ·
Pending acceptance" → **Save** in the dialog. A "Changes saved" toast, and the page button turns
into **Manage collaborators**; the page's own Save stays grey (saving the dialog is enough). The
analytics toggle and the popup's X both miss while their dialog is still fading in: re-read
`aria-checked` / screenshot, click again. The invite stacks two dialogs on the form: never
Escape.

**Accept** (once per batch): `https://www.youtube.com/channel_switcher` → the collaborator
(`ytd-account-item-renderer`) → load `studio.youtube.com` and check the URL carries ITS id →
open `https://studio.youtube.com/channel/<collaborator id>/collaboration/<this channel's id>`
(accepting channel first, inviting channel last; the same link for every video). Tested
2026-10-05 with 3D accepting 2D's invites; 2D accepting 3D's is untested until its first run:
note here what differs.
- several pending → a "Collaboration requests" list → click a row → "You're invited to
  collaborate on <title>" → **Accept** (progress bar, then the dialog closes without returning to
  the list) → open the link again for the next;
- one pending → straight to its Accept dialog;
- none left → "No new requests yet". That is the done check.

Then **switch back** to this channel (same switcher) before anything else here, and spot-check
one video: Manage collaborators → "<collaborator> · **Active collaborator**" → Cancel.

## 5. Verify and report

- Studio Content → Shorts: every new row **Scheduled** at its slot time.
- With a collaborator: every new Short shows it as **Active collaborator**, and the invite link
  shows "No new requests yet".
- `final/ready-to-upload/` no longer holds the uploaded videos (and 3D's `.txt`); `final/uploaded/`
  does; 3D's `docs/uploads/compilation_<name>.md` still exists.
- Report in chat: a table video → publish time (channel time) → title (with its suffix) → URL; the
  picks' reasons; any clash noted in Luka's order (or a hand reorder of a random one); the ideas
  saved from the comments (§1b, with links); and that the pinned comments are pending until each
  video goes live (next session's §1 posts them).

## Studio traps (found on real runs: add every new one here)

- **The first click often only focuses: verify every control by readback.** On 3D's first run the
  audience radio, the schedule date and the time field each took the second click, by ref AND by
  coordinate (another run, every control took the first). The date / time / zone readback before
  Schedule is not optional.
- **The UI language varies by run and PC** (the account language; `?hl=en` is ignored): check a
  label before using it. Georgian labels seen (2026-09-17): Select files `ფაილების არჩევა` · Show
  more `მეტის ჩვენება` · None of the above `არცერთი ზემოთ მითითებულიდან` · Submit rating
  `შეფასების გაგზავნა` · Next `შემდეგი` · related-video Add `დამატება` · Schedule `განრიგი` ·
  Local time zone `(GMT+0400) ადგილობრივი დრო` · Edit draft `მონახაზის რედაქტირება` · a Short's
  comments button aria `N კომენტარის ნახვა`. IDs work in any language: `#next-button`,
  `#done-button`, `#toggle-button`, `#tags-container input`.
- **Keyboard layout:** Luka's Georgian layout broke Ctrl+A inside the OS file dialog once (it
  typed `a`); `pick_file_dialog.ps1` uses HOME+SHIFT+END there. Extension typing in the page is
  unaffected.
- **The brother's PC (MICHAELL) runs Chrome, not Edge** (3D, 2026-10-20): `activate_edge_tab.ps1`
  finds nothing there and `verify_local_browser.ps1` says NOT_LOCAL (use the localhost proof).
  The extension's tab was NOT the active one (the window title showed another Studio tab), so
  `pick_file_dialog.ps1` said `NO_BUTTON` / `NO_DIALOG`. Fix: a REAL click on the tab in Chrome's
  tab strip (`SetProcessDPIAware` + `SetCursorPos` + `mouse_event`). Chrome sat on a second
  monitor at negative X (`Screen.AllScreens`), so the click's X is the desktop screenshot's X plus
  that monitor's left edge. Once the window title read `UPLOAD-...`, every pick worked first time.
- **vidIQ blocks the extension on the Visibility step** (3D, MICHAELL's Chrome, 2026-10-22): its
  injected "Suggestions" / "Comment Draft" panels under Schedule are `chrome-extension://` frames,
  and from then on EVERY call on that tab (click, screenshot, JS) fails "Cannot access a
  chrome-extension:// URL of different extension"; earlier steps worked. Before an upload set
  vidIQ's Site access to "On click" (or turn it off) in `chrome://extensions`; already stuck:
  Luka finishes the Schedule by hand, then `record` with his link. **From 2026-10-22 MICHAELL
  uploads through EDGE** (Luka: no vidIQ there; the Claude extension is installed in it): prove it
  local (crosspost §0 step 1) and rewrite `.claude/browser.local` with Edge's deviceId.
- **`record` with a `"` in the title: run it from Bash**, not PowerShell (PS 5.1 split the
  argument at the quotes; 3D, 2026-10-20).
- Edit `yt_upload_prep.py` with the Edit tool: a bash heredoc mangled its regex backslashes twice.
- Write every new Studio or YouTube fact from a real run into this file, with its date and the
  channel it was found on. The file is shared, so it reaches every channel.
