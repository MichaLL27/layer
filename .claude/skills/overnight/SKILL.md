---
name: overnight
description: Run an unattended night session while Luka sleeps. Use whenever Luka says he is going to sleep, going away, "overnight", "night session", "while I sleep", or hands over a big job to run until morning (many videos, new concepts, analytics, research, anything). He can answer questions only in the first minutes; after START nothing may wait on him. The task itself is in his prompt and differs every night; this skill is how the night runs - questions up front with a 15-minute timeout, a night log to resume from, an hourly reminder that restarts a stopped session, no questions after START, a report at the end, and a PC shutdown only when his prompt asks for one.
---

# Overnight

Luka gives the night's task in his prompt (videos, concepts, analytics, research: anything) and
goes to sleep. This skill is how the night runs; the task, and how this channel makes a video,
come from his prompt and the channel's own `CLAUDE.md` and skills. The shared rule "When Luka is
away" applies throughout.

## 1. Before START: questions now or never

- Read the prompt, the channel's `CLAUDE.md` and the skills the task needs. Then ask EVERY
  question in ONE message, at once, each with the default you will take if he doesn't answer.
  Ask only what changes the work; decide the rest yourself. No questions: say so and start.
- In the same message: the plan in a few lines; whether the night ends in a shutdown (only if
  his prompt says so, see step 4); the model and the permission mode this session runs in. If
  the mode isn't auto (or bypass), say plainly that a permission prompt will stop the night.
- End the message with: "Answer, or say START. In 15 minutes I start with my defaults."
  Then schedule that timeout with CronCreate, one-shot (`recurring: false`): read the local
  time (`Get-Date`), pin minute, hour, day and month 15 minutes ahead, prompt:
  `OVERNIGHT: 15 minutes passed with no answer. Take your proposed defaults as the answers,
  write them into the night log and start the night (overnight skill, step 2). If the night
  has already started, ignore this.` Keep its job ID.
- When he answers or says START: CronDelete the timeout and go.

## 2. START

1. **Night log** (create it first): `docs/overnight/<YYYY-MM-DD>.md` in the channel (add `_2`
   for a second night on the same date). It holds the task as he gave it, the answers or the
   defaults taken, a checklist of the steps, and the decisions made on the way. It is the
   resume point: after any stop, the session reads it and continues from its first open step.
   Tick a step the moment it is done.
2. **Hourly reminder**: CronCreate, recurring, at a minute that isn't :00 or :30 (e.g.
   `7 * * * *`), prompt: `OVERNIGHT REMINDER: if tonight's work is not finished, read the night
   log docs/overnight/<file>.md and continue from its first open step (overnight skill). If it is
   all done, finish the night (overnight skill, step 4).` It fires only when the session is idle,
   which is exactly when it has stopped (an API error, a usage limit that has since reset). Keep
   its job ID in the night log.
3. **Mark the night as running**: write `$env:LOCALAPPDATA\claude-overnight\<channel>.running`
   (the start time inside). Another channel's overnight session on this PC reads these.
4. **All CPU threads** are allowed tonight (the channel's overnight switch, e.g.
   `$env:BPS_OVERNIGHT='1'` where its `CLAUDE.md` names one), unless another channel's
   `.running` file exists: two nights on one PC share it, so both keep the normal free threads.
   GPU renders still queue on the shared lock.

## 3. The night

- **No questions after START, ever.** A question nobody answers stops the night until morning.
  Unsure: take the best option, write the decision and why into the night log, go on.
- **One task at a time, to the end**: built, checked as the channel's rules ask, staged with
  its one metadata file, ticked in the log. Then the next. Half-done work everywhere is worth
  less than fewer finished things.
- **A blocker doesn't stop the night.** A failing tool, a denied action, a broken render: try
  another way once or twice, then log it with what was tried and move on to the next task.
- **Everything made tonight is provisional**: it waits in the review folder (or
  `final/new-concepts/` for a test of an unproven idea) for Luka's verdict.
- Nothing is committed, pushed, published or uploaded unless the prompt says so.
- Keep the night log current: if the session stops, the next one starts from it, not from
  memory.

## 4. End of the night

1. Every task done or logged as not done (with why). The checklist is all ticked or explained.
2. CronDelete the hourly reminder (and any timeout still pending: CronList).
3. Delete this channel's `.running` file.
4. **The report**: the final reply: what was made, each file as a link, what failed and why,
   what waits for his verdict. A big night (many videos, several kinds of work) also gets a
   `## Morning report` at the top of the night log, linked from the reply; a small night needs
   only the reply. Close with the short overview.
5. **Shutdown, only if his prompt asked for it** ("shut down after", "turn off the PC"). Without
   that, the PC stays on: the default is no shutdown. Before shutting down:
   - wait until no other channel's `.running` file remains (check every 10 minutes; a file
     older than 16 hours is a crashed night and doesn't count): shutting down would kill
     another channel's night;
   - make sure nothing of this session is still writing (renders, encodes);
   - then `shutdown /s /t 120 /c "Overnight session finished"`.
6. If Luka is back and the session goes on, drop back to the normal free threads.
