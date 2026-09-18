using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace DungeonRun.UI
{
    public sealed class CardHandController : MonoBehaviour
    {
        public RectTransform handRoot, drawAnchor, discardAnchor, playAnchor;
        public CardView cardPrefab;
        public CardActionSlot[] actionSlots;
        public CombatHUDTheme theme;
        [Header("Fan (1-10 cards)")]
        public float fanWidth = 850, spacing = 185, verticalCurve = 27, maxRotation = 8;
        public float idleTilt = 3, hoverElevation = 70, selectedElevation = 88, hoverScale = 1.075f;
        public float neighbourSeparation = 22, animationDuration = .2f;
        private readonly Dictionary<int, CardView> views = new Dictionary<int, CardView>();
        private readonly HashSet<CardView> exitingViews = new HashSet<CardView>();
        private ICombatHUDSource source;
        private CombatHUDSnapshot snapshot;
        private int selected = -1, hovered = -1;
        private bool instant;
        public int SelectedCardId => selected;
        public int ViewCount => views.Count;

        public void ConfigureSlots(int count)
        {
            if (count < 1 || count > 4) throw new System.InvalidOperationException("Combat HUD supports 1-4 action slots.");
            if (actionSlots == null || actionSlots.Length == 0 || !actionSlots[0])
                throw new System.InvalidOperationException("Assign an existing action slot template.");
            var slots = actionSlots.ToList();
            while (slots.Count < count)
            {
                var slot = Instantiate(slots[0], slots[0].transform.parent);
                slot.name = "ActionSlot" + (slots.Count + 1);
                slots.Add(slot);
            }
            actionSlots = slots.ToArray();
            for (int i = 0; i < actionSlots.Length; i++)
            {
                var slot = actionSlots[i];
                slot.slotIndex = i;
                slot.gameObject.SetActive(i < count);
                // Preserve the V1 pair's center and spacing while accommodating bounded playtests.
                slot.Rect.anchoredPosition = new Vector2(-127.5f + (i - (count - 1) * .5f) * 175, slot.Rect.anchoredPosition.y);
                slot.Bind(this);
            }
        }

        public void Bind(ICombatHUDSource data)
        {
            Cleanup(); source = data;
            foreach (var stale in handRoot.GetComponentsInChildren<CardView>(true)) DestroyView(stale);
            foreach (var slot in actionSlots) slot.Bind(this);
            source.CardMoving += AnimateMotion;
        }

        public void Render(CombatHUDSnapshot state)
        {
            snapshot = state;
            var all = state.Hand.Concat(state.Slots.Where(x => x != null)).ToArray();
            var ids = new HashSet<int>(all.Select(x => x.Id));
            foreach (int id in views.Keys.Where(x => !ids.Contains(x)).ToArray())
            {
                var departing = views[id]; views.Remove(id);
                if (Application.isPlaying && !instant && departing.State == CardInteractionState.Discarding)
                {
                    exitingViews.Add(departing);
                    DOVirtual.DelayedCall(.26f, () => { exitingViews.Remove(departing); DestroyView(departing); });
                }
                else DestroyView(departing);
            }
            foreach (var item in all)
            {
                if (views.ContainsKey(item.Id)) continue;
                var view = Instantiate(cardPrefab, handRoot);
                if (!Application.isPlaying) view.gameObject.hideFlags = HideFlags.DontSaveInEditor;
                view.name = "Card_" + item.Id + "_" + item.Definition.cardName;
                view.gameObject.SetActive(true); view.Bind(item, theme, this);
                view.Rect.anchoredPosition = ToHand(drawAnchor); view.Rect.localScale = Vector3.one * .35f;
                views.Add(item.Id, view);
            }
            if (!state.Hand.Any(x => x.Id == selected)) selected = -1;
            for (int i = 0; i < state.Slots.Count; i++) actionSlots[i].Show(state.Slots[i] != null, selected >= 0 && !state.IsResolving, state.IsResolving);
            Layout();
        }

        public void SetInstantAnimations(bool value) { instant = value; Layout(); }
        private Vector2 ToHand(RectTransform anchor) => handRoot.InverseTransformPoint(anchor.position);
        private void Layout()
        {
            if (snapshot == null) return;
            int count = snapshot.Hand.Count;
            float step = count < 2 ? 0 : Mathf.Min(spacing, fanWidth / (count - 1));
            for (int i = 0; i < count; i++)
            {
                var item = snapshot.Hand[i];
                if (!views.TryGetValue(item.Id, out var view) || view.State == CardInteractionState.Dragging) continue;
                float t = count < 2 ? 0 : (i - (count - 1) * .5f) / ((count - 1) * .5f);
                bool isSelected = item.Id == selected, isHovered = item.Id == hovered;
                int focusIndex = snapshot.Hand.ToList().FindIndex(x => x.Id == (selected >= 0 ? selected : hovered));
                float separation = focusIndex < 0 || focusIndex == i ? 0 : Mathf.Sign(i - focusIndex) * neighbourSeparation;
                Vector2 p = new Vector2((i - (count - 1) * .5f) * step + separation,
                    -verticalCurve * t * t + (isSelected ? selectedElevation : isHovered ? hoverElevation : 0));
                var state = snapshot.IsResolving || snapshot.CanEndTurn ? CardInteractionState.Disabled :
                    isSelected ? CardInteractionState.Selected : isHovered ? CardInteractionState.Hovered : CardInteractionState.Idle;
                view.SetState(state);
                UIAnimationHelpers.Pose(view.Rect, p, new Vector3(isSelected || isHovered ? 0 : idleTilt, 0,
                    isSelected || isHovered ? 0 : -t * maxRotation), isSelected || isHovered ? hoverScale : 1,
                    instant ? 0 : animationDuration);
                view.transform.SetSiblingIndex(i);
            }
            for (int i = 0; i < snapshot.Slots.Count; i++)
            {
                var item = snapshot.Slots[i];
                if (item == null || !views.TryGetValue(item.Id, out var view)) continue;
                if (view.State == CardInteractionState.Playing || view.State == CardInteractionState.Discarding) continue;
                view.SetState(CardInteractionState.Queued);
                UIAnimationHelpers.Pose(view.Rect, ToHand(actionSlots[i].cardAnchor), Vector3.zero, .27f,
                    instant ? 0 : animationDuration);
            }
            int focus = selected >= 0 ? selected : hovered;
            if (views.TryGetValue(focus, out var focused)) focused.transform.SetAsLastSibling();
        }

        public void Hover(CardView view, bool active)
        {
            if (snapshot.IsResolving) return;
            hovered = active ? view.Item.Id : hovered == view.Item.Id ? -1 : hovered;
            Layout();
        }
        public void Select(int id)
        {
            if (snapshot.IsResolving || snapshot.CanEndTurn || !snapshot.Hand.Any(x => x.Id == id)) return;
            selected = selected == id ? -1 : id;
            for (int i = 0; i < snapshot.Slots.Count; i++) actionSlots[i].Show(snapshot.Slots[i] != null, selected >= 0);
            Layout();
        }
        public void BeginDrag(CardView view) { selected = view.Item.Id; hovered = -1; }
        public void EndDrag(CardView view, Vector2 screenPosition, Camera eventCamera)
        {
            view.SetState(CardInteractionState.Idle);
            foreach (var slot in actionSlots)
                if (slot.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screenPosition, eventCamera) && source.TryQueue(view.Item.Id, slot.slotIndex))
                { selected = -1; return; }
            selected = -1; Layout();
        }
        public void SlotClicked(int index)
        {
            if (snapshot == null || snapshot.IsResolving || index < 0 || index >= snapshot.Slots.Count) return;
            if (snapshot.Slots[index] != null) source.TryCancel(index);
            else if (selected >= 0) source.TryQueue(selected, index);
        }
        public void Cancel(CardView view)
        {
            if (snapshot == null || snapshot.IsResolving) return;
            for (int i = 0; i < snapshot.Slots.Count; i++)
                if (snapshot.Slots[i]?.Id == view.Item.Id) { source.TryCancel(i); return; }
            selected = -1; hovered = -1; Layout();
        }
        public void CancelSelection()
        {
            if (snapshot == null || snapshot.IsResolving) return;
            if (selected >= 0 || hovered >= 0) { selected = hovered = -1; Layout(); return; }
            for (int i = snapshot.Slots.Count - 1; i >= 0; i--) if (snapshot.Slots[i] != null) { source.TryCancel(i); return; }
        }
        private void AnimateMotion(int id, CardMotion motion)
        {
            if (!views.TryGetValue(id, out var view)) return;
            view.SetState(motion == CardMotion.Playing ? CardInteractionState.Playing : CardInteractionState.Discarding);
            view.transform.SetAsLastSibling();
            UIAnimationHelpers.Pose(view.Rect, ToHand(motion == CardMotion.Playing ? playAnchor : discardAnchor),
                new Vector3(0, 0, motion == CardMotion.Playing ? 0 : 12), motion == CardMotion.Playing ? .7f : .18f,
                instant ? 0 : .25f);
        }
        private static void DestroyView(CardView view)
        {
            if (!view) return;
            view.Rect.DOKill(); view.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
        }
        public void Cleanup()
        {
            if (source != null) source.CardMoving -= AnimateMotion;
            foreach (var view in views.Values) DestroyView(view);
            foreach (var view in exitingViews) DestroyView(view);
            exitingViews.Clear();
            views.Clear(); source = null; snapshot = null; selected = hovered = -1;
        }
        private void OnDestroy() => Cleanup();
    }
}
