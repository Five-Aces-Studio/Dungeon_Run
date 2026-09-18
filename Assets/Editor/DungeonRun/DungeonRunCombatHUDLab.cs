using System;
using System.IO;
using System.Linq;
using DungeonRun.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit lab composition builder. No existing gameplay/root/renderer changes; scene save stays user-owned.</summary>
public static class DungeonRunCombatHUDLab
{
    public const string RootName = "CombatHUDV1";
    public const string ThemePath = "Assets/Settings/CombatHUDV1Theme.asset";
    public const string PrefabFolder = "Assets/Prefabs/UI/CombatHUDV1";
    public const string ArtFolder = "Assets/Art/UI/CombatHUDV1";
    private static CombatHUDTheme theme;
    private static Sprite whiteSprite;

    [MenuItem("Dungeon Run/Combat HUD V1/Build Lab HUD")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require clean SceneVictorLab in stable Edit Mode.");
        if (scene.GetRootGameObjects().Any(x => x.name == RootName))
            throw new InvalidOperationException("CombatHUDV1 already exists. Use Refresh Preview or explicitly remove only that root before rebuilding.");
        var stage = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var camera = stage.transform.Find("CombatCamera").GetComponent<Camera>();
        var anchors = Enumerable.Range(1, 3).Select(i => stage.GetComponentsInChildren<MeshRenderer>(true)
            .Single(x => x.name == "CHR_Enemy_0" + i)).Cast<Renderer>().ToArray();
        theme = LoadTheme();
        EnsureFolder(PrefabFolder);
        var attack = AssetDatabase.LoadAssetAtPath<CardData>("Assets/Scriptable Objects/Attack.asset");
        var defence = AssetDatabase.LoadAssetAtPath<CardData>("Assets/Scriptable Objects/Armor.asset");
        var heal = AssetDatabase.LoadAssetAtPath<CardData>("Assets/Scriptable Objects/Basic Healing.asset");
        if (!attack || !defence || !heal) throw new InvalidOperationException("Existing card definition assets are required.");

        var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Create Combat HUD V1");
        root.SetActive(false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var source = root.AddComponent<LabCombatHUDSource>();
        source.attack = attack; source.defence = defence; source.heal = heal; source.enemyAnchors = anchors;
        var hud = root.AddComponent<CombatHUD>();
        hud.deferInitialization = true;
        hud.sourceComponent = source; hud.theme = theme; hud.worldCamera = camera; hud.canvas = canvas;
        hud.instantAnimations = true;

        var playerRoot = Stretch("PlayerCombatHUD", root.transform);
        var player = playerRoot.gameObject.AddComponent<PlayerCombatHUD>(); hud.player = player;
        BuildPlayer(player, playerRoot);
        var drawAnchor = player.drawText.rectTransform.parent as RectTransform;
        var discardAnchor = player.discardText.rectTransform.parent as RectTransform;

        var handRoot = Rect("Hand", root.transform, new Vector2(.5f, 0), new Vector2(0, 200), new Vector2(1100, 340));
        var hand = handRoot.gameObject.AddComponent<CardHandController>(); hud.hand = hand;
        hand.handRoot = handRoot; hand.drawAnchor = drawAnchor; hand.discardAnchor = discardAnchor; hand.theme = theme;
        hand.playAnchor = Rect("PlayAnchor", root.transform, new Vector2(.5f, .5f), new Vector2(130, -50), Vector2.zero);
        hand.cardPrefab = BuildCardPrefab();
        hand.actionSlots = new CardActionSlot[2];
        for (int i = 0; i < 2; i++)
        {
            var slot = BuildSlot(root.transform, i);
            hand.actionSlots[i] = slot;
            if (i == 0) PrefabUtility.SaveAsPrefabAsset(slot.gameObject, PrefabFolder + "/ActionSlot.prefab");
        }
        hud.resolveButton = Button("Resolve", root.transform, new Vector2(.5f, 0), new Vector2(235, 363), new Vector2(145, 48), "RESOLVE", 21);
        hud.endTurnButton = Button("EndTurn", root.transform, new Vector2(1, 0), new Vector2(-166, 122), new Vector2(242, 70), "END TURN", 27);
        hud.statusText = Label("Status", root.transform, new Vector2(.5f, 0), new Vector2(-70, 475), new Vector2(850, 25), "", 17, theme.text);
        hud.previewLabel = Label("PreviewLabel", root.transform, new Vector2(.5f, 1), new Vector2(0, -23), new Vector2(600, 24),
            "LAB PREVIEW  /  PRESENTATION ONLY", 13, new Color(.66f, .66f, .6f, .9f));
        hud.enemies = new EnemyCombatHUD[3];
        for (int i = 0; i < 3; i++)
        {
            hud.enemies[i] = BuildEnemy(root.transform, i);
            if (i == 0) PrefabUtility.SaveAsPrefabAsset(hud.enemies[i].gameObject, PrefabFolder + "/EnemyIntent.prefab");
        }
        if (!UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(x => x.gameObject.scene == scene))
        {
            var events = new GameObject("HUDInput", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        handRoot.SetAsLastSibling();
        PrefabUtility.SaveAsPrefabAsset(playerRoot.gameObject, PrefabFolder + "/PlayerCombatHUD.prefab");
        root.SetActive(true);
        hud.deferInitialization = false;
        hud.instantAnimations = false;
        // Save active and before preview card creation; the installer injects scene camera/anchor references.
        PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CombatHUD.prefab");
        hud.Initialize(); hud.SetInstantAnimations(true);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }

    [MenuItem("Dungeon Run/Combat HUD V1/Refresh Preview")]
    public static void RefreshPreview()
    {
        var hud = FindHUD(); theme = LoadTheme(); hud.theme = theme; hud.hand.theme = theme;
        hud.Initialize(); hud.SetInstantAnimations(true);
    }

    public static CombatHUD FindHUD()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath) throw new InvalidOperationException("SceneVictorLab only.");
        return scene.GetRootGameObjects().Single(x => x.name == RootName).GetComponent<CombatHUD>();
    }

    public static void DebugHand(int count) { var hud = FindHUD(); ((LabCombatHUDSource)hud.sourceComponent).DebugSetHandSize(count); hud.SetInstantAnimations(true); }
    public static void DebugHealth(int current, int maximum) => ((LabCombatHUDSource)FindHUD().sourceComponent).DebugSetHealth(current, maximum);

    private static CombatHUDTheme LoadTheme()
    {
        EnsureFolder("Assets/Settings");
        var result = AssetDatabase.LoadAssetAtPath<CombatHUDTheme>(ThemePath);
        if (!result) { result = ScriptableObject.CreateInstance<CombatHUDTheme>(); AssetDatabase.CreateAsset(result, ThemePath); }
        Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png");
        result.cardFrame = Load("CardFrame") ?? result.cardFrame;
        result.panelFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/PanelFrameSlice.asset") ?? result.panelFrame;
        result.portrait = Load("Portrait") ?? result.portrait; result.cardBack = Load("CardBack") ?? result.cardBack;
        result.attackIcon = Load("AttackIcon") ?? result.attackIcon; result.defenceIcon = Load("DefenceIcon") ?? result.defenceIcon;
        result.dodgeIcon = Load("DodgeIcon") ?? result.dodgeIcon; result.healIcon = Load("HealIcon") ?? result.healIcon;
        result.piercingIcon = Load("PiercingIcon") ?? result.piercingIcon;
        if (!result.bodyFont) result.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (!result.displayFont) result.displayFont = result.bodyFont;
        if (!result.bodyFont) throw new InvalidOperationException("Assign a readable TMP body font to CombatHUDV1Theme.");
        EditorUtility.SetDirty(result); AssetDatabase.SaveAssets(); return result;
    }

    private static CardView BuildCardPrefab()
    {
        var card = Rect("Card", null, new Vector2(.5f, .5f), Vector2.zero, new Vector2(225, 300));
        var shadow = Plate("Shadow", card, Vector2.zero, new Vector2(235, 308), new Color(0, 0, 0, .42f));
        shadow.rectTransform.anchoredPosition = new Vector2(5, -8);
        Plate("Thickness", card, new Vector2(2,-3), new Vector2(227,302), new Color(.24f,.17f,.09f));
        Plate("BronzeEdge", card, Vector2.zero, new Vector2(225,300), theme.gold);
        var frame = Plate("Parchment", card, Vector2.zero, new Vector2(219,294), theme.parchment);
        if (theme.cardFrame) { frame.sprite = theme.cardFrame; frame.color = Color.white; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 6; }
        frame.raycastTarget = true;
        var view = card.gameObject.AddComponent<CardView>(); view.frame = frame;
        view.group = card.gameObject.AddComponent<CanvasGroup>();
        view.focus = Plate("Focus", card, new Vector2(0,146), new Vector2(205,3), new Color(1,.86f,.46f)); view.focus.enabled = false;
        view.title = Label("Title", card, new Vector2(.5f,.5f), new Vector2(10,113), new Vector2(153,35), "ATTACK", 23, theme.ink, true);
        view.title.enableAutoSizing = true; view.title.fontSizeMin = 17; view.title.fontSizeMax = 23;
        Plate("TitleRule",card,new Vector2(0,91),new Vector2(168,1),new Color(.36f,.25f,.13f,.65f));
        var inset = Plate("ArtInset",card,new Vector2(0,24),new Vector2(168,118),new Color(.74f,.67f,.5f,.45f));
        view.illustration = Plate("Illustration",card,new Vector2(0,24),new Vector2(158,112),Color.white);
        view.illustration.preserveAspect = true;
        var glyphRect = Rect("FallbackIcon",card,new Vector2(.5f,.5f),new Vector2(0,24),new Vector2(80,85));
        view.fallbackGlyph = glyphRect.gameObject.AddComponent<CombatGlyph>(); view.fallbackGlyph.color = theme.ink; view.fallbackGlyph.raycastTarget = false;
        view.description = Label("Description",card,new Vector2(.5f,.5f),new Vector2(0,-69),new Vector2(177,65),"",18,theme.ink);
        view.description.enableAutoSizing = true; view.description.fontSizeMin = 15; view.description.fontSizeMax = 18;
        view.category = Label("Category",card,new Vector2(.5f,.5f),new Vector2(0,-125),new Vector2(156,24),"STRIKE",13,new Color(.32f,.23f,.13f),true);
        var costBacking = Plate("CostBacking",card,new Vector2(-87,126),new Vector2(34,34),theme.panel);
        view.cost = Label("Cost",costBacking.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(30,30),"1",21,theme.text);
        var saved = PrefabUtility.SaveAsPrefabAsset(card.gameObject, PrefabFolder + "/Card.prefab");
        UnityEngine.Object.DestroyImmediate(card.gameObject);
        return saved.GetComponent<CardView>();
    }

    private static void BuildPlayer(PlayerCombatHUD player,RectTransform parent)
    {
        var header = Rect("Vitals",parent,new Vector2(0,1),new Vector2(244,-81),new Vector2(420,112));
        Plate("VitalsPanel",header,new Vector2(32,0),new Vector2(355,81),theme.panel);
        var portraitFrame = Plate("PortraitFrame",header,new Vector2(-159,0),new Vector2(105,105),Color.clear);
        player.portrait = Plate("Portrait",portraitFrame.transform,Vector2.zero,new Vector2(97,97),theme.panel);
        if(theme.portrait){player.portrait.sprite=theme.portrait;player.portrait.color=Color.white;player.portrait.preserveAspect=true;}
        Label("PlayerName",header,new Vector2(.5f,.5f),new Vector2(27,25),new Vector2(252,28),"THE WAYFARER",23,theme.text,true);
        var healthBack=Plate("HealthTrack",header,new Vector2(30,-10),new Vector2(250,24),new Color(.12f,.075f,.06f));
        player.healthFill=Plate("HealthFill",healthBack.transform,Vector2.zero,new Vector2(246,20),new Color(.62f,.14f,.11f));
        player.healthFill.type=Image.Type.Filled;player.healthFill.fillMethod=Image.FillMethod.Horizontal;
        player.healthText=Label("HP",header,new Vector2(.5f,.5f),new Vector2(30,-11),new Vector2(240,27),"",19,theme.text);
        var floor=Rect("Floor",parent,new Vector2(1,1),new Vector2(-183,-58),new Vector2(305,72));
        Plate("FloorPanel",floor,Vector2.zero,new Vector2(290,67),theme.panel);
        player.floorText=Label("FloorText",floor,new Vector2(.5f,.5f),new Vector2(0,9),new Vector2(250,42),"",27,theme.text,true);
        Label("Location",floor,new Vector2(.5f,.5f),new Vector2(0,-18),new Vector2(260,22),"TRIGONAL ABYSS",13,theme.gold);
        var actions=Rect("Actions",parent,new Vector2(0,.5f),new Vector2(105,-76),new Vector2(142,132));
        Plate("ActionsEdge",actions,Vector2.zero,new Vector2(128,120),Color.clear);
        Plate("ActionsPanel",actions,Vector2.zero,new Vector2(122,114),theme.panel);
        player.actionsText=Label("ActionsValue",actions,new Vector2(.5f,.5f),new Vector2(0,12),new Vector2(120,46),"",34,theme.text);
        Label("ActionsLabel",actions,new Vector2(.5f,.5f),new Vector2(0,-32),new Vector2(120,24),"ACTIONS",16,theme.gold);
        player.drawText=BuildPile(parent,"DrawPile",new Vector2(0,0),new Vector2(125,166),CombatGlyph.Kind.Deck,"DRAW");
        player.discardText=BuildPile(parent,"DiscardPile",new Vector2(1,0),new Vector2(-164,257),CombatGlyph.Kind.Discard,"DISCARD");
    }

    private static TextMeshProUGUI BuildPile(Transform parent,string name,Vector2 anchor,Vector2 p,CombatGlyph.Kind kind,string label)
    {
        var root=Rect(name,parent,anchor,p,new Vector2(124,138));
        Plate("LowerEdge",root,new Vector2(5,-4),new Vector2(82,100),Color.clear);
        Plate("Pile",root,Vector2.zero,new Vector2(82,100),theme.panel);
        var glyph=Rect("Emblem",root,new Vector2(.5f,.5f),new Vector2(0,12),new Vector2(34,42)).gameObject.AddComponent<CombatGlyph>();
        glyph.kind=kind;glyph.color=theme.gold;glyph.raycastTarget=false;
        var count=Label("Count",root,new Vector2(.5f,.5f),new Vector2(0,-23),new Vector2(80,32),"",27,theme.text);
        Label("Label",root,new Vector2(.5f,.5f),new Vector2(0,-67),new Vector2(135,23),label,15,theme.text);
        return count;
    }

    private static CardActionSlot BuildSlot(Transform parent,int index)
    {
        var rect=Rect("ActionSlot"+(index+1),parent,new Vector2(.5f,0),new Vector2(-215+index*175,413),new Vector2(154,108));
        var edge=Plate("Edge",rect,Vector2.zero,new Vector2(154,108),theme.gold);
        edge.raycastTarget=true;
        Plate("Inset",rect,Vector2.zero,new Vector2(150,104),new Color(.065f,.08f,.09f,.82f));
        var slot=rect.gameObject.AddComponent<CardActionSlot>();slot.slotIndex=index;slot.border=edge;
        slot.label=Label("SlotLabel",rect,new Vector2(.5f,.5f),new Vector2(0,-39),new Vector2(145,20),"ACTION "+(index+1),12,theme.text);
        slot.cardAnchor=Rect("CardAnchor",rect,new Vector2(.5f,.5f),new Vector2(0,12),Vector2.zero);
        return slot;
    }

    private static EnemyCombatHUD BuildEnemy(Transform parent,int index)
    {
        var rect=Rect("EnemyHUD"+index,parent,new Vector2(.5f,.5f),Vector2.zero,new Vector2(160,56));
        var component=rect.gameObject.AddComponent<EnemyCombatHUD>();component.group=rect.gameObject.AddComponent<CanvasGroup>();
        var back=Plate("HealthTrack",rect,Vector2.zero,new Vector2(140,14),new Color(.08f,.055f,.045f,.95f));back.raycastTarget=true;
        component.healthFill=Plate("Health",rect,Vector2.zero,new Vector2(136,10),new Color(.60f,.16f,.11f));
        component.healthFill.type=Image.Type.Filled;component.healthFill.fillMethod=Image.FillMethod.Horizontal;
        component.healthText=Label("HealthText",rect,new Vector2(.5f,.5f),new Vector2(0,-19),new Vector2(130,24),"",15,theme.text);
        component.intentText=Label("Intent",rect,new Vector2(.5f,.5f),new Vector2(0,27),new Vector2(160,24),"",16,theme.text);
        component.selection=Plate("Selected",rect,new Vector2(0,-33),new Vector2(38,2),theme.gold);
        return component;
    }

    private static Button Button(string name,Transform parent,Vector2 anchor,Vector2 p,Vector2 size,string title,float fontSize)
    {
        var rect=Rect(name,parent,anchor,p,size);
        var edge=Plate("Edge",rect,Vector2.zero,size,theme.panelFrame ? Color.clear : theme.gold);
        var face=Plate("Face",rect,Vector2.zero,size-new Vector2(5,5),theme.panel);face.raycastTarget=true;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=face;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.4f,1.3f,1.1f);
        colors.pressedColor=new Color(.65f,.7f,.7f);colors.disabledColor=new Color(.45f,.45f,.45f,.8f);button.colors=colors;
        Label("Label",rect,new Vector2(.5f,.5f),Vector2.zero,size-new Vector2(8,8),title,fontSize,theme.text,true);
        return button;
    }

    private static Image Plate(string name,Transform parent,Vector2 p,Vector2 size,Color color)
    {
        var rect=Rect(name,parent,new Vector2(.5f,.5f),p,size);
        var image=rect.gameObject.AddComponent<Image>();image.sprite=WhiteSprite();image.color=color;image.raycastTarget=false;
        if (theme.panelFrame && (name == "VitalsPanel" || name == "FloorPanel" || name == "Face" || name == "Pile" || name == "ActionsPanel"))
        { image.sprite=theme.panelFrame; image.type=Image.Type.Sliced; image.pixelsPerUnitMultiplier=12; image.color=Color.white; }
        return image;
    }
    private static TextMeshProUGUI Label(string name,Transform parent,Vector2 anchor,Vector2 p,Vector2 size,string value,float fontSize,Color color,bool display=false)
    {
        var rect=Rect(name,parent,anchor,p,size);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=display ? theme.displayFont : theme.bodyFont;text.fontSize=fontSize;text.color=color;text.text=value;
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.overflowMode=TextOverflowModes.Ellipsis;
        return text;
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 p,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        if(parent)rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=p;rect.sizeDelta=size;return rect;
    }
    private static RectTransform Stretch(string name,Transform parent)
    {
        var rect=Rect(name,parent,Vector2.zero,Vector2.zero,Vector2.zero);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite) return whiteSprite;
        string path = PrefabFolder + "/UIWhite.asset";
        whiteSprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (whiteSprite) return whiteSprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "UIWhite", filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        whiteSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f,.5f), 100);
        whiteSprite.name = "UIWhiteSprite";
        AssetDatabase.AddObjectToAsset(whiteSprite, texture); AssetDatabase.SaveAssets();
        return whiteSprite;
    }
}
