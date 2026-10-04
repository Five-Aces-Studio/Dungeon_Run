# AI Workflow — Dungeon Run

## Agent split

- Codex Desktop + GPT-6 Astra: visual technical art, shaders, UI, VFX, cameras, Blender, scene composition.
- Claude Code + Opus: C# architecture, combat, cards, enemies, dungeon systems, tests, difficult refactors.
- Terminal / CLI: Git, builds, scripts, batch automation.

## Scene workflow

- `SceneVictor` = main scene.
- `SceneVictorLab` = shared experimental / validation scene.

Use `SceneVictorLab` first for experimental work. Integrate approved results into `SceneVictor` only as a separate integration step.

## One-writer rule

Do not have two agents edit the same files at the same time.
