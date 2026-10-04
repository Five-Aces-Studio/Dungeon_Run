# Shader & Rendering Guide — Dungeon Run

## Goal

Build a distinctive 3D-to-2.5D pixel rendering style for Dungeon Run.

The target is **stylized 3D that reads like an authored pixel illustration**, not a generic full-screen pixelation filter.

## Expected pipeline

The current prototype direction assumes Unity URP.

Before implementing any rendering feature, inspect the actual project:

- Unity version;
- active render pipeline;
- active URP renderer;
- render scale;
- anti-aliasing;
- post-processing;
- camera stack.

If the project is not URP, do not blindly implement URP-specific code.

## Lab scene convention

All shader and rendering experiments must use `SceneVictorLab`.

`SceneVictor` is the main scene and should remain untouched during experimentation unless the user explicitly requests integration.

Do not create another shader-specific lab scene.

## Rendering stack

Build the look in layers:

### Layer 1 — Stylized material lighting

Create a reusable stylized lit material/shader.

Start with:

- 3–4 lighting bands;
- controlled shadow/midtone/light colors;
- optional subtle rim;
- strong silhouettes;
- readable normals.

A material must still look intentional before fullscreen pixel rendering.

### Layer 2 — Pixel rendering

Start around a virtual 16:9 resolution of:

**480 × 270**

Test alternatives:

- 320×180: aggressive;
- 480×270: baseline;
- 640×360: cleaner;
- 960×540: subtle.

The pixel grid must be stable under camera movement.

Prefer point/nearest-style final sampling where appropriate.

### Layer 3 — Color treatment

Add configurable color quantization only after the lighting works.

Start around a moderate level and A/B test.

Do not crush important gradients/silhouettes solely to increase the "pixel" impression.

### Layer 4 — Outlines

Prefer selective outlines.

Priority:

- characters;
- enemies;
- important interactables.

Environment outlines should be lighter or absent unless the art direction proves otherwise.

### Layer 5 — Dithering

Dithering is optional.

Use sparingly to support limited tones.

Do not cover the entire screen in noisy patterns.

### Layer 6 — Matika

Only after the base look is stable.

Matika may modulate:

- pixel-grid stability;
- palette;
- dither;
- outline behavior;
- localized UV distortion;
- lighting bands.

Avoid generic RGB-split glitch as the primary identity.

## UI rule

World pixel rendering must not destroy card or text readability.

Keep UI rendering separable from the world post-process.

## Anti-aliasing

During early pixel-art testing, compare with post AA disabled.

Do not assume the final setting until pixel stability is evaluated.

## Camera tests

Shader work must be tested at:

- close;
- normal combat distance;
- far/small-enemy distance.

A shader that looks good only in close-up fails the game.

## Geometry tests

`SceneVictorLab` should contain at least:

- sphere/rounded geometry;
- cube/hard-surface geometry;
- humanoid/character-like mesh;
- floor/wall/environment;
- multiple depth layers.

## Lighting tests

Test:

- primary combat lighting;
- dark environment;
- brighter environment;
- front/side/back light where relevant.

## HLSL vs Shader Graph

Use Shader Graph when:

- iteration speed matters;
- the effect is naturally graph-based;
- visual editing helps.

Use HLSL/custom functions when:

- precise control is required;
- graph complexity becomes unmanageable;
- performance/maintainability improves.

Do not choose lower-level code merely because it looks more advanced.

## Performance rules

Be cautious with:

- extra full-screen passes;
- transparency;
- overdraw;
- repeated screen-color sampling;
- expensive procedural per-pixel noise;
- excessive branches;
- uncontrolled shader variants.

See `PERFORMANCE_BUDGET.md`.

## Visual quality gate

A shader milestone is complete only after:

1. compilation;
2. clean relevant Console state;
3. application to representative geometry;
4. Game View capture;
5. visual inspection;
6. A/B comparison;
7. correction of obvious artifacts;
8. final capture.

## First milestone

Do only:

1. stylized lighting;
2. 480×270 pixel render baseline;
3. color quantization;
4. simple selective outline;
5. A/B captures.

Do **not** add Matika distortion, chromatic aberration, complex VFX or final polish in the first milestone.
