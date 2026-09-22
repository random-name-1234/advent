using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class MoonlitLandscapeScene(TimeProvider? timeProvider = null) : PixelStoryScene("Moonlit Landscape")
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    internal double Phase { get; private set; }

    public override void Activate()
    {
        base.Activate();
        Phase = CalculatePhase(clock.GetUtcNow());
    }

    // Mean-lunation approximation. NASA reference new moon: 2000-01-06 18:14 UTC.
    // https://eclipse.gsfc.nasa.gov/phase/phases1901.html
    internal static double CalculatePhase(DateTimeOffset utc)
    {
        var cycles = (utc - new DateTimeOffset(2000, 1, 6, 18, 14, 0, TimeSpan.Zero)).TotalDays / 29.530588;
        return cycles - Math.Floor(cycles);
    }

    internal static bool IsLit(double x, double y, double phase)
    {
        var z = Math.Sqrt(Math.Max(0, 1 - x * x - y * y));
        var angle = phase * Math.Tau;
        return x * Math.Sin(angle) - z * Math.Cos(angle) > 0;
    }

    protected override void Render(Image<Rgba32> image)
    {
        Sky(image, Color(2, 5, 18), Color(20, 34, 53));
        for (var i = 0; i < 14; i++)
            Dot(image, (i * 19 + 3) % 64, (i * 7 + 1) % 15, Color(74 + i % 3 * 19, 99, 119));
        for (var y = -6; y <= 6; y++)
        for (var x = -6; x <= 6; x++)
        {
            if (x * x + y * y > 36) continue;
            var lit = IsLit(x / 6.1, y / 6.1, Phase);
            var crater = (x == 2 && y is >= -2 and <= 0) || (x is >= -3 and <= -1 && y == 2);
            Dot(image, 43 + x, 9 + y, lit ? crater ? Color(159, 173, 156) : Color(212, 220, 190) : Color(24, 32, 47));
        }
        Cloud(image, Wrap(Seconds * .55 + 12, 86) - 14, 12, Color(31, 44, 61));
        for (var x = 0; x < 64; x++)
        {
            var hill = 20 + (int)(Math.Sin(x * .1) * 3 + Math.Sin(x * .23));
            Box(image, x, hill, 1, 32 - hill, Color(16, 35, 43));
            var near = 24 + (int)(Math.Sin(x * .13 + 1.5) * 3);
            Box(image, x, near, 1, 32 - near, Color(8, 23, 31));
        }
        Box(image, 0, 26, 64, 6, Color(7, 22, 38));
        var illumination = (1 - Math.Cos(Phase * Math.Tau)) / 2;
        for (var y = 27; y < 32; y++)
        {
            var x = 42 + (int)(Math.Sin(Seconds * .8 + y * 2) * 3);
            Box(image, x - (y - 25), y, (y - 25) * 2, 1,
                Mix(Color(9, 28, 43), Color(104, 135, 140), illumination * .7));
        }
        Box(image, 2, 25, 1, 7, Color(3, 12, 19));
        Box(image, 4, 28, 1, 4, Color(3, 12, 19));
    }
}
