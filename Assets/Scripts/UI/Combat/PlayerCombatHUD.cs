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
        private int previousHealth = -1, previousMaxHealth = -1, previousActions = -1;
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
                else healthFill.DOFillAmount(fill, .35f).SetEase(Ease.OutCubic).SetUpdate(true);
                previousHealth = state.Health; previousMaxHealth = state.MaxHealth;
            }
            if (previousActions != state.Actions)
            { UIAnimationHelpers.Pulse(actionsText.rectTransform, instant || previousActions < 0); previousActions = state.Actions; }
        }
        private void OnDestroy()
        { if (healthFill) healthFill.DOKill(); if (actionsText) actionsText.rectTransform.DOKill(); }
    }
}
