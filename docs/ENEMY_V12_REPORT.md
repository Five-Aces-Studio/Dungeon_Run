# Enemy Vertical Slice V1.2 — Snake Soldier, Sentinel, Chieftain

V1.2 rebuilds the three representative snake enemies on the approved plan:

- **Shoulders:** fixed at the root cause (joint position, topology, continuous weights and clavicle).
- **Art:** raised to match the acrylic world (models, textures and animations), following the printable cards (pp. 7, 9, 11).
- **Textures:** edge artifacts solved at the source.
- **Size:** a tunable hierarchy, bigger than the protagonist.

**Status: ready for review as production references. The rest of the roster was NOT started.**

## Quick path

1. Open `SceneVictorLab`. `EnemyArtReview_V1` holds the three updated prefabs, offstage at x ≈ 30.
2. To tune size, select a prefab and edit `EnemyVisualScale` → **Visual Scale** in the Inspector. The prefab root stays at scale 1.
3. Evidence is in `Captures/EnemyRosterV12/`:
   - `before/` and `after/`: shoulder test poses;
   - `unity/`: combat camera, close-ups, LODs, mipmaps;
   - `shoulder_before_after.png`.
4. Rebuild from source (Blender MCP):
   1. `build_enemy_v12.build_character(name, rebuild=True)`
   2. `bake_color(name)`
   3. `build_lods(name)`
   4. `export_fbx(name)`
   5. `save_source(name)`

   Then run `Dungeon Run/Enemy Art V1/Import Representative Assets` in Unity.

## Verification

| Check | Result |
|---|---|
| Unity Console (import, captures, Play) | 0 errors, 0 warnings |
| EditMode tests | 385/385 (+7 `EnemyVisualScaleRules`, +6 `WorldStyleGate`) |
| Prefabs (Animator, avatar, 3 LODs, null bones, anchors, collider, weapon sockets) | All valid on all three |
| SceneVictor | Untouched by enemy work. The only change is the approved removal of `EnemyArtReview_V1`; checkpoint at `docsBlender/EnemyRosterV1/V12/SceneVictor_PreReviewRemoval.unity.bak` |
| Blender deformation | Linear skinning only (`use_deform_preserve_volume = False` on every armature modifier) |

---

## 1. Shoulder root cause (V1.1)

It was a combination of four causes, not weights alone:

- **Bone placement:** `UpperArm` pivoted at x = 0.485, on the torso skin rather than inside the deltoid mass. Raising the arm swung the socket rim around a surface point, so the shoulder top sank into the torso and the armpit tore away.
- **Weights:** the socket rim was hard-set to Chest 0.65 / Clavicle 0.35 with 0 UpperArm. The whole transition happened across only two arm loops, and the torso-side deltoid and trapezius had no clavicle influence.
- **Topology:** a rectangular 3×8 hole was cut into the cylinder, with no radial loops.
- **Animation:** Attack rotated the upper arm up to about 60° with 0% clavicle.

## 2. Shoulder topology changes

- The body is one continuous 16-sided loft from tail tip to neck.
- The socket is a 3×4 face opening whose 14-vertex rim is **re-projected onto an ellipse** perpendicular to the humerus, with even angular spacing.
- The arm loops start at the socket with deltoid "lift" loops: 3 loops within 0.23 m of the socket, with a bulge biased upward.

## 3. Bone-placement changes

- **Humerus pivot:** moved inside the deltoid mass, to (±0.43, 0.01, 1.97); ±0.49 on the Chieftain.
- **Clavicle:** now runs from the sternoclavicular point (±0.10, −0.02, 2.10) to the pivot. It is a short lever that carries the shoulder mass.
- **Naming:** unchanged, except that the weapon sockets now follow the standard `MainHand` (right) and `OffHand` (left), with `VFX_WeaponTip_Main` and `VFX_WeaponTip_Off`.

## 4. Weight changes

Torso, socket and arm weights are **one continuous spatial field** (`shoulder_field` and `arm_field`):

- **Axial chain:** COG → Spine → Chest → Neck.
- **Clavicle:** weighted over the deltoid and trapezius region (up to 0.78).
- **Upper arm:** ramps in smoothly along the humerus axis starting just outside the pivot.
- **Forearm and hand:** blended at the elbow and wrist.
- **Neck influence:** removed from the lateral trapezius, so head turns do not drag the shoulder.

Every vertex has at most 4 influences, and there are 0 unweighted vertices.

## 5. Corrective bones or blendshapes

**None.** Joint placement, topology, weights and clavicle participation were enough for the real animation range.

## 6. Clavicle behaviour

The `arm()` pose helper gives the clavicle **25% of the arm elevation, capped at 28°**, in every raise and forward reach. The validation test poses use the same rule (25%, capped at 30°). Idle adds a subtle clavicle breath.

## 7. Attack-pose result

Attack has five phases:

1. anticipation (f14);
2. strike (f20);
3. impact hold (f24);
4. follow-through (f32);
5. recovery (f48).

It includes torso rotation, clavicle, tail counterbalance and COG lunge. Weapons are aimed by direction (a hand-orientation solver), not by guessed Euler angles. The shoulders stay rounded at anticipation and impact in both Blender (linear skinning) and Unity.

## 8. Weapon-grip regression

- **Grip preserved:** the V1.1 grip design (digits wrapping a vertical haft) is kept, and the weapon origin sits at the grip.
- **Aiming:** the aim solver rotates the hand, so palm and haft stay aligned in every key pose.
- **Rigging:** weapons are separate rigid objects, children of `MainHand` and `OffHand`.

## 9. Torso–tail regression

- **Clip range:** clean across Idle, Attack, HitReaction and Death.
- **Stress pose** (tail S-curve ±20° plus 50° body twist): no corkscrew, seam or collapse.
- **Floor contact:** the COG translation is cancelled at `Tail_01`, so the coil stays planted during lunges.

## 10. Texture artifact root cause

The artifact was **not an alpha problem**; the PNG has no alpha. There were two causes:

1. **Per-face colour** from palette cells: the belly and back boundary followed polygon edges, which produced staircase borders.
2. **Black gutters:** the bake cleared to black with an 8 px margin, so bilinear and mip sampling pulled black into the island edges.

A tooling defect also appeared in V1.2: `smart_project` and `pack_islands` silently collapse UVs to zero area when run from a script context. Blender still reported "baked", but the image was empty.

## 11. Texture cleanup

- **Colour shader:** colour is now a per-pixel painted field, evaluated at bind pose. It covers:
  - dorsal, side and belly masses with noise-broken boundaries;
  - dorsal saddles and faint belly scutes;
  - broad value breakup and vertical brush streaks;
  - painted cavity shading (AO) and a warm top light.
- **Bake:** emission to 1024 px. Every unbaked texel is filled with the **nearest island colour**, so no black or foreign colour remains under any mip.
- **Result:** all three textures have 0 near-black texels.
- **Style:** broad masses only, with no pixel grids, dithering or micro-scales.

## 12. UV changes

- Each mesh gets a fresh `BakeUV` layer and its own `smart_project`, then is placed in a fixed square atlas cell (body 0.70, others 0.30) at uniform scale. The layer is then renamed to `UV0`.
- Gutters are at least 18 px.
- A UV area and range check refuses to bake on collapsed UVs.
- Atlas coverage is 13–15%, which gives about 106 texels/m. That is above the on-screen density at combat distance (about 70 px/m at 1080p, about 95 px/m at 1440p).

## 13. Unity import-setting changes

- **Settings:** unchanged from the existing importer (sRGB, no alpha, bilinear, clamp, uncompressed, max 2048, mipmaps on). The texture is now RGB24, 1024².
- **Mipmap comparison** (combat camera, same frame, on vs off): **no measurable difference** (mean Δ 0.005/255, same high-frequency energy). The broad-mass texture has nothing to alias.
- **Mipmaps kept on:** this was the importer's existing setting, costs +33% memory and protects other views. The comparison does not show a visible improvement.

## 14. LOD validation

| | LOD0 | LOD1 | LOD2 |
|---|---|---|---|
| Soldier | 4874 | 2618 | 1402 |
| Sentinel | 4798 | 2548 | 1350 |
| Chieftain | 6564 | 3482 | 1838 |

- In the Unity close review, LOD0, LOD1 and LOD2 are visually indistinguishable.
- Head, weapon, shoulders, armour and tail silhouette are preserved.
- Weapons use gentler ratios (0.8 and 0.55), so they never vanish.

## 15. Final material result

Two materials per enemy (Organic → EnemyJade acrylic, Equipment → CharacterMetal acrylic), sharing one baked texture.

- **Shared family:** desaturated olive/ochre skin, calibrated against the lit world.
- **Soldier:** yellow-olive skin, leather.
- **Sentinel:** cooler green, steel, purple glyph.
- **Chieftain:** deeper green, umber saddles, gold and red.

## 16–18. Validation matrices

| Check | Soldier | Sentinel | Chieftain |
|---|---|---|---|
| Neutral | PASS | PASS | PASS |
| 45° arm | PASS | PASS | PASS |
| 90° arm | PASS | PASS | PASS |
| Attack anticipation | PASS | PASS | PASS |
| Attack impact | PASS | PASS | PASS |
| Idle | PASS | PASS | PASS |
| HitReaction | PASS | PASS | PASS |
| Death | PASS | PASS | PASS |
| Tail bend | PASS | PASS | PASS |
| Weapon grip | PASS | PASS | PASS |
| Texture borders | PASS | PASS | PASS |
| LOD transitions | PASS | PASS | PASS |
| Unity acrylic rendering | PASS | PASS | PASS |

Chieftain note: a 150° lateral overhead brings the forearms very close to the two side heads. That pose is outside its real range; its attack raises the arms forward-up, and 90° is clean. This is recorded as design debt, not a FAIL of the used range.

## 19–21. Counts

| | Bones (deform) | SkinnedMeshRenderers per LOD | Renderers per LOD | Materials | Texture | Forward draw lower bound |
|---|---|---|---|---|---|---|
| Soldier | 33 (22) | 2 | 3 | 2 | 1024² RGB24 + mips | 3 |
| Sentinel | 33 (22) | 2 | 3 | 2 | 1024² RGB24 + mips | 3 |
| Chieftain | 39 | 2 | 4 | 2 | 1024² RGB24 + mips | 4 |

V1.1 had 5–7 renderers per LOD. Merging skin, face, jaw and hands into one skinned mesh lowered the renderer count and the triangle budget.

## 22. Unity prefab validation

Prefab hierarchy:

- `PF_*`: scale 1, with `LODGroup` and `EnemyVisualScale`.
  - `VisualRoot`: the tuned uniform scale, plus the trigger `CapsuleCollider` and the anchors (TargetAnchor, CenterMass, DamageTextAnchor, HUDAnchor, IntentAnchor, VFX_Ground).
    - `Model`: FBX with Animator (Generic, 4 clips).

Details:

- Weapons are children of the `MainHand` and `OffHand` bones.
- No combat definitions are bound.
- Re-imports keep an Inspector-tuned scale.
- The prefab anchors are authoritative. The rig's non-deforming anchor bones remain for the rig standard.

## 23. Acrylic rendering result

- The characters use the existing `SH_DungeonRun_StylizedLit` with `_DR_ACRYLIC` and UV0 brush mapping, rendered by the unchanged world stack: acrylic pass, pixel finish, palette and outlines.
- **Look:** faceted low-poly planes with smooth skin below 40°. This matches the world's chunky low-poly stone and the protagonist.
- **Reference fidelity:** designs follow the cards:
  - Soldier: four eyes, rope-wrapped axe, left pauldron, baldric;
  - Sentinel: cobra hood, third eye, glaive, breastplate with eye glyph;
  - Chieftain: three heads, twin scimitars, gold eye amulet, red belt.

## 24. Combat-camera result

At combat distance:

- **Heads and eyes:** readable.
- **Weapons:** readable.
- **Role and silhouette:** recognisable and distinct.
- **Texture borders:** clean.
- **No wasted detail:** the texture density matches what the camera can show.

Evidence: `unity/v12_combat_*.png`.

## 25. Group composition

With the player, under the current lighting, all three are bigger than the protagonist:

| | Protagonist | Soldier | Sentinel | Chieftain |
|---|---|---|---|---|
| Height | 2.90 m | ≈ 3.8 m | ≈ 4.2 m | ≈ 5.0 m |

- **Scales:** Visual Scale is 1.16 / 1.26 / 1.35. It was tuned from the approved start values (1.15× / 1.30× / 1.50× the protagonist) because the coiled bodies read smaller on screen.
- **Fit:** they fit between the pillars and do not cover the arch.
- **Distinction:** colours stay distinct (leather / steel / gold and red), and the scene is not overloaded.
- **Facing:** the model front is local −Z in Unity. Yaw +53° faces the player and the camera; yaw +90° faces the player in profile.

## 26. Console

0 errors, 0 warnings.

## 27. SceneVictor preservation

- Untouched by the enemy work; no enemy asset or prefab is referenced.
- The only edit was the approved removal of `EnemyArtReview_V1`, done after checking that no production object depended on it.
- A checkpoint was saved before the removal.

## 28. Remaining art debt

- Validation clips are not final choreography. Timing and personality per enemy still need an animation pass.
- Atlas coverage is 13–15%. It is adequate for the combat camera, but a custom loft unwrap would free texture space for close-up shots.
- Eyes read slightly emissive under the acrylic pass. This is fine for readability, but can be toned down.
- Pupils and brows are simple plates. A painted face pass could add expression if close-ups are ever needed.

## 29. Remaining rig debt

- Chieftain: lateral arm elevation above about 120° approaches the side heads. This needs either a clip-range rule or a side-neck avoidance helper if future attacks need it.
- The Sentinel is right-handed only (`OffHand` socket unused), which is fine for the glaive.

## 30. Approved as production references?

Recommended **yes**, pending the user's visual approval. The deformation, texture and LOD pipeline is solved and reproducible from `build_enemy_v12.py`.

## 31. Full roster production recommended?

Recommended **after** the user approves the art direction of these three:

- the faceted style;
- the palette;
- the size hierarchy;
- the head design.

These choices propagate to every enemy.

## Files

- **New:**
  - `docsBlender/EnemyRosterV1/build_enemy_v12.py` (builder, painter, bake, LOD, export)
  - `docsBlender/EnemyRosterV1/review_v12.py` (review renders)
  - `Assets/Scripts/Enemies/EnemyVisualScale.cs`
  - `Assets/Scripts/UI/Core/EnemyVisualScaleRules.cs` and its tests
- **Modified:**
  - `Assets/Editor/DungeonRun/DungeonRunEnemyArtLab.cs` (VisualRoot structure, height ratios, visual scale)
  - `DungeonRunEnemyArtCapture.cs` (output folder, posed-bounds scale fix)
  - The three FBX files, textures and prefabs
- **Checkpoints:** `V12/PreV12Sources`, `V12/PreV12UnityAssets`, `V12/PreV12LiveSession.blend` and `V12/SceneVictor_PreReviewRemoval.unity.bak`. The V1/V1.1 sources are untouched.
- **V1.2 sources:** `V12/<Name>/<Name>_V12.blend`.
