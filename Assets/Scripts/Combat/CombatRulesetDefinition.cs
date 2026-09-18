using UnityEngine;

namespace DungeonRun.Combat
{
    [CreateAssetMenu(menuName = "Dungeon Run/Combat/Ruleset")]
    public sealed class CombatRulesetDefinition : ScriptableObject
    {
        [Header("Player planning")]
        [Range(1, 10)] public int handSize = 5;
        [Range(1, 4)] public int playerActionsPerTurn = 2;
        public bool requireExactActionCount = true;
        [Min(0)] public int baseBlock = 0;
        [Header("Card flow")]
        [Range(0, 10)] public int cardsDrawnAfterTurn = 2;
        [Tooltip("Draw the configured number, capped at handSize. Disable to test bounded accumulating hands (maximum 10).")]
        public bool refillHandAfterResolution = true;
        public bool shuffleInitialPlayerDeck = true;
        public DeckRecycleMode drawPileRecycleMode = DeckRecycleMode.ShuffleDiscardIntoDraw;
        [Header("Information")]
        public EnemyRevealMode enemyActionRevealMode = EnemyRevealMode.AfterPlayerCommit;
        [Header("Optional pattern default; encounter entries may override")]
        public EnemyPatternDefinition defaultEnemyPattern;
    }
}
