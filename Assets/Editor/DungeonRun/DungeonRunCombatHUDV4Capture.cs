using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonRun.Combat;
using DungeonRun.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Play Mode capture harness for the SceneVictorLab combat HUD: drives scripted combat states through public HUD APIs
/// and writes Game view screenshots at fixed sizes. Runtime-only; never saves assets or scene changes.
/// </summary>
public static class DungeonRunCombatHUDV4Capture
{
    /// <summary>Project-root relative folder that receives one sub-folder per capture label.</summary>
    public const string OutputRoot = "Captures/CombatHUDV4";
    /// <summary>Canonical scenario ids. Any subset passed to <see cref="Begin"/> runs in this order.</summary>
    public static readonly string[] Scenarios =
    {
        "idle", "hover", "selected", "drag_valid", "drag_invalid", "queued1", "queued2", "commit_hover", "detail", "split",
        "reveal", "resolving", "turn2", "victory", "defeat", "config4"
    };
    /// <summary>Opt-in ids outside the default set. "nohud": world-only frame (HUD canvas off) for the coverage metric.</summary>
    public static readonly string[] ExtraScenarios = { "nohud", "config1", "feedback" };
    /// <summary>Realtime settle delay after each scenario setup; HUD tweens run unscaled.</summary>
    public static double SettleSeconds = .6;

    private const string SizeName = "DungeonRun HUD Capture";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

    private sealed class Baseline
    {
        public CombatRulesetDefinition ruleset; public CombatEncounterDefinition encounter;
        public int seed, floor; public float reveal, phase; public bool labDebug, deferInit;
        public Renderer[] anchors; public CombatHUDTheme hudTheme, handTheme; public CardActionSlot[] slots;
    }
    private sealed class Fixture
    {
        public CombatRulesetDefinition ruleset; public CombatEncounterDefinition encounter;
        public float reveal, phase; public bool longFeedback;
    }

    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static readonly List<string> files = new List<string>(), warnings = new List<string>(),
        errors = new List<string>(), notes = new List<string>();
    private static readonly List<Object> clones = new List<Object>();
    private static readonly Dictionary<string, Fixture> fixtures = new Dictionary<string, Fixture>();
    private static Func<bool> wait;
    private static string state = "idle", step = "", label, folder, scenario;
    private static int done, total, sizeIndex = -1;
    private static Vector2Int resolution;
    private static CombatHUD hud;
    private static CombatBattleController battle;
    private static Baseline baseline;
    private static CombatHUDTheme longTheme;
    private static EventSystem eventSystem;
    private static bool eventSystemEnabled, runInBackground;
    private static float? flameTime;
    /// <summary>Flame clock value held during a capture job (same value as the Acrylic V3 world captures).</summary>
    public static float FrozenFlameTime = 1.25f;
    private static EditorWindow gameView;
    private static double revealedAt = -1;

    private static double Now => EditorApplication.timeSinceStartup;
    private static BattleSnapshot Session => battle.Session.Snapshot;
    private static CombatHUDSnapshot Snap => hud.liveSource.Snapshot;

    /// <summary>
    /// Starts an asynchronous capture job (driven by EditorApplication.update) and returns immediately.
    /// Requires unpaused Play Mode in SceneVictorLab with a Live-bound CombatHUD. Poll <see cref="Status"/>.
    /// </summary>
    /// <param name="label">Output sub-folder under Captures/CombatHUDV4; must not exist or must be empty.</param>
    /// <param name="scenarios">Scenario ids from <see cref="Scenarios"/>; null captures all.</param>
    /// <param name="include1440">Also capture 2560x1440 after 1920x1080.</param>
    public static string Begin(string label, string[] scenarios = null, bool include1440 = true)
    {
        if (stack.Count > 0) return "Busy. " + Status();
        var known = Scenarios.Concat(ExtraScenarios).ToArray();
        var unknown = (scenarios ?? Array.Empty<string>()).Except(known).ToArray();
        if (unknown.Length > 0) return "Unknown scenario(s): " + string.Join(", ", unknown) + ". Valid: " + string.Join(", ", known);
        var ids = scenarios == null ? Scenarios : known.Where(scenarios.Contains).ToArray();
        if (ids.Length == 0) return "No scenarios selected.";
        if (!Application.isPlaying || EditorApplication.isPaused) return "Enter unpaused Play Mode in SceneVictorLab first.";
        if (SceneManager.GetActiveScene().path != DungeonRunStylizedLighting.LabPath) return "SceneVictorLab only.";
        if (string.IsNullOrWhiteSpace(label) || label.Contains("..") || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Invalid label: use a plain folder name.";
        var found = Object.FindObjectsByType<CombatHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x =>
            x.isActiveAndEnabled && x.sourceMode == CombatHUD.SourceMode.Live && x.liveSource && x.liveSource.battle &&
            x.liveSource.battle.Session != null);
        if (!found) return "No CombatHUD bound in Live mode.";
        var target = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputRoot, label);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            return "Refusing: " + OutputRoot + "/" + label + " already exists and is not empty.";
        var view = FindGameView();
        if (!view || GameViewMember("SetCustomResolution") == null || GameViewMember("SizeSelectionCallback") == null ||
            GameViewType.GetProperty("selectedSizeIndex", AnyInstance) == null)
            return "Game view resolution API not found (Unity internals changed or no Game view open).";

        foreach (var list in new[] { files, warnings, errors, notes }) list.Clear();
        Directory.CreateDirectory(target);
        DungeonRunCombatHUDV4Capture.label = label; folder = target; hud = found; battle = found.liveSource.battle; gameView = view;
        var live = found.liveSource;
        baseline = new Baseline
        {
            ruleset = battle.ruleset, encounter = battle.encounter, seed = battle.seed, reveal = battle.revealSeconds,
            phase = battle.phaseSeconds, labDebug = battle.enableLabPatternDebug, floor = live.dungeonFloor,
            anchors = (Renderer[])live.enemyAnchors.Clone(), hudTheme = found.theme, handTheme = found.hand.theme,
            slots = (CardActionSlot[])found.hand.actionSlots.Clone(), deferInit = found.deferInitialization
        };
        runInBackground = Application.runInBackground; Application.runInBackground = true;
        // Frozen flame shape and light flicker make repeated captures of static states pixel-comparable.
        flameTime = DungeonRunFlameClock.OverrideTime; DungeonRunFlameClock.OverrideTime = FrozenFlameTime;
        // Real pointer input would race the scripted hover/drag states; the harness calls handlers directly.
        eventSystem = EventSystem.current;
        if (eventSystem) { eventSystemEnabled = eventSystem.enabled; eventSystem.enabled = false; }
        sizeIndex = (int)GameViewType.GetProperty("selectedSizeIndex", AnyInstance).GetValue(view);
        view.Focus();
        var sizes = include1440 ? new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) } : new[] { new Vector2Int(1920, 1080) };
        done = 0; total = ids.Length * sizes.Length; state = "running"; wait = null;
        stack.Push(Run(ids, sizes));
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += OnPlayModeChanged;
        return "Started '" + label + "': " + ids.Length + " scenario(s) x " + sizes.Length + " size(s) -> " + OutputRoot + "/" + label + ". Poll Status().";
    }

    /// <summary>Progress, current step, written files (project-root relative), warnings, errors and notes.</summary>
    public static string Status()
    {
        var text = new StringBuilder("[" + state + "] " + (label ?? "-") + "  " + done + "/" + total);
        if (stack.Count > 0) text.Append("  step: ").Append(step);
        foreach (var (title, list) in new[] { ("Files", files), ("Warnings", warnings), ("Errors", errors), ("Notes", notes) })
            if (list.Count > 0) text.Append('\n').Append(title).Append(" (").Append(list.Count).Append("):\n  ").Append(string.Join("\n  ", list));
        return text.ToString();
    }

    /// <summary>Stops a running job and restores input, background mode, Game view size, themes and a fresh battle.</summary>
    public static void Cancel()
    {
        if (stack.Count == 0) return;
        stack.Clear(); wait = null; state = "cancelled"; Restore(true);
    }

    [MenuItem("Dungeon Run/Combat HUD V4/Capture Set (Play Mode)")]
    private static void CaptureSetMenu() => Debug.Log("[HUD V4 Capture] " + Begin("manual_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")));
    [MenuItem("Dungeon Run/Combat HUD V4/Capture Set (Play Mode)", true)]
    private static bool CaptureSetMenuValid() => Application.isPlaying && stack.Count == 0;

    private static void Tick()
    {
        if (stack.Count == 0) return;
        if (!Application.isPlaying) { Abort("Play Mode ended during the capture job.", false); return; }
        try
        {
            if (wait != null && !wait()) return;
            wait = null;
            var top = stack.Peek();
            if (!top.MoveNext()) stack.Pop();
            else if (top.Current is IEnumerator child) stack.Push(child);
            else wait = top.Current as Func<bool>;
        }
        catch (Exception exception)
        {
            // A failure ends only the innermost routine (normally one scenario); the job continues with the next.
            wait = null; stack.Pop(); errors.Add(step + ": " + exception.GetType().Name + ": " + exception.Message);
            Debug.LogException(exception);
        }
        if (stack.Count == 0 && state == "running")
        {
            Restore(true); state = errors.Count > 0 ? "done with errors" : "done";
            Debug.Log("[HUD V4 Capture] " + state + ": " + files.Count + " file(s) in " + OutputRoot + "/" + label);
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode && stack.Count > 0) Abort("Play Mode exited during the capture job.", false);
    }

    private static void Abort(string reason, bool freshBattle)
    {
        errors.Add(reason); stack.Clear(); wait = null; state = "error"; Restore(freshBattle);
    }

    private static IEnumerator Run(string[] ids, Vector2Int[] sizes)
    {
        foreach (var size in sizes)
        {
            resolution = size; step = "resize " + size.x + "x" + size.y;
            GameViewMember("SetCustomResolution").Invoke(gameView, new object[] { new Vector2(size.x, size.y), SizeName });
            int start = Time.frameCount;
            yield return Until(() => Time.frameCount - start >= 6 && Screen.width == size.x && Screen.height == size.y, 5, "Game view resize");
            if (Screen.width != size.x || Screen.height != size.y)
                warnings.Add(step + ": screen is " + Screen.width + "x" + Screen.height + ", Game view target " +
                    GameViewType.GetProperty("targetRenderSize", AnyInstance)?.GetValue(gameView));
            foreach (var id in ids)
            {
                scenario = id; step = id + " @ " + size.x + "x" + size.y;
                yield return Scenario(id);
                done++;
            }
        }
    }

    private static IEnumerator Scenario(string id)
    {
        var fixture = MakeFixture(id);
        if (fixture == null) yield break;
        ResetBattle(fixture);
        yield return Frames(2);
        var s = Session; int hand = Math.Min(fixture.ruleset.handSize, fixture.encounter.startingDeck.Length);
        if (s.Turn != 1 || s.Phase != BattlePhase.Planning || s.Hand.Count != hand || hud.hand.ViewCount != hand)
            throw new InvalidOperationException("Fresh battle check failed: turn " + s.Turn + ", " + s.Phase + ", hand " +
                s.Hand.Count + "/" + hand + ", views " + hud.hand.ViewCount + ".");
        switch (id)
        {
            case "hover": hud.hand.Hover(View(Snap.Hand[Math.Min(2, Snap.Hand.Count - 1)].Id), true); break;
            case "selected": hud.hand.Select(FirstAttack().Id); break;
            case "drag_valid":
            case "drag_invalid":
            {
                if (id == "drag_invalid") { Queue(Snap.Hand[0].Id, 0, LivingEnemies()[0]); yield return Frames(1); }
                var view = View(Snap.Hand[0].Id);
                var pointer = Pointer(ScreenPoint(hud.hand.actionSlots[0].Rect));
                view.OnBeginDrag(pointer);
                yield return Seconds(SettleSeconds);
                view.OnDrag(pointer); // re-apply drop feedback after any refresh during the settle
                yield return Frames(1); yield return Shot(); yield break;
            }
            case "queued1": QueueFirstCards(1); break;
            case "queued2": QueueFirstCards(2); break;
            case "commit_hover":
                QueueFirstCards(2); yield return Frames(1);
                ExecuteEvents.Execute(hud.resolveButton.gameObject, Pointer(ScreenPoint((RectTransform)hud.resolveButton.transform)),
                    ExecuteEvents.pointerEnterHandler);
                break;
            case "detail": hud.hand.Inspect(View(Snap.Hand[0].Id)); break;
            case "split":
            {
                var card = Snap.Hand.FirstOrDefault(x => x.Definition.hitCount > 1 && x.Definition.targetMode == TargetMode.SingleOpponent);
                var living = LivingEnemies();
                if (card == null || living.Length < 2) { Note("split: skipped (no multi-hit card in the opening hand, or fewer than two living enemies)"); yield break; }
                hud.hand.Select(card.Id);
                ((ICombatTargetingSource)hud.liveSource).SetPerHitTargeting(true);
                hud.liveSource.TrySelectTarget(living[0]); hud.liveSource.TrySelectTarget(living[1]);
                if (card.Definition.hitCount == 2) Note("split: 2-hit card; assigning both hits queued it (split targets show on the slot)");
                break;
            }
            case "reveal":
            {
                QueueFirstCards(2); Commit();
                yield return Until(() => revealedAt >= 0, 3, "EnemyRevealed");
                double at = revealedAt + .35;
                yield return Until(() => Now >= at, 2, "reveal delay");
                yield return Shot(); yield break;
            }
            case "resolving":
                QueueFirstCards(2); Commit();
                yield return Until(() => Session.Phase == BattlePhase.Resolution, 6, "Resolution phase");
                yield return Seconds(.45); yield return Shot(); yield break;
            case "turn2":
                QueueFirstCards(2); Commit();
                yield return Until(() => Session.Turn == 2 && Session.Phase == BattlePhase.Planning, 10, "turn 2 planning");
                yield return Seconds(SettleSeconds + .3); yield return Shot(); yield break;
            case "nohud":
            {
                var canvas = hud.canvas; bool was = canvas.enabled; canvas.enabled = false;
                yield return Seconds(SettleSeconds); yield return Shot(); canvas.enabled = was; yield break;
            }
            case "config1": QueueFirstCards(1); break;
            case "feedback":
            {
                // Presentation-only: drives the presenter's own Float with every label kind the battle events produce.
                var fx = hud.feedback; var theme = hud.theme;
                var floatMethod = typeof(CombatFeedbackPresenter).GetMethod("Float", BindingFlags.Instance | BindingFlags.NonPublic);
                int enemyId = LivingEnemies()[0];
                var labels = new (int, string, Color)[]
                {
                    (0, "-2", theme.damageColor), (0, "BLOCK 1", theme.blockColor), (0, "+1", theme.healColor), (0, "CHARGED", theme.gold),
                    (enemyId, "PIERCED 1", theme.damageColor), (enemyId, "DODGE", theme.text), (enemyId, "-1", theme.damageColor)
                };
                foreach (var (actor, message, color) in labels) floatMethod.Invoke(fx, new object[] { actor, message, color });
                yield return Seconds(.5); yield return Shot(); yield break;
            }
            case "victory":
            case "defeat":
                yield return DriveToTerminal(id);
                yield return Seconds(Math.Max(SettleSeconds, 1.2)); yield return Shot(); yield break;
        }
        yield return Seconds(SettleSeconds);
        yield return Shot();
    }

    private static Fixture MakeFixture(string id)
    {
        if (fixtures.TryGetValue(id, out var cached)) return cached;
        var f = new Fixture { ruleset = baseline.ruleset, encounter = baseline.encounter, reveal = baseline.reveal, phase = baseline.phase };
        switch (id)
        {
            // Pacing fields are presentation-only; longer windows keep the timed captures inside their phase.
            case "reveal": f.reveal = Mathf.Max(baseline.reveal, 2.5f); break;
            case "resolving": f.phase = Mathf.Max(baseline.phase, 3f); f.longFeedback = true; break;
            case "victory":
                f.encounter = Clone(baseline.encounter);
                foreach (var enemy in f.encounter.enemies) enemy.initialHealth = 1;
                f.reveal = f.phase = .1f; break;
            case "defeat":
                f.encounter = Clone(baseline.encounter); f.encounter.playerInitialHealth = 1;
                foreach (var enemy in f.encounter.enemies) enemy.pattern = AttackOnlyPattern(enemy.pattern ? enemy.pattern : baseline.ruleset.defaultEnemyPattern);
                f.reveal = f.phase = .1f; break;
            case "config4":
                f.ruleset = Clone(baseline.ruleset); f.ruleset.playerActionsPerTurn = 4; f.ruleset.handSize = 6;
                f.ruleset.cardsDrawnAfterTurn = Mathf.Max(f.ruleset.cardsDrawnAfterTurn, 4); break;
            case "config1":
                f.ruleset = Clone(baseline.ruleset); f.ruleset.playerActionsPerTurn = 1; f.ruleset.handSize = 3; break;
        }
        var invalid = BattleConfigurationFactory.Validate(f.ruleset, f.encounter);
        if (!string.IsNullOrEmpty(invalid)) { Note(id + ": skipped, fixture invalid (" + invalid + ")"); f = null; }
        fixtures[id] = f;
        return f;
    }

    private static EnemyPatternDefinition AttackOnlyPattern(EnemyPatternDefinition source)
    {
        bool Attack(CardData x) => x && x.attackPower > 0 && x.targetMode == TargetMode.SingleOpponent;
        var pool = source ? (source.knownActions ?? new CardData[0]).Concat(source.variableActionPool ?? new CardData[0]) : Enumerable.Empty<CardData>();
        var attack = pool.FirstOrDefault(Attack) ?? baseline.encounter.startingDeck.FirstOrDefault(Attack);
        if (!attack) throw new InvalidOperationException("Defeat fixture: no single-target attack CardData available.");
        var pattern = ScriptableObject.CreateInstance<EnemyPatternDefinition>();
        pattern.name = "AttackOnly (Capture)"; pattern.hideFlags = HideFlags.HideAndDontSave; clones.Add(pattern);
        pattern.knownActions = new[] { attack }; pattern.variableSlots = 0; pattern.variableActionPool = new CardData[0];
        return pattern;
    }

    private static T Clone<T>(T source) where T : Object
    {
        var copy = Object.Instantiate(source);
        copy.name = source.name + " (Capture)"; copy.hideFlags = HideFlags.HideAndDontSave; clones.Add(copy);
        return copy;
    }

    /// <summary>
    /// Replaces the battle controller and live source with fresh runtime components. CombatBattleController.Initialize is
    /// one-shot and CombatHUD pins its first Play Mode source until that object is destroyed, so replacement is the only
    /// public reset path (no reflection on LiveCombatHUDSource caches).
    /// </summary>
    private static void ResetBattle(Fixture f)
    {
        var oldSource = hud.liveSource; var oldBattle = oldSource.battle;
        PointerExitCommit(); ClearEnemyHistory();
        hud.deferInitialization = true;
        try
        {
            hud.enabled = false; // OnDisable -> Unbind: listeners, feedback, card views, panels
            if (oldBattle) oldBattle.EventRaised -= OnBattleEvent;
            var host = oldBattle ? oldBattle.gameObject : oldSource.gameObject;
            var fresh = host.AddComponent<CombatBattleController>();
            fresh.ruleset = f.ruleset; fresh.encounter = f.encounter; fresh.seed = baseline.seed;
            fresh.revealSeconds = f.reveal; fresh.phaseSeconds = f.phase; fresh.enableLabPatternDebug = baseline.labDebug;
            var source = oldSource.gameObject.AddComponent<LiveCombatHUDSource>();
            source.battle = fresh; source.enemyAnchors = (Renderer[])baseline.anchors.Clone(); source.dungeonFloor = baseline.floor;
            Object.DestroyImmediate(oldSource);
            if (oldBattle) Object.DestroyImmediate(oldBattle);
            battle = fresh; battle.EventRaised += OnBattleEvent; revealedAt = -1;
            hud.liveSource = source;
            if (f.longFeedback)
            {
                if (!longTheme) { longTheme = Clone(baseline.hudTheme); longTheme.floatingTextDuration = 6; }
                hud.theme = hud.hand.theme = longTheme;
            }
            else { hud.theme = baseline.hudTheme; hud.hand.theme = baseline.handTheme; }
            hud.enabled = true;
            hud.Initialize();
            ResetCommitScale();
        }
        finally { hud.deferInitialization = baseline.deferInit; }
    }

    private static void ClearEnemyHistory()
    {
        // Presentation-only caches (observed history, V4 intent visuals); clearing them makes every scenario match a fresh Play session.
        foreach (var enemy in hud.enemies.Where(x => x)) enemy.ResetPresentation();
    }

    private static void ResetCommitScale()
    {
        // A pulse cut short by CombatHUD.Unbind can leave the Commit plate scaled; every scenario starts from rest.
        if (hud && hud.resolveButton) hud.resolveButton.transform.localScale = Vector3.one;
    }

    private static IEnumerator DriveToTerminal(string id)
    {
        for (int turn = 1; turn <= 8 && !Session.IsTerminal; turn++)
        {
            var living = LivingEnemies();
            for (int slot = 0; slot < Session.Slots.Count; slot++)
            {
                var hand = Snap.Hand;
                var card = hand.FirstOrDefault(x => x.Definition.attackPower > 0) ??
                    hand.FirstOrDefault(x => x.Definition.defensePower == 0 && x.Definition.dodgeCount == 0) ?? hand[0];
                Queue(card.Id, slot, living[slot % living.Length]);
            }
            Commit();
            int current = turn;
            yield return Until(() => Session.IsTerminal || Session.Phase == BattlePhase.Planning && Session.Turn > current, 10, "turn " + turn);
            // The controller's progression coroutine still waits phaseSeconds after returning to Planning.
            yield return Seconds(battle.phaseSeconds + .25f);
        }
        var s = Session;
        if (!s.IsTerminal) throw new InvalidOperationException(id + " fixture did not reach a terminal phase within 8 turns.");
        var expected = id == "victory" ? BattlePhase.Victory : BattlePhase.Defeat;
        if (s.Phase != expected) warnings.Add(step + ": fixture ended in " + s.Phase + " instead of " + expected);
        Note(id + ": transient fixture driven by commits; reached " + s.Phase + " on turn " + s.Turn);
    }

    private static void QueueFirstCards(int count)
    {
        for (int slot = 0; slot < count; slot++) Queue(Snap.Hand[0].Id, slot, LivingEnemies()[0]);
    }

    private static void Queue(int cardId, int slot, int enemyId)
    {
        var source = hud.liveSource;
        if (source.TryQueue(cardId, slot)) return;
        // SingleOpponent cards enter card-first targeting instead of queueing; a living-enemy click completes them.
        if (source.Snapshot.TargetingCardId == cardId && source.TrySelectTarget(enemyId) && Session.Slots[slot] != null) return;
        throw new InvalidOperationException("Could not queue card " + cardId + " in slot " + slot + ": " + battle.LastError);
    }

    private static void Commit()
    {
        revealedAt = -1;
        hud.resolveButton.onClick.Invoke(); // same path as a click: CombatHUD pulse, then source.TryResolve()
        if (Session.Phase == BattlePhase.Planning) throw new InvalidOperationException("Commit rejected: " + battle.LastError);
    }

    private static void OnBattleEvent(BattleEvent value)
    {
        if (value.Kind == BattleEventKind.EnemyRevealed && revealedAt < 0) revealedAt = Now;
    }

    private static IEnumerator Shot()
    {
        var size = resolution; var name = scenario + "_" + size.x + "x" + size.y + ".png"; var path = Path.Combine(folder, name);
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        long length = -1; double since = 0;
        yield return Until(() =>
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0) return false;
            if (info.Length != length) { length = info.Length; since = Now; return false; }
            return Now - since >= .2;
        }, 10, "write " + name);
        if (!File.Exists(path)) throw new IOException("Screenshot not written: " + name + " (is the Game view visible?)");
        var actual = PngSize(path);
        if (actual != size) warnings.Add(name + ": PNG is " + actual.x + "x" + actual.y + ", expected " + size.x + "x" + size.y);
        var relative = OutputRoot + "/" + label + "/" + name;
        files.Add(relative);
        Debug.Log("[HUD V4 Capture] " + relative + " (" + actual.x + "x" + actual.y + ", " + new FileInfo(path).Length + " bytes)");
    }

    private static Vector2Int PngSize(string path)
    {
        var header = new byte[24];
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            if (stream.Read(header, 0, header.Length) < header.Length || header[1] != 'P' || header[2] != 'N' || header[3] != 'G')
                return Vector2Int.zero;
        int BigEndian(int i) => header[i] << 24 | header[i + 1] << 16 | header[i + 2] << 8 | header[i + 3];
        return new Vector2Int(BigEndian(16), BigEndian(20)); // IHDR width/height
    }

    private static void Restore(bool freshBattle)
    {
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Try("restore battle", () =>
        {
            if (!freshBattle || !Application.isPlaying || !hud || baseline == null) return;
            var extra = hud.hand.actionSlots.Except(baseline.slots).ToArray(); // config4 runtime slot clones
            hud.hand.actionSlots = (CardActionSlot[])baseline.slots.Clone();
            foreach (var slot in extra) if (slot) Object.DestroyImmediate(slot.gameObject);
            ResetBattle(new Fixture { ruleset = baseline.ruleset, encounter = baseline.encounter, reveal = baseline.reveal, phase = baseline.phase });
        });
        Try("restore input", () => { if (eventSystem) eventSystem.enabled = eventSystemEnabled; });
        if (Application.isPlaying) Application.runInBackground = runInBackground; // runtime value; Play Mode exit resets it anyway
        DungeonRunFlameClock.OverrideTime = flameTime;
        Try("restore Game view size", () =>
        {
            if (gameView && sizeIndex >= 0) GameViewMember("SizeSelectionCallback").Invoke(gameView, new object[] { sizeIndex, null });
        });
        if (hud && baseline != null) { hud.theme = baseline.hudTheme; hud.hand.theme = baseline.handTheme; }
        foreach (var clone in clones.Where(x => x)) Object.DestroyImmediate(clone); // transient runtime instances, never assets
        clones.Clear(); fixtures.Clear(); longTheme = null;
    }

    private static void Try(string what, Action action)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(what + ": " + exception.Message); Debug.LogException(exception); }
    }

    private static EditorWindow FindGameView()
    {
        if (GameViewType == null) return null;
        var main = GameViewType.BaseType?.GetMethod("GetMainPlayModeView", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null) as EditorWindow;
        return main && GameViewType.IsInstanceOfType(main) ? main : Resources.FindObjectsOfTypeAll(GameViewType).OfType<EditorWindow>().FirstOrDefault();
    }

    private static MethodInfo GameViewMember(string name) => name == "SetCustomResolution"
        ? GameViewType?.GetMethod(name, AnyInstance, null, new[] { typeof(Vector2), typeof(string) }, null)
        : GameViewType?.GetMethod(name, AnyInstance, null, new[] { typeof(int), typeof(object) }, null);

    private static CardView View(int cardId) =>
        hud.hand.handRoot.GetComponentsInChildren<CardView>().FirstOrDefault(x => x.Item != null && x.Item.Id == cardId) ??
        throw new InvalidOperationException("No card view for card " + cardId + ".");

    private static CombatCardItem FirstAttack() =>
        Snap.Hand.FirstOrDefault(x => x.Kind == PreviewCardKind.Attack && x.Definition.targetMode == TargetMode.SingleOpponent) ??
        Snap.Hand.FirstOrDefault(x => x.Definition.targetMode == TargetMode.SingleOpponent) ?? Snap.Hand[0];

    private static int[] LivingEnemies() => Session.Enemies.Where(x => x.Alive).Select(x => x.Id).ToArray();

    private static void PointerExitCommit()
    {
        if (hud && hud.resolveButton) ExecuteEvents.Execute(hud.resolveButton.gameObject, Pointer(Vector2.zero), ExecuteEvents.pointerExitHandler);
    }

    private static PointerEventData Pointer(Vector2 screen) =>
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = screen };

    // Screen Space Overlay canvas: world position is already in screen pixels (null camera).
    private static Vector2 ScreenPoint(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);

    private static void Note(string text) { if (!notes.Contains(text)) notes.Add(text); }

    private static Func<bool> Until(Func<bool> condition, double timeout, string what)
    {
        double deadline = Now + timeout;
        return () =>
        {
            if (condition()) return true;
            if (Now < deadline) return false;
            warnings.Add(step + ": timed out waiting for " + what); return true;
        };
    }

    private static Func<bool> Seconds(double seconds) { double end = Now + seconds; return () => Now >= end; }
    private static Func<bool> Frames(int count) { int end = Time.frameCount + count; return () => Time.frameCount >= end; }
}
