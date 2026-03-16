using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailDmiTextTests
{
    [Fact]
    public void Draw_RendersCuratedKGlyph()
    {
        using var canvas = new Image<Rgba32>(4, 5);

        RailDmiText.Draw(canvas, "K", 0, 0, new Rgba32(255, 255, 255));

        Assert.Equal(10, CountLitPixels(canvas));
        AssertLit(canvas, 0, 0);
        AssertLit(canvas, 3, 0);
        AssertLit(canvas, 2, 1);
        AssertLit(canvas, 1, 2);
        AssertLit(canvas, 2, 3);
        AssertLit(canvas, 3, 4);
    }

    [Fact]
    public void TrimToWidth_UsesCuratedGlyphMetrics()
    {
        var maxWidth = RailDmiText.MeasureWidth("Kings");

        var trimmed = RailDmiText.TrimToWidth("Kings cross", maxWidth);

        Assert.Equal("Kings", trimmed);
        Assert.True(RailDmiText.MeasureWidth("W") > RailDmiText.MeasureWidth("I"));
        Assert.True(RailDmiText.MeasureWidth(trimmed) <= maxWidth);
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
