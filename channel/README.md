# channel/ — identity of "Layer by Layer 3D"

Live design: **02 sunny cutaway** (chosen 2026-10-31, after Lazar asked for a brighter look in the
spirit of the reference channel; design 01 stays as the older version).

| Item | Live file | Since | Notes |
|---|---|---|---|
| Avatar | `avatar/current.png` (= `avatar/02_sunny_cutaway.png`, 800x800) | not uploaded yet | layered isometric block with our house on top, sunny yellow |
| Banner | `banner/current.png` (= `banner/02_sunny_cutaway.png`, 2560x1440) | not uploaded yet | title left, cutaway slab (house, bunker, pool, tree) right; all inside the 1546x423 safe area |
| Watermark | `watermark.png` (150x150, transparent) | not uploaded yet | block + house, no background |

Older: `avatar/01_layer_block.png`, `banner/01_layer_block.png` (navy, plain block; its sources
`src/*_01.svg`).

Sources are `channel/src/*.svg` (current design). Rebuild: `python tools/make_brand.py`
(`--line1/--line2/--tagline` change the banner text). A new design = a new numbered file
(`03_...png`) + a row here; older ones stay.

**Why it looks like this (REMAKE RULE):** the idea is the reference channel's (bright background,
bold title, cutaway ground slab), the execution is ours: yellow instead of blue, text left and
slab right instead of stacked, a teal-roof house, concrete bunker, pool, isometric layered
mark. Do not drift toward the reference's blue / brick-roof house.

Palette: sky `#FFE873` -> `#FFB347`, navy text `#0D1B2A`, coral `#E8553D`, teal roof `#1B7F74` /
`#2AA396`; layers grass / soil / clay / water / stone. Font: Segoe UI Black.
