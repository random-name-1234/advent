using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class PixelCityScene(
    TimeProvider? timeProvider = null, double latitude = 52.2053, double longitude = .1218)
    : PixelStoryScene("Pixel City")
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    internal bool IsNight { get; private set; }

    public override void Activate()
    {
        base.Activate();
        var now = clock.GetLocalNow();
        var (rise, set) = SunriseSunsetScene.CalculateSunTimes(now, latitude, longitude);
        IsNight = now.TimeOfDay.TotalHours < rise || now.TimeOfDay.TotalHours >= set;
    }

    protected override void Render(Image<Rgba32> image)
    {
        Sky(image, IsNight ? Color(3, 7, 22) : Color(33, 87, 121), IsNight ? Color(27, 35, 55) : Color(127, 169, 180));
        Disc(image, 51, 5, 2, IsNight ? Color(177, 192, 174) : Color(250, 213, 117));
        Cloud(image, Wrap(Seconds * .5 + 4, 85) - 14, 4, IsNight ? Color(26, 36, 54) : Color(152, 192, 204));
        for (var i = 0; i < 6; i++)
        {
            var x = i * 12 - 3;
            var top = 10 + i % 3 * 3;
            var wall = IsNight ? Color(37 + i * 3, 40, 51) : Color(97 + i * 11, 80 + i * 5, 70 + i * 3);
            Box(image, x, top, 10, 15, wall);
            Box(image, x - 1, top - 1, 12, 1, Mix(wall, Color(8, 14, 20), .4));
            Box(image, x + 7, top - 4, 2, 3, wall);
            for (var row = 0; row < 2; row++)
            for (var col = 0; col < 2; col++)
                Box(image, x + 2 + col * 4, top + 2 + row * 5, 2, 3,
                    IsNight && (i + row + col + (int)(Seconds / 6)) % 4 != 0 ? Color(228, 171, 74) : Color(27, 53, 65));
        }
        Box(image, 0, 24, 64, 2, Color(101, 102, 96));
        Box(image, 0, 26, 64, 6, Color(22, 28, 34));
        for (var x = 3; x < 64; x += 13) Box(image, x, 30, 5, 1, Color(147, 140, 103));
        for (var p = 0; p < 3; p++)
        {
            var x = p == 1 ? 63 - Wrap(Seconds * 1.1 + 11, 70) : Wrap(p * 22 + Seconds * .8, 70) - 3;
            Dot(image, x, 21, Color(204, 159, 123));
            Box(image, x, 22, 1, 2, p == 1 ? Color(183, 110, 59) : Color(64, 157, 170));
            Dot(image, x + ((int)(Seconds * 3 + p) % 2), 24, Color(22, 25, 37));
        }
        var bus = Wrap(Seconds * 5 + 7, 95) - 23;
        Box(image, bus, 20, 19, 9, Color(172, 39, 35));
        for (var row = 0; row < 2; row++)
        for (var window = 0; window < 4; window++)
            Box(image, bus + 2 + window * 4, 21 + row * 4, 2, 2, IsNight ? Color(232, 185, 104) : Color(137, 187, 194));
        Disc(image, bus + 4, 29, 1, Color(5, 9, 14));
        Disc(image, bus + 15, 29, 1, Color(5, 9, 14));
        Dot(image, bus + 18, 27, Color(255, 216, 148));
    }
}
