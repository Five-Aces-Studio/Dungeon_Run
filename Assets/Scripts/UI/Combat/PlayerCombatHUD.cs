using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    public sealed class PlayerCombatHUD : MonoBehaviour
    {
        public Image portrait, healthFill;
        public TextMeshProUGUI healthText, actionsText, floorText, drawText, discardText;
        private int previousHealth = -1, previousMaxHealth = -1, previousActions = -1, previousDraw = -1, previousDiscard = -1;
        public void Render(CombatHUDSnapshot state, CombatHUDTheme theme, bool instant)
        {
            if (theme.portrait) { portrait.sprite = theme.portrait; portrait.color = Color.white; }
            healthText.text = state.Health + " / " + state.MaxHealth;
            actionsText.text = state.Actions + " / " + state.MaxActions;
            floorText.text = "FLOOR " + state.Floor.ToString("00");
            drawText.text = state.DrawCount.ToString(); discardText.text = state.DiscardCount.ToString();
            if (previousHealth != state.Health || previousMaxHealth != state.MaxHealth)
            {
                healthFill.DOKill();
                float fill = state.MaxHealth <= 0 ? 0 : state.Health / (float)state.MaxHealth;
                if (instant || previousHealth < 0) healthFill.fillAmount = fill;
                else healthFill.DOFillAmount(fill, theme.healthTweenDuration).SetEase(Ease.OutCubic).SetUpdate(true);
                if (!instant && previousHealth >= 0)
                {
                    portrait.DOKill(); portrait.color = state.Health < previousHealth ? theme.damageColor : theme.healColor;
                    portrait.DOColor(Color.white, theme.healthTweenDuration).SetUpdate(true);
                }
                previousHealth = state.Health; previousMaxHealth = state.MaxHealth;
            }
            if (previousDraw != state.DrawCount)
            { UIAnimationHelpers.Pulse(drawText.rectTransform, instant || previousDraw < 0); previousDraw = state.DrawCount; }
            if (previousDiscard != state.DiscardCount)
            { UIAnimationHelpers.Pulse(discardText.rectTransform, instant || previousDiscard < 0); previousDiscard = state.DiscardCount; }
            if (previousActions != state.Actions)
            { UIAnimationHelpers.Pulse(actionsText.rectTransform, instant || previousActions < 0); previousActions = state.Actions; }
        }
        private void OnDestroy()
        { if (portrait) portrait.DOKill(); if (drawText) drawText.rectTransform.DOKill(); if (discardText) discardText.rectTransform.DOKill(); if (healthFill) healthFill.DOKill(); if (actionsText) actionsText.rectTransform.DOKill(); }
    }
}
