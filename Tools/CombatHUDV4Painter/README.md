# Combat HUD V4 painter

Deterministic, procedural "hand-painted" sprite painter for the Dungeon Run Combat HUD V4
(seed `4242`, per-sprite generators from a stable SHA-256 name hash). Everything is
authored at 2x the 1920x1080 reference size.

## Run (from the repo root)

```bash
python Tools/CombatHUDV4Painter/paint_hud_v4.py --all                  # every sprite + layout JSON
python Tools/CombatHUDV4Painter/paint_hud_v4.py --only cards,controls  # groups: cards controls crest enemy static glyphs icons layout
python Tools/CombatHUDV4Painter/paint_hud_v4.py --only cards --name Attack
python Tools/CombatHUDV4Painter/paint_hud_v4.py --list
python Tools/CombatHUDV4Painter/mock_composite.py --round r1           # mocks + contact sheet
python -m unittest discover -s Tools/CombatHUDV4Painter -p "test_*.py" -v
```

Requirements: Python 3.14, numpy, scipy, Pillow (FreeType). Inputs (read only): the world stroke
texture `Assets/Art/Rendering/AcrylicV3/T_DungeonRun_PaintStrokes.png`, the DR fonts in
`Assets/Art/UI/CombatHUDV4/Fonts/` and the V3 icons in `Assets/Art/UI/CombatHUDV3/`.

## Outputs

- `Assets/Art/UI/CombatHUDV4/{Cards,Controls,Crest,Enemy,Static,Glyphs,Icons,Icons/Small}/*.png`
- `Assets/Settings/CombatHUDV4/HudLayoutV4.json` (shared layout table, Unity RectTransform semantics)
- `Captures/CombatHUDV4/mock/<round>/` (mock composites and `sheet.png`; git-ignored)

A path guard (`paint_hud_v4.guard`) refuses any write outside those three folders and never touches
`Assets/Art/UI/CombatHUDV4/Fonts/`.

## Modules

| Module | Role |
| --- | --- |
| `palette.py` | Spec palette (ink, charcoal-teal, bronze, parchment, health, amber, category accents). |
| `shapes.py` | SDF primitives and outline builders (chamfers, pointed arches, shields, hex plates, trigons). |
| `paint.py` | Float premultiplied `Layer`, per-sprite `Ctx` (seeded rng, value noise, world brush grain), materials: cel bronze, stone inset, parchment, health band, living-width ink, wear dabs, chips, halos, text-zone flattening. |
| `kit.py` | Composite recipes: bronze-rimmed plate, gems, trigonal studs, acrylic art-window wash, cel edges. |
| `cards.py` | Card frames x5, shadow, focus rim, detail frame, detail art washes x5. |
| `hud.py` | Sockets, Commit x6, small/close buttons, player crest, enemy HUD, plaques, tokens, banners, vignette, text wash. |
| `glyphs.py` | 10 glyphs, painted 4x supersampled. |
| `icons.py` | V4 painted variants of the V3 icons, Combo / Counterattack / Charge / Miss, 96 px small copies. |
| `registry.py` | The sprite list: names, sizes, painters, text-safe zones and probe points (used by the tests). |
| `layout.py` | `HudLayoutV4.json` content. |
| `mock_composite.py` | Planning / queued / reveal mocks over the M2 world capture + contact sheet. |

## Painting rules

- Irregular silhouettes (low-frequency SDF noise), living-width warm ink contour, cel-shaded bronze
  (outer bevel, flat top, inner bevel), world brush grain, worn gold dabs, dark nicks, painted
  vignettes with warm/cool shifts.
- Text-safe zones are flattened to a gentle fitted plane (luminance std < 3/255, alpha 255) and are
  tested.
- Deterministic: no wall clock, no salted `hash()`, PNGs written without metadata.

## Iteration log

- W2 resume: palette/shapes/paint kept; `paint.value_noise` switched to a separable B-spline matrix
  (fast, same role), `Grain` channels sampled lazily, `metal()` now shades a raised bar with explicit
  cel regions (outer bevel / flat / inner bevel) and a global top-left sweep.
- Milestone 1: kit/cards/hud/glyphs/icons/registry/layout/CLI written; first complete paint (`--all`).
- Milestone 2: unittest suite (10 tests) green in 81 s; process opts out of Windows EcoQoS throttling
  (hybrid CPU demoted the painter to E-cores, ~3x slower; speed only, results unchanged). Full paint 38 s.
- Round r1 (mocks `Captures/CombatHUDV4/mock/r1`): defects: plaques/Commit/terminal read as dark faces with
  thin gold borders; parchment too bright/flat; COMBO effect text hidden by the next fanned card; "?" intent
  sigil nearly invisible; focus rim too weak. Fixes: `metal()` bevels in absolute px; Commit, floor plaque,
  small button and terminal get solid bronze pointed end caps with trigonal gems; medallion ring 9.5 wide;
  stone faces get a top-left light sweep and stronger world-dab grain; text zones keep a gentle gradient
  (std <= 2/255); parchment toned down with a painted top light; "?" larger/brighter; effect wrap width 140.
- Round r2: HUD coverage outside the hand (diff vs world, 5px box, >25) V4 0.052 vs V3 0.111. Defects: focus
  rim hidden under the frame (only the outer glow showed); terminal/banner faces still large flat areas;
  long effect texts still clipped by the neighbour card in a 4-card fan; history icons too faint.
- Round r3: focus rim now painted above the frame (layout `focusRim.order = above_frame`), terminal has an
  engraved divider with a set amber diamond, banner hem fold light, effect wrap 132, history icons 20 px / .7.
  Remaining defects: revealed intent icons (30 px) low contrast on the dark socket; banner stitches read as a
  regular dashed line; bronze a little uniform at 1080.
- Round r4: small icons get a thin warm parchment halo outside the thickened ink (legible on dark stone),
  hand-sewn irregular banner stitches, bronze grain .2 / hue .05. Remaining: the V3 sword silhouette is thin
  at 30 px, so the intent icon grows to 34 px (overlaps the ring slightly).
- Round r5 (final): layout intent icon 34 px; full unittest run green.
- Round 3 (critic `v4_r2`, mocks `Captures/CombatHUDV4/mock/r6`): implements the 11 painter/layout
  deltas from the round-3 spec. Highlights:
  1. Action sockets: face `#1E2A2E` (desaturated teal-charcoal), a 10-unit inner recess shadow (~45%,
     darker top/left), bronze rim 1.35x brighter (`BRONZE_SOCKET`) with 3 worn dabs, a painted ink
     ghost glyph (arch + triangle, ~2.5-unit stroke, alpha .30, `#C9B893`), and a parchment numeral tab.
  2. New `Cards/CardFrameSocket_<Category>.png` (450x600, one per category): same rail/ribbon as the
     hand frame, an art window enlarged down to ~88% of the card height, no effect box, a thin footer
     glyph band.
  3. Hand card icon 118x118 -> 150x150 (centre kept); MISS card's dust puff ~2x area (radii * sqrt(2)).
  4. `Static/Banner.png` repainted as an ivory parchment ribbon (440x46 display, 880x92 sprite, ink
     text), non-directional trigon studs; `Controls/SmallButton.png` repainted as a ~110x30 parchment
     chip (220x60 sprite).
  5. `Enemy/TargetMarker.png` repainted as a painted antique-gold down-pointing trigon, 24x20 display
     (48x40 sprite).
  6. HP fills: specular streak cut to ~.15 alpha, a thin ink contour added; empty track interior is
     oxblood `#4A1A18` (`palette.OXBLOOD`). Enemy track 132x16 -> 136x20, fill 124x8 -> 128x12, hp
     text 13 -> 14.
  7. `Crest/ChipBlock.png` / `ChipDodge.png` redesigned as a tag: an ivory parchment disc holding the
     ink icon, plus a short dark pointed-end tag for the number. 52x26 -> 58x28 (116x56 sprite).
  8. New `Enemy/HistoryToken.png` (44x44 sprite, 22x22 display): a small parchment disc with a bronze
     rim behind each observed-history icon.
  9. New `Cards/DetailFrame_<Category>.png` (720x960, one per category): the hand-card frame recipe
     scaled 1.6x, with a slim parchment target-line zone and a clean parchment effect box (~130
     tall). `Cards/DetailFrame.png` stays as a category-agnostic fallback.
  10. Non-card chrome (Commit x6, Floor plaque, Terminal banner, draw/discard tokens, the ACTIONS
      medallion, IntentSocket rims): a painted vertical gradient (+8%/-15%) and a subtle extra brush
      grain outside text zones (`kit.gradient_grain`), 2-3 worn `#F0D89A` dabs biased to the lit edge
      (`kit.worn_dabs_10oclock`), and every left/right directional triangle replaced by a
      non-directional (upward) trigon stud. Commit is 10% larger (236x76 -> 260x84, sprite 520x168)
      with a small pointed-arch crest carrying the sole remaining flow-pointer gem. Floor plaque and
      the ACTIONS ring are pulled ~15% dimmer (`BRONZE_STATIC_DIM`) so the sockets/Commit out-rank
      static chrome. Terminal banner 640x190 -> 560x166 (sprite 1120x332), `pos` raised to [0, 210].
  11. `Glyphs/GlyphBurst.png` (the damage-feedback glyph) repainted as a slash-spark (ivory core, warm
      red edge, ink contour) instead of a red/amber starburst.

  Extended the unittest suite for the new sprites (sizes via `EXPECTED`, determinism, and dedicated
  text-zone checks for the socket-card and category detail-frame zones) and the new layout keys.
  `mock_composite.py` now renders the in-socket card mode, the category detail frame (via the new
  `detail.zones`), the ribbon's Cancel/Split-hits chips, and history tokens, plus a
  `--changed-sheet` flag that writes `contact_r3.png`, a contact sheet of only the round-3
  new/changed sprites.

- Round 4 (in-game captures `v4_r3`, reviewed inline): three readability deltas.
  1. `Cards/CardFrameSocket_<Category>.png`: the title ribbon is taller (`cards.SOCKET_RIBBON`,
     39 -> 56 units) with a matching text zone (`cards.SOCKET_TITLE_ZONE` = [26,12,199,55]); the art
     window starts at 58 and the side gems moved down to y 78. `hand.socketCard.title.size` (34) is
     now the socketed title's auto-size ceiling, so the title reads ~15 px at 1080 instead of ~9 px.
  2. `Enemy/TargetMarker.png`: 24x20 -> 36x30 display (72x60 sprite), lifted to y 52, and shown
     untinted at runtime (it was multiplied by the teal drop-feedback tint and disappeared).
  3. Runtime only (no painter change): the detail panel's effect text is 25 pt and centred in its
     parchment box.

## Layout JSON keys (round 3)

- `hand.socketCard`: `{ "title": <text spec, zone [26,12,199,55], size 34 = auto-size ceiling>,
  "icon": {"centre":[112.5,164], "size":[140,140]}, "glyph": {"centre":[37,281], "size":[16,16]} }`
  -- in-socket card mode (`Cards/CardFrameSocket_<Category>.png`), same 225x300 card units as
  `hand.cardText` (values as of round 4).
- `detail` (new top-level key, sibling of `sockets`/`hand`/`enemy`): `{ "zones": {
  "title": [x0,y0,x1,y1], "art": [x0,y0,x1,y1], "icon": {"centre":[x,y], "size":[w,h]},
  "target": [x0,y0,x1,y1], "effect": [x0,y0,x1,y1], "footer_label": [x0,y0,x1,y1],
  "footer_glyph": {"centre":[x,y], "size":[w,h]}, "close": {"centre":[x,y]} } }` -- in 360x480
  display units, top-left origin, for `Cards/DetailFrame_<Category>.png`.
- `elements[id="targeting_banner"].buttons`: `{ "size": [110, 30], "gap": 8, "text": <text spec> }`
  -- the CANCEL / SPLIT HITS chips on the ribbon's own row.
- `enemy.elements[id="history"].token` / `.token_size`: `"Enemy/HistoryToken.png"` / `[22, 22]` --
  the parchment token painted behind each observed-history icon (`icon_size` also tightened to
  `[16, 16]` and `spacing` to `25`).
