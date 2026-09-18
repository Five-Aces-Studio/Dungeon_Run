using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public TextMeshProUGUI title, description, cost, category;
        public Image illustration, frame, focus;
        public CanvasGroup group;
        public CombatGlyph fallbackGlyph;
        public CombatCardItem Item { get; private set; }
        public CardInteractionState State { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        private CardHandController owner;

        public void Bind(CombatCardItem item, CombatHUDTheme theme, CardHandController controller)
        {
            Item = item; owner = controller;
            title.text = item.Definition.cardName.ToUpperInvariant();
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 15;
            description.fontSizeMax = 18;
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.text = item.Definition.description;
            cost.text = item.Definition.actionCost.ToString();
            category.text = item.Kind == PreviewCardKind.Heal ? "RESTORATION" : item.Kind == PreviewCardKind.Defence ||
                item.Kind == PreviewCardKind.Dodge || item.Kind == PreviewCardKind.Counterattack || item.Kind == PreviewCardKind.Charge
                    ? "TACTIC" : item.Kind == PreviewCardKind.Miss ? "NO EFFECT" : "STRIKE";
            var icon = item.Definition.illustration ? item.Definition.illustration : theme.Icon(item.Kind);
            illustration.sprite = icon; illustration.enabled = icon;
            fallbackGlyph.gameObject.SetActive(!icon);
            fallbackGlyph.kind = item.Kind == PreviewCardKind.Miss || item.Kind == PreviewCardKind.Charge ? CombatGlyph.Kind.Diamond :
                item.Kind == PreviewCardKind.Combo ? CombatGlyph.Kind.Attack : item.Kind == PreviewCardKind.Counterattack ? CombatGlyph.Kind.Dodge : (CombatGlyph.Kind)item.Kind;
            fallbackGlyph.SetVerticesDirty();
            if (theme.cardFrame) { frame.sprite = theme.cardFrame; frame.color = Color.white; }
            foreach (var text in new[] { title, category }) if (theme.displayFont) text.font = theme.displayFont;
            foreach (var text in new[] { description, cost }) if (theme.bodyFont) text.font = theme.bodyFont;
            SetState(CardInteractionState.Idle);
        }

        public void SetState(CardInteractionState state)
        {
            State = state;
            focus.enabled = state == CardInteractionState.Selected || state == CardInteractionState.Hovered || state == CardInteractionState.Dragging;
            group.alpha = state == CardInteractionState.Disabled ? .67f : 1;
            group.blocksRaycasts = state != CardInteractionState.Dragging && state != CardInteractionState.Playing && state != CardInteractionState.Discarding;
        }

        private bool InHand => State == CardInteractionState.Idle || State == CardInteractionState.Hovered || State == CardInteractionState.Selected;
        public void OnPointerEnter(PointerEventData e) { if (InHand) owner.Hover(this, true); }
        public void OnPointerExit(PointerEventData e) { if (InHand) owner.Hover(this, false); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { owner.Cancel(this); return; }
            if (State == CardInteractionState.Queued) owner.Cancel(this);
            else if (InHand) owner.Select(Item.Id);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if (!InHand || e.button != PointerEventData.InputButton.Left) return;
            e.eligibleForClick = false; SetState(CardInteractionState.Dragging); Rect.DOKill();
            Rect.localScale = Vector3.one * owner.hoverScale; Rect.localRotation = Quaternion.identity;
            transform.SetAsLastSibling(); owner.BeginDrag(this); OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (State != CardInteractionState.Dragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.handRoot, e.position, e.pressEventCamera, out var p);
            Rect.anchoredPosition = p;
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (State != CardInteractionState.Dragging) return;
            owner.EndDrag(this, e.position, e.pressEventCamera);
        }
        private void OnDestroy() => Rect.DOKill();
    }
}
