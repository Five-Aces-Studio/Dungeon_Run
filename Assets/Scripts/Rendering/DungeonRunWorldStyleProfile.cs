using DungeonRun.Rendering;
using UnityEngine;

/// <summary>Acrylic + Subtle Pixel V3 tuning profile: style, pixel accent, colour, atmosphere and material globals.</summary>
[CreateAssetMenu(menuName = "Dungeon Run/World Style Profile", fileName = "DungeonRun_World_Profile")]
public sealed class DungeonRunWorldStyleProfile : ScriptableObject
{
    public enum SceneLook { LegacyPixel, Acrylic }
    public enum Stage { BeforePostProcessing, AfterPostProcessing }

    [Header("Style")]
    [Tooltip("LegacyPixel restores the accepted C480/Palette/Outline pipeline exactly. Acrylic enables this profile's fullscreen pass and painterly material response.")]
    public SceneLook sceneLook = SceneLook.Acrylic;
    [Tooltip("Master multiplier for every painterly material response (brush, light wrap, warm pools, ambient coolness). 0 = materials render neutral/banded.")]
    [Range(0, 1)] public float painterlyInfluence = 1f;
    [Tooltip("How strongly the fullscreen pass pulls toward the pixel grid vs. the full-resolution painted image.")]
    [Range(0, 1)] public float pixelInfluence = .24f;

    [Header("Pixel")]
    [Tooltip("1080p-reference screen pixels per virtual pixel. 4 matches the old C480 block size.")]
    [Range(1, 6)] public float pixelScale = 1.75f;
    [Tooltip("How much silhouette depth edges push toward the pixel grid, on top of Pixel Influence.")]
    [Range(0, 1)] public float pixelEdgeStrength = .42f;
    [Tooltip("Cell colour sampling: 0 = point sample at the cell centre, 1 = 4-tap bilinear area average.")]
    [Range(0, 1)] public float cellFilter = .75f;
    [Tooltip("Reduces the pixel/quantization mix on bright pixels so bloom highlights stay smooth.")]
    [Range(0, 1)] public float glowProtection = .8f;
    [Tooltip("Before post-processing (default): the world is stylized first and URP Bloom blurs on top, so glow stays soft. After: the pixel/quantization accent also touches the bloom (A/B only).")]
    public Stage stage = Stage.BeforePostProcessing;
    [Tooltip("Advanced. Eye-space depth difference (metres) that counts as a full silhouette edge.")]
    [Range(0.05f, 3f)] public float edgeDepthThreshold = .5f;

    [Header("Color")]
    [Tooltip("How strongly the fullscreen pass groups colour toward the Dungeon Run palette families (values read from DungeonRunPaletteSettings).")]
    [Range(0, 1)] public float paletteStrength = .4f;
    [Tooltip("Global saturation around luminance. 1 = unchanged.")]
    [Range(0, 2)] public float saturation = 1.05f;
    [Tooltip("Global contrast around mid-grey in display space. 1 = unchanged.")]
    [Range(0.5f, 1.5f)] public float contrast = 1.03f;
    [Tooltip("Lifts the darkest values toward the cool shadow colour so shadows keep readable colour instead of black.")]
    [Range(0, 0.2f)] public float shadowLift = .04f;
    [Tooltip("Cool colour used by Shadow Lift and Ambient Coolness.")]
    [ColorUsage(false)] public Color coolShadowColor = new Color(.10f, .20f, .24f);
    [Tooltip("Blend toward a quantized colour ramp (display space). Keep low: a faint digital accent, not posterization.")]
    [Range(0, 1)] public float quantizationStrength = .14f;
    [Tooltip("Steps per channel for the quantized ramp. Higher = subtler.")]
    [Range(8, 64)] public int colorLevels = 56;
    [Tooltip("Ordered (Bayer) dither aligned to the pixel grid, applied at quantization transitions only.")]
    [Range(0, 1)] public float ditherStrength = .08f;

    [Header("Atmosphere")]
    [Tooltip("Desaturates with distance between the scene fog start/end distances.")]
    [Range(0, 1)] public float distanceDesaturation = .3f;
    [Tooltip("Blends distant surfaces toward the fog colour (atmospheric perspective), on top of material fog.")]
    [Range(0, 1)] public float hazeStrength = .25f;

    [Header("Paint (material globals)")]
    [Tooltip("Value breakup from the brush blotch channel (scaled per material by Brush Strength).")]
    [Range(0, 0.5f)] public float albedoVariation = .18f;
    [Tooltip("Warm/cool hue breakup between each material's shadow and highlight tints.")]
    [Range(0, 1)] public float colorVariation = .5f;
    [Tooltip("Irregular, brush-like light/shadow edges (modulates light intensity before the tone curve).")]
    [Range(0, 1)] public float terminatorBreakup = .35f;
    [Tooltip("Metres. Breaks up cast-shadow edges on materials with Shadow Edge Breakup > 0 (environment only by default).")]
    [Range(0, 0.5f)] public float shadowEdgeBreakup = .12f;

    [Header("Light (material globals)")]
    [Tooltip("0 = legacy hard lighting bands, 1 = continuous painted gradients through the same band levels.")]
    [Range(0, 1)] public float lightingSmoothness = .95f;
    [Tooltip("Broadens point-light falloff (torches, combat pools): dimmer hot spot, wider warm gradient.")]
    [Range(0, 1)] public float warmLightInfluence = .5f;
    [Tooltip("Pulls ambient light toward the cool shadow colour (luminance preserved).")]
    [Range(0, 1)] public float ambientCoolness = .35f;
    [Tooltip("How far strong lights (torches, combat pools) can brighten surfaces above the painted value range. 0 = flattened to the top band; higher = brighter warm pools with a real falloff.")]
    [Range(0, 2f)] public float lightHeadroom = .8f;

    [Header("Paint Filter (screen-space acrylic)")]
    [Tooltip("Blend toward the anisotropic Kuwahara painted image (half resolution, depth-aware upsample). 0 = filter off (no extra passes).")]
    [Range(0, 1)] public float paintStrength = .85f;
    [Tooltip("1080p-reference screen pixels. Brush dab size of the painted image.")]
    [Range(1, 8)] public float paintRadius = 4f;
    [Tooltip("Kuwahara sharpness (q). Higher = crisper dab edges, flatter paint inside each dab.")]
    [Range(1, 16)] public float paintSharpness = 8f;
    [Tooltip("Anisotropy (alpha). Lower = longer strokes that follow the image structure.")]
    [Range(.25f, 4f)] public float paintAnisotropy = 1f;
    [Tooltip("Metres. Depth tolerance of the painted-image upsample, so paint does not bleed across silhouettes.")]
    [Range(.05f, 3f)] public float paintDepthSigma = .5f;

    [Header("Value Structure")]
    [Tooltip("Painter's vignette: darkens the frame outside the focal ellipse so dark masses frame the lit floor.")]
    [Range(0, 1)] public float focusStrength = .35f;
    [Tooltip("Vertical centre of the focal ellipse (0 = bottom, 1 = top of the screen).")]
    [Range(0, 1)] public float focusCentreY = .42f;
    [Tooltip("Focal ellipse radii in screen UV (x, y).")]
    public Vector2 focusRadius = new Vector2(.62f, .55f);
    [Tooltip("Width of the soft transition around the focal ellipse edge.")]
    [Range(.05f, 1f)] public float focusSoftness = .45f;

    [Header("Silhouette Rim")]
    [Tooltip("Painted edge light on silhouettes facing the rim direction (depth-edge based, fades with fog).")]
    [Range(0, 1)] public float rimStrength = .25f;
    [Tooltip("1080p-reference screen pixels. Width of the rim edge.")]
    [Range(.5f, 4f)] public float rimWidth = 1.5f;
    [Tooltip("Relative depth gap behind a silhouette: (background depth - silhouette depth) / silhouette depth. The rim starts at this gap and is full at twice it. 0.25 = the background must be 25% farther than the edge. Edges against the empty void (far plane) never rim.")]
    [Range(.02f, 1f)] public float rimDepthThreshold = .25f;
    [Tooltip("Metres (eye depth). Silhouettes up to this distance get the full rim.")]
    [Range(1f, 60f)] public float rimFullDistance = 14f;
    [Tooltip("Metres (eye depth). The rim fades out between Rim Full Distance and this distance; nothing farther rims.")]
    [Range(1f, 60f)] public float rimMaxDistance = 22f;
    [Tooltip("Screen direction the rim light comes from (+y up). Normalized by the feature.")]
    public Vector2 rimDirection = new Vector2(-1f, 1f);
    [Tooltip("Rim colour (authored sRGB).")]
    [ColorUsage(false)] public Color rimColor = new Color(.35f, .75f, .8f);

    [Header("Painted Glow Pools")]
    [Tooltip("Global strength of the painted glow pools around torches (decoupled from physical light falloff).")]
    [Range(0, 3)] public float poolStrength = 1f;
    [Tooltip("How much the brush strokes break up the pool edge.")]
    [Range(0, 1)] public float poolBreakup = .5f;
    [Tooltip("Painted value steps inside each pool. 0 = smooth gradient.")]
    [Range(0, 6)] public int poolSteps = 3;
    [Tooltip("Warm torch pool colour (authored sRGB). Written to warm DungeonRunFlameLights by an Apply preset.")]
    public Color warmPoolColor = new Color(1f, .6f, .3f);
    [Tooltip("Warm torch pool intensity. Written to warm DungeonRunFlameLights by an Apply preset.")]
    [Range(0, 4)] public float warmPoolIntensity = 1.2f;
    [Tooltip("Warm torch pool radius (metres). Written to warm DungeonRunFlameLights by an Apply preset.")]
    [Range(.5f, 20f)] public float warmPoolRadius = 7f;
    [Tooltip("Cold fixture pool colour (authored sRGB). Written to cold DungeonRunFlameLights by an Apply preset.")]
    public Color coldPoolColor = new Color(.35f, .8f, 1f);
    [Tooltip("Cold fixture pool intensity. Written to cold DungeonRunFlameLights by an Apply preset.")]
    [Range(0, 4)] public float coldPoolIntensity = .9f;
    [Tooltip("Cold fixture pool radius (metres). Written to cold DungeonRunFlameLights by an Apply preset.")]
    [Range(.5f, 20f)] public float coldPoolRadius = 6f;

    [Header("Flames")]
    [Tooltip("Relative light/flame flicker amplitude. Written to every DungeonRunFlameLight by an Apply preset.")]
    [Range(0, 1)] public float flameFlicker = .22f;
    [Tooltip("Flicker tempo. Written to every DungeonRunFlameLight by an Apply preset.")]
    [Range(.1f, 4f)] public float flameFlickerSpeed = 1f;

    [Header("Scene Lighting (written by Apply preset)")]
    [Tooltip("Linear fog start distance (metres). Written to the scene by an Acrylic V3 Apply preset.")]
    public float fogStart = 26f;
    [Tooltip("Linear fog end distance (metres). Written to the scene by an Acrylic V3 Apply preset.")]
    public float fogEnd = 80f;
    [Tooltip("Fog / haze colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color fogColor = new Color(.05f, .105f, .12f);
    [Tooltip("Trilight ambient sky colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color ambientSky = new Color(.15f, .23f, .26f);
    [Tooltip("Trilight ambient equator colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color ambientEquator = new Color(.09f, .15f, .17f);
    [Tooltip("Trilight ambient ground colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color ambientGround = new Color(.06f, .065f, .075f);
    [Tooltip("Key (moon) light shadow strength. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 1)] public float keyShadowStrength = .85f;
    [Tooltip("Key (moon) light intensity relative to the legacy rig. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 3f)] public float keyIntensityScale = 1f;
    [Tooltip("Warm combat pool (player/enemy) range relative to the legacy rig. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0.5f, 2f)] public float combatPoolRangeScale = 1.3f;
    [Tooltip("Warm combat pool (player/enemy) intensity relative to the legacy rig. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 3f)] public float combatPoolIntensityScale = 1f;
    [Tooltip("Cool vista fill intensity relative to the legacy rig. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 3f)] public float coolVistaIntensityScale = 1.25f;
    [Tooltip("Torch point light colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color torchColor = new Color(1f, .56f, .24f);
    [Tooltip("Torch point light intensity. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 40f)] public float torchIntensity = 9f;
    [Tooltip("Torch point light range (metres). Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(1, 30f)] public float torchRange = 9f;
    [Tooltip("Cold (blue) fixture point light colour. Written to the scene by an Acrylic V3 Apply preset.")]
    public Color coldTorchColor = new Color(.4f, .85f, 1f);
    [Tooltip("Cold (blue) fixture point light intensity. Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(0, 40f)] public float coldTorchIntensity = 20f;
    [Tooltip("Cold (blue) fixture point light range (metres). Written to the scene by an Acrylic V3 Apply preset.")]
    [Range(1, 30f)] public float coldTorchRange = 11f;

    // Installer upgrade marker; 0 = asset authored before the painted-flame / paint-filter milestone.
    [HideInInspector] public int paintSchemaVersion;

    public PainterlyGlobals GetGlobals() => PainterlyGlobals.Create(painterlyInfluence, lightingSmoothness,
        warmLightInfluence, ambientCoolness, albedoVariation, colorVariation, terminatorBreakup, shadowEdgeBreakup);
}
