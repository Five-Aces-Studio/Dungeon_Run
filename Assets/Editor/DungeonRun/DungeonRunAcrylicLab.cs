using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonRun.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Acrylic + Subtle Pixel V3 installer, preset apply and capture harness for SceneVictorLab. Every asset create is
/// idempotent (existing tuned assets are never overwritten); every scene mutation is Undo-grouped and never saves
/// the scene. Guards: active scene is SceneVictorLab, not playing, not compiling.
/// </summary>
public static class DungeonRunAcrylicLab
{
    public const string LabPath = "Assets/Scenes/SceneVictor/SceneVictorLab.unity";
    private const string BrushTexturePath = "Assets/Art/Rendering/AcrylicV3/T_DungeonRun_Brush.png";
    private const string PaintStrokeTexturePath = "Assets/Art/Rendering/AcrylicV3/T_DungeonRun_PaintStrokes.png";
    private const string SnapshotPath = "Assets/Settings/AcrylicV3/DungeonRun_AcrylicV3_LegacySnapshot.asset";
    private const string MaterialFolder = "Assets/Materials/TrigonalAbyss/AcrylicV3";
    private const string RenderingMaterialPath = MaterialFolder + "/Rendering/MAT_DungeonRun_WorldStyle.mat";
    private const string MeshFolder = "Assets/Meshes/TrigonalAbyss/AcrylicV3";
    private const string FlameCardMeshPath = MeshFolder + "/SM_FlameCard.asset";
    // Milestone-1 faceted flame mesh: its scene renderers are replaced by flame cards; the asset stays on disk.
    private const string LegacyFlameMeshName = "SM_TrigonalFlame";
    private const string FlameShaderName = "DungeonRun/SH_DungeonRun_Flame";
    private const int PaintSchemaVersion = 2;
    private const float CaptureFlameTime = 1.25f;
    private const string ProfileFolder = "Assets/Settings/AcrylicV3";
    private const string GlowProfilePath = ProfileFolder + "/DungeonRun_AcrylicV3_Glow.asset";
    private const string CurrentPixelProfilePath = ProfileFolder + "/DungeonRun_World_CurrentPixel.asset";
    private const string TargetProfilePath = ProfileFolder + "/DungeonRun_World_AcrylicPixel_Target.asset";
    private const string SoftProfilePath = ProfileFolder + "/DungeonRun_World_AcrylicSoft.asset";
    private const string RendererDataPath = "Assets/Settings/PC_LabPixel_Renderer.asset";
    private const string WorldStyleShaderName = "DungeonRun/SH_DungeonRun_WorldStyle";
    private const string RigRootName = "AcrylicV3_LabRig";
    private const string CaptureRoot = "Captures/AcrylicV3";

    // ---------------------------------------------------------------------------------------------- Guards/helpers

    private static void RequireEditableScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != LabPath || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require SceneVictorLab in stable Edit Mode.");
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        for (var parent = t.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }

    private static Transform ResolvePath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        Transform current = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == parts[0]);
        for (int i = 1; current != null && i < parts.Length; i++) current = current.Find(parts[i]);
        return current;
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        int slash = folder.LastIndexOf('/');
        string parent = slash >= 0 ? folder.Substring(0, slash) : "Assets";
        string name = slash >= 0 ? folder.Substring(slash + 1) : folder;
        if (parent != "Assets") EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Transform RequireRoot(Scene scene)
    {
        var root = scene.GetRootGameObjects().SingleOrDefault(x => x.name == "TrigonalAbyss_Prototype");
        if (root == null) throw new InvalidOperationException("TrigonalAbyss_Prototype root was not found.");
        return root.transform;
    }

    private static readonly string[] ExcludedSnapshotRootNames = { "Preserved_PreviousLab", RigRootName };

    // True when this transform or any ancestor is one of the roots the snapshot/apply flow must never touch.
    private static bool IsUnderExcludedRoot(Transform t)
    {
        for (var current = t; current != null; current = current.parent)
            if (Array.IndexOf(ExcludedSnapshotRootNames, current.name) >= 0)
                return true;
        return false;
    }

    private static Material SourceAsset(string sourceName)
    {
        string path = sourceName.StartsWith("V2_", StringComparison.Ordinal)
            ? $"{DungeonRunCharacterValueLab.VariantFolder}/{sourceName}.mat"
            : $"{DungeonRunStylizedLighting.StylizedFolder}/{sourceName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException($"Missing source material: {sourceName} at {path}.");
        return material;
    }

    // ------------------------------------------------------------------------------------------------------ Install

    [MenuItem("Dungeon Run/Acrylic V3/Install or Update Assets")]
    public static void Install()
    {
        RequireEditableScene();
        var scene = SceneManager.GetActiveScene();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Acrylic V3: Install or Update Assets");
        try
        {
            InstallBrushTexture();
            InstallPaintStrokeTexture();
            InstallSnapshot(scene);
            InstallMaterials();
            UpgradeMaterialsToPaintV2();
            var flameAssets = InstallFlameAssets();
            InstallRenderingFeature();
            var (currentPixel, target, soft) = InstallProfiles();
            UpgradeProfilesToV2(currentPixel, target, soft);
            var glowProfile = InstallGlowProfile();
            var rig = InstallSceneObjects(scene, glowProfile);
            UpgradeRigTorches(scene, rig, target, flameAssets);
            InstallCameraSettings(scene, currentPixel);
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Acrylic V3: install/update complete. Scene changes are staged via Undo, not saved.");
    }

    private static void InstallBrushTexture()
    {
        EnsureFolder("Assets/Art/Rendering/AcrylicV3");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(BrushTexturePath) != null) return;
        byte[] rgba = BrushNoiseBaker.BakeRgba32(512, 20260921);
        var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true, true) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixelData(rgba, 0);
        texture.Apply(true, false);
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        File.WriteAllBytes(BrushTexturePath, png);
        AssetDatabase.ImportAsset(BrushTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(BrushTexturePath);
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 2;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    // Paint stroke map (linear RGBA: dab value, dab hue id, bristle, blotch). Uncompressed: the hue-id channel is
    // compared against thresholds, so block compression would smear dab boundaries and ids.
    private static void InstallPaintStrokeTexture()
    {
        EnsureFolder("Assets/Art/Rendering/AcrylicV3");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(PaintStrokeTexturePath) != null) return;
        const int size = 512;
        byte[] rgba = PaintStrokeBaker.BakeRgba32(size, 20260922);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
        byte[] png;
        try
        {
            texture.SetPixelData(rgba, 0);
            texture.Apply(false, false);
            png = texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
        File.WriteAllBytes(PaintStrokeTexturePath, png);
        AssetDatabase.ImportAsset(PaintStrokeTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaintStrokeTexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false; // alpha is blotch data, not coverage: no colour dilation
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8; // the combat camera sees the floor at a grazing angle: keep dabs from smearing
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void InstallSnapshot(Scene scene)
    {
        if (AssetDatabase.LoadAssetAtPath<DungeonRunAcrylicLegacySnapshot>(SnapshotPath) != null) return;

        var root = RequireRoot(scene);
        if (root.Find(RigRootName) != null)
            throw new InvalidOperationException("Rig root already present; scene is not in legacy state. Refusing to snapshot.");
        var cameraTransform = root.Find("CombatCamera");
        if (cameraTransform == null || !cameraTransform.TryGetComponent<Camera>(out var camera))
            throw new InvalidOperationException("CombatCamera was not found.");
        var pixel = cameraTransform.GetComponent<DungeonRunPixelRenderSettings>();
        if (pixel == null || !pixel.isActiveAndEnabled || !pixel.PixelEnabled)
            throw new InvalidOperationException("Scene is not in legacy pixel state; refusing to snapshot.");

        var rendererEntries = new List<DungeonRunAcrylicLegacySnapshot.RendererEntry>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnderExcludedRoot(renderer.transform)) continue;
            if (renderer.sharedMaterials.Any(m => m != null && AcrylicMaterialMap.IsAcrylic(m.name)))
                throw new InvalidOperationException($"{renderer.name} already has an AcrylicV3 material; scene is not in legacy state.");
            if (!renderer.sharedMaterials.Any(m => m != null && AcrylicMaterialMap.SourceNames.Contains(m.name))) continue;
            rendererEntries.Add(new DungeonRunAcrylicLegacySnapshot.RendererEntry
            {
                hierarchyPath = HierarchyPath(renderer.transform),
                sharedMaterials = (Material[])renderer.sharedMaterials.Clone()
            });
        }

        var lightEntries = root.GetComponentsInChildren<Light>(true).Select(light =>
            new DungeonRunAcrylicLegacySnapshot.LightEntry
            {
                hierarchyPath = HierarchyPath(light.transform), enabled = light.enabled, color = light.color,
                intensity = light.intensity, range = light.range, shadowStrength = light.shadowStrength,
                shadows = light.shadows
            }).ToArray();

        var snapshot = ScriptableObject.CreateInstance<DungeonRunAcrylicLegacySnapshot>();
        snapshot.renderers = rendererEntries.ToArray();
        snapshot.lights = lightEntries;
        snapshot.fog = new DungeonRunAcrylicLegacySnapshot.FogState
        {
            enabled = RenderSettings.fog, mode = RenderSettings.fogMode, color = RenderSettings.fogColor,
            start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance, density = RenderSettings.fogDensity
        };
        snapshot.ambient = new DungeonRunAcrylicLegacySnapshot.AmbientState
        {
            mode = RenderSettings.ambientMode, sky = RenderSettings.ambientSkyColor,
            equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
            intensity = RenderSettings.ambientIntensity
        };
        snapshot.cameraRenderPostProcessing = camera.GetUniversalAdditionalCameraData().renderPostProcessing;
        snapshot.pixelEnabled = pixel.PixelEnabled;
        snapshot.virtualResolution = pixel.VirtualResolution;

        EnsureFolder(ProfileFolder);
        AssetDatabase.CreateAsset(snapshot, SnapshotPath);
    }

    private static void InstallMaterials()
    {
        EnsureFolder(MaterialFolder);
        var brush = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushTexturePath);
        var shader = Shader.Find(DungeonRunStylizedLighting.ShaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Stylized shader is missing or has compiler errors.");
        foreach (var acrylicName in AcrylicMaterialMap.AllAcrylicNames)
        {
            string path = $"{MaterialFolder}/{acrylicName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue; // never overwrite existing tuning
            string sourceName = AcrylicMaterialMap.Inverse(acrylicName);
            var source = SourceAsset(sourceName);
            var material = new Material(source) { name = acrylicName };
            material.EnableKeyword("_DR_ACRYLIC");
            material.SetFloat("_DRAcrylic", 1f);
            material.SetTexture("_BrushMap", brush);
            var defaults = AcrylicFamilyDefaults.Get(acrylicName);
            material.SetFloat("_BrushMapping", (float)defaults.BrushMapping);
            material.SetFloat("_BrushStrength", defaults.BrushStrength);
            material.SetFloat("_BrushScale", defaults.BrushScale);
            material.SetFloat("_LightWrap", defaults.LightWrap);
            material.SetFloat("_ShadowBreakup", defaults.ShadowBreakup);
            material.SetFloat("_RimStrength", defaults.RimStrength);
            Vector4 bandLevels = material.GetVector("_BandLevels");
            bandLevels.x = Mathf.Max(bandLevels.x, defaults.MinBandLevel0);
            material.SetVector("_BandLevels", bandLevels);
            material.SetFloat("_AmbientStrength", material.GetFloat("_AmbientStrength") * defaults.AmbientScale);
            material.SetColor("_EmissionColor", material.GetColor("_EmissionColor") * defaults.EmissionScale);
            AssetDatabase.CreateAsset(material, path);
        }
    }

    // Paint V2: stroke map + per-family colour touches. Runs once per material (_PaintVersion marker); every other
    // tuned property is left as is.
    private static void UpgradeMaterialsToPaintV2()
    {
        var strokes = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintStrokeTexturePath);
        if (strokes == null) throw new InvalidOperationException("Paint stroke texture is missing: " + PaintStrokeTexturePath);
        foreach (var acrylicName in AcrylicMaterialMap.AllAcrylicNames)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{acrylicName}.mat");
            if (material == null) continue;
            if (!material.HasProperty("_PaintVersion") || !material.HasProperty("_TouchAmount"))
                throw new InvalidOperationException($"{acrylicName}: stylized shader lacks the paint V2 properties (recompile shaders first).");
            if (material.GetFloat("_PaintVersion") >= PaintSchemaVersion) continue;
            var palette = AcrylicTouchPalettes.Get(acrylicName);
            material.SetTexture("_BrushMap", strokes);
            // Touch colours are authored sRGB, like any material colour: SetColor linearizes them.
            material.SetColor("_TouchColorA", new Color(palette.ARed, palette.AGreen, palette.ABlue, 1f));
            material.SetColor("_TouchColorB", new Color(palette.BRed, palette.BGreen, palette.BBlue, 1f));
            material.SetVector("_TouchAmount", new Vector4(palette.AmountA, palette.AmountB, palette.Strength, 0f));
            material.SetFloat("_PaintVersion", PaintSchemaVersion);
            EditorUtility.SetDirty(material);
        }
    }

    private sealed class FlameAssets
    {
        public Mesh card;
        public Material flameWarm, flameCold, haloWarm, haloCold;

        public Material Flame(DungeonRunFlameLight.FlameKind kind) =>
            kind == DungeonRunFlameLight.FlameKind.Cold ? flameCold : flameWarm;

        public Material Halo(DungeonRunFlameLight.FlameKind kind) =>
            kind == DungeonRunFlameLight.FlameKind.Cold ? haloCold : haloWarm;
    }

    private static FlameAssets InstallFlameAssets()
    {
        EnsureFolder(MeshFolder);
        EnsureFolder(MaterialFolder);
        var shader = Shader.Find(FlameShaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Flame shader is missing or has compiler errors.");

        var card = AssetDatabase.LoadAssetAtPath<Mesh>(FlameCardMeshPath);
        if (card == null)
        {
            // Unit card: x in [-0.5, 0.5], y in [0, 1], uv = (x + 0.5, y). The flame shader billboards it around the
            // pivot, so the bounds are padded to cover every camera-facing orientation plus the centred halo.
            card = new Mesh { name = "SM_FlameCard" };
            card.SetVertices(new[]
            {
                new Vector3(-.5f, 0f, 0f), new Vector3(.5f, 0f, 0f), new Vector3(-.5f, 1f, 0f), new Vector3(.5f, 1f, 0f)
            });
            card.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            card.SetNormals(new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            card.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            card.bounds = new Bounds(new Vector3(0f, .3f, 0f), new Vector3(1.6f, 1.8f, 1.6f));
            AssetDatabase.CreateAsset(card, FlameCardMeshPath);
        }

        var warmCore = new Color(4f, 3.2f, 1.6f, 1f);
        var warmMid = new Color(3.2f, 1.3f, .3f, 1f);
        var warmOuter = new Color(1.6f, .35f, .06f, 1f);
        var coldCore = new Color(2.4f, 3.8f, 4.2f, 1f);
        var coldMid = new Color(.6f, 1.9f, 3.4f, 1f);
        var coldOuter = new Color(.35f, .95f, 1.6f, 1f);
        return new FlameAssets
        {
            card = card,
            flameWarm = InstallFlameMaterial("FlameWarm_AcrylicV3", shader, warmCore, warmMid, warmOuter, 1f, false),
            flameCold = InstallFlameMaterial("FlameCold_AcrylicV3", shader, coldCore, coldMid, coldOuter, 1f, false),
            haloWarm = InstallFlameMaterial("FlameHaloWarm_AcrylicV3", shader, warmCore, warmMid,
                new Color(1f, .45f, .12f, 1f), .8f, true),
            haloCold = InstallFlameMaterial("FlameHaloCold_AcrylicV3", shader, coldCore, coldMid,
                new Color(.25f, .7f, 1f, 1f), .7f, true)
        };
    }

    // Converged milestone-2 flame look (visual convergence rounds 1-5); applied only when a flame material is created.
    private static void ApplyFlameLookDefaults(Material material, string materialName)
    {
        if (materialName == "FlameWarm_AcrylicV3" || materialName == "FlameCold_AcrylicV3")
        {
            material.SetFloat("_Distortion", .3f);
            material.SetFloat("_Wisps", .6f);
        }
        if (materialName == "FlameCold_AcrylicV3")
        {
            material.SetFloat("_EdgeSoftness", .11f);
            material.SetFloat("_Opacity", .85f);
        }
    }

    private static Material InstallFlameMaterial(string materialName, Shader shader, Color core, Color mid, Color outer,
        float intensity, bool halo)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing; // never overwrite existing tuning
        // HDR colours go through SetColor like the shader's [HDR] Property defaults, so both read identically.
        var material = new Material(shader) { name = materialName };
        material.SetColor("_CoreColor", core);
        material.SetColor("_MidColor", mid);
        material.SetColor("_OuterColor", outer);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_HaloMode", halo ? 1f : 0f); // uniform branch; the shader declares no halo keyword
        ApplyFlameLookDefaults(material, materialName);
        // Halo (soft additive glow) draws first, then the flame card over it.
        material.renderQueue = halo ? 3000 : 3001;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void InstallRenderingFeature()
    {
        EnsureFolder($"{MaterialFolder}/Rendering");
        var shader = Shader.Find(WorldStyleShaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("World Style shader is missing or has compiler errors.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(RenderingMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_DungeonRun_WorldStyle" };
            AssetDatabase.CreateAsset(material, RenderingMaterialPath);
        }

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
        if (rendererData == null) throw new InvalidOperationException("PC_LabPixel_Renderer.asset was not found.");
        if (rendererData.rendererFeatures.OfType<DungeonRunWorldStyleFeature>().Any()) return;

        var feature = ScriptableObject.CreateInstance<DungeonRunWorldStyleFeature>();
        feature.name = "DungeonRunWorldStyleFeature";
        feature.material = material;
        rendererData.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        EditorUtility.SetDirty(rendererData);
        // Invalidates URP's cached renderer instance; without it the new feature never runs until a domain reload.
        rendererData.SetDirty();
        AssetDatabase.SaveAssets();
    }

    private static (DungeonRunWorldStyleProfile currentPixel, DungeonRunWorldStyleProfile target, DungeonRunWorldStyleProfile soft) InstallProfiles()
    {
        EnsureFolder(ProfileFolder);
        var currentPixel = LoadOrCreateProfile(CurrentPixelProfilePath,
            p => p.sceneLook = DungeonRunWorldStyleProfile.SceneLook.LegacyPixel);
        var target = LoadOrCreateProfile(TargetProfilePath, _ => { }); // field defaults already match the Target spec
        var soft = LoadOrCreateProfile(SoftProfilePath, p =>
        {
            p.pixelInfluence = .10f;
            p.pixelScale = 1.25f;
            p.pixelEdgeStrength = .25f;
            p.cellFilter = .9f;
            p.paletteStrength = .30f;
            p.lightingSmoothness = 1f;
            p.quantizationStrength = .06f;
            p.colorLevels = 64;
            p.ditherStrength = .04f;
            p.distanceDesaturation = .4f;
        });
        return (currentPixel, target, soft);
    }

    private static DungeonRunWorldStyleProfile LoadOrCreateProfile(string path, Action<DungeonRunWorldStyleProfile> configure)
    {
        var existing = AssetDatabase.LoadAssetAtPath<DungeonRunWorldStyleProfile>(path);
        if (existing != null) return existing; // never overwrite an existing tuned preset
        var profile = ScriptableObject.CreateInstance<DungeonRunWorldStyleProfile>();
        configure(profile);
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    // Paint-filter / painted-flame milestone. Fields missing from older YAML already deserialize to their field
    // initializers (the Target spec), so Target and CurrentPixel only get the marker; Soft gets its gentler values.
    private static void UpgradeProfilesToV2(DungeonRunWorldStyleProfile currentPixel, DungeonRunWorldStyleProfile target,
        DungeonRunWorldStyleProfile soft)
    {
        UpgradeProfile(currentPixel, _ => { });
        UpgradeProfile(target, _ => { });
        UpgradeProfile(soft, p =>
        {
            p.paintStrength = .6f;
            p.paintRadius = 3f;
            p.focusStrength = .25f;
            p.rimStrength = .15f;
            p.poolSteps = 0;
            p.flameFlicker = .15f;
        });
    }

    private static void UpgradeProfile(DungeonRunWorldStyleProfile profile, Action<DungeonRunWorldStyleProfile> upgrade)
    {
        if (profile == null || profile.paintSchemaVersion >= PaintSchemaVersion) return;
        upgrade(profile);
        profile.paintSchemaVersion = PaintSchemaVersion;
        EditorUtility.SetDirty(profile);
    }

    private static VolumeProfile InstallGlowProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlowProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GlowProfilePath);
        }
        // Repairs in place (keeps the GUID the scene Volume references) when the Bloom sub-asset is missing.
        profile.components.RemoveAll(component => component == null);
        if (profile.TryGet<Bloom>(out _)) return profile;
        var bloom = profile.Add<Bloom>(true);
        bloom.name = "Bloom";
        bloom.threshold.value = 1.0f;
        bloom.intensity.value = 0.4f;
        bloom.scatter.value = 0.6f;
        bloom.downscale.value = BloomDownscaleMode.Half;
        bloom.highQualityFiltering.value = false;
        // VolumeProfile.Add only creates the component in memory; it must be stored as a sub-asset to persist.
        AssetDatabase.AddObjectToAsset(bloom, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static Transform InstallSceneObjects(Scene scene, VolumeProfile glowProfile)
    {
        var root = RequireRoot(scene);
        var existing = root.Find(RigRootName);
        if (existing != null) return existing; // idempotent: never overwrite an existing rig

        var rig = new GameObject(RigRootName);
        Undo.RegisterCreatedObjectUndo(rig, "Acrylic V3: create lab rig");
        Undo.SetTransformParent(rig.transform, root, "Acrylic V3: parent lab rig");
        rig.SetActive(false);

        var glowVolumeGO = new GameObject("GlowVolume");
        Undo.RegisterCreatedObjectUndo(glowVolumeGO, "Acrylic V3: create glow volume");
        Undo.SetTransformParent(glowVolumeGO.transform, rig.transform, "Acrylic V3: parent glow volume");
        var volume = glowVolumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = glowProfile;

        EditorSceneManager.MarkSceneDirty(scene);
        return rig.transform;
    }

    private static bool HasMaterialContaining(Renderer renderer, string token) =>
        renderer.sharedMaterials.Any(m => m != null && m.name.IndexOf(token, StringComparison.Ordinal) >= 0);

    // Painted torches on every arcane fixture (warm amber and cold blue): flame card + halo + flickering light.
    // Idempotent: creates what is missing, replaces milestone-1 faceted flames, repairs structural references and
    // never overwrites tuned transforms, lights or flicker values of existing torches.
    private static void UpgradeRigTorches(Scene scene, Transform rig, DungeonRunWorldStyleProfile targetProfile,
        FlameAssets flame)
    {
        if (rig == null) return;
        var root = RequireRoot(scene);
        var candidates = root.GetComponentsInChildren<Transform>(true);
        bool changed = false;
        for (int i = 0; i <= 2; i++)
        {
            string fixtureName = "ENV_ArcaneFixture_" + i;
            var fixture = candidates.FirstOrDefault(t => t.name == fixtureName && !IsUnderExcludedRoot(t));
            if (fixture == null || !fixture.TryGetComponent<Renderer>(out var fixtureRenderer)) continue;
            // Legacy (StylizedV1) and acrylic material names both carry the family name.
            DungeonRunFlameLight.FlameKind kind;
            if (HasMaterialContaining(fixtureRenderer, "ArcaneCool")) kind = DungeonRunFlameLight.FlameKind.Cold;
            else if (HasMaterialContaining(fixtureRenderer, "ArcaneWarm")) kind = DungeonRunFlameLight.FlameKind.Warm;
            else continue;
            bool warm = kind == DungeonRunFlameLight.FlameKind.Warm;

            string torchName = "Torch_" + i;
            var torch = rig.Find(torchName);
            if (torch == null)
            {
                Bounds bounds = fixtureRenderer.bounds;
                var torchGO = new GameObject(torchName);
                Undo.RegisterCreatedObjectUndo(torchGO, "Acrylic V3: create torch");
                Undo.SetTransformParent(torchGO.transform, rig, "Acrylic V3: parent torch");
                torchGO.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                torchGO.transform.rotation = Quaternion.identity;
                torch = torchGO.transform;
                changed = true;
            }

            var legacyFlames = torch.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.transform != torch && f.sharedMesh != null && f.sharedMesh.name == LegacyFlameMeshName)
                .Select(f => f.gameObject)
                .Distinct()
                .ToList();
            foreach (var legacyFlame in legacyFlames)
            {
                if (legacyFlame == null) continue; // already destroyed with a destroyed parent
                Undo.DestroyObjectImmediate(legacyFlame);
                changed = true;
            }

            var cardRenderer = EnsureFlameRenderer(torch, "FlameCard", flame.card, flame.Flame(kind),
                new Vector3(0f, -.18f, 0f), warm ? new Vector3(1.1f, 1.8f, 1.1f) : new Vector3(.95f, 1.6f, .95f), ref changed);
            var haloRenderer = EnsureFlameRenderer(torch, "FlameHalo", flame.card, flame.Halo(kind),
                new Vector3(0f, .45f, 0f), Vector3.one * 3.2f, ref changed);
            var light = EnsureTorchLight(torch, (warm ? "Warm_Torch_" : "Cool_Torch_") + i, warm, targetProfile, ref changed);

            var renderers = new Renderer[] { cardRenderer, haloRenderer };
            var flameLight = torch.GetComponent<DungeonRunFlameLight>();
            if (flameLight == null)
            {
                flameLight = Undo.AddComponent<DungeonRunFlameLight>(torch.gameObject);
                Undo.RecordObject(flameLight, "Acrylic V3: configure flame light");
                flameLight.kind = kind;
                flameLight.targetLight = light;
                flameLight.flameRenderers = renderers;
                flameLight.seed = 1013 * (i + 1) + 7;
                flameLight.baseLightLocalPosition = light.transform.localPosition;
                ConfigureFlameFromProfile(flameLight, targetProfile);
                changed = true;
            }
            else if (flameLight.kind != kind || flameLight.targetLight != light || flameLight.flameRenderers == null ||
                     !flameLight.flameRenderers.SequenceEqual(renderers))
            {
                // Structural references only; numeric tuning on an existing torch stays untouched.
                Undo.RecordObject(flameLight, "Acrylic V3: repair flame light");
                if (flameLight.targetLight != light) flameLight.baseLightLocalPosition = light.transform.localPosition;
                flameLight.kind = kind;
                flameLight.targetLight = light;
                flameLight.flameRenderers = renderers;
                changed = true;
            }
        }
        if (changed) EditorSceneManager.MarkSceneDirty(scene);
    }

    private static MeshRenderer EnsureFlameRenderer(Transform torch, string childName, Mesh mesh, Material material,
        Vector3 localPosition, Vector3 localScale, ref bool changed)
    {
        var child = torch.Find(childName);
        if (child == null)
        {
            var go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, "Acrylic V3: create " + childName);
            Undo.SetTransformParent(go.transform, torch, "Acrylic V3: parent " + childName);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            child = go.transform;
            changed = true;
        }

        if (!child.TryGetComponent<MeshFilter>(out var filter))
        {
            filter = Undo.AddComponent<MeshFilter>(child.gameObject);
            changed = true;
        }
        if (filter.sharedMesh == null)
        {
            Undo.RecordObject(filter, "Acrylic V3: assign flame card mesh");
            filter.sharedMesh = mesh;
            changed = true;
        }

        bool configure = false;
        if (!child.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer = Undo.AddComponent<MeshRenderer>(child.gameObject);
            configure = true;
        }
        if (configure || renderer.sharedMaterial == null)
        {
            Undo.RecordObject(renderer, "Acrylic V3: configure " + childName);
            renderer.sharedMaterial = material;
            // Unlit painted card: no shadows, probes or motion vectors needed.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            changed = true;
        }
        return renderer;
    }

    private static Light EnsureTorchLight(Transform torch, string lightName, bool warm,
        DungeonRunWorldStyleProfile profile, ref bool changed)
    {
        var lightTransform = torch.Find(lightName);
        if (lightTransform != null && lightTransform.TryGetComponent<Light>(out var existing)) return existing;

        Light light;
        if (lightTransform == null)
        {
            var lightGO = new GameObject(lightName);
            Undo.RegisterCreatedObjectUndo(lightGO, "Acrylic V3: create torch light");
            Undo.SetTransformParent(lightGO.transform, torch, "Acrylic V3: parent torch light");
            lightGO.transform.localPosition = new Vector3(0, .35f, -.8f);
            lightGO.transform.localRotation = Quaternion.identity;
            light = lightGO.AddComponent<Light>();
        }
        else
        {
            light = Undo.AddComponent<Light>(lightTransform.gameObject);
            Undo.RecordObject(light, "Acrylic V3: configure torch light");
        }
        light.type = LightType.Point;
        light.color = warm ? profile.torchColor : profile.coldTorchColor;
        light.intensity = warm ? profile.torchIntensity : profile.coldTorchIntensity;
        light.range = warm ? profile.torchRange : profile.coldTorchRange;
        light.shadows = LightShadows.None;
        changed = true;
        return light;
    }

    // Kind-specific profile values for one torch (Install for new torches, Apply for all of them).
    private static void ConfigureFlameFromProfile(DungeonRunFlameLight flameLight, DungeonRunWorldStyleProfile profile)
    {
        bool warm = flameLight.kind == DungeonRunFlameLight.FlameKind.Warm;
        flameLight.baseIntensity = warm ? profile.torchIntensity : profile.coldTorchIntensity;
        flameLight.baseRange = warm ? profile.torchRange : profile.coldTorchRange;
        flameLight.intensityFlicker = profile.flameFlicker;
        flameLight.flickerSpeed = profile.flameFlickerSpeed;
        flameLight.poolColor = warm ? profile.warmPoolColor : profile.coldPoolColor;
        flameLight.poolIntensity = warm ? profile.warmPoolIntensity : profile.coldPoolIntensity;
        flameLight.poolRadius = warm ? profile.warmPoolRadius : profile.coldPoolRadius;
    }

    private static void InstallCameraSettings(Scene scene, DungeonRunWorldStyleProfile currentPixelProfile)
    {
        var root = RequireRoot(scene);
        var cameraTransform = root.Find("CombatCamera");
        if (cameraTransform == null) throw new InvalidOperationException("CombatCamera was not found.");
        if (cameraTransform.TryGetComponent<DungeonRunWorldStyleSettings>(out var settings))
        {
            if (settings.profile == null)
            {
                Undo.RecordObject(settings, "Acrylic V3: assign World Style profile");
                settings.profile = currentPixelProfile;
                EditorSceneManager.MarkSceneDirty(scene);
            }
            return;
        }
        settings = Undo.AddComponent<DungeonRunWorldStyleSettings>(cameraTransform.gameObject);
        settings.profile = currentPixelProfile;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // -------------------------------------------------------------------------------------------------------- Apply

    [MenuItem("Dungeon Run/Acrylic V3/Apply CurrentPixel")]
    public static void ApplyCurrentPixel() => Apply(LoadProfile(CurrentPixelProfilePath));

    [MenuItem("Dungeon Run/Acrylic V3/Apply AcrylicPixel Target")]
    public static void ApplyAcrylicPixelTarget() => Apply(LoadProfile(TargetProfilePath));

    [MenuItem("Dungeon Run/Acrylic V3/Apply AcrylicSoft")]
    public static void ApplyAcrylicSoft() => Apply(LoadProfile(SoftProfilePath));

    private static DungeonRunWorldStyleProfile LoadProfile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonRunWorldStyleProfile>(path);
        if (profile == null) throw new InvalidOperationException($"Missing profile asset: {path}. Run Install first.");
        return profile;
    }

    public static string Apply(DungeonRunWorldStyleProfile profile)
    {
        RequireEditableScene();
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        var scene = SceneManager.GetActiveScene();
        var snapshot = AssetDatabase.LoadAssetAtPath<DungeonRunAcrylicLegacySnapshot>(SnapshotPath);
        if (snapshot == null) throw new InvalidOperationException("Missing legacy snapshot; run Install on a legacy scene first.");
        var root = RequireRoot(scene);
        var cameraTransform = root.Find("CombatCamera");
        if (cameraTransform == null || !cameraTransform.TryGetComponent<Camera>(out var camera))
            throw new InvalidOperationException("CombatCamera was not found.");
        if (!cameraTransform.TryGetComponent<DungeonRunWorldStyleSettings>(out var settings))
            throw new InvalidOperationException("Run Install first (missing DungeonRunWorldStyleSettings).");
        var pixel = cameraTransform.GetComponent<DungeonRunPixelRenderSettings>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        var rig = root.Find(RigRootName);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Acrylic V3: Apply " + profile.name);
        int changed = 0;
        try
        {
            if (profile.sceneLook == DungeonRunWorldStyleProfile.SceneLook.LegacyPixel)
            {
                changed += RestoreLegacy(snapshot, scene);
                if (rig != null)
                {
                    Undo.RecordObject(rig.gameObject, "Acrylic V3: deactivate rig");
                    rig.gameObject.SetActive(false);
                }
                Undo.RecordObject(pixel, "Acrylic V3: restore pixel settings");
                pixel.PixelEnabled = snapshot.pixelEnabled;
                pixel.VirtualResolution = snapshot.virtualResolution;
                Undo.RecordObject(cameraData, "Acrylic V3: restore post-processing flag");
                cameraData.renderPostProcessing = snapshot.cameraRenderPostProcessing;
            }
            else
            {
                changed += ApplyAcrylicMaterials(snapshot, scene);
                Undo.RecordObject(pixel, "Acrylic V3: disable legacy pixel");
                pixel.PixelEnabled = false;
                Undo.RecordObject(cameraData, "Acrylic V3: enable post-processing");
                cameraData.renderPostProcessing = true;
                if (rig != null)
                {
                    Undo.RecordObject(rig.gameObject, "Acrylic V3: activate rig");
                    rig.gameObject.SetActive(true);
                }
                ApplyTorchLights(rig, profile);
                ApplyFogAndAmbient(profile);
                ApplyKeyAndPoolLights(root, snapshot, profile);
            }
            Undo.RecordObject(settings, "Acrylic V3: assign profile");
            settings.profile = profile;
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
        string result = $"Acrylic V3: applied {profile.name} ({changed} renderers changed). Scene not automatically saved.";
        Debug.Log(result);
        return result;
    }

    private static int RestoreLegacy(DungeonRunAcrylicLegacySnapshot snapshot, Scene scene)
    {
        int changed = 0;
        foreach (var entry in snapshot.renderers)
        {
            var transform = ResolvePath(scene, entry.hierarchyPath);
            if (transform == null || !transform.TryGetComponent<Renderer>(out var renderer)) continue;
            if (renderer.sharedMaterials.SequenceEqual(entry.sharedMaterials)) continue;
            Undo.RecordObject(renderer, "Acrylic V3: restore legacy materials");
            renderer.sharedMaterials = (Material[])entry.sharedMaterials.Clone();
            changed++;
        }
        foreach (var entry in snapshot.lights)
        {
            var transform = ResolvePath(scene, entry.hierarchyPath);
            if (transform == null || !transform.TryGetComponent<Light>(out var light)) continue;
            Undo.RecordObject(light, "Acrylic V3: restore legacy light");
            light.enabled = entry.enabled;
            light.color = entry.color;
            light.intensity = entry.intensity;
            light.range = entry.range;
            light.shadowStrength = entry.shadowStrength;
            light.shadows = entry.shadows;
        }
        RenderSettings.fog = snapshot.fog.enabled;
        RenderSettings.fogMode = snapshot.fog.mode;
        RenderSettings.fogColor = snapshot.fog.color;
        RenderSettings.fogStartDistance = snapshot.fog.start;
        RenderSettings.fogEndDistance = snapshot.fog.end;
        RenderSettings.fogDensity = snapshot.fog.density;
        RenderSettings.ambientMode = snapshot.ambient.mode;
        RenderSettings.ambientSkyColor = snapshot.ambient.sky;
        RenderSettings.ambientEquatorColor = snapshot.ambient.equator;
        RenderSettings.ambientGroundColor = snapshot.ambient.ground;
        RenderSettings.ambientIntensity = snapshot.ambient.intensity;
        return changed;
    }

    private static int ApplyAcrylicMaterials(DungeonRunAcrylicLegacySnapshot snapshot, Scene scene)
    {
        int changed = 0;
        foreach (var entry in snapshot.renderers)
        {
            var transform = ResolvePath(scene, entry.hierarchyPath);
            if (transform == null || !transform.TryGetComponent<Renderer>(out var renderer)) continue;
            // Start from the snapshot's original slots every time, so repeated applies are stable/idempotent.
            var materials = (Material[])entry.sharedMaterials.Clone();
            bool slotChanged = false;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var source = materials[slot];
                if (source == null) continue;
                string acrylicName = AcrylicMaterialMap.Resolve(transform.name, source.name);
                if (acrylicName == null) continue;
                var acrylic = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{acrylicName}.mat");
                if (acrylic == null) throw new InvalidOperationException($"Missing AcrylicV3 material: {acrylicName}. Run Install first.");
                materials[slot] = acrylic;
                slotChanged = true;
            }
            if (!slotChanged || renderer.sharedMaterials.SequenceEqual(materials)) continue;
            Undo.RecordObject(renderer, "Acrylic V3: assign acrylic materials");
            renderer.sharedMaterials = materials;
            changed++;
        }
        return changed;
    }

    private static void ApplyTorchLights(Transform rig, DungeonRunWorldStyleProfile profile)
    {
        if (rig == null) return;
        foreach (var light in rig.GetComponentsInChildren<Light>(true))
        {
            bool warm = light.name.StartsWith("Warm_Torch_", StringComparison.Ordinal);
            if (!warm && !light.name.StartsWith("Cool_Torch_", StringComparison.Ordinal)) continue;
            Undo.RecordObject(light, "Acrylic V3: apply torch light");
            light.color = warm ? profile.torchColor : profile.coldTorchColor;
            light.intensity = warm ? profile.torchIntensity : profile.coldTorchIntensity;
            light.range = warm ? profile.torchRange : profile.coldTorchRange;
        }
        // Flame lights own their light's rest state (intensity, range, position) and their painted pool; RestoreBase
        // writes that rest state back so the scene never keeps a flickered value.
        foreach (var flameLight in rig.GetComponentsInChildren<DungeonRunFlameLight>(true))
        {
            Undo.RecordObject(flameLight, "Acrylic V3: apply flame light");
            ConfigureFlameFromProfile(flameLight, profile);
            if (flameLight.targetLight != null)
            {
                Undo.RecordObject(flameLight.targetLight, "Acrylic V3: rest torch light");
                Undo.RecordObject(flameLight.targetLight.transform, "Acrylic V3: rest torch light position");
            }
            flameLight.RestoreBase();
        }
    }

    private static void ApplyFogAndAmbient(DungeonRunWorldStyleProfile profile)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        // RenderSettings colours are authored (sRGB) values, like the profile's; Unity linearizes them itself.
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogStartDistance = profile.fogStart;
        RenderSettings.fogEndDistance = profile.fogEnd;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = profile.ambientSky;
        RenderSettings.ambientEquatorColor = profile.ambientEquator;
        RenderSettings.ambientGroundColor = profile.ambientGround;
    }

    private static void ApplyKeyAndPoolLights(Transform root, DungeonRunAcrylicLegacySnapshot snapshot, DungeonRunWorldStyleProfile profile)
    {
        var snapshotByPath = snapshot.lights.ToDictionary(e => e.hierarchyPath);
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            string path = HierarchyPath(light.transform);
            if (!snapshotByPath.TryGetValue(path, out var entry)) continue;
            if (light.type == LightType.Directional)
            {
                Undo.RecordObject(light, "Acrylic V3: key light");
                light.shadowStrength = profile.keyShadowStrength;
                light.intensity = entry.intensity * profile.keyIntensityScale;
            }
            else if (light.name == "Warm_PlayerPool" || light.name == "Warm_EnemyPool")
            {
                Undo.RecordObject(light, "Acrylic V3: combat pool");
                light.range = entry.range * profile.combatPoolRangeScale;
                light.intensity = entry.intensity * profile.combatPoolIntensityScale;
            }
            else if (light.name == "Cool_Vista")
            {
                Undo.RecordObject(light, "Acrylic V3: cool vista intensity");
                light.intensity = entry.intensity * profile.coolVistaIntensityScale;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------ Capture

    private static Camera RequireCamera()
    {
        RequireEditableScene();
        var scene = SceneManager.GetActiveScene();
        var root = RequireRoot(scene);
        var cameraTransform = root.Find("CombatCamera");
        if (cameraTransform == null || !cameraTransform.TryGetComponent<Camera>(out var camera))
            throw new InvalidOperationException("CombatCamera was not found.");
        return camera;
    }

    private static string PrepareCaptureDirectory(string label)
    {
        if (string.IsNullOrEmpty(label) || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Invalid capture label.");
        string captureRootFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CaptureRoot))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string directory = Path.GetFullPath(Path.Combine(captureRootFull, label));
        if (!directory.StartsWith(captureRootFull, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must be inside " + CaptureRoot + "/.");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new IOException("Refusing to overwrite an existing capture.");
        Directory.CreateDirectory(directory);
        return directory;
    }

    // Renders `camera` into a width x height sRGB target and reads it back. The caller owns (destroys) the result.
    private static Texture2D RenderReadback(Camera camera, int width, int height)
    {
        var renderTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "AcrylicV3LabCapture", antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave
        };
        renderTarget.Create();
        RenderTexture previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D readback = null;
        try
        {
            camera.aspect = (float)width / height;
            camera.targetTexture = renderTarget;
            // Warm-up renders settle target switches and the first post-processing frame after a preset change.
            for (int i = 0; i < 4; i++) camera.Render();
            RenderTexture.active = renderTarget;
            readback = new Texture2D(width, height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            readback.Apply(false, false);
            var result = readback;
            readback = null; // ownership moves to the caller
            return result;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            renderTarget.Release();
            UnityEngine.Object.DestroyImmediate(renderTarget);
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            camera.ResetAspect();
        }
    }

    private static byte[] RenderPng(Camera camera, int width, int height)
    {
        var readback = RenderReadback(camera, width, height);
        try
        {
            return readback.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(readback);
        }
    }

    // Point-upscales a crop of `source` (clamped to its bounds) by `scale` and writes it as PNG.
    private static void WriteScaledCrop(Texture2D source, int x, int y, int w, int h, int scale, string path)
    {
        x = Mathf.Clamp(x, 0, source.width - 1);
        y = Mathf.Clamp(y, 0, source.height - 1);
        w = Mathf.Clamp(w, 1, source.width - x);
        h = Mathf.Clamp(h, 1, source.height - y);
        var sourcePixels = source.GetPixels(x, y, w, h);
        var scaledPixels = new Color[w * scale * h * scale];
        for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                Color pixel = sourcePixels[sy * w + sx];
                for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        scaledPixels[(sy * scale + dy) * (w * scale) + sx * scale + dx] = pixel;
            }
        var cropped = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            cropped.SetPixels(scaledPixels);
            cropped.Apply(false, false);
            File.WriteAllBytes(path, cropped.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cropped);
        }
    }

    // Pixel position of a world point inside a width x height capture (bottom-left origin, like ReadPixels). The
    // camera's own pixelRect is the Game view, not the capture target, so project with the capture aspect instead.
    private static Vector2 CaptureScreenPoint(Camera camera, Vector3 worldPosition, int width, int height)
    {
        try
        {
            camera.aspect = (float)width / height;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return new Vector2(viewport.x * width, viewport.y * height);
        }
        finally
        {
            camera.ResetAspect();
        }
    }

    // Freezes the flame shader clock and every active flame light at `time`, so captures are reproducible.
    private static void FreezeFlames(float time)
    {
        DungeonRunFlameClock.OverrideTime = time;
        DungeonRunFlameLight.EvaluateAll(time);
    }

    // Back to live flame time; lights return to their rest (base) state.
    private static void ReleaseFlames()
    {
        DungeonRunFlameClock.OverrideTime = null;
        DungeonRunFlameLight.RestoreAll();
    }

    public static string CaptureLook(string label, bool distances)
    {
        var camera = RequireCamera();
        string directory = PrepareCaptureDirectory(label);
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        Vector3 originPosition = camera.transform.position;
        Quaternion originRotation = camera.transform.rotation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            FreezeFlames(CaptureFlameTime);
            File.WriteAllBytes(Path.Combine(directory, "normal_1920x1080.png"), RenderPng(camera, 1920, 1080));
            File.WriteAllBytes(Path.Combine(directory, "normal_2560x1440.png"), RenderPng(camera, 2560, 1440));
            if (distances)
            {
                camera.transform.position = originPosition + camera.transform.forward * 7f;
                File.WriteAllBytes(Path.Combine(directory, "close_1920x1080.png"), RenderPng(camera, 1920, 1080));
                camera.transform.position = originPosition - camera.transform.forward * 6f;
                File.WriteAllBytes(Path.Combine(directory, "far_1920x1080.png"), RenderPng(camera, 1920, 1080));
            }
            return directory;
        }
        finally
        {
            ReleaseFlames();
            camera.transform.position = originPosition;
            camera.transform.rotation = originRotation;
            ShaderUtil.allowAsyncCompilation = previousAsync;
        }
    }

    public static string CaptureMotion(string label, string motion, int frames, float amplitude)
    {
        if (motion != "camera_x" && motion != "player_x") throw new ArgumentException("motion must be camera_x or player_x.");
        if (frames < 1 || frames > 120) throw new ArgumentException("frames must be 1..120.");
        var camera = RequireCamera();
        string directory = PrepareCaptureDirectory(label);
        var scene = SceneManager.GetActiveScene();
        var root = RequireRoot(scene);
        Transform mover = motion == "camera_x"
            ? camera.transform
            : root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "CHR_Player");
        Vector3 originPosition = mover.position;
        Vector3 right = camera.transform.right;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            // Flames stay frozen at one moment so frame-to-frame differences come from the motion only.
            FreezeFlames(CaptureFlameTime);
            for (int frame = 0; frame < frames; frame++)
            {
                float t = frames == 1 ? 0f : Mathf.Lerp(-1f, 1f, frame / (float)(frames - 1));
                mover.position = originPosition + right * (t * amplitude);
                File.WriteAllBytes(Path.Combine(directory, $"frame-{frame:D3}.png"), RenderPng(camera, 1920, 1080));
            }
            return directory;
        }
        finally
        {
            ReleaseFlames();
            mover.position = originPosition;
            ShaderUtil.allowAsyncCompilation = previousAsync;
        }
    }

    public static string CaptureCrop(string label, int x, int y, int w, int h, int scale)
    {
        if (w <= 0 || h <= 0 || scale < 1) throw new ArgumentException("Invalid crop rectangle or scale.");
        var camera = RequireCamera();
        string directory = PrepareCaptureDirectory(label);
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        Texture2D full = null;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            FreezeFlames(CaptureFlameTime);
            full = RenderReadback(camera, 2560, 1440);
            string path = Path.Combine(directory, "crop.png");
            WriteScaledCrop(full, x, y, w, h, scale, path);
            return path;
        }
        finally
        {
            ReleaseFlames();
            if (full != null) UnityEngine.Object.DestroyImmediate(full);
            ShaderUtil.allowAsyncCompilation = previousAsync;
        }
    }

    /// <summary>
    /// Flame animation strip: frame k freezes the flame clock and lights at 1.25 + k * dt, renders 2560x1440 and saves
    /// a 2x point-scaled 220x300 crop around every active torch (torch index = order by hierarchy path), plus full
    /// 1920x1080 frames for the first and last k.
    /// </summary>
    public static string CaptureFlames(string label, int frames, float dt)
    {
        const int width = 2560, height = 1440, cropWidth = 220, cropHeight = 300, cropScale = 2;
        if (frames < 1 || frames > 100) throw new ArgumentException("frames must be 1..100.");
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentException("dt must be a finite, non-negative time step.");
        var camera = RequireCamera();
        var scene = camera.gameObject.scene;
        var torches = DungeonRunFlameLight.Active
            .Where(flame => flame != null && flame.isActiveAndEnabled && flame.gameObject.scene == scene)
            .OrderBy(flame => HierarchyPath(flame.transform), StringComparer.Ordinal)
            .ToArray();
        if (torches.Length == 0)
            throw new InvalidOperationException("No active DungeonRunFlameLight in the scene; run Install and apply an Acrylic preset first.");
        string directory = PrepareCaptureDirectory(label);
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            for (int frame = 0; frame < frames; frame++)
            {
                FreezeFlames(CaptureFlameTime + frame * dt);
                var full = RenderReadback(camera, width, height);
                try
                {
                    for (int index = 0; index < torches.Length; index++)
                    {
                        Vector2 centre = CaptureScreenPoint(camera, torches[index].transform.position + Vector3.up * .25f,
                            width, height);
                        int x = Mathf.Clamp(Mathf.RoundToInt(centre.x) - cropWidth / 2, 0, width - cropWidth);
                        int y = Mathf.Clamp(Mathf.RoundToInt(centre.y) - cropHeight / 2, 0, height - cropHeight);
                        WriteScaledCrop(full, x, y, cropWidth, cropHeight, cropScale,
                            Path.Combine(directory, $"torch{index}_f{frame:D2}.png"));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(full);
                }
                if (frame == 0 || frame == frames - 1)
                    File.WriteAllBytes(Path.Combine(directory, $"full_f{frame:D2}_1920x1080.png"), RenderPng(camera, 1920, 1080));
            }
            return directory;
        }
        finally
        {
            ReleaseFlames();
            ShaderUtil.allowAsyncCompilation = previousAsync;
        }
    }
}
