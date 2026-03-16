using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class PixelTextTests
{
    [Fact]
    public void Draw_RendersExpectedGlyphShape()
    {
        using var canvas = new Image<Rgba32>(8, 5);

        PixelText.Draw(canvas, "A", 1, 0, new Rgba32(255, 255, 255));

        Assert.Equal(12, CountLitPixels(canvas));
        AssertLit(canvas, 2, 0);
        AssertLit(canvas, 3, 0);
        AssertLit(canvas, 1, 1);
        AssertLit(canvas, 4, 1);
        AssertLit(canvas, 1, 2);
        AssertLit(canvas, 2, 2);
        AssertLit(canvas, 3, 2);
        AssertLit(canvas, 4, 2);
        AssertLit(canvas, 1, 4);
        AssertLit(canvas, 4, 4);
    }

    [Fact]
    public void TrimToWidth_NormalizesAndTrims()
    {
        var maxWidth = PixelText.MeasureWidth("CAM");

        var trimmed = PixelText.TrimToWidth("cambridge", maxWidth);

        Assert.Equal("CAM", trimmed);
        Assert.True(PixelText.MeasureWidth(trimmed) <= maxWidth);
    }

    private static void AssertLit(Image<Rgba32> image, int x, int y)
    {
        var pixel = image[x, y];
        Assert.True(pixel.R != 0 || pixel.G != 0 || pixel.B != 0, $"Expected pixel {x},{y} to be lit.");
    }

    private static int CountLitPixels(Image<Rgba32> image)
    {
        var litPixels = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                litPixels++;
        }

        return litPixels;
    }
}
