using System;
using DungeonRun.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Fixed, deliberate inspection panel. Full text scrolls instead of shrinking or clipping.</summary>
    public sealed class CardDetailPanel : MonoBehaviour
    {
        private TextMeshProUGUI heading, effect, targeting;
        private Image icon;
        private ScrollRect scroll;
        private RectTransform content;
        public static CardDetailPanel Create(Transform parent, CombatHUDTheme theme)
        {
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
            hint.text = "Scroll for full effect · Middle-click to inspect";
            Button("Close", root, new Vector2(142, -15), new Vector2(38, 30), "X", theme, panel.Hide);
            panel.Hide(); return panel;
        }
        public void Show(CombatCardItem item, CombatHUDTheme theme)
        {
            gameObject.SetActive(true); transform.SetAsLastSibling();
            heading.text = item.Definition.cardName;
            icon.sprite = item.Definition.illustration ? item.Definition.illustration : theme.Icon(item.Kind);
            icon.enabled = icon.sprite;
            targeting.text = TargetLabel(item.Definition.targetMode) + (item.Definition.hitCount > 1 ? " · " + item.Definition.hitCount + " hits" : "");
            effect.text = item.Definition.description;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(140, effect.GetPreferredValues(effect.text, 302, 0).y + 12));
            scroll.verticalNormalizedPosition = 1;
        }
        public void Hide() => gameObject.SetActive(false);
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
