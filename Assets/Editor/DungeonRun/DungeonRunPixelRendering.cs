using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Lab-only setup and C controls; runtime isolation uses camera opt-in, not scene names.</summary>
public static class DungeonRunPixelRendering
{
    public const string ShaderName = "DungeonRun/SH_DungeonRun_PixelRender";

    private static Camera RequireCamera()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open SceneVictorLab in Edit Mode before using pixel controls.");
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "TrigonalAbyss_Prototype") continue;
            var camera = root.transform.Find("CombatCamera");
            if (camera != null && camera.TryGetComponent<Camera>(out var result)) return result;
        }
        throw new InvalidOperationException("Validated CombatCamera was not found; no changes made.");
    }

    private static void ValidateRenderer(int rendererIndex)
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) throw new InvalidOperationException("The active pipeline must be URP.");
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        int defaultIndex = serialized.FindProperty("m_DefaultRendererIndex").intValue;
        if (rendererIndex < 0 || rendererIndex >= renderers.arraySize || rendererIndex == defaultIndex)
            throw new InvalidOperationException("Pixel controls require an explicit non-default lab renderer.");
        var data = renderers.GetArrayElementAtIndex(rendererIndex).objectReferenceValue as ScriptableRendererData;
        if (data != null)
            foreach (var feature in data.rendererFeatures)
                if (feature is DungeonRunPixelRenderFeature pixel && pixel.isActive && pixel.material != null &&
                    pixel.material.shader != null && pixel.material.shader.name == ShaderName &&
                    !ShaderUtil.ShaderHasError(pixel.material.shader)) return;
        throw new InvalidOperationException("Lab renderer needs an active pixel feature with its compiled material.");
    }

    /// <summary>Call after creating/registering the dedicated renderer. Does not create assets or save the scene.</summary>
    public static void ConfigureCamera(int rendererIndex)
    {
        var camera = RequireCamera();
        ValidateRenderer(rendererIndex);
        if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            throw new InvalidOperationException("CombatCamera must already have URP camera data.");
        Undo.RecordObject(data, "Assign isolated lab renderer");
        data.SetRenderer(rendererIndex);
        if (!camera.TryGetComponent<DungeonRunPixelRenderSettings>(out _))
            Undo.AddComponent<DungeonRunPixelRenderSettings>(camera.gameObject);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
    }

    // Called inside the existing A/B Undo group, after material prevalidation.
    public static void DisableForBaselineOrStylized(Scene scene)
    {
        if (scene.path != DungeonRunStylizedLighting.LabPath)
            throw new InvalidOperationException("Pixel A/B changes are restricted to SceneVictorLab.");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var settings in root.GetComponentsInChildren<DungeonRunPixelRenderSettings>(true))
            {
                if (!settings.PixelEnabled) continue;
                Undo.RecordObject(settings, "Disable spatial pixel rendering");
                settings.PixelEnabled = false;
                EditorUtility.SetDirty(settings);
                EditorSceneManager.MarkSceneDirty(scene);
            }
    }

    [MenuItem("Dungeon Run/Stylized Lighting V1/C - Apply Stylized + Pixel 480x270")]
    public static void ApplyPixel480() => ApplyPixel(480, 270);

    [MenuItem("Dungeon Run/Stylized Lighting V1/C - Compare Pixel 320x180")]
    public static void ApplyPixel320() => ApplyPixel(320, 180);

    [MenuItem("Dungeon Run/Stylized Lighting V1/C - Compare Pixel 640x360")]
    public static void ApplyPixel640() => ApplyPixel(640, 360);

    public static void ApplyPixel(int width, int height)
    {
        var camera = RequireCamera();
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException("Virtual resolution must be positive.");
        if (!camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var settings) ||
            !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            throw new InvalidOperationException("Configure the isolated pixel camera before applying C.");
        ValidateRenderer(new SerializedObject(data).FindProperty("m_RendererIndex").intValue);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        DungeonRunStylizedLighting.ApplyStylized();
        Undo.RecordObject(settings, "Enable spatial pixel rendering");
        settings.enabled = true;
        settings.VirtualResolution = new Vector2Int(width, height);
        settings.PixelEnabled = true;
        EditorUtility.SetDirty(settings);
        Undo.CollapseUndoOperations(group);
        Undo.SetCurrentGroupName("Apply Stylized + Pixel Rendering");
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log($"Pixel Rendering V1: C applied at {width}x{height}. Scene not automatically saved.");
    }
}
