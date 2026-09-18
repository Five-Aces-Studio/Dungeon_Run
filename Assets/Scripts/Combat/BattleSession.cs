using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonRun.Combat
{
    /// <summary>Authoritative deterministic battle. Presentation advances phases, never resolves rules.</summary>
    public sealed class BattleSession
    {
        private sealed class Actor
        {
            public int Id, Health, Maximum, Block, Dodge;
            public string Name;
            public readonly List<Charge> Charges = new List<Charge>();
        }
        private sealed class Charge { public int First, Last, Multiplier; }
        private sealed class Planned
        {
            public int ActorId, CardId;
            public ActionDefinition Action;
            public int[] Targets;
        }
        private readonly BattleConfiguration config;
        private readonly BattleRandom random;
        private readonly Actor[] actors;
        private readonly BattlePattern[] patterns;
        private readonly List<CardInstance> draw = new List<CardInstance>();
        private readonly List<CardInstance> hand = new List<CardInstance>();
        private readonly List<CardInstance> discard = new List<CardInstance>();
        private readonly QueuedActionSnapshot[] slots;
        private readonly List<int> handOrder = new List<int>();
        private readonly List<Planned> planned = new List<Planned>();
        private readonly List<BattleEvent> events = new List<BattleEvent>();
        private BattlePhase phase = BattlePhase.Planning;
        private int turn = 1;
        private bool busy;
        public event Action Changed;
        public event Action<BattleEvent> EventRaised;

        public BattleSession(BattleConfiguration configuration, int seed)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            random = new BattleRandom(seed);
            slots = new QueuedActionSnapshot[config.Rules.ActionsPerTurn];
            actors = new Actor[config.Enemies.Count + 1];
            actors[0] = new Actor { Id = 0, Name = config.PlayerName, Health = config.PlayerInitialHealth, Maximum = config.PlayerMaxHealth };
            patterns = new BattlePattern[config.Enemies.Count];
            for (int i = 0; i < config.Enemies.Count; i++)
            {
                var enemy = config.Enemies[i];
                actors[i + 1] = new Actor { Id = i + 1, Name = enemy.Name, Health = enemy.InitialHealth, Maximum = enemy.MaxHealth };
                patterns[i] = new BattlePattern(enemy.Pattern, random);
            }
            for (int i = 0; i < config.StartingDeck.Count; i++) draw.Add(new CardInstance(i + 1, config.StartingDeck[i]));
            if (config.Rules.ShuffleInitialDeck) random.Shuffle(draw);
            DrawCards(config.Rules.HandSize);
            events.Clear();
        }

        public ActionDefinition GetDefinition(int definitionId) => config.Definitions[definitionId];
        public BattleSnapshot Snapshot => new BattleSnapshot(phase, turn, Project(actors[0]), actors.Skip(1).Select(Project),
            hand, slots, draw.Count, discard.Count, CommitError(true) == null);

        private ActorSnapshot Project(Actor actor)
        {
            string intent = "Unknown";
            if (actor.Health <= 0) intent = "Defeated";
            else if (actor.Id > 0)
            {
                var revealed = planned.Where(x => x.ActorId == actor.Id).Select(x => x.Action.Name).ToArray();
                if (revealed.Length > 0) intent = string.Join(" + ", revealed);
                else if (config.Rules.RevealMode == EnemyRevealMode.AlwaysVisibleForDebug)
                    intent = GetDefinition(patterns[actor.Id - 1].Peek()).Name + " [DEBUG]";
            }
            return new ActorSnapshot(actor.Id, actor.Name, actor.Health, actor.Maximum, actor.Block, actor.Dodge, intent);
        }

        public IReadOnlyList<DebugPatternSnapshot> DebugPatterns(bool explicitlyAuthorizedLabDebug)
        {
            if (!explicitlyAuthorizedLabDebug) throw new UnauthorizedAccessException("Full patterns require explicit Lab debug authorization.");
            return Array.AsReadOnly(patterns.Select((p, i) => new DebugPatternSnapshot(i + 1, p.Index,
                p.Entries.Select(x => GetDefinition(x.Definition).Name), p.Entries.Select(x => x.Variable))).ToArray());
        }

        public bool TryQueue(int cardId, int slot, TargetSelection targets, out string error)
        {
            error = PlanningError();
            if (error != null) return false;
            if (slot < 0 || slot >= slots.Length || slots[slot] != null) { error = "Choose an empty action slot."; return false; }
            var card = hand.Find(x => x.Id == cardId);
            if (card == null) { error = "Card is not in hand."; return false; }
            if (!ResolveTargets(0, GetDefinition(card.DefinitionId), targets, out int[] frozen, out error)) return false;
            if (slots.All(x => x == null)) { handOrder.Clear(); handOrder.AddRange(hand.Select(x => x.Id)); }
            hand.Remove(card); slots[slot] = new QueuedActionSnapshot(card, frozen);
            Emit(BattleEventKind.Queued, 0, 0, cardId); Publish(); return true;
        }

        public bool TryUnqueue(int slot, out string error)
        {
            error = PlanningError();
            if (error != null) return false;
            if (slot < 0 || slot >= slots.Length || slots[slot] == null) { error = "Slot is empty or invalid."; return false; }
            var card = slots[slot].Card; slots[slot] = null;
            int rank = handOrder.IndexOf(card.Id);
            int insert = hand.FindIndex(x => handOrder.IndexOf(x.Id) > rank);
            hand.Insert(insert < 0 ? hand.Count : insert, card);
            Emit(BattleEventKind.Unqueued, 0, 0, card.Id); Publish(); return true;
        }

        public bool TryCommit(out string error)
        {
            error = CommitError(); if (error != null) return false;
            planned.Clear();
            foreach (var slot in slots.Where(x => x != null))
                planned.Add(new Planned { ActorId = 0, CardId = slot.Card.Id, Action = GetDefinition(slot.Card.DefinitionId), Targets = slot.Targets.ToArray() });
            for (int i = 1; i < actors.Length; i++)
            {
                if (actors[i].Health <= 0) continue;
                for (int n = 0; n < config.Enemies[i - 1].ActionsPerResolution; n++)
                {
                    var action = GetDefinition(patterns[i - 1].Take());
                    var selection = action.Targeting == TargetMode.SingleOpponent ? new TargetSelection(0) : new TargetSelection();
                    if (action.Targeting == TargetMode.FriendlyTarget) selection = new TargetSelection(i);
                    if (!ResolveTargets(i, action, selection, out int[] targets, out string reason))
                        throw new InvalidOperationException("Validated enemy action cannot target: " + reason);
                    planned.Add(new Planned { ActorId = i, Action = action, Targets = targets });
                    Emit(BattleEventKind.EnemyRevealed, i, 0, 0, 0, action.Name);
                }
            }
            Emit(BattleEventKind.Committed); SetPhase(BattlePhase.EnemyReveal); Publish(); return true;
        }

        public bool Advance(out string error)
        {
            error = null;
            if (busy) { error = "A command is already being published."; return false; }
            switch (phase)
            {
                case BattlePhase.EnemyReveal: Resolve(); SetPhase(BattlePhase.Resolution); break;
                case BattlePhase.Resolution:
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == null) continue;
                        discard.Add(slots[i].Card); Emit(BattleEventKind.CardDiscarded, 0, 0, slots[i].Card.Id); slots[i] = null;
                    }
                    foreach (var actor in actors) { actor.Block = 0; actor.Dodge = 0; }
                    SetPhase(BattlePhase.Cleanup); break;
                case BattlePhase.Cleanup:
                    bool playerAlive = actors[0].Health > 0, enemyAlive = actors.Skip(1).Any(x => x.Health > 0);
                    if (!playerAlive || !enemyAlive) SetPhase(playerAlive ? BattlePhase.Victory : enemyAlive ? BattlePhase.Defeat : BattlePhase.Draw);
                    else { DrawCards(config.Rules.DrawAfterTurn); SetPhase(BattlePhase.Refill); }
                    break;
                case BattlePhase.Refill:
                    turn++; planned.Clear(); handOrder.Clear();
                    foreach (var actor in actors) actor.Charges.RemoveAll(x => x.Last < turn);
                    SetPhase(BattlePhase.Planning); break;
                default: error = "Only committed nonterminal phases can advance."; return false;
            }
            Publish(); return true;
        }

        private string PlanningError() => busy ? "A command is already being published." : phase != BattlePhase.Planning ? "Battle is not planning." : null;
        private string CommitError(bool projection = false)
        {
            string error = projection ? (phase == BattlePhase.Planning ? null : "Battle is not planning.") : PlanningError(); if (error != null) return error;
            int count = slots.Count(x => x != null);
            if (count == 0 || (config.Rules.RequireExactCount && count != slots.Length)) return "Queue the required number of cards.";
            return null;
        }

        private bool ResolveTargets(int source, ActionDefinition action, TargetSelection selection, out int[] result, out string error)
        {
            result = (selection?.ActorIds ?? Array.Empty<int>()).ToArray(); error = null;
            bool Opponent(int id) => (source == 0) != (id == 0);
            bool Valid(int id) => id >= 0 && id < actors.Length && actors[id].Health > 0;
            switch (action.Targeting)
            {
                case TargetMode.None:
                    if (result.Length != 0) error = "This action has no target.";
                    break;
                case TargetMode.Self:
                    if (result.Length == 0) result = new[] { source };
                    if (result.Length != 1 || result[0] != source) error = "This action targets its owner.";
                    break;
                case TargetMode.SingleOpponent:
                    if ((result.Length != 1 && result.Length != action.HitCount) || result.Any(id => !Valid(id) || !Opponent(id)))
                        error = "Choose one living opponent, or one living opponent per hit.";
                    break;
                case TargetMode.FriendlyTarget:
                    if (result.Length != 1 || !Valid(result[0]) || Opponent(result[0])) error = "Choose one living friendly actor.";
                    break;
                case TargetMode.AllOpponents:
                case TargetMode.AllCombatants:
                    if (result.Length != 0) { error = "Area targets are captured automatically."; break; }
                    result = actors.Where(x => x.Health > 0 && (action.Targeting == TargetMode.AllCombatants || Opponent(x.Id))).Select(x => x.Id).ToArray();
                    break;
            }
            return error == null;
        }

        private void Resolve()
        {
            // Stable tie order: player slot order, then enemy ID/action order, then hit index/target ID.
            // All declarations were captured alive at commit. No later casualty cancels a declared hit.
            foreach (var actor in actors) { actor.Block = actor.Id == 0 ? config.Rules.BaseBlock : 0; actor.Dodge = 0; }
            foreach (var item in planned) { actors[item.ActorId].Block += item.Action.Block; actors[item.ActorId].Dodge += item.Action.Dodge; }
            var damage = new int[actors.Length];
            foreach (var item in planned)
            {
                var action = item.Action;
                if (action.Damage == 0) continue;
                int multiplier = actors[item.ActorId].Charges.Where(x => x.First <= turn && x.Last >= turn).Select(x => x.Multiplier).DefaultIfEmpty(1).Max();
                for (int hit = 0; hit < action.HitCount; hit++)
                {
                    var targets = action.Targeting == TargetMode.SingleOpponent
                        ? new[] { item.Targets[item.Targets.Length == 1 ? 0 : hit] } : item.Targets;
                    foreach (int id in targets)
                    {
                        var target = actors[id]; int amount = action.Damage * multiplier;
                        if (target.Dodge > 0) { target.Dodge--; Emit(BattleEventKind.Dodged, item.ActorId, id, item.CardId, 1); continue; }
                        if (action.Piercing == PiercingMode.Normal)
                        {
                            int blocked = Math.Min(target.Block, amount); target.Block -= blocked; amount -= blocked;
                            if (blocked > 0) Emit(BattleEventKind.Blocked, item.ActorId, id, item.CardId, blocked);
                        }
                        damage[id] += amount;
                        if (amount > 0) Emit(BattleEventKind.Damage, item.ActorId, id, item.CardId, amount);
                    }
                }
            }
            foreach (var actor in actors)
            {
                int before = actor.Health; actor.Health = Math.Max(0, actor.Health - damage[actor.Id]);
                if (before > 0 && actor.Health == 0) Emit(BattleEventKind.Defeated, actor.Id, actor.Id);
            }
            foreach (var item in planned)
            {
                if (actors[item.ActorId].Health == 0) continue;
                var action = item.Action;
                // Healing-only actions use their frozen friendly/self targets; healing cannot resurrect.
                // Configuration rejects ambiguous mixed attack/heal actions rather than inventing lifesteal.
                var recipients = item.Targets;
                if (action.Heal > 0) foreach (int id in recipients)
                {
                    var target = actors[id]; if (target.Health == 0) continue;
                    int amount = Math.Min(action.Heal, target.Maximum - target.Health); target.Health += amount;
                    if (amount > 0) Emit(BattleEventKind.Healed, item.ActorId, id, item.CardId, amount);
                }
                // Bounded next-turn charge, strongest active multiplier wins (never multiplicative stacking).
                if (action.ChargeMultiplier > 1) actors[item.ActorId].Charges.Add(new Charge
                    { First = turn + 1, Last = turn + action.ChargeTurns, Multiplier = action.ChargeMultiplier });
            }
        }

        private void DrawCards(int count)
        {
            int cap = config.Rules.CapRefillAtHandSize ? config.Rules.HandSize : 10;
            while (count-- > 0 && hand.Count < cap)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw.AddRange(discard); discard.Clear();
                    if (config.Rules.RecycleMode == DeckRecycleMode.ShuffleDiscardIntoDraw) random.Shuffle(draw);
                }
                var card = draw[0]; draw.RemoveAt(0); hand.Add(card); Emit(BattleEventKind.CardDrawn, 0, 0, card.Id);
            }
        }
        private void SetPhase(BattlePhase value) { phase = value; Emit(BattleEventKind.PhaseChanged, message: value.ToString()); }
        private void Emit(BattleEventKind kind, int actor = 0, int target = 0, int card = 0, int amount = 0, string message = "")
            => events.Add(new BattleEvent(kind, actor, target, card, amount, message));
        private void Publish()
        {
            busy = true;
            try { foreach (var item in events.ToArray()) EventRaised?.Invoke(item); Changed?.Invoke(); }
            finally { events.Clear(); busy = false; }
        }
    }

    internal sealed class BattleRandom
    {
        private uint state;
        public BattleRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x9E3779B9u; }
        public int Next(int maximum) { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (int)(state % (uint)maximum); }
        public void Shuffle<T>(IList<T> list)
        { for (int i = list.Count - 1; i > 0; i--) { int j = Next(i + 1); T value = list[i]; list[i] = list[j]; list[j] = value; } }
    }

    internal sealed class BattlePattern
    {
        internal sealed class Entry { public int Definition; public bool Variable; }
        public readonly List<Entry> Entries = new List<Entry>();
        public int Index { get; private set; }
        private readonly PatternConfiguration config;
        private readonly BattleRandom random;
        public BattlePattern(PatternConfiguration configuration, BattleRandom rng)
        {
            config = configuration; random = rng;
            Entries.AddRange(config.Known.Select(id => new Entry { Definition = id }));
            for (int i = 0; i < config.VariableSlots; i++) Entries.Add(new Entry { Definition = Variable(), Variable = true });
            if (config.ShuffleOnSpawn) random.Shuffle(Entries);
        }
        private int Variable() => config.VariablePool[random.Next(config.VariablePool.Count)];
        public int Peek() => Entries[Index].Definition;
        public int Take()
        {
            int result = Peek(); Index++;
            if (Index == Entries.Count)
            {
                Index = 0;
                if (config.RegenerateVariables) foreach (var entry in Entries.Where(x => x.Variable)) entry.Definition = Variable();
                if (config.CycleMode == PatternCycleMode.ShuffleOnCycle) random.Shuffle(Entries);
            }
            return result;
        }
    }
}
