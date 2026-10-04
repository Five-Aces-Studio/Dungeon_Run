# Game Design — Dungeon Run: Trials of Pascala

## Status

This file consolidates the current digital adaptation without pretending unresolved tabletop variants are already final.

Use `docs/DECISIONS.md` to determine what is locked and what still needs playtesting.

## High concept

Dungeon Run is a single-player roguelike deck-building dungeon crawler set in the Trigonal Abyss.

The player enters a reality-warped dungeon associated with the wizard Pascala and the power of Matika, seeking the Tetra Head and ultimately confronting its three-headed Hydra guardian.

## Design pillars

### 1. Two-action tactical combat

- Player hand size: **5 cards**.
- Normal player turn: **exactly 2 cards/actions**.
- Encounters may contain up to **3 enemies**.
- The tension comes from having fewer actions than possible threats.

Do not replace this with a generic energy-spending card system without an explicit design decision.

### 2. Learnable enemies + controlled uncertainty

Enemies have recurring attack behavior that can be learned.

The physical rules use a monster attack deck whose order is preserved when exhausted during that enemy's encounter. This creates a pattern the player can remember.

A special uncertainty mechanism adds a variable enemy action ("7th card" in the tabletop design).

The exact digital ratio is still open:
- original baseline: 6 known actions + 1 variable action;
- meeting discussion also considered 5 known + 2 variable.

The invariant is:
**pattern recognition must matter, but certainty must never be perfect.**

### 3. Fixed-size evolving deck

The player deck remains **21 cards**.

Progression primarily replaces/upgrades cards rather than endlessly growing deck size.

This is a core identity.

### 4. Triangular dungeon

The dungeon should preserve the triangular identity of the tabletop game.

Original tile concepts include:

- Encounter;
- Armory;
- Observatory;
- Bonus;
- Teleport.

The digital version may automate tedious placement while preserving meaningful route/reveal decisions.

### 5. Matika as systemic uncertainty

Matika is the setting's reality-warping force.

The digital adaptation should aim to connect Matika to systems such as:

- controlled randomness;
- corruption/risk;
- events;
- special cards;
- visual distortion;
- dungeon changes;
- boss behavior.

Not every idea above is locked yet.

## Combat resolution

Current meeting direction:

1. Player commits 2 actions.
2. Enemy actions are resolved in the same turn.
3. Damage/attack and defence interactions resolve before healing.
4. A unit that dies from damage cannot rely on a later heal.
5. Miss/no-op actions do nothing.

Implementation must be deterministic and explicit about ordering.

## Card families

Existing tabletop effects include:

- Attack;
- Defence;
- Miss;
- Dodge;
- Combo;
- Heal;
- Piercing Attack;
- Counterattack;
- Charge;
- upgraded Defence;
- upgraded Dodge;
- upgraded Heal;
- Combo+;
- Lifesteal;
- Explosion.

Digital expansion may add families such as:

- Guard / counter;
- Fury / combo;
- Venom / damage-over-time;
- Matika / risk-reward;
- Vitality / sustain.

Do not lock the player into a class simply because a family exists.

## Character/build philosophy

Current preferred direction:

- one flexible protagonist;
- broad access to multiple card families;
- build identity emerges during the run;
- no strict "poison character / warrior character" lock unless later playtesting proves it better.

This is a direction, not a fully balanced final rule.

## Encounter progression

The tabletop sources consistently point toward a run culminating in a boss after approximately **9 normal encounters**, but source documents disagree on the Basic/Intermediate/Endgame split.

Do not hard-code a phase distribution until `DECISIONS.md` locks it.

## Boss

The final guardian is a three-headed Hydra associated with the Tetra Head / Matika.

Digital adaptation goal:
make the Hydra test the game's core skills:

- pattern learning;
- prioritizing multiple threats;
- two-action economy;
- controlled uncertainty;
- build quality.

A promising direction is to let different heads express distinct patterns/roles, but this must be prototyped before being treated as final.

## Exploration and events

The physical game includes triangular tiles and room effects.

The digital adaptation may expand with:

- narrative events;
- passive items/relics;
- secret items;
- optional risk;
- corruption;
- alternate outcomes.

These ideas are currently **to validate**, not locked.

## Adaptation principle

Automate tabletop administration, not interesting decisions.

If a physical action exists only because a human had to manage cards/tokens, Unity may automate it.

If the action creates a strategic choice, preserve or deepen it.

## Numbers

Prefer small, readable numbers unless balancing proves otherwise.

The tabletop system gains clarity from low HP/damage values and discrete effects.

Avoid inflating values simply because the game is digital.
