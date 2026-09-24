using System;
using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    /// <summary>Bounded, event-driven presentation. Never advances combat or reads hidden patterns.</summary>
    public sealed class CombatFeedbackPresenter : MonoBehaviour
    {
        [Serializable] public sealed class CueEvent : UnityEvent<string> { }
        [Tooltip("Silent semantic hooks for future audio, not gameplay callbacks.")]
        public CueEvent onCue = new CueEvent();
        public event Action<string> Cue;
        private CombatHUD hud;
        private CombatBattleController battle;
        private readonly List<TextMeshProUGUI> pool = new List<TextMeshProUGUI>();
        private readonly List<int> chargeActors = new List<int>();
        private readonly Dictionary<int, int> actorOccurrences = new Dictionary<int, int>();
        private RectTransform root;
        private CanvasGroup terminal;
        private TextMeshProUGUI terminalTitle, terminalBody;
        private int next;
        private bool terminalShown;

        public void Bind(CombatHUD owner, CombatBattleController controller)
        {
            Unbind(); hud = owner; battle = controller;
            if (!root)
            {
                root = Rect("CombatFeedback", owner.canvas.transform, Vector2.zero, Vector2.zero);
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
                var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                for (int i = 0; i < Mathf.Clamp(owner.theme.feedbackPoolSize, 8, 48); i++)
                {
                    var text = Label("Feedback" + i, root, Vector2.zero, new Vector2(220, 38), 24, owner.theme);
                    text.fontStyle = FontStyles.Bold; text.gameObject.SetActive(false); pool.Add(text);
                }
                var panel = Rect("Terminal", owner.canvas.transform, new Vector2(0, 30), new Vector2(610, 166));
                terminal = panel.gameObject.AddComponent<CanvasGroup>();
                var image = panel.gameObject.AddComponent<Image>(); image.sprite = owner.theme.panelFrame;
                image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 12;
                image.color = image.sprite ? Color.white : owner.theme.panel; image.raycastTarget = false;
                var backing = Rect("OpaqueBacking", panel, Vector2.zero, new Vector2(560, 130)).gameObject.AddComponent<Image>();
                var backingColor = owner.theme.panel; backingColor.a = 1;
                backing.color = backingColor; backing.raycastTarget = false;
                terminalTitle = Label("Title", panel, new Vector2(0, 29), new Vector2(570, 57), 40, owner.theme);
                terminalTitle.font = owner.theme.displayFont ? owner.theme.displayFont : owner.theme.bodyFont;
                terminalTitle.fontStyle = FontStyles.Bold;
                terminalBody = Label("Body", panel, new Vector2(0, -31), new Vector2(560, 45), 17, owner.theme);
                terminal.alpha = 0; terminal.blocksRaycasts = false; terminal.interactable = false;
                terminal.gameObject.SetActive(false);
            }
            if (battle) { battle.EventRaised += OnEvent; battle.Changed += OnChanged; }
        }
        public void Unbind()
        {
            if (battle) { battle.EventRaised -= OnEvent; battle.Changed -= OnChanged; }
            battle = null;
            terminalShown = false; next = 0; chargeActors.Clear(); actorOccurrences.Clear();
            foreach (var text in pool)
            {
                if (!text) continue;
                text.rectTransform.DOKill(); text.DOKill(); text.alpha = 0;
                text.text = string.Empty; text.gameObject.SetActive(false);
            }
            if (terminal)
            {
                terminal.DOKill(); terminal.alpha = 0; terminal.gameObject.SetActive(false);
                terminalTitle.text = string.Empty; terminalBody.text = string.Empty;
            }
        }
        public void EmitCue(string name) { onCue?.Invoke(name); Cue?.Invoke(name); }
        private void OnEvent(BattleEvent value)
        {
            if (!battle || !hud) return;
            var session = battle.Session;
            ActionDefinition action = value.DefinitionId > 0 ? session.GetDefinition(value.DefinitionId) : null;
            switch (value.Kind)
            {
                case BattleEventKind.EnemyRevealed:
                    foreach (var enemy in hud.enemies) if (enemy.ActorId == value.ActorId) enemy.Observe(action);
                    if (action != null && action.ChargeMultiplier > 1) chargeActors.Add(value.ActorId);
                    EmitCue("enemy-reveal"); break;
                case BattleEventKind.Committed:
                    actorOccurrences.Clear();
                    foreach (var slot in session.Snapshot.Slots)
                    {
                        if (slot == null) continue;
                        var definition = session.GetDefinition(slot.Card.DefinitionId);
                        if (definition.ChargeMultiplier > 1) chargeActors.Add(0);
                    }
                    EmitCue("commit"); break;
                case BattleEventKind.Damage:
                    bool pierced = action != null && action.Piercing == PiercingMode.IgnoreBlock;
                    Float(value.TargetId, (pierced ? "PIERCED " : "-") + value.Amount, hud.theme.damageColor);
                    EmitCue(pierced ? "piercing" : "attack"); break;
                case BattleEventKind.Blocked:
                    Float(value.TargetId, "BLOCK " + value.Amount, hud.theme.blockColor); EmitCue("block"); break;
                case BattleEventKind.Dodged:
                    Float(value.TargetId, "DODGE", hud.theme.text); EmitCue("dodge"); break;
                case BattleEventKind.Healed:
                    Float(value.TargetId, "+" + value.Amount, hud.theme.healColor); EmitCue("heal"); break;
                case BattleEventKind.Defeated:
                    Float(value.ActorId, "DEFEATED", hud.theme.damageColor); EmitCue("actor-defeat"); break;
                case BattleEventKind.CardDrawn: EmitCue("draw"); break;
                case BattleEventKind.CardDiscarded: EmitCue("discard"); break;
            }
        }
        private void OnChanged()
        {
            var state = battle.Session.Snapshot;
            if (state.Phase == BattlePhase.Resolution)
            {
                foreach (int id in chargeActors)
                {
                    bool alive = id == 0 ? state.Player.Alive : false;
                    foreach (var enemy in state.Enemies) if (enemy.Id == id) alive = enemy.Alive;
                    if (alive) { Float(id, "CHARGED", hud.theme.gold); EmitCue("charge"); }
                }
                chargeActors.Clear();
            }
            if (!state.IsTerminal || terminalShown) return;
            terminalShown = true;
            string title = state.Phase == BattlePhase.Victory ? "VICTORY" : state.Phase == BattlePhase.Defeat ? "DEFEAT" : "MUTUAL DEFEAT";
            terminalTitle.text = title; terminalTitle.color = state.Phase == BattlePhase.Victory ? hud.theme.gold : hud.theme.damageColor;
            terminalBody.text = "Combat complete  /  " + state.Turn + " turn" + (state.Turn == 1 ? "" : "s") +
                "\nLab encounter - progression is not connected.";
            terminal.gameObject.SetActive(true); terminal.transform.SetAsLastSibling();
            terminal.DOKill(); terminal.alpha = hud.instantAnimations ? 1 : 0;
            if (!hud.instantAnimations) terminal.DOFade(1, hud.theme.terminalFadeDuration).SetUpdate(true);
            EmitCue(state.Phase == BattlePhase.Victory ? "victory" : "defeat");
        }
        private void Float(int actor, string message, Color color)
        {
            if (pool.Count == 0) return;
            var text = pool[next++ % pool.Count]; var rect = text.rectTransform;
            rect.DOKill(); text.DOKill(); text.text = message; text.color = color; text.alpha = 0;
            actorOccurrences.TryGetValue(actor, out int occurrence);
            actorOccurrences[actor] = occurrence + 1;
            const int laneCount = 4;
            int lane = occurrence % laneCount;
            float direction = actor == 0 ? -1 : 1;
            Vector2 position;
            if (actor == 0)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    RectTransformUtility.WorldToScreenPoint(null, hud.player.healthText.transform.position), null, out position);
                position += new Vector2(0, -65);
            }
            else
            {
                position = new Vector2(0, 70);
                foreach (var enemy in hud.enemies) if (enemy.ActorId == actor)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                        RectTransformUtility.WorldToScreenPoint(null, enemy.transform.position), null, out position);
                    // Clear the entire revealed intent block, including four-action enemies.
                    position += new Vector2(0, 45 + enemy.intentText.rectTransform.sizeDelta.y); break;
                }
            }
            position.y += direction * lane * 44;
            rect.anchoredPosition = position; text.gameObject.SetActive(true);
            // Reuse four readable lanes only after the previous batch has faded.
            float delay = occurrence / laneCount * (hud.theme.floatingTextDuration + .15f +
                (laneCount - 1) * hud.theme.combatEventSpacing) + lane * hud.theme.combatEventSpacing;
            DOTween.Sequence().SetTarget(rect).SetUpdate(true).AppendInterval(delay)
                .AppendCallback(() => text.alpha = 1)
                .Append(rect.DOAnchorPosY(position.y + direction * 8, hud.theme.floatingTextDuration).SetEase(Ease.OutCubic))
                .Join(text.DOFade(0, hud.theme.floatingTextDuration).SetEase(Ease.InQuad))
                .OnComplete(() => text.gameObject.SetActive(false));
        }
        public static PreviewCardKind Icon(ActionIconKind kind)
        {
            switch (kind)
            {
                case ActionIconKind.Defence: return PreviewCardKind.Defence;
                case ActionIconKind.Dodge: return PreviewCardKind.Dodge;
                case ActionIconKind.Heal: return PreviewCardKind.Heal;
                case ActionIconKind.Piercing: return PreviewCardKind.Piercing;
                case ActionIconKind.Combo: return PreviewCardKind.Combo;
                case ActionIconKind.Counterattack: return PreviewCardKind.Counterattack;
                case ActionIconKind.Charge: return PreviewCardKind.Charge;
                case ActionIconKind.Miss: return PreviewCardKind.Miss;
                default: return PreviewCardKind.Attack;
            }
        }
        public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        public static TextMeshProUGUI Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, CombatHUDTheme theme)
        {
            var rect = Rect(name, parent, position, size); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.bodyFont; text.fontSize = fontSize; text.color = theme.text;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        private void OnDisable() => Unbind();
        private void OnDestroy()
        {
            Unbind(); foreach (var text in pool) if (text) { text.DOKill(); text.rectTransform.DOKill(); }
            if (terminal) terminal.DOKill();
        }
    }
}
