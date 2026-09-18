# Combat HUD V1 art provenance

## Original tabletop icons

Source: repository `docsDungeonRun/DungeonRun_Stuff_Printable.pdf`, page 13 (1-based), 19-page raster PDF. This supplied repository copy is byte-identical to the earlier Downloads source (SHA-256 `B0F916309A9271CE720A330D279FC8FA817DBD4B9DD57AB5F72EF34019B9B8C4`). Extraction now uses the repository-relative path.

`AttackIcon.png`, `DefenceIcon.png`, `DodgeIcon.png`, `HealIcon.png`, and `PiercingIcon.png` are rendered icon-region excerpts, not complete printable cards. Their original drawing and parchment background are retained. The source PDF is unmodified. Region coordinates and deterministic extraction are recorded in `Captures/CombatHUDV1/extract_printable_icons.py`.

Existing CardData assets have null illustration references. The HUD theme supplies presentation-only icons without mutating those gameplay assets. The lab creates transient clones of Attack/Armor/Basic Healing with Guide base-one values, plus transient missing Dodge/Piercing definitions; none is saved as a second card database. See `docsDungeonRun/SOURCE_DOCUMENTS.md` for the bounded rule-source interpretation.

## Generated frame

`CardFrame.png` uses the built-in image-generation tool with the user's `Imagen de Codex 17 sept 2026, 23_47_19.png` as a style reference, not a screenshot to display as UI. Generated source: `exec-748cd736-2475-48c8-aa4c-e94f7efb4fe3.png` in the thread's generated-images folder. No API key/CLI fallback was used.

Prompt:

> Create ONE production game UI asset, not a mockup or scene. Reference is STYLE REFERENCE ONLY. A single perfectly front-facing rectangular fantasy playing card frame, portrait 3:4 proportions, flat orthographic with no tilt or perspective. Entire rectangle fills image edge to edge, 768x1024 preferred, no outside margin. Warm light parchment interior, subtle fine paper grain, thin dark forged bronze beveled metal border, restrained tiny triangular corner ornaments and rivets inspired by the reference. The parchment must be clean and light enough for dark readable UI text. EMPTY CARD: absolutely NO text, numbers, icons, illustration, internal dividers or logo. All typography and art will be separate native Unity elements. Border occupies only 5% of width. High quality painted fantasy game UI, warm gold edge highlights, dark charcoal outer rim. Reusable digital card frame asset.

## Portrait

`Portrait.png` is a generated reconstruction of the supplied mockup portrait, not a pixel-exact extraction or final protagonist design. Alpha is preserved. Generated source: `exec-a70eec3b-55c3-4cf1-95ff-519d6ed09ccf.png`.

Prompt:

> Asset extraction for this user's Unity game. Extract and faithfully reconstruct ONLY the round player portrait medallion in the TOP LEFT of the reference mockup: mysterious hooded dark face with small golden eyes, muted deep green hood, weathered bronze and steel circular rim with a few restrained triangular points. One centered circular portrait medallion filling 90% of a square image. Genuine transparent background outside the medallion, preserve alpha. No health bar, no leaves trailing far outside, no text, no numbers, no other UI or scenery. Keep the same illustrated dark fantasy game art identity, do not invent a different character. Crisp native-resolution reusable HUD portrait asset.

## Generated panel frame

`PanelFrame.png` is a blank wide bronze/gold plaque with a dark-green interior, generated from the supplied mockup as a style reference. Generated source: `exec-8e98fe84-0ec2-4ec0-b8cb-38106e60e9da.png`. Only the reusable frame is used; labels, values and interaction remain native Unity UI. The integration uses a cropped Sprite region without altering the source raster.

## Generation availability correction

The initial frame request returned HTTP 429 usage_limit_reached. After the user asked to recheck, a new built-in request succeeded. The earlier printable search had covered the repository, not the source PDF in Downloads; the later search recovered the source art. Neither limitation should be treated as permanent.
