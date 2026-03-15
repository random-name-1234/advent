using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class ProceduralScenesTests
{
    public static IEnumerable<object[]> SceneCases()
    {
        yield return new object[] { new GameOfLifeScene(), true };
        yield return new object[] { new StarfieldParallaxScene(), true };
        yield return new object[] { new PlasmaSdfScene(), true };
        yield return new object[] { new MatrixRainScene(), true };
        yield return new object[] { new SynthwaveGridScene(), true };
        yield return new object[] { new OrbitalScene(), true };
        yield return new object[] { new FireworksScene(), true };
        yield return new object[] { new WarpCoreScene(), false };
        yield return new object[] { new WarpCoreScene(WarpCorePalettePreset.RedAlert), false };
    }

    [Theory]
    [MemberData(nameof(SceneCases))]
    public void Scene_Activates_Draws_AndExpires(ISpecialScene scene, bool hidesTimeWhileActive)
    {
        scene.Activate();

        Assert.True(scene.IsActive);
        Assert.False(scene.RainbowSnow);
        Assert.Equal(hidesTimeWhileActive, scene.HidesTime);

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(200));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);

        scene.Elapsed(TimeSpan.FromSeconds(30));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }
}
