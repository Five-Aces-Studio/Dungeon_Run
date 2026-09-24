using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Exact legacy (LegacyPixel/CurrentPixel) SceneVictorLab state, captured once by Install() and restored
/// byte-for-byte by Apply(CurrentPixel). Never mutated after creation except by re-running Install on a legacy scene.</summary>
public sealed class DungeonRunAcrylicLegacySnapshot : ScriptableObject
{
    [Serializable]
    public sealed class RendererEntry
    {
        public string hierarchyPath;
        public Material[] sharedMaterials;
    }

    [Serializable]
    public sealed class LightEntry
    {
        public string hierarchyPath;
        public bool enabled;
        public Color color;
        public float intensity;
        public float range;
        public float shadowStrength;
        public LightShadows shadows;
    }

    [Serializable]
    public sealed class FogState
    {
        public bool enabled;
        public FogMode mode;
        public Color color;
        public float start;
        public float end;
        public float density;
    }

    [Serializable]
    public sealed class AmbientState
    {
        public AmbientMode mode;
        public Color sky;
        public Color equator;
        public Color ground;
        public float intensity;
    }

    public RendererEntry[] renderers = Array.Empty<RendererEntry>();
    public LightEntry[] lights = Array.Empty<LightEntry>();
    public FogState fog = new FogState();
    public AmbientState ambient = new AmbientState();
    public bool cameraRenderPostProcessing;
    public bool pixelEnabled;
    public Vector2Int virtualResolution = new Vector2Int(480, 270);
}
