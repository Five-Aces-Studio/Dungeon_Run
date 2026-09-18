using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    public sealed class EnemyCombatHUD : MonoBehaviour, IPointerClickHandler
    {
        public TextMeshProUGUI healthText, intentText;
        public Image healthFill, selection;
        public CanvasGroup group;
        public Vector2 screenOffset = new Vector2(0, 46);
        private EnemyHUDSnapshot snapshot;
        private ICombatHUDSource source;
        private Camera worldCamera;
        private RectTransform canvas;
        private float intentBottom;
        public void Bind(ICombatHUDSource data, Camera camera, RectTransform canvasRoot)
        {
            source = data; worldCamera = camera; canvas = canvasRoot;
            var rect = intentText.rectTransform;
            intentBottom = rect.anchoredPosition.y - rect.sizeDelta.y * rect.pivot.y;
            intentText.textWrappingMode = TextWrappingModes.Normal;
            intentText.enableAutoSizing = true;
            intentText.fontSizeMin = 12;
            intentText.fontSizeMax = 16;
            intentText.overflowMode = TextOverflowModes.Ellipsis;
        }
        public void Render(EnemyHUDSnapshot state, bool selected)
        {
            snapshot = state;
            healthText.text = state.Health + " / " + state.MaxHealth +
                (state.Block > 0 ? "  B " + state.Block : "") + (state.Dodge > 0 ? "  D " + state.Dodge : "");
            // Keep the original single-action footprint. Multi-action labels grow upward, away from HP.
            string intent = state.Intent.Replace(" + ", "\n");
            int lines = intent.Split('\n').Length;
            var intentRect = intentText.rectTransform;
            float height = 24 * Mathf.Clamp(lines, 1, 4);
            intentRect.sizeDelta = new Vector2(intentRect.sizeDelta.x, height);
            intentRect.anchoredPosition = new Vector2(intentRect.anchoredPosition.x, intentBottom + height * intentRect.pivot.y);
            intentText.text = intent;
            healthFill.fillAmount = state.MaxHealth <= 0 ? 0 : state.Health / (float)state.MaxHealth;
            selection.enabled = selected;
            Project();
        }
        private void LateUpdate() => Project();
        private void Project()
        {
            if (snapshot == null || !snapshot.Anchor || !worldCamera || !canvas) return;
            var bounds = snapshot.Anchor.bounds;
            Vector3 p = worldCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            bool visible = p.z > 0 && snapshot.Anchor.gameObject.activeInHierarchy;
            group.alpha = visible ? 1 : 0; group.blocksRaycasts = visible;
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, p, null, out var local);
            ((RectTransform)transform).anchoredPosition = local + screenOffset;
        }
        public void OnPointerClick(PointerEventData e) { if (snapshot != null) source?.TrySelectTarget(snapshot.Id); }
    }
}
