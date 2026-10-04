using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, presentation-only import/review tools. Never runs automatically or modifies combat bindings.</summary>
public static class DungeonRunEnemyArtLab
{
    public const string LabPath = "Assets/Scenes/SceneVictor/SceneVictorLab.unity";
    public const string ReviewRootName = "EnemyArtReview_V1";
    private const string OwnershipMarker = "__DungeonRunEnemyArtLabOwned_V1";
    private const string ArtRoot = "Assets/Art/Characters/Enemies/";
    private const string AcrylicRoot = "Assets/Materials/TrigonalAbyss/AcrylicV3/";
    private static readonly string[] Names = { "SnakeSoldier", "SnakeSentinel", "SnakeChieftain" };
    private static readonly string[] Categories = { "Basic", "Elite", "Boss" };
    private static readonly string[] ClipNames = { "Idle", "Attack", "HitReaction", "Death" };
    // Presentation-only size hierarchy: body height (weapons excluded) relative to the protagonist.
    // Starting values for visual tuning with the combat camera, not combat statistics or final classification.
    private static readonly float[] HeightRatios = { 1.15f, 1.30f, 1.50f };
    private const float ProtagonistHeightFallback = 2.90f; // CHR_Player bounds in SceneVictorLab (V1.1 audit)

    public static float HeightRatio(string enemyName)
    {
        int index = Array.IndexOf(Names, enemyName);
        if (index < 0) throw new ArgumentException("Unknown enemy art candidate: " + enemyName);
        return HeightRatios[index];
    }

    private static float ProtagonistHeight()
    {
        var player = GameObject.Find("CHR_Player");
        var renderer = player ? player.GetComponent<Renderer>() : null;
        return renderer ? renderer.bounds.size.y : ProtagonistHeightFallback;
    }

    private static string Folder(int index) => ArtRoot + Categories[index] + "/" + Names[index];
    private static string ModelPath(int index) => Folder(index) + "/Models/CHR_" + Names[index] + ".fbx";
    private static string PrefabPath(int index) => Folder(index) + "/Prefab/PF_" + Names[index] + ".prefab";

    private static void RequireLab(bool clean)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != LabPath || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Enemy art tools require SceneVictorLab in stable Edit Mode.");
        if (clean && scene.isDirty)
            throw new InvalidOperationException("Save or resolve current Lab edits before installing enemy assets.");
        if (AnimationMode.InAnimationMode())
            throw new InvalidOperationException("End the animation preview before installing or modifying the review layout.");
    }

    [MenuItem("Dungeon Run/Enemy Art V1/Import Representative Assets")]
    public static void InstallAssets()
    {
        RequireLab(true);
        // Validate every required source before creating or updating any generated asset.
        for (int i = 0; i < Names.Length; i++)
        {
            if (!File.Exists(ModelPath(i))) throw new FileNotFoundException("Missing enemy FBX", ModelPath(i));
            if (!File.Exists(PalettePath(i))) throw new FileNotFoundException("Missing enemy palette", PalettePath(i));
        }
        foreach (string family in new[] { "EnemyJade", "CharacterMetal" })
            if (AssetDatabase.LoadAssetAtPath<Material>(AcrylicRoot + family + "_AcrylicV3.mat") == null)
                throw new InvalidOperationException("Missing AcrylicV3 family: " + family);

        for (int i = 0; i < Names.Length; i++)
        {
            EnsureFolder(Folder(i) + "/Materials");
            EnsureFolder(Folder(i) + "/Animations");
            EnsureFolder(Folder(i) + "/Prefab");
            ImportPalette(i);
            var organic = MaterialCopy(i, "Organic", "EnemyJade");
            var equipment = MaterialCopy(i, "Equipment", "CharacterMetal");
            ImportModel(i, organic, equipment);
            BuildPrefab(i, BuildController(i));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy Art V1 assets imported. Review root and combat bindings are unchanged.");
    }

    private static string PalettePath(int index) => Folder(index) + "/Textures/" + Names[index] + "_Palette.png";

    private static void ImportPalette(int index)
    {
        string path = PalettePath(index);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true; // The legacy Palette filename now contains a full opaque baked color atlas.
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static Material MaterialCopy(int index, string slot, string family)
    {
        string path = Folder(index) + "/Materials/MAT_" + Names[index] + "_" + slot + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material; // Preserve artist tuning on subsequent imports.
        var source = AssetDatabase.LoadAssetAtPath<Material>(AcrylicRoot + family + "_AcrylicV3.mat");
        material = new Material(source) { name = "MAT_" + Names[index] + "_" + slot };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath(index)));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_BrushMapping", 2f);
        material.SetFloat("_BrushScale", 8f);
        material.EnableKeyword("_DR_ACRYLIC");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ImportModel(int index, Material organic, Material equipment)
    {
        string path = ModelPath(index);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false; // Keep sockets and modular editing transforms accessible.
        importer.preserveHierarchy = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true; // Match the accepted Blender stage's -Z forward / Y up export.
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        // Blender namespaces materials in an interactive multi-character file (e.g. .001).
        // Read the original source identifiers, not the already-remapped destination material names.
        var serialized = new SerializedObject(importer);
        var sourceMaterials = serialized.FindProperty("m_Materials");
        if (sourceMaterials == null || !sourceMaterials.isArray || sourceMaterials.arraySize == 0)
            throw new InvalidOperationException(path + ": no source material identifiers were imported.");
        for (int materialIndex = 0; materialIndex < sourceMaterials.arraySize; materialIndex++)
        {
            var nameProperty = sourceMaterials.GetArrayElementAtIndex(materialIndex).FindPropertyRelative("name");
            string sourceName = nameProperty == null ? "" : nameProperty.stringValue;
            string family = sourceName;
            int suffix = family.LastIndexOf('.');
            if (suffix >= 0 && int.TryParse(family.Substring(suffix + 1), out _)) family = family.Substring(0, suffix);
            var target = family == "M_EnemyOrganic" || family == "M_" + Names[index] + "_Organic" ? organic :
                family == "M_EnemyEquipment" || family == "M_" + Names[index] + "_Equipment" ? equipment : null;
            if (!target) throw new InvalidOperationException(path + ": unknown source material family " + sourceName);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), target);
        }
        importer.SaveAndReimport();

        var defaults = importer.defaultClipAnimations;
        var clips = new List<ModelImporterClipAnimation>();
        foreach (string clipName in ClipNames)
        {
            var matches = defaults.Where(c => MatchesClip(c.name, clipName) || MatchesClip(c.takeName, clipName)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(path + ": expected one " + clipName + " take, found " + matches.Length);
            var clip = matches[0];
            clip.name = clipName;
            clip.loopTime = clipName == "Idle";
            clip.loopPose = clipName == "Idle";
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionXZ = true;
            clip.keepOriginalPositionY = true;
            clips.Add(clip);
        }
        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
    }

    private static bool MatchesClip(string actual, string expected)
    {
        return actual == expected || actual.EndsWith("|" + expected, StringComparison.Ordinal) ||
               actual.EndsWith("_" + expected, StringComparison.Ordinal);
    }

    private static AnimatorController BuildController(int index)
    {
        string path = Folder(index) + "/Animations/AC_" + Names[index] + "_Validation.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath(index)).OfType<AnimationClip>().ToArray();
        foreach (string name in ClipNames)
        {
            var clip = clips.Single(c => c.name == name);
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
            state.motion = clip;
            if (name == "Idle") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void BuildPrefab(int index, AnimatorController controller)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(index));
        // Preserve an Inspector-tuned visual scale across re-imports.
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(index));
        var existingScale = existing ? existing.GetComponent<DungeonRun.Enemies.EnemyVisualScale>() : null;
        var root = new GameObject("PF_" + Names[index]);
        try
        {
            // PF (scale 1, LODGroup, EnemyVisualScale) > VisualRoot (uniform visual scale, collider, anchors) > Model.
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Model";
            visual.transform.SetParent(visualRoot, false);
            foreach (var importedLod in visual.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(importedLod);
            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (animator.avatar == null || !animator.avatar.isValid || animator.avatar.isHuman)
                throw new InvalidOperationException(model.name + ": a valid Generic avatar is required.");

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var lods = new LOD[3];
            float[] transitions = { .38f, .18f, .035f };
            for (int lod = 0; lod < 3; lod++)
            {
                string suffix = "_LOD" + lod;
                var members = renderers.Where(r => r.name.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
                if (members.Length == 0) throw new InvalidOperationException(model.name + ": no renderers for " + suffix);
                lods[lod] = new LOD(transitions[lod], members);
            }
            if (lods.Sum(l => l.renderers.Length) != renderers.Length)
                throw new InvalidOperationException(model.name + ": all renderers must have an explicit _LOD0/1/2 suffix.");
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.Any(m => m == null || !AssetDatabase.GetAssetPath(m).StartsWith(Folder(index) + "/Materials/", StringComparison.Ordinal)))
                    throw new InvalidOperationException(renderer.name + ": unexpected or unmapped material slot.");
            }
            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods);
            group.RecalculateBounds();
            // Body bounds exclude the rigid weapons so the size hierarchy compares bodies, not spears.
            var body = lods[0].renderers.Where(r => !r.name.Contains("Weapon")).ToArray();
            var bounds = body[0].bounds;
            foreach (var renderer in body.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var capsule = visualRoot.gameObject.AddComponent<CapsuleCollider>();
            capsule.height = bounds.size.y;
            capsule.radius = Mathf.Min(bounds.size.x, bounds.size.z) * .3f;
            capsule.center = new Vector3(0, bounds.center.y, 0);
            capsule.isTrigger = true;
            // Anchors live under the scaled visual root, so they follow the tuned size.
            Anchor(visualRoot, "TargetAnchor", new Vector3(0, bounds.size.y * .65f, 0));
            Anchor(visualRoot, "CenterMass", new Vector3(0, bounds.size.y * .5f, 0));
            Anchor(visualRoot, "DamageTextAnchor", new Vector3(0, bounds.max.y + .15f, 0));
            Anchor(visualRoot, "HUDAnchor", new Vector3(0, bounds.max.y + .25f, 0));
            Anchor(visualRoot, "IntentAnchor", new Vector3(0, bounds.max.y + .4f, 0));
            Anchor(visualRoot, "VFX_Ground", Vector3.zero);
            var scaler = root.AddComponent<DungeonRun.Enemies.EnemyVisualScale>();
            scaler.visualRoot = visualRoot;
            scaler.visualScale = existingScale ? existingScale.visualScale :
                DungeonRun.UI.Presentation.EnemyVisualScaleRules.ForHeightRatio(HeightRatios[index], ProtagonistHeight(), bounds.size.y);
            scaler.Apply();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(index));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void Anchor(Transform parent, string name, Vector3 position)
    {
        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = position;
    }

    [MenuItem("Dungeon Run/Enemy Art V1/Install Isolated Review Root")]
    public static void InstallReview()
    {
        RequireLab(true);
        var scene = SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == ReviewRootName);
        if (root != null)
        {
            if (root.transform.Find(OwnershipMarker) == null)
                throw new InvalidOperationException("Existing review root is not owned by this tool; leaving it untouched.");
            Debug.Log("Enemy review root already exists; positions, visibility and manual edits were preserved.");
            return;
        }
        var prefabs = Enumerable.Range(0, Names.Length).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i))).ToArray();
        if (prefabs.Any(p => p == null)) throw new InvalidOperationException("Import all three enemy prefabs first.");
        root = new GameObject(ReviewRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create isolated enemy art review");
        root.transform.position = new Vector3(30, 0, 0); // Deliberately outside the accepted combat stage.
        Anchor(root.transform, OwnershipMarker, Vector3.zero);
        var instances = new List<Transform>();
        float nextEdge = 0f;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
            instance.transform.SetParent(root.transform, false);
            var renderers = instance.GetComponent<LODGroup>().GetLODs()[0].renderers;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float offset = nextEdge - (bounds.min.x - root.transform.position.x);
            instance.transform.localPosition = new Vector3(offset, 0, 0);
            nextEdge += bounds.size.x + .75f;
            instances.Add(instance.transform);
        }
        float center = (nextEdge - .75f) * .5f;
        foreach (var instance in instances) instance.localPosition -= Vector3.right * center;
        EditorSceneManager.MarkSceneDirty(scene); // Caller owns review staging, capture and explicit save.
    }

    /// <summary>Preview imported clips in Edit Mode, restoring the original pose with EndPosePreview.</summary>
    public static void PreviewPose(string enemyName, string clipName, float normalizedTime)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != LabPath)
            throw new InvalidOperationException("Pose sampling requires SceneVictorLab Edit Mode.");
        int index = Array.IndexOf(Names, enemyName);
        if (index < 0 || !ClipNames.Contains(clipName)) throw new ArgumentException("Unknown enemy or validation clip.");
        var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == ReviewRootName);
        var instance = root.transform.Find("PF_" + enemyName);
        if (instance == null) throw new InvalidOperationException("Missing review instance: " + enemyName);
        var animator = instance.GetComponentInChildren<Animator>();
        var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath(index)).OfType<AnimationClip>().Single(c => c.name == clipName);
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        try { AnimationMode.SampleAnimationClip(animator.gameObject, clip, Mathf.Clamp01(normalizedTime) * clip.length); }
        finally { AnimationMode.EndSampling(); }
        SceneView.RepaintAll();
    }

    [MenuItem("Dungeon Run/Enemy Art V1/End Pose Preview")]
    public static void EndPosePreview()
    {
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        SceneView.RepaintAll();
    }

    [Serializable] private sealed class RosterReport { public EnemyReport[] enemies; public string costNote; }
    [Serializable] private sealed class EnemyReport
    {
        public string name, prefab, avatarType;
        public int[] triangles, skinnedRenderers, renderers, forwardDrawLowerBound;
        public int boneCount, uniqueMaterialCount;
        public Vector3 rootScale;
        public float visualScale;
        public string[] animationClips, textureDetails;
        public long uniqueTextureResidentBytes;
    }

    /// <summary>Counts imported meshes and material/texture references, not guesses from source budgets.</summary>
    public static string ReportJson()
    {
        var reports = new List<EnemyReport>();
        for (int i = 0; i < Names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i));
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + PrefabPath(i));
            var lods = prefab.GetComponent<LODGroup>().GetLODs();
            var all = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = all.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var textures = materials.SelectMany(m => m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t => t != null).Distinct().ToArray();
            var animator = prefab.GetComponentInChildren<Animator>();
            reports.Add(new EnemyReport
            {
                name = Names[i], prefab = PrefabPath(i), avatarType = animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman ? "Generic" : "INVALID",
                rootScale = prefab.transform.localScale,
                visualScale = prefab.GetComponent<DungeonRun.Enemies.EnemyVisualScale>() is var vs && vs ? vs.Applied : 1f,
                triangles = lods.Select(l => l.renderers.Sum(r => TriangleCount(MeshOf(r)))).ToArray(),
                skinnedRenderers = lods.Select(l => l.renderers.Count(r => r is SkinnedMeshRenderer)).ToArray(),
                renderers = lods.Select(l => l.renderers.Length).ToArray(),
                forwardDrawLowerBound = lods.Select(l => l.renderers.Sum(r => r.sharedMaterials.Length)).ToArray(),
                boneCount = all.OfType<SkinnedMeshRenderer>().SelectMany(r => r.bones).Where(b => b != null).Distinct().Count(),
                uniqueMaterialCount = materials.Length,
                animationClips = animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().ToArray(),
                textureDetails = textures.Select(t => AssetDatabase.GetAssetPath(t) + " " + t.width + "x" + t.height + " " + Profiler.GetRuntimeMemorySizeLong(t) + " bytes resident").ToArray(),
                uniqueTextureResidentBytes = textures.Sum(Profiler.GetRuntimeMemorySizeLong)
            });
        }
        return JsonUtility.ToJson(new RosterReport
        {
            enemies = reports.ToArray(),
            costNote = "Forward draw lower bound counts material slots in the selected LOD, not measured GPU draws. Shadow/depth passes add work; shared textures are counted once per enemy but may be shared across the roster. Bone count is the union referenced by skin renderers, excluding unweighted sockets."
        }, true);
    }

    private static Mesh MeshOf(Renderer renderer)
    {
        var skin = renderer as SkinnedMeshRenderer;
        return skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
    }

    private static int TriangleCount(Mesh mesh)
    {
        int result = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) result += (int)(mesh.GetIndexCount(i) / 3);
        return result;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
