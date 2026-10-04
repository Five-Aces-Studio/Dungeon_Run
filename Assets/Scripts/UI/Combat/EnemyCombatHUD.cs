using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.Combat;
using DungeonRun.UI.Presentation;
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
        [Tooltip("V4: HP track rect, used to anchor floating damage numbers (unused by V1/V3).")] public RectTransform healthTrack;
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
        /// <summary>World renderer this view is projected from (presentation anchor only).</summary>
        public Renderer Anchor => snapshot?.Anchor;
        [Header("V4 painted enemy HUD (unused by V1/V3 themes)")]
        public TextMeshProUGUI nameText, intentLabel;
        public Image intentSocket, revealFlash, healthGhost;
        [Tooltip("Icon 0 sits inside the intent socket; the rest form a compact row for multi-action intents.")] public Image[] intentIcons = new Image[0];
        [Tooltip("Past observed actions as ghosted small icons; their parent row also holds the eye glyph.")] public Image[] historyIcons = new Image[0];
        [Tooltip("Parchment disc behind each history icon (style.historyToken), same length as historyIcons.")] public Image[] historyTokens = new Image[0];
        public GameObject blockChip, dodgeChip;
        public TextMeshProUGUI blockChipText, dodgeChipText;
        private CombatHUDStyleV4 style;
        private readonly List<PreviewCardKind> revealedKinds = new List<PreviewCardKind>(), historyKinds = new List<PreviewCardKind>();
        private bool frozen, frozenVisible, intentFadedV4;
        private Vector2 markerBasePos; private bool markerBobbing, markerBaseCaptured;
        public void Bind(ICombatHUDSource data, Camera camera, RectTransform canvasRoot)
        {
            source = data; worldCamera = camera; canvas = canvasRoot;
            theme = GetComponentInParent<CombatHUD>().theme;
            style = theme ? theme.v4Style : null;
            if (style) { ResetPresentation(); return; }
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
            if (style && isPlanning && !planning) BeginTurnV4();
            planning = isPlanning;
        }
        public void Observe(ActionDefinition action)
        {
            if (action == null) return;
            if (style) { ObserveV4(action); return; }
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
            if (style) { RenderV4(state, selected); return; }
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
            if (style && snapshot != null && snapshot.Health <= 0 && ProjectDefeatedV4()) return;
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
        /// <summary>Clears observed history and revealed-intent visuals for a fresh battle. Presentation only.</summary>
        public void ResetPresentation()
        {
            history.Clear();
            if (historyText) historyText.text = string.Empty;
            if (!style) return;
            revealedKinds.Clear(); historyKinds.Clear(); frozen = false; lastHealth = -1;
            ResetIntentV4(); RefreshHistoryV4();
        }
        private void BeginTurnV4()
        {
            // Only past turns feed the history row; the current intent lives in the socket.
            historyKinds.AddRange(revealedKinds);
            while (historyKinds.Count > 3) historyKinds.RemoveAt(0);
            revealedKinds.Clear();
            ResetIntentV4(); RefreshHistoryV4();
        }
        private void ObserveV4(ActionDefinition action)
        {
            bool first = revealedKinds.Count == 0;
            revealedKinds.Add(CombatFeedbackPresenter.Icon(action.Icon));
            if (intentSocket && first)
            {
                var rect = intentSocket.rectTransform; rect.DOKill(); rect.localScale = Vector3.one;
                if (instant) SetRevealedSocketV4();
                else
                {
                    float half = style.revealFlipSeconds * .5f;
                    DOTween.Sequence().SetTarget(rect).SetUpdate(true)
                        .Append(rect.DOScaleX(0, half).SetEase(Ease.InCubic))
                        .AppendCallback(SetRevealedSocketV4)
                        .Append(rect.DOScaleX(1, half).SetEase(Ease.OutCubic));
                }
            }
            RefreshIntentIconsV4();
            if (intentLabel)
            {
                intentLabel.DOKill(); intentLabel.enabled = true; intentLabel.alpha = instant ? 1 : 0;
                if (!instant) intentLabel.DOFade(1, style.revealFlipSeconds).SetDelay(style.revealFlipSeconds * .5f).SetUpdate(true);
            }
            if (revealFlash && !instant)
            {
                var flash = revealFlash; var flashRect = flash.rectTransform;
                flash.DOKill(); flash.gameObject.SetActive(true); flash.color = new Color(1, 1, 1, 0); flashRect.localScale = Vector3.one * .6f;
                DOTween.Sequence().SetTarget(flash).SetUpdate(true)
                    .Append(flash.DOFade(1, .12f)).Join(flashRect.DOScale(1.2f, style.revealFlipSeconds + .1f).SetEase(Ease.OutCubic))
                    .Append(flash.DOFade(0, .35f).SetEase(Ease.InQuad))
                    .OnComplete(() => flash.gameObject.SetActive(false));
            }
        }
        private void SetRevealedSocketV4()
        {
            if (style.intentSocket) intentSocket.sprite = style.intentSocket;
            RefreshIntentIconsV4();
        }
        private void RenderV4(EnemyHUDSnapshot state, bool selected)
        {
            snapshot = state;
            bool alive = state.Health > 0;
            if (alive) frozen = false;
            healthText.text = state.Health + " / " + state.MaxHealth;
            if (nameText) nameText.text = state.Name;
            RenderChip(blockChip, blockChipText, state.Block); RenderChip(dodgeChip, dodgeChipText, state.Dodge);
            CombatHUDStyleV4.PackChips(blockChip, dodgeChip);
            if (lastHealth != state.Health)
            {
                healthFill.DOKill(); float fill = state.MaxHealth <= 0 ? 0 : state.Health / (float)state.MaxHealth;
                bool snap = instant || lastHealth < 0 || !Application.isPlaying;
                if (snap) healthFill.fillAmount = fill;
                else healthFill.DOFillAmount(fill, theme.healthTweenDuration).SetUpdate(true);
                if (healthGhost)
                {
                    healthGhost.DOKill(); healthGhost.color = style.ghostColor;
                    if (snap || state.Health >= lastHealth) healthGhost.fillAmount = fill;
                    else healthGhost.DOFillAmount(fill, theme.healthTweenDuration).SetDelay(.35f).SetEase(Ease.OutCubic).SetUpdate(true);
                }
                lastHealth = state.Health;
            }
            bool known = revealedKinds.Count > 0 || !string.IsNullOrEmpty(state.Intent) && state.Intent != "Unknown";
            bool flipping = intentSocket && DOTween.IsTweening(intentSocket.rectTransform);
            if (!alive)
            {
                if (intentLabel) { intentLabel.DOKill(); intentLabel.enabled = true; intentLabel.alpha = 1; intentLabel.text = "DEFEATED"; }
                if (!intentFadedV4) { intentFadedV4 = true; FadeDefeatedIntentV4(); }
            }
            else if (!known)
            {
                if (intentLabel) intentLabel.enabled = false;
                if (intentSocket) { intentSocket.color = Color.white; if (!flipping && style.intentUnknown) intentSocket.sprite = style.intentUnknown; }
            }
            else
            {
                if (intentLabel) { intentLabel.enabled = true; intentLabel.text = state.Intent.Replace(" + ", HudStatusFormat.Separator); }
                if (intentSocket) { intentSocket.color = Color.white; if (!flipping && style.intentSocket) intentSocket.sprite = style.intentSocket; }
                RefreshIntentIconsV4();
            }
            selection.enabled = alive && (selected || state.IsValidTarget);
            if (style.targetMarker)
            {
                selection.sprite = style.targetMarker; selection.type = Image.Type.Simple;
                selection.color = selected ? style.targetMarkerSelected : style.targetMarkerValid;
                selection.rectTransform.localScale = Vector3.one * (selected ? style.targetMarkerSelectedScale : 1f);
            }
            else
            {
                selection.color = selected ? theme.gold : theme.validTargetColor;
                selection.rectTransform.sizeDelta = new Vector2(selected ? 120 : 70, selected ? 3 : 2);
            }
            UpdateTargetMarkerBobV4(alive && state.IsValidTarget);
            Project();
        }
        /// <summary>Fades whatever the intent socket currently shows (known or "?") to nothing; never leaves a stale ring or "?".</summary>
        private void FadeDefeatedIntentV4()
        {
            if (intentSocket)
            {
                intentSocket.DOKill();
                if (instant) intentSocket.color = new Color(1, 1, 1, 0);
                else intentSocket.DOFade(0, .25f).SetUpdate(true);
            }
            foreach (var icon in intentIcons)
            {
                if (!icon || !icon.gameObject.activeSelf) continue;
                icon.DOKill();
                if (instant) { icon.color = new Color(1, 1, 1, 0); icon.gameObject.SetActive(false); }
                else icon.DOFade(0, .25f).SetUpdate(true).OnComplete(() => { if (icon) icon.gameObject.SetActive(false); });
            }
        }
        /// <summary>Gentle sine bob (±4px, 1.2s period) on the target marker while the enemy is a valid target; killed and reset otherwise.</summary>
        private void UpdateTargetMarkerBobV4(bool bob)
        {
            if (!selection) return;
            var rect = selection.rectTransform;
            if (!markerBaseCaptured) { markerBasePos = rect.anchoredPosition; markerBaseCaptured = true; }
            if (bob)
            {
                if (markerBobbing) return;
                markerBobbing = true;
                rect.DOKill(); rect.anchoredPosition = new Vector2(markerBasePos.x, markerBasePos.y - 4);
                rect.DOAnchorPosY(markerBasePos.y + 4, .6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            else if (markerBobbing)
            {
                markerBobbing = false; rect.DOKill(); rect.anchoredPosition = markerBasePos;
            }
        }
        private void RefreshIntentIconsV4()
        {
            bool socketOpen = !intentSocket || !style.intentUnknown || intentSocket.sprite != style.intentUnknown;
            for (int i = 0; i < intentIcons.Length; i++)
            {
                var icon = intentIcons[i];
                if (!icon) continue;
                var sprite = i < revealedKinds.Count ? style.SmallIcon(revealedKinds[i]) : null;
                icon.sprite = sprite; icon.color = Color.white; icon.preserveAspect = true;
                icon.gameObject.SetActive(sprite && (i > 0 || socketOpen));
            }
        }
        private void ResetIntentV4()
        {
            intentFadedV4 = false;
            if (intentSocket)
            {
                intentSocket.rectTransform.DOKill(); intentSocket.rectTransform.localScale = Vector3.one;
                intentSocket.DOKill(); intentSocket.color = Color.white;
                if (style.intentUnknown) intentSocket.sprite = style.intentUnknown;
            }
            if (revealFlash) { revealFlash.DOKill(); revealFlash.gameObject.SetActive(false); }
            if (intentLabel) { intentLabel.DOKill(); intentLabel.alpha = 1; intentLabel.enabled = false; }
            foreach (var icon in intentIcons) if (icon) { icon.DOKill(); icon.color = Color.white; }
            RefreshIntentIconsV4();
        }
        private void RefreshHistoryV4()
        {
            if (historyIcons.Length == 0) return;
            var row = historyIcons[0] ? historyIcons[0].transform.parent : null;
            if (row && row != transform) row.gameObject.SetActive(historyKinds.Count > 0);
            for (int i = 0; i < historyIcons.Length; i++)
            {
                var icon = historyIcons[i];
                var token = i < historyTokens.Length ? historyTokens[i] : null;
                if (!icon) continue;
                bool visible = i < historyKinds.Count;
                icon.gameObject.SetActive(visible);
                if (token) token.gameObject.SetActive(visible);
                if (!visible) continue;
                // Newest observation renders at full alpha; older entries dim to style.historyIconAlpha. Past observations only, never future-facing.
                bool newest = i == historyKinds.Count - 1;
                icon.sprite = style.SmallIcon(historyKinds[i]); icon.color = new Color(1, 1, 1, newest ? 1f : style.historyIconAlpha);
                icon.preserveAspect = true;
                if (token) token.color = Color.white;
            }
        }
        /// <summary>Once defeated, the HUD stays where the model stood (the model itself sinks) and dims.</summary>
        private bool ProjectDefeatedV4()
        {
            if (!frozen)
            {
                if (!snapshot.Anchor || !worldCamera || !canvas) return false;
                var bounds = snapshot.Anchor.bounds;
                Vector3 p = worldCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                frozenVisible = p.z > 0 && snapshot.Anchor.gameObject.activeInHierarchy;
                if (frozenVisible)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, p, null, out var local);
                    ((RectTransform)transform).anchoredPosition = local + screenOffset;
                }
                frozen = true;
            }
            group.alpha = frozenVisible ? .55f : 0; group.blocksRaycasts = false;
            return true;
        }
        private static void RenderChip(GameObject chip, TextMeshProUGUI text, int value)
        {
            if (!chip) return;
            chip.SetActive(value > 0);
            if (text && value > 0) text.text = value.ToString();
        }
        public void OnPointerClick(PointerEventData e) { if (snapshot != null && snapshot.Health > 0 && planning) source?.TrySelectTarget(snapshot.Id); }
        private void OnDestroy() { if (healthFill) healthFill.DOKill(); if (intentText) intentText.rectTransform.DOKill(); foreach (var icon in icons) if (icon) icon.rectTransform.DOKill();
            if (intentSocket) { intentSocket.rectTransform.DOKill(); intentSocket.DOKill(); } if (revealFlash) revealFlash.DOKill(); if (healthGhost) healthGhost.DOKill(); if (intentLabel) intentLabel.DOKill();
            foreach (var icon in intentIcons) if (icon) icon.DOKill(); if (selection) selection.rectTransform.DOKill(); }
    }
}
