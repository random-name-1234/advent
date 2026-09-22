using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class WeatherWindowScene(IWeatherSnapshotSource source) : PixelStoryScene("Weather Window")
{
    private WeatherSnapshot? weather;

    public override void Activate()
    {
        base.Activate();
        IsActive = source.TryGetSnapshot(out weather!);
    }

    internal static string Condition(int code) => code switch
    {
        0 or 1 => "clear",
        2 or 3 => "cloud",
        45 or 48 => "fog",
        71 or 73 or 75 or 77 or 85 or 86 => "snow",
        95 or 96 or 99 => "storm",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
        _ => "unknown"
    };

    protected override void Render(Image<Rgba32> image)
    {
        if (weather is null) return;
        var condition = Condition(weather.CurrentWeatherCode);
        var bright = weather.IsDay && condition is "clear" or "cloud";
        Sky(image, bright ? Color(51, 112, 148) : Color(17, 32, 48), bright ? Color(146, 179, 171) : Color(70, 89, 94));
        if (condition == "clear") Disc(image, 45, 8, 4, weather.IsDay ? Color(247, 207, 113) : Color(185, 200, 181));
        if (condition != "clear")
        {
            Cloud(image, Wrap(Seconds * .8 + 10, 85) - 12, 5, Color(98, 117, 128));
            Cloud(image, Wrap(Seconds * .6 + 47, 85) - 12, 10, Color(85, 104, 117));
        }
        for (var x = 0; x < 64; x++)
            Box(image, x, 22 + (int)(Math.Sin(x * .12) * 2), 1, 10, condition == "snow" ? Color(152, 172, 176) : Color(37, 69, 53));
        Box(image, 10, 18, 2, 9, Color(50, 48, 43));
        Disc(image, 11, 17, 4, Color(31, 71, 57));
        if (condition is "rain" or "storm" or "snow")
        {
            for (var i = 0; i < 22; i++)
            {
                var x = Wrap(i * 17 + (condition == "snow" ? Math.Sin(Seconds + i) * 2 : Seconds * 1.5), 64);
                var y = Wrap(i * 11 + Seconds * (condition == "snow" ? 2 : 9), 29);
                Box(image, x, y, 1, condition == "snow" ? 1 : 2, condition == "snow" ? Color(216, 226, 222) : Color(126, 174, 195));
            }
        }
        if (condition == "fog")
            for (var y = 10; y < 25; y += 5) Box(image, 3, y, 58, 2, Color(112, 130, 136));

        // Opaque joinery is drawn last, keeping rain outside the room.
        Box(image, 0, 0, 64, 2, Color(96, 66, 44));
        Box(image, 0, 0, 3, 32, Color(78, 53, 37));
        Box(image, 61, 0, 3, 32, Color(78, 53, 37));
        Box(image, 31, 2, 2, 26, Color(128, 94, 61));
        Box(image, 3, 15, 58, 2, Color(128, 94, 61));
        Box(image, 0, 28, 64, 4, Color(125, 88, 55));
        if (condition == "clear" && weather.IsDay) Box(image, 33, 28, 18, 2, Color(185, 145, 79));
        Box(image, 8, 25, 5, 4, Color(165, 81, 50));
        Box(image, 10, 21, 1, 4, Color(56, 120, 64));
        Dot(image, 9, 22, Color(83, 151, 71));
        Dot(image, 11, 23, Color(83, 151, 71));
    }
}
