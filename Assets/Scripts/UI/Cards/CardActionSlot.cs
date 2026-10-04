using TMPro;
using DG.Tweening;
using DungeonRun.Combat;
using DungeonRun.UI.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CardActionSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int slotIndex;
        public TextMeshProUGUI label;
        public Image border;
        public RectTransform cardAnchor;
        private CardHandController owner;
        public TextMeshProUGUI targetLabel;
        private Color restingColor;
        private CanvasGroup presentationGroup;
        public RectTransform Rect => (RectTransform)transform;
        [Tooltip("V4 socket overlays and rune numeral (unused by V1/V3).")] public Image rim, glow;
        public TextMeshProUGUI rune;
        [Tooltip("V4 soft backing behind the target label (style.textWash).")] public Image targetLabelWash;
        private BattlePhase phase;
        private bool terminal, resolving, occupied, armed, dropOver, dropValid, instant;
        private float denseScale = 1;
        private CombatHUDStyleV4 Style => owner && owner.theme ? owner.theme.v4Style : null;

        /// <summary>Base scale from the dense (3-4 slot) row layout; Repaint multiplies it with the per-state scale.</summary>
        public void SetDenseScale(float scale) => denseScale = scale <= 0 ? 1 : scale;

        private CanvasGroup EnsurePresentationGroup()
        {
            if (!presentationGroup && !TryGetComponent(out presentationGroup))
            {
                presentationGroup = gameObject.AddComponent<CanvasGroup>();
            }
            return presentationGroup;
        }

        public void Bind(CardHandController controller)
        {
            owner = controller;
            EnsurePresentationGroup();
            if (!targetLabel)
            {
                targetLabel = CardDetailPanel.Text("AssignedTargets", transform, new Vector2(0, -110), new Vector2(172, 44), controller.theme.bodyFont, 13);
                targetLabel.alignment = TextAlignmentOptions.Center;
                targetLabel.enableAutoSizing = true;
                targetLabel.fontSizeMin = 10;
                targetLabel.fontSizeMax = 13;
            }
            var style = Style;
            if (!targetLabelWash && style && style.textWash)
            {
                var wash = CardDetailPanel.Rect("AssignedTargetsWash", transform, Vector2.zero);
                targetLabelWash = wash.gameObject.AddComponent<Image>();
                targetLabelWash.sprite = style.textWash; targetLabelWash.raycastTarget = false;
                targetLabelWash.transform.SetSiblingIndex(targetLabel.transform.GetSiblingIndex());
            }
        }

        public void Show(bool occupied, bool available, bool locked = false, string targets = "")
        {
            var style = Style;
            if (style) { ShowV4(style, occupied, available, locked, targets); return; }
            // Played cards live under handRoot, not this slot. Hide only the obsolete
            // planning plate/labels during reveal and resolution so they cannot cover staging.
            EnsurePresentationGroup();
            presentationGroup.alpha = locked ? 0 : 1;
            presentationGroup.blocksRaycasts = !locked;
            presentationGroup.interactable = !locked;
            label.text = "ACTION " + (slotIndex + 1) + (occupied ? locked ? " - LOCKED" : " - RETURN" : "");
            if (targetLabel) targetLabel.text = targets;
            border.color = available ? new Color(.72f, .58f, .32f, .9f) : new Color(.45f, .39f, .27f, .7f);
            restingColor = border.color;
        }

        public void SetDropFeedback(bool over, bool valid)
        {
            if (Style) { dropOver = over; dropValid = valid; Repaint(); return; }
            border.color = !over ? restingColor : valid ? new Color(.25f, .8f, .5f, 1) : new Color(.9f, .25f, .2f, 1);
        }

        public void Reject()
        {
            var style = Style;
            if (style) { RejectV4(style); return; }
            border.DOKill();
            border.color = new Color(.9f, .25f, .2f);
            border.DOColor(restingColor, .35f).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData e) => owner?.SlotClicked(slotIndex);
        public void OnPointerEnter(PointerEventData e) => owner?.SlotHover(slotIndex, true);
        public void OnPointerExit(PointerEventData e) => owner?.SlotHover(slotIndex, false);

        /// <summary>Battle context for the V4 socket state. Stored only; V1/V3 presentation ignores it.</summary>
        public void SetContext(BattlePhase battlePhase, bool isTerminal, bool isResolving, bool instantAnimations = false)
        { phase = battlePhase; terminal = isTerminal; resolving = isResolving; instant = instantAnimations; }

        private void ShowV4(CombatHUDStyleV4 style, bool isOccupied, bool available, bool locked, string targets)
        {
            EnsurePresentationGroup();
            occupied = isOccupied; armed = available; dropOver = false;
            // Locked sockets stay visible (dimmed by Repaint) but never take input.
            presentationGroup.blocksRaycasts = !locked;
            presentationGroup.interactable = !locked;
            label.enabled = false;
            if (rune) rune.text = HudStatusFormat.Roman(slotIndex + 1);
            if (targetLabel)
            {
                targetLabel.text = targets;
                // Placed every time (not only on a font change): a label that already used the V4 body font
                // stayed at its V3 spot, hidden behind the queued card.
                CombatHUDStyleV4.SetFont(targetLabel, style.displayFont, style.labelOutline); targetLabel.color = style.parchmentText;
                // 8px above the socket top; staged/queued cards never grow past their socket, so this never overlaps them.
                var rect = targetLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 8); rect.sizeDelta = new Vector2(172, 20);
                bool hasTargets = !string.IsNullOrEmpty(targets);
                if (targetLabelWash)
                {
                    var wash = targetLabelWash.rectTransform;
                    wash.anchorMin = rect.anchorMin; wash.anchorMax = rect.anchorMax; wash.pivot = rect.pivot;
                    wash.anchoredPosition = rect.anchoredPosition;
                    var fit = hasTargets ? targetLabel.GetPreferredValues(targets, rect.sizeDelta.x, 0) : Vector2.zero;
                    wash.sizeDelta = new Vector2(Mathf.Min(rect.sizeDelta.x, fit.x + 12), fit.y + 6);
                    targetLabelWash.color = new Color(1, 1, 1, .55f);
                    targetLabelWash.gameObject.SetActive(hasTargets);
                }
            }
            Repaint();
        }

        private void Repaint()
        {
            var style = Style;
            if (!style) return;
            EnsurePresentationGroup();
            if (terminal)
            {
                presentationGroup.blocksRaycasts = presentationGroup.interactable = false;
                presentationGroup.DOKill();
                if (instant) presentationGroup.alpha = style.socketTerminalAlpha;
                else presentationGroup.DOFade(style.socketTerminalAlpha, .25f).SetUpdate(true);
                Rect.localScale = new Vector3(denseScale, denseScale, 1);
                return;
            }
            var state = SlotVisualStateResolver.Resolve(occupied, armed, dropOver, dropValid, (HudPhase)(int)phase, resolving, terminal);
            Color rimColor = Color.clear, glowColor = Color.clear, socketColor = Color.white;
            bool lit = false; float alpha = 1, scale = 1;
            switch (state)
            {
                case SlotVisualState.Empty: alpha = style.socketEmptyAlpha; break;
                case SlotVisualState.Armed: rimColor = style.rimArmed; break;
                case SlotVisualState.HoverValid: rimColor = style.rimHoverValid; glowColor = style.glowHoverValid; lit = true; break;
                case SlotVisualState.HoverInvalid: rimColor = style.rimHoverInvalid; socketColor = style.socketInvalidTint; break;
                case SlotVisualState.Queued: rimColor = style.rimQueued; break;
                case SlotVisualState.Locked: rimColor = style.rimLocked; alpha = style.socketLockedAlpha; scale = .97f; break;
                case SlotVisualState.Resolving: rimColor = style.rimResolving; glowColor = style.glowResolving; lit = true; break;
            }
            presentationGroup.DOKill();
            presentationGroup.alpha = alpha; presentationGroup.blocksRaycasts = presentationGroup.interactable = !terminal;
            border.DOKill(); border.color = socketColor;
            if (rim) { rim.DOKill(); rim.color = rimColor; }
            if (glow) glow.color = glowColor;
            if (rune) rune.color = lit ? style.runeLit : style.runeIdle;
            Rect.localScale = new Vector3(scale * denseScale, scale * denseScale, 1);
        }

        private void RejectV4(CombatHUDStyleV4 style)
        {
            dropOver = false; Repaint();
            if (!rim) return;
            var rest = rim.color; rim.color = style.rimHoverInvalid;
            rim.DOColor(rest, .35f).SetUpdate(true);
        }

        private void OnDestroy() { if (rim) rim.DOKill(); if (presentationGroup) presentationGroup.DOKill(); }
    }
}
