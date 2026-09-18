using TMPro;
using UnityEngine;

namespace DungeonRun.UI
{
    [CreateAssetMenu(menuName = "Dungeon Run/Combat HUD Theme")]
    public sealed class CombatHUDTheme : ScriptableObject
    {
        public Sprite cardFrame, panelFrame, portrait, cardBack;
        public Sprite attackIcon, defenceIcon, dodgeIcon, healIcon, piercingIcon;
        public TMP_FontAsset bodyFont, displayFont;
        public Color ink = new Color(.14f, .12f, .10f);
        public Color parchment = new Color(.84f, .78f, .61f);
        public Color gold = new Color(.77f, .62f, .34f);
        public Color panel = new Color(.065f, .083f, .09f, .93f);
        public Color text = new Color(.91f, .87f, .75f);
        public Sprite Icon(PreviewCardKind kind)
        {
            switch (kind)
            {
                case PreviewCardKind.Defence: return defenceIcon;
                case PreviewCardKind.Dodge: return dodgeIcon;
                case PreviewCardKind.Counterattack: return dodgeIcon;
                case PreviewCardKind.Heal: return healIcon;
                case PreviewCardKind.Piercing: return piercingIcon;
                case PreviewCardKind.Miss: case PreviewCardKind.Charge: return null;
                default: return attackIcon;
            }
        }
    }
}
