using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class ProceduralScenesTests
{
    public static IEnumerable<object[]> SceneCases()
    {
        yield return new object[] { new GameOfLifeScene(), true, 30d };
        yield return new object[] { new StarfieldParallaxScene(), true, 30d };
        yield return new object[] { new PlasmaSdfScene(), true, 30d };
        yield return new object[] { new MetaballsScene(), true, 30d };
        yield return new object[] { new DonkeyKongScene(), true, 70d };
        yield return new object[] { new BonkersParadeScene(), true, 30d };
        yield return new object[] { new MatrixRainScene(), true, 30d };
        yield return new object[] { new SynthwaveGridScene(), true, 30d };
        yield return new object[] { new OrbitalScene(), true, 30d };
        yield return new object[] { new FireworksScene(), true, 30d };
        yield return new object[] { new WarpCoreScene(), false, 30d };
        yield return new object[] { new WarpCoreScene(WarpCorePalettePreset.RedAlert), false, 30d };
    }

    [Theory]
    [MemberData(nameof(SceneCases))]
    public void Scene_Activates_Draws_AndExpires(
        ISpecialScene scene,
        bool hidesTimeWhileActive,
        double expirationSeconds)
    {
        scene.Activate();

        Assert.True(scene.IsActive);
        Assert.False(scene.RainbowSnow);
        Assert.Equal(hidesTimeWhileActive, scene.HidesTime);

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(200));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);

        scene.Elapsed(TimeSpan.FromSeconds(expirationSeconds));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }
}
