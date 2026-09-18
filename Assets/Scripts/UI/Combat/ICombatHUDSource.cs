using System;
using System.Collections.Generic;
using UnityEngine;

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
    }

    public sealed class CombatHUDSnapshot
    {
        public int Health, MaxHealth, Actions, MaxActions, Floor, DrawCount, DiscardCount, SelectedTarget;
        public bool IsPreview, IsResolving, CanResolve, CanEndTurn;
        public string Status;
        public IReadOnlyList<CombatCardItem> Hand;
        public IReadOnlyList<CombatCardItem> Slots;
        public IReadOnlyList<EnemyHUDSnapshot> Enemies;
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
