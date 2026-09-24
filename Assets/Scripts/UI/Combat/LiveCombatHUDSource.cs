using System;
using System.Collections.Generic;
using System.Linq;
using DungeonRun.Combat;
using UnityEngine;

namespace DungeonRun.UI
{
    /// <summary>Read-only presentation projection and command translation. No combat arithmetic lives here.</summary>
    public sealed class LiveCombatHUDSource : MonoBehaviour, ICombatHUDSource, ICombatTargetingSource
    {
        public CombatBattleController battle;
        [Tooltip("Roster order: enemy actor IDs start at 1. Presentation anchors only.")]
        public Renderer[] enemyAnchors = Array.Empty<Renderer>();
        public int dungeonFloor = 1;
        public event Action Changed;
        public event Action<int, CardMotion> CardMoving;
        private readonly Dictionary<int, CardData> presentationDefinitions = new Dictionary<int, CardData>();
        private bool initialized;
        private int selectedTarget = -1;
        private int targetingCardId = -1, targetingSlot = -1;
        private bool perHit;
        private readonly List<int> assignedTargets = new List<int>();
        private string targetingError = "";

        public CombatHUDSnapshot Snapshot
        {
            get
            {
                EnsureInitialized();
                var state = battle.Session.Snapshot;
                return new CombatHUDSnapshot
                {
                    Phase = state.Phase, IsTerminal = state.IsTerminal,
                    TargetingCardId = targetingCardId, TargetingHitCount = PendingAction()?.HitCount ?? 0,
                    TargetingPerHit = perHit, TargetingTargets = assignedTargets.ToArray(),
                    TargetingPrompt = TargetingPrompt(),
                    SlotTargets = state.Slots.Select(x => x == null ? "" : TargetSummary(x.Targets)).ToArray(),
                    Health = state.Player.Health, MaxHealth = state.Player.MaxHealth,
                    Actions = state.ActionsRemaining, MaxActions = state.Slots.Count, Floor = dungeonFloor,
                    DrawCount = state.DrawCount, DiscardCount = state.DiscardCount,
                    IsPreview = false, IsResolving = state.Phase != BattlePhase.Planning,
                    CanResolve = state.CanCommit, CanEndTurn = false, SelectedTarget = selectedTarget,
                    Status = string.IsNullOrEmpty(battle.LastError)
                        ? "TURN " + state.Turn + "  /  " + PhaseLabel(state.Phase) +
                          "  |  BLOCK " + state.Player.Block + "  DODGE " + state.Player.Dodge
                        : battle.LastError,
                    Hand = state.Hand.Select(Card).ToArray(),
                    Slots = state.Slots.Select(x => x == null ? null : Card(x.Card)).ToArray(),
                    Enemies = state.Enemies.Select((x, i) => new EnemyHUDSnapshot
                    {
                        Id = x.Id, Name = x.Name, Health = x.Health, MaxHealth = x.MaxHealth,
                        Intent = x.Intent, Block = x.Block, Dodge = x.Dodge,
                        IsValidTarget = targetingCardId >= 0 && x.Alive && state.Phase == BattlePhase.Planning,
                        Anchor = i < enemyAnchors.Length ? enemyAnchors[i] : null
                    }).ToArray()
                };
            }
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            if (!battle) throw new InvalidOperationException("LiveCombatHUDSource requires a CombatBattleController.");
            battle.Initialize();
            int enemyCount = battle.Session.Snapshot.Enemies.Count;
            if (enemyAnchors == null || enemyAnchors.Length < enemyCount || enemyAnchors.Take(enemyCount).Any(x => !x))
                throw new InvalidOperationException("Assign one presentation renderer anchor for every encounter enemy.");
            // Copy artwork and strings as well as numbers: Inspector edits apply only to the next battle.
            foreach (var pair in battle.Definitions)
            {
                var action = battle.Session.GetDefinition(pair.Key);
                var copy = Instantiate(pair.Value);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.cardName = action.Name;
                copy.description = Describe(action);
                // V2 validates a one-card-per-slot economy; all tunable effects come from the frozen action.
                copy.actionCost = 1;
                copy.attackPower = action.Damage; copy.defensePower = action.Block; copy.healPower = action.Heal;
                copy.dodgeCount = action.Dodge; copy.hitCount = action.HitCount;
                copy.targetMode = action.Targeting; copy.piercingMode = action.Piercing;
                copy.chargeMultiplier = action.ChargeMultiplier; copy.chargeTurns = action.ChargeTurns;
                presentationDefinitions.Add(pair.Key, copy);
            }
            battle.Changed += OnChanged;
            battle.EventRaised += OnEvent;
            initialized = true;
        }

        private CombatCardItem Card(CardInstance value)
        {
            var action = battle.Session.GetDefinition(value.DefinitionId);
            return new CombatCardItem(value.Id, presentationDefinitions[value.DefinitionId], Icon(action.Icon));
        }
        private static PreviewCardKind Icon(ActionIconKind value)
        {
            switch (value)
            {
                case ActionIconKind.Defence: return PreviewCardKind.Defence;
                case ActionIconKind.Dodge: return PreviewCardKind.Dodge;
                case ActionIconKind.Counterattack: return PreviewCardKind.Counterattack;
                case ActionIconKind.Heal: return PreviewCardKind.Heal;
                case ActionIconKind.Piercing: return PreviewCardKind.Piercing;
                case ActionIconKind.Combo: return PreviewCardKind.Combo;
                case ActionIconKind.Charge: return PreviewCardKind.Charge;
                case ActionIconKind.Miss: return PreviewCardKind.Miss;
                default: return PreviewCardKind.Attack;
            }
        }
        private static string Describe(ActionDefinition value)
        {
            var parts = new List<string>();
            if (value.Damage > 0) parts.Add(value.HitCount > 1 ? value.HitCount + " hits.\n" + value.Damage + " damage each." : "Deal " + value.Damage + " damage.");
            if (value.Block > 0) parts.Add("Block " + value.Block + " damage.");
            if (value.Dodge > 0) parts.Add("Evade " + value.Dodge + " attack(s).");
            if (value.Heal > 0) parts.Add("Restore " + value.Heal + " HP.");
            if (value.Piercing == PiercingMode.IgnoreBlock) parts.Add("Ignore block; can be dodged.");
            if (value.ChargeMultiplier > 1) parts.Add("Next " + value.ChargeTurns + " turn(s): damage x" + value.ChargeMultiplier + ".");
            if (parts.Count == 0) parts.Add("No effect.");
            return string.Join(" ", parts);
        }
        private static string PhaseLabel(BattlePhase phase)
            => phase == BattlePhase.EnemyReveal ? "ENEMY REVEAL" : phase.ToString().ToUpperInvariant();

        private ActionDefinition PendingAction()
        {
            var card = battle.Session.Snapshot.Hand.FirstOrDefault(x => x.Id == targetingCardId);
            return card == null ? null : battle.Session.GetDefinition(card.DefinitionId);
        }
        private string TargetSummary(IReadOnlyList<int> targets)
        {
            var state = battle.Session.Snapshot;
            return string.Join(" / ", targets.Select(id => id == 0 ? "Self" : state.Enemies.FirstOrDefault(x => x.Id == id)?.Name ?? "Unknown"));
        }
        private string TargetingPrompt()
        {
            if (!string.IsNullOrEmpty(targetingError)) return targetingError;
            var action = PendingAction();
            if (action == null) return "";
            if (!perHit) return action.Name + " — choose an enemy" + (action.HitCount > 1 ? " (all " + action.HitCount + " hits)" : "");
            return action.Name + " — choose hit " + (assignedTargets.Count + 1) + " / " + action.HitCount +
                (assignedTargets.Count > 0 ? "\nAssigned: " + TargetSummary(assignedTargets) : "");
        }
        public bool BeginTargeting(int cardId, int slot = -1)
        {
            EnsureInitialized();
            var state = battle.Session.Snapshot;
            if (state.Phase != BattlePhase.Planning) return false;
            var card = state.Hand.FirstOrDefault(x => x.Id == cardId);
            if (card == null) return false;
            if (slot < 0) for (int i = 0; i < state.Slots.Count; i++) if (state.Slots[i] == null) { slot = i; break; }
            if (slot < 0 || slot >= state.Slots.Count || state.Slots[slot] != null)
            { targetingCardId = targetingSlot = -1; assignedTargets.Clear(); perHit = false;
              targetingError = "Return a queued card to free an action slot."; Changed?.Invoke(); return false; }
            var action = battle.Session.GetDefinition(card.DefinitionId);
            if (action.Targeting != TargetMode.SingleOpponent)
                return TryQueueTargets(cardId, slot, action.Targeting == TargetMode.Self || action.Targeting == TargetMode.FriendlyTarget ? new[] { 0 } : Array.Empty<int>());
            if (targetingCardId != cardId) { assignedTargets.Clear(); perHit = false; }
            targetingCardId = cardId; targetingSlot = slot; selectedTarget = -1; targetingError = "";
            Changed?.Invoke(); return true;
        }
        public void CancelTargeting()
        {
            targetingCardId = targetingSlot = selectedTarget = -1; perHit = false; assignedTargets.Clear(); targetingError = "";
            Changed?.Invoke();
        }
        public void SetPerHitTargeting(bool enabled)
        {
            EnsureInitialized();
            var action = PendingAction();
            if (action == null || action.HitCount < 2 || battle.Session.Snapshot.Phase != BattlePhase.Planning) return;
            perHit = enabled; assignedTargets.Clear(); selectedTarget = -1; Changed?.Invoke();
        }
        public bool TryQueueTargets(int cardId, int slot, IReadOnlyList<int> targets)
        {
            EnsureInitialized();
            bool queued = battle.TryQueue(cardId, slot, new TargetSelection(targets?.ToArray() ?? Array.Empty<int>()));
            if (queued) CancelTargeting();
            return queued;
        }
        public bool TryQueue(int cardId, int slot)
        {
            EnsureInitialized();
            var card = battle.Session.Snapshot.Hand.FirstOrDefault(x => x.Id == cardId);
            if (card == null) return false;
            var target = battle.Session.GetDefinition(card.DefinitionId).Targeting;
            if (target == TargetMode.SingleOpponent)
            {
                // An already selected target remains supported for scripted V2 clients.
                if (targetingCardId < 0 && selectedTarget >= 0) return TryQueueTargets(cardId, slot, new[] { selectedTarget });
                BeginTargeting(cardId, slot); return false;
            }
            return TryQueueTargets(cardId, slot, target == TargetMode.Self || target == TargetMode.FriendlyTarget ? new[] { 0 } : Array.Empty<int>());
        }
        public bool TryCancel(int slot) { EnsureInitialized(); CancelTargeting(); return battle.TryUnqueue(slot); }
        public bool TryResolve() { EnsureInitialized(); CancelTargeting(); return battle.TryCommit(); }
        public bool TryEndTurn() => false;
        public bool TrySelectTarget(int enemyId)
        {
            EnsureInitialized();
            var state = battle.Session.Snapshot;
            if (state.Phase != BattlePhase.Planning || !state.Enemies.Any(x => x.Id == enemyId && x.Alive))
            { targetingError = "Invalid target — choose a living enemy."; Changed?.Invoke(); return false; }
            selectedTarget = enemyId; targetingError = "";
            if (targetingCardId < 0) { Changed?.Invoke(); return true; }
            var action = PendingAction();
            if (action == null) return false;
            if (!perHit) return TryQueueTargets(targetingCardId, targetingSlot, new[] { enemyId });
            if (assignedTargets.Count >= action.HitCount) assignedTargets.Clear();
            assignedTargets.Add(enemyId);
            if (assignedTargets.Count >= action.HitCount) return TryQueueTargets(targetingCardId, targetingSlot, assignedTargets);
            Changed?.Invoke(); return true;
        }
        private void OnChanged()
        {
            var state = battle.Session.Snapshot;
            if (state.Phase != BattlePhase.Planning || !state.Hand.Any(x => x.Id == targetingCardId))
            { targetingCardId = targetingSlot = -1; assignedTargets.Clear(); perHit = false; }
            Changed?.Invoke();
        }
        private void OnEvent(BattleEvent value)
        {
            if (value.Kind == BattleEventKind.Committed)
                foreach (var slot in battle.Session.Snapshot.Slots)
                    if (slot != null) CardMoving?.Invoke(slot.Card.Id, CardMotion.Playing);
            if (value.Kind == BattleEventKind.CardDiscarded) CardMoving?.Invoke(value.CardId, CardMotion.Discarding);
        }
        private void OnDestroy()
        {
            if (initialized && battle) { battle.Changed -= OnChanged; battle.EventRaised -= OnEvent; }
            foreach (var card in presentationDefinitions.Values)
                if (Application.isPlaying) Destroy(card); else DestroyImmediate(card);
            presentationDefinitions.Clear();
        }
    }
}
