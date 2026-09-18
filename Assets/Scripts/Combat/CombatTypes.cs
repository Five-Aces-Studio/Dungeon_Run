using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonRun.Combat
{
    public enum BattlePhase { Planning, EnemyReveal, Resolution, Cleanup, Refill, Victory, Defeat, Draw }
    public enum TargetMode { None, Self, SingleOpponent, FriendlyTarget, AllOpponents, AllCombatants }
    public enum PiercingMode { Normal, IgnoreBlock }
    public enum ActionIconKind { Attack, Defence, Dodge, Heal, Piercing, Combo, Counterattack, Charge, Miss }
    public enum DeckRecycleMode { ShuffleDiscardIntoDraw, PreserveOrder }
    public enum PatternCycleMode { PreserveOrder, ShuffleOnCycle }
    public enum EnemyRevealMode { AfterPlayerCommit, AlwaysVisibleForDebug }
    public enum BattleEventKind { Queued, Unqueued, Committed, EnemyRevealed, Damage, Blocked, Dodged, Healed, Defeated, CardDiscarded, CardDrawn, PhaseChanged }

    /// <summary>Immutable action values. Unity assets are converted once, never consulted during battle.</summary>
    public sealed class ActionDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public int Damage { get; }
        public int Block { get; }
        public int Heal { get; }
        public int Dodge { get; }
        public int HitCount { get; }
        public TargetMode Targeting { get; }
        public PiercingMode Piercing { get; }
        public ActionIconKind Icon { get; }
        public int ChargeMultiplier { get; }
        public int ChargeTurns { get; }
        public ActionDefinition(int id, string name, int damage, int block, int heal, int dodge, int hitCount,
            TargetMode targeting, PiercingMode piercing, ActionIconKind icon, int chargeMultiplier = 1, int chargeTurns = 1)
        {
            Id = id; Name = name; Damage = damage; Block = block; Heal = heal; Dodge = dodge;
            HitCount = hitCount; Targeting = targeting; Piercing = piercing; Icon = icon;
            ChargeMultiplier = chargeMultiplier; ChargeTurns = chargeTurns;
        }
    }

    public sealed class TargetSelection
    {
        public IReadOnlyList<int> ActorIds { get; }
        public TargetSelection(params int[] actorIds) => ActorIds = Array.AsReadOnly((int[])(actorIds ?? Array.Empty<int>()).Clone());
    }

    public sealed class CardInstance
    {
        public int Id { get; }
        public int DefinitionId { get; }
        public CardInstance(int id, int definitionId) { Id = id; DefinitionId = definitionId; }
    }

    public sealed class QueuedActionSnapshot
    {
        public CardInstance Card { get; }
        public IReadOnlyList<int> Targets { get; }
        public QueuedActionSnapshot(CardInstance card, IEnumerable<int> targets)
        { Card = card; Targets = Array.AsReadOnly(targets.ToArray()); }
    }

    public sealed class ActorSnapshot
    {
        public int Id { get; }
        public string Name { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int Block { get; }
        public int Dodge { get; }
        public bool Alive => Health > 0;
        public string Intent { get; }
        public ActorSnapshot(int id, string name, int health, int maximum, int block, int dodge, string intent)
        { Id = id; Name = name; Health = health; MaxHealth = maximum; Block = block; Dodge = dodge; Intent = intent; }
    }

    public sealed class BattleSnapshot
    {
        public BattlePhase Phase { get; }
        public int Turn { get; }
        public ActorSnapshot Player { get; }
        public IReadOnlyList<ActorSnapshot> Enemies { get; }
        public IReadOnlyList<CardInstance> Hand { get; }
        public IReadOnlyList<QueuedActionSnapshot> Slots { get; }
        public int DrawCount { get; }
        public int DiscardCount { get; }
        public int ActionsPerTurn => Slots.Count;
        public int ActionsRemaining => Slots.Count(x => x == null);
        public bool CanCommit { get; }
        public bool IsTerminal => Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat || Phase == BattlePhase.Draw;
        public BattleSnapshot(BattlePhase phase, int turn, ActorSnapshot player, IEnumerable<ActorSnapshot> enemies,
            IEnumerable<CardInstance> hand, IEnumerable<QueuedActionSnapshot> slots, int draw, int discard, bool canCommit)
        {
            Phase = phase; Turn = turn; Player = player; Enemies = Array.AsReadOnly(enemies.ToArray());
            Hand = Array.AsReadOnly(hand.ToArray()); Slots = Array.AsReadOnly(slots.ToArray());
            DrawCount = draw; DiscardCount = discard; CanCommit = canCommit;
        }
    }

    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; }
        public int ActorId { get; }
        public int TargetId { get; }
        public int CardId { get; }
        public int Amount { get; }
        public string Message { get; }
        public BattleEvent(BattleEventKind kind, int actorId = 0, int targetId = 0, int cardId = 0, int amount = 0, string message = "")
        { Kind = kind; ActorId = actorId; TargetId = targetId; CardId = cardId; Amount = amount; Message = message; }
    }

    public sealed class DebugPatternSnapshot
    {
        public int EnemyId { get; }
        public int NextIndex { get; }
        public IReadOnlyList<string> Actions { get; }
        public IReadOnlyList<bool> VariableEntries { get; }
        public DebugPatternSnapshot(int enemy, int index, IEnumerable<string> actions, IEnumerable<bool> variable)
        { EnemyId = enemy; NextIndex = index; Actions = Array.AsReadOnly(actions.ToArray()); VariableEntries = Array.AsReadOnly(variable.ToArray()); }
    }
}
