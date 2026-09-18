using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reversible palette controls and transient captures; never saves scenes or assets.</summary>
public static class DungeonRunPaletteLab
{
    public const string ShaderName = "DungeonRun/SH_DungeonRun_Palette";

    private static Camera RequireCamera(bool clean)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || (clean && scene.isDirty) ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require SceneVictorLab in stable Edit Mode (clean for captures).");
        var root = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var camera = root.transform.Find("CombatCamera").GetComponent<Camera>();
        var pixel = camera.GetComponent<DungeonRunPixelRenderSettings>();
        var outline = camera.GetComponent<DungeonRunSelectiveOutlineSettings>();
        if (!pixel || !pixel.isActiveAndEnabled || !pixel.PixelEnabled || pixel.VirtualResolution != new Vector2Int(480, 270) ||
            !outline || outline.candidate != DungeonRunSelectiveOutlineSettings.Candidate.O0)
            throw new InvalidOperationException("Require accepted C480 and O0; palette controls never change outlines.");
        return camera;
    }

    /// <summary>Adds camera opt-in settings; renderer feature/material integration is intentionally separate.</summary>
    [MenuItem("Dungeon Run/Palette V1/Configure Camera")]
    public static void Configure()
    {
        var camera = RequireCamera(false);
        var settings = camera.GetComponent<DungeonRunPaletteSettings>();
        if (!settings) settings = Undo.AddComponent<DungeonRunPaletteSettings>(camera.gameObject);
        Undo.RecordObject(settings, "Configure palette camera");
        settings.candidate = DungeonRunPaletteSettings.Candidate.P0;
        settings.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
    }

    private static DungeonRunPaletteSettings.Candidate Parse(string candidate)
    {
        if (!new[] { "P0", "P1", "P2" }.Contains(candidate)) throw new ArgumentException("Expected P0, P1 or P2.");
        return (DungeonRunPaletteSettings.Candidate)Enum.Parse(typeof(DungeonRunPaletteSettings.Candidate), candidate);
    }

    public static void Apply(string candidate)
    {
        var camera = RequireCamera(false);
        var selected = Parse(candidate);
        var settings = camera.GetComponent<DungeonRunPaletteSettings>();
        if (!settings) throw new InvalidOperationException("Configure palette camera first.");
        Undo.RecordObject(settings, "Apply palette " + candidate);
        settings.candidate = selected;
        settings.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
    }

    [MenuItem("Dungeon Run/Palette V1/P0 - Accepted Baseline")]
    public static void ApplyP0() => Apply("P0");
    [MenuItem("Dungeon Run/Palette V1/P1 - Subtle Grouping")]
    public static void ApplyP1() => Apply("P1");
    [MenuItem("Dungeon Run/Palette V1/P2 - Stronger Grouping")]
    public static void ApplyP2() => Apply("P2");

    [Serializable] private sealed class CaptureRecord
    {
        public string candidate, characterName, settingsJson;
        public int enemyCount;
        public string pipeline = "O0 + V2 + C1 + Stylized Lighting + C480 + palette + native UI later";
    }

    public static string Capture(string candidate, int enemyCount, string motion, float amplitude,
        int frames, string outputDir, string characterName = "CHR_Player")
    {
        var camera = RequireCamera(true);
        var selected = Parse(candidate);
        if (enemyCount < 1 || enemyCount > 3) throw new ArgumentException("Enemy count must be 1..3.");
        var settings = camera.GetComponent<DungeonRunPaletteSettings>();
        if (!settings) throw new InvalidOperationException("Configure palette camera first.");
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
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(result), "palette-state.json"), JsonUtility.ToJson(new CaptureRecord
            { candidate = candidate, enemyCount = enemyCount, characterName = characterName,
                settingsJson = EditorJsonUtility.ToJson(settings) }, true));
            return result;
        }
        finally
        {
            settings.candidate = previous;
            settings.enabled = enabled;
            for (int i = 0; i < enemies.Length; i++) enemies[i].SetActive(active[i]);
            // Canonical lab camera uses automatic aspect; don't retain the harness's temporary explicit override.
            camera.ResetAspect();
        }
    }
}
