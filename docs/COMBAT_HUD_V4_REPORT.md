# Combat HUD V4 — Acrylic Painted HUD Report

V4 turns the SceneVictorLab combat HUD from a technical overlay (dark boxes, gold hairlines,
Liberation Sans, placeholder slot plates) into an original hand-painted HUD that belongs to the
acrylic world. It is presentation-only: no rules, patterns, BattleSession math or world art changed.
**Status: ready for visual approval in SceneVictorLab. Nothing was migrated to SceneVictor, and Main
integration waits for explicit approval.**

## Quick path

1. Open `Assets/Scenes/SceneVictor/SceneVictorLab.unity`. It is saved with `CombatHUDV4` active and
   `CombatHUDV1` (V3) inactive.
2. Enter Play Mode and play a turn: hand → sockets → COMMIT → reveal → resolution.
3. A/B menu: `Dungeon Run/Combat HUD V4/Revert to V3 (A/B)` and `Activate V4 (A/B)`. Both are scene
   toggles; save the scene to keep a choice.
4. Evidence: `Captures/CombatHUDV4/v4_r4/` (final, 1080p + 1440p), `v3_revert_final/` (V3 after
   revert), `v3_newcode_a/` (V3 baseline).

## Verification summary

| Check | Result |
|---|---|
| `BattleSessionChecks.RunAll()` | **14/14 PASS** |
| EditMode tests (incl. 8 new UI Core suites) | **372/372 PASS** |
| Painter unittest (`Tools/CombatHUDV4Painter`) | **14/14 OK** |
| Unity Console after install, Play, captures, revert, save | **0 errors, 0 warnings** |
| Protected files SHA-256 (428 files: SceneVictor, world shaders/materials, Combat scripts, CardData, V1/V3 art/theme/prefab) | **428/428 unchanged** |
| Revert to V3 vs V3 baseline (32 captures) | Same layout, text and art. Differences are limited to anti-aliasing on rotated hand-card text (≤0.47% of pixels). That is below the measured noise between two identical V3 runs (1.2%). |
| HUD world coverage, planning (diff vs no-HUD frame) | V4 ≤ V3 in every scenario (idle 19.3% vs 20.8%, config4 21.4% vs 24.0%) |
| Pure black / white / neon pixels (idle 1080) | 0.08% / 0% / 0% |
| `gentle-ai review assess` | risk `medium` (executable editor harness); RDD is off, so writer self-verification plus the parent re-run of the tests applies |

---

## 1. Initial HUD visual audit (V3)

- One shape family everywhere: dark translucent rectangles with thin gold borders. The HUD read as a
  debug overlay laid on top of the painting.
- Cards looked like the printable sheet: flat parchment, hairline rules and Liberation Sans, with no
  painted frame or material.
- **Action slots were the weakest element.** Queued cards sat at a hard-coded 0.27 scale inside
  154x108 landscape plates, so the empty plate dominated and the card did not.
- The Floor panel was the heaviest static element. The player vitals sat on a dark `VitalsPanel`
  slab.
- Enemies showed HP only: no name, a "BLOCK 1" text instead of status chips, and a generic reveal.
- Combo, Counterattack, Charge and Miss had no painted icons (vector fallbacks).
- Commit showed "LOCKED" for every resolving phase. Victory/Defeat was a plain text panel.
  Defeated enemies stayed standing.

## 2. Art-direction principles

- **Shape families**, not one rectangle:
  - crest/plaque: player, floor;
  - medallion/token: actions, draw, discard;
  - socket: action slots;
  - command plate: Commit;
  - illustrated frame: cards, detail;
  - compact track: enemy HP.
- **Dungeon Run motifs:** trigonal studs and gems, pointed-arch art windows, and the trigon target
  marker. These echo the world's arch and the Trigonal Abyss.
- **Painted, not generated:**
  - living-width warm ink contour;
  - 2–3 step cel bevels;
  - brush grain sampled from the world stroke texture;
  - worn highlight dabs biased to the lit edge;
  - low-frequency irregular silhouettes.
- **Clean text zones:** a flattened plane behind every text box (luminance std < 3/255, enforced by
  tests).
- **Palette:** parchment/ivory, muted bronze, antique gold, blue-black charcoal, desaturated teal,
  warm health red and limited amber. No neon or flat white.
- **Hierarchy and negative space:**
  - Cards sit highest, with a painted contact shadow and lift.
  - Sockets and Commit are the interactive layer; static chrome is ~15% dimmer.
  - V4 covers less of the world than V3.
- **References:** the Slay the Spire 2 screenshots were used only for integration quality and
  hierarchy. Dungeon Run's own tabletop printable and the V1 mockup provided identity. No external
  asset was copied.

## 3. Before/after summary

| Area | V3 | V4 |
|---|---|---|
| Player | Dark slab with text | Portrait medallion joined to a painted HP track, name in display caps, status chips on demand |
| Floor | Heavy dark panel | Small hanging plaque, "FLOOR 01" primary, location secondary |
| Actions / Draw / Discard | Text boxes | Trigonal medallion with pips; shield and tapered tokens with painted card stacks |
| Action slots | Landscape plates, card at 0.27 | Card-shaped engraved sockets with a rune tab; card at 0.5 in a compact socket frame |
| Commit | Button | Hex-ended command plate with an arch crest, 6 states |
| Cards | Printable sheet | Painted frames per category, parchment title ribbon, pointed-arch art window |
| Enemies | HP bar only | Intent socket ("?" → icon), name, HP track, chips, observed history, gold target trigon |
| End states | Text | Painted terminal banner and vignette; defeated enemies sink and darken |

## 4. Files created

- **Runtime:**
  - `Assets/Scripts/UI/Common/CombatHUDStyleV4.cs`
  - `Assets/Scripts/UI/Combat/CommitButtonPresenter.cs`
  - `Assets/Scripts/UI/Combat/EnemyDefeatPresenter.cs`
- **Pure presentation core** (`DungeonRun.UI.Presentation.Core`, no engine references), in
  `Assets/Scripts/UI/Core/`:
  - `SlotVisualStateResolver`, `CommitVisualStateResolver`, `CardCategoryMap`, `ActionPipLayout`,
    `SlotRowLayout`
  - `HudStatusFormat`, `HudFeedbackFormat`, `HudPhase(s)`, plus their state enums
- **Tests:** `Assets/Tests/EditMode/UI/` (8 suites, `DungeonRun.UI.Presentation.Tests.asmdef`).
- **Editor:**
  - `Assets/Editor/DungeonRun/DungeonRunCombatHUDV4Lab.cs`: installer, fonts, art import, A/B.
  - `Assets/Editor/DungeonRun/DungeonRunCombatHUDV4Capture.cs`: Play Mode scenario capture harness.
- **Assets:**
  - `Assets/Settings/CombatHUDV4Theme.asset`
  - `Assets/Settings/CombatHUDV4/CombatHUDStyleV4.asset`
  - `Assets/Settings/CombatHUDV4/HudLayoutV4.json`
  - `Assets/Prefabs/UI/CombatHUDV4/Card.prefab`
  - `Assets/Art/UI/CombatHUDV4/**`: 84 sprites, fonts and TMP assets, `ART_SOURCES.md`
- **Tools:**
  - `Tools/CombatHUDV4Painter/`: deterministic painter, mock compositor, unittest, README.
  - `Tools/CombatHUDV4Fonts/`: lining-digit font derivation.

## 5. Files modified

- Every runtime change below is gated on `theme.v4Style != null`; the V3 branches are unchanged:
  - `Cards/CardView.cs`, `Cards/CardActionSlot.cs`, `Cards/CardHandController.cs`
  - `Cards/CardDetailPanel.cs`, `Cards/CardTargetingPanel.cs`
  - `Combat/CombatHUD.cs`, `Combat/PlayerCombatHUD.cs`, `Combat/EnemyCombatHUD.cs`
  - `Combat/CombatFeedbackPresenter.cs`
- `Common/CombatHUDTheme.cs` gains one field, `v4Style`.
- `Combat/ICombatHUDSource.cs` and `LiveCombatHUDSource.cs` gain two read-only snapshot fields,
  `PlayerBlock` and `PlayerDodge`, projected from the existing session snapshot.
- `SceneVictorLab.unity` gains a `CombatHUDV4` root (a restyled copy of `CombatHUDV1`, which is kept
  inactive). The world edits made outside this task (torches, pool light, fixture material) and the
  `EnemyArtReview_V1` root are preserved.

## 6. Visual assets created or edited

84 procedural sprites at 2x the 1080 reference:

| Folder | Sprites |
|---|---|
| `Cards/` | Hand frames x5, socket frames x5, detail frames x5 + fallback, art washes, shadow, focus rim |
| `Controls/` | Socket + rim + glow, Commit x6, small button, close button |
| `Crest/` | HP track, fill, ghost; block and dodge chips |
| `Enemy/` | HP track and fill, intent socket, unknown sigil, reveal flash, history token, target trigon |
| `Static/` | Floor plaque, action medallion, pips on/off, draw/discard tokens, banner, terminal banner, text wash, vignette |
| `Glyphs/` | Category and feedback glyphs |
| `Icons/` | 9 action icons, each with a 96 px small copy |

Seed, command and per-file SHA-256 are in `Assets/Art/UI/CombatHUDV4/ART_SOURCES.md`. No image was
edited by hand.

## 7. Assets reused

- **V3 ink icons** (Attack, Defence, Dodge, Heal, Piercing): read-only. They are recoloured to warm
  ink and grained, and their parts are recomposited for Combo and Counterattack.
- **Art:** `Portrait.png` and the world stroke texture, used as brush grain.
- **Code:** the V3 interaction code (fan, drag, targeting, split hits, queue/cancel, scroll, feedback
  lanes and pool), the theme timings, and the DOTween conventions.

## 8. Player HUD

- Portrait medallion (118 px) physically joined to a painted HP track (250x30).
- "THE WAYFARER" in display caps above the track.
- HP number in bold body with an outline, sitting on the bar.
- BLOCK/DODGE chips (58x28 parchment tag + number) appear under the bar only when above 0.
- The `VitalsPanel` slab is gone.

## 9. HP bars

- Recessed track with an oxblood empty interior and bronze end caps.
- Painted red fill with an ink contour, a lighter top band and a faint streak.
- A lighter "recent damage" ghost trails the fill tween.
- Enemy version: 136x20 track, 128x12 fill, number on the bar.

## 10. Floor HUD

- 230x64 hanging plaque with a pointed bottom, rim ~15% dimmer than the interactive controls.
- "FLOOR 01" (display) is primary; "TRIGONAL ABYSS" is small and secondary.
- Digits use lining figures: the derived fonts remap them so "01" does not read as "o1".

## 11. Action counter

- 132 px trigonal medallion showing "2 / 2" in display type, with a row of diamond pips (lit =
  available).
- `ActionPipLayout` supports any max count and hides the pips above 6.
- Small "ACTIONS" label.

## 12. Draw / Discard

- Related tokens with different silhouettes: shield-shaped for draw, tapered for discard. Both are
  96x112.
- Each has a painted card-stack icon, a count and a small label, and stays subordinate to the hand.

## 13. Action-slot redesign

- **Shape:** portrait card sockets (118x158):
  - teal-charcoal engraved face with an inner recess;
  - brighter bronze rim with worn dabs;
  - ghost arch glyph;
  - parchment rune tab (I, II, III, IV).
- **Queued cards:** shown at 0.5 in a dedicated socket frame (enlarged art window, title only), so
  the card dominates. Round 4 made the socket ribbon taller and the title ceiling 34 units, which
  reads ~15 px at 1080 (was ~9 px).
- **States:** from `SlotVisualStateResolver`:
  - EMPTY;
  - SELECTED/armed: warm rim;
  - HOVER_VALID: warm rim and lit rune;
  - HOVER_INVALID: desaturated, `#C8553D` rim;
  - QUEUED;
  - LOCKED: sunk and dimmed during the reveal;
  - RESOLVING: amber.
- **Targets:** shown above the socket, including split targets ("ABYSS WARDEN / ASH HOUND").
- **Row layout:** 3–4 slots switch to a dense row (0.82 scale, 112 spacing) that clears the
  characters. The row lays out only active slots.
- **Terminal:** once the battle ends, the sockets fade out and stop taking input.
- **Flow:** the row sits between the hand and the world and reads HAND → sockets → COMMIT.

## 14. Commit redesign

- 260x84 hex-ended command plate with a pointed-arch crest carrying the single flow gem.
- Six sprite states resolved by `CommitVisualStateResolver`: DISABLED, READY, HOVER (1.03), PRESSED
  (0.97), LOCKED and RESOLVING.
- RESOLVING now has its own label; the terminal label is "COMPLETE".
- No bloom and no looping pulse; the one-shot ready pulse is kept.

## 15. Card redesign

- The root stays 225x300, so the fan and staging maths are unchanged.
- **Frame:** one painted frame per category. Shared chamfered silhouette, bronze rail with ink
  contour, category-tinted bevel and corner gems.
- **Title and art:** parchment title ribbon (display type) over a pointed-arch art window. The window
  holds a painted category wash; the icon is enlarged to ~150 px over a soft shadow.
- **Text:** clean parchment effect box in body type; footer tab with the category glyph and a
  small-caps label.
- **Depth:** painted contact shadow; hover and selection use a soft painted rim.
- **Disabled cards:** a CanvasRenderer tint instead of alpha, so fanned cards never ghost.
- **Unchanged:** no cost numbers; hierarchy stays TITLE > VISUAL > EFFECT > CATEGORY.

## 16. Category visual system

| Category | Kinds | Colour |
|---|---|---|
| ATTACK | attack, piercing, combo | oxblood |
| DEFENCE | defence | slate steel |
| MOBILITY | dodge, counterattack | verdigris |
| SUPPORT | heal | moss |
| SPECIAL | charge, miss | dusk violet-umber |

Each category is carried by the frame accent, the art wash, the ribbon tails, the footer glyph and
the label, never by colour alone. The mapping is `CardCategoryMap`, which is tested.

## 17. Combo / Counterattack treatment

- New painted icons in the same ink family:
  - Combo: staggered twin swords, from the tabletop art.
  - Counterattack: boot and sword.
  - Charge: clock.
  - Miss: swipe arcs and a dust puff.
- The Miss icon deliberately differs from the tabletop "prohibition circle"; a prohibition sign read
  as "disabled" in the HUD.
- Combo multi-hit targeting uses the painted ribbon with CANCEL / SPLIT HITS chips on the same row.

## 18. Typography and licensing

- **DISPLAY:** Alegreya SC Bold / ExtraBold.
- **BODY:** Alegreya Sans Regular / Medium / Bold / ExtraBold.
- **License:** SIL OFL 1.1, downloaded from google/fonts with the user's authorization. The license
  files and `FONT_SOURCES.md` are next to the fonts.
- **Modification:** the derived "DR" families remap digits to the fonts' own tabular lining glyphs,
  because TMP cannot apply `lnum`.
- **TMP presets:** Display Ink, Display Outline, Body Outline, Number Outline and Engraved Rune.

## 19. Card detail

- Enlarged illustrated card (360x480) with a per-category frame, the same ribbon, arch window and
  icon.
- Ink target line ("Target: one enemy · 2 hits").
- Effect text in the parchment box: since round 4 it is 25 pt and centred. The existing scroll
  viewport is kept for long text.
- The scroll hint appears only on overflow, outside the card.
- Painted close button.

## 20. Enemy HUD

- Compact and anchored to the enemy.
- Intent socket (52 px, 40 px icon) left of the HP track.
- Name in display caps (V3 never showed it).
- BLOCK/DODGE chips beside the track.
- Observed history: up to 3 past intents on parchment tokens behind an eye glyph. It shows past
  observations only and never leaks future pattern order.
- Valid targets get a bobbing gold trigon (36x30 since round 4; it was 24x20 and tint-darkened).
  The selected target is brighter and 1.2x.

## 21. Enemy reveal

- Planning shows a painted "?" sigil.
- On the authoritative reveal:
  1. The "?" scales out and the icon scales in (0.3 s).
  2. A brief amber flash plays.
  3. The socket shows the parchment face with the icon and a concise label ("ATTACK", "DODGE").
- Multi-action intents use a compact icon row.
- The reveal is driven by the existing `Observe` trigger.

## 22. Combat feedback

- Same pool, lanes, timings and strings.
- 34 pt display numbers with an outline, anchored to the HP tracks and staggered.
- Damage colours: ivory `#EFE3C8` on enemies, warm red `#C9482F` on the player.
- Status labels (BLOCK, DODGE, PIERCED, CHARGED, +heal) carry a small painted glyph. Plain damage
  has none.

## 23. Enemy-defeat presentation

- `EnemyDefeatPresenter` reacts to the authoritative `Defeated` event, with health ≤ 0 as a safety
  net.
- Over 0.8 s the model sinks 0.45x its bounds height, tilts 10° and darkens 55% toward the fog tone
  through a `MaterialPropertyBlock` (no asset edits).
- The body stays visible as the defeated state. The intent socket fades out and the HUD shows
  "DEFEATED".
- Everything is restored on `Unbind`; the animation owns no gameplay state.

## 24. Victory / Defeat presentation

- 560x166 painted terminal banner.
- Title in display type: gold for VICTORY, oxblood for DEFEAT. Body text unchanged.
- Soft full-screen vignette at 0.35.
- Commit shows COMPLETE; the sockets fade out.

## 25. Inspector / theme controls

- `CombatHUDStyleV4` holds art only:
  - sprites per element and state, category sprites, fonts and material presets;
  - socket, rim and focus colours;
  - Commit scales and labels;
  - dense-row numbers;
  - socket-card zones and title ceiling;
  - target-marker tints and scale;
  - reveal and defeat timings, vignette alpha, `shadowIntensity`, `highlightStrength`;
  - detail and targeting zones.
- No gameplay values live in presentation assets.
- Layout numbers come from `HudLayoutV4.json` (the painter's table) and are written by the installer.
  The style asset remains editable in the Inspector.

## 26. 1080p result (`Captures/CombatHUDV4/v4_r4/*_1920x1080.png`)

18 scenarios: idle, hover, selected, drag valid/invalid, queued 1/2, commit hover, detail, split,
reveal, resolving, turn 2, victory, defeat, config4, config1 and feedback.

All text is legible:

- socket titles ~15 px;
- enemy names and HP;
- the targeting ribbon and chips;
- the detail effect.

The world and characters stay visible, and HUD coverage is below V3 in every planning scenario.

## 27. 1440p result (`*_2560x1440.png`)

The same set. Sprites are authored at 2x, so every element renders at or below 1:1 texel density.
Crops of the socket cards and target trigons are crisp, with no upscaling blur.

## 28. 14/14 regression result

- `BattleSessionChecks.RunAll()`: 14/14 PASS.
- EditMode: 372/372 PASS.
- Painter unittest: 14/14 OK (sizes, determinism, text-safe zones, layout keys).

## 29. Console result

0 errors and 0 warnings after install, Play Mode, 68 captures (36 V4 + 32 V3 revert), revert,
re-activate and scene save.

## 30. Remaining visual debt

- The Commit plate's end studs can read as left/right arrows.
- The detail panel's target line (16 pt) is small and sits on a strip at the arch base.
- On the terminal banner, the hand stays fanned and the action pips stay lit. This is the V3 logic,
  unchanged.
- The synthetic `feedback` capture passes V3 theme colours through the harness, so its enemy "-1"
  is red. Live resolution uses the ivory enemy-damage colour (see `resolving`).
- Critique method: rounds 1–2 used the 3-lens critic workflow (illustration, readability, cohesion)
  plus a synthesizer. The round-3 panel run failed on a session limit, so rounds 3–4 were reviewed
  directly against the same lenses and the §42 bar.
- The V3 revert is not byte-identical in pixels: rotated TMP text anti-aliasing varies between runs
  (see the verification table).

## 31. Ready for visual approval?

Yes. All §42 acceptance items are met in the final captures:

- not a prototype overlay, and it belongs to the acrylic world;
- tactile illustrated cards and authored sockets;
- a Commit identity;
- a compact crest and a light floor plaque;
- a cohesive actions/draw/discard family;
- illustrated HP bars and intentional typography;
- coherent enemy intents with painted Combo/Counterattack icons;
- inspect in the same language, and a working defeat reaction;
- painted end states, a visible world, and clear flow.

## 32. Main integration recommended?

Not yet. That is the user's call after reviewing SceneVictorLab.

- SceneVictor currently references no HUD script.
- Integration would mean installing `CombatHUDV4` there. It should be a separate, explicitly approved
  step.
