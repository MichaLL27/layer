---
name: comment-video
description: >
  Build a REPLY video answering a viewer's YouTube comment, on any channel: the next
  episode of the video the comment was left on, with the change the viewer asked for,
  opening on the real comment. Use whenever Luka pastes a comment screenshot (or quotes
  a comment) together with the video it was left on and wants the requested change made,
  e.g. "can you make the circle rotate", "do it with squares", "make it faster".
  Triggers: a pasted comment screenshot, "viewer asked", "someone commented", "reply to
  this comment", "make what they asked".
---

# Reply to a viewer comment

A viewer asked for a change to a video they watched. The reply is that video again with the
change, opening on their comment, with a pinned comment that names them: the commenter comes
back, and everyone else sees that ideas get built.

## What differs per channel: one entry in `## Channel facts`

Read it in the channel's `CLAUDE.md` before step 1:

```
- **Comment video:** episode: <the skill or tool that makes one video, and how it makes an
  episode>. Card: <the tool that puts the comment on screen, or "no comment-card tool yet">.
  Images: <folder>/<handle>.png. Timing: <how long the card holds, what replaces it>.
```

A part missing or `unknown`: work it out from the channel's `CLAUDE.md`, skills and `tools/`,
tell Luka in one line, and write it into the entry (shared rules, "Shared skills"). Until then:
images in `research/comments/`, card 4 s.

## 1. Get the comment as a file (first reply)

- The comment may come from the channel's ideas file (`research/comment-ideas.md`, saved by the
  youtube-upload skill): its entry gives the words, the handle and the comment's link.

- A pasted image can't be saved. At the top of the first reply, ask Luka to save it to the
  entry's images folder as `<handle>.png`, captured LARGE (browser zoom ~250%): a default
  capture is ~350 px wide and gets upscaled several times on a 4K frame, so it looks soft.
  Then keep working: everything but the final render is independent of it.
- The REAL screenshot ships. A drawn stand-in (if the card tool has one) is a placeholder while
  you wait, and ships only if Luka says there is no screenshot.
- Only a comment the channel actually received. Never invent or reword a comment, a handle or
  a like count.

## 2. Find the video it was left on

Luka names it, or the screenshot shows it. Find its folder in `final/` (usually `uploaded/`),
read its `.md` (the Notes say how it was built: command, seed, version) and its scene or format
doc. Ask only if two videos match.

## 3. The reply is the NEXT EPISODE of that video

- Same scene, look, mechanic family, sound and on-screen identity (caption, counter, HUD),
  plus the one change the viewer asked for. **Never a new scene, however good the new idea
  is**: the viewer asked about the video they watched, so the answer must read as that video
  again. A good new format that falls out of the work is kept as its own unstaged concept and
  mentioned; it is not the reply.
- Build what the viewer means, not only the words: "make the circle rotate" meant the arena
  spins and drags its contents, and a perfect circle spinning is invisible, so the spin also
  had to be made visible.
- Add the change as an option that defaults OFF (a flag, a parameter), as a small exact patch
  (other sessions share the checkout). Never fork the scene or change its defaults: the
  shipped video must still rebuild exactly, so re-render the old default at a few frames and
  diff against the committed version (max diff 0). Look for the narrow seam first: one option
  is usually enough even when it looks like it isn't.
- Expect the change to break the scene's balance. Never ship the first run that works:
  re-measure the original's own numbers (length, when the payoff lands, fill, event density,
  the channel's checks) and re-tune until they match.
- If the change alters what the on-screen text claims, rewrite the text, then treat it as a
  spec the visuals must satisfy.

## 4. Build it the channel's way

Hand off to the entry's episode route (the channel's make-video or build tool). Its gates
stay: test clip, self-verify, Luka validates (unless the run is unattended), the ratios it
makes. Everything in the channel's `CLAUDE.md` still applies.

## 5. Put the comment on screen first

With the entry's card tool:

- The real screenshot, sharp, about 60% of the frame width: a quoted comment, never a banner.
  Wider only when a 3+ line comment can't be read at that size. On a Short, clear of the
  bottom fifth and the right-edge buttons.
- Up from frame 0, held for the entry's time, then faded out and replaced by the video's own
  caption or title. The video plays underneath from frame 0: the card never delays the
  mechanic, which still fires by second 5.
- If the channel burns its own text into the opening, the card takes that slot and the text
  arrives once the card is gone; never text over the card.
- A loop stays a loop: on a perfect loop the card is full at frame 0 (not faded in) and fades
  back in at the end, so the seam matches. Draw it as a function of the frame index, so a
  rewind un-swaps it.
- Check stills in every ratio the video ships in: on frame, readable, gone on time, the
  caption up right after.

**No comment-card tool yet:** build one in the channel's own `tools/` before the final render,
and write it into the entry. Either an option of the step that already burns the channel's
on-screen text, or a post-pass over a base rendered without the opening text: it takes the
video, the image and the hold time; overlays the image at ~0.6 of the frame width on the
channel's caption or title position for that ratio; full from frame 0, fading out over
~0.4 s after the hold; audio stream copied, codec, size and fps kept as the channel's
masters; refuses to run if the image is missing. Prove it on stills before using it.

## 6. Stage it with its one metadata file

Stage it with the channel's tool into its review folder, with its `.md` (shared rules, "One
metadata file per video"):

- **Title**: lead with the reply angle ("You asked me to spin it 🌀 ..."); a Short's options
  carry 1-3 emoji; none close to a recent title.
- **Pinned comment**: @-mentions the commenter (it pulls them back, and shows everyone that
  ideas get built) and says one real thing learned making it. Where the channel's upload
  skill posts a fixed pin, this video is the exception: say so in its Notes.
- **Notes**: the video it answers, and the rebuild command with the new option and the image.

Then the title options in the reply, and a link to every file.
