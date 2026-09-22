using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class ErrorTextLayoutTests
{
    [Fact]
    public void GlitchNoise_DoesNotOverwriteTheTitle()
    {
        using var reference = Draw(1);
        for (var seed = 2; seed < 32; seed++)
        {
            using var image = Draw(seed);
            for (var y = 1; y <= 8; y++)
            for (var x = 1; x < 63; x++)
                Assert.Equal(reference[x, y], image[x, y]);
        }
    }

    private static Image<Rgba32> Draw(int seed)
    {
        var scene = new ErrorScene();
        scene.Activate();
        typeof(ErrorScene).GetField("noiseSeed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, seed);
        scene.Elapsed(TimeSpan.FromSeconds(3.5));
        var image = new Image<Rgba32>(64, 32);
        scene.Draw(image);
        return image;
    }
}
