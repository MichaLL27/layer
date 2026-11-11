---
name: youtube-analytics
description: >
  Pull every YouTube Studio metric for any YouTube HQ channel through this PC's automation Edge
  (content table, traffic source, content type, device, geography, sharing service, age,
  gender, per-video retention curves) as CSV exports into reports/exports/<date>/, run the shared
  yt_analytics.py over them, write reports/<date>_channel_analysis.md with what changed since the
  last report, publish the shareable page, and write each recent video's measured outcome where
  the channel keeps it. Channel, families, ledger and outcome docs come from the Analytics entry
  of the channel's CLAUDE.md `## Channel facts`. Use whenever Luka says "analyse the channel",
  "check youtube studio", "how are the videos doing", "score the batch", "pull the analytics",
  or asks whether we are on a good path ("are we on a good path"). Read-only on YouTube: it never
  edits a title, a visibility or a comment.
---

# YouTube analytics: the channel readout

The browser part and the writing are this file; everything mechanical goes through
`yt_analytics.py` beside it, so reports a month apart, and two channels, compare like for like.
**Read-only:** it changes nothing on YouTube and can run unattended. What Studio doesn't show
(YPP emails, strikes): ask Luka. **When:** on request; best once the latest batch is 7+ days
old (younger Shorts are still climbing). `YA` below = `py -3
.claude/skills/youtube-analytics/yt_analytics.py` from the channel root (from the hub:
`<channel>/.claude/...`; it finds its channel from its own location).

## What differs per channel: one entry in `## Channel facts`

Read it in the channel's `CLAUDE.md` first (the tool reads it too):

```
- **Analytics:**
  - Channel: `<the name Studio writes into its zip names>`, `<UC... id>`.
  - Families: `<profile in families.py>` | none.
  - Ledger: `<upload ledger: id -> clip>` | none.
  - Title options: `<glob of the video .md files>`, ...
  - Outcomes: `<doc path with <name> or <scene>>` | none.
  - Ignore before: <date>.        (optional: a rebranded channel's older videos leave every table)
  - Refresh: <docs that carry these numbers>.   (optional)
  - Page: `<artifact title>`.     (optional; default "<Channel> Channel Readout")
```

No entry, or the ID `unknown`: ask Luka once (confirm the channel in Studio with him), write
it in (new channels: Families, Ledger, Outcomes `none`), go on. Outcomes `<name>` = a doc per
video, newest block on top; `<scene>` = the doc of the scene (the stem's longest leading run
naming a `scenes/*.py`), blocks appended; `none` = the report is the record.

## 0. Browser and channel (every session)

1. **This PC's Edge, proven local:** `.claude/skills/crosspost/SKILL.md` §0 step 1 (a-d)
   exactly. Then `tabs_context_mcp createIfEmpty:true` and work in that tab group.
2. **Confirm the channel before exporting anything.** Open
   `https://studio.youtube.com/channel/<ID>/analytics/tab-overview/period-default`: the URL
   still carries `<ID>` after load AND the sidebar shows the channel's name. Studio opens on
   the last session's channel ("Oops, you don't have permission" on this URL), and an analysis
   of the wrong channel looks plausible: this check is the whole guard. Switch with avatar →
   Switch account (or `youtube.com/channel_switcher`); ask Luka first if another channel's
   session may be using this browser right now.
3. Note the **Overview headline** (last 28 days: views, watch hours, subs, revenue; realtime
   48 h): it goes into the report's first table and is in no export. `DATE` = today.

## 1. Exports: Advanced mode BY URL, never by the pickers

The Breakdown and Metrics pickers are flaky (clicks toggle, refs go stale, select-all breaks
the query). Every report is one URL:

```
https://studio.youtube.com/channel/<ID>/analytics/tab-overview/period-default/explore?entity_type=CHANNEL&entity_id=<ID>&time_period=lifetime&explore_type=TABLE_AND_CHART&metric=EXTERNAL_VIEWS&granularity=DAY&dimension=<DIM>&t_metrics=<M1>&t_metrics=<M2>...&o_column=EXTERNAL_VIEWS&o_direction=ANALYTICS_ORDER_DIRECTION_DESC
```

Per report: **a NEW tab** → navigate → wait ~10 s → screenshot: the table has rendered →
export icon (top right, beside "Show chart"; aria-label "Export current view") → wait 2 s →
**"Comma-separated values (.csv)"** (the second item, test-id `CSV`; the first opens Google
Sheets) → leave the tab open until the zip is in Downloads, then close it. After all are
down: `YA ingest --date <DATE>`.

| kind | `dimension` | `t_metrics` |
|---|---|---|
| content (main table) | `VIDEO` | `EXTERNAL_VIEWS ENGAGED_VIEWS EXTERNAL_WATCH_TIME SUBSCRIBERS_NET_CHANGE AVERAGE_WATCH_TIME AVERAGE_WATCH_PERCENTAGE VIDEO_THUMBNAIL_IMPRESSIONS VIDEO_THUMBNAIL_IMPRESSIONS_VTR SHORTS_FEED_IMPRESSIONS_VTR ESTIMATED_UNIQUE_VIEWERS UNIQUE_REACH AVERAGE_VIEWS_PER_VIEWER RECENT_VIEWERS RETURNING_VIEWERS OCCASIONAL_VIEWERS FREQUENT_VIEWERS SUBSCRIBERS_GAINED SUBSCRIBERS_LOST RATINGS_LIKES RATINGS_DISLIKES LIKES_PER_LIKES_PLUS_DISLIKES_PERCENT SHARINGS COMMENTS TOTAL_ESTIMATED_EARNINGS AD_EARNINGS YOUTUBE_EARNINGS IMPRESSIONS CPM IMPRESSIONS_CPM PLAYBACKS EPM EXTERNAL_YOUTUBE_RED_VIEWS EXTERNAL_YOUTUBE_RED_WATCH_TIME` |
| traffic_source | `TRAFFIC_SOURCE_TYPE` | `EXTERNAL_VIEWS ENGAGED_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME AVERAGE_WATCH_PERCENTAGE VIDEO_THUMBNAIL_IMPRESSIONS VIDEO_THUMBNAIL_IMPRESSIONS_VTR` |
| content_type | `CREATOR_CONTENT_TYPE` | `EXTERNAL_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME SUBSCRIBERS_NET_CHANGE` |
| device_type | `DEVICE_PLATFORM_TYPE` | `EXTERNAL_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME UNIQUE_REACH` |
| geography | `COUNTRY` | `EXTERNAL_VIEWS ENGAGED_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME AVERAGE_WATCH_PERCENTAGE SUBSCRIBERS_NET_CHANGE EPM` |
| sharing_service | `SHARING_SERVICE` | `EXTERNAL_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME` |
| viewer_age | `VIEWER_AGE` | `EXTERNAL_VIEWS ENGAGED_VIEWS EXTERNAL_WATCH_TIME AVERAGE_WATCH_TIME AVERAGE_WATCH_PERCENTAGE` |
| viewer_gender | `VIEWER_GENDER` | same as age |

`SHORTS_FEED_IMPRESSIONS_VTR` is "Stayed to watch", `EPM` is RPM, `YOUTUBE_EARNINGS` is
Premium. `ingest` keys each zip by its Studio name (`Content 2026-…_… <Channel>.zip` →
`content/`), takes only this channel's zips whose window ends 0-2 days before `--date`, moves
each into its folder, and never overwrites: a second export of a kind lands in `<kind>_2/`
(a daily-curves pull → rename it `daily_curves/`; a re-pull that should win → swap the folders).

**Per-video retention:** every long-form published since the previous report (all of them on
a first run), the top 5 Shorts by views, and every Short of the batch under judgement.

```
https://studio.youtube.com/video/<VIDEO_ID>/analytics/tab-overview/period-default/explore?entity_type=VIDEO&entity_id=<VIDEO_ID>&time_period=lifetime&explore_type=AUDIENCE_RETENTION
```

New tab → navigate → **wait ~28 s** (the dialog takes 20-40 s; ANY click before it paints
dismisses it) → screenshot: the curve is there → export → CSV → zip in Downloads →
`YA ingest-ret --date <DATE> --id <VIDEO_ID>` **immediately**. The zip is named by TITLE (window
starting at the publish date); the tool takes the newest and refuses one older than 30 min (a
stale zip once landed under the wrong id). Several queued: `--match <title piece>` or `--zip
<path>`; titles that normalise alike (2D's two "12 Most" megamixes): the window start decides.
Also screenshot the Overview of the top 1-2 videos ("Key moments for audience retention",
realtime 48 h, traffic split): the "typical retention" comparison isn't in the export.

## 2. Studio traps (found on real runs: add every new one here)

- **Edge allows one automatic download per tab** (13 exports measured, 2026-09-15): every
  later export from a used tab vanished without a toast. Hence a new tab per export. The CSV
  click also often doesn't register the first 1-3 times: `find` the menuitem, click it by ref,
  retry until the zip is in Downloads (a menu still open on a screenshot = not registered).
  Real mouse clicks are the reliable ones; JS `.click()` opened the export menu on 3D but not
  on 2D, and never sorts columns (it does work on row checkboxes and the date picker).
- **Preparing a download can take ~2 min** on a big channel; navigating the tab away cancels it
  silently. Export before the table renders does nothing, and the shifted menu then hits
  "Google Sheets". One report per tool call: batched navigations time out and land clicks on
  the wrong pages.
- **A hung explore page poisons the tab** (blank, spinner forever), and so do ~4-6 explore
  loads: `tabs_create_mcp`, close the old tab, continue. After any blank screenshot, this first.
- **Invalid metrics or dimensions**: selecting all 80 metrics breaks the query; playlist, live
  and post metrics are invalid with `VIDEO`; `DEVICE_TYPE` and `SUBSCRIBED_STATUS` hang the
  page; `SUBSCRIPTION_SOURCE` blanks it. The subscriber split comes from a retention export's
  `Subscribers and non-subscribers.csv`.
- **`get_page_text` can't see Advanced mode** (a dialog outside `<main>`): read screenshots or
  the CSV (a failed small breakdown, age, gender, device, may come off a screenshot).
- **Daily curves**: `Chart data.csv` holds curves only for the rows SELECTED in the table
  (default the top 5), at most 12, the 12 most recently selected; `Totals.csv` holds the
  channel's daily total regardless. A curves export = tick exactly the 12 wanted (checkboxes
  `[aria-label^="Select row for "]` + the title's first ~25 characters, two per row, click the
  visible one); the table pages 50 rows. It is also named `Content ….zip`.
- **Per-video DAILY views for many clips: never tick rows** (~13 ticks froze the renderer).
  One single-day table per day: `time_period=<startMs>%2C<endMs>` (epoch ms of Pacific
  midnight, 1 day = 86400000; Sep 1 2026 = `1788246000000%2C1788332400000`),
  `o_column=VIDEO_PUBLISH_DATE` for newest first. Downloads blocked: read the rows' `innerText`
  into page `localStorage` (a key per day), build one CSV in a textarea, copy it with a REAL
  mouse click on an injected `navigator.clipboard.writeText` button, `Get-Clipboard -Raw`
  (keyboard copy leaves the clipboard empty). Reference: 2D's `reports/2026-09-15_cadence.md`.
- **Dates**: a zip's end date is Studio's last DATA day (the pull date or the day before); the
  table's last full day is that minus one, and the tool ages every video from it. Table
  publish-date labels run in a timezone ahead of the Pacific day buckets (±1 day per-day upload
  counts): the launch day is the first Pacific day with views.
- **Views changed meaning on 2026-08-27** (they now count from frame one, autoplay included;
  re-pulled totals came out ~3% lower): compare across that date with ENGAGED views, and check
  engaged/views is flat over any window before comparing dates.
- **Cadence can't be read off daily curves during a breakout surge** (3D, Sep 9-13: channel
  views 0.9M → 2.6M/day exactly as uploads went to 4-5/day): compare at matched age across two
  snapshots, and name the confound.
- Per-video unique/new/returning columns are blank in a lifetime export: expected. A Short's
  comments page can say "No comments found" while the export counts some: not worth chasing.

## 3. Run the numbers

```
YA analyze --date <DATE> --prev <PREV_DATE> --since <BATCH_START> --mature 7
```

`--prev` = the previous report's export date (none on a first run); `--since` = the first
publish date of the cohort under judgement (usually the latest batch's first upload). It writes
`reports/exports/<DATE>/summary.md` + `.json` and prints: totals and the delta; **breakouts**
(> 15% of all Shorts views, EXCLUDED from every median); the cohort **family table** and the
same families before it (3D adds family x subject); weekly cohorts; **upload cadence**;
correlations and length bands; top 15 / bottom 10 / too young; **DUPLICATE TITLES** (always
flag); long-form; daily views split breakout / rest (the halo); every breakdown; every
retention curve in **seconds**, split at 30 s against YouTube's "other videos".

**Names and families.** A video's name is its ledger row's clip stem, else the `.md` whose
title options hold its title (exact; for a Short, else >= 0.6 word overlap). The entry's
profile in `families.py` turns the name, or failing that the title, into a family; `fam_src`
says which route each video took (`title` on a video newer than the ledger = a missing ledger
row). A wrong family → one row `id,family` in `reports/video_families.csv`, never a regex
change for one video. `Families: none` = every Short `other`, until the channel has enough
videos to name families: then add its profile to `families.py`. The classifier is the weakest
part of the pipeline: check the top-15 and bottom-10 families by eye every run.

## 4. Write the report: `reports/<DATE>_channel_analysis.md`

Keep the section order so reports diff:

0. **Headline**: the one-paragraph answer to "are we on a good path", the Overview numbers, the
   delta table vs the previous report.
1. **What changed since last time**: which of the previous report's conclusions still hold,
   which are overturned, with the numbers (a first run says so). Then add a banner to the
   PREVIOUS report saying the same, so nobody acts on a stale conclusion.
2. **The batch under judgement**: every video of the latest batch with views / stayed / APV /
   subs against the cohort median, grouped by what its `.md` or docs say it was testing (new
   look vs new rule, new subject, song A vs song B). This section decides the next batch.
3. Families, cadence, correlations, length (commentary only where a number moved).
4. Long-form. 5. Audience and traffic. 6. Monetisation (once the earnings columns are
   non-zero). 7. Risks and hygiene (duplicates, title shapes reused close together).
8. **Recommendations**: numbered, each tied to a number above.
9. **Not on disk**: exports that failed or were never pulled, and nothing else.

Every claim carries its number and source table; "n is small" when n < 6; no correlation
without its n; length is a family proxy, so no length advice without that confound (and the
channel's own length rule); a Shorts spike's decay is not a penalty: say so before it happens.
A conclusion that contradicts a rule in the channel's `CLAUDE.md` goes in §1 with its numbers;
the rule is rewritten only where the entry's Refresh says the audit wins, else it is a
recommendation to Luka. No revenue decision from a few days of data (RPM is unreadable before
~7 days of monetised data and noisy for weeks).

## 5. Land it

1. **Outcomes:** `YA outcomes --date <DATE> --since <BATCH_START>` prints one MEASURED OUTCOME
   block per named video; add `--write` to put them into the entry's Outcomes docs (once per
   video per date). Videos without a name are listed: give the important ones a ledger row
   (where the channel keeps one) or a `reports/video_families.csv` row.
2. **Every doc that carries a number this run changed**: the entry's Refresh docs; every
   pointer to the previous report (grep its filename in `CLAUDE.md`, `docs/`, `.claude/`) now
   names the new one; the previous report's superseded banner (§4.1). In the hub, when it is on
   this PC: the channel's sheet `_hq/channels/<channel>.md` and its roster row in the hub
   `CLAUDE.md`, each number with its date.
3. **Shareable page:** publish the report as an Artifact: headline tiles, the daily views split
   breakout / rest, the retention curve(s) in seconds, the family table, the batch scorecard,
   the recommendations. Title = the entry's Page, the same every run, with the date in the
   description so the gallery stays readable.
4. **Reply:** the headline, the batch scorecard, the recommendations, what didn't export, the
   page link and a link to the report file. Nothing is committed, nothing goes to memory, and
   nothing is fixed on YouTube (a duplicate, a title, a pinned comment: Luka's or the upload
   skill's).
