# Art Direction — Dungeon Run

## Core visual statement

**A dark fantasy card-battler presented as a stylized 3D diorama that reads like a hand-authored pixel illustration.**

The visual result should feel deliberate and graphic, not like ordinary 3D with a mosaic filter placed on top.

## Reference intent

Slay the Spire is a reference for:

- combat readability;
- strong character silhouettes;
- clear separation between combatants and background;
- illustrated/card-game staging;
- fast visual comprehension.

Do not copy:

- characters;
- UI layouts verbatim;
- card frames;
- icons;
- proprietary artwork;
- exact color palettes.

Dungeon Run must develop its own identity around:

- triangular geometry;
- the Trigonal Abyss;
- Pascala;
- Matika;
- reality instability;
- the Tetra Head;
- dark fantasy.

## Visual priorities

When goals conflict:

1. Gameplay readability.
2. Silhouette clarity.
3. Art-direction consistency.
4. Card/UI readability.
5. Atmosphere.
6. Detail.

## 2.5D philosophy

Production geometry may be fully 3D.

The 2.5D feeling should come from:

- controlled combat cameras;
- compressed/illustrative perspective;
- stylized materials;
- pixel-stable rendering;
- restricted lighting bands;
- selective outlines;
- controlled animation staging;
- UI layered independently from world pixel rendering.

## Shape language

### Dungeon

Prefer:

- triangular motifs;
- repeating geometry linked to the Trigonal Abyss;
- large readable masses;
- fewer noisy micro-details.

### Enemies

Each enemy type must be identifiable primarily by silhouette and posture.

### Matika

Matika may break established visual rules intentionally:

- pixel grid offsets;
- palette instability;
- fractured geometry motifs;
- controlled dithering changes;
- selective distortion.

Matika effects should look like reality breaking, not generic cyberpunk glitch.

## Color

Exact palette is not locked yet.

General hierarchy:

`background < environment < combatants < interaction/critical information`

Do not make every object saturated or emissive.

Reserve strong accents for:

- player actions;
- enemy intent;
- elites/boss;
- Matika;
- critical UI.

## Lighting

Prefer stylized, deliberate lighting.

Goals:

- readable silhouettes;
- clear focal points;
- limited tonal bands;
- controlled shadows;
- restrained bloom/emission.

Dark fantasy must remain readable.

## Materials

Prefer reusable material families.

A material should still look intentional when the fullscreen pixel effect is disabled.

This is a key quality check.

## Camera

Default combat camera should feel staged rather than like third-person exploration.

Prototype:

- narrow perspective FOV or orthographic alternatives;
- stable combat framing;
- strong left/right or near/far readability;
- limited camera movement during decision-making.

Use alternate cameras for:

- attacks;
- boss moments;
- Matika events;

but return quickly to a readable tactical view.

## Animation

Combat animation should communicate timing and action ownership.

Prefer:

- readable anticipation;
- fast execution;
- clear impact;
- short recovery.

Do not let animation delay turn-based input unnecessarily.

## Anti-goals

Avoid:

- generic realistic PBR;
- uncontrolled bloom;
- excessive screen-space noise;
- outlines around every environment edge;
- unreadable darkness;
- constant camera rotation;
- post-processing that harms cards/text;
- "pixelate everything" as the only style.

## Scene workflow

Use `SceneVictorLab` to test and approve visual direction.

Only integrate approved visual work into `SceneVictor` when explicitly requested or when the current task is the integration step.

## Pixel-aware 3D authoring guideline

**Design important features around roughly 2 virtual pixels in the tested pose, then verify their actual facets, negative space and motion. Do not apply a universal 3-pixel minimum.**

These findings are scoped to `SceneVictorLab`, its FOV 34 combat camera, and the unchanged C480 renderer. They are not universal pixel-art rules or a guarantee of temporal stability.

| Representative content | Evidence and authoring implication |
|---|---|
| Chains | 23 larger links with 0.165 m wire preserve openings better than thickening the original 35 links to approximately 3 pixels. Sampled facet cross-sections span approximately 1.92–2.59 virtual pixels in maximum projected diameter; this is not a minimum thickness at every orientation. Preserve negative space as well as wire visibility. |
| Player staff | The 0.121 m candidate measures approximately 1.90 pixels horizontally at the sampled shaft bottom and 2.02 pixels at the terminal. A nonzero terminal avoids a taper to nothing. The larger 3-pixel candidate looks blunter and still changes shape during motion: thickness alone is not a solution. |
| Paving | Reducing 110 slabs to 35 and removing 0.025 m bevels and corner chips reduces fine-frequency noise while retaining 3D slabs and the trigonal inlay. Major joints still step across the grid. |

Simplify or omit decorative details that remain below 1 virtual pixel at their intended depth. Measure projected geometry rather than relying only on nominal cylinder or torus diameter; validate translation and representative orientation changes before accepting a variant.

Do not enlarge already-readable-width legs or shield interiors to solve dark overlap. Check silhouette/background value separation, contact shadows and internal contrast independently. This content experiment introduces no new effects and does not authorize renderer changes.

See [Pixel-aware Content Robustness V1](../docsBlender/TrigonalAbyss/PIXEL_CONTENT_ROBUSTNESS_V1.md) for comparisons, measurements, limitations and the final retained lab selection.

## Palette hierarchy validated in the lab

SceneVictorLab's Palette V1 selects P2: smooth authored color-family grouping after C480, preserving source linear luminance rather than adding another lighting-band quantizer. Desaturate distant architecture more than the combat plane; retain distinct cool stone, earthy clay, jade, muted bone, cloth and restrained magical accents. Warm light remains locally warm, not universally converted to blue shadows.

The matched comparison supports a modest gain in color coherence and reduced background competition, not a claim of finished illustrated art. Preserve Character Value V2 first: hue is not a substitute for shield/body/leg value separation. Keep Matika-specific disruption reserved. Native-resolution overlay UI must remain outside world palette/pixel treatment.

These findings are scoped to the canonical C480 lab framing. Depth-based chroma attenuation is not semantic interactable tagging and must be reevaluated for new staging. Existing contact-shadow foot merging remains an accepted prototype limitation.

See [Color / Palette Treatment V1](../docsBlender/TrigonalAbyss/COLOR_PALETTE_TREATMENT_V1.md) for P0/P1/P2 comparisons, parameters, rollback, motion/display checks and rendering-cost limitations.
