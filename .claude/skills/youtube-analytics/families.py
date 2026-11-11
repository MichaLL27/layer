"""Family vocabularies for yt_analytics.py, one profile per channel that has one.

A channel picks its profile with `families:` in the Analytics facts of its CLAUDE.md
(`## Channel facts`). `none` (or a profile not listed here) = every Short is `other` and
every long-form video `long-form`. A wrong guess for ONE video is a row in the channel's
reports/video_families.csv (id,family), never a change here.

A profile:
  name_family(name) -> family     from the video's name (ledger clip stem or matched .md)
  title_family                    [(family, regex)] tried in order on the lowercased title
  pre_pivot                       regex on the title: matching videos leave every table
  longform                        the family of a video >= 3 min
  subject(row)                    optional extra split of the cohort (3D: marble vs subject)
"""
import re

# ---------------------------------------------------------------- sims2d (Satisfying 2D Sims)
# Families follow satisfying-2d-sims research/channel-performance-2026-09-09.md section 6.
# Tokens are drawn from scenes/*.py names and ledger stems; first match wins, in this order.
_2D_NAME = [
    ("pacman", ["pac"]),
    ("press_crush", ["press", "crush", "jaw", "baler", "millstone", "cinch", "choke",
                     "squish", "smash", "pushback", "shrink", "hydro", "tower", "quake",
                     "swing_cage", "v_arms", "crumple", "grind", "anvil", "piston"]),
    ("recursion", ["nest", "droste", "fractal", "tree_ring", "recursion"]),
    ("merge_sink", ["merge", "buoy", "sink"]),
    ("eat_hunter", ["devour", "gorge", "snake", "hunt", "eat"]),
    ("fire_water", ["steam", "fire", "water"]),
    ("melody", ["song", "melody", "note_rain", "carillon", "harp", "chord", "tune"]),
    ("big_number", ["double", "doubling", "multiply", "billion", "trillion", "million",
                    "power", "digit", "counter"]),
    ("overflow_hold", ["spawn_crash", "pop_bowl", "glass_float", "glass_swell",
                       "balloon", "overflow", "boing"]),
    ("beat_rhythm", ["beat_", "neon_tunnel", "solved_bounce", "runner"]),
    ("mitosis_cell", ["mitosis", "cell"]),
    ("escape_door", ["escape", "hatch", "trapdoor", "door", "gate"]),
    ("sort", ["sort", "sift"]),
    ("turf", ["turf", "rps", "paint_giant", "wet_paint"]),
    ("shatter_explode", ["shatter", "spiral", "break_", "cannon", "laser", "blast",
                         "red_zone", "spike"]),
    ("fill_grow", ["grow_", "fill_", "morph", "grid_grow", "packing", "wash", "paint"]),
    ("static_pattern", ["mandala", "harmonograph", "one_line", "chladni", "kaleido",
                        "snowflake", "illusion"]),
    ("prediction", ["plinko", "roll_race", "predict", "compare"]),
]
# for the manual uploads before the ledger (2026-09-15); built from the uploaded titles
_2D_TITLE = [
    ("pacman", r"pac[- ]?man|ghost"),
    ("press_crush", r"\bpress\b|crush|jaw\b|grind|millstone|squeez|piston|hydraulic|"
                    r"baler|ram slams|walls? clos|closing walls|shrink|tighter|bale\b|"
                    r"every block"),
    ("recursion", r"how deep|inside a|rings? in(side)? (a|the) ring|nested|deeper|"
                  r"ring inside|another one inside|all the way down"),
    ("merge_sink", r"merge|absorb|swallow|sink|combine"),
    ("eat_hunter", r"\beat|\bate\b|hunter|hunts?\b|devour|feeds? on|it got a taste"),
    ("fire_water", r"fire (vs|and|v)? ?water|water vs|lava|steam"),
    ("melody", r"plays? (what|a |the )|name th(e|is) song|guess the song|song\b|"
               r"melody|sound on|sand understand music"),
    ("big_number", r"doubl(es?|ing)|x ?\d|billion|trillion|million|quintillion|"
                   r"quadrillion|1,000,000|reach(es)? \d|count(er)? (hit|to)|10,?000|"
                   r"\dx past|past the goal"),
    ("overflow_hold", r"overflow|spawns? \d|too many|hold\b|blender|"
                      r"multiply|panicked|handle\b|chance to split|balloon"),
    ("beat_rhythm", r"\bbeat\b|rhythm|tempo|to the music"),
    ("mitosis_cell", r"mitosis|cell|divid(es|ing)|splits? in(to)? (two|2)"),
    ("escape_door", r"escap|way out|door|trapdoor|the gap|get out|let it out|freed?\b|"
                    r"fall(s|ing)? through"),
    ("sort", r"sort"),
    ("turf", r"takes over|turf|colou?r war|territory|team|last colou?r|"
             r"colou?rs? fight|paint(s|ed)? the (board|arena)|which colou?r|"
             r"erased|two colou?rs|colou?r fills|fills? the \w+ last"),
    ("shatter_explode", r"shatter|glass|explo(d|si)|blast|pieces|blade|slice|laser|"
                        r"spike|acid|melt|reach(es)? the cent|hits? the cent|"
                        r"rings? breaks?|breaks? exactly"),
    ("fill_grow", r"fill(s|ed)?( the| it)?\b|gets bigger|arena (grows|gets)|grows? the|"
                  r"every bounce grows|grows? (with )?every|keeps? growing|"
                  r"grows? until|circle turns|becom|morph|paints? the|unpaints?|"
                  r"gains? a (side|mirror)|bigger every|spinning \w+agon"),
    ("static_pattern", r"mandala|kaleidoscope|illusion|one line|snowflake|pattern"),
    ("prediction", r"predict|which (column|moose)|who wins|plinko|pick your"),
]


def _2d_name(name):
    n = name.lower()
    for fam, toks in _2D_NAME:
        if any(t in n for t in toks):
            return fam
    return "other"


# ---------------------------------------------------------------- sims3d (Satisfying 3D Sims)
# MACHINE token -> family. A name is compilation_<machine>_<subject> (utube_candy,
# hourglass_dice) or, for the older marble videos, marble_<machine>. The FIRST token found
# here is the machine; subject words (dice, coin, candy, gem, marble, ...) are deliberately
# not keys, so a swapped-in subject never decides the family ("hourglass_dice" once came out
# as crush because "dice" matched first, 2026-09-15).
_3D_MACHINE = {
    **dict.fromkeys(["melody", "canon", "xylophone", "tempo", "notes"], "melody"),
    **dict.fromkeys(["plinko", "dicetower", "galton", "zigzag", "pegwall"], "plinko"),
    **dict.fromkeys(["utube", "manifold", "manifold3", "kerplunk", "spiral", "vortex", "screw"],
                    "marble_run"),
    **dict.fromkeys(["fishbowl", "jar", "aquarium", "beaker", "bottle", "decanter", "pitcher",
                     "wineglass", "hourglass", "martini", "vase", "vase02", "chest", "pot",
                     "barrel", "flask", "bulb", "cubebox", "vessel"], "vessel_fill"),
    **dict.fromkeys(["magnet"], "magnet"),
    **dict.fromkeys(["shatter", "melon", "bust", "cat", "glass", "lego"], "shatter"),
    **dict.fromkeys(["cloth"], "cloth"),
    **dict.fromkeys(["splash", "champagne", "water"], "splash"),
    **dict.fromkeys(["grass"], "grass"),
    **dict.fromkeys(["cube", "pillar", "wall", "pyramid", "punch", "rain", "smash", "anvil",
                     "shake", "domino", "spring", "gnome", "shelf", "polygon", "billiards",
                     "card", "drop", "jelly", "toilet", "donut", "ball"], "crush"),
}
# substring fallback for names with no machine token
_3D_NAME = [
    ("melody", ["melody", "canon", "xylophone", "tempo", "notes"]),
    ("plinko", ["plinko", "zigzag", "funnel", "pegwall", "galton", "dicetower"]),
    ("marble_run", ["utube", "manifold", "kerplunk", "spiral", "vortex", "screw"]),
    ("vessel_fill", ["flask", "bulb", "cubebox"]),
    ("magnet", ["magnet"]),
    ("shatter", ["shatter", "melon", "bust", "cat_"]),
    ("cloth", ["cloth"]),
    ("splash", ["splash", "water_balls", "champagne"]),
    ("crush", ["cube", "pillar", "wall", "pyramid", "punch", "rain", "box", "smash", "anvil",
               "tower", "shake", "domino", "dice"]),
    ("vessel_fill", ["fishbowl", "jar", "aquarium", "beaker", "bottle", "decanter", "pitcher",
                     "wineglass", "hourglass", "martini", "vase", "chest", "pot", "barrel",
                     "vessel", "object_"]),
]
_3D_TITLE = [
    ("melody", r"playing .*song|plays a note|melody|name the song|in harmony"),
    ("vessel_fill", r"filling a|fits? in|fit inside|in a fishbowl|champagne glasses|fish tank|ball pit|how many .* fill"),
    ("plinko", r"plinko|funnel|peg wall|zigzag"),
    ("marble_run", r"vortex|u-tube|screw lift|spiral|kerplunk|glass tube|tube in|loop track|marbles|one hole per shelf|two exits"),
    ("shatter", r"shatter|vase|claypot|watermelon|pieces|bust"),
    ("cloth", r"cloth|covering a can|melts right through|tablecloth"),
    ("magnet", r"magnet"),
    ("grass", r"grass"),
    ("crush", r"crush|pyramid|tower|anvil|smash|wall|cube|block|spike|billiard|house of cards|ice block|heavy|springs|jelly|rolls the furthest|floor out|punch"),
]


def _3d_parts(name):
    """name -> (family, machine, subject); subject "" = the machine's own shipped subject,
    "marble" for the marble_<machine> names, else the tokens after the machine."""
    toks = name.lower().replace("compilation_", "").split("_")
    for i, t in enumerate(toks):
        if t in _3D_MACHINE:
            after = [x for x in toks[i + 1:] if x not in _3D_MACHINE]
            subj = "_".join(after) or ("marble" if "marble" in toks[:i] else "")
            return _3D_MACHINE[t], t, subj
    return "", "", ""


def _3d_name(name):
    fam = _3d_parts(name)[0]
    if fam:
        return fam
    n = name.lower()
    for fam, toks in _3D_NAME:
        if any(t in n for t in toks):
            return fam
    return "other"


def _3d_subject(row):
    """Is the thing moving through the machine a marble or a swapped-in subject? The
    2026-09 batches were built on "proven machine, NEW SUBJECT"; this is its scorecard."""
    if not row["name"]:
        return "unknown (no doc)"
    if row.fam == "melody":
        return "melody"
    subj = _3d_parts(row["name"])[2]
    if subj in ("", "max", "res", "height", "count", "speed", "pieces", "noloop"):
        return "shipped subject"
    if subj == "marble":
        return "marble"
    if subj == "micromarble":
        return "micro marble"
    return "new subject"


# ---------------------------------------------------------------- the profiles
PROFILES = {
    "sims2d": dict(name_family=_2d_name, title_family=_2D_TITLE, pre_pivot=None,
                   longform="megamix", subject=None),
    "sims3d": dict(name_family=_3d_name, title_family=_3D_TITLE,
                   pre_pivot=r"john wick|the boys|jumpstyle|tiki tiki|los voltaje",
                   longform="megamix", subject=_3d_subject),
}
NONE = dict(name_family=lambda name: "other", title_family=[], pre_pivot=None,
            longform="long-form", subject=None)


def profile(key):
    return PROFILES.get((key or "").strip("` ").lower(), NONE)


def family_from_title(p, title, duration, longform_sec):
    t = title.lower()
    if p["pre_pivot"] and re.search(p["pre_pivot"], t):
        return "pre_pivot"
    if duration >= longform_sec:
        return p["longform"]
    for fam, pat in p["title_family"]:
        if re.search(pat, t):
            return fam
    return "other"
