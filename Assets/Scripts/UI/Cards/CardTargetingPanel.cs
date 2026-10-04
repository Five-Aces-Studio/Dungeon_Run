using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Nonmodal shared-target / explicit per-hit control; never calculates combat.</summary>
    public sealed class CardTargetingPanel : MonoBehaviour
    {
        private TextMeshProUGUI prompt, toggleLabel;
        private Button toggle;
        private Action<bool> changeMode;
        private bool perHit;
        public void Bind(Action<bool> change, UnityEngine.Events.UnityAction cancel)
        { changeMode = change; cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(cancel); }
        private Button cancelButton;
        public static CardTargetingPanel Create(Transform parent, CombatHUDTheme theme)
        {
            var style = theme ? theme.v4Style : null;
            if (style) return CreateV4(parent, style);
            var rect = CardDetailPanel.Rect("CardTargetingV3", parent, new Vector2(650, 90));
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -240);
            var panel = rect.gameObject.AddComponent<CardTargetingPanel>();
            rect.gameObject.AddComponent<Image>().color = new Color(.04f, .07f, .08f, .9f);
            panel.prompt = CardDetailPanel.Text("Prompt", rect, new Vector2(-90, -12), new Vector2(430, 76), theme.bodyFont, 20);
            panel.prompt.alignment = TextAlignmentOptions.Center;
            panel.toggle = CardDetailPanel.Button("HitMode", rect, new Vector2(224, -8), new Vector2(172, 34), "Split hits", theme,
                () => panel.changeMode?.Invoke(!panel.perHit));
            panel.toggleLabel = panel.toggle.GetComponentInChildren<TextMeshProUGUI>();
            panel.cancelButton = CardDetailPanel.Button("Cancel", rect, new Vector2(224, -48), new Vector2(172, 30), "Cancel", theme, () => {});
            rect.gameObject.SetActive(false); return panel;
        }
        /// <summary>V4: a parchment ribbon with the prompt, and CANCEL / SPLIT HITS chips on its own row, to its right.</summary>
        private static CardTargetingPanel CreateV4(Transform parent, CombatHUDStyleV4 style)
        {
            var rect = CardDetailPanel.Rect("CardTargetingV4", parent, Vector2.zero); style.targetingBanner.Apply(rect);
            var panel = rect.gameObject.AddComponent<CardTargetingPanel>();
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = style.banner;
            image.type = style.banner && style.banner.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = style.banner ? Color.white : new Color(.04f, .07f, .08f, .9f);
            // Ink text on parchment, centred on the ribbon: the plain font material (an outline preset reads muddy on light parchment).
            var promptRect = CardDetailPanel.Rect("Prompt", rect, new Vector2(rect.sizeDelta.x * .78f, rect.sizeDelta.y * .8f));
            promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(.5f, .5f);
            panel.prompt = promptRect.gameObject.AddComponent<TextMeshProUGUI>();
            CombatHUDStyleV4.SetFont(panel.prompt, style.displayFont); panel.prompt.color = style.titleInk;
            panel.prompt.fontSize = 18; panel.prompt.raycastTarget = false;
            panel.prompt.textWrappingMode = TextWrappingModes.Normal;
            panel.prompt.alignment = TextAlignmentOptions.Center;
            panel.prompt.enableAutoSizing = true; panel.prompt.fontSizeMin = 12; panel.prompt.fontSizeMax = 18;
            // CANCEL / SPLIT HITS chips on the ribbon's own row, to its right (not a second row).
            var chipSize = style.targetingButtonSize; float gap = style.targetingButtonGap;
            float chipX = rect.sizeDelta.x * .5f + gap + chipSize.x * .5f;
            panel.cancelButton = ButtonV4("Cancel", rect, new Vector2(chipX, 0), chipSize, "Cancel", style, () => {});
            chipX += chipSize.x + gap;
            panel.toggle = ButtonV4("HitMode", rect, new Vector2(chipX, 0), chipSize, "Split hits", style,
                () => panel.changeMode?.Invoke(!panel.perHit));
            panel.toggleLabel = panel.toggle.GetComponentInChildren<TextMeshProUGUI>();
            rect.gameObject.SetActive(false); return panel;
        }
        private static Button ButtonV4(string name, Transform parent, Vector2 position, Vector2 size, string caption, CombatHUDStyleV4 style,
            UnityEngine.Events.UnityAction action)
        {
            var rect = CardDetailPanel.Rect(name, parent, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = style.smallButton ? style.smallButton : style.banner;
            image.type = !style.smallButton && style.banner && style.banner.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = style.smallButton ? Color.white : style.banner ? new Color(.78f, .78f, .78f, 1) : new Color(.14f, .2f, .2f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var text = CardDetailPanel.Text("Label", rect, Vector2.zero, size, style.displayFont, 14);
            CombatHUDStyleV4.SetFont(text, style.displayFont); text.color = style.titleInk;
            text.text = caption; text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        public void Render(CombatHUDSnapshot state)
        {
            bool visible = state.TargetingCardId >= 0 || !string.IsNullOrEmpty(state.TargetingPrompt);
            gameObject.SetActive(visible && !state.IsResolving);
            if (!visible) return;
            prompt.text = state.TargetingPrompt; perHit = state.TargetingPerHit;
            toggle.gameObject.SetActive(state.TargetingHitCount > 1);
            toggleLabel.text = perHit ? "Use shared target" : "Split hits";
        }
    }
}
