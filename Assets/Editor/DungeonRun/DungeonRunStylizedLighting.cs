using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reversible A/B controls for the validated combat-stage lab; both disable spatial pixel rendering.</summary>
public static class DungeonRunStylizedLighting
{
    public const string LabPath = "Assets/Scenes/SceneVictor/SceneVictorLab.unity";
    public const string BaselineFolder = "Assets/Materials/TrigonalAbyss";
    public const string StylizedFolder = BaselineFolder + "/StylizedV1";
    public const string ShaderName = "DungeonRun/SH_DungeonRun_StylizedLit";
    private static readonly string[] Families =
    {
        "StoneDark", "StoneWorn", "StoneFloor", "StoneBackground", "MetalDark",
        "ArcaneWarm", "ArcaneCool", "ArcaneDistant", "TravelerCloth", "TravelerArmor",
        "EnemyBone", "EnemyClay", "EnemyJade"
    };

    private static Transform RequireStage()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != LabPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open SceneVictorLab in Edit Mode before using material A/B controls.");
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "TrigonalAbyss_Prototype") continue;
            var stage = root.transform.Find("TrigonalAbyssStage");
            if (stage != null) return stage;
        }
        throw new InvalidOperationException("Validated TrigonalAbyssStage root was not found; no changes made.");
    }

    private static Material Baseline(string family)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>($"{BaselineFolder}/{family}.mat");
        if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException($"Missing or unexpected URP/Lit baseline: {family}.");
        return material;
    }

    private static string VariantPath(string family) => $"{StylizedFolder}/{family}_StylizedV1.mat";

    [MenuItem("Dungeon Run/Stylized Lighting V1/Initialize Missing Materials")]
    public static void Initialize()
    {
        RequireStage();
        var shader = Shader.Find(ShaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Stylized shader is missing or has compiler errors.");
        // Validate every source before creating assets. Existing variants retain artist tuning.
        foreach (string family in Families) Baseline(family);
        if (!AssetDatabase.IsValidFolder(StylizedFolder))
            AssetDatabase.CreateFolder(BaselineFolder, "StylizedV1");
        int created = 0;
        foreach (string family in Families)
        {
            string path = VariantPath(family);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) continue;
            var source = Baseline(family);
            var material = new Material(shader) { name = family + "_StylizedV1" };
            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            material.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            material.SetFloat("_SpecularStrength", source.GetFloat("_Metallic") > 0.2f ? 0.9f : 0.05f);
            material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
            material.globalIlluminationFlags = source.globalIlluminationFlags;
            if (source.IsKeywordEnabled("_EMISSION")) material.EnableKeyword("_EMISSION");
            material.enableInstancing = source.enableInstancing;
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
            created++;
        }
        Debug.Log($"Stylized Lighting V1: created {created} missing materials. Scene unchanged; existing tuning preserved.");
    }

    [MenuItem("Dungeon Run/Stylized Lighting V1/A - Apply Baseline")]
    public static void ApplyBaseline() => Apply(false);

    [MenuItem("Dungeon Run/Stylized Lighting V1/B - Apply Stylized")]
    public static void ApplyStylized() => Apply(true);

    private static void Apply(bool stylized)
    {
        Transform stage = RequireStage();
        var map = new Dictionary<Material, Material>();
        foreach (string family in Families)
        {
            Material baseline = Baseline(family);
            var variant = AssetDatabase.LoadAssetAtPath<Material>(VariantPath(family));
            if (variant == null || variant.shader == null || variant.shader.name != ShaderName)
                throw new InvalidOperationException($"Missing or unexpected stylized variant: {family}. Run Initialize first.");
            Material target = stylized ? variant : baseline;
            map.Add(baseline, target);
            map.Add(variant, target);
        }
        // Character Value V1 materials resolve to their accepted family, so A/B/C reproduce V0 characters.
        foreach (var (material, family) in DungeonRunCharacterValueLab.VariantMaterials())
            map.Add(material, map[Baseline(family)]);
        var pending = new List<(Renderer renderer, Material[] materials)>();
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material source = materials[slot];
                if (source == null || !map.TryGetValue(source, out var target))
                    throw new InvalidOperationException($"Unknown material on {renderer.name}, slot {slot}. A/B aborted without changing renderers.");
                changed |= source != target;
                materials[slot] = target;
            }
            if (changed) pending.Add((renderer, materials));
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(stylized ? "Apply Stylized Lighting V1" : "Restore URP Baseline");
        foreach (var item in pending)
        {
            Undo.RecordObject(item.renderer, "Switch combat-stage materials");
            item.renderer.sharedMaterials = item.materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(item.renderer);
            EditorUtility.SetDirty(item.renderer);
        }
        DungeonRunPixelRendering.DisableForBaselineOrStylized(stage.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        if (pending.Count > 0) EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log($"Stylized Lighting V1: {(stylized ? "B" : "A")} applied to {pending.Count} renderers. Scene not automatically saved.");
    }
}
