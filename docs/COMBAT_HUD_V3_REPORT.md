# Combat HUD V3 — validated Lab progress

Status: implemented and runtime-tested foundation, **not final visual acceptance or Main integration approval**. SceneVictorLab only. No commit, staging or push was performed. Review mode remains disabled/unmanaged.

## Delivery summary

1. **Visual changes:** normalized transparent card symbols, retained ornamental V1 frames, restrained category accents, cleaner portrait backing, native-resolution HUD, lower-right Commit, separated phase status.
2. **UX changes:** card-first enemy targeting, optional per-hit assignment, deliberate middle-click detail inspection with scroll, drag/drop feedback, resolution locks and observed enemy history.
3. **Created files/assets:** `Assets/Editor/DungeonRun/DungeonRunCombatPolishLab.cs`; `Assets/Scripts/UI/Cards/{CardDetailPanel,CardTargetingPanel}.cs`; `Assets/Scripts/UI/Combat/CombatFeedbackPresenter.cs`; `Assets/Settings/CombatHUDV3Theme.asset`; `Assets/Prefabs/UI/CombatHUDV3/Card.prefab`; five icons under `Assets/Art/UI/CombatHUDV3/`, with Unity metadata. This local report is ignored by the existing `docs/*.md` rule.
4. **Modified files:** `Assets/Scripts/UI/Cards/{CardActionSlot,CardHandController,CardView}.cs`; `Assets/Scripts/UI/Combat/{CombatHUD,EnemyCombatHUD,ICombatHUDSource,LiveCombatHUDSource,PlayerCombatHUD}.cs`; `Assets/Scripts/UI/Common/{CombatGlyph,CombatHUDTheme}.cs`; `Assets/Scripts/Combat/{BattleSession,CombatTypes}.cs`; `Assets/Scenes/SceneVictor/SceneVictorLab.unity`. Domain edits add optional typed event provenance only; no combat arithmetic or phase changes.
5. **New visual assets:** Attack, Defence, Dodge, Heal and Piercing transparent PNGs. Miss and Charge have native vector fallbacks. V3 theme/prefab are isolated from V1 assets.
6. **Tabletop reuse:** official `docsDungeonRun/DungeonRun_Stuff_Printable.pdf`, 19 raster pages, page 13 card fronts and page 14 backs inspected. Existing V1 crops supplied the five cleaning inputs. The new images are faithful AI-assisted edits, not pixel-exact extractions. Original sources remain unchanged. The printed reverse layout was inspected, not imported as a new digital card back.
7. **Typography:** existing Liberation Sans TMP font, bold display hierarchy and regular body/numeric hierarchy. No external font was imported. A suitably licensed fantasy display face remains an art dependency; two distinct font families are not claimed.
8. **Card hierarchy:** title, large central symbol, effect text and small category footer. Color is supplemented by icon and category text. Redundant action-cost badge hidden; rules still validate one card per slot.
9. **Targeting:** semantic TargetMode controls the flow, never card-name matching. Single-opponent selection highlights valid living targets. Self/no-target cards do not require an enemy. Invalid target IDs are rejected.
10. **Multi-hit:** shared target is the default; optional split mode collects each hit, displays assignments, and queues the frozen target list. Combo split to enemies 1 and 2 was exercised.
11. **Enemy reveal:** future intent remains Unknown during planning. Typed reveal metadata selects icons after commit; multi-action intent is multiline. History contains only the last two observed actions. Miss/Charge reveal text works, but their enemy icon slots currently lack the card-view vector fallback.
12. **Combat feedback:** bounded 24-label pool (theme range 8–48), damage, BLOCK, DODGE, heal, PIERCED, CHARGED and defeat text; HP interpolation; silent semantic audio hooks. Per-actor lanes replace overlapping jitter; overflow is presentation-only batching, never a domain delay.
13. **Animation/staging:** adaptive hand fan, hover lift/neighbor spacing, selected elevation, draw/play/discard transitions and distinct per-slot play positions. Locked slot plates hide during reveal/resolution rather than covering played cards. Default two-card staging was recaptured after correction.
14. **Terminal:** Victory, Defeat and mutual-defeat titles with modest fade and opaque framed backing. Combat controls lock. No rewards, progression or restart system added. Enemy HUD dims and becomes untargetable on death; the 3D enemy model does not yet collapse/fade.
15. **Inspector tuning:** hand spacing, arc, tilt, lift/scale, neighbor separation, draw/play/discard timing and spacing; theme feedback pool, event spacing, HP/reveal/terminal timing and colors. Gameplay settings remain separate.
16. **Preserved gameplay/world files:** ruleset, encounter, patterns, CardData assets, configuration factory, controller pacing and deterministic checks unchanged. Main and world renderer/shader/material/mesh assets unchanged against the resume baseline. The only domain changes are BattleEvent.DefinitionId and its event population, needed to avoid name-based icon inference. Newer Acrylic world work from another task was preserved, not rolled back to the earlier visual baseline.
17. **Regression results:** `DungeonRun.Combat.BattleSessionChecks.RunAll()` returned **PASS 14 deterministic checks** after the final presentation compile. Includes simultaneous kill, damage-before-healing, block/dodge, piercing, split targets, cancellation, exact action count, deck conservation, reveal redaction, 6+1/5+2 seeded patterns, configuration copies, charge and reentrancy. No tests weakened.
18. **1080p:** actual Game View capture inspected. Default hand, counters, enemy HP, hidden intent, Commit, corrected two-card staging and terminal layer exercised.
19. **1440p:** actual 2560×1440 captures inspected for default hand, detail scrolling, multiline reveal, feedback lanes, Victory and Defeat. The 18-line custom detail fixture reaches its last line through ScrollRect input without shrinking its text.
20. **Console:** final Play validation returned zero errors/warnings. A slot CanvasGroup fake-null error encountered during development was fixed using Unity-aware component checks and retested. Tool-snippet compile mistakes were not project compilation failures.
21. **Visual debt:** action slots still look too generic; final fantasy font; stronger enemy status/icon treatment; dedicated Combo/Counterattack symbols; world-model defeat response. This is a meaningful improvement, not mockup-equivalent production finish.
22. **UX debt:** no full mouse-driven endurance playthrough or profiler session; synthetic EventSystem pointer callbacks exercised hover and drag-to-enemy. Extreme configurations are functional but not final composition. Tooltip styling is still plain. No new persistent automated UI suite was added.
23. **Match to target:** card prominence, frame language and HUD hierarchy improved, but not close enough to call visual acceptance complete. World geometry/composition is intentionally outside this task.
24. **Main recommendation:** do not migrate yet. Finish the visual debt and obtain visual approval first. SceneVictor remains untouched.

## Runtime evidence

All altered test rules/decks/encounters were transient Play Mode clones, never saved to the source assets.

| Scenario | Observed result |
|---|---|
| Default 5 cards / 2 actions | Hidden intent → commit → reveal → resolution → turn 2, hand 5, discard 2 |
| 6 cards / 4 actions, 5+2 pattern, multi-action enemy | Turn 2: hand 6, draw 11, discard 4; Attack + Miss reveal |
| 3 cards / 1 action | Turn 2: hand 3, draw 17, discard 1 |
| Defence + Dodge vs three attacks | Player HP 9; DODGE, BLOCK 1, -1 feedback |
| Heal + Piercing vs Defence | Player 5→6, enemy 20→19; +1 and PIERCED 1 |
| Charge + Miss | CHARGED feedback |
| Terminal fixtures | Victory and Defeat, Commit disabled |
| LabPreview | Hand 5, preview End Turn available; Live does not expose it |

Useful captures in `Captures/CombatHUDV3/`:

- `final-1080.png`, `final-1440.png`: current default composition.
- `staging-corrected.png`: slot plates no longer cover committed cards.
- `detail-1440.png`, `detail-bottom-1440.png`: full-text inspection from first to last lines.
- `feedback-corrected-1440.png`: separated damage/block/dodge lanes. This diagnostic capture used a transient 8-second label lifetime; shipped theme remains 0.9 seconds.
- `victory-final-1440.png`: corrected opaque terminal panel.
- `defeat-1440.png`: Defeat state before the shared terminal frame correction.

Earlier `iteration1-*`, `config4-reveal.png` and `feedback-block-dodge-1440.png` show defects found during iteration, not the accepted corrected state. Four-action gameplay was verified; the final slot-plate correction was recaptured with the default two actions rather than repeating the four-action capture.

## Preservation and rollback boundary

The 231 files recorded in `Captures/CombatHUDV3/resume-protected.json` match their baseline hashes. Comparing serialized Lab blocks against `SceneVictorLab-resume-before.txt` found 44 changed blocks, all under CombatHUDV1; no added or removed blocks and no world changes. Do not restore the entire Lab from Git: the working tree also contains unrelated Acrylic world work.

The coherent rollback unit is V3 presentation code/assets plus only the HUD subtree changes and optional event provenance fields. Preserve unrelated renderer, shader, material, mesh and scene edits. No rollback was performed.

## Icon provenance

Builtin image editing was used; no API-key CLI fallback. The official PDF and original V1 crops were not edited. Five generated sources in this task's generated-images directory:

| V3 asset | Generated source |
|---|---|
| AttackIcon.png | exec-08f03c65-7df0-45e2-b652-32fdc2619823.png |
| DefenceIcon.png | exec-2dba252d-3a5e-49bd-9cfc-365e2c63fc25.png |
| DodgeIcon.png | exec-34105af7-cca7-4230-b8e4-29c16b283ca3.png |
| HealIcon.png | exec-c39106b9-7073-43e2-bb27-588be5313850.png |
| PiercingIcon.png | exec-0aa20c17-4fcc-41c2-aa5c-54eebb4f9f96.png |

Cleaning intent: preserve the original symbol, remove printed parchment/dot remnants, normalize framing and margins, retain transparent alpha. Rights to the source artwork remain those of the supplied official material; this report does not assert a new redistribution license. V1 frame/portrait provenance remains in `Assets/Art/UI/CombatHUDV1/ART_SOURCES.md`.
