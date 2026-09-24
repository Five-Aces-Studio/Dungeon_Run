using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.Combat;
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
        private CombatHUDTheme theme;
        private TextMeshProUGUI historyText;
        private readonly List<Image> icons = new List<Image>();
        private readonly Queue<string> history = new Queue<string>();
        private int revealedCount, lastHealth = -1;
        private bool instant, planning;
        public int ActorId => snapshot == null ? -1 : snapshot.Id;
        public void Bind(ICombatHUDSource data, Camera camera, RectTransform canvasRoot)
        {
            source = data; worldCamera = camera; canvas = canvasRoot;
            theme = GetComponentInParent<CombatHUD>().theme;
            intentText.textWrappingMode = TextWrappingModes.Normal;
            intentText.enableAutoSizing = true; intentText.fontSizeMin = 12; intentText.fontSizeMax = 16;
            intentText.overflowMode = TextOverflowModes.Ellipsis;
            if (Application.isPlaying && !historyText)
            {
                historyText = CombatFeedbackPresenter.Label("ObservedHistory", transform, new Vector2(0, -48), new Vector2(210, 22), 12, theme);
                historyText.color = new Color(.72f, .72f, .65f);
                for (int i = 0; i < 4; i++)
                {
                    var rect = CombatFeedbackPresenter.Rect("RevealedAction" + i, transform, new Vector2(-94, 29 + i * 24), new Vector2(21, 21));
                    var icon = rect.gameObject.AddComponent<Image>(); icon.raycastTarget = false;
                    icons.Add(icon); rect.gameObject.SetActive(false);
                }
            }
        }
        public void SetPresentation(bool isPlanning, bool instantAnimations)
        {
            instant = instantAnimations;
            if (isPlanning && !planning) { revealedCount = 0; foreach (var icon in icons) icon.gameObject.SetActive(false); }
            planning = isPlanning;
        }
        public void Observe(ActionDefinition action)
        {
            if (action == null) return;
            history.Enqueue(action.Name); while (history.Count > 2) history.Dequeue();
            if (historyText) historyText.text = "SEEN: " + string.Join(" / ", history);
            if (revealedCount < icons.Count)
            {
                var icon = icons[revealedCount++];
                icon.sprite = theme.Icon(CombatFeedbackPresenter.Icon(action.Icon));
                icon.color = theme.text; icon.gameObject.SetActive(icon.sprite != null);
                icon.rectTransform.DOKill(); icon.rectTransform.localScale = Vector3.one;
                if (!instant) { icon.rectTransform.localScale = new Vector3(.1f, 1, 1); icon.rectTransform.DOScaleX(1, theme.revealTweenDuration).SetEase(Ease.OutCubic).SetUpdate(true); }
            }
            if (!instant) UIAnimationHelpers.Pulse(intentText.rectTransform, false);
        }
        public void Render(EnemyHUDSnapshot state, bool selected)
        {
            snapshot = state;
            healthText.text = state.Health + " / " + state.MaxHealth +
                (state.Block > 0 ? "  BLOCK " + state.Block : "") + (state.Dodge > 0 ? "  DODGE " + state.Dodge : "");
            string intent = state.Health <= 0 ? "DEFEATED" : state.Intent.Replace(" + ", "\n");
            int lines = intent.Split('\n').Length;
            var rect = intentText.rectTransform;
            float height = 24 * Mathf.Clamp(lines, 1, 4);
            rect.sizeDelta = new Vector2(160, height);
            rect.anchoredPosition = new Vector2(0, 15 + height * .5f);
            intentText.text = intent;
            if (lastHealth != state.Health)
            {
                healthFill.DOKill(); float fill = state.MaxHealth <= 0 ? 0 : state.Health / (float)state.MaxHealth;
                if (instant || lastHealth < 0 || !Application.isPlaying) healthFill.fillAmount = fill;
                else healthFill.DOFillAmount(fill, theme.healthTweenDuration).SetUpdate(true);
                lastHealth = state.Health;
            }
            selection.enabled = state.Health > 0 && (selected || state.IsValidTarget);
            selection.color = selected ? theme.gold : theme.validTargetColor;
            selection.rectTransform.sizeDelta = new Vector2(selected ? 120 : 70, selected ? 3 : 2);
            if (state.Health <= 0) foreach (var icon in icons) icon.gameObject.SetActive(false);
            Project();
        }
        private void LateUpdate() => Project();
        private void Project()
        {
            if (snapshot == null || !snapshot.Anchor || !worldCamera || !canvas) return;
            var bounds = snapshot.Anchor.bounds;
            Vector3 p = worldCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            bool visible = p.z > 0 && snapshot.Anchor.gameObject.activeInHierarchy;
            group.alpha = visible ? snapshot.Health > 0 ? 1 : .4f : 0;
            group.blocksRaycasts = visible && snapshot.Health > 0 && planning;
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, p, null, out var local);
            ((RectTransform)transform).anchoredPosition = local + screenOffset;
        }
        public void OnPointerClick(PointerEventData e) { if (snapshot != null && snapshot.Health > 0 && planning) source?.TrySelectTarget(snapshot.Id); }
        private void OnDestroy() { if (healthFill) healthFill.DOKill(); if (intentText) intentText.rectTransform.DOKill(); foreach (var icon in icons) if (icon) icon.rectTransform.DOKill(); }
    }
}
