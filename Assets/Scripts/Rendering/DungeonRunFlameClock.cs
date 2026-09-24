using UnityEngine;

/// <summary>
/// Shared clock for painted flames: the flame shader (via the World Style feature's _DR_FlameTime global) and every
/// DungeonRunFlameLight read the same time, so captures can freeze flame shape and light flicker deterministically.
/// </summary>
public static class DungeonRunFlameClock
{
    /// <summary>Deterministic override for captures; null = live time.</summary>
    public static float? OverrideTime;

    /// <summary>Current flame time in seconds: the override when set, else play time (or editor real time).</summary>
    public static float Now => OverrideTime ?? (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
}
