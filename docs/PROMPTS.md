# Ready-to-use Agent Prompts — Dungeon Run


## 0. Astra — Project Preflight

```text
Activate the current Dungeon_Run repository using Serena and read Serena's initial instructions.

Then read AGENTS.md.

Do not modify anything yet.

Verify access to:
- Serena
- Unity MCP

Use Serena to inspect the C# structure and Unity MCP to inspect the currently open Unity project.

Project scene conventions:
- SceneVictor = main scene
- SceneVictorLab = experimental / validation scene

Report:
- whether Serena is correctly activated
- whether semantic C# navigation works
- whether Unity MCP is connected
- Unity version
- active render pipeline
- currently open scene
- whether SceneVictor and SceneVictorLab are present
- current Console errors/warnings
- current Git state
- relevant existing shaders/materials
- whether the repository is ready to begin the shader milestone in SceneVictorLab

Do not create, edit, delete, move, or rename anything.
```


## 1. Astra — First Shader Milestone

Use in Codex Desktop with GPT-6 Astra.

```text
You are the primary technical-art and visual-development agent for Dungeon Run.

Read:
- AGENTS.md
- docs/ART_DIRECTION.md
- docs/SHADER_GUIDE.md
- docs/PERFORMANCE_BUDGET.md
- docs/DECISIONS.md

Use Unity MCP as the preferred interface for structured Unity Editor operations.
Use visual computer interaction when visual inspection, Shader Graph work, camera composition, or an operation not conveniently exposed through MCP makes it useful.

Do not rely on GUI automation for operations that Unity MCP can perform more reliably.

Before modifying anything:
1. Inspect the current project and open scene.
2. Determine Unity version, render pipeline, active renderer, camera, lighting and Console state.
3. Inspect the available test geometry.
4. Preserve the current production scene.

Use the existing `SceneVictorLab` scene.

Do not create another lab scene.
Do not modify `SceneVictor` during this milestone.

GOAL:
Validate Dungeon Run's first rendering direction: stylized 3D with strong card-battler readability that reads like a pixel illustration, without copying proprietary assets or exact designs from Slay the Spire.

PHASE 1 — Stylized lighting
Create a reusable stylized lit shader/material with:
- 3–4 lighting bands;
- configurable tonal colors;
- strong readable silhouettes;
- optional subtle rim lighting.

Test on:
- rounded geometry;
- hard-surface geometry;
- a humanoid/character-like mesh if available.

PHASE 2 — Pixel rendering
Create a configurable fullscreen/world pixel-rendering prototype.
Start around 480x270 virtual resolution at 16:9.
Keep UI/text separable from the world effect.

PHASE 3 — Color + outline
Add:
- moderate configurable color quantization;
- a simple selective outline focused on combatants.

Do not add Matika distortion, chromatic aberration, complex VFX or final polish yet.

CAMERA:
Create a restrained 2.5D combat framing.
Test perspective and/or orthographic alternatives if useful.
Prioritize combat readability.

VISUAL VALIDATION:
Keep reproducible states:
A — Standard/base URP
B — Stylized lighting
C — Stylized + pixel rendering
D — Stylized + pixel + outline

For each meaningful stage:
- compile;
- inspect Console;
- render/capture the same framing;
- visually analyze;
- identify concrete problems;
- correct;
- capture again.

Also test the final current stack at:
- close;
- normal combat distance;
- far/small-enemy distance.

Do not declare completion without rendered visual inspection.

At the end report:
- assets created/modified;
- renderer changes;
- recommended current parameter values;
- visual observations from A/B comparison;
- Console state;
- performance concerns;
- the single best next visual milestone.
```

## 2. Claude — Technical Review of Astra Rendering

```text
Review the current Dungeon Run rendering implementation.

Read:
- AGENTS.md
- CLAUDE.md
- docs/SHADER_GUIDE.md
- docs/PERFORMANCE_BUDGET.md
- docs/ART_DIRECTION.md

Do not redesign the approved visual appearance.

Review:
- URP compatibility;
- renderer feature architecture;
- shader performance;
- unnecessary fullscreen passes;
- texture/screen samples;
- allocations;
- variant growth;
- maintainability;
- platform assumptions;
- separation between world rendering and UI.

Inspect Unity Console and relevant project files.

First report findings ranked by severity.
Do not make a large rewrite unless I explicitly ask you to implement the fixes.
```

## 3. Astra — UI Milestone

```text
Implement the requested Dungeon Run UI milestone.

Read:
- AGENTS.md
- docs/ART_DIRECTION.md
- docs/UI_DESIGN_SYSTEM.md
- docs/GAME_DESIGN.md

Use SceneVictorLab or an isolated development setup before altering production screens.

Preserve:
- 5-card hand readability;
- exactly 2 visible action slots;
- enemy intent readability;
- crisp text outside destructive world pixel processing.

After implementation:
- render Game View;
- inspect 1920x1080;
- inspect 2560x1440;
- inspect one different aspect ratio;
- correct clipping, hierarchy, spacing and readability;
- capture again.

Do not declare completion without visual verification.
```

## 4. Claude — Combat Core Milestone

```text
Implement the requested combat-core milestone.

Read:
- AGENTS.md
- CLAUDE.md
- docs/GAME_DESIGN.md
- docs/PROJECT_ARCHITECTURE.md
- docs/TESTING_GUIDE.md
- docs/DECISIONS.md

Keep combat deterministic and testable outside MonoBehaviour lifecycle.

Do not invent answers for TO VALIDATE design decisions.
If the requested feature depends on one, isolate the decision behind data/configuration where practical.

Implement the smallest coherent domain slice.
Add meaningful EditMode tests.
Run the relevant tests.
Inspect Unity Console.
Review git diff.
Report remaining design assumptions explicitly.
```
