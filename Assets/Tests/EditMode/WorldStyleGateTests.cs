using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class WorldStyleGateTests
{
    private static bool Call(
        string sceneName = "SceneVictorLab",
        bool isGameCamera = true,
        bool isBaseCamera = true,
        bool stereo = false,
        bool orthographic = false,
        bool settingsActive = true,
        bool hasProfile = true,
        bool acrylicLook = true,
        bool legacyPixelEnabled = false) =>
        WorldStyleGate.ShouldRender(sceneName, isGameCamera, isBaseCamera, stereo, orthographic, settingsActive,
            hasProfile, acrylicLook, legacyPixelEnabled);

    [Test]
    public void HappyPath_ReturnsTrue()
    {
        Assert.IsTrue(Call());
    }

    [Test]
    public void WrongScene_ReturnsFalse() => Assert.IsFalse(Call(sceneName: "SceneVictor"));

    [Test]
    public void NotGameCamera_ReturnsFalse() => Assert.IsFalse(Call(isGameCamera: false));

    [Test]
    public void NotBaseCamera_ReturnsFalse() => Assert.IsFalse(Call(isBaseCamera: false));

    [Test]
    public void Stereo_ReturnsFalse() => Assert.IsFalse(Call(stereo: true));

    [Test]
    public void Orthographic_ReturnsFalse() => Assert.IsFalse(Call(orthographic: true));

    [Test]
    public void SettingsInactive_ReturnsFalse() => Assert.IsFalse(Call(settingsActive: false));

    [Test]
    public void NoProfile_ReturnsFalse() => Assert.IsFalse(Call(hasProfile: false));

    [Test]
    public void NotAcrylicLook_ReturnsFalse() => Assert.IsFalse(Call(acrylicLook: false));

    [Test]
    public void LegacyPixelEnabled_ReturnsFalse() => Assert.IsFalse(Call(legacyPixelEnabled: true));
}
