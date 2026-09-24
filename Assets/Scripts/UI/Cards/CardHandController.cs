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
        private readonly HashSet<int> drawnIds = new HashSet<int>();
        private readonly HashSet<CardView> exitingViews = new HashSet<CardView>();
        private ICombatHUDSource source;
        private CombatHUDSnapshot snapshot;
        private int selected = -1, hovered = -1;
        private bool instant;
        private CardDetailPanel detail;
        private TMPro.TextMeshProUGUI inspectHint;
        private EnemyCombatHUD[] enemyViews;
        private CardTargetingPanel targetingPanel;
        [Header("Presentation timing")]
        public float drawDuration = .32f, playDuration = .3f, discardDuration = .3f;
        public float playCardSpacing = 190, playCardScale = .6f;
        public event System.Action<string> PresentationCue;
        private ICombatTargetingSource Targeting => source as ICombatTargetingSource;
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
            enemyViews = handRoot.GetComponentInParent<Canvas>().GetComponentsInChildren<EnemyCombatHUD>();
            if (!detail) detail = CardDetailPanel.Create(handRoot.GetComponentInParent<Canvas>().transform, theme);
            if (!targetingPanel) targetingPanel = CardTargetingPanel.Create(handRoot.GetComponentInParent<Canvas>().transform, theme);
            targetingPanel.Bind(enabled => Targeting?.SetPerHitTargeting(enabled), CancelSelection);
            if (!inspectHint)
            {
                inspectHint = CardDetailPanel.Text("CardInspectHintV3", handRoot.GetComponentInParent<Canvas>().transform,
                    Vector2.zero, new Vector2(360, 24), theme.bodyFont, 15);
                inspectHint.rectTransform.anchorMin = inspectHint.rectTransform.anchorMax = new Vector2(.5f, 0);
                inspectHint.rectTransform.pivot = new Vector2(.5f, 0);
                inspectHint.rectTransform.anchoredPosition = new Vector2(0, 12);
                inspectHint.alignment = TMPro.TextAlignmentOptions.Center;
                inspectHint.text = "Middle-click a card: inspect";
            }
        }

        public void Render(CombatHUDSnapshot state)
        {
            snapshot = state;
            if (targetingPanel) targetingPanel.Render(state);
            if (state.IsResolving && detail) detail.Hide();
            var all = state.Hand.Concat(state.Slots.Where(x => x != null)).ToArray();
            var ids = new HashSet<int>(all.Select(x => x.Id));
            foreach (int id in views.Keys.Where(x => !ids.Contains(x)).ToArray())
            {
                var departing = views[id]; views.Remove(id);
                if (Application.isPlaying && !instant && departing.State == CardInteractionState.Discarding)
                {
                    exitingViews.Add(departing);
                    DOVirtual.DelayedCall(discardDuration + .02f, () => { exitingViews.Remove(departing); DestroyView(departing); });
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
                views.Add(item.Id, view); drawnIds.Add(item.Id);
            }
            if (!state.Hand.Any(x => x.Id == selected)) selected = -1;
            for (int i = 0; i < state.Slots.Count; i++) actionSlots[i].Show(state.Slots[i] != null, selected >= 0 && !state.IsResolving, state.IsResolving, i < state.SlotTargets.Count ? state.SlotTargets[i] : "");
            Layout();
        }

        public void SetInstantAnimations(bool value) { instant = value; Layout(); }
        private Vector2 ToHand(RectTransform anchor) => handRoot.InverseTransformPoint(anchor.position);
        private void Layout()
        {
            if (snapshot == null) return;
            int count = snapshot.Hand.Count;
            int focusIndex = -1;
            for (int i = 0; i < count; i++) if (snapshot.Hand[i].Id == (selected >= 0 ? selected : hovered)) { focusIndex = i; break; }
            float step = count < 2 ? 0 : Mathf.Min(spacing, fanWidth / (count - 1));
            for (int i = 0; i < count; i++)
            {
                var item = snapshot.Hand[i];
                if (!views.TryGetValue(item.Id, out var view) || view.State == CardInteractionState.Dragging) continue;
                float t = count < 2 ? 0 : (i - (count - 1) * .5f) / ((count - 1) * .5f);
                bool isSelected = item.Id == selected, isHovered = item.Id == hovered;
                float separation = focusIndex < 0 || focusIndex == i ? 0 : Mathf.Sign(i - focusIndex) * neighbourSeparation;
                Vector2 p = new Vector2((i - (count - 1) * .5f) * step + separation,
                    -verticalCurve * t * t + (isSelected ? selectedElevation : isHovered ? hoverElevation : 0));
                var state = snapshot.IsResolving || snapshot.CanEndTurn ? CardInteractionState.Disabled :
                    isSelected ? CardInteractionState.Selected : isHovered ? CardInteractionState.Hovered : CardInteractionState.Idle;
                view.SetState(state);
                UIAnimationHelpers.Pose(view.Rect, p, new Vector3(isSelected || isHovered ? 0 : idleTilt, 0,
                    isSelected || isHovered ? 0 : -t * maxRotation), isSelected || isHovered ? hoverScale : 1,
                    instant ? 0 : drawnIds.Remove(item.Id) ? drawDuration : animationDuration);
                view.transform.SetSiblingIndex(i);
            }
            for (int i = 0; i < snapshot.Slots.Count; i++)
            {
                var item = snapshot.Slots[i];
                if (item == null || !views.TryGetValue(item.Id, out var view)) continue;
                if (view.State == CardInteractionState.Playing || view.State == CardInteractionState.Discarding) continue;
                view.SetState(snapshot.IsResolving ? CardInteractionState.Disabled : CardInteractionState.Queued);
                UIAnimationHelpers.Pose(view.Rect, ToHand(actionSlots[i].cardAnchor), Vector3.zero, .27f,
                    instant ? 0 : animationDuration);
            }
            int focus = selected >= 0 ? selected : hovered;
            if (views.TryGetValue(focus, out var focused)) focused.transform.SetAsLastSibling();
        }

        public void Hover(CardView view, bool active)
        {
            if (snapshot.IsResolving) return;
            if (active) PresentationCue?.Invoke("Hover");
            hovered = active ? view.Item.Id : hovered == view.Item.Id ? -1 : hovered;
            Layout();
        }
        public void Select(int id)
        {
            if (snapshot.IsResolving || snapshot.CanEndTurn || !snapshot.Hand.Any(x => x.Id == id)) return;
            selected = selected == id ? -1 : id;
            if (selected < 0) { Targeting?.CancelTargeting(); if (detail) detail.Hide(); }
            else
            {
                PresentationCue?.Invoke("Select");
                var item = snapshot.Hand.First(x => x.Id == id);
                // Target metadata, never card names, chooses the card-first flow.
                if (item.Definition.targetMode == DungeonRun.Combat.TargetMode.SingleOpponent) Targeting?.BeginTargeting(id);
                else Targeting?.CancelTargeting();

            }
            for (int i = 0; i < snapshot.Slots.Count; i++) actionSlots[i].Show(snapshot.Slots[i] != null, selected >= 0, false, i < snapshot.SlotTargets.Count ? snapshot.SlotTargets[i] : "");
            Layout();
        }
        public void BeginDrag(CardView view)
        {
            selected = view.Item.Id; hovered = -1;
            if (detail) detail.Hide();
            if (view.Item.Definition.targetMode == DungeonRun.Combat.TargetMode.SingleOpponent) Targeting?.BeginTargeting(selected);
            else Targeting?.CancelTargeting();
        }
        public void Inspect(CardView view) { if (detail) detail.Show(view.Item, theme); }
        public void DragFeedback(CardView view, Vector2 screenPosition, Camera eventCamera)
        {
            bool valid = false;
            foreach (var slot in actionSlots)
            {
                if (!slot.gameObject.activeSelf || slot.slotIndex >= snapshot.Slots.Count) continue;
                bool over = RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screenPosition, eventCamera);
                bool canDrop = !snapshot.IsResolving && snapshot.Slots[slot.slotIndex] == null;
                slot.SetDropFeedback(over, canDrop);
                valid |= over && canDrop;
            }
            foreach (var enemy in enemyViews)
                if (enemy && enemy.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)enemy.transform, screenPosition, eventCamera))
                    valid |= IsValidEnemy(enemy.ActorId);
            view.SetDropFeedback(valid);
        }
        private bool IsValidEnemy(int actorId)
        {
            for (int i = 0; i < snapshot.Enemies.Count; i++)
                if (snapshot.Enemies[i].Id == actorId) return snapshot.Enemies[i].IsValidTarget;
            return false;
        }
        public void EndDrag(CardView view, Vector2 screenPosition, Camera eventCamera)
        {
            view.SetState(CardInteractionState.Idle);
            foreach (var slot in actionSlots)
            {
                slot.SetDropFeedback(false, false);
                if (slot.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screenPosition, eventCamera))
                {
                    if (snapshot.Slots[slot.slotIndex] == null)
                    {
                        if (source.TryQueue(view.Item.Id, slot.slotIndex)) selected = -1;
                        Layout(); return;
                    }
                    slot.Reject();
                }
            }
            var enemies = enemyViews;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] && enemies[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)enemies[i].transform, screenPosition, eventCamera))
                { if (view.Item.Definition.targetMode == DungeonRun.Combat.TargetMode.SingleOpponent) source.TrySelectTarget(enemies[i].ActorId);
                  else Targeting?.CancelTargeting(); Layout(); return; }
            Targeting?.CancelTargeting(); selected = -1; Layout();
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
            Targeting?.CancelTargeting(); selected = -1; hovered = -1; if (detail) detail.Hide(); Layout();
        }
        public void CancelSelection()
        {
            if (snapshot == null || snapshot.IsResolving) return;
            if (selected >= 0 || hovered >= 0 || snapshot.TargetingCardId >= 0) { Targeting?.CancelTargeting(); selected = hovered = -1; if (detail) detail.Hide(); Layout(); return; }
            for (int i = snapshot.Slots.Count - 1; i >= 0; i--) if (snapshot.Slots[i] != null) { source.TryCancel(i); return; }
        }
        private void AnimateMotion(int id, CardMotion motion)
        {
            if (!views.TryGetValue(id, out var view)) return;
            view.SetState(motion == CardMotion.Playing ? CardInteractionState.Playing : CardInteractionState.Discarding);
            view.transform.SetAsLastSibling();
            int slotIndex = 0;
            for (int i = 0; i < snapshot.Slots.Count; i++) if (snapshot.Slots[i]?.Id == id) { slotIndex = i; break; }
            Vector2 target = motion == CardMotion.Playing
                ? ToHand(playAnchor) + new Vector2((slotIndex - (snapshot.Slots.Count - 1) * .5f) * playCardSpacing, 0)
                : ToHand(discardAnchor);
            UIAnimationHelpers.Pose(view.Rect, target,
                new Vector3(0, 0, motion == CardMotion.Playing ? 0 : 12), motion == CardMotion.Playing ? playCardScale : .18f,
                instant ? 0 : motion == CardMotion.Playing ? playDuration : discardDuration);
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
            views.Clear(); drawnIds.Clear(); if (detail) detail.Hide(); if (targetingPanel) targetingPanel.gameObject.SetActive(false); source = null; snapshot = null; selected = hovered = -1;
        }
        private void OnDestroy() => Cleanup();
    }
}
