"""Numeric half of the shared youtube-analytics skill (SKILL.md beside this file).

Everything that must come out IDENTICAL every run, on every channel, so two reports a month
apart (and two channels) compare like for like: the export layout, the family of every
video, the cohort tables, the correlations, the breakout split, the cadence, the retention
curves in seconds, and the per-video outcome blocks.

  ingest      --date D                   unzip every Studio export of this channel whose
                                         window ends 0-2 days before D from ~/Downloads into
                                         reports/exports/<D>/<kind>/ (never over an earlier
                                         one: a second export of a kind lands in <kind>_2/)
  ingest-ret  --date D --id VIDEO_ID [--zip PATH | --match TITLE_PIECE]
                                         file the newest "Audience retention*.zip" (fresh:
                                         < --max-age minutes) into reports/exports/<D>/retention_<id>/
  analyze     --date D [--prev P] [--since YYYY-MM-DD] [--mature 7]
                                         print every table; write summary.md + summary.json
                                         beside the exports
  outcomes    --date D [--since YYYY-MM-DD] [--write]
                                         one MEASURED OUTCOME block per named video;
                                         --write puts it into the doc the channel facts name

The channel is the folder this file sits in (<channel>/.claude/skills/youtube-analytics/),
else the current folder, else --root. What differs per channel comes from the
`- **Analytics**` list under `## Channel facts` in its CLAUDE.md (see SKILL.md); the family
vocabularies are profiles in families.py.

NAMES: the upload ledger maps a YouTube id to its clip stem; a video with no ledger row is
matched by title against the title options in the channel's video .md files (exact, then
>= 0.6 word overlap). reports/video_families.csv (id,family) overrides every family.

Dates: the export's last full day is the zip's end date minus one; ages are measured
against that, never against today.
"""
import argparse
import json
import re
import shutil
import sys
import time
import zipfile
from pathlib import Path

import numpy as np
import pandas as pd

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.dont_write_bytecode = True   # no __pycache__ in a synced skill folder
import families as FAM  # noqa: E402

LONGFORM_SEC = 180
DOWNLOADS = Path.home() / "Downloads"
# Studio zip name prefix -> folder name under reports/exports/<date>/
KINDS = {
    "Content": "content",
    "Traffic source": "traffic_source",
    "Content type": "content_type",
    "Device type": "device_type",
    "Geography": "geography",
    "Sharing service": "sharing_service",
    "Viewer age": "viewer_age",
    "Viewer gender": "viewer_gender",
}
C = None   # the channel (set in main)


def log(*a):
    print(*a, flush=True)


def ascii_(s):
    return str(s).encode("ascii", "ignore").decode("ascii").strip()


# ---------------------------------------------------------------- channel facts
class Channel:
    def __init__(self, root):
        self.root = root
        f = read_facts(root)
        ch = f.get("channel", "")
        ids = re.findall(r"UC[\w-]{22}", ch)
        names = [t for t in re.findall(r"`([^`]+)`", ch) if not re.fullmatch(r"UC[\w-]{22}", t)]
        self.id = ids[0] if ids else ""
        self.name = names[0] if names else ch.split(",")[0].strip(" `\"")
        if not self.name or self.name.lower() == "unknown":
            sys.exit("Analytics facts: `channel:` has no name (the name Studio puts in its zips)")
        led = paths(f.get("ledger"))
        self.ledger = root / led[0] if led else None
        self.title_globs = paths(f.get("title options"))
        self.fam_key = (paths(f.get("families")) or [""])[0]
        self.prof = FAM.profile(self.fam_key)
        out = paths(f.get("outcomes"))
        self.outcomes = out[0] if out else ""
        ib = re.search(r"\d{4}-\d{2}-\d{2}", f.get("ignore before", ""))
        self.ignore_before = pd.Timestamp(ib.group(0)) if ib else None
        self.exports = root / "reports" / "exports"
        self.overrides = root / "reports" / "video_families.csv"


def read_facts(root):
    """The `- **Analytics**` sub-list under `## Channel facts` in CLAUDE.md -> {key: value}."""
    p = root / "CLAUDE.md"
    txt = p.read_text(encoding="utf-8-sig") if p.exists() else ""
    sec = re.search(r"^## Channel facts[^\n]*\n(.*?)(?=^## |\Z)", txt, re.S | re.M)
    blk = sec and re.search(r"^- \*\*Analytics:?\*\*[^\n]*\n((?:[ \t]+\S[^\n]*(?:\n|\Z))+)",
                            sec.group(1), re.M)
    if not blk:
        sys.exit(f"no `- **Analytics**` list under `## Channel facts` in {p} (see SKILL.md)")
    facts, key = {}, None
    for line in blk.group(1).splitlines():
        m = re.match(r"\s+- ([\w ]+?):\s*(.*)", line)
        if m:
            key = m.group(1).strip().lower()
            facts[key] = m.group(2).strip()
        elif key and line.strip():
            facts[key] += " " + line.strip()
    return facts


def paths(v):
    """`a`, `b` -> [a, b]; none / unknown / empty -> []."""
    v = (v or "").strip()
    if not v or re.match(r"(none|unknown)\b", v.strip("` ").lower()):
        return []
    tick = re.findall(r"`([^`]+)`", v)
    return tick or [x.strip() for x in v.split(",") if x.strip()]


def export_dir(date):
    return C.exports / date


# ---------------------------------------------------------------- ingest
def zip_end(name):
    m = re.search(r"\d{4}-\d{2}-\d{2}_(\d{4}-\d{2}-\d{2})", name)
    return pd.Timestamp(m.group(1)) if m else None


def cmd_ingest(a):
    out = export_dir(a.date)
    out.mkdir(parents=True, exist_ok=True)
    found = 0
    # LONGEST prefix first: "Content type ..." also starts with "Content "
    prefixes = sorted(KINDS.items(), key=lambda kv: -len(kv[0]))
    for z in sorted(DOWNLOADS.glob("*.zip"), key=lambda p: p.stat().st_mtime):
        # Studio names the zip after its last DATA day: the pull date or the day before.
        # Older ones are a stale run's leftovers and would hand analyze the wrong end date.
        end = zip_end(z.name)
        if end is None or not 0 <= (pd.Timestamp(a.date) - end).days <= 2:
            continue
        for prefix, kind in prefixes:
            if not (z.name.startswith(prefix + " ") and C.name in z.name):
                continue
            # never over an earlier export of the kind: every VIDEO-table export (the
            # daily-curves one too) is named "Content ...zip" and once replaced the main table
            dest, n = out / kind, 2
            while dest.exists() and any(dest.glob("*.zip")):
                dest, n = out / f"{kind}_{n}", n + 1
            dest.mkdir(exist_ok=True)
            with zipfile.ZipFile(z) as zf:
                zf.extractall(dest)
            shutil.move(str(z), str(dest / z.name))   # keep the zip beside its CSVs
            log(f"ingested {dest.name:15s} <- {z.name}")
            if dest.name != kind:
                log(f"  ! {kind}/ already held an export: a daily-curves pull -> rename "
                    f"{dest.name}/ to daily_curves/; a re-pull that should win -> swap it with {kind}/")
            found += 1
            break
    if not found:
        log(f"nothing to ingest: no '<Kind> ... {C.name}.zip' ending 0-2 days before {a.date} in Downloads")
    log(f"exports in {out}")


def cmd_ingest_ret(a):
    if a.zip:
        z = Path(a.zip)
        if not z.exists():
            sys.exit(f"no such zip: {z}")
    else:
        zs = sorted(DOWNLOADS.glob("Audience retention *.zip"), key=lambda p: p.stat().st_mtime)
        if a.match:   # named by TITLE, not id: with several queued, match a piece of the title
            zs = [z for z in zs if a.match.lower() in z.name.lower()]
        if not zs:
            sys.exit("no matching 'Audience retention*.zip' in Downloads")
        z = zs[-1]
        age = (time.time() - z.stat().st_mtime) / 60
        if age > a.max_age:   # a stale zip would be filed silently under the wrong id
            sys.exit(f"newest match is {age:.0f} min old ({z.name}): not this export. "
                     f"Download it again, or pass --zip if it really is the one")
    dest = export_dir(a.date) / f"retention_{a.id}"
    dest.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(z) as zf:
        zf.extractall(dest)
    shutil.move(str(z), str(dest / z.name))
    log(f"ingested retention_{a.id} <- {z.name}")


# ---------------------------------------------------------------- names and families
def ledger_map():
    """YouTube id -> clip stem (`final/uploaded/throne_9x16.mp4` -> `throne`)."""
    m = {}
    if not (C.ledger and C.ledger.exists()):
        return m
    for line in C.ledger.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|") or "youtube.com" not in line:
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        url = next((c for c in cells if "youtube.com" in c), "")
        clip = next((c for c in cells if ".mp4" in c), "")
        vid = re.search(r"(?:shorts/|v=|youtu\.be/)([\w-]{11})", url)
        stem = re.search(r"([\w][\w.-]*?)(?:_9x16|_16x9)?\.mp4", clip)
        if vid and stem:
            m[vid.group(1)] = stem.group(1)
    return m


def scene_of(stem):
    """Clip stem -> scene: the longest leading token run that names a scenes/<name>.py
    (`double_up_pentagon` -> `double_up`)."""
    toks = stem.split("_")
    for n in range(len(toks), 0, -1):
        cand = "_".join(toks[:n])
        if (C.root / "scenes" / f"{cand}.py").exists():
            return cand
    return stem


def _norm_title(t):
    """Compare titles without emoji, hashtags, punctuation or case."""
    t = re.sub(r"#\w+", "", str(t)).lower()
    t = re.sub(r"[^a-z0-9 ]", "", t)
    return re.sub(r"\s+", " ", t).strip()


ITEM = re.compile(r"\s*(?:\d+[.)]|[-*])\s+(.+)")


def _option_titles(body):
    """One title-option line -> every reading of it that may be the uploaded title. Seen:
    `Title` -- note | **`Title`** — note | **Title** — note | **Label** — Title |
    *(label)* `Title` | Title *(label)* | Title (note). A reading that is no title is
    harmless: it never equals a real one."""
    tick = re.findall(r"`([^`]+)`", body)
    pieces = re.split(r"\s+(?:--|—)\s+", body)
    out = tick[:1] + [pieces[0].strip().strip("`*").strip()]
    for p in pieces:
        t = re.sub(r"\s*\((?:[^()]*)\)\s*$", "", p)                     # (note) behind
        t = re.sub(r"^\*\*[^*]+\*\*\s*[—-]\s*", "", t).strip("*\"“” ")  # **Label** - in front
        t2 = re.sub(r"^\*?\([^()]*\)\*?\s*", "", t)                      # *(label)* in front
        out += [t, re.sub(r"\s*\*?\([^()]*\)\*?$", "", t2).strip("*\"“” `")]   # or behind
    return [re.sub(r"\s#\w+", "", t).strip() for t in out]


def docs_title_map():
    """normalised title option -> video name (the .md's stem), from every .md the channel
    facts name: the list under a `## Title...` heading or a `**Title...**` label, or the title
    beside the label (`**Title:** X`)."""
    m = {}
    for g in C.title_globs:
        for doc in sorted(C.root.glob(g)):
            txt = doc.read_text(encoding="utf-8", errors="ignore")
            lists = []
            sec = re.search(r"## Title[^\n]*\n(.*?)(?:\n## |\Z)", txt, re.S)
            if sec:
                lists.append(sec.group(1).splitlines())
            for lab in re.finditer(r"^\*\*Titles?\b[^\n]*", txt, re.M):
                inline = re.match(r"\*\*Titles?:?\*\*:?\s+(?!\()(.+)", lab.group(0))
                if inline:
                    lists.append([f"1. {inline.group(1)}"])
                    continue
                run = []
                for ln in txt[lab.end():].lstrip("\n").splitlines():
                    if not ITEM.match(ln):
                        break
                    run.append(ln)
                lists.append(run)
            for lines in lists:
                for ln in filter(ITEM.match, lines):
                    for t in _option_titles(ITEM.match(ln).group(1)):
                        if _norm_title(t):
                            m[_norm_title(t)] = doc.stem
    return m


def overrides():
    if not C.overrides.exists():
        return {}
    o = pd.read_csv(C.overrides)
    return dict(zip(o["id"], o["family"]))


def assign_families(d):
    lm, ov, tm = ledger_map(), overrides(), docs_title_map()
    fams, names, src = [], [], []
    for vid, title, dur in zip(d.Content, d["Video title"], d.dur):
        name = lm.get(vid.strip(), "")
        by = "ledger"
        if not name:   # no ledger row: match the title to a doc's options
            name = tm.get(_norm_title(title), "")
            by = "doc-title"
        if not name and dur < LONGFORM_SEC:   # retitled at upload: best word overlap >= 0.6
            words = set(_norm_title(title).split())
            best, score = "", 0.0
            for opt, nm in tm.items():
                ow = set(opt.split())
                j = len(words & ow) / max(1, len(words | ow))
                if j > score:
                    best, score = nm, j
            if score >= 0.6:
                name, by = best, "doc-fuzzy"
        if vid.strip() in ov:
            fams.append(ov[vid.strip()]); src.append("override")
        elif dur >= LONGFORM_SEC:
            fams.append(C.prof["longform"]); src.append("length")
        elif name:
            fams.append(C.prof["name_family"](name)); src.append(by)
        else:
            fams.append(FAM.family_from_title(C.prof, title, dur, LONGFORM_SEC)); src.append("title")
        names.append(name)
    d["fam"], d["name"], d["fam_src"] = fams, names, src
    if C.ignore_before is not None:   # a rebranded channel's old videos leave every table
        d.loc[d.pub < C.ignore_before, "fam"] = "pre_pivot"
    return d


# ---------------------------------------------------------------- load
def load_content(date):
    p = export_dir(date) / "content" / "Table data.csv"
    d = pd.read_csv(p)
    ends = [zip_end(z.name) for z in (export_dir(date) / "content").glob("Content *.zip")]
    ends = [e for e in ends if e is not None]
    end = max(ends) if ends else pd.Timestamp(date)
    end = end - pd.Timedelta(days=1)   # Studio's last full day is the day before the zip's end
    total = d[d.Content == "Total"].iloc[0] if (d.Content == "Total").any() else None
    d = d[d.Content != "Total"].copy()
    d["pub"] = pd.to_datetime(d["Video publish time"], format="%b %d, %Y", errors="coerce")
    d["dur"] = pd.to_numeric(d.Duration, errors="coerce").fillna(0).astype(int)
    d["age"] = (end - d.pub).dt.days
    d["title"] = d["Video title"].map(ascii_).str.slice(0, 60)
    d = assign_families(d)
    for col in ("Shares", "Likes", "Subscribers", "Engaged views", "Comments added"):
        if col not in d:            # older exports were pulled with fewer metrics
            d[col] = np.nan
    v = d.Views.replace(0, np.nan)
    d["subs1k"] = d.Subscribers / v * 1000
    d["share1k"] = d.Shares / v * 1000
    d["like1k"] = d.Likes / v * 1000
    d["stayed"] = d.get("Stayed to watch (%)")
    d["apv"] = d.get("Average percentage viewed (%)")
    d["ctr"] = d.get("Thumbnail click-through rate (%)")
    return d, total, end


def load_csv(date, kind, name):
    p = export_dir(date) / kind / name
    if not p.exists():
        return None
    t = pd.read_csv(p)
    if name != "Table data.csv":   # the daily files
        t["Date"] = pd.to_datetime(t.Date)
    return t


# ---------------------------------------------------------------- analysis
def sp(x, y):
    from scipy.stats import spearmanr
    x, y = pd.to_numeric(x, errors="coerce"), pd.to_numeric(y, errors="coerce")
    ok = x.notna() & y.notna()
    return float(spearmanr(x[ok], y[ok]).correlation) if ok.sum() >= 5 else float("nan")


def table(df, cols=None):
    return (df if cols is None else df[cols]).to_string(index=False)


def cadence(say, js, shorts, brk, a):
    """Does uploading more Shorts on a day cost each clip views? ENGAGED views are the main
    measure: YouTube changed what "views" counts on 2026-08-27, engaged views did not."""
    allS = shorts[shorts.pub.notna()].copy()
    allS["perday"] = allS.groupby("pub").Content.transform("size")
    cad = allS[(allS.age >= a.mature) & ~allS.Content.isin(brk.Content)].copy()
    say(f"\n## Upload cadence (Shorts >= {a.mature} d old, breakouts excluded, n={len(cad)})\n")
    say("clips per publish day, by month:")
    say(pd.crosstab(allS.pub.dt.to_period("M"), allS.perday).to_string())
    days = cad.groupby(["pub", "perday"]).agg(v=("Views", "sum"), e=("Engaged views", "sum")).reset_index()
    ct = cad.groupby("perday").agg(days=("pub", "nunique"), clips=("Views", "size"),
                                   med_views=("Views", "median"), med_engaged=("Engaged views", "median"),
                                   stayed=("stayed", "median"))
    ct["day_total_views"] = days.groupby("perday").v.median()
    ct["day_total_engaged"] = days.groupby("perday").e.median()
    say("\n" + ct.round(0).to_string())
    say("\n(medians of the day totals; a cadence seen on < 3 days is anecdote, not evidence)")
    js["cadence"] = ct.reset_index().to_dict(orient="records")
    # per-clip cost of one more upload that day, holding family and publish week fixed
    if cad.perday.nunique() > 1 and len(cad) > 20:
        cad["week"] = cad.pub.dt.to_period("W").astype(str)
        for col in ("Engaged views", "Views"):
            c = cad[pd.to_numeric(cad[col], errors="coerce").notna()]
            if c.perday.nunique() < 2 or len(c) <= 20:
                continue
            X = pd.get_dummies(c[["fam", "week"]], drop_first=True).astype(float)
            X.insert(0, "perday", c.perday.astype(float)); X.insert(0, "const", 1.0)
            yv = np.log(pd.to_numeric(c[col], errors="coerce").clip(lower=1).values)
            beta, *_ = np.linalg.lstsq(X.values, yv, rcond=None)
            res = yv - X.values @ beta
            dof = max(1, len(yv) - X.shape[1])
            se = float(np.sqrt((res @ res / dof) * np.linalg.pinv(X.values.T @ X.values)[1, 1]))
            b = float(beta[1])
            pct = lambda v: (np.exp(v) - 1) * 100
            say(f"- per extra upload that day, {col.lower()} per clip: {pct(b):+.0f}% "
                f"(95% CI {pct(b - 1.96*se):+.0f}% to {pct(b + 1.96*se):+.0f}%), "
                f"family + publish week held fixed, n={len(yv)}")
            js.setdefault("cadence_effect", {})[col] = {"pct": round(pct(b), 1),
                                                        "lo": round(pct(b - 1.96*se), 1),
                                                        "hi": round(pct(b + 1.96*se), 1), "n": len(yv)}
    young = allS[allS.age < a.mature]
    if len(young):
        yt = young.groupby("perday").agg(clips=("Views", "size"), med_age=("age", "median"),
                                         med_engaged=("Engaged views", "median"))
        say("\ntoo young to score, by cadence (NOT comparable to the table above -- age differs):")
        say(yt.round(0).to_string())


def analyze(a):
    d, total, end = load_content(a.date)
    md, js = [], {"date": a.date, "export_end": str(end.date()), "channel": C.name}
    say = lambda s="": md.append(s)

    phys = d[d.fam != "pre_pivot"]
    shorts = phys[phys.dur < LONGFORM_SEC]
    long = phys[phys.dur >= LONGFORM_SEC]
    say(f"# yt_analytics summary — {C.name}, export to {end.date()}\n")
    say(f"{len(d)} rows | {len(phys)} physics | {len(shorts)} shorts | {len(long)} long-form | "
        f"families from: {d.fam_src.value_counts().to_dict()}\n")

    # totals + delta vs previous export
    tot = {k: float(phys[k].sum()) for k in ("Views", "Engaged views", "Watch time (hours)",
                                             "Subscribers", "Shares", "Likes") if k in phys}
    js["totals"] = tot
    say("## Totals (physics videos)\n")
    say(table(pd.DataFrame([tot]).round(0)))
    if a.prev:
        dp, _, endp = load_content(a.prev)
        pp = dp[dp.fam != "pre_pivot"]
        say(f"\nvs previous export ({endp.date()}): views {pp.Views.sum():,.0f} -> {tot['Views']:,.0f} "
            f"(+{tot['Views']-pp.Views.sum():,.0f}), subs {pp.Subscribers.sum():,.0f} -> "
            f"{tot['Subscribers']:,.0f}")
        js["prev"] = {"date": a.prev, "views": float(pp.Views.sum()), "subs": float(pp.Subscribers.sum())}

    # breakouts: any Short with > 15% of all Shorts views, excluded from every median below
    share = shorts.Views / shorts.Views.sum()
    brk = shorts[share > 0.15]
    js["breakouts"] = brk.Content.tolist()
    if len(brk):
        say("\n## Breakout(s): > 15% of all Shorts views\n")
        say(table(brk[["Content", "title", "pub", "dur", "Views", "Subscribers", "stayed", "apv", "fam"]]))

    # cohort: mature Shorts since --since, breakouts excluded
    since = pd.Timestamp(a.since) if a.since else end - pd.Timedelta(days=42)
    k = shorts[(shorts.age >= a.mature) & (shorts.pub >= since) & ~shorts.Content.isin(brk.Content)].copy()
    say(f"\n## Cohort: Shorts published since {since.date()}, age >= {a.mature} d, breakouts excluded (n={len(k)})\n")
    fam = (k.groupby("fam").agg(n=("Views", "size"), median=("Views", "median"), mean=("Views", "mean"),
                                stayed=("stayed", "median"), apv=("apv", "median"), subs1k=("subs1k", "median"),
                                dur=("dur", "median")).sort_values("median", ascending=False).round(1))
    say("### Family\n"); say(fam.to_string())
    js["family"] = fam.reset_index().to_dict(orient="records")

    if C.prof["subject"]:
        # an empty cohort makes apply() return a DataFrame
        k["subject"] = k.apply(C.prof["subject"], axis=1) if len(k) else pd.Series(dtype=str)
        subj = (k.groupby(["fam", "subject"]).agg(n=("Views", "size"), median=("Views", "median"),
                                                   engaged=("Engaged views", "median"),
                                                   stayed=("stayed", "median")).round(1))
        say("\n### Family x subject (marble vs swapped-in subject)\n"); say(subj.to_string())
        js["subject"] = subj.reset_index().to_dict(orient="records")

    prev_k = shorts[(shorts.age >= a.mature) & (shorts.pub < since) & ~shorts.Content.isin(brk.Content)]
    if len(prev_k):
        pf = prev_k.groupby("fam").Views.agg(["size", "median"]).round(0)
        say(f"\n### Same families, before {since.date()} (n={len(prev_k)})\n"); say(pf.to_string())

    k["week"] = k.pub.dt.to_period("W").astype(str)
    wk = k.groupby("week").agg(n=("Views", "size"), median=("Views", "median"), stayed=("stayed", "median"),
                               dur=("dur", "median"), subs=("Subscribers", "sum")).round(0)
    say("\n### By publish week\n"); say(wk.to_string())

    cadence(say, js, shorts, brk, a)

    say("\n### Spearman rho vs views (cohort)\n")
    corr = {}
    for col, lab in (("dur", "duration"), ("stayed", "stayed to watch"), ("apv", "avg % viewed"),
                     ("ctr", "thumbnail CTR"), ("share1k", "shares/1k"), ("like1k", "likes/1k")):
        if col in k:
            corr[lab] = round(sp(k[col], k.Views), 3)
            say(f"- {lab}: {corr[lab]:+.3f}")
    js["corr"] = corr
    k["band"] = pd.cut(k.dur, [0, 25, 35, 45, 70, 999], labels=["<=25", "26-35", "36-45", "46-70", "70+"])
    say("\n### Length bands (cohort)\n")
    say(k.groupby("band", observed=True).agg(n=("Views", "size"), median=("Views", "median"),
                                             stayed=("stayed", "median")).round(0).to_string())

    cols = [c for c in ["title", "pub", "dur", "Views", "stayed", "apv", "Subscribers", "Shares",
                        "fam", "fam_src"] if c in k]
    say("\n### Top 15 (cohort)\n"); say(table(k.sort_values("Views", ascending=False).head(15)[cols]))
    say("\n### Bottom 10 (cohort)\n"); say(table(k.sort_values("Views").head(10)[cols]))
    say("\n### Too young to score (age < mature)\n")
    say(table(shorts[shorts.age < a.mature].sort_values("pub")[["title", "pub", "age", "Views", "stayed", "fam"]]))

    stem = shorts["Video title"].str.lower().str.replace(r"[^a-z0-9 ]", "", regex=True) \
        .str.replace(r"\s+", " ", regex=True).str.replace("simulation satisfying shorts", "").str.strip()
    dup = shorts[stem.duplicated(keep=False)]
    if len(dup):
        say("\n## DUPLICATE TITLES (title-gap rule)\n"); say(table(dup[["Content", "title", "pub", "Views"]]))

    if len(long):
        say("\n## Long-form\n")
        lc = [c for c in ["title", "pub", "dur", "Views", "Thumbnail impressions", "ctr", "Average view duration",
                          "apv", "Subscribers", "Watch time (hours)"] if c in long]
        say(table(long.sort_values("pub")[lc]))
        say(f"\nsubs/1k: shorts {shorts.Subscribers.sum()/shorts.Views.sum()*1000:.2f}, "
            f"long-form {long.Subscribers.sum()/long.Views.sum()*1000:.2f}")

    # daily channel views split breakout / rest (the halo measure)
    t, c = load_csv(a.date, "content", "Totals.csv"), load_csv(a.date, "content", "Chart data.csv")
    if t is not None:
        m = t.copy()
        if c is not None and len(brk):
            b = c[c.Content.isin(brk.Content)].groupby("Date").Views.sum().rename("breakout")
            m = m.merge(b, on="Date", how="left").fillna(0)
            m["rest"] = m.Views - m.breakout
        say("\n## Daily channel views, last 28 days\n"); say(table(m.tail(28)))
        js["daily_last7_mean"] = float(m.Views.tail(7).mean())

    for kind, label in (("traffic_source", "Traffic source"), ("content_type", "Content type"),
                        ("device_type", "Device type"), ("sharing_service", "Sharing service"),
                        ("viewer_gender", "Gender"), ("viewer_age", "Age"),
                        ("geography", "Geography (top 12)")):
        tb = load_csv(a.date, kind, "Table data.csv")
        if tb is not None:
            say(f"\n## {label}\n"); say(table(tb.head(13)))

    for r in sorted(export_dir(a.date).glob("retention_*")):
        vid = r.name.split("_", 1)[1]
        row = d[d.Content.str.strip() == vid]
        src = next((p for p in (r / "Organic.csv", r / "All.csv") if p.exists()), None)
        if not len(row) or src is None:
            continue
        dur = int(row.dur.iloc[0])
        o = pd.read_csv(src); o["sec"] = o.iloc[:, 0] / 100 * dur
        ret = o.iloc[:, 1]; cmp_ = o.iloc[:, 2] if o.shape[1] > 2 else None
        pts = [0, 3, 5, 10, 15, 20, 30, 40, 50, 60] if dur < LONGFORM_SEC else [0, 10, 30, 60, 90, 120, 180, 240, 300, 360, 480]
        pts = [p for p in pts if p <= dur] + [dur - 1]
        curve = {p: round(float(np.interp(p, o.sec, ret)), 1) for p in pts}
        say(f"\n## Retention: {row.title.iloc[0]} ({dur} s, {int(row.Views.iloc[0]):,} views)\n")
        say("abs % at s: " + ", ".join(f"{p}:{v}" for p, v in curve.items()))
        if cmp_ is not None:
            say(f"vs comparable videos: first 30 s {cmp_[o.sec <= 30].mean():+.1f}, after {cmp_[o.sec > 30].mean():+.1f}")
        js.setdefault("retention", {})[vid] = curve

    out = export_dir(a.date)
    (out / "summary.md").write_text("\n".join(md), encoding="utf-8")
    (out / "summary.json").write_text(json.dumps(js, indent=1, default=str), encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    print("\n".join(md))
    print(f"\nwrote {out/'summary.md'} and summary.json")


# ---------------------------------------------------------------- outcomes
def outcome_doc(name):
    """The doc a video's outcome goes into, per the `outcomes:` fact: `<name>` = one doc per
    video (newest block on top), `<scene>` = the scene's doc, shared by its clips (appended)."""
    pat = C.outcomes.replace("<scene>", scene_of(name)).replace("<name>", name)
    if any(ch in pat for ch in "*?["):
        hits = sorted(C.root.glob(pat))
        return hits[0] if hits else C.root / pat.replace("**/", "")
    return C.root / pat


def outcome_block(r, date, clip_line):
    lines = [f"> ## MEASURED OUTCOME {date} (yt_analytics, export to the day before)",
             ">",
             f"> **{int(r.Views):,} views, {int(r.Subscribers)} subs, {int(r.Shares)} shares, {int(r.Likes)} likes** "
             f"at {int(r.age)} days old; stayed to watch {r.stayed}%, APV {r.apv}%, "
             f"{r.get('Average view duration', '?')} AVD on {int(r.dur)} s. Family `{r.fam}`."
             + (f" Clip `{r['name']}`." if clip_line else "")]
    return "\n".join(lines) + "\n\n"


def cmd_outcomes(a):
    d, _, end = load_content(a.date)
    since = pd.Timestamp(a.since) if a.since else end - pd.Timedelta(days=42)
    sel = d[(d.pub >= since) & (d.name != "")].sort_values("pub")
    shared = "<scene>" in C.outcomes
    sys.stdout.reconfigure(encoding="utf-8")
    if not C.outcomes:
        print("outcomes: none in this channel's facts -- the report is the record; blocks only printed")
    for _, r in sel.iterrows():
        blk = outcome_block(r, a.date, shared)
        doc = outcome_doc(r["name"]) if C.outcomes else None
        print(f"--- {r['name']}" + (f" -> {doc.relative_to(C.root)} ({'doc found' if doc.exists() else 'NO DOC'})"
                                   if doc else ""))
        print(blk)
        if not (a.write and doc and doc.exists()):
            continue
        txt = doc.read_text(encoding="utf-8")
        if f"MEASURED OUTCOME {a.date}" in txt and (not shared or f"Clip `{r['name']}`" in txt):
            continue
        if shared:
            txt = txt + ("" if txt.endswith("\n") else "\n") + "\n" + blk
        else:
            head, _, rest = txt.partition("\n")
            txt = head + "\n\n" + blk + rest
        doc.write_text(txt, encoding="utf-8", newline="\r\n" if b"\r\n" in doc.read_bytes() else "\n")
        print(f"    written to {doc.name}")
    unmatched = d[(d.pub >= since) & (d.name == "") & (d.dur < LONGFORM_SEC)]
    if len(unmatched):
        print(f"\n{len(unmatched)} Shorts since {since.date()} have no name (no ledger row, no title match "
              f"in the channel's .md files):")
        print(table(unmatched[["Content", "title", "pub", "Views"]]))


def main():
    global C
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--root", help="the channel folder (default: the one this script is synced into, else cwd)")
    s = p.add_subparsers(dest="cmd", required=True)
    q = s.add_parser("ingest"); q.add_argument("--date", required=True); q.set_defaults(f=cmd_ingest)
    q = s.add_parser("ingest-ret"); q.add_argument("--date", required=True); q.add_argument("--id", required=True)
    q.add_argument("--zip"); q.add_argument("--match", help="piece of the video title the zip name must contain")
    q.add_argument("--max-age", type=float, default=30, help="minutes; an older newest zip is refused")
    q.set_defaults(f=cmd_ingest_ret)
    q = s.add_parser("analyze"); q.add_argument("--date", required=True); q.add_argument("--prev")
    q.add_argument("--since"); q.add_argument("--mature", type=int, default=7); q.set_defaults(f=analyze)
    q = s.add_parser("outcomes"); q.add_argument("--date", required=True); q.add_argument("--since")
    q.add_argument("--write", action="store_true"); q.set_defaults(f=cmd_outcomes)
    a = p.parse_args()
    if a.root:
        root = Path(a.root).resolve()
    elif HERE.parent.name == "skills" and HERE.parent.parent.name == ".claude":
        root = HERE.parents[2]
    else:
        root = Path.cwd()
    C = Channel(root)
    a.f(a)


if __name__ == "__main__":
    main()
