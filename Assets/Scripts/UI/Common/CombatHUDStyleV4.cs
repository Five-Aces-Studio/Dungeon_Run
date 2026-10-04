using System;
using DungeonRun.UI.Presentation;
using TMPro;
using UnityEngine;

namespace DungeonRun.UI
{
    /// <summary>
    /// Combat HUD V4 painted presentation: sprites, fonts, colours and presentation timings only (never gameplay rules).
    /// Referenced from <see cref="CombatHUDTheme.v4Style"/>; a null reference keeps the V1/V3 visuals.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Run/Combat HUD Style V4")]
    public sealed class CombatHUDStyleV4 : ScriptableObject
    {
        /// <summary>RectTransform placement for runtime-built panels (written by the V4 installer from HudLayoutV4.json).</summary>
        [Serializable]
        public struct PanelPlacement
        {
            public Vector2 anchor, pivot, position, size;
            public PanelPlacement(Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
            { this.anchor = anchor; this.pivot = pivot; this.position = position; this.size = size; }
            public void Apply(RectTransform rect)
            { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        }

        [Header("Fonts")]
        [Tooltip("Alegreya SC Bold: small labels, names, footers, status.")] public TMP_FontAsset displayFont;
        [Tooltip("Alegreya SC ExtraBold: titles, counters, floating numbers.")] public TMP_FontAsset displayHeavyFont;
        [Tooltip("Alegreya Sans Medium: body copy and hints.")] public TMP_FontAsset bodyFont;
        [Tooltip("Alegreya Sans Bold: card effect text.")] public TMP_FontAsset bodyBoldFont;
        [Tooltip("TMP preset on the displayHeavyFont atlas: warm dark outline.")] public Material displayOutline;
        [Tooltip("TMP preset on the displayHeavyFont atlas: outline plus soft underlay for floating numbers.")] public Material numberOutline;
        [Tooltip("TMP preset on the displayHeavyFont atlas: light underlay below the face (engraved rune numerals).")] public Material engravedRune;
        [Tooltip("TMP preset on the displayFont atlas: thin outline for small labels on painted tabs.")] public Material labelOutline;

        [Header("Cards")]
        [Tooltip("Frame per CardCategory: Attack, Defence, Mobility, Support, Special.")] public Sprite[] cardFrames = new Sprite[5];
        [Tooltip("Frame while a card sits in an action socket (queued, locked during reveal, or staged as Playing).")] public Sprite[] socketCardFrames = new Sprite[5];
        [Tooltip("Painted contact shadow behind each card.")] public Sprite cardShadow;
        [Tooltip("Painted rim glow above the frame; tinted per state.")] public Sprite cardFocusRim;
        [Tooltip("Enlarged neutral frame for the inspect panel (category-agnostic fallback).")] public Sprite detailFrame;
        [Tooltip("Enlarged frame per CardCategory for the inspect panel; falls back to detailFrame.")] public Sprite[] detailFrames = new Sprite[5];
        [Tooltip("Category wash inside the detail art window (CardCategory order).")] public Sprite[] detailArt = new Sprite[5];
        [Tooltip("Footer glyph per CardCategory.")] public Sprite[] categoryGlyphs = new Sprite[5];
        [Tooltip("Full-size action icons in PreviewCardKind order: Attack, Defence, Dodge, Heal, Piercing, Combo, Counterattack, Charge, Miss.")]
        public Sprite[] icons = new Sprite[9];
        [Tooltip("96 px icons (thickened ink) in PreviewCardKind order: enemy intent, history.")] public Sprite[] smallIcons = new Sprite[9];
        public Color titleInk = Hex(0x2A1E14), bodyInk = Hex(0x2A1E14), footerText = Hex(0xEADCB8), mutedGold = Hex(0xC49E57);
        [Tooltip("Detail panel target-line ink, on parchment.")] public Color targetInk = Hex(0x5A3A28);
        public Color focusHover = Hex(0xE8C27A), focusSelected = Hex(0xD9B56E);
        [Tooltip("Drop-feedback rim while dragging over a valid socket (desaturated teal).")] public Color focusValid = Hex(0x6E9A94, .9f);
        [Tooltip("Drop-feedback rim while dragging over an invalid socket; kept away from the oxblood ATTACK frame hue.")] public Color focusInvalid = Hex(0xC8553D, .95f);

        [Header("Socket card mode (delta 2; installer-written from HudLayoutV4.json hand.socketCard)")]
        public Vector2 socketCardTitlePos, socketCardTitleSize = new Vector2(165, 26);
        public Vector2 socketCardIconPos = new Vector2(0, -47), socketCardIconSize = new Vector2(148, 148);
        public Vector2 socketCardGlyphPos = new Vector2(-75.5f, -131), socketCardGlyphSize = new Vector2(16, 16);
        [Tooltip("Title auto-size ceiling while socketed (card units); the hand value is restored when the card leaves the socket.")]
        public float socketCardTitleFontMax = 34f;

        [Header("Dense action slot row (3-4 slots; delta 1)")]
        [Tooltip("Base scale multiplier applied by CardActionSlot.Repaint on top of its state scale, from denseFromCount slots.")]
        public float denseSocketScale = .82f;
        public float denseSlotSpacing = 112f, denseSlotCenterX = -95f;
        public int denseFromCount = 3;

        [Header("Action sockets")]
        public Sprite socket;
        public Sprite socketRim, socketGlow;
        [Tooltip("Invalid-hover rim: kept a warm desaturated red, away from the oxblood ATTACK frame hue.")]
        public Color rimArmed = Hex(0xD9B56E), rimHoverValid = Hex(0xE8C27A), rimHoverInvalid = Hex(0xC8553D), rimQueued = Hex(0xA9864C);
        public Color rimLocked = Hex(0x4A361E, .8f), rimResolving = Hex(0xE09A3A), glowHoverValid = Hex(0x6E9A94), glowResolving = Hex(0xE09A3A);
        [Tooltip("Ink numeral on the parchment rune tab (idle) and warm dark amber (lit/armed).")]
        public Color runeIdle = Hex(0x3A2A1C), runeLit = Hex(0x7A3E12);
        [Tooltip("Socket tint while a drop there would be rejected (desaturated look).")] public Color socketInvalidTint = new Color(.72f, .72f, .72f);
        [Range(0, 1)] public float socketEmptyAlpha = 1f, socketLockedAlpha = .5f;
        [Range(0, 1)] [Tooltip("Action sockets fade to this alpha, no input, once the battle is terminal.")] public float socketTerminalAlpha = 0;
        [Tooltip("The rune numeral uses the engraved preset by default; disable if the light underlay clashes with the parchment tab.")]
        public bool runeUseEngravedMaterial = true;

        [Header("Commit")]
        public Sprite commitDisabled;
        public Sprite commitReady, commitHover, commitPressed, commitLocked, commitResolving;
        public Color commitLabelReady = Hex(0xF3E9D2), commitLabelIdle = Hex(0x8E7A55), commitLabelBusy = Hex(0xB9A57E);
        public float hoverScale = 1.03f, pressedScale = .97f;

        [Header("Crest and static HUD")]
        public Sprite playerHPTrack;
        public Sprite playerHPFill, playerHPGhost, chipBlock, chipDodge, floorPlaque, actionMedallion, pipOn, pipOff, drawToken, discardToken;
        public Sprite banner, terminalBanner, vignette, textWash;
        [Tooltip("Optional; null falls back to a tinted banner.")] public Sprite smallButton;
        [Tooltip("Optional; null keeps the V3 selection bar.")] public Sprite targetMarker;
        [Tooltip("Target marker tint: valid target (the painted gold as-is) and the currently selected target.")]
        public Color targetMarkerValid = Color.white, targetMarkerSelected = Hex(0xFFE9B8);
        public float targetMarkerSelectedScale = 1.2f;
        [Tooltip("Optional; null shows a small display \"X\".")] public Sprite closeButton;
        [Tooltip("Order: damage burst, pierce, block shield, dodge boot, heal heart, charge clock, defeat (see FeedbackGlyph).")]
        public Sprite[] feedbackGlyphs = new Sprite[7];
        public Color ghostColor = Hex(0xE0A08A), victoryColor = Hex(0xD9B56E), defeatColor = Hex(0xA8302A);
        public Color parchmentText = Hex(0xEADCB8), hintText = Hex(0xD6C29A);
        [Tooltip("Floating damage number colour: ivory when the enemy takes the hit.")] public Color floaterEnemyDamage = Hex(0xEFE3C8);
        [Tooltip("Floating damage number colour: warm red when the player takes the hit.")] public Color floaterPlayerDamage = Hex(0xC9482F);

        [Header("Enemy HUD")]
        public Sprite enemyHPTrack;
        public Sprite enemyHPFill, intentSocket, intentUnknown, revealFlash, historyEye;
        [Tooltip("Parchment disc painted behind each observed-history icon.")] public Sprite historyToken;
        [Range(0, 1)] public float historyIconAlpha = .6f;

        [Header("Art controls")]
        [Range(0, 1)] public float shadowIntensity = .55f;
        [Range(0, 1)] public float highlightStrength = .85f;
        [Tooltip("Card shadow offset at rest; the painted shadow already carries its own downward offset.")] public Vector2 shadowIdleOffset = Vector2.zero;
        [Tooltip("Card shadow offset while hovered, selected or dragged.")] public Vector2 shadowLiftedOffset = new Vector2(2, -8);
        [Tooltip("Hand cards that cannot be played (reveal/resolution) are darkened with this tint instead of made translucent, so overlapping fan cards never show through each other.")]
        public Color cardDisabledTint = new Color(.6f, .58f, .56f, 1);
        public float revealFlipSeconds = .3f, defeatSinkSeconds = .8f;
        [Tooltip("Sink distance as a multiple of the enemy renderer bounds height; below 1 the darkened body stays visible as the defeated state.")] public float defeatSinkDepth = .45f;
        public float defeatTiltDegrees = 10f;
        [Range(0, 1)] public float defeatDarken = .55f;
        [Tooltip("Fog-like cool dark the defeated model is darkened toward.")] public Color defeatTint = Hex(0x28343A);

        [Header("Runtime-built panels (written by the V4 installer from HudLayoutV4.json)")]
        public PanelPlacement detailPanel = new PanelPlacement(new Vector2(0, 1), new Vector2(0, 1), new Vector2(196, -146), new Vector2(360, 480));
        public PanelPlacement targetingBanner = new PanelPlacement(new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -196), new Vector2(600, 76));
        public PanelPlacement terminalPanel = new PanelPlacement(new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(640, 190));
        public PanelPlacement inspectHint = new PanelPlacement(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(320, 26));
        public PanelPlacement inspectWash = new PanelPlacement(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(340, 28));
        [Range(0, 1)] public float vignetteAlpha = .35f;
        public float terminalTitleSize = 54, terminalBodySize = 20;

        [Header("Targeting ribbon buttons (installer-written from HudLayoutV4.json targeting_banner.buttons)")]
        public Vector2 targetingButtonSize = new Vector2(110, 30);
        public float targetingButtonGap = 8;

        [Header("Detail panel zones (installer-written from HudLayoutV4.json detail.zones; 360x480 basis)")]
        public Vector2 detailTitlePos, detailTitleSize = new Vector2(264, 42);
        public Vector2 detailArtPos, detailArtSize = new Vector2(296, 221);
        public Vector2 detailIconPos, detailIconSize = new Vector2(200, 200);
        public Vector2 detailTargetPos, detailTargetSize = new Vector2(300, 20);
        public Vector2 detailEffectPos, detailEffectSize = new Vector2(300, 130);
        public Vector2 detailFooterLabelPos, detailFooterLabelSize = new Vector2(200, 18);
        public Vector2 detailFooterGlyphPos, detailFooterGlyphSize = new Vector2(24, 24);
        public Vector2 detailClosePos = new Vector2(158, 232);

        public Sprite CardFrame(PreviewCardKind kind) => At(cardFrames, (int)CardCategoryMap.Category((int)kind));
        public Sprite SocketFrame(PreviewCardKind kind) => At(socketCardFrames, (int)CardCategoryMap.Category((int)kind));
        public Sprite DetailFrame(PreviewCardKind kind) { var s = At(detailFrames, (int)CardCategoryMap.Category((int)kind)); return s ? s : detailFrame; }
        public Sprite DetailArt(PreviewCardKind kind) => At(detailArt, (int)CardCategoryMap.Category((int)kind));
        public Sprite CategoryGlyph(PreviewCardKind kind) => At(categoryGlyphs, CardCategoryMap.GlyphIndex((int)kind));
        public Sprite Icon(PreviewCardKind kind) => At(icons, (int)kind);
        /// <summary>Small icon, falling back to the full-size icon.</summary>
        public Sprite SmallIcon(PreviewCardKind kind) { var small = At(smallIcons, (int)kind); return small ? small : Icon(kind); }
        public Sprite FeedbackIcon(FeedbackGlyph glyph) => At(feedbackGlyphs, (int)glyph);

        public Sprite CommitSprite(CommitVisualState state)
        {
            switch (state)
            {
                case CommitVisualState.Ready: return commitReady;
                case CommitVisualState.Hover: return commitHover ? commitHover : commitReady;
                case CommitVisualState.Pressed: return commitPressed ? commitPressed : commitReady;
                case CommitVisualState.Locked:
                case CommitVisualState.Complete: return commitLocked ? commitLocked : commitDisabled;
                case CommitVisualState.Resolving: return commitResolving ? commitResolving : commitLocked;
                default: return commitDisabled;
            }
        }

        /// <summary>Sets the font and applies <paramref name="preset"/> only when it samples that font's atlas; otherwise the font's own material.</summary>
        public static void SetFont(TMP_Text text, TMP_FontAsset font, Material preset = null)
        {
            if (!text) return;
            if (font && text.font != font) text.font = font;
            var current = text.font;
            if (!current) return;
            text.fontSharedMaterial = preset && preset.mainTexture && preset.mainTexture == current.atlasTexture ? preset : current.material;
        }

        /// <summary>Packs the BLOCK/DODGE chips from the left: a lone DODGE chip takes the BLOCK chip's slot.</summary>
        public static void PackChips(GameObject block, GameObject dodge)
        {
            if (!block || !dodge) return;
            var slot = (RectTransform)block.transform;
            ((RectTransform)dodge.transform).anchoredPosition =
                slot.anchoredPosition + (block.activeSelf ? new Vector2(slot.sizeDelta.x + 4, 0) : Vector2.zero);
        }

        private static Sprite At(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        private static Color Hex(uint rgb, float alpha = 1) =>
            new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
}
