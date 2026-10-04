using TMPro;
using UnityEngine;

namespace DungeonRun.UI
{
    [CreateAssetMenu(menuName = "Dungeon Run/Combat HUD Theme")]
    public sealed class CombatHUDTheme : ScriptableObject
    {
        public Sprite cardFrame, panelFrame, portrait, cardBack;
        public Sprite attackIcon, defenceIcon, dodgeIcon, healIcon, piercingIcon;
        public Sprite missIcon, chargeIcon, comboIcon, counterattackIcon;
        [Header("Presentation timing (never gameplay rules)")]
        [Range(.05f, 1f)] public float healthTweenDuration = .32f;
        [Range(.2f, 2f)] public float floatingTextDuration = .9f;
        [Range(0f, .3f)] public float combatEventSpacing = .07f;
        [Range(.1f, 1f)] public float revealTweenDuration = .25f;
        [Range(.1f, 1f)] public float terminalFadeDuration = .4f;
        [Range(8, 48)] public int feedbackPoolSize = 24;
        public Color damageColor = new Color(1f, .48f, .36f);
        public Color blockColor = new Color(.63f, .8f, 1f);
        public Color healColor = new Color(.4f, .95f, .65f);
        public Color validTargetColor = new Color(.45f, .85f, .7f);
        public TMP_FontAsset bodyFont, displayFont;
        public Color ink = new Color(.14f, .12f, .10f);
        public Color parchment = new Color(.84f, .78f, .61f);
        public Color gold = new Color(.77f, .62f, .34f);
        public Color panel = new Color(.065f, .083f, .09f, .93f);
        public Color text = new Color(.91f, .87f, .75f);
        [Tooltip("Painted V4 presentation. Null = V1/V3 visuals.")] public CombatHUDStyleV4 v4Style;
        public Sprite Icon(PreviewCardKind kind)
        {
            switch (kind)
            {
                case PreviewCardKind.Defence: return defenceIcon;
                case PreviewCardKind.Dodge: return dodgeIcon;
                case PreviewCardKind.Counterattack: return counterattackIcon ? counterattackIcon : dodgeIcon;
                case PreviewCardKind.Heal: return healIcon;
                case PreviewCardKind.Piercing: return piercingIcon;
                case PreviewCardKind.Miss: return missIcon;
                case PreviewCardKind.Charge: return chargeIcon;
                case PreviewCardKind.Combo: return comboIcon ? comboIcon : attackIcon;
                default: return attackIcon;
            }
        }
    }
}
