using DG.Tweening;
using DungeonRun.UI.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>
    /// V4 Commit command plate: sprite, label and scale from <see cref="CommitVisualStateResolver"/>.
    /// Pointer handlers are visual only; the Button click still owns the commit request.
    /// </summary>
    public sealed class CommitButtonPresenter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image face;
        public TextMeshProUGUI label;
        private CombatHUDStyleV4 style;
        private CombatHUDSnapshot snapshot;
        private bool pointerOver, pointerDown;
        public CommitVisual Visual { get; private set; }

        public void Apply(CombatHUDSnapshot state, CombatHUDStyleV4 v4Style)
        {
            snapshot = state; style = v4Style;
            Repaint(false);
        }

        /// <summary>Forgets pointer state and restores the resting scale (rebind, capture reset).</summary>
        public void ResetPointer()
        {
            pointerOver = pointerDown = false;
            transform.DOKill(true); transform.localScale = Vector3.one;
            Repaint(false);
        }

        public void OnPointerEnter(PointerEventData e) { pointerOver = true; Repaint(true); }
        public void OnPointerExit(PointerEventData e) { pointerOver = pointerDown = false; Repaint(true); }
        public void OnPointerDown(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left) { pointerDown = true; Repaint(true); } }
        public void OnPointerUp(PointerEventData e) { pointerDown = false; Repaint(true); }

        private void Repaint(bool fromPointer)
        {
            if (!style || snapshot == null) return;
            Visual = CommitVisualStateResolver.Resolve(snapshot.IsPreview, snapshot.IsTerminal, (HudPhase)(int)snapshot.Phase,
                snapshot.IsResolving, snapshot.CanResolve, pointerOver, pointerDown);
            var sprite = style.CommitSprite(Visual.State);
            if (face && sprite) { face.sprite = sprite; face.type = Image.Type.Simple; face.color = Color.white; }
            if (label)
            {
                label.text = Visual.Label;
                label.color = Visual.State == CommitVisualState.Disabled ? style.commitLabelIdle :
                    Visual.State == CommitVisualState.Ready || Visual.State == CommitVisualState.Hover || Visual.State == CommitVisualState.Pressed
                        ? style.commitLabelReady : style.commitLabelBusy;
            }
            float scale = Visual.State == CommitVisualState.Hover ? style.hoverScale : Visual.State == CommitVisualState.Pressed ? style.pressedScale : 1;
            // Pointer input wins over the one-shot ready/click pulse; snapshot refreshes never cut a running pulse.
            if (fromPointer) transform.DOKill(true);
            if (fromPointer || !DOTween.IsTweening(transform)) transform.localScale = new Vector3(scale, scale, 1);
        }

        private void OnDisable() { pointerOver = pointerDown = false; transform.DOKill(true); transform.localScale = Vector3.one; }
    }
}
