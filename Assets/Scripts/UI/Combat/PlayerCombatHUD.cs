using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.UI.Presentation;
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
        [Header("V4 crest (unused by V1/V3 themes)")]
        public Image healthGhost;
        [Tooltip("HP track rect, used to anchor floating damage numbers.")] public RectTransform healthTrack;
        public RectTransform pipRow;
        [Tooltip("First pip of the row; extra pips are cloned from it.")] public Image pipTemplate;
        public GameObject blockChip, dodgeChip;
        public TextMeshProUGUI blockChipText, dodgeChipText;
        private readonly List<Image> pips = new List<Image>();
        public void Render(CombatHUDSnapshot state, CombatHUDTheme theme, bool instant)
        {
            var style = theme ? theme.v4Style : null;
            if (style) { RenderV4(state, theme, style, instant); return; }
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
        private void RenderV4(CombatHUDSnapshot state, CombatHUDTheme theme, CombatHUDStyleV4 style, bool instant)
        {
            if (theme.portrait) { portrait.sprite = theme.portrait; portrait.color = Color.white; }
            healthText.text = state.Health + " / " + state.MaxHealth;
            actionsText.text = ActionPipLayout.Counter(state.Actions, state.MaxActions);
            floorText.text = "FLOOR " + state.Floor.ToString("00");
            drawText.text = state.DrawCount.ToString(); discardText.text = state.DiscardCount.ToString();
            if (previousHealth != state.Health || previousMaxHealth != state.MaxHealth)
            {
                healthFill.DOKill();
                float fill = state.MaxHealth <= 0 ? 0 : state.Health / (float)state.MaxHealth;
                bool snap = instant || previousHealth < 0;
                if (snap) healthFill.fillAmount = fill;
                else healthFill.DOFillAmount(fill, theme.healthTweenDuration).SetEase(Ease.OutCubic).SetUpdate(true);
                if (healthGhost)
                {
                    // Damage: the ghost keeps the old fill, then drains after a beat. Heal: it snaps.
                    healthGhost.DOKill(); healthGhost.color = style.ghostColor;
                    if (snap || state.Health >= previousHealth) healthGhost.fillAmount = fill;
                    else healthGhost.DOFillAmount(fill, theme.healthTweenDuration).SetDelay(.35f).SetEase(Ease.OutCubic).SetUpdate(true);
                }
                if (!instant && previousHealth >= 0)
                {
                    portrait.DOKill(); portrait.color = state.Health < previousHealth ? theme.damageColor : theme.healColor;
                    portrait.DOColor(Color.white, theme.healthTweenDuration).SetUpdate(true);
                }
                previousHealth = state.Health; previousMaxHealth = state.MaxHealth;
            }
            RenderPips(ActionPipLayout.Pips(state.Actions, state.MaxActions), style);
            RenderChip(blockChip, blockChipText, state.PlayerBlock);
            RenderChip(dodgeChip, dodgeChipText, state.PlayerDodge);
            CombatHUDStyleV4.PackChips(blockChip, dodgeChip);
            if (previousDraw != state.DrawCount)
            { UIAnimationHelpers.Pulse(drawText.rectTransform, instant || previousDraw < 0); previousDraw = state.DrawCount; }
            if (previousDiscard != state.DiscardCount)
            { UIAnimationHelpers.Pulse(discardText.rectTransform, instant || previousDiscard < 0); previousDiscard = state.DiscardCount; }
            if (previousActions != state.Actions)
            { UIAnimationHelpers.Pulse(actionsText.rectTransform, instant || previousActions < 0); previousActions = state.Actions; }
        }
        private void RenderPips(PipState[] states, CombatHUDStyleV4 style)
        {
            if (!pipRow || !pipTemplate) return;
            pipRow.gameObject.SetActive(states.Length > 0);
            if (pips.Count == 0) pips.Add(pipTemplate);
            while (pips.Count < states.Length)
            { var pip = Instantiate(pipTemplate, pipRow); pip.name = "Pip" + pips.Count; pips.Add(pip); }
            for (int i = 0; i < pips.Count; i++)
            {
                bool visible = i < states.Length;
                pips[i].gameObject.SetActive(visible);
                if (visible) pips[i].sprite = states[i] == PipState.Available ? style.pipOn : style.pipOff;
            }
        }
        private static void RenderChip(GameObject chip, TextMeshProUGUI text, int value)
        {
            if (!chip) return;
            chip.SetActive(value > 0);
            if (text && value > 0) text.text = value.ToString();
        }
        private void OnDestroy()
        { if (portrait) portrait.DOKill(); if (drawText) drawText.rectTransform.DOKill(); if (discardText) discardText.rectTransform.DOKill(); if (healthFill) healthFill.DOKill(); if (actionsText) actionsText.rectTransform.DOKill(); if (healthGhost) healthGhost.DOKill(); }
    }
}
