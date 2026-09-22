using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal readonly record struct RailTextRun(string Text, int X, int Y, Rgba32 Color);

internal static class RailCardRenderer
{
    private static readonly Rgba32 Amber = new(255, 176, 64);
    private static readonly Rgba32 Primary = new(255, 214, 128);
    private static readonly Rgba32 Secondary = new(200, 134, 52);
    private static readonly Rgba32 Divider = new(45, 30, 10);
    private static readonly Rgba32 Cancelled = new(255, 96, 72);

    internal static void Draw(Image<Rgba32> image, RailCard card, TimeSpan elapsed)
    {
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
                rows.GetRowSpan(y).Fill(new Rgba32(0, 0, 0));
        });
        for (var x = 2; x < 62 && x < image.Width; x++)
            if (image.Height > 7) image[x, 7] = Divider;
        foreach (var run in TextRuns(card, elapsed))
            RailDmiText.Draw(image, run.Text, run.X, run.Y, run.Color);

        if (card is RailDepartureCard departure)
        {
            ClockRenderer.DrawTimeDigits(image, departure.Time, 2, 9, Primary);
            for (var y = 9; y < 19 && y < image.Height; y++)
                if (image.Width > 40) image[40, y] = Divider;
        }
    }

    internal static IReadOnlyList<RailTextRun> TextRuns(RailCard card, TimeSpan elapsed)
    {
        switch (card)
        {
            case RailDepartureCard departure:
                return
                [
                    new($"FROM {departure.Station}", departure.IsFast ? 1 : 2, 1, Secondary),
                    Right(departure.IsFast ? "FAST" : departure.Number switch { 1 => "1ST", 2 => "2ND", _ => "3RD" },
                        departure.IsFast ? 63 : 62, 1, departure.IsFast ? Primary : Secondary),
                    new("PLAT", 43, 9, Secondary),
                    Center(departure.Platform, 52, 15, Primary),
                    new(departure.Destination, 2, 21, Amber),
                    new(departure.Status, 2, 27, departure.Status == "CANCELLED" ? Cancelled : Primary)
                ];
            case RailOverviewCard overview:
                return
                [
                    Center(overview.Station, 32, 1, Amber),
                    Center(overview.Lines[0], 32, 10, Primary),
                    Center(overview.Lines[1], 32, 18, Primary),
                    Center(overview.Lines[2], 32, 26, Secondary)
                ];
            case RailNoticeCard notice:
                var page = Math.Clamp((int)(elapsed.TotalSeconds / notice.Duration.TotalSeconds * notice.Pages.Count),
                    0, notice.Pages.Count - 1);
                var result = new List<RailTextRun>
                {
                    new($"{notice.Station} INFO", 2, 1, Amber),
                    Right($"{page + 1}/{notice.Pages.Count}", 62, 1, Secondary)
                };
                result.AddRange(notice.Pages[page].Select((line, index) => new RailTextRun(line, 2, 10 + index * 8, Primary)));
                return result;
            default:
                return [];
        }
    }

    private static RailTextRun Center(string text, int center, int y, Rgba32 color) =>
        new(text, center - RailDmiText.MeasureWidth(text) / 2, y, color);

    private static RailTextRun Right(string text, int right, int y, Rgba32 color) =>
        new(text, right - RailDmiText.MeasureWidth(text), y, color);
}
