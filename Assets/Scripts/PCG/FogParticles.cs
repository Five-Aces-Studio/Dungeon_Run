using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FogParticles : MonoBehaviour
{
    [Header("Fog Parameters")]
    [SerializeField] private Color fogColor = new Color(0.55f, 0.6f, 0.75f, 0.4f);
    [SerializeField] private float puffSize = 2.2f;
    [SerializeField] private float height = 0.9f;

    [Header("Emission Parameters")]
    [SerializeField] private float emitInterval = 0.45f;
    [SerializeField, Range(0f, 1f)] private float emitChance = 0.35f;
    [SerializeField] private float lifetime = 4f;

    public struct FogPoint
    {
        public Vector3 position;
        public float density;
    }

    private readonly List<FogPoint> coverage = new List<FogPoint>();
    private RandomWalkWFC generator;
    private ParticleSystem ps;
    private float timer;

    private void Awake()
    {
        generator = GetComponent<RandomWalkWFC>();
    }

    private void OnEnable()
    {
        if (generator != null) generator.OnGenerationComplete += HandleGenerationComplete;
    }

    private void OnDisable()
    {
        if (generator != null) generator.OnGenerationComplete -= HandleGenerationComplete;
    }

    private void HandleGenerationComplete()
    {
        if (ps == null) CreateSystem();
    }

    public void SetCoverage(List<FogPoint> points)
    {
        coverage.Clear();
        coverage.AddRange(points);
    }

    public void ResetFog()
    {
        coverage.Clear();
        if (ps != null) ps.Clear();
    }

    private void Update()
    {
        if (ps == null || coverage.Count == 0)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < emitInterval) return;
        timer = 0f;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        foreach (FogPoint point in coverage)
        {
            if (Random.value > emitChance * point.density) continue;

            Vector2 offset = Random.insideUnitCircle * (puffSize * 0.35f);
            emit.position = point.position + new Vector3(offset.x, height + Random.Range(-0.15f, 0.35f), offset.y);
            emit.startSize = puffSize * Random.Range(1f, 1.6f);
            emit.startLifetime = lifetime * Random.Range(0.8f, 1.2f);
            emit.rotation = Random.Range(0f, 360f);
            emit.velocity = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(0.01f, 0.05f), Random.Range(-0.12f, 0.12f));
            emit.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, fogColor.a * point.density);
            ps.Emit(emit, 1);
        }
    }

    private void CreateSystem()
    {
        Debug.Log("Creando particulas");
        GameObject go = new GameObject("FogPuffs");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.maxParticles = 4000;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-8f * Mathf.Deg2Rad, 8f * Mathf.Deg2Rad);

        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = CreateFogMaterial();
        psRenderer.shadowCastingMode = ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;
    }

    private static Material CreateFogMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetTexture("_BaseMap", CreateCloudTexture());
        mat.SetColor("_BaseColor", Color.white);
        return mat;
    }

    private static Texture2D CreateCloudTexture()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = (size - 1) * 0.5f;
        float noiseScale = 5f / size;
        Vector2 seed = new Vector2(Random.value, Random.value) * 100f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float falloff = Mathf.Exp(-(dx * dx + dy * dy) * 3.5f);
                float noise = Mathf.PerlinNoise(seed.x + x * noiseScale, seed.y + y * noiseScale);
                float a = Mathf.Clamp01(falloff * Mathf.Lerp(0.6f, 1f, noise));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }
}
