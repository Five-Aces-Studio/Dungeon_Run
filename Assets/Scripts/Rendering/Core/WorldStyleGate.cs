using System;

namespace DungeonRun.Rendering
{
    /// <summary>Pure predicate deciding whether the World Style fullscreen pass should render this camera.</summary>
    public static class WorldStyleGate
    {
        /// <summary>Scenes that use the acrylic world style: the main scene and the shared lab scene.</summary>
        public static bool IsStyledScene(string sceneName) => sceneName == "SceneVictor" || sceneName == "SceneVictorLab" || sceneName == "SceneGuilleMaze";

        public static bool ShouldRender(string sceneName, bool isGameCamera, bool isBaseCamera, bool stereo,
            bool orthographic, bool settingsActive, bool hasProfile, bool acrylicLook, bool legacyPixelEnabled)
        {
            return IsStyledScene(sceneName) && isGameCamera && isBaseCamera && !stereo && !orthographic &&
                settingsActive && hasProfile && acrylicLook && !legacyPixelEnabled;
        }
    }
}
