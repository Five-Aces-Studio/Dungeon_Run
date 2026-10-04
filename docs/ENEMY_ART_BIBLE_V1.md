# Dungeon Run Enemy Art Bible V1

Production scope: **Snake Soldier, Snake Sentinel, Snake Chieftain** only. The user approved these representatives and a long serpentine lower body instead of legs on 25 September 2026. These are the Basic, Elite and Boss samples respectively. Stop before full-roster production until the rendered slice is accepted.

## Source authority

1. Current user decisions: acrylic-painted 3D, subtle renderer-produced pixel character, approved serpentine tails, three-character slice.
2. Current digital decisions and recent meeting documents.
3. `docsDungeonRun/DungeonRun_Stuff_Printable.pdf`, one-based pages 7, 9, 11; paired reverse pages 8, 10, 12 establish classifications.
4. Guide and other tabletop documents.
5. Existing Unity placeholders: scale/composition reference only, never enemy identity.

`docs/ART_DIRECTION.md` retains older pixel-illustration wording. The current explicit acrylic-first brief supersedes that emphasis for this milestone. Preserve the existing shader and world setup rather than rewriting it.

## Official roster audit

The three front pages contain **24 printed enemy cards, 11 unique designs and 13 duplicate instances**. Numbers below are source-card HP/action entries, not proposed digital balance changes. Apparent roles are interpretations of visible equipment and printed actions, not new classes or lore.

| Enemy | Front / reverse | Copies | Classification | HP | Visible design, equipment and silhouette | Apparent role / printed actions 1–6 |
|---|---|---:|---|---:|---|---|
| Snake Hemomancer | 7 / 8 | 2 | CONFIRMED_ELITE | 7 | Narrow elongated snake head, green skin, yellow eyes, upright blade, belts and metal wrist details; slim vertical silhouette | Sustain/counter duelist: Attack, Dodge, Piercing Attack, Heal Self, Counter Attack, Lifesteal |
| Snake Mercenary | 7 / 8 | 2 | CONFIRMED_ELITE | 10 | Broad cobra-like head, green musculature, brown leather shoulder equipment, sword and round metal shield | Durable fighter: Attack, Attack, Piercing Attack, Miss, Defence, Defence |
| Snake Phantom | 7 / 8 | 2 | CONFIRMED_ELITE | 6 | Narrow long head with stacked yellow eye motif, angular shoulders, crossed long blades, metal straps | Evasive duelist: Attack, Attack, Piercing Attack, Dodge, Dodge, Dodge |
| Snake Chieftain | 7 / 8 | 1 | CONFIRMED_BOSS | 16 | Three green heads; each has one horizontal yellow eye patch with three pupils. Twin curved silver sabers, gold bracers, red sash, belt, triangular pendant; broad three-headed crown silhouette | Leader/multi-hit fighter: Piercing Attack, Piercing Attack, Combo, Attack, Heal Self, Miss |
| Snake Sentinel | 9 / 10 | 2 | CONFIRMED_ELITE | 8 | Wide angular cobra head, two side eyes and vertical central eye, silver breastplate with dark eye emblem, silver bracers, long curved glaive | Reach/piercing fighter: Piercing Attack, Miss, Dodge, Attack, Attack, Attack |
| Snake Guard | 9 / 10 | 2 | CONFIRMED_BASIC | 7 | Helmet, straight sword, large angular shield with purple eye mark, purple shoulder motif; shield-dominant silhouette | Defensive fighter: Defence, Defence, Attack, Attack, Miss, Heal Self |
| Snake Assassin | 9 / 10 | 3 | CONFIRMED_BASIC | 4 | Angular head with vertically stacked eyes, two small curved daggers, diagonal chest strap, brown forearm equipment | Evasive fighter: Attack, Attack, Attack, Miss, Dodge, Dodge |
| Snake Partizan | 9 / 10 | 2 | CONFIRMED_BASIC | 5 | Narrow head, orange brow marking, scarred green chest, short blade and dark spherical explosive | Explosive fighter: Attack, Attack, Miss, Miss, Explosion, Explosion |
| Snake Madman | 9 / 10 | 3 | CONFIRMED_BASIC | 5 | Bandaged head/torso/arms, thin angular face, yellow eye-like chest motif; ragged asymmetry | Unstable sustain fighter: Attack, Attack, Attack, Miss, Miss, Heal Self |
| Snake Soldier | 9, 11 / 10, 12 | 3 | CONFIRMED_BASIC | 6 | Elongated head, three yellow slit eyes asymmetrically arranged (two on image-left), brown single shoulder armor and bracers, diagonal strap, wrapped poleaxe shaft | Basic melee: Attack, Attack, Attack, Miss, Miss, Miss |
| Snake Healer | 9 / 10 | 2 | CONFIRMED_BASIC | 4 | Broad hood/cobra silhouette, cyan forehead accent, blue-topped staff, engraved wrist band | Support: Heal Other, Heal Other, Heal Other, Heal Self, Attack, Miss |

### Classification evidence and uncertainty

- Reverse layouts pair by **vertical page reflection**, not identical row position. This matters on page 10: its Elite reverses correspond to **Sentinel**, not Guard.
- All eleven printed classifications are established by the paired backs. Digital reclassification has not been authorized.
- Guide pages 2–3 identify a three-headed Hydra guarding the Tetra Head; the cards name their boss Snake Chieftain. Do not silently assert those names are interchangeable or invent separate production lore.
- Most artwork ends near the waist. Full tail anatomy, rear costume, undersides and exact weapon thickness are 3D interpretations. The tail choice is now explicitly user-approved.
- Purple/red snake symbols in meeting discussion refer to encounter classification cues; do not recolor the illustrated character eyes wholesale. Preserve the actual reference eyes.
- These are character-art deliverables. Printed action lists do not authorize changes to current rulesets, encounter composition or boss mechanics.

## Shared visual language

| Area | Production rule |
|---|---|
| Proportions | Serpentine head and neck flow into a stylized powerful torso; two arms, no human legs; tapered curved tail supports the body. Avoid a human mannequin with a snake mask. |
| Heads | Prioritize brow, cheek, muzzle, mouth and neck transitions. Preserve Soldier's asymmetric three eyes, Sentinel's central vertical eye and Chieftain's three separate heads. |
| Eyes | Broad readable yellow shapes, dark slit pupils where shown; restrained highlight, no default emissive glow. Chieftain has three pupil marks per eye patch. |
| Skin | Green family with controlled warm/cool/value variation; broad planes and sparse large scale accents. No pore detail or thousands of tiny scales. |
| Hands | Readable stylized digits and real grip positions; weapons attach at the grip, never bend with skin. |
| Tail | Generous taper and clear negative space; no stacked spheres or voxel segments. Smooth ring-based deformation topology, enough curvature to support silhouette at distance. |
| Costume | Soldier leather asymmetry; Sentinel metal chest/forearms; Chieftain gold bracers, red sash and pendant. Distinguish materials by value as well as hue. |
| Weapons | Strong controlled exaggeration of the source shape: Soldier poleaxe, Sentinel curved glaive, Chieftain dual sabers. Preserve recognizable tips and negative space. |
| Armor | Visible thickness and shaped edges; near-rigid deformation. Never one material per buckle/plate. |
| Cloth | Broad folds, simple bone-driven preparation; no cloth simulation in this slice. |
| Detail density | Head, hands, weapons and outer silhouette first. Avoid details below useful gameplay projection. No geometry added merely to reach a budget. |
| Texture | Clean base-color regions and authored UVs; acrylic brush treatment comes from the existing renderer. Technical palette atlases are not a substitute for a final hand-painted texture pass and must be reported honestly. |
| Scale | Metric source, scale one, floor origin. Existing Lab prototypes are roughly 2–3 m tall; this is composition evidence, not canonical biological height. Start near 3.0/3.2/3.6 m for Soldier/Sentinel/Chieftain and validate in the current camera. |

## Technical contract

- Keep Blender source outside `Assets`: `docsBlender/EnemyRosterV1/`.
- Exports/prefabs: `Assets/Art/Characters/Enemies/<Basic|Elite|Boss>/<EnemyName>/`.
- Names: `CHR_<EnemyName>.fbx`, `Rig_<EnemyName>`, meaningful modular mesh names with `_LOD0`, `_LOD1`, `_LOD2`, and `PF_<EnemyName>.prefab`.
- Generic rig: Root, spine/chest, neck/head/jaw, arms/hands, tail chain; Chieftain extends the head chains without forcing Humanoid retargeting.
- Separate weapon meshes, grip pivots and Main/OffHand sockets. Add only useful head/chest/weapon-tip/ground VFX sockets and target/damage/intent/center anchors.
- Source objects have clean transforms; skinned vertices have normalized bounded influences. Armor remains rigid or near-rigid.
- LODs target approximately 100%, 45–60%, 15–30%, with visual checks for silhouette and deformation. Triangle counts are measured, not asserted from design targets.
- Target LOD0 ranges: Basic 8–15k, Elite 15–25k, Boss 25–45k triangles; fewer is acceptable when shape quality is retained.
- Primary texture targets: Basic 1024, Elite 1024–2048, Boss 2048 only when needed. No automatic 4K assets.
- Material budgets: approximately 1–2 Basic, 1–3 Elite, 2–4 Boss. Reuse compatible families; do not modify existing shared material assets.
- Collider: primitive capsule/box/sphere independent of skin topology. No detailed animated MeshCollider.
- Validation clips: Idle, Attack, HitReaction, Death. Additional pose checks must cover head turn, jaw, arm raise, weapon pivot and tail bend. These are test motions, not final combat choreography.

## Unity integration boundary

Reuse `DungeonRun/SH_DungeonRun_StylizedLit` and current AcrylicV3 material architecture. UV0 brush mapping is available and should be tested for skinned stability: reconstructed object-space coordinates can still move during deformation. The shader currently does not expose a normal-map input; do not introduce a new shader just to satisfy a generic texture checklist.

The existing V4 combat adapter associates each enemy with **one Renderer**, and its defeat presenter transforms that Renderer. This is not a complete modular-character contract. Keep the slice under removable `EnemyArtReview_V1`; do not swap one skinned child into the accepted battle and leave its weapons/body behind. Live integration is a later explicit presentation-adapter step, not a domain rewrite.

Preserve SceneVictor, existing Lab combatants, cameras, HUD V4, current renderer profile and material/shader files. No new test scene. No automatic commits or full-roster generation.

## Acceptance gate

For all three candidates, inspect front, 3/4, side, back and gameplay-distance color/silhouette captures. Test rig deformation, all LODs, primitive colliders and three enemies plus player under the actual acrylic stack. Record actual triangles, bones, renderers, materials, texture memory, clips, Console state and remaining defects in the slice report. Do not label a procedural candidate production-approved merely because it imports successfully.

## Interactive Blender checkpoint — 2026-09-25

**Status: rigged blockouts, NOT production-ready.** The open Blender 5.2.1 session now displays `EnemyRosterV1_Review` with all three candidates. The user's original empty `Scene` and unsaved document path were preserved. Source library writes do not replace the active document.

Sources: `docsBlender/EnemyRosterV1/{SnakeSoldier,SnakeSentinel,SnakeChieftain,EnemyRosterV1_Review}.blend`. Builder: `build_enemy_slice.py` in that directory. Each character has Body, Head, Arms, Armor, Details and separate Weapon meshes; four pose-validation actions (Idle, Attack, HitReaction, Death), and three mesh LODs.

| Candidate | LOD0 / LOD1 / LOD2 triangles | Rig bones | Meshes per LOD | Materials |
|---|---:|---:|---:|---:|
| Soldier | 12,908 / 6,454 / 3,208 | 26 | 6 | 2 |
| Sentinel | 12,608 / 6,304 / 3,146 | 26 | 6 | 2 |
| Chieftain | 17,540 / 8,770 / 4,356 | 32 | 7 | 2 |

All use a 256x128 technical color palette, not final painted textures. Structural checks on all 57 meshes found no unweighted/non-normalized vertices and no missing bone references. This does not establish deformation quality. Jaw bones currently move tongue geometry, not an articulated jaw surface.

Actual Cycles captures are under `Captures/EnemyRosterV1`: lineup, Soldier before/after correction, and front/side/back/Attack frame 12 for each character. Inspected lineup plus side/back/attack samples. Corrected tube-frame flips that pinched the tail, partial eye occlusion, invalid five-dimensional mathutils interpolation, and LOD modifier ordering. Preserved first Soldier pass in a separate in-memory scene for comparison.

**Next corrections before export:** continuous shoulder/neck/torso-tail transitions; more faithful head planes and mouth; Soldier shoulder-side correspondence; real closed hand grips; less segmented straps/sash; avoid palette-index shading crossing into another color family; richer authored material/skin treatment. Pose candidates need full stress testing, especially shoulders, necks, jaw, tail and death/floor intersection. LOD decimation still needs visual acceptance.

**Unity checkpoint:** editor importer compiles and its type resolves. Console query returned no errors/warnings; Lab remained clean Edit Mode. No FBX, prefab, collider or Unity visual acceptance yet. Production export is explicitly gated in the builder. Before enabling it, isolate each rig's four actions (do not export all compatible actions across the file), map per-character Blender material names robustly, and validate axes/unit scale in Unity. No live combat replacement was performed. All 384 protected baseline hashes matched, including SceneVictor, HUD/rendering/gameplay sources and settings. Existing unrelated Git changes were preserved; whole-repository diff check reports pre-existing Unity YAML trailing whitespace, not a clean global diff.

Rollback boundary: the new enemy-only source, palette and importer files; no combat or accepted world changes belong to this checkpoint. No commit/staging/push. Review mode remains disabled/unmanaged. Full-roster production remains gated.

## V1.1 R4 checkpoint — 2026-09-30

**Status: imported review candidates, NOT production-ready.** Only Soldier, Sentinel and Chieftain were advanced, using the connected Blender and Unity MCP sessions. The live combat roster has not been replaced.

- Corrected torso-tail frame rotation and shoulder loft ring correspondence; added curled grip geometry and articulated jaw surfaces. Extreme overhead deformation still fails acceptance.
- Baked 1024px color atlases with corrected linear/sRGB handling. These are procedural color candidates, not finished painted textures; jagged region boundaries remain visible.
- Imported scoped Idle, Attack, HitReaction and Death clips, three LODs, materials, controllers and prefabs. Unity triangle counts: Soldier 10462/5224/2611; Sentinel 10192/5094/2542; Chieftain 15180/7582/3782.
- Corrected capture slot lookup and verified yaw 0 against actual imported geometry. Inspected actual combat-camera Idle and Attack renders: `Captures/EnemyRosterV11/r4-front-{idle,attack}-0930.png`. HUD exclusion is intentional; accepted bindings remain untouched. These are Edit Mode sampled captures, not Play Mode validation.
- Disabled Blender Preserve Volume on the 45 current skinned mesh modifiers so stress checks use linear skinning. Saved separate post-bake sources in `docsBlender/EnemyRosterV1/V11/Linear0930/`; earlier revisions remain available. Inspected linear overhead capture; shoulder stretching remains unacceptable.
- Latest Unity Console query returned no errors/warnings after script compilation. An earlier Package Manager token HTTP 400 was observed. No automated gameplay tests or Play Mode checks were run.
- The isolated Lab review root remains unsaved in Editor memory. No SceneVictor save, staging, commit or push was performed.

The old 384-file protection baseline is no longer a current-session baseline: 12 HUD-related files differ; do not overwrite them or claim all protected hashes still match. SceneVictor and the other 371 files match that historical baseline.

Next: repair shoulder weights/topology under linear skinning, clean atlas boundaries, complete tail/jaw/death and LOD silhouette checks, then validate animation playback in Unity before production approval. Keep the review prefabs isolated from gameplay until the modular-renderer presentation contract is addressed.
