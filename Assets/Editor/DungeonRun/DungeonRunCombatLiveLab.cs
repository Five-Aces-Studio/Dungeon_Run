using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonRun.Combat;
using DungeonRun.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit upgrade of the existing Lab HUD; never rebuilds V1 visuals or changes scene rendering.</summary>
public static class DungeonRunCombatLiveLab
{
    public const string AssetFolder = "Assets/Settings/CombatV2";

    [MenuItem("Dungeon Run/Combat HUD V2/Install Live Lab")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || scene.isDirty ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require clean SceneVictorLab in stable Edit Mode.");
        var root = scene.GetRootGameObjects().Single(x => x.name == DungeonRunCombatHUDLab.RootName);
        var hud = root.GetComponent<CombatHUD>();
        var lab = root.GetComponent<LabCombatHUDSource>();
        if (!hud || !lab || !hud.hand || hud.hand.actionSlots.Length == 0 || hud.enemies.Length == 0)
            throw new InvalidOperationException("Install requires the existing complete Combat HUD V1 composition.");
        var stage = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var anchors = Enumerable.Range(1, 3).Select(i => stage.GetComponentsInChildren<MeshRenderer>(true)
            .Single(x => x.name == "CHR_Enemy_0" + i)).Cast<Renderer>().ToArray();

        EnsureFolder(AssetFolder + "/Cards");
        var cards = CreateCards();
        var six = Pattern("EnemyPattern_6plus1", 1, new[] { "Attack", "Attack", "Defence", "Dodge", "Miss", "Combo" }, cards);
        var five = Pattern("EnemyPattern_5plus2", 2, new[] { "Attack", "Defence", "Dodge", "Miss", "Combo" }, cards);
        var current = Rules("CombatRuleset_CurrentDigital", six);
        Rules("CombatRuleset_TabletopBaseline", six);
        Rules("CombatRuleset_Pattern_6plus1", six);
        Rules("CombatRuleset_Pattern_5plus2", five);
        var encounter = Asset<CombatEncounterDefinition>("CombatEncounter_Lab", value =>
        {
            value.playerName = "Wayfarer"; value.playerInitialHealth = value.playerMaxHealth = 10;
            // Authored baseline composition: 9 Attack, 4 Defence, 3 Miss, 2 Dodge, 2 Combo, 1 Heal.
            var deck = new List<CardData>();
            Add(deck, cards["Attack"], 9); Add(deck, cards["Defence"], 4); Add(deck, cards["Miss"], 3);
            Add(deck, cards["Dodge"], 2); Add(deck, cards["Combo"], 2); Add(deck, cards["Heal Self"], 1);
            value.startingDeck = deck.ToArray();
            value.enemies = new[]
            {
                Enemy("Abyss Warden", 6), Enemy("Ash Hound", 4), Enemy("Trigon Sentinel", 7)
            };
        });
        string validation = BattleConfigurationFactory.Validate(current, encounter);
        if (!string.IsNullOrEmpty(validation)) throw new InvalidOperationException(validation);

        Undo.RecordObject(hud, "Configure live combat source");
        Undo.RecordObject(lab, "Retain inactive Lab preview source");
        // Disable the presenter while adding references; no half-configured session may initialize.
        bool wasEnabled = hud.enabled;
        hud.enabled = false;
        hud.hand.Cleanup();
        var host = root.GetComponent<CombatBattleController>() ?? Undo.AddComponent<CombatBattleController>(root);
        var live = root.GetComponent<LiveCombatHUDSource>() ?? Undo.AddComponent<LiveCombatHUDSource>(root);
        Undo.RecordObject(host, "Configure battle startup"); Undo.RecordObject(live, "Configure live HUD projection");
        host.ruleset = current; host.encounter = encounter; host.seed = 173;
        host.enableLabPatternDebug = false;
        live.battle = host; live.enemyAnchors = anchors;
        hud.labSource = lab; hud.liveSource = live; hud.sourceComponent = lab;
        hud.sourceMode = CombatHUD.SourceMode.Live;
        lab.enabled = false;
        hud.deferInitialization = true;
        hud.enabled = wasEnabled;
        hud.deferInitialization = false;
        // Keep the saved scene definition-only. Live state initializes when the HUD enters Play Mode.
        hud.endTurnButton.gameObject.SetActive(false);
        hud.previewLabel.text = "LIVE COMBAT  /  ENTER PLAY MODE";
        hud.statusText.text = "Select ruleset and encounter on CombatBattleController before Play.";
        foreach (var item in new UnityEngine.Object[] { hud, lab, host, live }) EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }

    private static CombatEnemyDefinition Enemy(string name, int hp) => new CombatEnemyDefinition
    { displayName = name, initialHealth = hp, maxHealth = hp, actionsPerResolution = 1, pattern = null };

    private static void Add(List<CardData> deck, CardData card, int count)
    { for (int i = 0; i < count; i++) deck.Add(card); }

    private static Dictionary<string, CardData> CreateCards()
    {
        var cards = new Dictionary<string, CardData>();
        void Card(string name, ActionIconKind icon, TargetMode target, int damage = 0, int block = 0,
            int heal = 0, int dodge = 0, int hits = 1, PiercingMode piercing = PiercingMode.Normal, int charge = 1)
        {
            cards.Add(name, Asset<CardData>("Cards/" + name.Replace(" ", ""), value =>
            {
                value.cardName = name; value.description = "Live numeric description is generated from the frozen combat definition.";
                value.actionCost = 1; value.presentationKind = icon; value.targetMode = target;
                value.attackPower = damage; value.defensePower = block; value.healPower = heal;
                value.dodgeCount = dodge; value.hitCount = hits; value.piercingMode = piercing;
                value.chargeMultiplier = charge; value.chargeTurns = 1;
            }));
        }
        Card("Attack", ActionIconKind.Attack, TargetMode.SingleOpponent, damage: 1);
        Card("Defence", ActionIconKind.Defence, TargetMode.Self, block: 1);
        Card("Miss", ActionIconKind.Miss, TargetMode.None);
        Card("Dodge", ActionIconKind.Dodge, TargetMode.Self, dodge: 1);
        Card("Combo", ActionIconKind.Combo, TargetMode.SingleOpponent, damage: 1, hits: 2);
        Card("Combo Plus", ActionIconKind.Combo, TargetMode.SingleOpponent, damage: 1, hits: 3);
        Card("Heal Self", ActionIconKind.Heal, TargetMode.Self, heal: 1);
        Card("Piercing Attack", ActionIconKind.Piercing, TargetMode.SingleOpponent, damage: 1, piercing: PiercingMode.IgnoreBlock);
        Card("Counterattack", ActionIconKind.Counterattack, TargetMode.SingleOpponent, damage: 1, dodge: 1);
        Card("Charge", ActionIconKind.Charge, TargetMode.Self, charge: 2);
        return cards;
    }

    private static EnemyPatternDefinition Pattern(string name, int variable, string[] known, Dictionary<string, CardData> cards)
    {
        return Asset<EnemyPatternDefinition>(name, value =>
        {
            value.knownActions = known.Select(x => cards[x]).ToArray();
            value.variableSlots = variable;
            value.variableActionPool = new[] { cards["Attack"], cards["Defence"], cards["Dodge"], cards["Miss"] };
            value.shuffleOnSpawn = true; value.cycleMode = PatternCycleMode.PreserveOrder;
            value.regenerateVariableEntriesOnCycle = false;
        });
    }
    private static CombatRulesetDefinition Rules(string name, EnemyPatternDefinition pattern)
    {
        return Asset<CombatRulesetDefinition>(name, value =>
        {
            value.handSize = 5; value.playerActionsPerTurn = 2; value.requireExactActionCount = true;
            value.cardsDrawnAfterTurn = 2; value.refillHandAfterResolution = true;
            value.shuffleInitialPlayerDeck = true; value.drawPileRecycleMode = DeckRecycleMode.ShuffleDiscardIntoDraw;
            value.enemyActionRevealMode = EnemyRevealMode.AfterPlayerCommit;
            value.defaultEnemyPattern = pattern;
        });
    }
    private static T Asset<T>(string name, Action<T> initialize) where T : ScriptableObject
    {
        string path = AssetFolder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing) return existing; // Reinstallation must not overwrite Inspector playtest tuning.
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = Path.GetFileName(name); initialize(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
