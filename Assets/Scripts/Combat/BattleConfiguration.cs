using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonRun.Combat
{
    public sealed class BattleRules
    {
        public int HandSize { get; }
        public int ActionsPerTurn { get; }
        public bool RequireExactCount { get; }
        public int BaseBlock { get; }
        public int DrawAfterTurn { get; }
        public bool CapRefillAtHandSize { get; }
        public bool ShuffleInitialDeck { get; }
        public DeckRecycleMode RecycleMode { get; }
        public EnemyRevealMode RevealMode { get; }
        public BattleRules(int handSize = 5, int actions = 2, bool exact = true, int baseBlock = 0, int draw = 2,
            bool capRefill = true, bool shuffle = true, DeckRecycleMode recycle = DeckRecycleMode.ShuffleDiscardIntoDraw,
            EnemyRevealMode reveal = EnemyRevealMode.AfterPlayerCommit)
        {
            if (handSize < 1 || handSize > 10 || actions < 1 || actions > 4 || handSize < actions)
                throw new ArgumentException("Hand must be 1..10 and at least actions; actions must be 1..4.");
            if (draw < 0 || draw > 10 || baseBlock < 0 || baseBlock > 10000)
                throw new ArgumentException("Draw must be 0..10; base block must be nonnegative and bounded.");
            if (draw < (exact ? actions : 1))
                throw new ArgumentException("Draw must sustain the minimum cards committed per turn; lower values can permanently starve planning.");
            if (!Enum.IsDefined(typeof(DeckRecycleMode), recycle) || !Enum.IsDefined(typeof(EnemyRevealMode), reveal))
                throw new ArgumentException("Invalid recycle/reveal enum.");
            HandSize = handSize; ActionsPerTurn = actions; RequireExactCount = exact; BaseBlock = baseBlock;
            DrawAfterTurn = draw; CapRefillAtHandSize = capRefill; ShuffleInitialDeck = shuffle;
            RecycleMode = recycle; RevealMode = reveal;
        }
    }

    public sealed class PatternConfiguration
    {
        public IReadOnlyList<int> Known { get; }
        public int VariableSlots { get; }
        public IReadOnlyList<int> VariablePool { get; }
        public bool ShuffleOnSpawn { get; }
        public PatternCycleMode CycleMode { get; }
        public bool RegenerateVariables { get; }
        public PatternConfiguration(IEnumerable<int> known, int variableSlots, IEnumerable<int> pool,
            bool shuffle = true, PatternCycleMode cycle = PatternCycleMode.PreserveOrder, bool regenerate = false)
        {
            Known = Array.AsReadOnly((known ?? throw new ArgumentNullException(nameof(known))).ToArray());
            VariablePool = Array.AsReadOnly((pool ?? throw new ArgumentNullException(nameof(pool))).ToArray());
            if (variableSlots < 0 || Known.Count + variableSlots < 1 || Known.Count + variableSlots > 64 ||
                (variableSlots > 0 && VariablePool.Count == 0))
                throw new ArgumentException("Pattern requires 1..64 total entries and a pool for variable slots.");
            if (!Enum.IsDefined(typeof(PatternCycleMode), cycle)) throw new ArgumentException("Invalid pattern cycle enum.");
            VariableSlots = variableSlots; ShuffleOnSpawn = shuffle; CycleMode = cycle; RegenerateVariables = regenerate;
        }
    }

    public sealed class EnemyConfiguration
    {
        public string Name { get; }
        public int InitialHealth { get; }
        public int MaxHealth { get; }
        public int ActionsPerResolution { get; }
        public PatternConfiguration Pattern { get; }
        public EnemyConfiguration(string name, int initial, int maximum, int actions, PatternConfiguration pattern)
        {
            if (initial < 1 || maximum < initial || maximum > 100000 || actions < 1 || actions > 4)
                throw new ArgumentException("Enemy requires positive initial/max HP and 1..4 actions.");
            Name = name ?? "Enemy"; InitialHealth = initial; MaxHealth = maximum; ActionsPerResolution = actions;
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }
    }

    /// <summary>Complete frozen battle input; no Unity references or mutable ScriptableObjects.</summary>
    public sealed class BattleConfiguration
    {
        public BattleRules Rules { get; }
        public string PlayerName { get; }
        public int PlayerInitialHealth { get; }
        public int PlayerMaxHealth { get; }
        public IReadOnlyDictionary<int, ActionDefinition> Definitions { get; }
        public IReadOnlyList<int> StartingDeck { get; }
        public IReadOnlyList<EnemyConfiguration> Enemies { get; }
        public BattleConfiguration(BattleRules rules, string playerName, int initial, int maximum,
            IEnumerable<ActionDefinition> definitions, IEnumerable<int> deck, IEnumerable<EnemyConfiguration> enemies)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (initial < 1 || maximum < initial || maximum > 100000) throw new ArgumentException("Player requires positive initial/max HP.");
            PlayerName = playerName ?? "Player"; PlayerInitialHealth = initial; PlayerMaxHealth = maximum;
            var authored = (definitions ?? throw new ArgumentNullException(nameof(definitions))).ToArray();
            if (authored.Any(x => x == null)) throw new ArgumentException("Action definitions cannot contain null entries.");
            var map = authored.ToDictionary(x => x.Id);
            foreach (var action in map.Values) ValidateAction(action);
            Definitions = new ReadOnlyDictionary<int, ActionDefinition>(map);
            StartingDeck = Array.AsReadOnly((deck ?? throw new ArgumentNullException(nameof(deck))).ToArray());
            Enemies = Array.AsReadOnly((enemies ?? throw new ArgumentNullException(nameof(enemies))).ToArray());
            if (StartingDeck.Count < rules.HandSize || StartingDeck.Count > 256) throw new ArgumentException("Deck must cover initial hand and contain at most 256 authored instances.");
            if (Enemies.Count < 1 || Enemies.Count > 8 || Enemies.Any(x => x == null)) throw new ArgumentException("Encounter needs 1..8 enemies.");
            foreach (int id in StartingDeck.Concat(Enemies.SelectMany(x => x.Pattern.Known.Concat(x.Pattern.VariablePool))))
                if (!map.ContainsKey(id)) throw new ArgumentException("Missing action definition " + id + ".");
        }

        private static void ValidateAction(ActionDefinition action)
        {
            if (action.Id < 1 || string.IsNullOrWhiteSpace(action.Name)) throw new ArgumentException("Action requires positive ID and display name.");
            if (new[] { action.Damage, action.Block, action.Heal, action.Dodge }.Any(x => x < 0 || x > 10000) ||
                action.HitCount < 1 || action.HitCount > 16 || action.ChargeMultiplier < 1 || action.ChargeMultiplier > 8 ||
                action.ChargeTurns < 1 || action.ChargeTurns > 3) throw new ArgumentException("Invalid numeric action values: " + action.Name);
            if (!Enum.IsDefined(typeof(TargetMode), action.Targeting) || !Enum.IsDefined(typeof(PiercingMode), action.Piercing) ||
                !Enum.IsDefined(typeof(ActionIconKind), action.Icon)) throw new ArgumentException("Invalid action metadata enum: " + action.Name);
            if (action.Damage > 0 && (action.Targeting == TargetMode.None || action.Targeting == TargetMode.Self || action.Targeting == TargetMode.FriendlyTarget))
                throw new ArgumentException("Damage requires opponent/all-combatant targeting: " + action.Name);
            if (action.Damage == 0 && action.Heal > 0 && action.Targeting != TargetMode.Self && action.Targeting != TargetMode.FriendlyTarget)
                throw new ArgumentException("Healing-only actions require self/friendly targeting: " + action.Name);
            if (action.Damage == 0 && action.Piercing != PiercingMode.Normal) throw new ArgumentException("Piercing requires damage.");
            if (action.Damage == 0 && action.HitCount != 1) throw new ArgumentException("Multiple hits require damage.");
            if (action.Damage > 0 && action.Heal > 0)
                throw new ArgumentException("Combined damage and healing requires an explicit healing-target policy not implemented in V2: " + action.Name);
            if (action.Damage == 0 && action.Heal == 0 && action.Targeting != TargetMode.None && action.Targeting != TargetMode.Self)
                throw new ArgumentException("Defence/dodge/charge/miss actions require no target or self targeting: " + action.Name);
        }
    }
}
