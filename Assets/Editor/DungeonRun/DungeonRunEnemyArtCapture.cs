using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Transient art-only combat-camera evidence. No gameplay bindings, scene saves or renderer edits.</summary>
public static class DungeonRunEnemyArtCapture
{
    private const int Width = 1920, Height = 1080;
    /// <summary>Evidence folder (repo-relative). V1.2 writes to its own batch; earlier V1.1 evidence stays untouched.</summary>
    public static string OutputDirectory = "Captures/EnemyRosterV12/unity";
    private static readonly string[] Names = { "SnakeSoldier", "SnakeSentinel", "SnakeChieftain" };
    private static readonly string[] Clips = { "Idle", "Attack", "HitReaction", "Death" };

    [Serializable] private sealed class LightRecord
    {
        public string name, type;
        public bool enabled;
        public Color color;
        public float intensity;
        public Vector3 position, euler;
    }

    [Serializable] private sealed class CharacterRecord
    {
        public string name;
        public Vector3 position, scale;
        public Bounds bounds;
        public Vector3 screenCenter;
        public int renderers, skinnedRenderers, materialSlots, triangles;
    }

    [Serializable] private sealed class CaptureRecord
    {
        public string image, pose, lighting, renderer, framing, note;
        public bool silhouette;
        public int lod, width = Width, height = Height;
        public float normalizedTime, yaw, cameraFov;
        public Vector3 cameraPosition, cameraEuler;
        public CharacterRecord[] characters;
        public LightRecord[] lights;
    }

    /// <summary>
    /// Captures the existing three review instances at the accepted enemy slots, preserving prefab scale.
    /// LOD 0..2 is explicit; yaw 0 was verified against the imported candidates and combat camera.
    /// Current preserves every light. CoolOnly/WarmOnly/AmbientOnly are clearly labeled diagnostics.
    /// The HUD is excluded because its bindings intentionally still belong to the accepted combat roster.
    /// Close review temporarily frames the posed candidate bounds; it is not combat-composition evidence.
    /// </summary>
    public static string Capture(string fileName, string pose = "Idle", float normalizedTime = 0f,
        int lod = 0, bool silhouette = false, string lighting = "Current", float yaw = 0f, bool closeReview = false)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunEnemyArtLab.LabPath || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Capture requires SceneVictorLab in stable Edit Mode without a pose preview.");
        if (!Clips.Contains(pose) || lod < 0 || lod > 2 || float.IsNaN(normalizedTime) || normalizedTime < 0 || normalizedTime > 1 ||
            !new[] { "Current", "CoolOnly", "WarmOnly", "AmbientOnly" }.Contains(lighting))
            throw new ArgumentException("Invalid explicit pose, LOD, time or lighting selection.");
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName ||
            !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || float.IsNaN(yaw) || float.IsInfinity(yaw))
            throw new ArgumentException("Use a plain .png filename and a finite yaw.");
        string directory = Path.GetFullPath(OutputDirectory);
        string path = Path.Combine(directory, fileName), sidecar = Path.ChangeExtension(path, ".json");
        if (File.Exists(path) || File.Exists(sidecar)) throw new IOException("Capture already exists: " + path);

        // Resolve every dependency before touching live state.
        var roots = scene.GetRootGameObjects();
        var stage = roots.Single(x => x.name == "TrigonalAbyss_Prototype").transform;
        var camera = stage.Find("CombatCamera").GetComponent<Camera>();
        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (!cameraData || cameraData.scriptableRenderer == null || camera.targetTexture != null || camera.orthographic)
            throw new InvalidOperationException("Require the accepted perspective URP combat camera with no target texture.");
        var review = roots.Single(x => x.name == DungeonRunEnemyArtLab.ReviewRootName);
        if (!review.transform.Find("__DungeonRunEnemyArtLabOwned_V1"))
            throw new InvalidOperationException("The isolated review root does not have the expected ownership marker.");
        var sources = Names.Select(n => review.transform.Find("PF_" + n)).ToArray();
        if (sources.Any(x => !x)) throw new InvalidOperationException("Import and install all three review prefabs first.");
        var stageTransforms = stage.GetComponentsInChildren<Transform>(true);
        var placeholders = Enumerable.Range(1, 3).Select(i => stageTransforms.SingleOrDefault(t => t.name == "CHR_Enemy_0" + i)).ToArray();
        if (placeholders.Any(x => !x)) throw new InvalidOperationException("Accepted enemy slots were not found.");
        var originals = placeholders.SelectMany(x => x.GetComponentsInChildren<Renderer>(true))
            .ToDictionary(x => x, x => x.enabled);
        var canvases = roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).ToDictionary(x => x, x => x.enabled);
        var lights = roots.SelectMany(x => x.GetComponentsInChildren<Light>(true)).ToDictionary(x => x, x => x.enabled);
        bool reviewActive = review.activeSelf;
        float aspect = camera.aspect;
        Vector3 cameraPosition = camera.transform.position;
        Quaternion cameraRotation = camera.transform.rotation;
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix, projectionMatrix = camera.projectionMatrix;
        // Restoring the serialized aspect avoids changing automatic-aspect cameras into explicit-aspect cameras.
        string cameraJson = EditorJsonUtility.ToJson(camera);
        RenderTexture previousActive = RenderTexture.active, target = null;
        Texture2D readback = null;
        GameObject transient = null;
        Material black = null;
        var records = new List<CharacterRecord>();
        try
        {
            transient = new GameObject("__TransientEnemyArtCapture") { hideFlags = HideFlags.HideAndDontSave };
            if (silhouette)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) throw new InvalidOperationException("URP Unlit is required for the black silhouette diagnostic.");
                black = new Material(shader) { name = "EnemyArtBlackSilhouette", hideFlags = HideFlags.HideAndDontSave };
                black.SetColor("_BaseColor", Color.black);
            }
            review.SetActive(false);
            foreach (var pair in originals) pair.Key.enabled = false;
            foreach (var pair in canvases) pair.Key.enabled = false;
            foreach (var pair in lights)
            {
                bool warm = pair.Key.color.r > pair.Key.color.b;
                pair.Key.enabled = pair.Value && (lighting == "Current" || lighting == "CoolOnly" && !warm ||
                    lighting == "WarmOnly" && warm);
            }
            for (int i = 0; i < sources.Length; i++)
            {
                var instance = Object.Instantiate(sources[i].gameObject, transient.transform);
                instance.name = Names[i];
                instance.SetActive(true);
                instance.transform.SetPositionAndRotation(placeholders[i].position, Quaternion.Euler(0, yaw, 0));
                instance.transform.localScale = sources[i].localScale;
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (!animator || !animator.runtimeAnimatorController)
                    throw new InvalidOperationException(instance.name + ": missing validation animator.");
                var clip = animator.runtimeAnimatorController.animationClips.Distinct().Single(c => c.name == pose);
                animator.enabled = false;
                clip.SampleAnimation(animator.gameObject, normalizedTime * clip.length);
                var group = instance.GetComponent<LODGroup>();
                if (!group || group.GetLODs().Length != 3) throw new InvalidOperationException(instance.name + ": expected three LODs.");
                var selected = group.GetLODs()[lod].renderers;
                // Explicit renderer enablement avoids previous Scene View camera LOD decisions affecting evidence.
                group.enabled = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = selected.Contains(renderer);
                    if (silhouette) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => black).ToArray();
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                var bounds = PosedBounds(selected[0]);
                foreach (var renderer in selected.Skip(1)) bounds.Encapsulate(PosedBounds(renderer));
                records.Add(new CharacterRecord
                {
                    name = Names[i], position = instance.transform.position, scale = instance.transform.lossyScale,
                    bounds = bounds, screenCenter = camera.WorldToViewportPoint(bounds.center),
                    renderers = selected.Length, skinnedRenderers = selected.Count(r => r is SkinnedMeshRenderer),
                    materialSlots = selected.Sum(r => r.sharedMaterials.Length), triangles = selected.Sum(Triangles)
                });
            }

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "EnemyArtCombatCapture", antiAliasing = 1, filterMode = FilterMode.Point,
                useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            camera.aspect = (float)Width / Height;
            if (closeReview)
            {
                var bounds = records[0].bounds;
                foreach (var character in records.Skip(1)) bounds.Encapsulate(character.bounds);
                FrameBounds(camera, bounds);
            }
            foreach (var character in records) character.screenCenter = camera.WorldToViewportPoint(character.bounds.center);
            camera.targetTexture = target;
            // Same production-camera path as the accepted CharacterValueLab harness: current URP renderer,
            // camera-specific acrylic/pixel settings and world lighting are used, not a replacement preview camera.
            camera.Render();
            camera.Render();
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (ShaderUtil.anythingCompiling && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(50);
            if (ShaderUtil.anythingCompiling) throw new InvalidOperationException("Shaders are still compiling; capture refused.");
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            readback.Apply(false, false);
            var record = new CaptureRecord
            {
                image = fileName, pose = pose, normalizedTime = normalizedTime, lod = lod, silhouette = silhouette,
                lighting = lighting, yaw = yaw, renderer = cameraData.scriptableRenderer.GetType().FullName,
                framing = closeReview ? "CloseReview" : "Combat",
                cameraPosition = camera.transform.position, cameraEuler = camera.transform.eulerAngles, cameraFov = camera.fieldOfView,
                characters = records.ToArray(), lights = lights.Keys.Select(l => new LightRecord
                { name = l.name, type = l.type.ToString(), enabled = l.isActiveAndEnabled, color = l.color,
                    intensity = l.intensity, position = l.transform.position, euler = l.transform.eulerAngles }).ToArray(),
                note = "Existing URP camera/world at 1920x1080. Art-only clones at accepted enemy slots with review-prefab root scales. HUD excluded; no gameplay binding. " +
                    (closeReview ? "CloseReview changes only temporary camera framing to fit posed bounds; not combat-composition evidence. " : "Combat framing is preserved. ") +
                    "Current lighting is unchanged; other lighting selections are diagnostic isolation only. Black silhouettes retain world occlusion. " +
                    "Triangle/material counts are structural, not measured GPU cost. Capture generation is not visual approval."
            };
            Directory.CreateDirectory(directory);
            File.WriteAllText(sidecar, JsonUtility.ToJson(record, true));
            File.WriteAllBytes(path, readback.EncodeToPNG());
            return path;
        }
        finally
        {
            camera.targetTexture = null;
            camera.aspect = aspect;
            EditorJsonUtility.FromJsonOverwrite(cameraJson, camera);
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            // Leave automatic matrices automatic when transform/property restoration already matches.
            if (!camera.worldToCameraMatrix.Equals(viewMatrix)) camera.worldToCameraMatrix = viewMatrix;
            if (!camera.projectionMatrix.Equals(projectionMatrix)) camera.projectionMatrix = projectionMatrix;
            RenderTexture.active = previousActive;
            foreach (var pair in originals) if (pair.Key) pair.Key.enabled = pair.Value;
            foreach (var pair in canvases) if (pair.Key) pair.Key.enabled = pair.Value;
            foreach (var pair in lights) if (pair.Key) pair.Key.enabled = pair.Value;
            review.SetActive(reviewActive);
            if (transient) Object.DestroyImmediate(transient);
            if (black) Object.DestroyImmediate(black);
            if (target) { target.Release(); Object.DestroyImmediate(target); }
            if (readback) Object.DestroyImmediate(readback);
            SceneView.RepaintAll();
        }
    }

    private static Bounds PosedBounds(Renderer renderer)
    {
        var skin = renderer as SkinnedMeshRenderer;
        if (skin == null) return renderer.bounds;
        var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // Imported skin culling bounds are not reliable evidence of the sampled pose's silhouette.
            // useScale bakes the renderer's lossy scale into the vertices, so only position/rotation remain
            // (TransformPoint would apply the FBX hierarchy scale a second time).
            skin.BakeMesh(mesh, true);
            mesh.RecalculateBounds();
            var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            Vector3[] corners = Corners(mesh.bounds);
            var result = new Bounds(toWorld.MultiplyPoint3x4(corners[0]), Vector3.zero);
            foreach (Vector3 corner in corners.Skip(1)) result.Encapsulate(toWorld.MultiplyPoint3x4(corner));
            return result;
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    private static Vector3[] Corners(Bounds bounds)
    {
        var result = new Vector3[8];
        for (int i = 0; i < result.Length; i++)
            result[i] = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        return result;
    }

    private static void FrameBounds(Camera camera, Bounds bounds)
    {
        Quaternion inverse = Quaternion.Inverse(camera.transform.rotation);
        float tanY = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
        float tanX = tanY * camera.aspect;
        float distance = camera.nearClipPlane;
        foreach (Vector3 corner in Corners(bounds))
        {
            Vector3 point = inverse * (corner - bounds.center);
            distance = Mathf.Max(distance, Mathf.Abs(point.x) * 1.12f / tanX - point.z,
                Mathf.Abs(point.y) * 1.12f / tanY - point.z, camera.nearClipPlane * 2f - point.z);
        }
        camera.transform.position = bounds.center - camera.transform.forward * distance;
    }

    private static int Triangles(Renderer renderer)
    {
        var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
        int triangles = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += (int)(mesh.GetIndexCount(i) / 3);
        return triangles;
    }

    public static string WriteStats(string fileName = "unity-imported-stats.json")
    {
        if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Use a plain .json filename.");
        string directory = Path.GetFullPath(OutputDirectory);
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path)) throw new IOException("Statistics already exist: " + path);
        string json = DungeonRunEnemyArtLab.ReportJson();
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, json);
        return path;
    }
}
