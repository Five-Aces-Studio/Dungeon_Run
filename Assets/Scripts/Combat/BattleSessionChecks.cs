using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonRun.Combat
{
    /// <summary>Deterministic, engine-independent regression checks; callable from an Editor bridge or console.</summary>
    public static class BattleSessionChecks
    {
        public static string RunAll()
        {
            var passed = new List<string>();
            void Run(string name, Action test) { test(); passed.Add(name); }
            Run("Simultaneous mutual kill", () =>
            {
                var s = Create(new[] { Attack(1) }, new[] { 1 }, new[] { 1 }, hp: 1, enemyHp: 1);
                Queue(s, 0, 0, 1); CommitResolve(s);
                Check(s.Snapshot.Player.Health == 0 && s.Snapshot.Enemies[0].Health == 0, "Both committed attacks must land.");
                s.Advance(out _); s.Advance(out _); Check(s.Snapshot.Phase == BattlePhase.Draw, "Mutual death is draw.");
            });
            Run("Lethal damage precedes survivor healing", () =>
            {
                var heal = Action(2, heal: 5, target: TargetMode.Self);
                var s = Create(new[] { Attack(1), heal }, new[] { 2 }, new[] { 1 }, hp: 1);
                Queue(s, 0, 0); CommitResolve(s); Check(s.Snapshot.Player.Health == 0, "Healing must not resurrect.");
            });
            Run("Defence and per-hit dodge prepare before attacks", () =>
            {
                var guard = Action(2, block: 1, dodge: 1);
                var s = Create(new[] { Attack(1, 1, 3), guard }, new[] { 2 }, new[] { 1 });
                Queue(s, 0, 0); CommitResolve(s); Check(s.Snapshot.Player.Health == 9, "Dodge first hit, block second, receive third.");
            });
            Run("Piercing bypasses block but not dodge", () =>
            {
                var pierce = Action(1, damage: 1, hits: 2, target: TargetMode.SingleOpponent, piercing: PiercingMode.IgnoreBlock);
                var s = Create(new[] { pierce, Action(2, block: 10, dodge: 1) }, new[] { 2 }, new[] { 1 });
                Queue(s, 0, 0); CommitResolve(s);
                Check(s.Snapshot.Player.Health == 9 && s.Snapshot.Player.Block == 10, "Piercing must preserve unused block.");
            });
            Run("Frozen multi-hit target distribution", () =>
            {
                var s = Create(new[] { Attack(1, 2, 2), Action(2) }, new[] { 1 }, new[] { 2 }, enemyCount: 2);
                int[] targets = { 1, 2 };
                Check(s.TryQueue(s.Snapshot.Hand[0].Id, 0, new TargetSelection(targets), out _), "Queue distributed hits.");
                targets[0] = 2; CommitResolve(s);
                Check(s.Snapshot.Enemies.All(x => x.Health == 8), "Target selection must be frozen, one hit each.");
            });
            Run("Invalid targets and stable cancellation", () =>
            {
                var s = Create(new[] { Attack(1), Action(2) }, new[] { 1, 1, 1 }, new[] { 2 }, actions: 2);
                int[] original = s.Snapshot.Hand.Select(x => x.Id).ToArray();
                Check(!s.TryQueue(original[0], 0, new TargetSelection(99), out _), "Invalid target rejected.");
                Queue(s, 0, 0, 1); Queue(s, 0, 1, 1); s.TryUnqueue(0, out _); s.TryUnqueue(1, out _);
                Check(s.Snapshot.Hand.Select(x => x.Id).SequenceEqual(original), "Cancel A then B preserves order.");
                Queue(s, 0, 0, 1); Queue(s, 0, 1, 1); s.TryUnqueue(1, out _); s.TryUnqueue(0, out _);
                Check(s.Snapshot.Hand.Select(x => x.Id).SequenceEqual(original), "Cancel B then A preserves order.");
            });
            Run("Exact versus up-to action count", () =>
            {
                var exact = Create(new[] { Action(1) }, new[] { 1, 1 }, new[] { 1 }, actions: 2);
                Queue(exact, 0, 0); Check(!exact.TryCommit(out _), "Exact count enforced.");
                var upto = Create(new[] { Action(1) }, new[] { 1, 1 }, new[] { 1 }, actions: 2, exact: false);
                Check(!upto.TryCommit(out _), "Empty turn rejected."); Queue(upto, 0, 0); Check(upto.TryCommit(out _), "Partial turn accepted.");
            });
            Run("Deck conservation through recycling", () =>
            {
                var s = Create(new[] { Action(1) }, Enumerable.Repeat(1, 21).ToArray(), new[] { 1 }, actions: 2, handSize: 5);
                for (int turn = 0; turn < 40; turn++)
                {
                    Queue(s, 0, 0); Queue(s, 0, 1); Check(Count(s) == 21, "Queued conservation.");
                    Check(s.TryCommit(out _), "Commit.");
                    while (s.Snapshot.Phase != BattlePhase.Planning) { Check(s.Advance(out _), "Advance."); Check(Count(s) == 21, "Phase conservation."); }
                    Check(s.Snapshot.Hand.Count == 5, "Default refill retains five.");
                }
            });
            Run("Reveal redaction and debug permission", () =>
            {
                var s = Create(new[] { Action(1) }, new[] { 1 }, new[] { 1 });
                Check(s.Snapshot.Enemies[0].Intent == "Unknown", "Future intent redacted.");
                Throws(() => s.DebugPatterns(false)); Queue(s, 0, 0); s.TryCommit(out _);
                Check(s.Snapshot.Enemies[0].Intent == "A1", "Committed intent revealed.");
            });
            Run("Seeded 6+1 and 5+2 patterns preserve cycles", () =>
            {
                foreach (int variables in new[] { 1, 2 })
                {
                    var pattern = new PatternConfiguration(Enumerable.Range(1, 7 - variables), variables, new[] { 7, 8 }, true);
                    var a = PatternSession(pattern, 123); var b = PatternSession(pattern, 123);
                    string[] before = a.DebugPatterns(true)[0].Actions.ToArray();
                    Check(before.SequenceEqual(b.DebugPatterns(true)[0].Actions), "Equal seeds match.");
                    Check(a.DebugPatterns(true)[0].VariableEntries.Count(x => x) == variables, "Variable slots tracked.");
                    for (int i = 0; i < 7; i++) Round(a);
                    Check(before.SequenceEqual(a.DebugPatterns(true)[0].Actions), "Default cycle preserved.");
                }
            });
            Run("Optional cycle shuffle and variable regeneration", () =>
            {
                var shuffled = PatternSession(new PatternConfiguration(Enumerable.Range(1, 6), 1, new[] { 7, 8 }, false, PatternCycleMode.ShuffleOnCycle), 123);
                var before = shuffled.DebugPatterns(true)[0].Actions.ToArray();
                for (int i = 0; i < 7; i++) Round(shuffled);
                Check(!before.SequenceEqual(shuffled.DebugPatterns(true)[0].Actions), "Seeded shuffle changes cycle order.");
                var regenerated = PatternSession(new PatternConfiguration(new[] { 1 }, 1, new[] { 7, 8 }, false, regenerate: true), 123);
                var seen = new HashSet<string>();
                for (int i = 0; i < 20; i++) { seen.Add(regenerated.DebugPatterns(true)[0].Actions[1]); Round(regenerated); }
                Check(seen.Count == 2, "Seeded regeneration samples pool on cycles.");
            });
            Run("Configuration input copies and numeric guards", () =>
            {
                var deck = new[] { 1 }; var known = new[] { 1 };
                var cfg = new BattleConfiguration(new BattleRules(1, 1), "P", 10, 10, new[] { Action(1) }, deck,
                    new[] { new EnemyConfiguration("E", 10, 10, 1, new PatternConfiguration(known, 0, new int[0])) });
                deck[0] = 99; known[0] = 99; Check(new BattleSession(cfg, 1).Snapshot.Hand[0].DefinitionId == 1, "Input arrays copied.");
                Throws(() => new BattleRules(0)); Throws(() => new BattleRules(draw: -1));
                Throws(() => new BattleRules(draw: 0)); Throws(() => new BattleRules(actions: 3, draw: 2));
                Throws(() => new BattleRules(recycle: (DeckRecycleMode)99));
                Throws(() => new PatternConfiguration(new int[0], 1, new int[0]));
                Throws(() => new EnemyConfiguration("E", 0, 1, 1, cfg.Enemies[0].Pattern));
                Throws(() => Create(new[] { Action(1, damage: 1) }, new[] { 1 }, new[] { 1 }));
                Throws(() => Create(new[] { Action(1, damage: 1, heal: 1, target: TargetMode.SingleOpponent) }, new[] { 1 }, new[] { 1 }));
            });
            Run("Next-turn bounded charge", () =>
            {
                var charge = new ActionDefinition(2, "Charge", 0, 0, 0, 0, 1, TargetMode.None, PiercingMode.Normal, ActionIconKind.Charge, 2, 1);
                var s = Create(new[] { Attack(1), charge, Action(3) }, new[] { 2, 1, 1 }, new[] { 3 }, handSize: 3);
                Queue(s, 0, 0); s.TryCommit(out _); while (s.Snapshot.Phase != BattlePhase.Planning) s.Advance(out _);
                Queue(s, 0, 0, 1); CommitResolve(s); Check(s.Snapshot.Enemies[0].Health == 8, "Charge applies next turn.");
            });
            Run("Published snapshot readiness and reentrancy guard", () =>
            {
                var s = Create(new[] { Action(1) }, new[] { 1 }, new[] { 1 });
                bool ready = false, reentryRejected = false;
                s.Changed += () => { ready = s.Snapshot.CanCommit; reentryRejected = !s.TryCommit(out _); };
                Queue(s, 0, 0); Check(ready && reentryRejected, "Snapshot ready while nested command is rejected.");
            });
            return "PASS " + passed.Count + " deterministic checks\n" + string.Join("\n", passed);
        }

        private static ActionDefinition Action(int id, int damage = 0, int block = 0, int heal = 0, int dodge = 0, int hits = 1,
            TargetMode target = TargetMode.None, PiercingMode piercing = PiercingMode.Normal)
            => new ActionDefinition(id, "A" + id, damage, block, heal, dodge, hits, target, piercing, ActionIconKind.Attack);
        private static ActionDefinition Attack(int id, int damage = 1, int hits = 1) => Action(id, damage: damage, hits: hits, target: TargetMode.SingleOpponent);
        private static BattleSession Create(ActionDefinition[] definitions, int[] deck, int[] enemyPattern, int hp = 10, int enemyHp = 10,
            int actions = 1, bool exact = true, int enemyCount = 1, int handSize = 0)
        {
            var rules = new BattleRules(handSize == 0 ? deck.Length : handSize, actions, exact, shuffle: false);
            var pattern = new PatternConfiguration(enemyPattern, 0, new int[0], false);
            return new BattleSession(new BattleConfiguration(rules, "P", hp, hp, definitions, deck,
                Enumerable.Range(0, enemyCount).Select(i => new EnemyConfiguration("E" + i, enemyHp, enemyHp, 1, pattern))), 123);
        }
        private static BattleSession PatternSession(PatternConfiguration pattern, int seed)
            => new BattleSession(new BattleConfiguration(new BattleRules(1, 1), "P", 10, 10,
                Enumerable.Range(1, 8).Select(id => Action(id)), new[] { 1 }, new[] { new EnemyConfiguration("E", 10, 10, 1, pattern) }), seed);
        private static void Round(BattleSession s) { Queue(s, 0, 0); Check(s.TryCommit(out _), "Commit pattern round."); while (s.Snapshot.Phase != BattlePhase.Planning) Check(s.Advance(out _), "Advance round."); }
        private static int Count(BattleSession s) => s.Snapshot.Hand.Count + s.Snapshot.Slots.Count(x => x != null) + s.Snapshot.DrawCount + s.Snapshot.DiscardCount;
        private static void Queue(BattleSession s, int hand, int slot, params int[] targets) => Check(s.TryQueue(s.Snapshot.Hand[hand].Id, slot, new TargetSelection(targets), out string error), error);
        private static void CommitResolve(BattleSession s) { Check(s.TryCommit(out string error), error); Check(s.Advance(out error), error); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new InvalidOperationException("Expected rejection."); }
    }
}
