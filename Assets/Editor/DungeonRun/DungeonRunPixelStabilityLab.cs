using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Disposable, synchronous lab captures. Never saves scenes, assets or gameplay state.</summary>
public static class DungeonRunPixelStabilityLab
{
    public const string CoverageShader = "DungeonRun/SH_DungeonRun_PixelCoverageTest";
    private const int Width = 1920, Height = 1080;

    [Serializable]
    private sealed class FrameRecord
    {
        public int frame;
        public float normalizedTime, requestedOffset, appliedOffset;
        public Vector3 cameraPosition, playerPosition, playerEulerAngles;
        public Vector3 characterPosition, characterEulerAngles;
        public string image;
    }

    [Serializable]
    private sealed class SequenceRecord
    {
        public string mode, motion, trajectory = "Linear -amplitude to +amplitude; normalized time, no realtime benchmark";
        public string characterName;
        public float amplitude, referenceDepth, referencePixelPitch, verticalFov;
        public int outputWidth = Width, outputHeight = Height;
        public int renderWidth, renderHeight, virtualWidth = 480, virtualHeight = 270;
        public FrameRecord[] frames;
    }

    /// <summary>
    /// Modes: C0, CameraSnap, Coverage4, TrueLowRes. Motions: camera_x, camera_y,
    /// character_x, character_z, character_yaw. Amplitude is half the total excursion in world units (yaw: degrees).
    /// One-frame sequences capture the unmodified pose. Output must be a new/empty directory.
    /// </summary>
    public static string CaptureSequence(string mode, string motion, float amplitude, int frames, string outputDir,
        string characterName = "CHR_Player")
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonRunStylizedLighting.LabPath || scene.isDirty ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Require clean SceneVictorLab in stable Edit Mode.");
        if (!new[] { "C0", "CameraSnap", "Coverage4", "TrueLowRes" }.Contains(mode) ||
            !new[] { "camera_x", "camera_y", "character_x", "character_z", "character_yaw" }.Contains(motion) ||
            float.IsNaN(amplitude) || float.IsInfinity(amplitude) || amplitude < 0 || amplitude > (motion == "character_yaw" ? 15 : 2) ||
            frames < 1 || frames > 120)
            throw new ArgumentException("Invalid mode/motion, amplitude (translation 0..2, yaw 0..15), or frames (1..120).");
        var root = scene.GetRootGameObjects().Single(x => x.name == "TrigonalAbyss_Prototype");
        var camera = root.transform.Find("CombatCamera").GetComponent<Camera>();
        if (!new[] { "CHR_Player", "CHR_Enemy_01", "CHR_Enemy_02", "CHR_Enemy_03" }.Contains(characterName))
            throw new ArgumentException("Unknown capture character.");
        var actualPlayer = root.GetComponentsInChildren<Transform>(true).Single(x => x.name == "CHR_Player");
        var player = root.GetComponentsInChildren<Transform>(true).Single(x => x.name == characterName);
        var settings = camera.GetComponent<DungeonRunPixelRenderSettings>();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            "Assets/Settings/PC_LabPixel_Renderer.asset");
        var feature = renderer.rendererFeatures.OfType<DungeonRunPixelRenderFeature>().Single();
        if (!settings || !settings.isActiveAndEnabled || !settings.PixelEnabled ||
            settings.VirtualResolution != new Vector2Int(480, 270) || !feature.isActive ||
            feature.material == null || feature.material.shader.name != DungeonRunPixelRendering.ShaderName ||
            camera.orthographic || camera.targetTexture != null)
            throw new InvalidOperationException("Require intact perspective C480 baseline with no camera target.");
        var coverage = mode == "Coverage4" ? Shader.Find(CoverageShader) : null;
        if (mode == "Coverage4" && (!coverage || !coverage.isSupported || ShaderUtil.ShaderHasError(coverage)))
            throw new InvalidOperationException("Coverage test shader is unavailable or failed compilation.");
        string directory = Path.GetFullPath(outputDir);
        string captureRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Captures/PixelStabilityV11"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(captureRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must be inside Captures/PixelStabilityV11.");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new IOException("Refusing to overwrite an existing capture sequence.");

        Vector3 cameraLocalPosition = camera.transform.localPosition;
        Quaternion cameraLocalRotation = camera.transform.localRotation;
        Vector3 playerLocalPosition = player.localPosition;
        Quaternion playerLocalRotation = player.localRotation;
        Vector3 cameraOrigin = camera.transform.position, playerOrigin = player.position;
        Vector3 cameraRight = camera.transform.right, cameraUp = camera.transform.up;
        float aspect = camera.aspect;
        RenderTexture target = camera.targetTexture, active = RenderTexture.active;
        bool pixelEnabled = settings.PixelEnabled;
        Vector2Int resolution = settings.VirtualResolution;
        Material sourceMaterial = feature.material, temporaryMaterial = null;
        RenderTexture renderTarget = null;
        Texture2D readback = null, expanded = null;
        float depth = camera.transform.InverseTransformPoint(playerOrigin).z;
        if (depth <= camera.nearClipPlane) throw new InvalidOperationException("Invalid reference-player depth.");
        float pitch = 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / 270;
        bool lowResolution = mode == "TrueLowRes";
        int width = lowResolution ? 480 : Width, height = lowResolution ? 270 : Height;
        var manifest = new SequenceRecord
        {
            mode = mode, motion = motion, characterName = characterName, amplitude = amplitude, referenceDepth = depth,
            referencePixelPitch = pitch, verticalFov = camera.fieldOfView,
            renderWidth = width, renderHeight = height, frames = new FrameRecord[frames]
        };
        try
        {
            Directory.CreateDirectory(directory);
            if (coverage)
            {
                temporaryMaterial = new Material(coverage) { hideFlags = HideFlags.HideAndDontSave };
                feature.material = temporaryMaterial;
            }
            settings.PixelEnabled = !lowResolution;
            renderTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "PixelStabilityLabCapture", antiAliasing = 1, filterMode = FilterMode.Point,
                useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.HideAndDontSave
            };
            renderTarget.Create();
            readback = new Texture2D(width, height, TextureFormat.RGB24, false, false)
                { hideFlags = HideFlags.HideAndDontSave };
            if (lowResolution)
                expanded = new Texture2D(Width, Height, TextureFormat.RGB24, false, false)
                    { hideFlags = HideFlags.HideAndDontSave };
            camera.aspect = (float)Width / Height;
            camera.targetTexture = renderTarget;
            for (int frame = 0; frame < frames; frame++)
            {
                float requested = frames == 1 ? 0 : Mathf.Lerp(-amplitude, amplitude, frame / (float)(frames - 1));
                float applied = mode == "CameraSnap" && motion.StartsWith("camera_")
                    ? Mathf.Round(requested / pitch) * pitch : requested;
                camera.transform.position = cameraOrigin;
                player.position = playerOrigin;
                if (motion == "camera_x") camera.transform.position += cameraRight * applied;
                else if (motion == "camera_y") camera.transform.position += cameraUp * applied;
                else if (motion == "character_x") player.position += Vector3.right * applied;
                else if (motion == "character_z") player.position += Vector3.forward * applied;
                else player.localRotation = playerLocalRotation * Quaternion.Euler(0, applied, 0);
                // Two renders settle transient allocations after a target/material switch.
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTarget;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false, false);
                Texture2D output = readback;
                if (lowResolution)
                {
                    Color32[] small = readback.GetPixels32();
                    var large = new Color32[Width * Height];
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                            large[y * Width + x] = small[(y / 4) * width + x / 4];
                    expanded.SetPixels32(large);
                    expanded.Apply(false, false);
                    output = expanded;
                }
                string file = $"frame-{frame:D3}.png";
                File.WriteAllBytes(Path.Combine(directory, file), output.EncodeToPNG());
                manifest.frames[frame] = new FrameRecord
                {
                    frame = frame, normalizedTime = frames == 1 ? 0 : frame / (float)(frames - 1), requestedOffset = requested,
                    appliedOffset = applied, cameraPosition = camera.transform.position,
                    playerPosition = actualPlayer.position, playerEulerAngles = actualPlayer.eulerAngles,
                    characterPosition = player.position, characterEulerAngles = player.eulerAngles, image = file
                };
            }
            string result = Path.Combine(directory, "manifest.json");
            File.WriteAllText(result, JsonUtility.ToJson(manifest, true));
            return result;
        }
        finally
        {
            camera.transform.localPosition = cameraLocalPosition;
            camera.transform.localRotation = cameraLocalRotation;
            player.localPosition = playerLocalPosition;
            player.localRotation = playerLocalRotation;
            camera.targetTexture = target;
            camera.aspect = aspect;
            settings.PixelEnabled = pixelEnabled;
            settings.VirtualResolution = resolution;
            feature.material = sourceMaterial;
            RenderTexture.active = active;
            if (renderTarget) { renderTarget.Release(); UnityEngine.Object.DestroyImmediate(renderTarget); }
            if (readback) UnityEngine.Object.DestroyImmediate(readback);
            if (expanded) UnityEngine.Object.DestroyImmediate(expanded);
            if (temporaryMaterial) UnityEngine.Object.DestroyImmediate(temporaryMaterial);
        }
    }
}

