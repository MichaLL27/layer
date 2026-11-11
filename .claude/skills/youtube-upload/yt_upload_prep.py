"""Mechanical half of the shared youtube-upload skill (SKILL.md beside this file).

Everything that must come out IDENTICAL every run, on every channel: a video's upload text, a
random upload order that keeps look-alikes apart, the publish slots, the ledger row, the move to
uploaded/ and the pin bookkeeping. The browser work is SKILL.md.

  facts                                   the channel's Upload entry, as this tool reads it
  meta    <video> ...                     each video's upload text -> JSON: the title options
                                          (the entry's suffix appended, length checked), the
                                          description and tags, the comment to pin, the file to
                                          upload, its duration, problems
  order   <video> ... [--after NAME ...]  a random upload order that keeps look-alikes apart
  slots   --count N [--existing "YYYY-MM-DD HH:MM" ...] [--immediate] [--now "..."]
                                          the next free publish times, in the channel's time
  record  --clip <video> --when "YYYY-MM-DD HH:MM" --title .. --url .. [--related ..]
          [--no-move] [--own-comment]     one ledger row, then the move to uploaded/
  due     [--now "..."]                   ledger rows now live whose comment is still pending
  pinned  --url <url>                     marks that row's comment pinned

The channel is --root, else the folder this file is synced into
(<channel>/.claude/skills/youtube-upload/), else the current folder. What differs per channel
comes from the `- **Upload:**` list under `## Channel facts` in its CLAUDE.md (SKILL.md has the
format). The entry's Video line picks one of three layouts, each the code path of the channel
it came from:
  pair         Satisfying 2D Sims: <stem>_9x16.mp4 + _16x9.mp4, <stem>.md beside them
  compilation  Satisfying 3D Sims: compilation_<name>_9x16.mp4, docs/uploads/compilation_<name>.md
               read through the channel's own parser module (tools/make_upload_txt.py)
  folder       the standard layout: one NN_<name>/ folder per video, NN_<name>.md `## Upload`
A <video> is its file, its .md or its folder; compilation and folder also take its name.

SCHEDULE: the entry's slots per "day" (a day runs from the first slot to just before it the next
day), at most the entry's count a day (manual uploads count). A slot is a TARGET: one already
served by a Short is skipped; a MISSED one (< now+10 min) is never skipped but moves to now+10 min
on the next 15-minute mark. Any time is then pushed forward in 15-minute steps until it is the
entry's gap from every other Short. --immediate: now+10 min on the next 5-minute mark, first.

LEDGER: `| # | publish | clip | title | url | related video | comment |`, comment `pending` until
the pin is posted. A video whose pin is not the entry's fixed comment (a comment-reply video:
--own-comment; every video of a channel whose pin comes from each .md) is recorded as
`pending: <its comment>` (line breaks as <br>), so `due` hands it over even after the .md is gone.
"""
import argparse
import datetime as dt
import importlib.util
import json
import random
import re
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True   # no __pycache__ in a synced skill folder or a channel's tools/

STEP = dt.timedelta(minutes=15)     # Studio's time dropdown grid
LEAD = dt.timedelta(minutes=10)     # a missed slot / "publish now" goes out at now + 10 min
ROOT = None                         # the channel folder (set in main)
F = None                            # its Upload facts (set in main)


def out(obj):
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(obj, ensure_ascii=False, indent=2))


def rel(p):
    p = Path(p)
    return p.relative_to(ROOT).as_posix() if p.is_relative_to(ROOT) else str(p)


# ---------------------------------------------------------------- channel facts
def read_facts(root):
    """The `- **Upload**` sub-list under `## Channel facts` in CLAUDE.md -> {key: value}."""
    p = root / "CLAUDE.md"
    txt = p.read_text(encoding="utf-8-sig") if p.exists() else ""
    sec = re.search(r"^## Channel facts[^\n]*\n(.*?)(?=^## |\Z)", txt, re.S | re.M)
    blk = sec and re.search(r"^- \*\*Upload:?\*\*[^\n]*\n((?:[ \t]+\S[^\n]*(?:\n|\Z))+)",
                            sec.group(1), re.M)
    if not blk:
        sys.exit(f"no `- **Upload:**` list under `## Channel facts` in {p} (see SKILL.md): "
                 "ask Luka for the facts once and write them there")
    facts, key = {}, None
    for line in blk.group(1).splitlines():
        m = re.match(r"\s+- ([\w ]+?):\s*(.*)", line)
        if m:
            key = m.group(1).strip().lower()
            facts[key] = m.group(2).strip()
        elif key and line.strip():
            facts[key] += " " + line.strip()
    return facts


def ticks(v):
    return re.findall(r"`([^`]+)`", v or "")


def unknown(v):
    """Empty, `unknown ...` or `none ...`."""
    v = (v or "").strip().strip("`").strip().lower()
    return not v or v.startswith(("unknown", "none"))


def _chan(v):
    """`name`, `UC...`, `@handle` -> (name, id, handle)."""
    ids = re.findall(r"UC[\w-]{22}", v or "")
    hs = re.findall(r"@[\w.-]+", v or "")
    names = [t for t in ticks(v) if not re.fullmatch(r"UC[\w-]{22}", t) and not t.startswith("@")]
    return (names[0] if names else ""), (ids[0] if ids else None), (hs[0] if hs else None)


class Facts:
    def __init__(self, root):
        self.root = root
        f = self.raw = read_facts(root)
        self.name, self.id, self.handle = _chan(f.get("channel", ""))
        self.scope = f.get("scope", "unknown")
        self.made_for_kids = f.get("made for kids", "unknown")
        c = f.get("collaborator", "")
        self.collab = None if unknown(c) else _chan(c)
        self.settings = f.get("settings", "")

        # Schedule: `HH:MM ...` Area/City (UTC+N), N a day, M min apart. Without one, ledger
        # times are this PC's local time.
        s = f.get("schedule", "")
        self.sched, self.city = None, None
        self.tz = dt.datetime.now().astimezone().tzinfo
        self.tz_label = "this PC's local time"
        if not unknown(s):
            times = next((t for t in ticks(s) if re.search(r"\d{1,2}:\d{2}", t)), "")
            hm = [tuple(int(x) for x in t.split(":")) for t in re.findall(r"\d{1,2}:\d{2}", times)]
            per = re.search(r"(\d+)\s*(?:a|per)\s*day", s)
            tz = re.search(r"UTC\s*([+-])\s*(\d{1,2})(?::(\d{2}))?", s)
            gap = re.search(r"(\d+)\s*min", s)
            zone = re.search(r"\b([A-Z][A-Za-z]+/[A-Za-z_]+)\b", s)
            if not (hm and tz):
                sys.exit("Upload facts: Schedule must read `HH:MM HH:MM ...` <Area/City> (UTC+N), "
                         f"[<N> a day], [<M> min apart] -- got: {s}")
            sign = 1 if tz.group(1) == "+" else -1
            off = sign * dt.timedelta(hours=int(tz.group(2)), minutes=int(tz.group(3) or 0))
            city = zone.group(1).split("/")[-1].replace("_", " ") if zone else "local"
            self.tz = dt.timezone(off, city)
            utc = f"UTC{tz.group(1)}{int(tz.group(2))}" + (f":{tz.group(3)}" if tz.group(3) else "")
            self.tz_label = f"{zone.group(1)} ({utc})" if zone else utc
            self.city = city
            # the pace is the channel's: N a day defaults to the times listed, the gap to 45 min
            self.sched = {"slots": hm, "per_day": int(per.group(1)) if per else len(hm),
                          "gap": dt.timedelta(minutes=int(gap.group(1)) if gap else 45)}

        # Text: title + `#tag`; description and tags empty on a Short | as the .md writes it
        t = f.get("text", "")
        sfx = next((x for x in ticks(t) if x.startswith("#")), None)
        self.suffix = " " + sfx if sfx else ""
        self.short_empty = re.search(r"\bempty\b", t, re.I) is not None

        # Pinned comment: `line 1` + `line 2` (fixed) | the video's .md
        pc = f.get("pinned comment", "")
        lines = ticks(pc)
        self.comment = "\n".join(lines) if lines and not re.match(r"(the|each)\b", pc, re.I) else None

        led = ticks(f.get("ledger", ""))
        self.ledger = None if unknown(f.get("ledger")) or not led else root / led[0]

        # Video: pair | compilation | folder: ...
        v = f.get("video", "")
        m = re.match(r"(pair|compilation|folder)\b", v)
        self.layout = m.group(1) if m else None
        if self.layout == "compilation":
            tk = ticks(v)
            clip = next((x for x in tk if x.endswith("_9x16.mp4")), None)
            doc = next((x for x in tk if x.endswith(".md")), None)
            self.parser = next((x for x in tk if x.endswith(".py")), None)
            if not (clip and doc and self.parser):
                sys.exit("Upload facts: a compilation Video line names the clip "
                         "(`.../<prefix><name>_9x16.mp4`), the doc (`.../<prefix><name>.md`) and "
                         f"the parser (`tools/<x>.py`) -- got: {v}")
            self.rtu = root / Path(clip).parent
            self.docs = root / Path(doc).parent
            self.prefix = Path(clip).name.split("<name>")[0]

        # Uploaded: `base`; `folder/` -> `dest`; non-PD music (`guard.py`) -> `dest`
        parts = [p.strip() for p in f.get("uploaded", "").split(";")]
        base = ticks(parts[0]) if parts else []
        self.uploaded = root / (base[0].split("<")[0] if base else "final/uploaded/")
        self.routes, self.guard = {}, None
        for p in parts[1:]:
            lr = re.split(r"->|→", p, maxsplit=1)
            if len(lr) != 2 or not ticks(lr[1]):
                continue
            left, dest = ticks(lr[0]), root / ticks(lr[1])[0]
            py = next((x for x in left if x.endswith(".py")), None)
            if py:
                self.guard = (py, dest)
            elif left:
                self.routes[left[0].strip("/")] = dest

        # Queues: `great/` (...), `monday/` (...): these folders stay when emptied
        self.keep = {x.strip("/") for x in ticks(f.get("queues", "")) if re.fullmatch(r"[\w -]+/", x)}

        o = f.get("order", "")
        m = re.match(r"(family|machine-subject)\b", o)
        self.order = m.group(1) if m else None

    def need(self, what):
        if what == "schedule" and not self.sched:
            sys.exit("Upload facts: Schedule is unknown. Ask Luka for the slots, the count a day, "
                     "the timezone and the gap once, and write them into the entry.")
        if what == "ledger" and not self.ledger:
            sys.exit("Upload facts: Ledger is unknown. Ask Luka where this channel's upload ledger "
                     "lives (one file, e.g. `docs/YOUTUBE-UPLOADS.md`) and write it into the entry.")
        if what == "layout" and not self.layout:
            sys.exit("Upload facts: the Video line must start with pair, compilation or folder "
                     "(SKILL.md).")


def channel_module(path):
    """A channel tool the entry names (`tools/x.py`), imported from the channel, as its own
    scripts import it."""
    p = ROOT / path
    if not p.exists():
        sys.exit(f"Upload facts name {path}, which is not in this channel")
    sys.path.insert(0, str(p.parent))
    if p.stem in sys.modules:
        return sys.modules[p.stem]
    spec = importlib.util.spec_from_file_location(p.stem, p)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[p.stem] = mod
    spec.loader.exec_module(mod)
    return mod


def mut():
    """The compilation layout's title parser (3D: tools/make_upload_txt.py). Its suffix must be
    the entry's, or the staged .txt and the typed title would differ."""
    m = channel_module(F.parser)
    if getattr(m, "TITLE_SUFFIX", F.suffix) != F.suffix:
        sys.exit(f"Upload facts say the title suffix is {F.suffix!r} but {F.parser} appends "
                 f"{m.TITLE_SUFFIX!r}: make them agree first")
    return m


# ---------------------------------------------------------------- time
def now_local():
    return dt.datetime.now(F.tz).replace(second=0, microsecond=0)


def parse_time(s):
    return dt.datetime.strptime(s.strip(), "%Y-%m-%d %H:%M").replace(tzinfo=F.tz)


def parse_when(s):
    """A ledger publish cell: `YYYY-MM-DD HH:MM`, or a hand-written `YYYY-MM-DD (manual)`
    (midnight of that day); None when it holds no date at all."""
    try:
        return parse_time(s)
    except ValueError:
        m = re.match(r"\s*(\d{4}-\d{2}-\d{2})(?!\d)(?:[ T](\d{1,2}:\d{2}))?", s)
        return parse_time(f"{m.group(1)} {m.group(2) or '00:00'}") if m else None


def fmt(t):
    return t.strftime("%Y-%m-%d %H:%M")


def duration(path):
    try:
        r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                            "-of", "csv=p=0", str(path)], capture_output=True, text=True)
        return round(float(r.stdout.strip()), 2)
    except (ValueError, OSError):
        return None


# ---------------------------------------------------------------- layout: pair (2D)
def pair_paths(arg):
    """-> (stem, 9x16 mp4, 16x9 mp4, the .md beside them)."""
    p = Path(arg)
    if not p.is_absolute():
        p = (Path.cwd() / p).resolve()
    stem = re.sub(r"_(9x16|16x9)$", "", p.stem)
    d = p.parent
    return stem, d / f"{stem}_9x16.mp4", d / f"{stem}_16x9.mp4", d / f"{stem}.md"


def md_sections(md_text):
    secs, cur = {}, None
    for line in md_text.splitlines():
        m = re.match(r"^##\s+(.*)", line)
        if m:
            cur = m.group(1).strip().lower()
            secs[cur] = []
        elif cur is not None:
            secs[cur].append(line)
    return secs


def find_sec(secs, prefix):
    for k, v in secs.items():
        if k.startswith(prefix):
            return v
    return None


def pair_titles(lines):
    """2D's two shapes, `1. **Type** -- <title in backticks>` and `1. title (type)`; an intro
    line above the list is ignored."""
    opts = []
    stripped = [l.strip() for l in lines or [] if l.strip()]
    listed = [s for s in stripped if re.match(r"^(?:\d+[.)]|[-*])\s+", s)]
    for s in listed or stripped:            # a list present -> ignore its intro line
        m = re.match(r"^(?:\d+[.)]|[-*])\s+(.*)", s)
        body = m.group(1) if m else s
        tick = re.findall(r"`([^`]+)`", body)
        if tick:
            t = tick[0]
        else:
            t = re.sub(r"\s*\((?:[^()]*)\)\s*$", "", body)       # "(rule restated)"
            t = re.sub(r"^\*\*[^*]+\*\*\s*[—-]\s*", "", t)       # "**Question** — "
            t = t.strip("*\"“” ")
        t = re.sub(r"\s#\w+", "", t).strip()                    # no stray hashtags
        if t:
            opts.append(t)
    return opts


def unwrap(text):
    """Markdown prose -> what it renders: a single line break is a space, a blank line a
    paragraph break."""
    paras = [p for p in re.split(r"\n\s*\n", text.strip()) if p.strip()]
    return "\n\n".join(" ".join(l.strip() for l in p.splitlines() if l.strip()) for p in paras)


def section_text(lines):
    """A section's lines -> its text: a fenced block's content as written, else the prose
    unwrapped."""
    body = "\n".join(lines or []).strip("\n")
    m = re.search(r"^\s*```[^\n]*\n(.*?)^\s*```", body, re.S | re.M)
    return m.group(1).strip() if m else unwrap(body)


def meta_pair(arg):
    stem, v916, v169, md = pair_paths(arg)
    item = {"stem": stem, "short": str(v916), "long_16x9": str(v169), "md": str(md),
            "short_exists": v916.exists(), "duration_s": duration(v916) if v916.exists() else None,
            "problems": []}
    if not v916.exists():
        item["problems"].append("9:16 file missing")
    if not md.exists():
        item["problems"].append(".md missing — write title/description by hand")
        secs = {}
    else:
        secs = md_sections(md.read_text(encoding="utf-8"))
    titles = pair_titles(find_sec(secs, "title"))
    item["title_options"] = []
    for t in titles:
        full = t + F.suffix
        item["title_options"].append({"title": full, "len": len(full), "ok": len(full) <= 100})
    if not titles:
        item["problems"].append("no title options parsed")
    item["description"] = ""    # Shorts: description box stays EMPTY
    item["tags"] = ""           # Shorts: tags field stays EMPTY
    item["comment"] = F.comment
    return item


# ---------------------------------------------------------------- layout: compilation (3D)
def comp_paths(arg):
    """-> (name, 9x16 mp4, 16x9 mp4, staged .txt, docs .md)."""
    p = Path(arg)
    name = re.sub(r"\.(mp4|txt|md)$", "", p.name)
    name = re.sub(r"_(9x16|16x9)$", "", name)
    name = re.sub("^" + re.escape(F.prefix), "", name)
    if p.suffix in (".mp4", ".txt") and p.parent != Path("."):
        d = p.parent if p.is_absolute() else (Path.cwd() / p.parent).resolve()
    else:
        d = F.rtu
    stem = f"{F.prefix}{name}"
    doc = mut()._doc_path(name)              # a `_noloop` variant shares its parent's .md
    doc = Path(doc) if doc else F.docs / f"{stem}.md"
    return name, d / f"{stem}_9x16.mp4", d / f"{stem}_16x9.mp4", d / f"{stem}.txt", doc


def doc_section(md_text, prefix):
    """Lines under the first `## <prefix>...` heading, up to the next `## `."""
    lines, cur = [], False
    for line in md_text.splitlines():
        if line.startswith("## "):
            if cur:
                break
            cur = line[3:].strip().lower().startswith(prefix)
        elif cur:
            lines.append(line)
    return lines


def meta_comp(arg):
    mu = mut()
    name, v916, v169, txt, doc = comp_paths(arg)
    item = {"name": name, "short": str(v916), "long_16x9": str(v169), "doc": rel(doc),
            "short_exists": v916.exists(), "duration_s": duration(v916) if v916.exists() else None,
            "problems": []}
    if not v916.exists():
        item["problems"].append("9:16 file missing")
    md = ""
    if not doc.exists():
        item["problems"].append("no docs/uploads .md (rule 15b) -- write title/description by hand")
    else:
        md = doc.read_text(encoding="utf-8")
    lines = mu.title_lines(md)
    rec = next((i for i, l in enumerate(lines) if "recommend" in l.lower()), 0)
    item["title_options"] = []
    for i, line in enumerate(lines):
        t = re.sub(r"\s#\w+", "", mu.clean_title(line)).strip()   # no stray hashtags
        full = t + F.suffix
        item["title_options"].append({"title": full, "len": len(full), "ok": len(full) <= 100,
                                      "recommended": i == rec, "emoji": mu.emoji_problem(t) or "ok"})
    if not lines:
        item["problems"].append("no title options parsed")
    else:
        pick = item["title_options"][rec]
        item["title"] = pick["title"]
        if pick["emoji"] != "ok":
            item["problems"].append("recommended " + pick["emoji"])
        if not pick["ok"]:
            item["problems"].append(f"recommended title is {pick['len']} chars (> 100)")
    item["description"] = ""    # Shorts: description box stays EMPTY
    item["tags"] = ""           # Shorts: tags field stays EMPTY
    item["comment"] = F.comment
    item["sequencing"] = [l.strip() for l in doc_section(md, "sequencing") if l.strip()]
    return item


# ---------------------------------------------------------------- layout: folder (standard)
STAGES = ("ready-to-upload", "needs-review")


def folder_of(arg):
    """A video's folder from its file, its .md, the folder itself, or its name (searched in
    final/ready-to-upload/ and final/needs-review/)."""
    p = Path(arg)
    if not p.is_absolute():
        p = Path.cwd() / p
    if p.exists():
        return (p if p.is_dir() else p.parent).resolve()
    name = re.sub(r"(_9x16|_16x9)?\.(mp4|md)$", "", p.name)
    hits = [d for s in STAGES for d in (ROOT / "final" / s).rglob("*") if d.is_dir()
            and (d.name == name or re.fullmatch(r"\d{2,}_" + re.escape(name), d.name))]
    if len(hits) != 1:
        sys.exit(f"{arg}: {'no' if not hits else len(hits)} video folder(s) of that name in "
                 f"final/{{{','.join(STAGES)}}}/ {[rel(h) for h in hits]}: pass the folder path")
    return hits[0].resolve()


def folder_paths(arg):
    """-> (folder, stem, kind, the file to upload, the .md)."""
    d = folder_of(arg)
    stem = d.name
    parts = d.relative_to(ROOT).parts if d.is_relative_to(ROOT) else d.parts
    kind = "shorts" if "shorts" in parts else "longform" if "longform" in parts else None
    want = ("_9x16", "") if kind == "shorts" else ("_16x9", "")
    up = next((d / f"{stem}{w}.mp4" for w in want if (d / f"{stem}{w}.mp4").exists()), None)
    if up is None:
        mp4s = sorted(d.glob("*.mp4"))
        up = mp4s[0] if len(mp4s) == 1 else d / f"{stem}{want[0] if kind == 'shorts' else ''}.mp4"
    return d, stem, kind, up, d / f"{stem}.md"


LABEL = re.compile(r"^(?:#{2,}\s*)?(?:\*\*)?\s*(title options|titles?|description|"
                   r"pinned(?: first)? comment|tags|thumbnail)\b(.*)$", re.I)


def upload_fields(md_text):
    """The `## Upload` part of a standard-layout .md -> {field: {"inline": [text after the
    label], "lines": [the lines below it], "note": "(...)" after the label}}; a label inside a
    fenced block is text, not a label."""
    m = re.search(r"^## Upload\b[^\n]*\n(.*?)(?=^## |\Z)", md_text, re.S | re.M)
    if not m:
        return None
    fields, cur, fence = {}, None, False
    for line in m.group(1).splitlines():
        lab = None if fence else LABEL.match(line.strip())
        if line.strip().startswith("```"):
            fence = not fence
        if lab:
            k = lab.group(1).lower()
            k = "title" if k.startswith("title") else "pinned comment" if k.startswith("pinned") else k
            rest = re.sub(r"^[\s*]+", "", lab.group(2))
            note = ""
            pm = re.match(r"^(\([^)]*\))(.*)$", rest)          # "(pick one ...):"
            if pm:
                note, rest = pm.group(1), pm.group(2)
            rest = re.sub(r"^[\s:*]+", "", rest).strip()
            if k in fields:                   # "**Title (alt, ...):** X": one more option
                fields[k]["inline"] += [rest] if rest else []
            else:
                fields[k] = {"inline": [rest] if rest else [], "lines": [], "note": note.strip("()")}
            cur = k
        elif cur:
            fields[cur]["lines"].append(line)
    return fields


def clean_option(s):
    """One title option -> the bare title (emoji kept, markup and editorial dropped)."""
    s = re.sub(r"^\s*(?:\d+[.)]|[-*])\s+", "", s).strip()
    tick = re.findall(r"`([^`]+)`", s)
    if tick:
        return tick[0].strip()
    bold = re.search(r"\*\*(.+?)\*\*", s)
    if bold:
        return bold.group(1).strip()
    s = re.sub(r"\s{2,}\([^)]*\)\s*$", "", s)                              # "  (recommended: ...)"
    s = re.sub(r"\s*\((?:recommended|rec\.)[^)]*\)\s*$", "", s, flags=re.I)
    s = re.sub(r"\s+[—–-]+\s+\*[^*]*recommended[^*]*\*\s*$", "", s, flags=re.I)
    return s.strip("*\"“” ").strip()


def field_text(fl):
    """A field -> its text: a fenced block's content as written, else the inline text and the
    lines up to the first blank one, unwrapped. "none ..." -> ""."""
    if not fl:
        return ""
    body = "\n".join(fl["lines"])
    m = re.search(r"^\s*```[^\n]*\n(.*?)^\s*```", body, re.S | re.M)
    if m:
        txt = m.group(1).strip("\n")
    else:
        more = []
        for l in fl["lines"]:
            if not l.strip() or re.match(r"\s*only if\b", l, re.I):
                if more or l.strip():
                    break
                continue
            more.append(l.strip())
        txt = " ".join(fl["inline"][:1] + more).strip()
    return "" if re.match(r"none\b", txt, re.I) else txt


EMOJI_RANGES = ((0x1F000, 0x1FAFF), (0x2600, 0x27BF), (0x2B00, 0x2BFF), (0x2300, 0x23FF))


def emoji_count(title):
    """Emoji clusters in a title (a ZWJ sequence, a flag pair, a skin tone count once)."""
    chars, n, prev_zwj, i = list(title or ""), 0, False, 0
    while i < len(chars):
        cp = ord(chars[i])
        if cp == 0x200D:
            prev_zwj, i = True, i + 1
            continue
        if cp in (0xFE0F, 0xFE0E) or 0x1F3FB <= cp <= 0x1F3FF:
            i += 1
            continue
        if any(lo <= cp <= hi for lo, hi in EMOJI_RANGES):
            if not prev_zwj:
                n += 1
            if 0x1F1E6 <= cp <= 0x1F1FF and i + 1 < len(chars) and 0x1F1E6 <= ord(chars[i + 1]) <= 0x1F1FF:
                i += 1
        prev_zwj, i = False, i + 1
    return n


def folder_comment(md_text):
    f = upload_fields(md_text) or {}
    return field_text(f.get("pinned comment")) or None


def meta_folder(arg):
    d, stem, kind, up, md = folder_paths(arg)
    item = {"stem": stem, "kind": kind, "folder": rel(d), "file": str(up), "md": rel(md),
            "file_exists": up.exists(), "duration_s": duration(up) if up.exists() else None,
            "problems": []}
    if not up.exists():
        item["problems"].append("video file missing")
    fields = None
    if not md.exists():
        item["problems"].append(".md missing -- every video has one (shared rules)")
    else:
        fields = upload_fields(md.read_text(encoding="utf-8-sig"))
        if fields is None:
            item["problems"].append("the .md has no `## Upload` part")
    fields = fields or {}
    tf = fields.get("title", {"inline": [], "lines": [], "note": ""})
    raw = [l for l in tf["lines"] if re.match(r"^\s*\d+[.)]\s+", l)] + tf["inline"]
    rec = next((i for i, l in enumerate(raw) if "recommend" in l.lower()), 0)
    item["title_options"] = []
    for i, line in enumerate(raw):
        t = clean_option(line)
        if F.suffix:                       # the suffix is then the title's only hashtag
            t = re.sub(r"\s#\w+", "", t).strip()
        full = t + F.suffix
        o = {"title": full, "len": len(full), "ok": len(full) <= 100, "recommended": i == rec}
        if kind == "shorts":
            n = emoji_count(t)
            o["emoji"] = "ok" if 1 <= n <= 3 else f"title has {n} emoji (a Short takes 1-3)"
        item["title_options"].append(o)
    if tf.get("note"):
        item["title_note"] = tf["note"]
    if not raw:
        item["problems"].append("no title options parsed")
    else:
        pick = item["title_options"][rec]
        item["title"] = pick["title"]
        if pick.get("emoji", "ok") != "ok":
            item["problems"].append("recommended " + pick["emoji"])
        if not pick["ok"]:
            item["problems"].append(f"recommended title is {pick['len']} chars (> 100)")
    empty = kind == "shorts" and F.short_empty
    desc = "" if empty else field_text(fields.get("description"))
    if kind == "shorts":                   # the photosensitivity warning is long-form only
        desc = "\n".join(l for l in desc.splitlines()
                         if not re.match(r"\s*photosensitivity warning", l, re.I)).strip("\n")
    item["description"] = desc
    tags = fields.get("tags")
    item["tags"] = "" if empty else field_text(tags)
    extra = [l for l in (tags or {}).get("lines", []) if re.match(r"\s*only if .*made for kids", l, re.I)]
    if extra and not empty:
        item["tags_if_made_for_kids"] = re.sub(r"^.*?:\s*", "", extra[0].strip())
    item["comment"] = F.comment or field_text(fields.get("pinned comment"))
    if not item["comment"]:
        item["problems"].append("no pinned comment in the .md")
    item["made_for_kids"] = F.made_for_kids
    if kind == "longform":
        th = d / "thumbnail"
        item["thumbnails"] = sorted(p.name for p in th.iterdir()) if th.is_dir() else []
        if "thumbnail" in fields:
            item["thumbnail_note"] = field_text(fields["thumbnail"])
    return item


# ---------------------------------------------------------------- meta / order
def cmd_meta(a):
    F.need("layout")
    fn = {"pair": meta_pair, "compilation": meta_comp, "folder": meta_folder}[F.layout]
    out([fn(c) for c in a.clips])


def video_name(arg):
    if F.layout == "pair":
        return pair_paths(arg)[0]
    if F.layout == "compilation":
        return comp_paths(arg)[0]
    return folder_paths(arg)[1]


def order_family(a):
    """2D: same family = the stem's first word (sort_*, double_*; after an NN_ number)."""
    stems = [video_name(c) for c in a.clips]
    fam = lambda s: re.sub(r"^\d+_", "", s).split("_")[0]
    best = None
    for _ in range(400):
        random.shuffle(stems)
        clashes = sum(fam(x) == fam(y) for x, y in zip(stems, stems[1:]))
        if best is None or clashes < best[0]:
            best = (clashes, list(stems))
        if clashes == 0:
            break
    out({"order": best[1], "adjacent_same_family": best[0]})


def machine(name):
    return name.split("_")[0]


def subject(name):
    return re.sub(r"^(gold|micro|mini|tiny)", "", name.split("_")[-1])


def names_other(name, other):
    """Does `name`'s doc `## Sequencing` section name `other`?"""
    doc = comp_paths(name)[4]
    if not doc.exists():
        return False
    text = "\n".join(doc_section(doc.read_text(encoding="utf-8"), "sequencing"))
    pat = r"(?:(?<=" + re.escape(F.prefix) + r")|(?<![A-Za-z0-9_]))" + re.escape(other) + r"(?![A-Za-z0-9_])"
    return re.search(pat, text) is not None


def clash(x, y):
    why = []
    if names_other(x, y) or names_other(y, x):
        why.append("sequencing")
    if machine(x) == machine(y):
        why.append("machine")
    if subject(x) == subject(y):
        why.append("subject")
    return why


def order_machine_subject(a):
    """3D: never back to back the same machine (first word), the same subject (last word,
    gold/micro/mini/tiny folded: coin = goldcoin), or a pair whose `## Sequencing` names the
    other; --after = the channel's latest upload(s), oldest first."""
    stems = [video_name(c) for c in a.clips]
    after = [video_name(c) for c in (a.after or [])]
    cache = {}

    def why(x, y):
        if (x, y) not in cache:
            cache[(x, y)] = clash(x, y)
        return cache[(x, y)]

    def score(seq):
        full = after[-1:] + seq
        return sum(10 * ("sequencing" in why(x, y)) + len(why(x, y)) for x, y in zip(full, full[1:]))

    best = None
    for _ in range(2000):
        random.shuffle(stems)
        s = score(stems)
        if best is None or s < best[0]:
            best = (s, list(stems))
        if s == 0:
            break
    full = after[-1:] + best[1]
    out({"order": best[1], "after": after[-1:] or None,
         "adjacent_clashes": [{"pair": [x, y], "why": why(x, y)}
                              for x, y in zip(full, full[1:]) if why(x, y)]})


def cmd_order(a):
    F.need("layout")
    if F.order == "family":
        return order_family(a)
    if F.order == "machine-subject":
        return order_machine_subject(a)
    stems = [video_name(c) for c in a.clips]
    random.shuffle(stems)
    out({"order": stems, "note": "the entry names no Order rule: a plain random order, "
                                 "check look-alikes by eye"})


# ---------------------------------------------------------------- slots
def day_start():
    return dt.timedelta(hours=F.sched["slots"][0][0], minutes=F.sched["slots"][0][1])


def day_key(t):
    return (t - day_start()).date()          # a day runs from its first slot to just before it


def day_slots(d):
    base = dt.datetime.combine(d, dt.time(), F.tz)
    first = F.sched["slots"][0]
    return [base + dt.timedelta(days=1 if (h, m) < first else 0, hours=h, minutes=m)
            for h, m in F.sched["slots"]]


def cmd_slots(a):
    F.need("schedule")
    per_day, gap = F.sched["per_day"], F.sched["gap"]
    now = parse_time(a.now) if a.now else now_local()
    taken = sorted(parse_time(s) for s in (a.existing or []))
    got = []
    if a.immediate:
        t = now + LEAD
        t += dt.timedelta(minutes=(-t.minute) % 5)
        got.append(t)
        taken.append(t)
    d = day_key(now)
    while len(got) < a.count:
        slots = day_slots(d)
        ends = slots[1:] + [slots[0] + dt.timedelta(days=1)]
        for i, (s, end) in enumerate(zip(slots, ends)):
            if len(got) >= a.count:
                break
            in_day = [x for x in taken if day_key(x) == d]
            if len(in_day) >= per_day:
                break
            # slot i is already served when the day holds i+1 Shorts before the next slot
            # (a Short that went out at 14:00 covers the 14:00 slot; it is not "missed")
            if sum(x < end for x in in_day) >= i + 1:
                continue
            t = s
            if t < now + LEAD:                          # missed: catch up, don't skip
                t = now + LEAD
                t += dt.timedelta(minutes=(-t.minute) % 15, seconds=-t.second,
                                  microseconds=-t.microsecond)
            while any(abs(t - x) < gap for x in taken):
                t += STEP
            if day_key(t) != d:
                break
            got.append(t)
            taken.append(t)
        d += dt.timedelta(days=1)
    out({"now": fmt(now), "timezone": F.tz_label,
         "slots": [{"when": fmt(t), "studio_date": t.strftime("%b %d, %Y").replace(" 0", " "),
                    "studio_time": t.strftime("%I:%M %p").lstrip("0")} for t in got]})


# ---------------------------------------------------------------- ledger
def ledger_head():
    zone = f" ({F.city})" if F.city else ""
    return f"""# YouTube upload ledger — {F.name}

One row per upload, written by the shared youtube-upload skill (`yt_upload_prep.py record`).
Times are {F.tz_label}. `comment` = pending until the pinned comment is posted + pinned
after the video goes live (`yt_upload_prep.py due` lists them). Never delete this file.

| # | publish{zone} | clip | title | url | related video | comment |
|---|-------------------|------|-------|-----|---------------|---------|
"""


def ledger_rows():
    if not F.ledger.exists():
        return []
    rows = []
    for line in F.ledger.read_text(encoding="utf-8").splitlines():
        if re.match(r"^\|\s*\d+\s*\|", line):
            rows.append([c.strip() for c in line.strip().strip("|").split("|")])
    return rows


def safe(s):
    return s.replace("|", "/").replace("\n", " ")


def pending_cell(comment):
    return "pending" if not comment else "pending: " + comment.replace("|", "/").replace("\n", "<br>")


def append_row(clip_rel, a, comment):
    if not F.ledger.exists():
        F.ledger.parent.mkdir(parents=True, exist_ok=True)
        F.ledger.write_text(ledger_head(), encoding="utf-8")
    n = len(ledger_rows()) + 1
    row = (f"| {n} | {a.when} | `{clip_rel}` | {safe(a.title)} | {a.url} | "
           f"{safe(a.related or '-')} | {pending_cell(comment)} |\n")
    with F.ledger.open("a", encoding="utf-8") as f:
        f.write(row)
    return n


def own_comment(text):
    if not text:
        sys.exit("--own-comment: the video's .md has no pinned comment to record")
    return text


def is_stage(p):
    return p == ROOT / "final" or p.parent == ROOT / "final"


def record_pair(a):
    stem, v916, v169, md = pair_paths(a.clip)
    if not a.no_move and not v916.exists():
        sys.exit(f"{v916} is not there: nothing recorded")
    comment = None
    if a.own_comment:
        secs = md_sections(md.read_text(encoding="utf-8")) if md.exists() else {}
        comment = own_comment(section_text(find_sec(secs, "pinned")))
    non_pd = None
    if F.guard:                                   # non-PD music never joins the megamix pool
        g = channel_module(F.guard[0])
        non_pd = g.reason(g.stem_of(stem), g.bank_keys())
    dest_dir = F.guard[1] if non_pd else F.routes.get(v916.parent.name, F.uploaded)
    dest = v916 if a.no_move else dest_dir / v916.name        # where the clip will live
    n = append_row(rel(dest), a, comment)
    moved = []
    if not a.no_move:
        dest_dir.mkdir(parents=True, exist_ok=True)
        for p in (v916, v169):
            if p.exists() and p.parent.resolve() != dest_dir.resolve():
                shutil.move(str(p), str(dest_dir / p.name))
                moved.append(p.name)
        if md.exists() and md.parent.resolve() != dest_dir.resolve():
            md.unlink()
            moved.append(f"deleted {md.name}")
        try:          # drop an emptied family subfolder; never a stage folder or a kept queue
            par = v916.parent
            if not is_stage(par) and par.name not in F.keep and not any(par.iterdir()):
                par.rmdir()
        except OSError:
            pass
    out({"ledger_row": n, "moved": moved, "shorts_only": non_pd})


def record_comp(a):
    name, v916, v169, txt, doc = comp_paths(a.clip)
    if not a.no_move and not v916.exists():
        sys.exit(f"{v916} is not there: nothing recorded")
    comment = None
    if a.own_comment:
        md = doc.read_text(encoding="utf-8") if doc.exists() else ""
        comment = own_comment(section_text(doc_section(md, "pinned")))
    dest = v916 if a.no_move else F.uploaded / v916.name        # where the clip will live
    n = append_row(rel(dest), a, comment)
    moved = []
    if not a.no_move:
        F.uploaded.mkdir(parents=True, exist_ok=True)
        for p in (v916, v169):
            if p.exists() and p.parent.resolve() != F.uploaded.resolve():
                shutil.move(str(p), str(F.uploaded / p.name))
                moved.append(p.name)
        if txt.exists():                       # the disposable extract goes with the mp4s
            txt.unlink()
            moved.append(f"deleted {txt.name}")
    out({"ledger_row": n, "moved": moved, "doc_kept": rel(doc)})


def record_folder(a):
    d, stem, kind, up, md = folder_paths(a.clip)
    stage = next((p for p in d.parents if p.parent == ROOT / "final"), None)
    if stage is None:
        sys.exit(f"{rel(d)} is not under final/<stage>/")
    dest_d = F.uploaded / d.relative_to(stage)
    moving = not a.no_move and stage.resolve() != F.uploaded.resolve()
    if moving and dest_d.exists():
        sys.exit(f"{rel(dest_d)} already exists: nothing recorded")
    if moving and not up.exists():
        sys.exit(f"{rel(up)} is not there: nothing recorded")
    text = folder_comment(md.read_text(encoding="utf-8-sig")) if md.exists() else None
    comment = F.comment if F.comment and not a.own_comment else text
    if a.own_comment:
        own_comment(text)
    dest = (dest_d / up.name) if moving else up
    n = append_row(rel(dest), a, None if comment == F.comment else comment)
    moved = []
    if moving:
        dest_d.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(d), str(dest_d))
        moved.append(f"{rel(d)} -> {rel(dest_d)}")
        grp = d.parent                     # the last Short of a from_NN_<long>/ group leaves it
        if grp.name.startswith("from_") and grp.exists() and not any(grp.iterdir()):
            grp.rmdir()
            moved.append(f"removed the emptied {rel(grp)}")
    out({"ledger_row": n, "moved": moved, "folder": rel(dest_d if moving else d),
         "comment": comment})


def cmd_record(a):
    F.need("layout")
    F.need("ledger")
    {"pair": record_pair, "compilation": record_comp, "folder": record_folder}[F.layout](a)


def cmd_due(a):
    F.need("ledger")
    now = parse_time(a.now) if a.now else now_local()
    due = []
    for r in ledger_rows():
        if len(r) < 7 or not (r[6] == "pending" or r[6].startswith("pending:")):
            continue
        when = parse_when(r[1])
        if when is not None and when > now:
            continue
        row = {"row": r[0], "when": r[1], "clip": r[2], "title": r[3], "url": r[4]}
        if r[6].startswith("pending:"):
            row["comment"] = r[6][len("pending:"):].strip().replace("<br>", "\n")
        due.append(row)
    out({"now": fmt(now), "comment": F.comment, "due": due})


def cmd_pinned(a):
    F.need("ledger")
    lines = F.ledger.read_text(encoding="utf-8").splitlines(keepends=True)
    hit = 0
    for i, line in enumerate(lines):
        s = line.rstrip()
        m = re.search(r"\| pending(?:: [^|]*)? \|$", s)
        if a.url in line and m:
            lines[i] = s[:m.start()] + "| pinned |\n"
            hit += 1
    F.ledger.write_text("".join(lines), encoding="utf-8")
    out({"marked": hit})


def cmd_facts(a):
    out({"root": str(ROOT), "channel": F.name, "id": F.id, "handle": F.handle, "scope": F.scope,
         "schedule": None if not F.sched else {
             "slots": [f"{h:02d}:{m:02d}" for h, m in F.sched["slots"]],
             "per_day": F.sched["per_day"], "gap_min": int(F.sched["gap"].total_seconds() // 60),
             "timezone": F.tz_label},
         "made_for_kids": F.made_for_kids,
         "collaborator": None if not F.collab else dict(zip(("name", "id", "handle"), F.collab)),
         "title_suffix": F.suffix, "short_description_and_tags_empty": F.short_empty,
         "pinned_comment": F.comment or "the video's own (its .md)",
         "settings": F.settings or None,
         "ledger": rel(F.ledger) if F.ledger else None, "layout": F.layout,
         "uploaded": rel(F.uploaded), "routes": {k: rel(v) for k, v in F.routes.items()},
         "non_pd_guard": None if not F.guard else {"tool": F.guard[0], "to": rel(F.guard[1])},
         "kept_folders": sorted(F.keep), "order": F.order})


def main():
    global ROOT, F
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", help="the channel folder (default: the one this script is synced into, else cwd)")
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("facts")
    m = sub.add_parser("meta"); m.add_argument("clips", nargs="+")
    o = sub.add_parser("order"); o.add_argument("clips", nargs="+"); o.add_argument("--after", action="append")
    s = sub.add_parser("slots"); s.add_argument("--count", type=int, required=True)
    s.add_argument("--existing", action="append"); s.add_argument("--immediate", action="store_true")
    s.add_argument("--now")
    r = sub.add_parser("record"); r.add_argument("--clip", required=True); r.add_argument("--when", required=True)
    r.add_argument("--title", required=True); r.add_argument("--url", required=True)
    r.add_argument("--related"); r.add_argument("--no-move", action="store_true")
    r.add_argument("--own-comment", action="store_true",
                   help="pin the .md's own comment, not the fixed one (a comment-reply video)")
    d = sub.add_parser("due"); d.add_argument("--now")
    p = sub.add_parser("pinned"); p.add_argument("--url", required=True)
    a = ap.parse_args()
    if a.root:
        ROOT = Path(a.root).resolve()
    elif HERE.parent.name == "skills" and HERE.parent.parent.name == ".claude":
        ROOT = HERE.parents[2]
    else:
        ROOT = Path.cwd().resolve()
    F = Facts(ROOT)
    {"facts": cmd_facts, "meta": cmd_meta, "order": cmd_order, "slots": cmd_slots,
     "record": cmd_record, "due": cmd_due, "pinned": cmd_pinned}[a.cmd](a)


if __name__ == "__main__":
    main()
