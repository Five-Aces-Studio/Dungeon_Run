using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lab-only representative mesh swaps. Materials, transforms and renderer state are untouched.</summary>
public static class DungeonRunPixelContentLab
{
    public const string Original = "Assets/Meshes/TrigonalAbyss/TrigonalAbyssStage.fbx";
    public const string Variants = "Assets/Meshes/TrigonalAbyss/PixelAware/";
    private static readonly string[] ChainChoices = { "Original", "Chain15", "Chain20", "Chain30", "ChainSparse20" };
    private static readonly string[] StaffChoices = { "Original", "Staff20", "Staff30" };
    private static readonly string[] FloorChoices = { "Original", "FloorSimple" };

    [Serializable]
    private sealed class ContentRecord
    {
        public string chain, staff, floor;
        public string renderer = "Accepted C480; sequence mode C0 means unchanged renderer, not original content";
        public MeshRecord[] meshes;
    }

    [Serializable]
    private sealed class MeshRecord
    {
        public string objectName, meshName, assetPath;
    }

    /// <summary>Configure only the new FBX imports to match the canonical coordinate contract.</summary>
    public static void ConfigureImports()
    {
        RequireLab();
        foreach (string name in ChainChoices.Concat(StaffChoices).Concat(FloorChoices).Distinct().Where(x => x != "Original"))
        {
            var importer = AssetImporter.GetAtPath(Variants + name + ".fbx") as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing generated FBX: " + name);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }
    }

    /// <summary>Save is deliberately explicit and left to the caller after visual acceptance.</summary>
    public static void Apply(string chain, string staff, string floor)
    {
        var replacements = Prepare(chain, staff, floor);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply pixel-aware representative content");
        foreach (var pair in replacements)
        {
            Undo.RecordObject(pair.Key, "Replace representative mesh");
            pair.Key.sharedMesh = pair.Value;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Dungeon Run/Pixel Content V1/Restore Original Meshes")]
    public static void RestoreOriginals() { Apply("Original", "Original", "Original"); }

    [MenuItem("Dungeon Run/Pixel Content V1/Apply Selected Pixel-Aware Meshes")]
    public static void ApplySelected() { Apply("ChainSparse20", "Staff20", "FloorSimple"); }

    /// <summary>Transient variants use the existing capture harness and restore even on capture failure.</summary>
    public static string Capture(string chain, string staff, string floor, string motion,
        float amplitude, int frames, string outputDirectory)
    {
        var replacements = Prepare(chain, staff, floor);
        var originals = replacements.ToDictionary(x => x.Key, x => x.Key.sharedMesh);
        try
        {
            foreach (var pair in replacements) pair.Key.sharedMesh = pair.Value;
            string manifest = DungeonRunPixelStabilityLab.CaptureSequence("C0", motion, amplitude, frames, outputDirectory);
            var content = new ContentRecord
            {
                chain = chain, staff = staff, floor = floor,
                meshes = replacements.OrderBy(x => x.Key.name).Select(x => new MeshRecord
                {
                    objectName = x.Key.name, meshName = x.Value.name,
                    assetPath = AssetDatabase.GetAssetPath(x.Value)
                }).ToArray()
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(manifest), "content-variants.json"),
                JsonUtility.ToJson(content, true));
            return manifest;
        }
        finally
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.sharedMesh = pair.Value;
        }
    }

    private static Dictionary<MeshFilter, Mesh> Prepare(string chain, string staff, string floor)
    {
        var root = RequireLab();
        if (!ChainChoices.Contains(chain) || !StaffChoices.Contains(staff) || !FloorChoices.Contains(floor))
            throw new ArgumentException("Unknown representative variant.");
        var replacements = new Dictionary<MeshFilter, Mesh>();
        AddGroup(root, replacements, chain, new[] { "ENV_Chains_0", "ENV_Chains_1" });
        AddGroup(root, replacements, staff, new[] { "CHR_Player" });
        AddGroup(root, replacements, floor, Enumerable.Range(0, 5).Select(x => "ENV_FloorTiles_" + x).ToArray());
        return replacements;
    }

    private static GameObject RequireLab()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || scene.isDirty ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require clean SceneVictorLab in stable Edit Mode.");
        return scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
    }

    private static void AddGroup(GameObject root, Dictionary<MeshFilter, Mesh> replacements,
        string variant, string[] groups)
    {
        string path = variant == "Original" ? Original : Variants + variant + ".fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new InvalidOperationException("Missing imported model: " + path);
        var sources = prefab.GetComponentsInChildren<MeshFilter>(true);
        var targets = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (string name in groups)
        {
            // Single-object FBX imports may rename the root to the filename; mesh identity is retained.
            var source = sources.Single(x => x.sharedMesh && x.sharedMesh.name == name);
            var target = targets.Single(x => x.name == name);
            var renderer = target.GetComponent<MeshRenderer>();
            if (!source.sharedMesh || !renderer || source.sharedMesh.subMeshCount != renderer.sharedMaterials.Length)
                throw new InvalidOperationException("Material/submesh contract mismatch for " + name);
            replacements.Add(target, source.sharedMesh);
        }
    }
}
