using System;
using System.IO;
using System.Linq;
using DungeonRun.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, repeatable presentation-only upgrade. Does not rebuild the world or change combat configuration.</summary>
public static class DungeonRunCombatPolishLab
{
    public const string ThemePath = "Assets/Settings/CombatHUDV3Theme.asset";
    public const string PrefabPath = "Assets/Prefabs/UI/CombatHUDV3/Card.prefab";
    public const string ArtFolder = "Assets/Art/UI/CombatHUDV3";
    private static readonly Color Bronze = new Color(.42f, .33f, .21f);
    private static readonly Color Paper = new Color(.79f, .75f, .65f);
    private static readonly Color Dark = new Color(.045f, .06f, .065f, .96f);

    [MenuItem("Dungeon Run/Combat HUD V3/Install Visual Polish")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || scene.isDirty ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require clean SceneVictorLab in stable Edit Mode.");
        var root = scene.GetRootGameObjects().Single(x => x.name == DungeonRunCombatHUDLab.RootName);
        var hud = root.GetComponent<CombatHUD>();
        if (!hud || !hud.hand || !hud.player || !hud.liveSource)
            throw new InvalidOperationException("Install the existing V2 Lab HUD before V3 polish.");
        foreach (var name in new[] { "Attack", "Defence", "Dodge", "Heal", "Piercing" })
            if (!File.Exists(ArtFolder + "/" + name + "Icon.png"))
                throw new FileNotFoundException("Missing cleaned V3 icon: " + name);

        var theme = BuildTheme();
        var card = BuildCard(theme);
        bool enabled = hud.enabled;
        hud.enabled = false;
        hud.hand.Cleanup();
        hud.theme = theme; hud.hand.theme = theme; hud.hand.cardPrefab = card;
        hud.hand.handRoot.anchoredPosition = new Vector2(0, 200);
        hud.hand.playAnchor.anchoredPosition = new Vector2(-110, -80);
        hud.hand.fanWidth = 850; hud.hand.maxRotation = 8; hud.hand.idleTilt = 3;
        hud.instantAnimations = false;
        PolishPlayer(hud.player, theme);
        foreach (var slot in hud.hand.actionSlots)
        {
            Paint(slot.transform, "Edge", new Vector2(154, 108), Bronze);
            Paint(slot.transform, "Inset", new Vector2(151, 105), new Color(.035f, .045f, .05f, .75f));
            slot.label.fontSize = 12; slot.label.color = theme.text;
        }
        var commit = (RectTransform)hud.resolveButton.transform;
        Place(commit, new Vector2(1, 0), new Vector2(-170, 125), new Vector2(220, 60));
        Paint(commit, "Edge", new Vector2(220, 60), Color.clear);
        Frame(commit, "Face", new Vector2(220, 60), theme.panelFrame, 12);
        var label = hud.resolveButton.GetComponentInChildren<TextMeshProUGUI>(true);
        label.rectTransform.sizeDelta = new Vector2(207, 48); label.fontSize = 26; label.fontStyle = FontStyles.Bold;
        var colors = hud.resolveButton.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f, 1.3f, 1.2f);
        colors.pressedColor = new Color(.7f, .8f, .75f); colors.disabledColor = new Color(.5f, .55f, .52f, .8f);
        hud.resolveButton.colors = colors;
        hud.statusText.fontSize = 16;
        Place(hud.statusText.rectTransform, new Vector2(.5f, 1), new Vector2(0, -115), new Vector2(850, 32));
        hud.previewLabel.fontSize = 11; hud.previewLabel.color = new Color(.8f, .78f, .7f, .6f);
        hud.endTurnButton.gameObject.SetActive(hud.sourceMode == CombatHUD.SourceMode.LabPreview);
        // Do not initialize a live battle in Edit Mode. Startup owns the immutable session snapshot.
        hud.deferInitialization = true; hud.enabled = enabled; hud.deferInitialization = false;
        EditorUtility.SetDirty(hud); EditorUtility.SetDirty(hud.hand); EditorUtility.SetDirty(hud.player);
        AssetDatabase.SaveAssets();
        Undo.FlushUndoRecordObjects();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }

    private static CombatHUDTheme BuildTheme()
    {
        if (!AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(ThemePath))
            if (!AssetDatabase.CopyAsset(DungeonRunCombatHUDLab.ThemePath, ThemePath))
                throw new InvalidOperationException("Cannot clone the existing V1 theme.");
        var theme = AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(ThemePath);
        var original = AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(DungeonRunCombatHUDLab.ThemePath);
        theme.cardFrame = original.cardFrame; theme.panelFrame = original.panelFrame;
        theme.parchment = Paper; theme.ink = new Color(.12f, .13f, .12f);
        theme.panel = Dark; theme.gold = new Color(.73f, .62f, .42f);
        theme.text = new Color(.94f, .9f, .8f);
        theme.attackIcon = Icon("Attack"); theme.defenceIcon = Icon("Defence");
        theme.dodgeIcon = Icon("Dodge"); theme.healIcon = Icon("Heal"); theme.piercingIcon = Icon("Piercing");
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static Sprite Icon(string name)
    {
        string path = ArtFolder + "/" + name + "Icon.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512; importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static CardView BuildCard(CombatHUDTheme theme)
    {
        EnsureFolder("Assets/Prefabs/UI/CombatHUDV3");
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            if (!AssetDatabase.CopyAsset(DungeonRunCombatHUDLab.PrefabFolder + "/Card.prefab", PrefabPath))
                throw new InvalidOperationException("Cannot clone the existing card prefab.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var card = root.GetComponent<CardView>();
            card.Rect.sizeDelta = new Vector2(225, 300);
            Paint(root.transform, "Shadow", new Vector2(231, 306), new Color(0, 0, 0, .38f));
            Paint(root.transform, "Thickness", new Vector2(227, 302), new Color(.09f, .085f, .07f));
            Paint(root.transform, "BronzeEdge", new Vector2(225, 300), Color.clear);
            Frame(root.transform, "Parchment", new Vector2(219, 294), theme.cardFrame, 6);
            card.frame.raycastTarget = true;
            Paint(root.transform, "TitleRule", new Vector2(185, 1), new Color(.3f, .26f, .2f, .4f));
            var inset = Paint(root.transform, "ArtInset", new Vector2(180, 142), new Color(.22f, .23f, .2f, .12f));
            inset.rectTransform.anchoredPosition = new Vector2(0, 14);
            card.illustration.rectTransform.anchoredPosition = new Vector2(0, 14);
            card.illustration.rectTransform.sizeDelta = new Vector2(156, 156);
            card.illustration.preserveAspect = true;
            card.fallbackGlyph.rectTransform.anchoredPosition = new Vector2(0, 14);
            card.fallbackGlyph.rectTransform.sizeDelta = new Vector2(90, 90);
            Text(card.title, new Vector2(0, 116), new Vector2(195, 36), 22, theme.ink, theme.displayFont);
            card.title.fontStyle = FontStyles.Bold; card.title.fontSizeMin = 17; card.title.fontSizeMax = 22;
            Text(card.description, new Vector2(0, -82), new Vector2(190, 60), 18, theme.ink, theme.bodyFont);
            card.description.textWrappingMode = TextWrappingModes.Normal;
            card.description.fontSizeMin = 15; card.description.fontSizeMax = 18;
            Text(card.category, new Vector2(0, -123), new Vector2(180, 24), 12, new Color(.21f, .19f, .15f), theme.displayFont);
            card.category.gameObject.SetActive(true); card.category.enabled = true;
            card.category.enableAutoSizing = false; card.category.fontStyle = FontStyles.Bold;
            card.category.characterSpacing = 2;
            card.cost.transform.parent.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CardView>();
    }

    private static void PolishPlayer(PlayerCombatHUD player, CombatHUDTheme theme)
    {
        var root = player.transform;
        var vitals = root.Find("Vitals");
        Place((RectTransform)vitals, new Vector2(0, 1), new Vector2(265, -86), new Vector2(450, 120));
        Frame(vitals, "VitalsPanel", new Vector2(355, 81), theme.panelFrame, 12);
        Paint(vitals, "PortraitFrame", new Vector2(122, 122), Color.clear);
        player.portrait.rectTransform.sizeDelta = new Vector2(116, 116);
        Paint(vitals, "HealthTrack", new Vector2(265, 28), new Color(.14f, .055f, .04f));
        player.healthFill.rectTransform.sizeDelta = new Vector2(261, 24);
        player.healthText.fontSize = 23; player.healthText.fontStyle = FontStyles.Bold;
        var actions = root.Find("Actions");
        Place((RectTransform)actions, new Vector2(0, .5f), new Vector2(112, -8), new Vector2(158, 150));
        Paint(actions, "ActionsEdge", new Vector2(154, 146), Color.clear);
        Frame(actions, "ActionsPanel", new Vector2(154, 146), theme.panelFrame, 10);
        player.actionsText.fontSize = 42; player.actionsText.fontStyle = FontStyles.Bold;
        player.actionsText.rectTransform.sizeDelta = new Vector2(140, 55);
        var draw = (RectTransform)root.Find("DrawPile");
        var discard = (RectTransform)root.Find("DiscardPile");
        Place(draw, new Vector2(0, 0), new Vector2(112, 280), new Vector2(124, 138));
        Place(discard, new Vector2(1, 0), new Vector2(-160, 295), new Vector2(124, 138));
        foreach (var pile in new[] { draw, discard })
        {
            Paint(pile, "LowerEdge", new Vector2(88, 106), Color.clear);
            Frame(pile, "Pile", new Vector2(92, 112), theme.panelFrame, 12);
        }
        foreach (var text in player.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (theme.bodyFont) text.font = theme.bodyFont;
    }

    private static Image Frame(Transform root, string name, Vector2 size, Sprite sprite, float multiplier)
    {
        var image = Paint(root, name, size, Color.white);
        image.sprite = sprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = multiplier;
        return image;
    }

    private static Image Paint(Transform root, string name, Vector2 size, Color color)
    {
        var image = root.Find(name).GetComponent<Image>();
        image.sprite = null; image.type = Image.Type.Simple; image.color = color;
        image.rectTransform.sizeDelta = size;
        return image;
    }
    private static void Text(TextMeshProUGUI text, Vector2 position, Vector2 size, float fontSize, Color color, TMP_FontAsset font)
    {
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        text.fontSize = fontSize; text.color = color; text.enableAutoSizing = true;
        if (font) text.font = font;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
