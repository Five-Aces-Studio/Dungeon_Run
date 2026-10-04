# AGENTS.md — Dungeon Run

## Purpose

This file defines how Codex, GPT-6 Astra, and other repository-aware coding agents must operate in this Unity project.

The user's explicit instruction for the current task has highest priority. These repository instructions define the default workflow when the user has not specified otherwise.

## Project identity

**Dungeon Run: Trials of Pascala** is being adapted from a solo tabletop dungeon-crawler/deck-builder into a Unity roguelike.

Core identity:

- turn-based card combat;
- 5 cards in hand;
- exactly 2 player actions per normal turn;
- enemies with learnable attack patterns plus controlled uncertainty;
- a fixed-size 21-card player deck that evolves by replacement/upgrading;
- a changing triangular dungeon;
- the Trigonal Abyss, Pascala, Matika, the Tetra Head and the three-headed Hydra;
- 3D production with a stylized 2.5D / pixel-rendered presentation;
- strong card-battler readability inspired by the clarity and illustrated staging of games such as Slay the Spire, without copying proprietary assets or visual designs.

## Source-of-truth documents

Do not invent project rules when a relevant document exists.

Read only the documents needed for the task:

- Game mechanics / roguelike design: `docs/GAME_DESIGN.md`
- Architecture: `docs/PROJECT_ARCHITECTURE.md`
- Art direction: `docs/ART_DIRECTION.md`
- UI: `docs/UI_DESIGN_SYSTEM.md`
- Shaders / rendering: `docs/SHADER_GUIDE.md`
- Testing: `docs/TESTING_GUIDE.md`
- Performance: `docs/PERFORMANCE_BUDGET.md`
- Multi-agent responsibilities: `docs/AI_WORKFLOW.md`
- Locked/open decisions: `docs/DECISIONS.md`

If documents conflict, do not silently choose one. Check `docs/DECISIONS.md`. If still unresolved, preserve the ambiguity and ask only if it materially blocks the requested task.

## General execution workflow

For significant tasks:

1. Inspect the relevant existing implementation.
2. Read only the relevant project docs.
3. Inspect Unity through MCP when Editor state matters.
4. Make the smallest coherent change.
5. Let Unity compile.
6. Inspect the Unity Console.
7. Fix errors introduced by the change.
8. Run tests appropriate to the change.
9. Enter Play Mode when runtime behavior matters.
10. Visually verify player-visible work.
11. Review `git diff`.
12. Report what changed and how it was verified.

Bias toward completing the requested work rather than stopping for routine clarifications. Ask when an unresolved choice would materially change the result.

## Unity MCP policy

Prefer Unity MCP for structured Editor operations:

- scene hierarchy;
- GameObjects and components;
- materials;
- assets;
- Console;
- Play Mode;
- tests;
- scene state;
- screenshots / captures when supported.

Use direct visual computer interaction when it adds value, especially for:

- visual inspection;
- camera composition;
- Shader Graph;
- Blender;
- UI layout;
- operations not exposed conveniently through MCP.

Do not replace reliable structured MCP operations with fragile GUI clicking without a reason.

## Mandatory visual verification

Visual work is not complete after compilation.

This applies to:

- UI;
- HUD;
- menus;
- shaders;
- materials;
- VFX;
- lighting;
- post-processing;
- cameras;
- animation presentation;
- scene composition.

Required loop:

`IMPLEMENT -> COMPILE -> CONSOLE -> RENDER/RUN -> CAPTURE -> ANALYZE -> CORRECT -> CAPTURE AGAIN`

Never claim visual verification unless actual rendered output was inspected.

For visual comparisons, keep camera framing reproducible.

## Visual A/B rule

For rendering changes, prefer reproducible comparisons:

- A: standard/base rendering;
- B: stylized lighting;
- C: stylized + pixel rendering;
- D: final current stack.

Do not keep an effect merely because it is technically impressive. It must improve readability or art direction.

## Project safety

Never manually edit:

- `Library/`
- `Temp/`
- `Logs/`
- `obj/`
- generated `.csproj`
- generated `.sln`

Treat `.meta` files and Unity GUIDs as critical.

Do not use destructive Git commands without explicit authorization.

Do not discard unrelated user changes.

Do not commit automatically unless requested.

## Experimental visual work

Use dedicated development scenes for experiments.

Preferred:

- `SceneVictorLab.unity`
- `SceneVictorLab.unity`
- `SceneVictorLab.unity`

Do not use production scenes as uncontrolled sandboxes.

## Testing calibration

Do not create broad or redundant tests for tiny reversible changes.

Do create meaningful tests for:

- combat resolution;
- deck state;
- enemy pattern state;
- seeded RNG;
- dungeon generation constraints;
- save/load;
- progression;
- regressions.

Visual appearance requires rendered inspection, not only unit tests.

## Completion report

At the end of a task report:

- changed files/assets;
- implementation summary;
- Unity Console state;
- tests performed;
- visual validation performed;
- remaining limitations or unresolved design decisions.

Be concise. Do not claim actions that were not actually performed.


## Scene policy

Canonical project scenes:

- Main scene: `SceneVictor`
- Experimental / validation scene: `SceneVictorLab`

For shader, UI, camera, lighting, VFX, rendering, gameplay prototyping, or other experimental work:

- use `SceneVictorLab` by default;
- do not modify `SceneVictor` unless the task explicitly requires integration into the main scene;
- validate experimental work in `SceneVictorLab` first;
- only port approved changes into `SceneVictor`;
- do not create alternative lab/test scenes unless explicitly requested.

Treat these scene names as project conventions, not suggestions.
