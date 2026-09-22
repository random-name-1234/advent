using advent.Data.Rail;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace advent.Tests;

public class RailCardCaptureTests
{
    [Fact]
    public void RenderDeterministicRailCards()
    {
        var now = DateTimeOffset.Parse("2026-09-21T18:10:00+01:00");
        var station = new RailStationSnapshot("CBG", "Cambridge", [], [], [], now, false) { Crs = "CBG" };
        var service = new RailServiceSnapshot("18:12", "London Kings Cross", "KGX", "P5", "On time",
            new Rgba32(230, 192, 112), "Great Northern", "ROYSTON, STEVENAGE", "", now.AddMinutes(2));
        var london = station with { StationName = "London Kings Cross", Crs = "KGX" };
        var notice = RailCards.Notice(station with
        {
            Alerts = [new("Signal failure between Foxton and Royston. Delays expected. Check your journey before travelling.", 3)]
        });
        var cards = new List<(string Name, RailCard Card, TimeSpan Elapsed)>
        {
            ("01-fast-to-kings-cross", RailCards.Departure(station, service with { IsFastToCounterpart = true }, 1), TimeSpan.Zero),
            ("02-on-time", RailCards.Departure(station, service, 1), TimeSpan.Zero),
            ("03-delayed-p10", RailCards.Departure(station, service with { ScheduledText = "18:19", PlatformText = "P10", StatusText = "+5" }, 2), TimeSpan.Zero),
            ("04-cancelled", RailCards.Departure(station, service with { ScheduledText = "18:32", PlatformText = "--", StatusText = "Cancelled" }, 3), TimeSpan.Zero),
            ("05-fast-to-cambridge", RailCards.Departure(london, service with { LocationText = "Cambridge", LocationCode = "CBG", IsFastToCounterpart = true }, 1), TimeSpan.Zero),
            ("06-cambridge-bound", RailCards.Departure(london, service with { LocationText = "Cambridge", LocationCode = "CBG", PlatformText = "P12A" }, 1), TimeSpan.Zero),
            ("07-fast-through-service", RailCards.Departure(london, service with { LocationText = "Kings Lynn", LocationCode = "KLN", PlatformText = "P10", StatusText = "+25", IsFastToCounterpart = true }, 2), TimeSpan.Zero),
            ("08-liverpool-street", RailCards.Departure(station, service with { LocationText = "London Liverpool Street", LocationCode = "LST", StatusText = "+125" }, 3), TimeSpan.Zero),
            ("09-unavailable", RailCards.StationStatus(station with { IsUnavailable = true }), TimeSpan.Zero),
            ("10-no-services", RailCards.StationStatus(station), TimeSpan.Zero)
        };
        for (var page = 0; page < notice.Pages.Count; page++)
            cards.Add(($"{11 + page:00}-notice-{page + 1}", notice, TimeSpan.FromSeconds(page * 20.0 / notice.Pages.Count)));

        var output = Environment.GetEnvironmentVariable("ADVENT_RAIL_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(output)) Directory.CreateDirectory(output);
        using var sheet = new Image<Rgba32>(64 * 4, 32 * (int)Math.Ceiling(cards.Count / 4.0), Color.Black);
        for (var index = 0; index < cards.Count; index++)
        {
            var (name, card, elapsed) = cards[index];
            using var image = new Image<Rgba32>(64, 32, Color.Black);
            RailCardRenderer.Draw(image, card, elapsed);
            Assert.Contains(RailCardRenderer.TextRuns(card, elapsed), run => run.Text.Length > 0);
            if (string.IsNullOrWhiteSpace(output)) continue;
            image.SaveAsPng(Path.Combine(output, name + ".png"));
            sheet.Mutate(context => context.DrawImage(image, new Point(index % 4 * 64, index / 4 * 32), 1));
            using var enlarged = image.Clone(context => context.Resize(640, 320, KnownResamplers.NearestNeighbor));
            enlarged.SaveAsPng(Path.Combine(output, name + "-large.png"));
        }
        if (!string.IsNullOrWhiteSpace(output))
        {
            sheet.Mutate(context => context.Resize(sheet.Width * 5, sheet.Height * 5, KnownResamplers.NearestNeighbor));
            sheet.SaveAsPng(Path.Combine(output, "rail-capture-sheet.png"));
        }
    }
}
