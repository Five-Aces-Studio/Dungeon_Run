using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonRun.Combat
{
    /// <summary>Unity lifecycle and pacing only. BattleSession validates every gameplay transition.</summary>
    public sealed class CombatBattleController : MonoBehaviour
    {
        [Header("Startup configuration (copied once)")]
        public CombatRulesetDefinition ruleset;
        public CombatEncounterDefinition encounter;
        public int seed = 173;
        [Header("Presentation pacing (not combat rules)")]
        [Range(0, 3)] public float revealSeconds = .85f;
        [Range(0, 2)] public float phaseSeconds = .4f;
        [Header("Lab debug — never included in the production HUD")]
        public bool enableLabPatternDebug;
        [SerializeField, TextArea] private string configurationValidation = "Assign ruleset and encounter.";

        public BattleSession Session { get; private set; }
        public IReadOnlyDictionary<int, CardData> Definitions => definitions;
        public string LastError { get; private set; } = "";
        public event Action Changed;
        public event Action<BattleEvent> EventRaised;
        private Dictionary<int, CardData> definitions;
        private Coroutine progression;

        public void Initialize()
        {
            if (Session != null) return;
            if (!ruleset || !encounter) throw new InvalidOperationException("Assign Combat Ruleset and Encounter before battle initialization.");
            if (ruleset.enemyActionRevealMode == EnemyRevealMode.AlwaysVisibleForDebug && gameObject.scene.name != "SceneVictorLab")
                throw new InvalidOperationException("Debug intent visibility is restricted to SceneVictorLab.");
            var configuration = BattleConfigurationFactory.Create(ruleset, encounter, out definitions);
            Session = new BattleSession(configuration, seed);
            Session.Changed += OnChanged;
            Session.EventRaised += OnEvent;
        }

        public bool TryQueue(int cardId, int slot, TargetSelection targets)
        { Initialize(); return Result(Session.TryQueue(cardId, slot, targets, out var error), error); }
        public bool TryUnqueue(int slot)
        { Initialize(); return Result(Session.TryUnqueue(slot, out var error), error); }
        public bool TryCommit()
        {
            Initialize();
            if (progression != null) return Result(false, "Resolution is already in progress.");
            if (!Result(Session.TryCommit(out var error), error)) return false;
            if (Application.isPlaying) progression = StartCoroutine(Progress());
            return true;
        }

        /// <summary>Explicit single-step API for deterministic integration tests and edit-mode inspection.</summary>
        public bool Advance()
        { Initialize(); return Result(Session.Advance(out var error), error); }

        private IEnumerator Progress()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0, revealSeconds));
            // The domain has a fixed finite phase graph; never spin indefinitely on malformed progression.
            for (int step = 0; step < 8 && !Session.Snapshot.IsTerminal && Session.Snapshot.Phase != BattlePhase.Planning; step++)
            {
                if (!Advance()) break;
                yield return new WaitForSecondsRealtime(Mathf.Max(0, phaseSeconds));
            }
            progression = null;
        }

        private bool Result(bool success, string error)
        {
            LastError = success ? "" : error;
            Changed?.Invoke();
            return success;
        }
        private void OnChanged() { LastError = ""; Changed?.Invoke(); }
        private void OnEvent(BattleEvent value) => EventRaised?.Invoke(value);

        [ContextMenu("LAB DEBUG / Print private enemy patterns (explicit opt-in)")]
        private void PrintLabPatterns()
        {
            if (!enableLabPatternDebug || gameObject.scene.name != "SceneVictorLab")
            { Debug.Log("Private pattern inspection requires SceneVictorLab and explicit Lab Pattern Debug opt-in.", this); return; }
            Initialize();
            foreach (var pattern in Session.DebugPatterns(true))
            {
                var visible = Session.Snapshot.Enemies.First(x => x.Id == pattern.EnemyId).Intent;
                var entries = pattern.Actions.Select((action, i) => (pattern.VariableEntries[i] ? "V:" : "K:") + action);
                Debug.Log("LAB DEBUG ONLY | Enemy " + pattern.EnemyId + " | next " + pattern.NextIndex +
                    " | visible intent: " + visible + " | " + string.Join(", ", entries), this);
            }
        }

        [ContextMenu("Validate startup configuration")]
        private void OnValidate()
        {
            configurationValidation = BattleConfigurationFactory.Validate(ruleset, encounter);
            if (string.IsNullOrEmpty(configurationValidation)) configurationValidation = "Valid startup configuration. Runtime changes apply on next Play Mode start.";
        }

        private void OnDestroy()
        {
            if (Session == null) return;
            Session.Changed -= OnChanged;
            Session.EventRaised -= OnEvent;
        }
    }
}
