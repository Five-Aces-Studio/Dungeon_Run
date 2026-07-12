using UnityEngine;

public enum CellVisualState { Fog, Penumbra, Revealed, Adjacent, Active }

[DisallowMultipleComponent]
public class HexCellVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color FogBaseColor = new Color(0.04f, 0.045f, 0.06f);

    private Renderer renderer;
    private MaterialPropertyBlock mpb;
    private Color baseColor = Color.white;
    private Color emissionColor = Color.white;
    private float intensity = 1.2f;
    private CellVisualState state = CellVisualState.Fog;

    public CellVisualState State => state;

    public void Configure(Color emission, float emissionIntensity)
    {
        CacheRenderer();
        emissionColor = emission;
        intensity = emissionIntensity;
        Apply();
    }

    public void SetState(CellVisualState newState)
    {
        state = newState;
        Apply();
    }

    private void CacheRenderer()
    {
        if (renderer != null) return;
        renderer = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(BaseColorId))
            baseColor = renderer.sharedMaterial.GetColor(BaseColorId);
    }

    private void Apply()
    {
        if (renderer == null) return;
        switch (state)
        {
            case CellVisualState.Active:
            case CellVisualState.Adjacent:
            case CellVisualState.Revealed:
                ApplyColors(baseColor, intensity);
                break;
            case CellVisualState.Penumbra: ApplyColors(baseColor, intensity * 0.2f); break;
            default:                       ApplyColors(FogBaseColor, 0f); break;
        }
    }

    private void ApplyColors(Color baseCol, float emissionIntensity)
    {
        mpb.SetColor(BaseColorId, baseCol);
        mpb.SetColor(EmissionColorId, emissionColor * emissionIntensity);
        renderer.SetPropertyBlock(mpb);
    }
}
