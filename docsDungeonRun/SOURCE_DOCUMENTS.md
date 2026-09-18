# Source documents for Combat HUD V1

## Scope and precedence

The current task, `docs/GAME_DESIGN.md`, and locked decisions remain the implementation constraints. `docs/DECISIONS.md` currently locks scene conventions, not a complete balance version. The supplied tabletop PDFs inform this **lab presentation**, not a silent rewrite of production gameplay.

## Guide facts used in the HUD

Page numbers are one-based PDF pages in `DungeonRunGuide.pdf`.

| Pages | Fact | V1 use |
|---|---|---|
| 5 | Draw five cards for the initial hand. | Initial hand is five; layout debug permits 1–10. |
| 6 | Player starts at 10 HP; upgrades may raise it to 13. | Lab maximum defaults to 10; initial 8 is an illustrative damaged state, not a rule. |
| 9 | Use two cards each combat turn; draw two next turn. | Two mandatory action slots; resolve, then end turn. The normal five-card preview refills by two. |
| 9–10 | Number 7 identifies the variable seventh enemy action. | **7 is not a player-card action cost.** |
| 10 | Attack deals 1; Defence blocks 1; Piercing deals 1 and negates defence but can be dodged. | Base-one presentation text and transient preview values. |
| 11 | Dodge evades one attack; Heal Self restores 1 HP. | Presentation text; simple healing feedback only. |

The 21-card fixed deck is already specified in `docs/GAME_DESIGN.md`. The lab's deterministic repeating five-kind sample conserves 21 instances; it is **not an authored starting-deck composition**.

## Legacy assets are preserved

Existing Attack/Armor/Basic Healing assets contain prototype values of five. The lab keeps their serialized references and clones them into unsaved `HideAndDontSave` CardData objects with Guide base-one values. Missing Dodge/Piercing definitions are also transient. Art still comes from the existing definition or HUD theme; no second persistent gameplay database is created.

`LabCombatHUDSource` is explicitly marked **LAB PREVIEW**. Its elementary damage/healing feedback does not implement defence, dodge, piercing interactions, simultaneous enemy actions, poison, or a complete combat resolver. Production gameplay and legacy global events are untouched.

## Conflicts intentionally left open

- Encounter progression: `DungeonRun_Rules.pdf` describes **4/3/2**; Guide page 9 describes **3/3/3**; older playtest notes discuss **3/4/3**.
- Boom-related behavior differs across supplied rule/playtest versions. No death/explosion balance is selected or implemented here.
- The legacy Unity hand/action defaults also differ from the documented five-card/two-action design. The lab follows the current design without changing those legacy components.

These conflicts require a separate design decision if gameplay implementation depends on them. This UI milestone does not resolve them.

## Art provenance

`DungeonRun_Stuff_Printable.pdf` page 13 supplies the five original action icon regions. The repository copy matches the previously used Downloads PDF byte-for-byte. Exact extraction regions are in `Captures/CombatHUDV1/extract_printable_icons.py`; digital frame/portrait provenance is in `Assets/Art/UI/CombatHUDV1/ART_SOURCES.md`.
