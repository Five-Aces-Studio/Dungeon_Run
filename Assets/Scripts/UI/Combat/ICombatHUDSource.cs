using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonRun.Combat;

namespace DungeonRun.UI
{
    public enum CardInteractionState { Idle, Hovered, Selected, Dragging, Queued, Playing, Discarding, Disabled }
    public enum PreviewCardKind { Attack, Defence, Dodge, Heal, Piercing, Combo, Counterattack, Charge, Miss }
    public enum CardMotion { Playing, Discarding }

    public sealed class CombatCardItem
    {
        public int Id { get; }
        public CardData Definition { get; }
        public PreviewCardKind Kind { get; }
        public CombatCardItem(int id, CardData definition, PreviewCardKind kind)
        { Id = id; Definition = definition; Kind = kind; }
    }

    public sealed class EnemyHUDSnapshot
    {
        public int Id;
        public string Name, Intent;
        public int Health, MaxHealth, Block, Dodge;
        public Renderer Anchor;
        public bool IsValidTarget;
    }

    public sealed class CombatHUDSnapshot
    {
        public int Health, MaxHealth, Actions, MaxActions, Floor, DrawCount, DiscardCount, SelectedTarget;
        public bool IsPreview, IsResolving, CanResolve, CanEndTurn;
        public string Status;
        public BattlePhase Phase;
        public bool IsTerminal;
        public int TargetingCardId = -1, TargetingHitCount;
        public bool TargetingPerHit;
        public string TargetingPrompt;
        public IReadOnlyList<int> TargetingTargets = Array.Empty<int>();
        public IReadOnlyList<string> SlotTargets = Array.Empty<string>();
        public IReadOnlyList<CombatCardItem> Hand;
        public IReadOnlyList<CombatCardItem> Slots;
        public IReadOnlyList<EnemyHUDSnapshot> Enemies;
    }

    /// <summary>Optional semantic targeting extension; preview sources need not implement it.</summary>
    public interface ICombatTargetingSource
    {
        bool BeginTargeting(int cardId, int slot = -1);
        void CancelTargeting();
        void SetPerHitTargeting(bool enabled);
        bool TryQueueTargets(int cardId, int slot, IReadOnlyList<int> targets);
    }

    /// <summary>Presentation boundary. A future live adapter owns validation, not CardView or static legacy events.</summary>
    public interface ICombatHUDSource
    {
        event Action Changed;
        event Action<int, CardMotion> CardMoving;
        CombatHUDSnapshot Snapshot { get; }
        bool TryQueue(int cardId, int slot);
        bool TryCancel(int slot);
        bool TryResolve();
        bool TryEndTurn();
        bool TrySelectTarget(int enemyId);
    }
}
