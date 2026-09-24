using System.Collections.Generic;
using DungeonRun.Rendering;
using UnityEngine;

/// <summary>
/// Drives one painted torch: flickers its point light (intensity, range, small position jitter) and its flame/halo
/// renderers from the same deterministic FlameFlicker signal, and publishes a painted glow pool that the World Style
/// feature pushes to acrylic materials. Only animates in Play Mode; Edit Mode changes happen solely through explicit
/// Evaluate/RestoreBase calls (capture harness, Apply preset).
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class DungeonRunFlameLight : MonoBehaviour
{
    public enum FlameKind { Warm, Cold }

    [Tooltip("Warm (amber torch) or Cold (blue arcane fixture). Selects which profile torch/pool values Apply writes.")]
    public FlameKind kind;
    [Tooltip("Point light flickered in sync with the flame. Optional.")]
    public Light targetLight;
    [Tooltip("Flame card and halo renderers that receive _FlameFlicker / _FlameSeed through a property block.")]
    public Renderer[] flameRenderers;

    [Header("Light Flicker")]
    [Tooltip("Light intensity at rest (flicker 0). Written by an Acrylic V3 Apply preset.")]
    public float baseIntensity = 30f;
    [Tooltip("Light range (metres) at rest. Written by an Acrylic V3 Apply preset.")]
    public float baseRange = 13f;
    [Tooltip("Relative intensity swing: intensity = base * (1 + this * flicker). Also scales the flame card height/brightness.")]
    [Range(0, 1)] public float intensityFlicker = .22f;
    [Tooltip("Relative range swing in sync with the intensity flicker.")]
    [Range(0, .5f)] public float rangeFlicker = .05f;
    [Tooltip("Metres. Small light position wobble so shadows and pools breathe with the flame.")]
    [Range(0, .2f)] public float positionJitter = .04f;
    [Tooltip("Light local position at rest; the jitter is applied around it.")]
    public Vector3 baseLightLocalPosition;
    [Tooltip("Flicker time scale. 1 = default flame tempo.")]
    [Range(.1f, 4f)] public float flickerSpeed = 1f;
    [Tooltip("Per-torch seed so neighbouring flames never flicker in unison.")]
    public int seed;

    [Header("Painted Glow Pool")]
    [Tooltip("Painted glow pool colour (authored sRGB). Written by an Acrylic V3 Apply preset.")]
    public Color poolColor = new Color(1f, .6f, .3f);
    [Tooltip("Painted glow pool intensity (multiplied by the current flicker).")]
    [Range(0, 4)] public float poolIntensity = 1.2f;
    [Tooltip("Painted glow pool radius (metres).")]
    [Range(.5f, 20f)] public float poolRadius = 7f;
    [Tooltip("Pool centre relative to this torch (local space); usually below and in front of the fixture.")]
    public Vector3 poolLocalOffset = new Vector3(0, -2.5f, -.6f);

    /// <summary>1 + intensityFlicker * flicker for the last Evaluate; 1 at rest.</summary>
    public float CurrentFlicker { get; private set; } = 1f;

    public Vector3 PoolWorldPosition => transform.TransformPoint(poolLocalOffset);

    private static readonly List<DungeonRunFlameLight> ActiveList = new List<DungeonRunFlameLight>();
    public static IReadOnlyList<DungeonRunFlameLight> Active => ActiveList;

    private static readonly int FlameFlickerId = Shader.PropertyToID("_FlameFlicker");
    private static readonly int FlameSeedId = Shader.PropertyToID("_FlameSeed");
    // Created lazily: MaterialPropertyBlock must not be constructed during MonoBehaviour deserialization.
    private static MaterialPropertyBlock flameBlock;

    // True while the light/renderers hold flickered values that RestoreBase has not reset yet.
    private bool flickerApplied;

    private void OnEnable()
    {
        if (!ActiveList.Contains(this)) ActiveList.Add(this);
        ApplyFlameBlock(1f); // per-torch seed also in Edit Mode (property blocks are not serialized)
    }

    private void OnDisable()
    {
        ActiveList.Remove(this);
        // Only undo our own flicker: never rewrite serialized light values that Evaluate did not touch.
        if (flickerApplied) RestoreBase();
    }

    private void Update()
    {
        if (Application.isPlaying) Evaluate(DungeonRunFlameClock.Now);
    }

    public void Evaluate(float time)
    {
        float f = FlameFlicker.Evaluate(time * flickerSpeed, seed);
        float f2 = FlameFlicker.Evaluate(time * flickerSpeed * 1.37f + 17f, seed + 101);
        CurrentFlicker = 1f + intensityFlicker * f;
        if (targetLight != null)
        {
            targetLight.intensity = baseIntensity * CurrentFlicker;
            targetLight.range = baseRange * (1f + rangeFlicker * f);
            targetLight.transform.localPosition = baseLightLocalPosition +
                new Vector3(f2, Mathf.Abs(f) * .5f, f * .3f) * positionJitter;
        }
        ApplyFlameBlock(1f + .9f * intensityFlicker * f);
        flickerApplied = true;
    }

    public void RestoreBase()
    {
        CurrentFlicker = 1f;
        if (targetLight != null)
        {
            targetLight.intensity = baseIntensity;
            targetLight.range = baseRange;
            targetLight.transform.localPosition = baseLightLocalPosition;
        }
        ApplyFlameBlock(1f);
        flickerApplied = false;
    }

    public static void EvaluateAll(float time)
    {
        for (int i = 0; i < ActiveList.Count; i++)
            if (ActiveList[i] != null) ActiveList[i].Evaluate(time);
    }

    public static void RestoreAll()
    {
        for (int i = 0; i < ActiveList.Count; i++)
            if (ActiveList[i] != null) ActiveList[i].RestoreBase();
    }

    private void ApplyFlameBlock(float flameFlicker)
    {
        if (flameRenderers == null) return;
        if (flameBlock == null) flameBlock = new MaterialPropertyBlock();
        foreach (var flameRenderer in flameRenderers)
        {
            if (flameRenderer == null) continue;
            flameRenderer.GetPropertyBlock(flameBlock);
            flameBlock.SetFloat(FlameFlickerId, flameFlicker);
            flameBlock.SetFloat(FlameSeedId, seed);
            flameRenderer.SetPropertyBlock(flameBlock);
        }
    }
}
