using System;
using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.Combat;
using DungeonRun.UI.Presentation;
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
        private readonly List<Image> poolIcons = new List<Image>();
        private CombatHUDStyleV4 v4;
        private RectTransform root;
        private CanvasGroup terminal;
        private TextMeshProUGUI terminalTitle, terminalBody;
        private int next;
        private bool terminalShown;

        public void Bind(CombatHUD owner, CombatBattleController controller)
        {
            Unbind(); hud = owner; battle = controller;
            v4 = owner.theme ? owner.theme.v4Style : null;
            if (!root && v4) BuildV4(owner, v4);
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
            foreach (var icon in poolIcons) if (icon) icon.DOKill();
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
                    Color damageColor = v4 ? (value.TargetId == 0 ? v4.floaterPlayerDamage : v4.floaterEnemyDamage) : hud.theme.damageColor;
                    Float(value.TargetId, (pierced ? "PIERCED " : "-") + value.Amount, damageColor);
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
            if (v4) terminalTitle.color = state.Phase == BattlePhase.Victory ? v4.victoryColor : v4.defeatColor;
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
            if (v4 && poolIcons.Count == pool.Count) { FloatV4(actor, message, color); return; }
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
        private void BuildV4(CombatHUD owner, CombatHUDStyleV4 style)
        {
            root = Rect("CombatFeedback", owner.canvas.transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            for (int i = 0; i < Mathf.Clamp(owner.theme.feedbackPoolSize, 8, 48); i++)
            {
                var text = Label("Feedback" + i, root, Vector2.zero, new Vector2(220, 38), 30, owner.theme);
                CombatHUDStyleV4.SetFont(text, style.displayHeavyFont, style.numberOutline);
                text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
                var icon = Rect("Icon", text.transform, Vector2.zero, new Vector2(26, 26)).gameObject.AddComponent<Image>();
                icon.raycastTarget = false; icon.preserveAspect = true;
                text.gameObject.SetActive(false); pool.Add(text); poolIcons.Add(icon);
            }
            // Full-screen terminal layer: the vignette fades with the painted plate through one CanvasGroup.
            var panel = Rect("Terminal", owner.canvas.transform, Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            terminal = panel.gameObject.AddComponent<CanvasGroup>();
            if (style.vignette)
            {
                var shade = Rect("Vignette", panel, Vector2.zero, Vector2.zero);
                shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
                var image = shade.gameObject.AddComponent<Image>(); image.sprite = style.vignette; image.raycastTarget = false;
                image.color = new Color(1, 1, 1, style.vignetteAlpha);
            }
            var banner = Rect("Banner", panel, Vector2.zero, Vector2.zero); style.terminalPanel.Apply(banner);
            var plate = banner.gameObject.AddComponent<Image>(); plate.sprite = style.terminalBanner; plate.raycastTarget = false;
            plate.color = plate.sprite ? Color.white : owner.theme.panel;
            // Text-safe zones of the painted 640x190 plate: title y 40..110, body y 118..170.
            float kx = banner.sizeDelta.x / 640f, ky = banner.sizeDelta.y / 190f;
            terminalTitle = Label("Title", banner, new Vector2(0, 20 * ky), new Vector2(520 * kx, 70 * ky), style.terminalTitleSize, owner.theme);
            CombatHUDStyleV4.SetFont(terminalTitle, style.displayHeavyFont, style.displayOutline);
            // The display face's line height exceeds the 70 px zone at 54 pt; Ellipsis would drop the single line entirely.
            terminalTitle.textWrappingMode = TextWrappingModes.NoWrap; terminalTitle.overflowMode = TextOverflowModes.Overflow;
            terminalBody = Label("Body", banner, new Vector2(0, -49 * ky), new Vector2(480 * kx, 52 * ky), style.terminalBodySize, owner.theme);
            CombatHUDStyleV4.SetFont(terminalBody, style.bodyFont); terminalBody.color = style.parchmentText;
            terminalBody.enableAutoSizing = true; terminalBody.fontSizeMin = 13; terminalBody.fontSizeMax = style.terminalBodySize;
            terminal.alpha = 0; terminal.blocksRaycasts = false; terminal.interactable = false;
            terminal.gameObject.SetActive(false);
        }
        private void FloatV4(int actor, string message, Color color)
        {
            int index = next++ % pool.Count;
            var text = pool[index]; var icon = poolIcons[index]; var rect = text.rectTransform;
            rect.DOKill(); text.DOKill(); icon.DOKill();
            text.text = message; text.color = color; text.alpha = 0;
            text.fontSize = HudFeedbackFormat.IsNumber(message) ? 34 : 22;
            var glyph = HudFeedbackFormat.Glyph(message);
            // Plain damage numbers carry no icon; pierced, block, dodge, heal and charge keep their painted glyph.
            var sprite = glyph == FeedbackGlyph.None || glyph == FeedbackGlyph.Damage ? null : v4.FeedbackIcon(glyph);
            icon.sprite = sprite; icon.color = new Color(1, 1, 1, 0); icon.gameObject.SetActive(sprite);
            if (sprite) icon.rectTransform.anchoredPosition = new Vector2(-text.GetPreferredValues(message).x * .5f - 17, 0);
            actorOccurrences.TryGetValue(actor, out int occurrence);
            actorOccurrences[actor] = occurrence + 1;
            const int laneCount = 4;
            int lane = occurrence % laneCount;
            float direction = actor == 0 ? -1 : 1;
            Vector2 position;
            if (actor == 0)
            {
                // Right end of the player HP track, vertically centred on it; pops stack downward.
                var trackRect = hud.player.healthTrack ? hud.player.healthTrack : hud.player.healthFill.rectTransform;
                var corners = new Vector3[4]; trackRect.GetWorldCorners(corners);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    RectTransformUtility.WorldToScreenPoint(null, (corners[2] + corners[3]) * .5f), null, out position);
                position += new Vector2(20, 0);
            }
            else
            {
                position = new Vector2(0, 70);
                foreach (var enemy in hud.enemies) if (enemy.ActorId == actor)
                {
                    // 40px above the enemy HP track (falls back to the enemy transform if the track rect is unset).
                    var trackRect = enemy.healthTrack ? enemy.healthTrack : enemy.healthFill.rectTransform;
                    var corners = new Vector3[4]; trackRect.GetWorldCorners(corners);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                        RectTransformUtility.WorldToScreenPoint(null, (corners[1] + corners[2]) * .5f), null, out position);
                    position += new Vector2(0, 40); break;
                }
            }
            // Staggered lanes: +18px in x and 34px in y per lane, instead of a pure vertical stack.
            position += new Vector2(lane * 18, direction * lane * 34);
            rect.anchoredPosition = position; text.gameObject.SetActive(true);
            float duration = hud.theme.floatingTextDuration;
            float delay = occurrence / laneCount * (duration + .15f + (laneCount - 1) * hud.theme.combatEventSpacing) + lane * hud.theme.combatEventSpacing;
            DOTween.Sequence().SetTarget(rect).SetUpdate(true).AppendInterval(delay)
                .AppendCallback(() => { text.alpha = 1; icon.color = Color.white; })
                .Append(rect.DOAnchorPosY(position.y + direction * 8, duration).SetEase(Ease.OutCubic))
                .Join(text.DOFade(0, duration).SetEase(Ease.InQuad))
                .Join(icon.DOFade(0, duration).SetEase(Ease.InQuad))
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
