using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Lab-only Character Value Separation V1. Character renderer slots receive character-scoped variants of the
/// accepted stylized materials. Environment materials, meshes, lights, fog, camera and renderer are untouched.
/// </summary>
public static class DungeonRunCharacterValueLab
{
    public const string VariantFolder = DungeonRunStylizedLighting.BaselineFolder + "/CharacterValueV1";
    public const string CaptureFolder = "Captures/CharacterValueV1";
    private const string RendererPath = "Assets/Settings/PC_LabPixel_Renderer.asset";
    private const int Width = 1920, Height = 1080, VirtualWidth = 480, VirtualHeight = 270;
    private static readonly string[] Characters = { "CHR_Player", "CHR_Enemy_01", "CHR_Enemy_02", "CHR_Enemy_03" };

    // Accepted StylizedV1 family per character slot -> character-scoped role. MetalDark and StoneDark are shared
    // with the environment, so characters must never edit those assets in place.
    private static readonly Dictionary<(string character, string family), string> Roles = new()
    {
        [("CHR_Player", "TravelerCloth")] = "PlayerCloth",
        [("CHR_Player", "MetalDark")] = "CharacterMetal",
        [("CHR_Player", "TravelerArmor")] = "PlayerArmor",
        [("CHR_Player", "ArcaneCool")] = "PlayerArcane",
        [("CHR_Enemy_01", "MetalDark")] = "CharacterMetal",
        [("CHR_Enemy_01", "EnemyBone")] = "GuardianBone",
        [("CHR_Enemy_01", "StoneDark")] = "GuardianShield",
        [("CHR_Enemy_02", "EnemyClay")] = "HoundClay",
        [("CHR_Enemy_02", "MetalDark")] = "CharacterMetal",
        [("CHR_Enemy_02", "StoneDark")] = "CreatureStone",
        [("CHR_Enemy_03", "StoneDark")] = "CreatureStone",
        [("CHR_Enemy_03", "EnemyJade")] = "SentinelJade",
        [("CHR_Enemy_03", "ArcaneWarm")] = "SentinelArcane",
    };

    // V1/V2 edit only existing stylized-shader material parameters. Roles not listed keep their V0 asset.
    // Band maps show legs, shield face and sentinel body already sit in readable lighting bands (1-2); their
    // near-black albedo (MetalDark ~0.8% linear, StoneDark shared with the environment) crushes that structure.
    private static readonly Dictionary<string, Dictionary<string, Action<Material>>> Variants = new()
    {
        ["V0"] = new Dictionary<string, Action<Material>>(),
        // Value only: dark character materials move out of the environment's shadowed-stone range, hue preserved.
        ["V1"] = new Dictionary<string, Action<Material>>
        {
            ["CharacterMetal"] = LiftCharacterMetal,
            ["GuardianShield"] = m => m.SetColor("_BaseColor", new Color(0.272f, 0.352f, 0.384f)),
            ["CreatureStone"] = m => m.SetColor("_BaseColor", new Color(0.272f, 0.352f, 0.384f)),
        },
        // V1 plus less self-shadow compression where legs meet shaded character forms (hound underside, legs
        // behind the shield). Only the lowest band level changes; lit bands and thresholds are untouched.
        ["V2"] = new Dictionary<string, Action<Material>>
        {
            ["CharacterMetal"] = m => { LiftCharacterMetal(m); SetShadowBandLevel(m, 0.20f); },
            ["GuardianShield"] = m => m.SetColor("_BaseColor", new Color(0.272f, 0.352f, 0.384f)),
            ["CreatureStone"] = m => m.SetColor("_BaseColor", new Color(0.272f, 0.352f, 0.384f)),
            ["HoundClay"] = m => SetShadowBandLevel(m, 0.20f),
        },
    };

    private static void LiftCharacterMetal(Material material)
    {
        material.SetColor("_BaseColor", new Color(0.15f, 0.17f, 0.19f));
        material.SetFloat("_Metallic", 0.45f);
    }

    private static void SetShadowBandLevel(Material material, float level)
    {
        Vector4 levels = material.GetVector("_BandLevels");
        levels.x = level;
        material.SetVector("_BandLevels", levels);
    }

    [Serializable] private class SlotRecord
    {
        public string character, role, material, assetPath;
        public int slot;
        public Color baseColor, shadowTint, midTint;
        public Vector4 bandLevels;
        public float ambientStrength, metallic, smoothness, specularStrength;
    }

    [Serializable] private class CaptureRecord
    {
        public string image, description;
        public int enemyCount;
        public bool characterShadows, ambientOcclusion, fog;
        public SlotRecord[] slots;
    }

    [Serializable] private class MaskEntry { public int id; public string name; public Color linearColor; }

    [Serializable] private class ProjectedSlot
    {
        public string character, role;
        public int slot, maskId;
        public Vector3[] world;
        public Vector2[] virtualPixel;
    }

    [Serializable] private class MaskRecord { public string image; public MaskEntry[] ids; public ProjectedSlot[] projections; }

    private static string RoleFamily(string role)
    {
        var families = Roles.Where(x => x.Value == role).Select(x => x.Key.family).Distinct().ToArray();
        if (families.Length != 1) throw new ArgumentException($"Unknown or ambiguous character role: {role}.");
        return families[0];
    }

    private static Material StylizedAsset(string family)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>($"{DungeonRunStylizedLighting.StylizedFolder}/{family}_StylizedV1.mat");
        if (material == null || material.shader == null || material.shader.name != DungeonRunStylizedLighting.ShaderName)
            throw new InvalidOperationException($"Missing or unexpected accepted stylized material: {family}.");
        return material;
    }

    private static string VariantPath(string variant, string role) => $"{VariantFolder}/{variant}_{role}.mat";

    private static Dictionary<string, Action<Material>> EditsFor(string variant) =>
        Variants.TryGetValue(variant ?? "", out var edits) ? edits : throw new ArgumentException($"Unknown variant: {variant}.");

    private static Material VariantAsset(string variant, string role)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(VariantPath(variant, role));
        if (material == null || material.shader == null || material.shader.name != DungeonRunStylizedLighting.ShaderName)
            throw new InvalidOperationException($"Missing {variant} material for {role}. Run Create or Update V1-V2 Materials.");
        return material;
    }

    /// <summary>Accepted StylizedV1 family of a V0 or Character Value material; null for anything else.</summary>
    public static string FamilyOf(Material material)
    {
        string path = material ? AssetDatabase.GetAssetPath(material) : "";
        string name = Path.GetFileNameWithoutExtension(path);
        if (path.StartsWith(DungeonRunStylizedLighting.StylizedFolder + "/") && name.EndsWith("_StylizedV1"))
            return name.Substring(0, name.Length - "_StylizedV1".Length);
        if (!path.StartsWith(VariantFolder + "/")) return null;
        string[] parts = name.Split('_');
        // Stale, renamed or duplicated assets (e.g. "V1_CharacterMetal 1") are ignored rather than blocking A/B/C.
        return parts.Length == 2 && Variants.ContainsKey(parts[0]) && Roles.ContainsValue(parts[1])
            ? RoleFamily(parts[1]) : null;
    }

    /// <summary>Character Value materials that A/B controls may map back to their accepted family.</summary>
    public static IEnumerable<(Material material, string family)> VariantMaterials()
    {
        if (!AssetDatabase.IsValidFolder(VariantFolder)) yield break;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { VariantFolder }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string family = FamilyOf(material);
            if (family != null) yield return (material, family);
        }
    }

    private static (Scene scene, Transform stage, Camera camera) RequireLab(bool requireClean)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
            throw new InvalidOperationException("Open SceneVictorLab in stable Edit Mode before Character Value controls.");
        if (requireClean && scene.isDirty)
            throw new InvalidOperationException("Save or revert SceneVictorLab before transient captures.");
        var root = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var stage = root.transform.Find("TrigonalAbyssStage");
        var cameraTransform = root.transform.Find("CombatCamera");
        Camera camera = null;
        if (stage == null || cameraTransform == null || !cameraTransform.TryGetComponent(out camera))
            throw new InvalidOperationException("Validated stage or CombatCamera was not found; no changes made.");
        return (scene, stage, camera);
    }

    private static Renderer CharacterRenderer(Transform stage, string character)
    {
        var transform = stage.Find(character);
        if (transform == null || !transform.TryGetComponent<Renderer>(out var renderer))
            throw new InvalidOperationException($"{character} renderer was not found; no changes made.");
        return renderer;
    }


    // Renderer the camera actually resolves through the active URP asset (quality levels can differ).
    private static ScriptableRendererData ActiveRendererData(Camera camera)
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return null;
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        int index = new SerializedObject(data).FindProperty("m_RendererIndex").intValue;
        if (index < 0) index = serialized.FindProperty("m_DefaultRendererIndex").intValue;
        return index < renderers.arraySize
            ? renderers.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData : null;
    }

    // Resolves every character slot before any mutation.
    private static Dictionary<Renderer, Material[]> ResolveCharacters(Transform stage,
        Func<string, int, string, string, Material> select)
    {
        var result = new Dictionary<Renderer, Material[]>();
        foreach (string character in Characters)
        {
            var renderer = CharacterRenderer(stage, character);
            Material[] materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                string family = FamilyOf(materials[slot]);
                if (family == null || !Roles.TryGetValue((character, family), out string role))
                    throw new InvalidOperationException(
                        $"Unexpected material on {character} slot {slot}; apply Stylized or C first. No changes made.");
                materials[slot] = select(character, slot, role, family);
            }
            result.Add(renderer, materials);
        }
        return result;
    }

    [MenuItem("Dungeon Run/Character Value V1/Create or Update V1-V2 Materials")]
    public static void CreateVariantMaterials()
    {
        RequireLab(false);
        // Validate every accepted source first so a missing asset cannot leave a partial regeneration.
        var work = Variants.Where(x => x.Key != "V0")
            .SelectMany(variant => variant.Value.Select(edit =>
                (variant: variant.Key, role: edit.Key, edit: edit.Value, source: StylizedAsset(RoleFamily(edit.Key)))))
            .ToArray();
        if (!AssetDatabase.IsValidFolder(VariantFolder))
            AssetDatabase.CreateFolder(DungeonRunStylizedLighting.BaselineFolder, "CharacterValueV1");
        foreach (var item in work)
        {
            string path = VariantPath(item.variant, item.role);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(item.source);
            else
            {
                material.shader = item.source.shader;
                material.CopyPropertiesFromMaterial(item.source);
            }
            // Variants are regenerated from the accepted V0 asset so the table above stays authoritative.
            material.name = $"{item.variant}_{item.role}";
            item.edit(material);
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Character Value V1: wrote {work.Length} variant materials. Scene unchanged.");
    }

    [MenuItem("Dungeon Run/Character Value V1/V0 - Restore Accepted Stylized Materials")]
    public static void ApplyV0() => Apply("V0");

    [MenuItem("Dungeon Run/Character Value V1/V1 - Apply")]
    public static void ApplyV1() => Apply("V1");

    [MenuItem("Dungeon Run/Character Value V1/V2 - Apply")]
    public static void ApplyV2() => Apply("V2");

    public static void Apply(string variant)
    {
        var edits = EditsFor(variant);
        var (scene, stage, _) = RequireLab(false);
        var assignments = ResolveCharacters(stage, (character, slot, role, family) =>
            edits.ContainsKey(role) ? VariantAsset(variant, role) : StylizedAsset(family));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"Apply Character Value {variant}");
        int changed = 0;
        foreach (var pair in assignments)
        {
            if (pair.Key.sharedMaterials.SequenceEqual(pair.Value)) continue;
            Undo.RecordObject(pair.Key, "Switch character value materials");
            pair.Key.sharedMaterials = pair.Value;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
            EditorUtility.SetDirty(pair.Key);
            changed++;
        }
        Undo.CollapseUndoOperations(group);
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
        Debug.Log($"Character Value V1: {variant} applied to {changed} character renderers. Scene not automatically saved.");
    }

    /// <summary>Transient capture of a saved variant with 1..3 enemies (01, 01+02, all). Nothing is saved.</summary>
    public static string CaptureVariant(string variant, int enemyCount, string fileName)
    {
        var edits = EditsFor(variant);
        return CaptureTransient(fileName, $"Character Value {variant}", enemyCount, true, true, true,
            (character, slot, role, family) => edits.ContainsKey(role) ? VariantAsset(variant, role) : StylizedAsset(family));
    }

    /// <summary>Diagnostic only: in-memory copies of V0 role materials, optional character shadow/AO removal.</summary>
    public static string CaptureExperiment(string fileName, string description, bool characterShadows,
        bool ambientOcclusion, Dictionary<string, Action<Material>> roleEdits)
    {
        var temporary = new Dictionary<string, Material>();
        try
        {
            return CaptureTransient(fileName, description, 3, characterShadows, ambientOcclusion, true,
                (character, slot, role, family) =>
                {
                    if (roleEdits == null || !roleEdits.TryGetValue(role, out var edit)) return StylizedAsset(family);
                    if (!temporary.TryGetValue(role, out var material))
                    {
                        material = new Material(StylizedAsset(family))
                            { name = "Experiment_" + role, hideFlags = HideFlags.HideAndDontSave };
                        temporary.Add(role, material); // Registered before the caller edit so a throw cannot leak it.
                        edit(material);
                    }
                    return material;
                });
        }
        finally
        {
            foreach (var material in temporary.Values) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    /// <summary>Diagnostic flat-ID render at the same camera/pixel grid, plus projected character vertices.</summary>
    public static string CaptureIdMask(string fileName)
    {
        var shader = Shader.Find(DungeonRunStylizedLighting.ShaderName);
        var ids = new List<MaskEntry>();
        var materials = new Dictionary<int, Material>();
        Material Id(int id, string name)
        {
            if (materials.TryGetValue(id, out var existing)) return existing;
            var color = new Color((id & 3) / 3f, ((id >> 2) & 3) / 3f, ((id >> 4) & 3) / 3f, 1);
            var material = new Material(shader) { name = "IdMask_" + id, hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", Color.black);
            material.SetVector("_BandLevels", Vector4.zero);
            material.SetFloat("_AmbientStrength", 0);
            material.SetFloat("_SpecularStrength", 0);
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
            materials.Add(id, material);
            ids.Add(new MaskEntry { id = id, name = name, linearColor = color });
            return material;
        }
        // Four IDs per character keep character IDs (1-16) below the environment IDs (20-23).
        int CharacterId(string character, int slot) => slot < 4
            ? 1 + Array.IndexOf(Characters, character) * 4 + slot
            : throw new InvalidOperationException($"{character} has more than four material slots; widen mask IDs.");
        int EnvironmentId(Renderer renderer) =>
            renderer.name.StartsWith("ENV_FloorTiles") ? 20 : renderer.name == "ENV_FloorInlay" ? 21 :
            renderer.name == "ENV_FloorFoundation" ? 22 : 23;
        string EnvironmentName(int id) => id switch { 20 => "FloorTiles", 21 => "FloorInlay", 22 => "FloorFoundation", _ => "OtherEnvironment" };
        try
        {
            string image = CaptureTransient(fileName, "ID mask", 3, true, true, false,
                (character, slot, role, family) => Id(CharacterId(character, slot), $"{character}/{slot}/{role}"),
                renderer => { int id = EnvironmentId(renderer); return Id(id, EnvironmentName(id)); },
                (camera, originals) => ProjectCharacters(camera, originals, CharacterId));
            string sidecar = Path.Combine(Path.GetDirectoryName(image), Path.GetFileNameWithoutExtension(image) + ".mask.json");
            var record = JsonUtility.FromJson<MaskRecord>(File.ReadAllText(sidecar));
            record.ids = ids.OrderBy(x => x.id).ToArray();
            File.WriteAllText(sidecar, JsonUtility.ToJson(record, true));
            return image;
        }
        finally
        {
            foreach (var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static MaskRecord ProjectCharacters(Camera camera, Dictionary<Renderer, Material[]> originals,
        Func<string, int, int> characterId)
    {
        var stage = camera.transform.parent.Find("TrigonalAbyssStage");
        var projections = new List<ProjectedSlot>();
        foreach (string character in Characters)
        {
            var renderer = CharacterRenderer(stage, character);
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            for (int slot = 0; slot < mesh.subMeshCount; slot++)
            {
                var world = mesh.GetTriangles(slot).Distinct().Select(i => renderer.transform.TransformPoint(vertices[i])).ToArray();
                projections.Add(new ProjectedSlot
                {
                    character = character, slot = slot, maskId = characterId(character, slot),
                    role = Roles[(character, FamilyOf(originals[renderer][slot]))],
                    world = world,
                    virtualPixel = world.Select(p =>
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(p);
                        return new Vector2(viewport.x * VirtualWidth, (1 - viewport.y) * VirtualHeight);
                    }).ToArray()
                });
            }
        }
        return new MaskRecord { projections = projections.ToArray() };
    }

    private static string CaptureTransient(string fileName, string description, int enemyCount,
        bool characterShadows, bool ambientOcclusion, bool fog, Func<string, int, string, string, Material> selectCharacter,
        Func<Renderer, Material> selectEnvironment = null,
        Func<Camera, Dictionary<Renderer, Material[]>, MaskRecord> maskRecord = null)
    {
        var (scene, stage, camera) = RequireLab(true);
        var settings = camera.GetComponent<DungeonRunPixelRenderSettings>();
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var pixelFeature = rendererData.rendererFeatures.OfType<DungeonRunPixelRenderFeature>().Single();
        var aoFeature = rendererData.rendererFeatures.Single(x => x && x.GetType().Name == "ScreenSpaceAmbientOcclusion");
        if (!settings || !settings.isActiveAndEnabled || !settings.PixelEnabled ||
            settings.VirtualResolution != new Vector2Int(VirtualWidth, VirtualHeight) || !pixelFeature.isActive ||
            pixelFeature.material == null || pixelFeature.material.shader.name != DungeonRunPixelRendering.ShaderName ||
            !aoFeature.isActive || camera.orthographic || camera.targetTexture != null || !RenderSettings.fog ||
            ActiveRendererData(camera) != rendererData)
            throw new InvalidOperationException("Require intact perspective C480 lab renderer with AO, fog and no camera target.");
        if (enemyCount < 1 || enemyCount > 3) throw new ArgumentOutOfRangeException(nameof(enemyCount));
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".png"))
            throw new ArgumentException("File name must be a plain .png name.");
        string directory = Path.GetFullPath(CaptureFolder);
        string path = Path.Combine(directory, fileName);
        string sidecar = Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName) + (maskRecord != null ? ".mask.json" : ".json"));
        if (File.Exists(path) || File.Exists(sidecar)) throw new IOException($"Refusing to overwrite {fileName}.");

        // Resolve everything before mutation.
        var characterMaterials = ResolveCharacters(stage, selectCharacter);
        var allRenderers = stage.GetComponentsInChildren<Renderer>(true);
        var environmentMaterials = selectEnvironment == null ? new Dictionary<Renderer, Material[]>() :
            allRenderers.Where(x => !characterMaterials.ContainsKey(x))
                .ToDictionary(x => x, x => x.sharedMaterials.Select(_ => selectEnvironment(x)).ToArray());
        var enemies = Characters.Skip(1).Select(x => stage.Find(x).gameObject).ToArray();
        if (enemies.Any(x => !x.activeSelf)) throw new InvalidOperationException("All three enemies must start active.");

        var originalMaterials = allRenderers.ToDictionary(x => x, x => x.sharedMaterials);
        var originalShadows = characterMaterials.Keys.ToDictionary(x => x, x => x.shadowCastingMode);
        RenderTexture active = RenderTexture.active, renderTarget = null;
        Texture2D readback = null;
        var record = new CaptureRecord
        {
            image = fileName, description = description, enemyCount = enemyCount,
            characterShadows = characterShadows, ambientOcclusion = ambientOcclusion, fog = fog,
        };
        try
        {
            foreach (var pair in characterMaterials.Concat(environmentMaterials)) pair.Key.sharedMaterials = pair.Value;
            if (!characterShadows)
                foreach (var renderer in characterMaterials.Keys) renderer.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < enemies.Length; i++) enemies[i].SetActive(i < enemyCount);
            aoFeature.SetActive(ambientOcclusion);
            RenderSettings.fog = fog;
            renderTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "CharacterValueLabCapture", antiAliasing = 1, filterMode = FilterMode.Point,
                useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.HideAndDontSave
            };
            renderTarget.Create();
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
            camera.aspect = (float)Width / Height;
            camera.targetTexture = renderTarget;
            // Two renders settle transient allocations after a target/material switch (same as the V1.1 harness).
            camera.Render();
            camera.Render();
            // New keyword/material variants can compile asynchronously and draw nothing on the first render.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (ShaderUtil.anythingCompiling && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(50);
            if (ShaderUtil.anythingCompiling) throw new InvalidOperationException("Shaders still compiling; capture refused.");
            camera.Render();
            camera.Render();
            RenderTexture.active = renderTarget;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            readback.Apply(false, false);
            // Build the sidecar before writing either file so a failure cannot leave an orphan PNG.
            string sidecarJson;
            if (maskRecord != null)
            {
                var mask = maskRecord(camera, originalMaterials);
                mask.image = fileName;
                sidecarJson = JsonUtility.ToJson(mask, true);
            }
            else
            {
                record.slots = characterMaterials.SelectMany(pair => pair.Value.Select((material, slot) => new SlotRecord
                {
                    character = pair.Key.name, slot = slot, material = material.name,
                    role = Roles[(pair.Key.name, FamilyOf(originalMaterials[pair.Key][slot]))],
                    assetPath = AssetDatabase.GetAssetPath(material),
                    baseColor = material.GetColor("_BaseColor"), shadowTint = material.GetColor("_ShadowTint"),
                    midTint = material.GetColor("_MidTint"), bandLevels = material.GetVector("_BandLevels"),
                    ambientStrength = material.GetFloat("_AmbientStrength"), metallic = material.GetFloat("_Metallic"),
                    smoothness = material.GetFloat("_Smoothness"), specularStrength = material.GetFloat("_SpecularStrength")
                })).ToArray();
                sidecarJson = JsonUtility.ToJson(record, true);
            }
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, readback.EncodeToPNG());
            File.WriteAllText(sidecar, sidecarJson);
            return path;
        }
        finally
        {
            foreach (var pair in originalMaterials) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            foreach (var pair in originalShadows) if (pair.Key) pair.Key.shadowCastingMode = pair.Value;
            foreach (var enemy in enemies) enemy.SetActive(true);
            aoFeature.SetActive(true);
            RenderSettings.fog = true;
            camera.targetTexture = null;
            // Setting aspect makes it explicit; the lab camera must return to automatic screen aspect.
            camera.ResetAspect();
            RenderTexture.active = active;
            if (renderTarget) { renderTarget.Release(); UnityEngine.Object.DestroyImmediate(renderTarget); }
            if (readback) UnityEngine.Object.DestroyImmediate(readback);
            if (scene.isDirty) Debug.LogError("Character Value V1: transient capture dirtied SceneVictorLab; inspect before saving.");
        }
    }
}
