using System.Text.Json;
using advent.Data.Rail;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailBoardSceneTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-21T18:10:00+01:00");

    [Fact]
    public void Carousel_StartsWithDeparturesAtBothStations_AndEndsAt90Seconds()
    {
        var scene = Scene();
        scene.Activate();
        Assert.True(scene.HidesTime);
        for (var station = 0; station < 2; station++)
        {
            for (var number = 1; number <= 3; number++)
            {
                var card = Assert.IsType<RailDepartureCard>(scene.CurrentCard);
                Assert.Equal(number, card.Number);
                Assert.Equal(station == 0 ? "CBG" : "KGX", card.Station);
                scene.Elapsed(TimeSpan.FromSeconds(15));
            }
        }
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void Carousel_WithTwoNotices_EndsAt130Seconds_AndCanReactivate()
    {
        var scene = Scene(Snapshot(withAlerts: true));
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(45));
        Assert.IsType<RailNoticeCard>(scene.CurrentCard);
        scene.Elapsed(TimeSpan.FromSeconds(20));
        Assert.Equal("KGX", Assert.IsType<RailDepartureCard>(scene.CurrentCard).Station);
        scene.Elapsed(TimeSpan.FromSeconds(64.9));
        Assert.True(scene.IsActive);
        scene.Elapsed(TimeSpan.FromSeconds(.1));
        Assert.False(scene.IsActive);
        scene.Activate();
        Assert.Equal(1, Assert.IsType<RailDepartureCard>(scene.CurrentCard).Number);
        scene.Elapsed(TimeSpan.FromDays(1));
        Assert.False(scene.IsActive);
    }

    [Fact]
    public void Cards_AreStableBetweenBoundaries_AndUseLatestPlatformAtBoundary()
    {
        var source = new MutableSource(Snapshot());
        var scene = new RailBoardScene(source, new TestClock(Now));
        scene.Activate();
        var original = scene.CurrentCard;
        source.Snapshot = source.Snapshot with
        {
            Cambridge = source.Snapshot.Cambridge with
            {
                Departures = [Service(30) with { PlatformText = "P10", StatusText = "+25", IsFastToCounterpart = true }]
            }
        };
        var reads = source.Reads;
        scene.Elapsed(TimeSpan.FromSeconds(14));
        Assert.Same(original, scene.CurrentCard);
        Assert.Equal(reads, source.Reads);
        scene.Elapsed(TimeSpan.FromSeconds(1));
        var card = Assert.IsType<RailDepartureCard>(scene.CurrentCard);
        Assert.Equal("10", card.Platform);
        Assert.Equal("+25 MIN", card.Status);
        Assert.True(card.IsFast);
        source.Snapshot = Snapshot();
        scene.Elapsed(TimeSpan.FromSeconds(14));
        Assert.Same(card, scene.CurrentCard);
    }

    [Fact]
    public void ChangedPlatformOrDelay_DoesNotRepeatAlreadyShownService()
    {
        var source = new MutableSource(Snapshot());
        var scene = new RailBoardScene(source, new TestClock(Now));
        scene.Activate();
        var first = Assert.IsType<RailDepartureCard>(scene.CurrentCard);
        source.Snapshot = source.Snapshot with
        {
            Cambridge = source.Snapshot.Cambridge with
            {
                Departures = [Service(20) with { PlatformText = "P10", StatusText = "+25", SortTime = Now.AddMinutes(45) }, Service(30)]
            }
        };
        scene.Elapsed(TimeSpan.FromSeconds(15));
        Assert.NotEqual(first.Time, Assert.IsType<RailDepartureCard>(scene.CurrentCard).Time);
        scene.Elapsed(TimeSpan.FromSeconds(15));
        Assert.Equal("KGX", Assert.IsType<RailDepartureCard>(scene.CurrentCard).Station);
    }

    [Fact]
    public void DepartedServices_AreRemovedAtTheNextBoundary()
    {
        var clock = new TestClock(Now);
        var source = new MutableSource(Snapshot());
        var scene = new RailBoardScene(source, clock);
        scene.Activate();
        clock.Now = Now.AddMinutes(25);
        source.Snapshot = source.Snapshot with
        {
            Cambridge = source.Snapshot.Cambridge with
            {
                Departures = [Service(20), Service(30) with { HasDeparted = true }, Service(40)]
            }
        };
        scene.Elapsed(TimeSpan.FromSeconds(15));
        Assert.Equal(Service(40).ScheduledText, Assert.IsType<RailDepartureCard>(scene.CurrentCard).Time);
    }

    [Fact]
    public void StaleStore_EndsVisitAtBoundary_AndActivationRechecksPreparedData()
    {
        var source = new MutableSource(Snapshot());
        var scene = new RailBoardScene(source, new TestClock(Now));
        scene.Prepare();
        Assert.True(scene.IsReadyToActivate);
        source.Available = false;
        scene.Activate();
        Assert.False(scene.IsActive);
        Assert.True(scene.ShouldSkipActivation);
        source.Available = true;
        scene.Activate();
        source.Available = false;
        scene.Elapsed(TimeSpan.FromSeconds(14));
        Assert.True(scene.IsActive);
        scene.Elapsed(TimeSpan.FromSeconds(1));
        Assert.False(scene.IsActive);
    }

    [Fact]
    public void EmptyAndUnavailableBoards_AreDistinct_AndSkipEmptySlots()
    {
        var snapshot = Snapshot();
        snapshot = snapshot with
        {
            Cambridge = snapshot.Cambridge with { Departures = [] },
            KingsCross = snapshot.KingsCross with { IsUnavailable = true }
        };
        var scene = Scene(snapshot);
        scene.Activate();
        Assert.Contains("TRAINS LISTED", Assert.IsType<RailOverviewCard>(scene.CurrentCard).Lines);
        scene.Elapsed(TimeSpan.FromSeconds(10));
        Assert.Contains("UNAVAILABLE", Assert.IsType<RailOverviewCard>(scene.CurrentCard).Lines);
        scene.Elapsed(TimeSpan.FromSeconds(10));
        Assert.False(scene.IsActive);
    }

    [Fact]
    public void Upcoming_HandlesMidnightAndDelays_WithoutComparingClockStrings()
    {
        var midnight = DateTimeOffset.Parse("2026-09-22T00:01:00+01:00");
        var past = Service(20) with { ScheduledText = "23:50", ScheduledAt = midnight.AddMinutes(-11), SortTime = midnight.AddMinutes(-1) };
        var delayed = past with { StatusText = "+25", SortTime = midnight.AddMinutes(14) };
        var next = Service(30) with { ScheduledText = "00:10", ScheduledAt = midnight.AddMinutes(9), SortTime = midnight.AddMinutes(9) };
        var station = Snapshot().Cambridge with { Departures = [past, delayed, next] };
        Assert.Equal(new[] { next, delayed }, RailCards.Upcoming(station, midnight));
    }

    [Theory]
    [InlineData("On time", "ON TIME")]
    [InlineData("+5", "+5 MIN")]
    [InlineData("+25", "+25 MIN")]
    [InlineData("+125", "+125 MIN")]
    [InlineData("Cancelled", "CANCELLED")]
    [InlineData("See front", "CHECK STATUS")]
    [InlineData("", "CHECK STATUS")]
    public void Status_IsExplicitAndFits(string input, string expected)
    {
        Assert.Equal(expected, RailCards.Status(input));
        Assert.InRange(RailDmiText.MeasureWidth(expected), 1, RailCards.TextWidth);
    }

    [Theory]
    [InlineData("P1", "1")]
    [InlineData("P10", "10")]
    [InlineData("P12A", "12A")]
    [InlineData("--", "--")]
    [InlineData("P123456", "--")]
    public void Platform_DoesNotTruncateIntoAnotherNumber(string input, string expected) =>
        Assert.Equal(expected, RailCards.Platform(input));

    [Theory]
    [InlineData("London King's Cross", "KGX", "KINGS CROSS")]
    [InlineData("London Liverpool Street", "LST", "LIVERPOOL ST")]
    [InlineData("Cambridge", "CBG", "CAMBRIDGE")]
    [InlineData("Kings Lynn", "KLN", "KINGS LYNN")]
    [InlineData("Peterborough", "PBO", "PETERBORO")]
    [InlineData("Very Long Destination Name", "XYZ", "XYZ")]
    public void Destination_UsesReadableNames_AndKeepsThroughDestinations(string input, string code, string expected) =>
        Assert.Equal(expected, RailCards.StationLabel(input, code));

    [Fact]
    public void Notices_WrapStaticPages_AndDiscloseOmittedAdvice()
    {
        var station = Snapshot().Cambridge with
        {
            Alerts = [new("Minor note", 0), new(string.Join(' ', Enumerable.Repeat("SIGNAL FAILURE", 30)), 3)]
        };
        var card = RailCards.Notice(station);
        Assert.Equal(4, card.Pages.Count);
        Assert.Equal("SIGNAL", card.Pages[0][0]);
        Assert.Contains("MORE ONLINE", card.Pages[^1]);
        Assert.Contains("NATIONAL RAIL", card.Pages[^1]);
        var text = new string('W', 60);
        var wrapped = RailCards.Wrap(text);
        Assert.Equal(text, string.Concat(wrapped));
        Assert.All(wrapped, line => Assert.InRange(RailDmiText.MeasureWidth(line), 1, 60));
    }

    [Fact]
    public void EveryTextRun_Fits64x32_WithoutOverlap_AndCardsClearThePreviousImage()
    {
        var station = Snapshot(withAlerts: true).Cambridge;
        var cards = new List<RailCard>
        {
            RailCards.StationStatus(station with { IsUnavailable = true }),
            RailCards.StationStatus(station),
            RailCards.Notice(station),
            RailCards.Notice(station with { HeaderLabel = "UNKNOWN", StationName = "Unknown Station", Crs = null })
        };
        foreach (var destination in new[] { "London Kings Cross", "London Liverpool Street", "Kings Lynn", "Cambridge", new string('W', 30) })
        foreach (var status in new[] { "On time", "+5", "+25", "+125", "Cancelled", "See front" })
        foreach (var origin in new[] { "CBG", "KGX" })
        foreach (var fast in new[] { false, true })
            cards.Add(RailCards.Departure(station with { Crs = origin }, Service(20) with
            {
                LocationText = destination, PlatformText = "P12A", StatusText = status, IsFastToCounterpart = fast
            }, 3));

        foreach (var card in cards)
        foreach (var second in new[] { 0, 5, 10, 15, 19 })
        {
            var elapsed = TimeSpan.FromSeconds(second);
            var runs = RailCardRenderer.TextRuns(card, elapsed);
            if (card is RailDepartureCard { IsFast: true })
                Assert.True(runs[1].X - (runs[0].X + RailDmiText.MeasureWidth(runs[0].Text)) >= 2);
            var bounds = runs.Select(run => new Rectangle(run.X, run.Y, RailDmiText.MeasureWidth(run.Text), RailDmiText.Height)).ToList();
            if (card is RailDepartureCard) bounds.Add(new Rectangle(2, 9, 36, 9));
            foreach (var rect in bounds)
            {
                Assert.InRange(rect.Left, 0, 63);
                Assert.InRange(rect.Top, 0, 31);
                Assert.InRange(rect.Right, 1, 64);
                Assert.InRange(rect.Bottom, 1, 32);
            }
            for (var first = 0; first < bounds.Count; first++)
            for (var secondRun = first + 1; secondRun < bounds.Count; secondRun++)
                Assert.False(bounds[first].IntersectsWith(bounds[secondRun]), $"Overlapping fields in {card}");

            using var blank = new Image<Rgba32>(64, 32, Color.Black);
            using var dirty = new Image<Rgba32>(64, 32, Color.White);
            RailCardRenderer.Draw(blank, card, elapsed);
            RailCardRenderer.Draw(dirty, card, elapsed);
            Assert.Equal(Pixels(blank), Pixels(dirty));
        }
    }

    [Fact]
    public void DifferentDelays_RenderDifferently_WhileTimeAndPlatformStayFixed()
    {
        var card = RailCards.Departure(Snapshot().Cambridge, Service(20), 1);
        using var first = new Image<Rgba32>(64, 32);
        using var second = new Image<Rgba32>(64, 32);
        RailCardRenderer.Draw(first, card with { Status = "+5 MIN" }, TimeSpan.Zero);
        RailCardRenderer.Draw(second, card with { Status = "+25 MIN" }, TimeSpan.Zero);
        Assert.NotEqual(Pixels(first), Pixels(second));
        for (var y = 0; y < 27; y++)
        for (var x = 0; x < 64; x++)
            Assert.Equal(first[x, y], second[x, y]);
    }

    [Fact]
    public void Provider_PreservesScheduledIdentity_ActualDeparture_AndUncappedDelay()
    {
        var dto = JsonSerializer.Deserialize<ServiceItemDto>("""
            {"destination":[{"locationName":"London Kings Cross","crs":"KGX"}],
             "std":"2026-09-21T18:00:00+01:00","etd":"2026-09-21T20:05:00+01:00",
             "atd":"2026-09-21T20:04:00+01:00","platform":"10"}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var service = DarwinRailSnapshotProvider.BuildDepartureSnapshot(dto)!;
        Assert.True(service.HasDeparted);
        Assert.Equal(DateTimeOffset.Parse(dto.Std!), service.ScheduledAt);
        Assert.Equal("+125", service.StatusText);
        Assert.Equal("P10", service.PlatformText);
        Assert.False(DarwinRailSnapshotProvider.BuildDepartureSnapshot(dto with { Atd = null })!.HasDeparted);
    }

    [Theory]
    [InlineData("CBG", null, "Cambridge")]
    [InlineData("KLN", "Kings Lynn", "Kings Lynn")]
    [InlineData("KGX", null, "London Kings Cross")]
    [InlineData(null, "London King's Cross", "London Kings Cross")]
    [InlineData(null, "Liverpool Street", "London Liverpool Street")]
    [InlineData(null, "Finsbury Pk", "Finsbury Park")]
    public void FullStationNames_AreStillPreserved(string? crs, string? name, string expected) =>
        Assert.Equal(expected, RailStationNames.DisplayName(crs, name));

    [Fact]
    public void GenericCorridorEnvironmentVariables_AreStillSupported()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ADVENT_RAIL_ENABLED"] = "true", ["ADVENT_RAIL_LDB_CONSUMER_KEY"] = "test-key",
            ["ADVENT_RAIL_ORIGIN_CRS"] = "cbg", ["ADVENT_RAIL_DESTINATION_CRS"] = "lst",
            ["ADVENT_RAIL_CAMBRIDGE_CRS"] = null, ["ADVENT_RAIL_KINGS_CROSS_CRS"] = null,
            ["ADVENT_RAIL_ORIGIN_LABEL"] = null, ["ADVENT_RAIL_DESTINATION_LABEL"] = null
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in settings) Environment.SetEnvironmentVariable(key, value);
            var options = Assert.IsType<RailBoardOptions>(RailBoardOptions.TryFromEnvironment());
            Assert.Equal("CBG", options.OriginCrs);
            Assert.Equal("LST", options.DestinationCrs);
            Assert.Equal("Cambridge", options.OriginLabel);
            Assert.Equal("London Liverpool Street", options.DestinationLabel);
        }
        finally
        {
            foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static RailBoardScene Scene(RailSceneSnapshot? snapshot = null) =>
        new(new MutableSource(snapshot ?? Snapshot()), new TestClock(Now));

    internal static RailSceneSnapshot Snapshot(bool withAlerts = false) => new(
        new("CBG", "Cambridge", [Service(20), Service(30), Service(40)], [],
            withAlerts ? [new("Signal failure between Foxton and Royston. Delays expected.", 3)] : [], Now, false) { Crs = "CBG" },
        new("KGX", "London Kings Cross", [Service(20), Service(30), Service(40)], [],
            withAlerts ? [new("Please check your platform before boarding.", 1)] : [], Now, false) { Crs = "KGX" }, Now);

    private static RailServiceSnapshot Service(int minutes) => new(
        Now.AddMinutes(minutes).ToString("HH:mm"), "London Kings Cross", "KGX", "P5", "On time",
        new Rgba32(230, 192, 112), "Great Northern", "CALLS ROYSTON, STEVENAGE", "", Now.AddMinutes(minutes))
        { ScheduledAt = Now.AddMinutes(minutes) };

    private static byte[] Pixels(Image<Rgba32> image)
    {
        var bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    private sealed class MutableSource(RailSceneSnapshot snapshot) : IRailSnapshotSource
    {
        internal RailSceneSnapshot Snapshot { get; set; } = snapshot;
        internal bool Available { get; set; } = true;
        internal int Reads { get; private set; }
        public bool TryGetSnapshot(out RailSceneSnapshot result)
        {
            Reads++;
            result = Snapshot;
            return Available;
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
