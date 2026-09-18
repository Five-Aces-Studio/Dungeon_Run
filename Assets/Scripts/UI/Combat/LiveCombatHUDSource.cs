using System;
using System.Collections.Generic;
using System.Linq;
using DungeonRun.Combat;
using UnityEngine;

namespace DungeonRun.UI
{
    /// <summary>Read-only presentation projection and command translation. No combat arithmetic lives here.</summary>
    public sealed class LiveCombatHUDSource : MonoBehaviour, ICombatHUDSource
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

        public CombatHUDSnapshot Snapshot
        {
            get
            {
                EnsureInitialized();
                var state = battle.Session.Snapshot;
                return new CombatHUDSnapshot
                {
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

        public bool TryQueue(int cardId, int slot)
        {
            EnsureInitialized();
            var card = battle.Session.Snapshot.Hand.FirstOrDefault(x => x.Id == cardId);
            if (card == null) return battle.TryQueue(cardId, slot, new TargetSelection());
            var target = battle.Session.GetDefinition(card.DefinitionId).Targeting;
            var selection = target == TargetMode.Self || target == TargetMode.FriendlyTarget
                ? new TargetSelection(0) : target == TargetMode.SingleOpponent
                    ? new TargetSelection(selectedTarget) : new TargetSelection();
            return battle.TryQueue(cardId, slot, selection);
        }
        public bool TryCancel(int slot) { EnsureInitialized(); return battle.TryUnqueue(slot); }
        public bool TryResolve() { EnsureInitialized(); return battle.TryCommit(); }
        public bool TryEndTurn() => false;
        public bool TrySelectTarget(int enemyId)
        {
            EnsureInitialized();
            var state = battle.Session.Snapshot;
            if (state.Phase != BattlePhase.Planning || !state.Enemies.Any(x => x.Id == enemyId && x.Alive)) return false;
            selectedTarget = enemyId; Changed?.Invoke(); return true;
        }
        private void OnChanged() => Changed?.Invoke();
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
