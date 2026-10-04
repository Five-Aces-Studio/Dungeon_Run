using System;
using DG.Tweening;
using DungeonRun.Combat;
using DungeonRun.UI.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonRun.UI
{
    public sealed class CombatHUD : MonoBehaviour
    {
        public enum SourceMode { LabPreview, Live }
        [Header("Source (selected at startup; no runtime switching)")]
        public SourceMode sourceMode;
        public LabCombatHUDSource labSource;
        public LiveCombatHUDSource liveSource;
        [Tooltip("Legacy V1 source reference. Used as the Lab fallback only.")]
        public MonoBehaviour sourceComponent;
        public CombatHUDTheme theme;
        public Camera worldCamera;
        public Canvas canvas;
        public PlayerCombatHUD player;
        public CardHandController hand;
        public EnemyCombatHUD[] enemies;
        public Button resolveButton, endTurnButton;
        public TextMeshProUGUI statusText, previewLabel;
        public bool instantAnimations;
        [HideInInspector] public bool deferInitialization;
        private ICombatHUDSource source;
        private bool bound;
        private MonoBehaviour startupSource;
        public CombatFeedbackPresenter feedback;
        private bool previousReady;
        [Tooltip("V4 Commit plate presenter (unused by V1/V3 themes).")] public CommitButtonPresenter commitPresenter;
        [Tooltip("V4 enemy defeat staging; added at runtime in Live Play Mode when missing.")] public EnemyDefeatPresenter defeatPresenter;

        private void OnEnable()
        {
            bool hasSource = sourceMode == SourceMode.Live ? liveSource != null : labSource != null || sourceComponent != null;
            if (!deferInitialization && hasSource && player && hand) Initialize();
        }
        public void Initialize()
        {
            Unbind();
            var selectedSource = sourceMode == SourceMode.Live ? (MonoBehaviour)liveSource : labSource ? labSource : sourceComponent;
            if (Application.isPlaying)
            {
                if (!startupSource) startupSource = selectedSource;
                selectedSource = startupSource;
            }
            source = selectedSource as ICombatHUDSource;
            if (source == null) throw new InvalidOperationException("CombatHUD requires ICombatHUDSource.");
            hand.ConfigureSlots(source.Snapshot.Slots.Count);
            EnsureEnemyViews(source.Snapshot.Enemies.Count);
            var resolveRect = (RectTransform)resolveButton.transform;
            var lastSlot = hand.actionSlots[source.Snapshot.Slots.Count - 1].Rect;
            if (resolveRect.anchorMax.x < .75f) resolveRect.anchoredPosition = new Vector2(Mathf.Max(235,
                lastSlot.anchoredPosition.x + lastSlot.sizeDelta.x * .5f + resolveRect.sizeDelta.x * .5f + 20), resolveRect.anchoredPosition.y);
            hand.Bind(source);
            hand.SetInstantAnimations(instantAnimations);
            foreach (var enemy in enemies) enemy.Bind(source, worldCamera, (RectTransform)canvas.transform);
            source.Changed += Refresh;
            resolveButton.onClick.AddListener(Resolve);
            endTurnButton.onClick.AddListener(EndTurn);
            bound = true; Refresh();
            if (Application.isPlaying && selectedSource is LiveCombatHUDSource live)
            {
                if (!feedback && !TryGetComponent(out feedback)) feedback = gameObject.AddComponent<CombatFeedbackPresenter>();
                feedback.Bind(this, live.battle);
            }
            if (theme && theme.v4Style && Application.isPlaying && selectedSource is LiveCombatHUDSource liveV4)
            {
                if (!defeatPresenter && !TryGetComponent(out defeatPresenter)) defeatPresenter = gameObject.AddComponent<EnemyDefeatPresenter>();
                defeatPresenter.Bind(this, liveV4.battle);
            }
        }
        public void Refresh()
        {
            if (source == null) return;
            var state = source.Snapshot;
            player.Render(state, theme, instantAnimations); hand.Render(state);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].gameObject.SetActive(i < state.Enemies.Count);
                if (i < state.Enemies.Count)
                {
                    enemies[i].SetPresentation(!state.IsResolving && !state.IsTerminal, instantAnimations);
                    enemies[i].Render(state.Enemies[i], state.Enemies[i].Id == state.SelectedTarget);
                }
            }
            resolveButton.interactable = state.CanResolve;
            endTurnButton.interactable = state.CanEndTurn;
            endTurnButton.gameObject.SetActive(state.IsPreview);
            var style = theme ? theme.v4Style : null;
            var resolveLabel = resolveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (style && commitPresenter) commitPresenter.Apply(state, style);
            else if (resolveLabel)
            {
                resolveLabel.text = state.IsPreview ? "RESOLVE" : state.IsTerminal ? "COMPLETE" : state.IsResolving ? "LOCKED" : "COMMIT";
                resolveLabel.color = state.CanResolve ? theme.gold : new Color(.65f, .66f, .61f);
            }
            if (state.CanResolve && !previousReady) UIAnimationHelpers.Pulse((RectTransform)resolveButton.transform, instantAnimations);
            previousReady = state.CanResolve;
            if (style)
            {
                statusText.text = !string.IsNullOrEmpty(state.Error) ? state.Error :
                    state.Turn > 0 ? HudStatusFormat.Status(state.Turn, (HudPhase)(int)state.Phase) : state.Status;
                previewLabel.text = state.IsPreview ? "LAB PREVIEW  \u00B7  PRESENTATION ONLY" : string.Empty;
            }
            else
            {
                statusText.text = state.Status;
                previewLabel.text = state.IsPreview ? "LAB PREVIEW  /  PRESENTATION ONLY" : "LIVE COMBAT";
            }
        }
        private void EnsureEnemyViews(int count)
        {
            if (enemies == null || enemies.Length == 0 || !enemies[0])
                throw new InvalidOperationException("Assign an existing enemy HUD template.");
            int previous = enemies.Length;
            if (count <= previous) return;
            Array.Resize(ref enemies, count);
            for (int i = previous; i < count; i++)
            {
                enemies[i] = Instantiate(enemies[0], enemies[0].transform.parent);
                enemies[i].name = "EnemyHUD" + i;
            }
        }
        private void Resolve()
        {
            UIAnimationHelpers.Pulse((RectTransform)resolveButton.transform, instantAnimations);
            source.TryResolve();
        }
        private void EndTurn() => source.TryEndTurn();
        public void SetInstantAnimations(bool value)
        { instantAnimations = value; hand.SetInstantAnimations(value); Refresh(); }
        private void Update()
        {
            if (bound && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) hand.CancelSelection();
        }
        private void Unbind()
        {
            if (!bound) return;
            source.Changed -= Refresh;
            if (feedback) feedback.Unbind();
            if (defeatPresenter) defeatPresenter.Unbind();
            if (resolveButton) resolveButton.transform.DOKill();
            // V4: never leave the Commit plate mid-pulse or hover-scaled across a rebind.
            if (resolveButton && theme && theme.v4Style) { resolveButton.transform.localScale = Vector3.one; if (commitPresenter) commitPresenter.ResetPointer(); }
            resolveButton.onClick.RemoveListener(Resolve); endTurnButton.onClick.RemoveListener(EndTurn);
            hand.Cleanup(); source = null; bound = false;
        }
        private void OnDisable() => Unbind();
    }
}
