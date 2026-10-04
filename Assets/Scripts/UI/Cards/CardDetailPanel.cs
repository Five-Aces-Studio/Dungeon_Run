using System;
using DungeonRun.Combat;
using DungeonRun.UI.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Fixed, deliberate inspection panel. Full text scrolls instead of shrinking or clipping.</summary>
    public sealed class CardDetailPanel : MonoBehaviour
    {
        private TextMeshProUGUI heading, effect, targeting, hint;
        private Image icon, frameImage;
        private ScrollRect scroll;
        private RectTransform content;
        private Image art, footerGlyph;
        private TextMeshProUGUI footerLabel;
        private CombatHUDStyleV4 v4;
        public static CardDetailPanel Create(Transform parent, CombatHUDTheme theme)
        {
            var style = theme ? theme.v4Style : null;
            if (style) return CreateV4(parent, theme, style);
            var root = Rect("CardDetailV3", parent, new Vector2(350, 430));
            root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(230, -190);
            var background = root.gameObject.AddComponent<Image>(); background.color = theme.panel;
            var panel = root.gameObject.AddComponent<CardDetailPanel>();
            panel.heading = Text("Title", root, new Vector2(0, -24), new Vector2(292, 52), theme.displayFont, 24);
            panel.heading.alignment = TextAlignmentOptions.TopLeft;
            var iconRect = Rect("Icon", root, new Vector2(96, 96)); Top(iconRect, new Vector2(0, -88));
            panel.icon = iconRect.gameObject.AddComponent<Image>(); panel.icon.preserveAspect = true; panel.icon.raycastTarget = false;
            panel.targeting = Text("Targeting", root, new Vector2(0, -188), new Vector2(302, 42), theme.bodyFont, 17);
            var viewport = Rect("EffectViewport", root, new Vector2(302, 140)); Top(viewport, new Vector2(0, -238));
            viewport.gameObject.AddComponent<RectMask2D>();
            panel.scroll = viewport.gameObject.AddComponent<ScrollRect>(); panel.scroll.viewport = viewport;
            panel.scroll.horizontal = false; panel.scroll.movementType = ScrollRect.MovementType.Clamped;
            panel.content = Rect("Content", viewport, new Vector2(302, 140)); Top(panel.content, Vector2.zero);
            panel.effect = panel.content.gameObject.AddComponent<TextMeshProUGUI>();
            panel.effect.font = theme.bodyFont; panel.effect.fontSize = 21; panel.effect.color = theme.text;
            panel.effect.textWrappingMode = TextWrappingModes.Normal; panel.effect.overflowMode = TextOverflowModes.Overflow;
            panel.effect.alignment = TextAlignmentOptions.TopLeft; panel.scroll.content = panel.content;
            var hint = Text("Hint", root, new Vector2(0, -395), new Vector2(302, 24), theme.bodyFont, 14);
            hint.text = "Scroll for full effect \u00B7 Middle-click to inspect";
            Button("Close", root, new Vector2(142, -15), new Vector2(38, 30), "X", theme, panel.Hide);
            panel.Hide(); return panel;
        }
        public void Show(CombatCardItem item, CombatHUDTheme theme)
        {
            if (v4) { ShowV4(item, theme); return; }
            gameObject.SetActive(true); transform.SetAsLastSibling();
            heading.text = item.Definition.cardName;
            icon.sprite = item.Definition.illustration ? item.Definition.illustration : theme.Icon(item.Kind);
            icon.enabled = icon.sprite;
            targeting.text = TargetLabel(item.Definition.targetMode) + (item.Definition.hitCount > 1 ? " \u00B7 " + item.Definition.hitCount + " hits" : "");
            effect.text = item.Definition.description;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(140, effect.GetPreferredValues(effect.text, 302, 0).y + 12));
            scroll.verticalNormalizedPosition = 1;
        }
        public void Hide() { gameObject.SetActive(false); if (hint) hint.gameObject.SetActive(false); }
        /// <summary>V4: an enlarged illustrated card, framed per category, laid out entirely from HudLayoutV4.json detail.zones (style fields); same scroll structure as V3.</summary>
        private static CardDetailPanel CreateV4(Transform parent, CombatHUDTheme theme, CombatHUDStyleV4 style)
        {
            var root = Rect("CardDetailV4", parent, Vector2.zero); style.detailPanel.Apply(root);
            var frame = root.gameObject.AddComponent<Image>(); frame.color = Color.white;
            var panel = root.gameObject.AddComponent<CardDetailPanel>(); panel.v4 = style; panel.frameImage = frame;
            // Zones are written by the installer from the 360x480 detail.zones basis; kx/ky rescale if the panel size ever differs.
            float kx = root.sizeDelta.x / 360f, ky = root.sizeDelta.y / 480f;
            RectTransform Zone(string name, Vector2 pos, Vector2 size)
            {
                var rect = Rect(name, root, Vector2.Scale(size, new Vector2(kx, ky)));
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.Scale(pos, new Vector2(kx, ky));
                return rect;
            }
            panel.art = Zone("Art", style.detailArtPos, style.detailArtSize).gameObject.AddComponent<Image>(); panel.art.raycastTarget = false;
            panel.icon = Zone("Icon", style.detailIconPos, style.detailIconSize).gameObject.AddComponent<Image>();
            panel.icon.preserveAspect = true; panel.icon.raycastTarget = false;
            panel.heading = Zone("Title", style.detailTitlePos, style.detailTitleSize).gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.heading, style.displayHeavyFont); panel.heading.fontSize = 26; panel.heading.color = style.titleInk;
            panel.heading.alignment = TextAlignmentOptions.Center; panel.heading.enableAutoSizing = true;
            panel.heading.fontSizeMin = 16; panel.heading.fontSizeMax = 26; panel.heading.raycastTarget = false;
            panel.targeting = Zone("Targeting", style.detailTargetPos, style.detailTargetSize).gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.targeting, style.bodyFont); panel.targeting.fontSize = 16; panel.targeting.color = style.targetInk;
            panel.targeting.alignment = TextAlignmentOptions.Center; panel.targeting.raycastTarget = false;
            var viewport = Zone("EffectViewport", style.detailEffectPos, style.detailEffectSize);
            viewport.gameObject.AddComponent<RectMask2D>();
            panel.scroll = viewport.gameObject.AddComponent<ScrollRect>(); panel.scroll.viewport = viewport;
            panel.scroll.horizontal = false; panel.scroll.movementType = ScrollRect.MovementType.Clamped;
            panel.content = Rect("Content", viewport, viewport.sizeDelta); Top(panel.content, Vector2.zero);
            panel.effect = panel.content.gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.effect, style.bodyFont); panel.effect.fontSize = 25; panel.effect.color = style.bodyInk;
            panel.effect.textWrappingMode = TextWrappingModes.Normal; panel.effect.overflowMode = TextOverflowModes.Overflow;
            // Content is never shorter than the viewport, so short effects sit centred and long ones still start at the top.
            panel.effect.alignment = TextAlignmentOptions.Center; panel.scroll.content = panel.content;
            panel.footerGlyph = Zone("FooterGlyph", style.detailFooterGlyphPos, style.detailFooterGlyphSize).gameObject.AddComponent<Image>();
            panel.footerGlyph.preserveAspect = true; panel.footerGlyph.raycastTarget = false;
            panel.footerLabel = Zone("FooterLabel", style.detailFooterLabelPos, style.detailFooterLabelSize).gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.footerLabel, style.displayFont, style.labelOutline); panel.footerLabel.fontSize = 14;
            panel.footerLabel.color = style.footerText; panel.footerLabel.alignment = TextAlignmentOptions.Center; panel.footerLabel.raycastTarget = false;
            var closeRect = Zone("Close", style.detailClosePos, new Vector2(32, 32));
            var closeImage = closeRect.gameObject.AddComponent<Image>(); closeImage.sprite = style.closeButton;
            closeImage.color = style.closeButton ? Color.white : new Color(.14f, .2f, .2f, 1);
            var close = closeRect.gameObject.AddComponent<Button>(); close.targetGraphic = closeImage; close.onClick.AddListener(panel.Hide);
            if (!style.closeButton)
            {
                var x = Text("Label", closeRect, Vector2.zero, closeRect.sizeDelta, style.displayFont, 16);
                x.text = "X"; x.alignment = TextAlignmentOptions.Center; x.color = style.parchmentText;
            }
            // Scroll hint: a sibling of the panel (not inside the card), shown only when the effect text overflows the viewport.
            var hintRect = Rect("HintV4", parent, new Vector2(root.sizeDelta.x, 20));
            hintRect.anchorMin = hintRect.anchorMax = root.anchorMin; hintRect.pivot = new Vector2(.5f, 1);
            hintRect.anchoredPosition = new Vector2(root.anchoredPosition.x + root.sizeDelta.x * (.5f - root.pivot.x),
                root.anchoredPosition.y - root.sizeDelta.y * root.pivot.y - 6);
            panel.hint = hintRect.gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.hint, style.bodyFont); panel.hint.fontSize = 12;
            var hintColor = style.hintText; hintColor.a = .7f; panel.hint.color = hintColor;
            panel.hint.alignment = TextAlignmentOptions.Center; panel.hint.raycastTarget = false;
            panel.hint.text = "Scroll for full effect" + HudStatusFormat.Separator + "Middle-click to inspect";
            panel.hint.gameObject.SetActive(false);
            panel.Hide(); return panel;
        }
        private void ShowV4(CombatCardItem item, CombatHUDTheme theme)
        {
            gameObject.SetActive(true); transform.SetAsLastSibling();
            if (frameImage)
            {
                var frameSprite = v4.DetailFrame(item.Kind);
                frameImage.sprite = frameSprite; frameImage.type = Image.Type.Simple;
                frameImage.color = frameSprite ? Color.white : theme.panel;
            }
            heading.text = item.Definition.cardName;
            art.sprite = v4.DetailArt(item.Kind); art.enabled = art.sprite;
            var sprite = item.Definition.illustration ? item.Definition.illustration : v4.Icon(item.Kind);
            icon.sprite = sprite ? sprite : theme.Icon(item.Kind); icon.enabled = icon.sprite;
            targeting.text = TargetLabel(item.Definition.targetMode) +
                (item.Definition.hitCount > 1 ? HudStatusFormat.Separator + item.Definition.hitCount + " hits" : "");
            effect.text = item.Definition.description;
            float width = content.sizeDelta.x, viewportHeight = scroll.viewport.sizeDelta.y;
            float textHeight = effect.GetPreferredValues(effect.text, width, 0).y + 12;
            content.sizeDelta = new Vector2(width, Mathf.Max(viewportHeight, textHeight));
            scroll.verticalNormalizedPosition = 1;
            bool overflow = textHeight > viewportHeight + .5f;
            scroll.enabled = overflow;
            if (hint) hint.gameObject.SetActive(overflow);
            footerGlyph.sprite = v4.CategoryGlyph(item.Kind); footerGlyph.enabled = footerGlyph.sprite;
            footerLabel.text = CardCategoryMap.FooterLabel((int)item.Kind);
        }
        public static string TargetLabel(TargetMode target)
        {
            switch (target)
            {
                case TargetMode.Self: return "Target: self";
                case TargetMode.FriendlyTarget: return "Target: ally";
                case TargetMode.SingleOpponent: return "Target: one enemy";
                case TargetMode.AllOpponents: return "Target: all enemies";
                case TargetMode.AllCombatants: return "Target: all combatants";
                default: return "No target required";
            }
        }
        internal static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; return rect;
        }
        internal static void Top(RectTransform rect, Vector2 position)
        { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = position; }
        internal static TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, TMP_FontAsset font, int fontSize)
        {
            var rect = Rect(name, parent, size); Top(rect, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize;
            text.color = new Color(.92f, .87f, .75f); text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false; return text;
        }
        internal static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string caption, CombatHUDTheme theme, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, size); Top(rect, position);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.14f, .2f, .2f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var text = Text("Label", rect, Vector2.zero, size, theme.bodyFont, 16); text.text = caption; text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
