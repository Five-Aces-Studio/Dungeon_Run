using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonRun.UI
{
    /// <summary>Explicit, scene-scoped presentation simulator. Not a combat resolver and never emits legacy gameplay events.</summary>
    public sealed class LabCombatHUDSource : MonoBehaviour, ICombatHUDSource
    {
        [Header("Existing legacy definitions (read only; Guide values on transient lab clones)")]
        public CardData attack, defence, heal;
        [Header("Editable preview state; not saved gameplay")]
        [Min(1)] public int maximumHealth = 10;
        [Min(0)] public int initialHealth = 8;
        [Min(1)] public int dungeonFloor = 1;
        [Range(1, 10)] public int initialHandSize = 5;
        public Renderer[] enemyAnchors = Array.Empty<Renderer>();
        public int[] enemyMaximumHealth = { 48, 32, 36 };
        public string[] enemyNames = { "Abyss Warden", "Ash Hound", "Trigon Sentinel" };
        public string[] enemyIntents = { "Attack 6", "Attack 4", "Unknown" };
        [Min(0)] public float previewStepDuration = .28f;
        public event Action Changed;
        public event Action<int, CardMotion> CardMoving;

        private readonly List<CombatCardItem> hand = new List<CombatCardItem>();
        private readonly List<CombatCardItem> draw = new List<CombatCardItem>();
        private readonly List<CombatCardItem> discard = new List<CombatCardItem>();
        private readonly CombatCardItem[] slots = new CombatCardItem[2];
        private readonly List<int> queueOriginOrder = new List<int>();
        private readonly List<CardData> transientDefinitions = new List<CardData>();
        private readonly List<EnemyHUDSnapshot> enemies = new List<EnemyHUDSnapshot>();
        private int health, actions = 2, target, nextId;
        private bool initialized, resolving, resolvedTurn;
        private string status = "Choose a card, then an action slot.";

        public CombatHUDSnapshot Snapshot
        {
            get
            {
                EnsureInitialized();
                return new CombatHUDSnapshot
                {
                    Health = health, MaxHealth = maximumHealth, Actions = actions, MaxActions = 2,
                    Floor = dungeonFloor, DrawCount = draw.Count, DiscardCount = discard.Count,
                    IsPreview = true, IsResolving = resolving, SelectedTarget = target, Status = status,
                    CanResolve = !resolving && !resolvedTurn && slots.All(x => x != null),
                    CanEndTurn = !resolving && resolvedTurn,
                    Hand = hand.ToArray(), Slots = (CombatCardItem[])slots.Clone(),
                    Enemies = enemies.Select(x => new EnemyHUDSnapshot { Id = x.Id, Name = x.Name, Intent = x.Intent,
                        Health = x.Health, MaxHealth = x.MaxHealth, Anchor = x.Anchor }).ToArray()
                };
            }
        }

        private void Awake() => EnsureInitialized();
        private void EnsureInitialized()
        {
            if (initialized) return;
            if (gameObject.scene.name != "SceneVictorLab")
                throw new InvalidOperationException("LabCombatHUDSource is restricted to SceneVictorLab.");
            if (!attack || !defence || !heal) throw new InvalidOperationException("Assign existing Attack, Armor and Basic Healing CardData assets.");
            initialized = true;
            maximumHealth = Mathf.Max(1, maximumHealth);
            health = Mathf.Clamp(initialHealth, 0, maximumHealth);
            // Guide pp.10-11: base attacks/defence/healing are one, unlike the old prototype assets.
            // Keep the original references/assets intact; this lab-only presentation overlay is never persisted.
            var attackPreview = GuideClone(attack, "Attack", "Deal 1 damage to one target.");
            attackPreview.attackPower = 1;
            var defencePreview = GuideClone(defence, "Defence", "Block 1 damage.");
            defencePreview.defensePower = 1;
            var healPreview = GuideClone(heal, "Heal Self", "Restore 1 HP to yourself.");
            healPreview.healPower = 1;
            var dodge = Temporary("Dodge", "Evade one attack completely.");
            var piercing = Temporary("Piercing Attack", "Deal 1 damage. Ignore defence. Can be dodged.");
            piercing.attackPower = 1;
            var definitions = new[] { attackPreview, dodge, defencePreview, healPreview, piercing };
            var kinds = new[] { PreviewCardKind.Attack, PreviewCardKind.Dodge, PreviewCardKind.Defence, PreviewCardKind.Heal, PreviewCardKind.Piercing };
            for (int i = 0; i < 21; i++) draw.Add(new CombatCardItem(++nextId, definitions[i % 5], kinds[i % 5]));
            for (int i = 0; i < Mathf.Clamp(initialHandSize, 1, 10); i++) DrawOne();
            for (int i = 0; i < enemyAnchors.Length; i++)
            {
                int hp = i < enemyMaximumHealth.Length ? Mathf.Max(1, enemyMaximumHealth[i]) : 30;
                enemies.Add(new EnemyHUDSnapshot { Id = i, Health = hp, MaxHealth = hp, Anchor = enemyAnchors[i],
                    Name = i < enemyNames.Length ? enemyNames[i] : "Enemy",
                    Intent = i < enemyIntents.Length ? enemyIntents[i] : "Unknown" });
            }
        }

        private CardData Temporary(string title, string description)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.hideFlags = HideFlags.HideAndDontSave;
            card.cardName = title; card.description = description; card.actionCost = 1;
            transientDefinitions.Add(card);
            return card;
        }

        private CardData GuideClone(CardData original, string title, string description)
        {
            var card = Instantiate(original);
            card.hideFlags = HideFlags.HideAndDontSave;
            card.cardName = title; card.description = description; card.actionCost = 1;
            card.attackPower = card.healPower = card.defensePower = card.poisonPower = card.poisonTurns = card.remainingTurns = 0;
            card.multipleTurns = false;
            transientDefinitions.Add(card);
            return card;
        }

        private void DrawOne()
        {
            if (draw.Count == 0 && discard.Count > 0) { draw.AddRange(discard); discard.Clear(); }
            if (draw.Count == 0) return;
            hand.Add(draw[0]); draw.RemoveAt(0);
        }

        public bool TryQueue(int cardId, int slot)
        {
            EnsureInitialized();
            if (resolving || resolvedTurn || slot < 0 || slot > 1 || slots[slot] != null) return false;
            int index = hand.FindIndex(x => x.Id == cardId);
            if (index < 0 || hand[index].Definition.actionCost > actions) return false;
            if (slots.All(x => x == null))
            {
                queueOriginOrder.Clear();
                queueOriginOrder.AddRange(hand.Select(x => x.Id));
            }
            slots[slot] = hand[index];
            actions -= slots[slot].Definition.actionCost;
            hand.RemoveAt(index);
            status = slots.All(x => x != null) ? "Two actions ready. Resolve when ready." : "Choose your second action.";
            Changed?.Invoke(); return true;
        }

        public bool TryCancel(int slot)
        {
            if (resolving || resolvedTurn || slot < 0 || slot > 1 || slots[slot] == null) return false;
            // Stable rank, not a shifted removal index: queue A then B; cancel A/B or B/A restores A,B.
            int rank = queueOriginOrder.IndexOf(slots[slot].Id);
            int insert = hand.FindIndex(x => queueOriginOrder.IndexOf(x.Id) > rank);
            hand.Insert(insert < 0 ? hand.Count : insert, slots[slot]);
            actions = Mathf.Min(2, actions + slots[slot].Definition.actionCost);
            slots[slot] = null; status = "Action returned to hand.";
            Changed?.Invoke(); return true;
        }

        public bool TryResolve()
        {
            if (!Snapshot.CanResolve) return false;
            resolving = true; status = "Resolving preview actions..."; Changed?.Invoke();
            if (Application.isPlaying) StartCoroutine(ResolvePreview());
            else { var preview = ResolvePreview(); while (preview.MoveNext()) { } }
            return true;
        }

        private IEnumerator ResolvePreview()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var item = slots[slot];
                CardMoving?.Invoke(item.Id, CardMotion.Playing);
                if (previewStepDuration > 0) yield return new WaitForSecondsRealtime(previewStepDuration);
                // Deliberately elementary visual feedback, not poison/armour/dodge or production combat resolution.
                if (item.Definition.healPower > 0) health = Mathf.Min(maximumHealth, health + item.Definition.healPower);
                var enemy = enemies.FirstOrDefault(x => x.Id == target);
                if (enemy != null && item.Definition.attackPower > 0)
                    enemy.Health = Mathf.Max(0, enemy.Health - item.Definition.attackPower);
                Changed?.Invoke();
                CardMoving?.Invoke(item.Id, CardMotion.Discarding);
                if (previewStepDuration > 0) yield return new WaitForSecondsRealtime(previewStepDuration);
                discard.Add(item); slots[slot] = null; Changed?.Invoke();
            }
            resolving = false; resolvedTurn = true;
            status = "Preview resolved. End Turn to draw back to five.";
            Changed?.Invoke();
        }

        public bool TryEndTurn()
        {
            if (!Snapshot.CanEndTurn) return false;
            while (hand.Count < 5 && draw.Count + discard.Count > 0) DrawOne();
            actions = 2; resolvedTurn = false; status = "Your turn. Choose two actions.";
            Changed?.Invoke(); return true;
        }

        public bool TrySelectTarget(int enemyId)
        {
            EnsureInitialized();
            if (resolving || !enemies.Any(x => x.Id == enemyId && x.Health > 0)) return false;
            target = enemyId; Changed?.Invoke(); return true;
        }

        public void DebugSetHandSize(int count)
        {
            EnsureInitialized();
            if (resolving) return;
            for (int i = 1; i >= 0; i--) TryCancel(i);
            count = Mathf.Clamp(count, 1, 10);
            while (hand.Count > count) { draw.Insert(0, hand[hand.Count - 1]); hand.RemoveAt(hand.Count - 1); }
            while (hand.Count < count && draw.Count + discard.Count > 0) DrawOne();
            resolvedTurn = false; actions = 2; Changed?.Invoke();
        }

        public void DebugSetHealth(int current, int maximum)
        { EnsureInitialized(); maximumHealth = Mathf.Max(1, maximum); health = Mathf.Clamp(current, 0, maximumHealth); Changed?.Invoke(); }

        private void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var definition in transientDefinitions)
                if (definition) { if (Application.isPlaying) Destroy(definition); else DestroyImmediate(definition); }
        }
    }
}
