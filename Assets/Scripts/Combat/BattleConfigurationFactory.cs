using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonRun.Combat
{
    /// <summary>Unity definition boundary; output freezes all numeric configuration before play.</summary>
    public static class BattleConfigurationFactory
    {
        public static BattleConfiguration Create(CombatRulesetDefinition rules, CombatEncounterDefinition encounter,
            out Dictionary<int, CardData> definitions)
        {
            if (!rules || !encounter) throw new ArgumentException("Assign a ruleset and encounter definition.");
            var cards = new Dictionary<CardData, int>();
            var mapping = new Dictionary<int, CardData>();
            var actions = new List<ActionDefinition>();
            int Register(CardData card)
            {
                if (!card) throw new ArgumentException("Missing CardData in deck or enemy pattern.");
                if (cards.TryGetValue(card, out int existing)) return existing;
                if (card.actionCost != 1) throw new ArgumentException(card.cardName + ": Live V2 uses one card per action slot, not variable energy costs; actionCost must be 1.");
                if (card.poisonPower != 0 || card.poisonTurns != 0 || card.multipleTurns || card.remainingTurns != 0)
                    throw new ArgumentException(card.cardName + ": poison/legacy multi-turn state is unsupported in V2. Use bounded charge metadata only.");
                int id = cards.Count + 1; cards.Add(card, id); mapping.Add(id, card);
                actions.Add(new ActionDefinition(id, card.cardName, card.attackPower, card.defensePower, card.healPower,
                    card.dodgeCount, card.hitCount, card.targetMode, card.piercingMode, card.presentationKind,
                    card.chargeMultiplier, card.chargeTurns));
                return id;
            }
            var frozenRules = new BattleRules(rules.handSize, rules.playerActionsPerTurn, rules.requireExactActionCount,
                rules.baseBlock, rules.cardsDrawnAfterTurn, rules.refillHandAfterResolution, rules.shuffleInitialPlayerDeck,
                rules.drawPileRecycleMode, rules.enemyActionRevealMode);
            if (encounter.startingDeck == null || encounter.enemies == null) throw new ArgumentException("Deck/encounter arrays cannot be null.");
            int[] deck = encounter.startingDeck.Select(Register).ToArray();
            var enemies = new List<EnemyConfiguration>();
            foreach (var enemy in encounter.enemies)
            {
                if (enemy == null) throw new ArgumentException("Null encounter enemy.");
                var pattern = enemy.pattern ? enemy.pattern : rules.defaultEnemyPattern;
                if (!pattern) throw new ArgumentException(enemy.displayName + ": assign enemy pattern or ruleset default.");
                if (pattern.knownActions == null || pattern.variableActionPool == null) throw new ArgumentException("Pattern arrays cannot be null.");
                var frozen = new PatternConfiguration(pattern.knownActions.Select(Register), pattern.variableSlots,
                    pattern.variableActionPool.Select(Register), pattern.shuffleOnSpawn, pattern.cycleMode, pattern.regenerateVariableEntriesOnCycle);
                enemies.Add(new EnemyConfiguration(enemy.displayName, enemy.initialHealth, enemy.maxHealth, enemy.actionsPerResolution, frozen));
            }
            var configuration = new BattleConfiguration(frozenRules, encounter.playerName, encounter.playerInitialHealth,
                encounter.playerMaxHealth, actions, deck, enemies);
            definitions = mapping;
            return configuration;
        }

        public static string Validate(CombatRulesetDefinition rules, CombatEncounterDefinition encounter)
        {
            try { Create(rules, encounter, out _); return ""; }
            catch (Exception exception) { return exception.Message; }
        }
    }
}
