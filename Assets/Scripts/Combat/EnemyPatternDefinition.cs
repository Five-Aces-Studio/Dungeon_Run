using UnityEngine;

namespace DungeonRun.Combat
{
    [CreateAssetMenu(menuName = "Dungeon Run/Combat/Enemy Pattern")]
    public sealed class EnemyPatternDefinition : ScriptableObject
    {
        [Header("Composition: pattern length is known entries + variable slots")]
        public CardData[] knownActions = new CardData[0];
        [Min(0)] public int variableSlots = 1;
        public CardData[] variableActionPool = new CardData[0];
        [Header("Memory behavior")]
        public bool shuffleOnSpawn = true;
        public PatternCycleMode cycleMode = PatternCycleMode.PreserveOrder;
        public bool regenerateVariableEntriesOnCycle = false;
    }
}
