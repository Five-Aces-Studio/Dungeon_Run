using UnityEngine;

/// <summary>Explicit world-camera opt-in; presence alone does not enable pixel rendering.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class DungeonRunPixelRenderSettings : MonoBehaviour
{
    [SerializeField] private bool pixelEnabled;
    [SerializeField] private Vector2Int virtualResolution = new Vector2Int(480, 270);

    public bool PixelEnabled { get => pixelEnabled; set => pixelEnabled = value; }
    public Vector2Int VirtualResolution
    {
        get => new Vector2Int(Mathf.Max(1, virtualResolution.x), Mathf.Max(1, virtualResolution.y));
        set => virtualResolution = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
    }

    private void OnValidate() => VirtualResolution = virtualResolution;
}
