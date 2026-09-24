using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CardActionSlot : MonoBehaviour, IPointerClickHandler
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
        }

        public void Show(bool occupied, bool available, bool locked = false, string targets = "")
        {
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
            border.color = !over ? restingColor : valid ? new Color(.25f, .8f, .5f, 1) : new Color(.9f, .25f, .2f, 1);
        }

        public void Reject()
        {
            border.DOKill();
            border.color = new Color(.9f, .25f, .2f);
            border.DOColor(restingColor, .35f).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData e) => owner?.SlotClicked(slotIndex);
    }
}
