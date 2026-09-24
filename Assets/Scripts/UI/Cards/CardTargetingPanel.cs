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
