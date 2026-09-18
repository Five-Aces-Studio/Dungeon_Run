using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lab-only outline controls. No method saves scenes or assets.</summary>
public static class DungeonRunSelectiveOutlineLab
{
    public const string MaskShader = "DungeonRun/SH_DungeonRun_SelectiveOutlineMask";
    public const string CompositeShader = "DungeonRun/SH_DungeonRun_SelectiveOutlineComposite";

    private static Camera RequireCamera(bool clean)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || (clean && scene.isDirty) ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require SceneVictorLab in stable Edit Mode (clean for captures).");
        var root = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var camera = root.transform.Find("CombatCamera").GetComponent<Camera>();
        var pixel = camera.GetComponent<DungeonRunPixelRenderSettings>();
        if (!pixel || !pixel.isActiveAndEnabled || !pixel.PixelEnabled || pixel.VirtualResolution != new Vector2Int(480, 270))
            throw new InvalidOperationException("Require accepted C480.");
        return camera;
    }

    /// <summary>Explicit setup only. Parent integration creates the renderer feature and referenced materials separately.</summary>
    public static void ConfigureTargets()
    {
        var camera = RequireCamera(false);
        var renderers = camera.transform.root.GetComponentsInChildren<MeshRenderer>(true);
        var guardian = renderers.Single(x => x.name == "CHR_Enemy_01");
        var hound = renderers.Single(x => x.name == "CHR_Enemy_02");
        var sentinel = renderers.Single(x => x.name == "CHR_Enemy_03");
        var settings = camera.GetComponent<DungeonRunSelectiveOutlineSettings>();
        if (!settings) settings = Undo.AddComponent<DungeonRunSelectiveOutlineSettings>(camera.gameObject);
        Undo.RecordObject(settings, "Configure selective outline targets");
        settings.targets = new[]
        {
            new DungeonRunSelectiveOutlineSettings.Target { renderer = guardian, submesh = 0,
                restrictLocalX = true, maximumLocalX = .59738f, restrictLocalY = true, maximumLocalY = .86f },
            new DungeonRunSelectiveOutlineSettings.Target { renderer = hound, submesh = 1 },
            new DungeonRunSelectiveOutlineSettings.Target { renderer = sentinel, submesh = 0,
                restrictLocalY = true, maximumLocalY = .65f }
        };
        settings.candidate = DungeonRunSelectiveOutlineSettings.Candidate.O0;
        settings.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
    }

    public static void Apply(string candidate)
    {
        var camera = RequireCamera(false);
        var selected = Parse(candidate);
        var settings = camera.GetComponent<DungeonRunSelectiveOutlineSettings>();
        if (!settings) throw new InvalidOperationException("Configure outline targets first.");
        Undo.RecordObject(settings, "Apply selective outline " + candidate);
        settings.candidate = selected;
        settings.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
    }

    [MenuItem("Dungeon Run/Selective Outlines V1/O0 - No Outlines")]
    public static void ApplyO0() => Apply("O0");
    [MenuItem("Dungeon Run/Selective Outlines V1/O1 - Dark Cool One Pixel")]
    public static void ApplyO1() => Apply("O1");
    [MenuItem("Dungeon Run/Selective Outlines V1/O2 - Charcoal One Pixel")]
    public static void ApplyO2() => Apply("O2");

    private static DungeonRunSelectiveOutlineSettings.Candidate Parse(string candidate)
    {
        if (!new[] { "O0", "O1", "O2" }.Contains(candidate)) throw new ArgumentException("Expected O0, O1 or O2.");
        return (DungeonRunSelectiveOutlineSettings.Candidate)Enum.Parse(typeof(DungeonRunSelectiveOutlineSettings.Candidate), candidate);
    }

    [Serializable] private sealed class CaptureRecord
    {
        public string candidate, characterName;
        public int enemyCount;
        public Color outlineColorSrgb;
        public string pipeline = "C1 + V2 + C480; selected visible mesh mask, inner cardinal one-cell edge before C480";
    }

    /// <summary>Transient sequence state is restored on success or failure. Directory follows the existing harness contract.</summary>
    public static string Capture(string candidate, int enemyCount, string motion, float amplitude,
        int frames, string outputDir, string characterName = "CHR_Player")
    {
        var camera = RequireCamera(true);
        var selected = Parse(candidate);
        if (enemyCount < 1 || enemyCount > 3) throw new ArgumentException("Enemy count must be 1..3.");
        var settings = camera.GetComponent<DungeonRunSelectiveOutlineSettings>();
        if (!settings || settings.targets == null || settings.targets.Length != 3)
            throw new InvalidOperationException("Configure three outline targets first.");
        var transforms = camera.transform.root.GetComponentsInChildren<Transform>(true);
        var enemies = Enumerable.Range(1, 3).Select(i => transforms.Single(t => t.name == "CHR_Enemy_0" + i).gameObject).ToArray();
        var active = enemies.Select(x => x.activeSelf).ToArray();
        var previous = settings.candidate;
        bool enabled = settings.enabled;
        try
        {
            settings.enabled = true;
            settings.candidate = selected;
            for (int i = 0; i < enemies.Length; i++) enemies[i].SetActive(i < enemyCount);
            string result = DungeonRunPixelStabilityLab.CaptureSequence("C0", motion, amplitude, frames, outputDir, characterName);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(result), "outline-state.json"), JsonUtility.ToJson(new CaptureRecord
            { candidate = candidate, enemyCount = enemyCount, characterName = characterName, outlineColorSrgb = settings.OutlineColor }, true));
            return result;
        }
        finally
        {
            settings.candidate = previous;
            settings.enabled = enabled;
            for (int i = 0; i < enemies.Length; i++) enemies[i].SetActive(active[i]);
        }
    }
}
