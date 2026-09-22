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
        var hasWindow = snapshot.NextCheapWindow is { } window && window.EndsAt > now;
        if (ShowWindow(Seconds, hasWindow)) { DrawWindow(image, now); return; }
        var current = snapshot.Current!;
        var color = BandColor(current.Band);
        RailDmiText.Draw(image, "NOW", 2, 1, color);
        RailDmiText.Draw(image, "p/kWh", 64 - RailDmiText.MeasureWidth("p/kWh") - 2, 1, Color(142, 167, 176));
        DrawPrice(image, current.Price, color);
        DrawGraph(image, snapshot, now);
    }

    internal static string PriceText(double price) => price.ToString("0.0", CultureInfo.InvariantCulture);
    internal static bool ShowWindow(double seconds, bool hasWindow) => seconds >= (hasWindow ? 10 : 17);

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
        var rates = data.Slots.Where(s => s.EndsAt > now && s.StartsAt > start && s.StartsAt < start.AddHours(6))
            .Prepend(data.Current).ToArray();
        var max = Math.Max(1, rates.Select(s => Math.Abs(s.Price)).DefaultIfEmpty(1).Max());
        Box(image, 2, 23, 59, 1, Color(36, 56, 63));
        foreach (var rate in rates)
        {
            var index = (int)((rate.StartsAt - start).TotalMinutes / 30);
            if (index is < 0 or >= 12) continue;
            var h = Math.Max(1, (int)Math.Round(Math.Abs(rate.Price) / max * (rate.Price < 0 ? 3 : 4)));
            Box(image, 3 + index * 5, rate.Price < 0 ? 24 : 23 - h, 3, h, BandColor(rate.Band));
        }
        Dot(image, 4, 18, Color(234, 239, 221));
        RailDmiText.Draw(image, "NOW", 2, 27, Color(173, 194, 195));
        RailDmiText.Draw(image, "+6H", 62 - RailDmiText.MeasureWidth("+6H"), 27, Color(142, 167, 176));
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
        Text(image, active ? "CHEAP NOW" : "CHEAP WINDOW", 1, color);
        Text(image, WindowEndpoint("FROM", localStart), 9, color);
        Text(image, WindowEndpoint("TO", localEnd), 17, Color(185, 207, 196));
        Text(image, WindowDays(localStart, localEnd, localNow), 25, Color(155, 180, 178));
    }

    internal static string WindowEndpoint(string label, DateTimeOffset endpoint) =>
        $"{label} {endpoint.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    internal static string WindowDays(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        string Day(DateTimeOffset endpoint) => endpoint.Date == now.Date ? "TOD" : endpoint.Date == now.Date.AddDays(1) ? "TOM" :
            endpoint.ToString("dd/MM", CultureInfo.InvariantCulture);
        return start.Date == end.Date ? Day(start) : $"{Day(start)}-{Day(end)}";
    }
}
