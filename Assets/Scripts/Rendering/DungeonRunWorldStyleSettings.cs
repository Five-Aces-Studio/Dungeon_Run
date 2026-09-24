using UnityEngine;

/// <summary>Explicit world-camera opt-in for the Acrylic + Subtle Pixel V3 fullscreen pass.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class DungeonRunWorldStyleSettings : MonoBehaviour
{
    [Tooltip("The profile driving this camera's World Style pass. None disables the pass for this camera.")]
    public DungeonRunWorldStyleProfile profile;
}
