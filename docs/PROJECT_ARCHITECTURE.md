# Project Architecture — Dungeon Run

## Objective

Keep game rules deterministic, testable and independent from presentation.

Unity should present the game, not secretly define its rules through scene timing.

## Proposed layers

```text
Domain
  |
  +-- Combat
  +-- Cards
  +-- Enemies
  +-- Dungeon
  +-- Run/Progression
  +-- RNG
  |
Application
  |
  +-- Commands / use cases
  +-- State transitions
  +-- Save orchestration
  |
Unity Presentation
  |
  +-- Views
  +-- Animations
  +-- Camera
  +-- VFX
  +-- Audio
  +-- UI
  |
Content/Data
  |
  +-- ScriptableObject definitions
```

## Domain layer

Plain C# where practical.

Suggested state:

- `RunState`
- `BattleState`
- `PlayerState`
- `EnemyState`
- `DeckState`
- `HandState`
- `StatusEffectState`
- `DungeonState`
- `RngState`

Suggested services/resolvers:

- `TurnResolver`
- `DamageResolver`
- `CardResolver`
- `EnemyPatternResolver`
- `EncounterResolver`
- `DungeonGenerator`
- `RewardResolver`

No domain rule should require a camera, Animator, particle system or scene object.

## Data definitions

Content can be data-driven.

Suggested definitions:

- `CardDefinition`
- `EnemyDefinition`
- `EnemyPatternDefinition`
- `EncounterDefinition`
- `RoomDefinition`
- `EventDefinition`
- `RelicDefinition`
- `UpgradeDefinition`

Definitions describe content.
Runtime state tracks mutable run data.

Do not mutate ScriptableObject assets as runtime state.

## Battle events

Domain resolution should emit semantic events such as:

- `CardPlayed`
- `AttackDeclared`
- `DamageApplied`
- `Blocked`
- `Dodged`
- `Healed`
- `EnemyActionRevealed`
- `EnemyDefeated`
- `TurnEnded`

Presentation consumes these events to sequence animation/VFX/audio.

## Deterministic RNG

Core roguelike randomness should accept an explicit seed/state.

Examples:

- dungeon generation;
- encounter selection;
- variable enemy actions;
- reward generation;
- event outcomes.

Avoid `UnityEngine.Random` spread throughout gameplay code.

Wrap randomness behind a deterministic service.

## Suggested folders

```text
Assets/Game/
├── Core/
│   ├── Domain/
│   ├── Application/
│   └── Infrastructure/
├── Combat/
├── Cards/
├── Enemies/
├── Dungeon/
├── Progression/
├── UI/
├── Art/
│   ├── Materials/
│   ├── Shaders/
│   ├── Models/
│   └── Textures/
├── VFX/
├── Audio/
├── Scenes/
│   ├── Production/
│   └── Development/
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

Follow existing project organization if it is already coherent; do not reorganize the repository solely to match this example.

## Scene policy

Canonical scenes:

- `SceneVictor` — main project scene.
- `SceneVictorLab` — shared experimental / validation scene.

Use `SceneVictorLab` for shader, UI, camera, lighting, VFX, rendering, and prototype work.

Do not create additional lab scenes unless explicitly requested.

Experimental work should be validated in `SceneVictorLab` before approved changes are integrated into `SceneVictor`.

## MonoBehaviour policy

MonoBehaviours should:

- bridge Unity and domain/application layers;
- manage scene references;
- present state;
- receive player input.

They should not become giant containers for unrelated game rules.

## Save/load

Persist serializable run state, not live MonoBehaviour references.

Version save data explicitly once persistence is implemented.

## Dependency direction

Domain must not depend on Unity presentation.

Presentation may depend on domain-facing interfaces and immutable content definitions.
