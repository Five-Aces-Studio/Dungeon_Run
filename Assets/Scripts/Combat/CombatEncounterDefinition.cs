using System;
using UnityEngine;

namespace DungeonRun.Combat
{
    [Serializable]
    public sealed class CombatEnemyDefinition
    {
        public string displayName = "Enemy";
        [Min(1)] public int maxHealth = 5;
        [Min(1)] public int initialHealth = 5;
        [Range(1, 4)] public int actionsPerResolution = 1;
        public EnemyPatternDefinition pattern;
    }

    [CreateAssetMenu(menuName = "Dungeon Run/Combat/Encounter")]
    public sealed class CombatEncounterDefinition : ScriptableObject
    {
        [Header("Player (one source for starting HP)")]
        public string playerName = "Wayfarer";
        [Min(1)] public int playerMaxHealth = 10;
        [Min(1)] public int playerInitialHealth = 10;
        [Header("Authored deck: baseline 21; instances remain conserved")]
        public CardData[] startingDeck = new CardData[0];
        [Header("Encounter roster (1-8)")]
        public CombatEnemyDefinition[] enemies = new CombatEnemyDefinition[0];
    }
}
