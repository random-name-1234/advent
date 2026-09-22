using System.Globalization;
using advent.Data.Home;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class AgilePowerScene(IHomeSnapshotSource source, TimeProvider? timeProvider = null) : PixelStoryScene("Agile Power")
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private HomeSnapshot? snapshot;
    internal static Rgba32 BandColor(string band) => band switch
    {
        "cheap" => Color(100, 212, 150),
        "expensive" => Color(246, 124, 68),
        "normal" => Color(228, 202, 131),
        _ => Color(136, 169, 181)
    };

    public override void Activate()
    {
        base.Activate();
        IsActive = source.TryGetSnapshot(out snapshot!) && snapshot.AgileReady(clock.GetUtcNow());
    }

    protected override void Render(Image<Rgba32> image)
    {
        Box(image, 0, 0, 64, 32, Color(1, 6, 10));
        var now = clock.GetUtcNow();
        snapshot = source.TryGetSnapshot(out var latest) ? latest : null;
        if (snapshot is null || !snapshot.AgileReady(now))
        {
            Text(image, "AGILE", 8, Color(166, 182, 185));
            Text(image, "NO LIVE DATA", 19, Color(166, 182, 185));
            return;
        }
        if (Seconds >= 10) { DrawWindow(image, now); return; }
        var current = snapshot.Current!;
        var color = BandColor(current.Band);
        RailDmiText.Draw(image, "NOW", 2, 1, color);
        RailDmiText.Draw(image, "p/kWh", 64 - RailDmiText.MeasureWidth("p/kWh") - 2, 1, Color(142, 167, 176));
        DrawPrice(image, current.Price, color);
        DrawGraph(image, snapshot, now);
    }

    internal static string PriceText(double price) => price.ToString("0.0", CultureInfo.InvariantCulture);

    private static void DrawPrice(Image<Rgba32> image, double price, Rgba32 color)
    {
        var text = PriceText(price);
        var parts = text.Split('.');
        var width = HeadlineText.MeasureWidth(parts[0]) + 4 + HeadlineText.MeasureWidth(parts[1]);
        if (width > 60)
        {
            var small = RailDmiText.MeasureWidth(text) <= 60 ? text : "OUT OF RANGE";
            Text(image, small, 11, color);
            return;
        }
        var x = (64 - width) / 2;
        HeadlineText.Draw(image, parts[0], x, 9, color);
        x += HeadlineText.MeasureWidth(parts[0]) + 1;
        Box(image, x, 16, 2, 2, color);
        HeadlineText.Draw(image, parts[1], x + 3, 9, color);
    }

    private static void DrawGraph(Image<Rgba32> image, HomeSnapshot data, DateTimeOffset now)
    {
        var start = data.Current!.StartsAt;
        var rates = data.Slots.Where(s => s.EndsAt > now && s.StartsAt < start.AddHours(6)).ToArray();
        var max = Math.Max(1, rates.Select(s => Math.Abs(s.Price)).DefaultIfEmpty(1).Max());
        Box(image, 2, 26, 59, 1, Color(36, 56, 63));
        foreach (var rate in rates)
        {
            var index = (int)((rate.StartsAt - start).TotalMinutes / 30);
            if (index is < 0 or >= 12) continue;
            var h = Math.Max(1, (int)Math.Round(Math.Abs(rate.Price) / max * (rate.Price < 0 ? 4 : 6)));
            Box(image, 3 + index * 5, rate.Price < 0 ? 27 : 26 - h, 3, h, BandColor(rate.Band));
        }
    }

    private void DrawWindow(Image<Rgba32> image, DateTimeOffset now)
    {
        var color = BandColor("cheap");
        var window = snapshot!.NextCheapWindow;
        if (window is null || window.EndsAt <= now)
        {
            Text(image, "CHEAP POWER", 3, color);
            Text(image, "NO WINDOW", 13, Color(185, 198, 195));
            Text(image, "PUBLISHED", 23, Color(140, 166, 175));
            return;
        }
        var active = window.StartsAt <= now;
        var localNow = TimeZoneInfo.ConvertTime(now, London);
        var localStart = TimeZoneInfo.ConvertTime(window.StartsAt, London);
        var localEnd = TimeZoneInfo.ConvertTime(window.EndsAt, London);
        Text(image, active ? "CHEAP UNTIL" : "CHEAP FROM", 1, color);
        HeadlineText.DrawTime(image, (active ? localEnd : localStart).ToString("HH:mm", CultureInfo.InvariantCulture), 14, 10, color);
        var date = active ? localEnd.Date : localStart.Date;
        var day = date == localNow.Date ? "TODAY" : date == localNow.Date.AddDays(1) ? "TOMORROW" :
            date.ToString("dd MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
        Text(image, day, 24, Color(155, 180, 178));
    }
}
