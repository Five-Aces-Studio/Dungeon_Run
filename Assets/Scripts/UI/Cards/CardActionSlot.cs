using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    public sealed class CardActionSlot : MonoBehaviour, IPointerClickHandler
    {
        public int slotIndex;
        public TextMeshProUGUI label;
        public Image border;
        public RectTransform cardAnchor;
        private CardHandController owner;
        public RectTransform Rect => (RectTransform)transform;
        public void Bind(CardHandController controller) => owner = controller;
        public void Show(bool occupied, bool available, bool locked = false)
        {
            label.text = occupied ? locked ? "COMMITTED" : "CLICK TO RETURN" : "ACTION " + (slotIndex + 1);
            border.color = available ? new Color(.72f, .58f, .32f, .9f) : new Color(.45f, .39f, .27f, .7f);
        }
        public void OnPointerClick(PointerEventData e) => owner?.SlotClicked(slotIndex);
    }
}
