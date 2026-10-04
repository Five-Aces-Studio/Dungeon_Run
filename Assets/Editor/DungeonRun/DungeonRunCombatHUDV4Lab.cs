using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonRun.UI;
using DungeonRun.UI.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Combat HUD V4 installer (SceneVictorLab only, presentation only). Non-destructive: the V3 root "CombatHUDV1" is never
/// edited except its active flag. Install V4 destroys any previous "CombatHUDV4", duplicates CombatHUDV1 into a fresh
/// "CombatHUDV4" (internal references are remapped by Instantiate; camera, enemy anchors and assets stay shared) and
/// restyles only the copy from Assets/Settings/CombatHUDV4/HudLayoutV4.json. Never saves the scene.
/// <para>Element id -> object path (relative to the CombatHUDV4 root; [new] = created by the installer):</para>
/// <code>
/// portrait            PlayerCombatHUD/Vitals/PortraitFrame           (child Portrait stretched to fill)
/// player_name         PlayerCombatHUD/Vitals/PlayerName
/// player_hp_track     PlayerCombatHUD/Vitals/HealthTrack
/// player_hp_ghost     PlayerCombatHUD/Vitals/HealthGhostV4           [new]
/// player_hp_fill      PlayerCombatHUD/Vitals/HealthFill              (re-parented from HealthTrack)
/// player_hp_text      PlayerCombatHUD/Vitals/HP
/// player_chip_block   PlayerCombatHUD/Vitals/BlockChipV4 (+Value)    [new]
/// player_chip_dodge   PlayerCombatHUD/Vitals/DodgeChipV4 (+Value)    [new]
/// floor_plaque        PlayerCombatHUD/Floor/FloorPanel
/// floor_text          PlayerCombatHUD/Floor/FloorText
/// floor_location      PlayerCombatHUD/Floor/Location
/// actions_medallion   PlayerCombatHUD/Actions/ActionsPanel
/// actions_text        PlayerCombatHUD/Actions/ActionsValue
/// actions_pips        PlayerCombatHUD/Actions/PipRowV4 (+Pip0 template) [new]
/// actions_label       PlayerCombatHUD/Actions/ActionsLabel
/// draw_token          PlayerCombatHUD/DrawPile/Pile                  (also CardHandController.drawAnchor)
/// draw_count          PlayerCombatHUD/DrawPile/Count
/// draw_label          PlayerCombatHUD/DrawPile/Label
/// discard_token       PlayerCombatHUD/DiscardPile/Pile               (also CardHandController.discardAnchor)
/// discard_count       PlayerCombatHUD/DiscardPile/Count
/// discard_label       PlayerCombatHUD/DiscardPile/Label
/// commit              Resolve (+Face stretched = plate sprite, CommitButtonPresenter)
/// commit_label        Resolve/Label                                  (placed relative to commit)
/// status_wash         StatusWashV4                                   [new]
/// status_text         Status
/// inspect_wash/_hint  runtime CardInspectWashV4/CardInspectHintV4    (CombatHUDStyleV4.inspectWash/inspectHint)
/// targeting_banner    runtime CardTargetingV4 (+Cancel, HitMode chips on its row) (CombatHUDStyleV4.targetingBanner, targetingButtonSize/Gap)
/// detail              runtime CardDetailV4 + sibling HintV4 below it (CombatHUDStyleV4.detailPanel, detailFrames, detail*Pos/Size)
/// terminal, vignette  runtime Terminal/Banner, Terminal/Vignette     (CombatHUDStyleV4.terminalPanel, vignetteAlpha)
/// sockets             ActionSlotN (Edge = socket, GlowV4, RimV4, RuneV4 [new], CardAnchor at centre)
/// hand                Hand (CardHandController fan, slot and staging fields)
/// enemy_hp_track      EnemyHUDn/HealthTrack        enemy_hp_fill   EnemyHUDn/Health (+HealthGhostV4 [new])
/// enemy_hp_text       EnemyHUDn/HealthText         intent_label    EnemyHUDn/Intent
/// intent_socket       EnemyHUDn/IntentSocketV4 (+Icon0) and IntentIcon1..3 [new]
/// reveal_flash        EnemyHUDn/RevealFlashV4 [new] enemy_name     EnemyHUDn/NameV4 [new]
/// history             EnemyHUDn/HistoryV4 (Eye, Seen0..2, HistoryTokenV4 0..2) [new]
/// enemy_chip_block    EnemyHUDn/BlockChipV4, DodgeChipV4 (+Value) [new]
/// target_marker       EnemyHUDn/Selected
/// </code>
/// Hidden (never deleted): Vitals/VitalsPanel, Actions/ActionsEdge, *Pile/LowerEdge, *Pile/Emblem, Resolve/Edge,
/// ActionSlotN/Inset, ActionSlotN/SlotLabel. Containers Vitals/Floor/Actions/DrawPile/DiscardPile are stretched to the
/// canvas so every JSON anchor applies in canvas space.
/// </summary>
public static class DungeonRunCombatHUDV4Lab
{
    public const string V3RootName = "CombatHUDV1", RootName = "CombatHUDV4";
    public const string ArtFolder = "Assets/Art/UI/CombatHUDV4";
    public const string FontFolder = ArtFolder + "/Fonts", TmpFolder = FontFolder + "/TMP";
    public const string LayoutPath = "Assets/Settings/CombatHUDV4/HudLayoutV4.json";
    public const string StylePath = "Assets/Settings/CombatHUDV4/CombatHUDStyleV4.asset";
    public const string ThemePath = "Assets/Settings/CombatHUDV4Theme.asset";
    public const string PrefabPath = "Assets/Prefabs/UI/CombatHUDV4/Card.prefab";
    private const string Menu = "Dungeon Run/Combat HUD V4/";
    private const float SpritePixelsPerUnit = 200; // art is authored at 2x the 1080 reference
    private static readonly string[] Weights = { "Regular", "Medium", "Bold", "ExtraBold" };
    private static readonly string[] StaticLabels = { "player_name", "floor_location", "actions_label", "draw_label", "discard_label" };
    private static readonly Color Ink = new Color(27 / 255f, 22 / 255f, 18 / 255f);

    // ------------------------------------------------------------------ HudLayoutV4.json (JsonUtility; missing keys default)
    [Serializable] public sealed class TextSpec
    {
        public string role, weight, color, sample, align, defeat_color;
        public float size, outline, wrapWidth;
        public float[] zone, pos, box;
    }
    [Serializable] public sealed class Border { public float left, right, top, bottom; }
    [Serializable] public sealed class NumberSpec { public float[] pos, size; }
    [Serializable] public sealed class CommitStates { public string Disabled, Ready, Hover, Pressed, Locked, Resolving; }
    [Serializable] public sealed class ButtonsSpec { public float[] size; public float gap; public TextSpec text; }
    [Serializable] public sealed class Element
    {
        public string id, sprite, asset, image, tint, sprite_on, sprite_off, unknown, glyph, token;
        public float[] anchor, pivot, pos, size, icon_size, glyph_size, token_size;
        public TextSpec text, title, body;
        public Border slice_border_px;
        public NumberSpec number;
        public CommitStates states;
        public ButtonsSpec buttons;
        public float spacing, icon_alpha, alpha;
        public int max_visible;
        public bool stretch;
    }
    [Serializable] public sealed class StateTints { public string ARMED, HOVER_VALID, HOVER_INVALID, QUEUED, RESOLVING, VALID_GLOW; }
    [Serializable] public sealed class SocketSpec
    {
        public float[] anchor, pivot, size, queuedCardOffset;
        public float y, slotCenterX, slotSpacing, queuedCardScale;
        public string sprite, rim, glow;
        public TextSpec rune;
        public StateTints state_tints;
    }
    [Serializable] public sealed class IconSpec { public float[] centre, size; }
    [Serializable] public sealed class CardText { public TextSpec title, effect, label; public IconSpec icon, glyph; }
    [Serializable] public sealed class FocusTints { public string hover, selected, valid, invalid; }
    [Serializable] public sealed class SpriteBox { public string sprite, order; public float[] size; public FocusTints tints; }
    [Serializable] public sealed class HandSpec
    {
        public float[] anchor, pos, card;
        public float fanWidth, spacing, curve, maxRotation, hoverLift, hoverScale;
        public CardText cardText, socketCard;
        public SpriteBox shadow, focusRim;
    }
    [Serializable] public sealed class EnemySpec { public float[] root, screenOffset; public Element[] elements; }
    [Serializable] public sealed class FontPaths { public string display, body; }
    [Serializable] public sealed class CentreSpec { public float[] centre; }
    [Serializable] public sealed class DetailZones
    {
        public float[] title, art, target, effect, footer_label;
        public IconSpec icon, footer_glyph;
        public CentreSpec close;
    }
    [Serializable] public sealed class DetailSpec { public DetailZones zones; }
    [Serializable] public sealed class Layout
    {
        public int version;
        public float[] reference;
        public string spriteRoot;
        public FontPaths fonts;
        public Element[] elements;
        public SocketSpec sockets;
        public HandSpec hand;
        public EnemySpec enemy;
        public DetailSpec detail;
    }

    private sealed class FontSet
    {
        public readonly Dictionary<string, TMP_FontAsset> assets = new Dictionary<string, TMP_FontAsset>();
        public readonly Dictionary<TMP_FontAsset, Material> outlines = new Dictionary<TMP_FontAsset, Material>();
        public Material number, engraved;
        public TMP_FontAsset Get(string role, string weight)
        {
            role = string.IsNullOrEmpty(role) ? "body" : role;
            if (!string.IsNullOrEmpty(weight) && assets.TryGetValue(role + "/" + weight, out var exact)) return exact;
            foreach (var w in Weights.Reverse()) if (assets.TryGetValue(role + "/" + w, out var near)) return near;
            return null;
        }
        public Material Outline(TMP_FontAsset font) => font && outlines.TryGetValue(font, out var preset) ? preset : null;
    }

    // ------------------------------------------------------------------ menus
    [MenuItem(Menu + "Install V4")]
    public static void Install()
    {
        var scene = RequireLab();
        var v1 = FindRoot(scene, V3RootName) ?? throw new InvalidOperationException("SceneVictorLab has no " + V3RootName + " root.");
        var v1Hud = v1.GetComponent<CombatHUD>();
        if (!v1Hud || !v1Hud.hand || !v1Hud.player || !v1Hud.resolveButton)
            throw new InvalidOperationException(V3RootName + " is not a complete V3 combat HUD.");
        var layout = LoadLayout();
        var fonts = BuildFonts(layout, false);
        ImportArt(layout);
        var style = BuildStyle(layout, fonts);
        var theme = BuildTheme(style);
        var card = BuildCard(layout, style, fonts);
        AssetDatabase.SaveAssets();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Combat HUD V4");
        foreach (var old in scene.GetRootGameObjects().Where(x => x.name == RootName).ToArray()) Undo.DestroyObjectImmediate(old);
        var copy = Object.Instantiate(v1);
        copy.name = RootName;
        copy.transform.SetSiblingIndex(v1.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(copy, "Install Combat HUD V4");
        var hud = copy.GetComponent<CombatHUD>();
        VerifyCopy(v1Hud, hud);
        Restyle(hud, layout, style, theme, card, fonts);
        Undo.RecordObject(v1, "Install Combat HUD V4");
        v1.SetActive(false); copy.SetActive(true);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = copy;
        Debug.Log("[HUD V4] Installed " + RootName + " (copy of " + V3RootName + ", which is now inactive). Scene not saved.");
    }

    [MenuItem(Menu + "Revert to V3 (A/B)")]
    public static void RevertToV3() => SetActiveRoot(false);

    [MenuItem(Menu + "Activate V4 (A/B)")]
    public static void ActivateV4() => SetActiveRoot(true);

    [MenuItem(Menu + "Rebuild Fonts")]
    public static void RebuildFonts()
    {
        RequireLab();
        var layout = LoadLayout();
        var fonts = BuildFonts(layout, true);
        if (AssetDatabase.LoadAssetAtPath<CombatHUDStyleV4>(StylePath)) BuildStyle(layout, fonts);
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD V4] Fonts rebuilt in " + TmpFolder + " (asset GUIDs kept).");
    }

    [MenuItem(Menu + "Reimport Art")]
    public static void ReimportArt()
    {
        RequireLab();
        Debug.Log("[HUD V4] Art import settings applied to " + ImportArt(LoadLayout()) + " sprite(s).");
    }

    private static void SetActiveRoot(bool v4)
    {
        var scene = RequireLab();
        var v1 = FindRoot(scene, V3RootName) ?? throw new InvalidOperationException("SceneVictorLab has no " + V3RootName + " root.");
        var copy = FindRoot(scene, RootName);
        if (v4 && !copy) throw new InvalidOperationException("Run Install V4 first.");
        Undo.RecordObjects(copy ? new Object[] { v1, copy } : new Object[] { v1 }, v4 ? "Activate Combat HUD V4" : "Revert Combat HUD to V3");
        if (copy) copy.SetActive(v4);
        v1.SetActive(!v4);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[HUD V4] Active root: " + (v4 ? RootName : V3RootName) + ". Scene not saved.");
    }

    private static Scene RequireLab()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require SceneVictorLab in stable Edit Mode (not playing, not compiling).");
        return scene;
    }

    private static GameObject FindRoot(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(x => x.name == name);

    private static Layout LoadLayout()
    {
        if (!File.Exists(LayoutPath)) throw new FileNotFoundException("Missing layout " + LayoutPath);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        if (layout?.elements == null || layout.sockets == null || layout.hand?.cardText == null || layout.enemy?.elements == null)
            throw new InvalidOperationException(LayoutPath + " is missing elements, sockets, hand.cardText or enemy.elements.");
        var ids = new HashSet<string>(layout.elements.Select(x => x.id).Concat(layout.enemy.elements.Select(x => x.id)));
        var required = new[]
        {
            "portrait", "player_name", "player_hp_track", "player_hp_ghost", "player_hp_fill", "player_hp_text", "player_chip_block",
            "player_chip_dodge", "floor_plaque", "floor_text", "floor_location", "actions_medallion", "actions_text", "actions_pips",
            "actions_label", "draw_token", "draw_count", "draw_label", "discard_token", "discard_count", "discard_label", "commit",
            "commit_label", "status_wash", "status_text", "inspect_wash", "inspect_hint", "targeting_banner", "detail", "terminal",
            "vignette", "enemy_hp_track", "enemy_hp_fill", "enemy_hp_text", "intent_socket", "reveal_flash", "intent_label",
            "enemy_name", "history", "enemy_chip_block", "target_marker"
        };
        var missing = required.Where(x => !ids.Contains(x)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException(LayoutPath + " is missing element id(s): " + string.Join(", ", missing));
        return layout;
    }

    // ------------------------------------------------------------------ fonts
    private static FontSet BuildFonts(Layout layout, bool rebuild)
    {
        EnsureFolder(TmpFolder);
        var set = new FontSet();
        foreach (var (role, template) in new[] { ("display", layout.fonts?.display), ("body", layout.fonts?.body) })
        {
            if (string.IsNullOrEmpty(template)) continue;
            foreach (var weight in Weights)
            {
                string ttf = template.Replace("{weight}", weight);
                if (!File.Exists(ttf)) continue;
                string name = Path.GetFileNameWithoutExtension(ttf);
                string path = TmpFolder + "/" + name + " SDF.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (!asset) asset = CreateFontAsset(ttf, path);
                else if (rebuild) asset.ClearFontAssetData(true);
                Warm(asset);
                set.assets[role + "/" + weight] = asset;
                set.outlines[asset] = Preset(asset, TmpFolder + "/" + name + " Outline.mat", OutlinePreset);
            }
        }
        var heavy = set.Get("display", "ExtraBold") ?? throw new InvalidOperationException("No display font found under " + FontFolder);
        string heavyName = heavy.name.Replace(" SDF", "");
        set.number = Preset(heavy, TmpFolder + "/" + heavyName + " Number.mat", NumberPreset);
        set.engraved = Preset(heavy, TmpFolder + "/" + heavyName + " Engraved.mat", EngravedPreset);
        AssetDatabase.SaveAssets();
        return set;
    }

    private static TMP_FontAsset CreateFontAsset(string ttfPath, string assetPath)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath) ?? throw new FileNotFoundException("Font not imported: " + ttfPath);
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true)
            ?? throw new InvalidOperationException("TMP could not load " + ttfPath + " (enable Include Font Data).");
        asset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);
        asset.atlasTextures[0].name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        return asset;
    }

    private static void Warm(TMP_FontAsset asset)
    {
        var glyphs = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) glyphs.Append(c);
        glyphs.Append((char)0x00B7).Append((char)0x2013).Append((char)0x2014).Append((char)0x2019);
        if (!asset.TryAddCharacters(glyphs.ToString(), out string missing) && !string.IsNullOrEmpty(missing))
            Debug.LogWarning("[HUD V4] " + asset.name + " is missing glyphs: " + missing);
        foreach (var texture in asset.atlasTextures)
            if (texture && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, asset);
        EditorUtility.SetDirty(asset);
    }

    private static Material Preset(TMP_FontAsset font, string path, Action<Material> configure)
    {
        var shader = Shader.Find("TextMeshPro/Distance Field") ?? font.material.shader;
        var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!preset) { preset = new Material(shader); AssetDatabase.CreateAsset(preset, path); }
        preset.shader = shader;
        preset.shaderKeywords = Array.Empty<string>();
        preset.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
        preset.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
        preset.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
        preset.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
        preset.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
        preset.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
        preset.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        preset.SetFloat(ShaderUtilities.ID_FaceDilate, 0);
        preset.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);
        configure(preset);
        ShaderUtilities.UpdateShaderRatios(preset);
        EditorUtility.SetDirty(preset);
        return preset;
    }

    private static void OutlinePreset(Material m)
    {
        m.EnableKeyword("OUTLINE_ON"); m.EnableKeyword("UNDERLAY_ON");
        m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(Ink.r, Ink.g, Ink.b, .92f));
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, .18f);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(Ink.r, Ink.g, Ink.b, .45f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .3f); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.6f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, .15f); m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .45f);
    }

    private static void NumberPreset(Material m)
    {
        m.EnableKeyword("OUTLINE_ON"); m.EnableKeyword("UNDERLAY_ON");
        m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(Ink.r, Ink.g, Ink.b, .95f));
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, .22f);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(Ink.r, Ink.g, Ink.b, .6f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .4f); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.8f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, .25f); m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .5f);
    }

    private static void EngravedPreset(Material m)
    {
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(234 / 255f, 220 / 255f, 184 / 255f, .35f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0); m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .2f);
    }

    // ------------------------------------------------------------------ art import
    private static int ImportArt(Layout layout)
    {
        var slices = new Dictionary<string, Vector4>();
        foreach (var e in layout.elements.Concat(layout.enemy.elements))
        {
            var b = e.slice_border_px;
            if (string.IsNullOrEmpty(e.sprite) || b == null || b.left + b.right + b.top + b.bottom <= 0) continue;
            slices[SpritePath(layout, e.sprite)] = new Vector4(b.left, b.bottom, b.right, b.top);
        }
        int count = 0;
        foreach (var file in Directory.GetFiles(ArtFolder, "*.png", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (path.StartsWith(FontFolder + "/")) continue;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (!importer) continue;
            bool icon = path.StartsWith(ArtFolder + "/Icons/") && !path.StartsWith(ArtFolder + "/Icons/Small/");
            var border = slices.TryGetValue(path, out var slice) ? slice : Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency || !importer.sRGBTexture || importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 2048 ||
                importer.mipmapEnabled != icon || !Mathf.Approximately(importer.spritePixelsPerUnit, SpritePixelsPerUnit) ||
                importer.spriteBorder != border || importer.wrapMode != TextureWrapMode.Clamp || settings.spriteMeshType != SpriteMeshType.FullRect;
            count++;
            if (!changed) continue;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.sRGBTexture = true; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            importer.mipmapEnabled = icon; importer.spritePixelsPerUnit = SpritePixelsPerUnit; importer.spriteBorder = border;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return count;
    }

    private static string SpritePath(Layout layout, string relative) =>
        relative.StartsWith("Assets/") ? relative : (string.IsNullOrEmpty(layout.spriteRoot) ? ArtFolder : layout.spriteRoot) + "/" + relative;

    private static Sprite Art(string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        string path = relative.StartsWith("Assets/") ? relative : ArtFolder + "/" + relative;
        if (!path.EndsWith(".png")) path += ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite) Debug.LogWarning("[HUD V4] Sprite not found: " + path);
        return sprite;
    }

    // ------------------------------------------------------------------ style, theme, card prefab
    private static CombatHUDStyleV4 BuildStyle(Layout layout, FontSet fonts)
    {
        EnsureFolder(Path.GetDirectoryName(StylePath).Replace('\\', '/'));
        var style = AssetDatabase.LoadAssetAtPath<CombatHUDStyleV4>(StylePath);
        if (!style) { style = ScriptableObject.CreateInstance<CombatHUDStyleV4>(); AssetDatabase.CreateAsset(style, StylePath); }
        style.displayFont = fonts.Get("display", "Bold"); style.displayHeavyFont = fonts.Get("display", "ExtraBold");
        style.bodyFont = fonts.Get("body", "Medium"); style.bodyBoldFont = fonts.Get("body", "Bold");
        style.displayOutline = fonts.Outline(style.displayHeavyFont); style.labelOutline = fonts.Outline(style.displayFont);
        style.numberOutline = fonts.number; style.engravedRune = fonts.engraved;

        var categories = Enum.GetNames(typeof(CardCategory));
        var kinds = Enum.GetNames(typeof(PreviewCardKind));
        style.cardFrames = categories.Select(c => Art("Cards/CardFrame_" + c)).ToArray();
        style.socketCardFrames = categories.Select(c => Art("Cards/CardFrameSocket_" + c)).ToArray();
        style.detailArt = categories.Select(c => Art("Cards/DetailArt_" + c)).ToArray();
        style.detailFrames = categories.Select(c => Art("Cards/DetailFrame_" + c)).ToArray();
        style.categoryGlyphs = new[] { "Sword", "Shield", "Boot", "Heart", "Star" }.Select(g => Art("Glyphs/Glyph" + g)).ToArray();
        style.icons = kinds.Select(k => Art("Icons/" + k + "Icon")).ToArray();
        style.smallIcons = kinds.Select(k => Art("Icons/Small/" + k + "Icon")).ToArray();
        style.cardShadow = Art(layout.hand.shadow?.sprite ?? "Cards/CardShadow");
        style.cardFocusRim = Art(layout.hand.focusRim?.sprite ?? "Cards/CardFocusRim");
        style.detailFrame = Art("Cards/DetailFrame");
        var so = layout.sockets;
        style.socket = Art(so.sprite ?? "Controls/Socket"); style.socketRim = Art(so.rim ?? "Controls/SocketRim"); style.socketGlow = Art(so.glow ?? "Controls/SocketGlow");
        style.commitDisabled = Art("Controls/Commit_Disabled"); style.commitReady = Art("Controls/Commit_Ready");
        style.commitHover = Art("Controls/Commit_Hover"); style.commitPressed = Art("Controls/Commit_Pressed");
        style.commitLocked = Art("Controls/Commit_Locked"); style.commitResolving = Art("Controls/Commit_Resolving");
        style.playerHPTrack = Art("Crest/PlayerHPTrack"); style.playerHPFill = Art("Crest/PlayerHPFill"); style.playerHPGhost = Art("Crest/PlayerHPGhost");
        style.chipBlock = Art("Crest/ChipBlock"); style.chipDodge = Art("Crest/ChipDodge");
        style.floorPlaque = Art("Static/FloorPlaque"); style.actionMedallion = Art("Static/ActionMedallion");
        style.pipOn = Art("Static/PipOn"); style.pipOff = Art("Static/PipOff");
        style.drawToken = Art("Static/DrawToken"); style.discardToken = Art("Static/DiscardToken");
        style.banner = Art("Static/Banner"); style.terminalBanner = Art("Static/TerminalBanner");
        style.vignette = Art("Static/Vignette"); style.textWash = Art("Static/TextWash");
        style.smallButton = Art("Controls/SmallButton"); style.closeButton = Art("Controls/CloseButton"); style.targetMarker = Art("Enemy/TargetMarker");
        style.feedbackGlyphs = new[] { "Burst", "Pierce", "Shield", "Boot", "Heart", "Clock", "Burst" }.Select(g => Art("Glyphs/Glyph" + g)).ToArray();
        style.enemyHPTrack = Art("Enemy/EnemyHPTrack"); style.enemyHPFill = Art("Enemy/EnemyHPFill");
        style.intentSocket = Art("Enemy/IntentSocket"); style.intentUnknown = Art("Enemy/IntentUnknown");
        style.revealFlash = Art("Enemy/RevealFlash"); style.historyEye = Art("Glyphs/GlyphEye");
        style.historyToken = Art(layout.enemy.elements.First(x => x.id == "history").token ?? "Enemy/HistoryToken");

        // Colours and runtime placements that HudLayoutV4.json defines are rewritten on every install (the JSON is authoritative;
        // focus tints may carry alpha as #RRGGBBAA).
        var tints = so.state_tints;
        if (tints != null)
        {
            style.rimArmed = Hex(tints.ARMED, style.rimArmed); style.rimHoverValid = Hex(tints.HOVER_VALID, style.rimHoverValid);
            style.rimHoverInvalid = Hex(tints.HOVER_INVALID, style.rimHoverInvalid); style.rimQueued = Hex(tints.QUEUED, style.rimQueued);
            style.rimResolving = Hex(tints.RESOLVING, style.rimResolving); style.glowResolving = Hex(tints.RESOLVING, style.glowResolving);
            style.glowHoverValid = Hex(tints.VALID_GLOW, style.glowHoverValid);
        }
        var focus = layout.hand.focusRim?.tints;
        if (focus != null)
        {
            style.focusHover = Hex(focus.hover, style.focusHover); style.focusSelected = Hex(focus.selected, style.focusSelected);
            style.focusValid = Hex(focus.valid, style.focusValid); style.focusInvalid = Hex(focus.invalid, style.focusInvalid);
        }
        style.runeIdle = Hex(so.rune?.color, style.runeIdle);
        var ct = layout.hand.cardText;
        style.titleInk = Hex(ct.title?.color, style.titleInk); style.bodyInk = Hex(ct.effect?.color, style.bodyInk); style.footerText = Hex(ct.label?.color, style.footerText);
        var E = Elements(layout);
        style.ghostColor = Hex(E["player_hp_ghost"].tint, style.ghostColor);
        style.hintText = Hex(E["inspect_hint"].text?.color, style.hintText);
        style.mutedGold = Hex(E["floor_location"].text?.color, style.mutedGold);
        style.parchmentText = Hex(E["floor_text"].text?.color, style.parchmentText);
        var terminal = E["terminal"];
        if (terminal.title != null)
        {
            style.victoryColor = Hex(terminal.title.color, style.victoryColor); style.defeatColor = Hex(terminal.title.defeat_color, style.defeatColor);
            if (terminal.title.size > 0) style.terminalTitleSize = terminal.title.size;
        }
        if (terminal.body != null && terminal.body.size > 0) style.terminalBodySize = terminal.body.size;
        if (E["vignette"].alpha > 0) style.vignetteAlpha = E["vignette"].alpha;
        var history = layout.enemy.elements.First(x => x.id == "history");
        // history.icon_alpha is the alpha of OLDER observed entries; the newest is always full alpha at runtime.
        if (history.icon_alpha > 0) style.historyIconAlpha = history.icon_alpha;
        style.detailPanel = Placement(E["detail"]); style.targetingBanner = Placement(E["targeting_banner"]);
        style.terminalPanel = Placement(terminal); style.inspectHint = Placement(E["inspect_hint"]); style.inspectWash = Placement(E["inspect_wash"]);

        // Socket-card mode zones (delta 2), in the 225x300 card-unit basis, same as hand.cardText.
        var sc = layout.hand.socketCard;
        if (sc != null)
        {
            var cardSize = V2(layout.hand.card, new Vector2(225, 300));
            (style.socketCardTitlePos, style.socketCardTitleSize) = ZoneCentre(sc.title?.zone, cardSize);
            if (sc.title != null && sc.title.size > 0) style.socketCardTitleFontMax = sc.title.size;
            style.socketCardIconPos = PointCentre(sc.icon?.centre, cardSize); style.socketCardIconSize = V2(sc.icon?.size, style.socketCardIconSize);
            style.socketCardGlyphPos = PointCentre(sc.glyph?.centre, cardSize); style.socketCardGlyphSize = V2(sc.glyph?.size, style.socketCardGlyphSize);
        }
        // Detail panel zones (delta 8), in the 360x480 basis documented by HudLayoutV4.json detail.zones.
        var dz = layout.detail?.zones;
        if (dz != null)
        {
            var basis = new Vector2(360, 480);
            (style.detailTitlePos, style.detailTitleSize) = ZoneCentre(dz.title, basis);
            (style.detailArtPos, style.detailArtSize) = ZoneCentre(dz.art, basis);
            style.detailIconPos = PointCentre(dz.icon?.centre, basis); style.detailIconSize = V2(dz.icon?.size, style.detailIconSize);
            (style.detailTargetPos, style.detailTargetSize) = ZoneCentre(dz.target, basis);
            (style.detailEffectPos, style.detailEffectSize) = ZoneCentre(dz.effect, basis);
            (style.detailFooterLabelPos, style.detailFooterLabelSize) = ZoneCentre(dz.footer_label, basis);
            style.detailFooterGlyphPos = PointCentre(dz.footer_glyph?.centre, basis); style.detailFooterGlyphSize = V2(dz.footer_glyph?.size, style.detailFooterGlyphSize);
            style.detailClosePos = PointCentre(dz.close?.centre, basis);
        }
        // Targeting ribbon buttons (delta 6).
        var buttons = E["targeting_banner"].buttons;
        if (buttons != null) { style.targetingButtonSize = V2(buttons.size, style.targetingButtonSize); if (buttons.gap > 0) style.targetingButtonGap = buttons.gap; }
        EditorUtility.SetDirty(style);
        return style;
    }

    /// <summary>Card/panel-local centred pos+size from a top-left zone [x0,y0,x1,y1] against a <paramref name="basis"/> size (same convention as Zone/CardPoint).</summary>
    private static (Vector2 pos, Vector2 size) ZoneCentre(float[] zone, Vector2 basis)
    {
        if (zone == null || zone.Length < 4) return (Vector2.zero, Vector2.zero);
        return (new Vector2((zone[0] + zone[2]) * .5f - basis.x * .5f, basis.y * .5f - (zone[1] + zone[3]) * .5f),
            new Vector2(zone[2] - zone[0], zone[3] - zone[1]));
    }

    private static Vector2 PointCentre(float[] centre, Vector2 basis) =>
        centre == null || centre.Length < 2 ? Vector2.zero : new Vector2(centre[0] - basis.x * .5f, basis.y * .5f - centre[1]);

    private static CombatHUDTheme BuildTheme(CombatHUDStyleV4 style)
    {
        if (!AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(ThemePath) && !AssetDatabase.CopyAsset(DungeonRunCombatPolishLab.ThemePath, ThemePath))
            throw new InvalidOperationException("Cannot clone the V3 theme into " + ThemePath);
        var theme = AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(ThemePath);
        theme.v4Style = style;
        // Legacy paths (theme.Icon, theme fonts) resolve to the V4 set as well.
        Sprite Icon(PreviewCardKind kind) => style.Icon(kind);
        theme.attackIcon = Icon(PreviewCardKind.Attack); theme.defenceIcon = Icon(PreviewCardKind.Defence);
        theme.dodgeIcon = Icon(PreviewCardKind.Dodge); theme.healIcon = Icon(PreviewCardKind.Heal); theme.piercingIcon = Icon(PreviewCardKind.Piercing);
        theme.comboIcon = Icon(PreviewCardKind.Combo); theme.counterattackIcon = Icon(PreviewCardKind.Counterattack);
        theme.chargeIcon = Icon(PreviewCardKind.Charge); theme.missIcon = Icon(PreviewCardKind.Miss);
        if (style.bodyFont) theme.bodyFont = style.bodyFont;
        if (style.displayFont) theme.displayFont = style.displayFont;
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static CardView BuildCard(Layout layout, CombatHUDStyleV4 style, FontSet fonts)
    {
        EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) && !AssetDatabase.CopyAsset(DungeonRunCombatPolishLab.PrefabPath, PrefabPath))
            throw new InvalidOperationException("Cannot clone the V3 card prefab into " + PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var card = root.GetComponent<CardView>();
            var hd = layout.hand; var ct = hd.cardText;
            var size = V2(hd.card, new Vector2(225, 300));
            card.Rect.sizeDelta = size;
            foreach (var name in new[] { "Thickness", "BronzeEdge", "TitleRule", "ArtInset" }) Hide(root.transform, name);
            if (card.cost) card.cost.transform.parent.gameObject.SetActive(false); // CostBacking
            var shadow = Find(root.transform, "Shadow").GetComponent<Image>();
            Centre(shadow.rectTransform, Vector2.zero, V2(hd.shadow?.size, new Vector2(245, 320)));
            shadow.sprite = style.cardShadow; shadow.type = Image.Type.Simple; shadow.raycastTarget = false;
            shadow.color = new Color(1, 1, 1, style.shadowIntensity * .75f);
            Centre(card.frame.rectTransform, Vector2.zero, size);
            card.frame.sprite = style.cardFrames.FirstOrDefault(); card.frame.type = Image.Type.Simple; card.frame.color = Color.white;
            card.frame.pixelsPerUnitMultiplier = 1; card.frame.raycastTarget = true;
            Centre(card.focus.rectTransform, Vector2.zero, V2(hd.focusRim?.size, new Vector2(237, 312)));
            card.focus.sprite = style.cardFocusRim; card.focus.type = Image.Type.Simple; card.focus.enabled = false; card.focus.raycastTarget = false;
            Zone(card.title.rectTransform, ct.title.zone, size); Style(card.title, ct.title, fonts, false);
            card.title.enableAutoSizing = true; card.title.fontSizeMin = 14; card.title.fontSizeMax = ct.title.size;
            var iconSize = V2(ct.icon?.size, new Vector2(118, 118));
            Centre(card.illustration.rectTransform, CardPoint(ct.icon?.centre, size), iconSize);
            card.illustration.preserveAspect = true; card.illustration.raycastTarget = false;
            Centre(card.fallbackGlyph.rectTransform, CardPoint(ct.icon?.centre, size), iconSize * .7f);
            Zone(card.description.rectTransform, ct.effect.zone, size); Style(card.description, ct.effect, fonts, false);
            card.description.textWrappingMode = TextWrappingModes.Normal; card.description.overflowMode = TextOverflowModes.Ellipsis;
            card.description.enableAutoSizing = true; card.description.fontSizeMin = 14; card.description.fontSizeMax = ct.effect.size;
            float margin = ct.effect.wrapWidth > 0 ? Mathf.Max(0, (card.description.rectTransform.sizeDelta.x - ct.effect.wrapWidth) * .5f) : 0;
            card.description.margin = new Vector4(margin, 0, margin, 0);
            Zone(card.category.rectTransform, ct.label.zone, size); Style(card.category, ct.label, fonts, true);
            card.category.gameObject.SetActive(true); card.category.enabled = true;
            var glyphTransform = root.transform.Find("CategoryGlyph");
            var glyph = glyphTransform ? glyphTransform.GetComponent<Image>() : NewRect("CategoryGlyph", root.transform).gameObject.AddComponent<Image>();
            Centre(glyph.rectTransform, CardPoint(ct.glyph?.centre, size), V2(ct.glyph?.size, new Vector2(16, 16)));
            glyph.sprite = style.categoryGlyphs.FirstOrDefault(); glyph.preserveAspect = true; glyph.raycastTarget = false;
            glyph.transform.SetSiblingIndex(card.category.transform.GetSiblingIndex() + 1);
            card.shadow = shadow; card.categoryGlyph = glyph;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CardView>();
    }

    // ------------------------------------------------------------------ scene copy
    private static void VerifyCopy(CombatHUD source, CombatHUD copy)
    {
        var root = copy.transform;
        void Inside(Component component, string what)
        {
            if (!component || !component.transform.IsChildOf(root))
                throw new InvalidOperationException("Instantiate did not remap " + what + " into " + RootName + ".");
        }
        Inside(copy.player, "player"); Inside(copy.hand, "hand"); Inside(copy.resolveButton, "resolveButton");
        Inside(copy.endTurnButton, "endTurnButton"); Inside(copy.statusText, "statusText"); Inside(copy.previewLabel, "previewLabel");
        if (copy.canvas) Inside(copy.canvas, "canvas");
        foreach (var enemy in copy.enemies) Inside(enemy, "enemies");
        foreach (var slot in copy.hand.actionSlots) Inside(slot, "hand.actionSlots");
        Inside(copy.hand.handRoot, "hand.handRoot"); Inside(copy.hand.drawAnchor, "hand.drawAnchor");
        Inside(copy.hand.discardAnchor, "hand.discardAnchor"); Inside(copy.hand.playAnchor, "hand.playAnchor");
        if (copy.liveSource)
        {
            Inside(copy.liveSource, "liveSource");
            if (copy.liveSource.battle) Inside(copy.liveSource.battle, "liveSource.battle");
            if (!copy.liveSource.enemyAnchors.SequenceEqual(source.liveSource.enemyAnchors))
                throw new InvalidOperationException("Copy lost the shared enemy anchor renderers.");
        }
        if (copy.worldCamera != source.worldCamera) throw new InvalidOperationException("Copy lost the shared world camera.");
        if (copy.theme != source.theme) throw new InvalidOperationException("Copy lost the theme asset reference.");
    }

    private static void Restyle(CombatHUD hud, Layout layout, CombatHUDStyleV4 style, CombatHUDTheme theme, CardView card, FontSet fonts)
    {
        var E = Elements(layout);
        hud.theme = theme; hud.hand.theme = theme; hud.hand.cardPrefab = card;
        var player = hud.player; var p = player.transform;
        Transform vitals = Find(p, "Vitals"), floor = Find(p, "Floor"), actions = Find(p, "Actions"), draw = Find(p, "DrawPile"), discard = Find(p, "DiscardPile");
        foreach (var container in new[] { vitals, floor, actions, draw, discard }) Stretch((RectTransform)container);
        Hide(vitals, "VitalsPanel"); Hide(actions, "ActionsEdge");
        foreach (var pile in new[] { draw, discard }) { Hide(pile, "LowerEdge"); Hide(pile, "Emblem"); }

        // Player crest
        Place((RectTransform)Find(vitals, "PortraitFrame"), E["portrait"]);
        Stretch(player.portrait.rectTransform);
        var track = (RectTransform)Find(vitals, "HealthTrack");
        Paint(track, E["player_hp_track"], style.playerHPTrack);
        player.healthTrack = track;
        var fill = player.healthFill.rectTransform;
        fill.SetParent(vitals, false);
        Paint(fill, E["player_hp_fill"], style.playerHPFill);
        var ghost = NewImage("HealthGhostV4", vitals, E["player_hp_ghost"], style.playerHPGhost);
        ghost.color = style.ghostColor; ghost.fillAmount = player.healthFill.fillAmount;
        ghost.transform.SetSiblingIndex(track.GetSiblingIndex() + 1);
        fill.SetSiblingIndex(ghost.transform.GetSiblingIndex() + 1);
        Text(player.healthText, E["player_hp_text"], fonts);
        player.healthText.transform.SetSiblingIndex(fill.GetSiblingIndex() + 1);
        Text(Find(vitals, "PlayerName").GetComponent<TMP_Text>(), E["player_name"], fonts);
        player.blockChip = Chip("BlockChipV4", vitals, E["player_chip_block"], E["player_chip_block"], style.chipBlock, fonts, out player.blockChipText);
        player.dodgeChip = Chip("DodgeChipV4", vitals, E["player_chip_dodge"], E["player_chip_dodge"], style.chipDodge, fonts, out player.dodgeChipText);
        player.healthGhost = ghost;

        // Floor plaque
        Paint((RectTransform)Find(floor, "FloorPanel"), E["floor_plaque"], style.floorPlaque);
        Text(player.floorText, E["floor_text"], fonts);
        Text(Find(floor, "Location").GetComponent<TMP_Text>(), E["floor_location"], fonts);

        // Action medallion and pips
        Paint((RectTransform)Find(actions, "ActionsPanel"), E["actions_medallion"], style.actionMedallion);
        Text(player.actionsText, E["actions_text"], fonts);
        Text(Find(actions, "ActionsLabel").GetComponent<TMP_Text>(), E["actions_label"], fonts);
        var pips = E["actions_pips"];
        var pipSize = V2(pips.size, new Vector2(16, 16));
        float pipSpacing = pips.spacing > 0 ? pips.spacing : pipSize.x + 6;
        var row = NewRect("PipRowV4", actions);
        Place(row, pips); row.sizeDelta = new Vector2(Mathf.Max(1, pips.max_visible > 0 ? pips.max_visible : 6) * pipSpacing, pipSize.y);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = pipSpacing - pipSize.x; rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        var pip = NewRect("Pip0", row).gameObject.AddComponent<Image>();
        pip.rectTransform.sizeDelta = pipSize; pip.sprite = Art(pips.sprite_on) ?? style.pipOn; pip.raycastTarget = false;
        player.pipRow = row; player.pipTemplate = pip;

        // Draw and discard tokens (cards still fly from and to these tokens)
        var drawToken = (RectTransform)Find(draw, "Pile");
        var discardToken = (RectTransform)Find(discard, "Pile");
        Paint(drawToken, E["draw_token"], style.drawToken);
        Paint(discardToken, E["discard_token"], style.discardToken);
        Text(player.drawText, E["draw_count"], fonts); Text(Find(draw, "Label").GetComponent<TMP_Text>(), E["draw_label"], fonts);
        Text(player.discardText, E["discard_count"], fonts); Text(Find(discard, "Label").GetComponent<TMP_Text>(), E["discard_label"], fonts);
        hud.hand.drawAnchor = drawToken; hud.hand.discardAnchor = discardToken;

        // Status line on a painted wash; preview label restyled (it is emptied at runtime in Live mode)
        var wash = NewImage("StatusWashV4", hud.transform, E["status_wash"], style.textWash);
        wash.transform.SetSiblingIndex(hud.statusText.transform.GetSiblingIndex());
        Text(hud.statusText, E["status_text"], fonts);
        CombatHUDStyleV4.SetFont(hud.previewLabel, style.bodyFont);

        BuildCommit(hud, E["commit"], E["commit_label"], style, fonts);
        BuildSockets(hud.hand, layout, style, fonts);
        foreach (var enemy in hud.enemies) if (enemy) BuildEnemy(enemy, layout, E["player_chip_block"], style, fonts);
        foreach (var component in hud.GetComponentsInChildren<Component>(true)) if (component) EditorUtility.SetDirty(component);
    }

    private static void BuildCommit(CombatHUD hud, Element commit, Element labelElement, CombatHUDStyleV4 style, FontSet fonts)
    {
        var rect = (RectTransform)hud.resolveButton.transform;
        Place(rect, commit);
        Hide(rect, "Edge");
        var face = Find(rect, "Face").GetComponent<Image>();
        Stretch(face.rectTransform);
        face.sprite = Art(commit.states?.Disabled) ?? style.commitDisabled; face.type = Image.Type.Simple; face.color = Color.white; face.raycastTarget = true;
        var label = hud.resolveButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Style(label, labelElement.text, fonts, true);
        PlaceRelative(label.rectTransform, labelElement, commit);
        label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = labelElement.text.size;
        hud.resolveButton.transition = Selectable.Transition.None; // the presenter drives every visual state
        hud.resolveButton.targetGraphic = face;
        var presenter = rect.GetComponent<CommitButtonPresenter>();
        if (!presenter) presenter = rect.gameObject.AddComponent<CommitButtonPresenter>();
        presenter.face = face; presenter.label = label;
        hud.commitPresenter = presenter;
    }

    private static void BuildSockets(CardHandController hand, Layout layout, CombatHUDStyleV4 style, FontSet fonts)
    {
        var so = layout.sockets; var hd = layout.hand;
        hand.handRoot.anchorMin = hand.handRoot.anchorMax = V2(hd.anchor, hand.handRoot.anchorMin);
        hand.handRoot.anchoredPosition = V2(hd.pos, hand.handRoot.anchoredPosition);
        if (hd.fanWidth > 0) hand.fanWidth = hd.fanWidth;
        if (hd.spacing > 0) hand.spacing = hd.spacing;
        if (hd.curve > 0) hand.verticalCurve = hd.curve;
        if (hd.maxRotation > 0) hand.maxRotation = hd.maxRotation;
        if (hd.hoverLift > 0) hand.hoverElevation = hd.hoverLift;
        if (hd.hoverScale > 0) hand.hoverScale = hd.hoverScale;
        hand.queuedCardScale = so.queuedCardScale; hand.slotCenterX = so.slotCenterX; hand.slotSpacing = so.slotSpacing;
        hand.queuedCardOffset = V2(so.queuedCardOffset, Vector2.zero);
        // Staged (playing) cards land centred on their sockets: same x rule, socket row height, same offset.
        float referenceHeight = layout.reference != null && layout.reference.Length > 1 ? layout.reference[1] : 1080;
        var anchor = V2(so.anchor, new Vector2(.5f, 0));
        if (hand.playAnchor.anchorMin != new Vector2(.5f, .5f) || hand.playAnchor.anchorMax != new Vector2(.5f, .5f))
            Debug.LogWarning("[HUD V4] PlayAnchor is not centre-anchored; staging assumes centre anchors.");
        hand.playAnchor.anchoredPosition = new Vector2(so.slotCenterX, so.y + (anchor.y - .5f) * referenceHeight) + hand.queuedCardOffset;
        // Staged cards must not grow: same scale as queued (V4 multiplies both by the dense-row scale at runtime).
        hand.playCardSpacing = so.slotSpacing; hand.playCardScale = so.queuedCardScale;
        var size = V2(so.size, new Vector2(118, 158));
        int count = hand.actionSlots.Length;
        for (int i = 0; i < count; i++)
        {
            var slot = hand.actionSlots[i];
            var rect = slot.Rect;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = V2(so.pivot, new Vector2(.5f, .5f)); rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(so.slotCenterX + (i - (count - 1) * .5f) * so.slotSpacing, so.y);
            Stretch(slot.border.rectTransform);
            slot.border.sprite = style.socket; slot.border.type = Image.Type.Simple; slot.border.color = Color.white; slot.border.raycastTarget = true;
            Hide(rect, "Inset");
            slot.label.gameObject.SetActive(false);
            var glow = NewStretchImage("GlowV4", rect, style.socketGlow);
            var rim = NewStretchImage("RimV4", rect, style.socketRim);
            glow.transform.SetSiblingIndex(slot.border.transform.GetSiblingIndex() + 1);
            rim.transform.SetSiblingIndex(glow.transform.GetSiblingIndex() + 1);
            var rune = NewRect("RuneV4", rect).gameObject.AddComponent<TextMeshProUGUI>();
            Centre(rune.rectTransform, V2(so.rune?.pos, new Vector2(0, -63)), V2(so.rune?.box, new Vector2(38, 16)));
            Style(rune, so.rune, fonts, false);
            // The engraved preset can read wrong on the new light parchment tab; style.runeUseEngravedMaterial lets the parent fall back to the plain face.
            CombatHUDStyleV4.SetFont(rune, rune.font, style.runeUseEngravedMaterial ? fonts.engraved : null);
            rune.color = style.runeIdle;
            rune.text = HudStatusFormat.Roman(i + 1);
            rune.transform.SetSiblingIndex(rim.transform.GetSiblingIndex() + 1);
            var cardAnchor = Find(rect, "CardAnchor") as RectTransform;
            if (cardAnchor) cardAnchor.anchoredPosition = Vector2.zero; // queuedCardOffset carries the offset
            slot.rim = rim; slot.glow = glow; slot.rune = rune;
        }
    }

    private static void BuildEnemy(EnemyCombatHUD enemy, Layout layout, Element chipTemplate, CombatHUDStyleV4 style, FontSet fonts)
    {
        var en = layout.enemy;
        var E = en.elements.ToDictionary(x => x.id);
        var rect = (RectTransform)enemy.transform;
        rect.sizeDelta = V2(en.root, rect.sizeDelta);
        if (en.screenOffset != null && en.screenOffset.Length >= 2) enemy.screenOffset = V2(en.screenOffset, enemy.screenOffset);
        var track = (RectTransform)Find(rect, "HealthTrack");
        Paint(track, E["enemy_hp_track"], style.enemyHPTrack); // keeps its raycast target: the enemy click area
        enemy.healthTrack = track;
        var fill = enemy.healthFill.rectTransform;
        Paint(fill, E["enemy_hp_fill"], style.enemyHPFill);
        var ghost = NewImage("HealthGhostV4", rect, E["enemy_hp_fill"], style.enemyHPFill);
        ghost.color = style.ghostColor; ghost.fillAmount = enemy.healthFill.fillAmount;
        ghost.transform.SetSiblingIndex(track.GetSiblingIndex() + 1);
        fill.SetSiblingIndex(ghost.transform.GetSiblingIndex() + 1);
        Text(enemy.healthText, E["enemy_hp_text"], fonts);
        enemy.healthText.transform.SetSiblingIndex(fill.GetSiblingIndex() + 1);

        var socketElement = E["intent_socket"];
        var flash = NewImage("RevealFlashV4", rect, E["reveal_flash"], style.revealFlash);
        flash.gameObject.SetActive(false);
        var socket = NewImage("IntentSocketV4", rect, socketElement, Art(socketElement.unknown) ?? style.intentUnknown);
        socket.sprite = Art(socketElement.unknown) ?? style.intentUnknown; // hidden intent until the reveal flip
        socket.preserveAspect = true; socket.raycastTarget = true; // second click area for enemy targeting
        var iconSize = V2(socketElement.icon_size, new Vector2(34, 34));
        var icons = new List<Image>();
        var icon0 = NewRect("Icon0", socket.transform).gameObject.AddComponent<Image>();
        Centre(icon0.rectTransform, Vector2.zero, iconSize);
        icons.Add(icon0);
        // Extra actions of a multi-action intent: a compact row to the left of the socket.
        float small = Mathf.Round(iconSize.x * .6f);
        var socketPos = V2(socketElement.pos, Vector2.zero); var socketSize = V2(socketElement.size, new Vector2(44, 44));
        for (int k = 1; k < 4; k++)
        {
            var extra = NewRect("IntentIcon" + k, rect).gameObject.AddComponent<Image>();
            Centre(extra.rectTransform, socketPos + new Vector2(-(socketSize.x * .5f + 3 + (k - .5f) * (small + 2)), 0), new Vector2(small, small));
            icons.Add(extra);
        }
        foreach (var icon in icons) { icon.preserveAspect = true; icon.raycastTarget = false; icon.gameObject.SetActive(false); }

        Text(enemy.intentText, E["intent_label"], fonts);
        enemy.intentText.enableAutoSizing = true; enemy.intentText.fontSizeMin = 10; enemy.intentText.fontSizeMax = enemy.intentText.fontSize;
        enemy.intentText.enabled = false;
        var name = NewRect("NameV4", rect).gameObject.AddComponent<TextMeshProUGUI>();
        Text(name, E["enemy_name"], fonts);

        var history = E["history"];
        var row = NewRect("HistoryV4", rect); Place(row, history);
        var glyphSize = V2(history.glyph_size, new Vector2(16, 16)); var seenSize = V2(history.icon_size, new Vector2(16, 16));
        var tokenSize = V2(history.token_size, new Vector2(22, 22));
        var tokenSprite = Art(history.token) ?? style.historyToken;
        float spacing = history.spacing > 0 ? history.spacing : 25;
        float eyeX = -row.sizeDelta.x * .5f + glyphSize.x * .5f + 5;
        var eye = NewRect("Eye", row).gameObject.AddComponent<Image>();
        Centre(eye.rectTransform, new Vector2(eyeX, 0), glyphSize); eye.sprite = Art(history.glyph) ?? style.historyEye;
        eye.preserveAspect = true; eye.raycastTarget = false;
        // At most 3 entries: a parchment token disc behind each observed-history icon.
        var seen = new Image[3];
        var tokens = new Image[3];
        for (int j = 0; j < seen.Length; j++)
        {
            var pos = new Vector2(eyeX + spacing * (j + 1), 0);
            if (tokenSprite)
            {
                tokens[j] = NewRect("HistoryTokenV4" + j, row).gameObject.AddComponent<Image>();
                Centre(tokens[j].rectTransform, pos, tokenSize);
                tokens[j].sprite = tokenSprite; tokens[j].preserveAspect = true; tokens[j].raycastTarget = false; tokens[j].gameObject.SetActive(false);
            }
            seen[j] = NewRect("Seen" + j, row).gameObject.AddComponent<Image>();
            Centre(seen[j].rectTransform, pos, seenSize);
            seen[j].preserveAspect = true; seen[j].raycastTarget = false; seen[j].gameObject.SetActive(false);
            if (tokens[j]) seen[j].transform.SetSiblingIndex(tokens[j].transform.GetSiblingIndex() + 1);
        }
        row.gameObject.SetActive(false);

        var chip = E["enemy_chip_block"];
        enemy.blockChip = Chip("BlockChipV4", rect, chip, chipTemplate, style.chipBlock, fonts, out enemy.blockChipText);
        var dodge = new Element { id = "enemy_chip_dodge", anchor = chip.anchor, pivot = chip.pivot, size = chip.size,
            pos = new[] { V2(chip.pos, Vector2.zero).x + V2(chip.size, new Vector2(52, 26)).x + 4, V2(chip.pos, Vector2.zero).y } };
        enemy.dodgeChip = Chip("DodgeChipV4", rect, dodge, chipTemplate, style.chipDodge, fonts, out enemy.dodgeChipText);

        var marker = enemy.selection;
        Paint(marker.rectTransform, E["target_marker"], style.targetMarker ? style.targetMarker : marker.sprite);
        marker.enabled = false;
        marker.transform.SetAsLastSibling();

        enemy.healthGhost = ghost; enemy.revealFlash = flash; enemy.intentSocket = socket; enemy.intentIcons = icons.ToArray();
        enemy.intentLabel = enemy.intentText; enemy.nameText = name; enemy.historyIcons = seen; enemy.historyTokens = tokens;
    }

    // ------------------------------------------------------------------ building blocks
    private static Dictionary<string, Element> Elements(Layout layout) => layout.elements.ToDictionary(x => x.id);

    private static CombatHUDStyleV4.PanelPlacement Placement(Element e) => new CombatHUDStyleV4.PanelPlacement(
        V2(e.anchor, new Vector2(.5f, .5f)), V2(e.pivot, new Vector2(.5f, .5f)), V2(e.pos, Vector2.zero), V2(e.size, new Vector2(100, 100)));

    private static void Place(RectTransform rect, Element e)
    {
        rect.anchorMin = rect.anchorMax = V2(e.anchor, new Vector2(.5f, .5f));
        rect.pivot = V2(e.pivot, new Vector2(.5f, .5f));
        rect.anchoredPosition = V2(e.pos, Vector2.zero);
        rect.sizeDelta = V2(e.size, rect.sizeDelta);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    /// <summary>Places a child of <paramref name="parent"/>'s object, both authored in canvas space with the same anchor.</summary>
    private static void PlaceRelative(RectTransform rect, Element e, Element parent)
    {
        if (V2(e.anchor, Vector2.zero) != V2(parent.anchor, Vector2.zero))
            Debug.LogWarning("[HUD V4] " + e.id + " and " + parent.id + " use different anchors; relative placement assumes equal anchors.");
        var parentPivot = V2(parent.pivot, new Vector2(.5f, .5f)); var parentSize = V2(parent.size, Vector2.zero);
        var centre = V2(parent.pos, Vector2.zero) + Vector2.Scale(new Vector2(.5f, .5f) - parentPivot, parentSize);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = V2(e.pivot, new Vector2(.5f, .5f));
        rect.anchoredPosition = V2(e.pos, Vector2.zero) - centre;
        rect.sizeDelta = V2(e.size, rect.sizeDelta);
        rect.localScale = Vector3.one;
    }

    private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero; rect.localScale = Vector3.one;
    }

    /// <summary>Card-local rect from a zone [x0, y0, x1, y1] in card top-left pixels (1080 reference).</summary>
    private static void Zone(RectTransform rect, float[] zone, Vector2 card)
    {
        if (zone == null || zone.Length < 4) return;
        Centre(rect, new Vector2((zone[0] + zone[2]) * .5f - card.x * .5f, card.y * .5f - (zone[1] + zone[3]) * .5f),
            new Vector2(zone[2] - zone[0], zone[3] - zone[1]));
    }

    private static Vector2 CardPoint(float[] centre, Vector2 card) =>
        centre == null || centre.Length < 2 ? Vector2.zero : new Vector2(centre[0] - card.x * .5f, card.y * .5f - centre[1]);

    /// <summary>Applies the element placement and sprite to an existing Image (raycast flag untouched).</summary>
    private static Image Paint(RectTransform rect, Element e, Sprite fallback)
    {
        Place(rect, e);
        var image = rect.GetComponent<Image>();
        var sprite = Art(e.sprite);
        image.sprite = sprite ? sprite : fallback;
        image.color = Color.white; image.pixelsPerUnitMultiplier = 1;
        bool sliced = e.slice_border_px != null && e.slice_border_px.left + e.slice_border_px.right + e.slice_border_px.top + e.slice_border_px.bottom > 0;
        if (!string.IsNullOrEmpty(e.image) && e.image.StartsWith("Filled"))
        {
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        else image.type = sliced && image.sprite && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Element e, Sprite fallback)
    {
        var rect = NewRect(name, parent);
        rect.gameObject.AddComponent<Image>().raycastTarget = false;
        return Paint(rect, e, fallback);
    }

    private static Image NewStretchImage(string name, Transform parent, Sprite sprite)
    {
        var rect = NewRect(name, parent); Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = Color.clear; image.raycastTarget = false;
        return image;
    }

    private static GameObject Chip(string name, Transform parent, Element e, Element template, Sprite sprite, FontSet fonts, out TextMeshProUGUI value)
    {
        var chip = NewImage(name, parent, e, sprite);
        var number = template.number;
        var rect = NewRect("Value", chip.transform);
        Centre(rect, V2(number?.pos, new Vector2(12, 0)), V2(number?.size, new Vector2(22, 20)));
        value = rect.gameObject.AddComponent<TextMeshProUGUI>();
        Style(value, template.text, fonts, true);
        value.text = template.text?.sample ?? "1";
        chip.gameObject.SetActive(false);
        return chip.gameObject;
    }

    private static void Text(TMP_Text text, Element e, FontSet fonts)
    {
        Place(text.rectTransform, e);
        Style(text, e.text, fonts, true);
        if (StaticLabels.Contains(e.id) && !string.IsNullOrEmpty(e.text?.sample)) text.text = e.text.sample;
    }

    private static void Style(TMP_Text text, TextSpec spec, FontSet fonts, bool outline)
    {
        if (spec == null) return;
        var font = fonts.Get(spec.role, spec.weight);
        CombatHUDStyleV4.SetFont(text, font, outline ? fonts.Outline(font) : null);
        if (spec.size > 0) text.fontSize = spec.size;
        text.color = Hex(spec.color, text.color);
        text.fontStyle = FontStyles.Normal; text.characterSpacing = 0; text.enableAutoSizing = false;
        text.alignment = spec.align == "left" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero; text.raycastTarget = false;
    }

    private static Transform Find(Transform parent, string path) =>
        parent.Find(path) ?? throw new InvalidOperationException("Missing " + parent.name + "/" + path + " in the V3 HUD hierarchy.");

    private static void Hide(Transform parent, string path)
    {
        var child = parent.Find(path);
        if (child) child.gameObject.SetActive(false);
    }

    private static Vector2 V2(float[] values, Vector2 fallback) =>
        values != null && values.Length >= 2 ? new Vector2(values[0], values[1]) : fallback;

    private static Color Hex(string hex, Color fallback) =>
        !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : fallback;

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
