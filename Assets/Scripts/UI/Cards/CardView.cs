using DG.Tweening;
using DungeonRun.UI.Presentation;
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
        [Tooltip("V4 painted contact shadow and footer category glyph (unused by V1/V3).")] public Image shadow, categoryGlyph;
        public CombatCardItem Item { get; private set; }
        public CardInteractionState State { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        private CardHandController owner;
        private Image categoryAccent;
        private CombatHUDStyleV4 style;
        private bool socketedV4;
        private Vector2 handTitlePos, handTitleSize, handIconPos, handIconSize, handGlyphPos, handGlyphSize;
        private float handTitleFontMax;

        public void Bind(CombatCardItem item, CombatHUDTheme theme, CardHandController controller)
        {
            style = theme ? theme.v4Style : null;
            if (style) { BindV4(item, theme, controller); return; }
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
            fallbackGlyph.kind = item.Kind == PreviewCardKind.Miss ? CombatGlyph.Kind.Miss :
                item.Kind == PreviewCardKind.Charge ? CombatGlyph.Kind.Charge :
                item.Kind == PreviewCardKind.Combo ? CombatGlyph.Kind.Attack : item.Kind == PreviewCardKind.Counterattack ? CombatGlyph.Kind.Dodge : (CombatGlyph.Kind)item.Kind;
            fallbackGlyph.SetVerticesDirty();
            if (theme.cardFrame) { frame.sprite = theme.cardFrame; frame.color = Color.white; }
            foreach (var text in new[] { title, category }) if (theme.displayFont) text.font = theme.displayFont;
            foreach (var text in new[] { description, cost }) if (theme.bodyFont) text.font = theme.bodyFont;
            if (!categoryAccent)
            {
                var accent = CardDetailPanel.Rect("CategoryAccent", transform, new Vector2(180, 3));
                accent.anchoredPosition = new Vector2(0, 84);
                categoryAccent = accent.gameObject.AddComponent<Image>(); categoryAccent.raycastTarget = false;
            }
            categoryAccent.color = item.Kind == PreviewCardKind.Heal ? new Color(.2f,.42f,.29f) :
                item.Kind == PreviewCardKind.Defence ? new Color(.3f,.4f,.49f) :
                item.Kind == PreviewCardKind.Dodge || item.Kind == PreviewCardKind.Counterattack ? new Color(.48f,.37f,.2f) :
                item.Kind == PreviewCardKind.Charge || item.Kind == PreviewCardKind.Miss ? new Color(.4f,.34f,.45f) : new Color(.52f,.22f,.17f);
            title.fontStyle = FontStyles.Bold;
            SetState(CardInteractionState.Idle);
        }

        public void SetState(CardInteractionState state)
        {
            if (style) { SetStateV4(state); return; }
            State = state;
            focus.color = new Color(.82f, .7f, .38f, .7f);
            focus.enabled = state == CardInteractionState.Selected || state == CardInteractionState.Hovered || state == CardInteractionState.Dragging;
            group.alpha = state == CardInteractionState.Disabled ? .67f : 1;
            group.blocksRaycasts = state != CardInteractionState.Dragging && state != CardInteractionState.Playing && state != CardInteractionState.Discarding;
        }

        /// <summary>V4: <paramref name="over"/> a socket/enemy paints valid/invalid on the rim and tints the body; otherwise the normal dragging look.</summary>
        public void SetDropFeedback(bool over, bool valid)
        {
            if (State != CardInteractionState.Dragging) return;
            if (style)
            {
                focus.enabled = true;
                if (!over) { var tint = style.focusHover; tint.a *= style.highlightStrength; focus.color = tint; TintV4(false); return; }
                focus.color = valid ? style.focusValid : style.focusInvalid;
                TintV4(!valid);
                return;
            }
            focus.enabled = true; focus.color = valid ? new Color(.3f, .85f, .55f, .8f) : new Color(.9f, .3f, .22f, .8f);
        }

        private bool InHand => State == CardInteractionState.Idle || State == CardInteractionState.Hovered || State == CardInteractionState.Selected;
        public void OnPointerEnter(PointerEventData e) { if (InHand) owner.Hover(this, true); }
        public void OnPointerExit(PointerEventData e) { if (InHand) owner.Hover(this, false); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Middle) { owner.Inspect(this); return; }
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
            owner.DragFeedback(this, e.position, e.pressEventCamera);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (State != CardInteractionState.Dragging) return;
            owner.EndDrag(this, e.position, e.pressEventCamera);
        }
        private void BindV4(CombatCardItem item, CombatHUDTheme theme, CardHandController controller)
        {
            Item = item; owner = controller;
            title.text = item.Definition.cardName.ToUpperInvariant();
            CombatHUDStyleV4.SetFont(title, style.displayHeavyFont); title.color = style.titleInk; title.fontStyle = FontStyles.Normal;
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 14;
            description.fontSizeMax = 18;
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.text = item.Definition.description;
            CombatHUDStyleV4.SetFont(description, style.bodyBoldFont ? style.bodyBoldFont : style.bodyFont); description.color = style.bodyInk;
            cost.text = item.Definition.actionCost.ToString();
            CombatHUDStyleV4.SetFont(cost, style.bodyFont);
            category.text = CardCategoryMap.FooterLabel((int)item.Kind);
            CombatHUDStyleV4.SetFont(category, style.displayFont, style.labelOutline); category.color = style.footerText;
            // Priority: card artwork, painted V4 icon, theme icon, vector glyph.
            var icon = item.Definition.illustration ? item.Definition.illustration : style.Icon(item.Kind);
            if (!icon) icon = theme.Icon(item.Kind);
            illustration.sprite = icon; illustration.enabled = icon; illustration.color = Color.white; illustration.preserveAspect = true;
            fallbackGlyph.gameObject.SetActive(!icon);
            fallbackGlyph.kind = item.Kind == PreviewCardKind.Miss ? CombatGlyph.Kind.Miss :
                item.Kind == PreviewCardKind.Charge ? CombatGlyph.Kind.Charge :
                item.Kind == PreviewCardKind.Combo ? CombatGlyph.Kind.Attack : item.Kind == PreviewCardKind.Counterattack ? CombatGlyph.Kind.Dodge : (CombatGlyph.Kind)item.Kind;
            fallbackGlyph.SetVerticesDirty();
            var frameSprite = style.CardFrame(item.Kind);
            frame.sprite = frameSprite ? frameSprite : theme.cardFrame; frame.type = Image.Type.Simple; frame.color = Color.white;
            if (categoryGlyph) { categoryGlyph.sprite = style.CategoryGlyph(item.Kind); categoryGlyph.enabled = categoryGlyph.sprite; categoryGlyph.color = Color.white; }
            if (shadow && style.cardShadow) { shadow.sprite = style.cardShadow; shadow.type = Image.Type.Simple; }
            if (style.cardFocusRim) { focus.sprite = style.cardFocusRim; focus.type = Image.Type.Simple; }
            if (categoryAccent) categoryAccent.gameObject.SetActive(false);
            // A freshly bound view always starts from the prefab's hand-mode geometry; cache it once for SetSocketedV4 to restore.
            handTitlePos = title.rectTransform.anchoredPosition; handTitleSize = title.rectTransform.sizeDelta; handTitleFontMax = title.fontSizeMax;
            handIconPos = illustration.rectTransform.anchoredPosition; handIconSize = illustration.rectTransform.sizeDelta;
            if (categoryGlyph) { handGlyphPos = categoryGlyph.rectTransform.anchoredPosition; handGlyphSize = categoryGlyph.rectTransform.sizeDelta; }
            socketedV4 = false;
            SetState(CardInteractionState.Idle);
        }

        private void SetStateV4(CardInteractionState state)
        {
            State = state;
            bool lifted = state == CardInteractionState.Selected || state == CardInteractionState.Hovered || state == CardInteractionState.Dragging;
            var tint = state == CardInteractionState.Selected ? style.focusSelected : style.focusHover;
            tint.a *= style.highlightStrength;
            focus.color = tint; focus.enabled = lifted;
            if (shadow)
            {
                shadow.color = new Color(1, 1, 1, style.shadowIntensity * (lifted ? 1 : .75f));
                shadow.rectTransform.anchoredPosition = lifted ? style.shadowLiftedOffset : style.shadowIdleOffset;
            }
            group.alpha = 1;
            // Socketed cards (queued, locked during reveal, or staged as Playing) are the committed actions: never dimmed.
            TintV4(state == CardInteractionState.Disabled && !socketedV4);
            group.blocksRaycasts = state != CardInteractionState.Dragging && state != CardInteractionState.Playing && state != CardInteractionState.Discarding;
        }

        /// <summary>V4 socket presentation: swaps the frame, hides description/category, and moves the title/icon/glyph into the socket-card zones.
        /// Restores the hand layout exactly when unsocketed (returns to hand, discarded, or re-bound).</summary>
        public void SetSocketedV4(bool value)
        {
            if (!style || socketedV4 == value) return;
            socketedV4 = value;
            var frameSprite = value ? style.SocketFrame(Item.Kind) : style.CardFrame(Item.Kind);
            if (frameSprite) frame.sprite = frameSprite;
            if (description) description.gameObject.SetActive(!value);
            if (category) category.gameObject.SetActive(!value);
            title.rectTransform.anchoredPosition = value ? style.socketCardTitlePos : handTitlePos;
            title.rectTransform.sizeDelta = value ? style.socketCardTitleSize : handTitleSize;
            title.fontSizeMax = value ? style.socketCardTitleFontMax : handTitleFontMax;
            illustration.rectTransform.anchoredPosition = value ? style.socketCardIconPos : handIconPos;
            illustration.rectTransform.sizeDelta = value ? style.socketCardIconSize : handIconSize;
            if (categoryGlyph)
            {
                categoryGlyph.rectTransform.anchoredPosition = value ? style.socketCardGlyphPos : handGlyphPos;
                categoryGlyph.rectTransform.sizeDelta = value ? style.socketCardGlyphSize : handGlyphSize;
            }
            if (State != CardInteractionState.Idle) TintV4(State == CardInteractionState.Disabled && !socketedV4);
        }

        /// <summary>V4 disabled look: a multiplicative CanvasRenderer tint (no translucency, so fanned cards never show through).</summary>
        private void TintV4(bool dim)
        {
            if (tinted == null)
            {
                var list = new System.Collections.Generic.List<Graphic>();
                foreach (var graphic in GetComponentsInChildren<Graphic>(true)) if (graphic != shadow && graphic != focus) list.Add(graphic);
                tinted = list.ToArray();
            }
            var color = dim ? style.cardDisabledTint : Color.white;
            foreach (var graphic in tinted) if (graphic) graphic.canvasRenderer.SetColor(color);
        }
        private Graphic[] tinted;
        private void OnDestroy() => Rect.DOKill();
    }
}
