using System;

namespace DungeonRun.Rendering
{
    /// <summary>Pure predicate deciding whether the World Style fullscreen pass should render this camera.</summary>
    public static class WorldStyleGate
    {
        public static bool ShouldRender(string sceneName, bool isGameCamera, bool isBaseCamera, bool stereo,
            bool orthographic, bool settingsActive, bool hasProfile, bool acrylicLook, bool legacyPixelEnabled)
        {
            return sceneName == "SceneVictorLab" && isGameCamera && isBaseCamera && !stereo && !orthographic &&
                settingsActive && hasProfile && acrylicLook && !legacyPixelEnabled;
        }
    }
}
