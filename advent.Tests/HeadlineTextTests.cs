using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class HeadlineTextTests
{
    // Golden rows from the deployed clock before the shared-font extraction.
    public static TheoryData<string, string[]> LegacyClockRows => new()
    {
        { "12:34", ["000110000111110000000011111000000110", "001110001100011001100110001100001110", "010110000000011001100000001100011110", "000110000000110000000000001100110110", "000110000011100000000001111001100110", "000110000110000000000000001101111111", "000110001100000001100000001100000110", "000110001100011001100110001100000110", "011111101111111000000011111000000110"] },
        { "08:59", ["011111000111110000000111111100111110", "110001101100011001100110000001100011", "110001101100011001100110000001100011", "110001101100011000000111111001100011", "110001100111110000000000001100111111", "110001101100011000000000001100000011", "110001101100011001100000001100000011", "110001101100011001100110001101100011", "011111000111110000000011111000111110"] },
        { "23:07", ["011111000111110000000011111001111111", "110001101100011001100110001101100011", "000001100000011001100110001100000110", "000011000000011000000110001100000110", "001110000011110000000110001100001100", "011000000000011000000110001100001100", "110000000000011001100110001100011000", "110001101100011001100110001100011000", "111111100111110000000011111000011000"] }
    };

    [Theory]
    [MemberData(nameof(LegacyClockRows))]
    public void SharedTimeDigits_MatchDeployedClockPixels(string time, string[] rows)
    {
        using var image = new Image<Rgba32>(36, 9);
        HeadlineText.DrawTime(image, time, 0, 0, Color.White);
        for (var y = 0; y < 9; y++)
        for (var x = 0; x < 36; x++)
            Assert.Equal(rows[y][x] == '1', image[x, y].R != 0);
    }

    [Fact]
    public void ClockAndRail_KeepTheSameTimeGeometry()
    {
        using var clock = new Image<Rgba32>(64, 32, Color.Black);
        using var rail = new Image<Rgba32>(64, 32, Color.Black);
        new ClockRenderer().DrawAt(clock, new DateTime(2026, 9, 21, 18, 12, 0));
        ClockRenderer.DrawTimeDigits(rail, "18:12", 14, 3, new Rgba32(220, 230, 255), new Rgba32(150, 165, 200));
        for (var y = 3; y < 12; y++)
        for (var x = 0; x < 64; x++)
            Assert.Equal(clock[x, y], rail[x, y]);
    }
}
