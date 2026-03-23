using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using advent.Data.Rail;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailBoardSceneTests
{
    [Fact]
    public void RailBoardScene_RendersCompactPages_AndExpiresAfterTheExtendedBoardDuration()
    {
        var snapshot = new RailSceneSnapshot(
            new RailStationSnapshot(
                "CAM",
                "Cambridge",
                [
                    CreateService("18:12", "London Kings Cross", "KGX", "P5", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON, STEVENAGE, FINSBURY PARK"),
                    CreateService("18:19", "London Liverpool Street", "LST", "P1", "+5", new Rgba32(255, 184, 64),
                        "Greater Anglia", "CALLS AUDLEY END, BISHOPS STORTFORD, BROXBOURNE")
                ],
                [],
                [
                    new RailAlertSnapshot("Signal failure between Foxton and Royston. Delays expected.", 3)
                ],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                false),
            new RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [],
                [
                    CreateService("18:28", "Cambridge", "CBG", "P8", "+4", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS FINSBURY PARK, STEVENAGE, ROYSTON"),
                    CreateService("18:34", "Peterborough", "PBO", "--", "On time", new Rgba32(210, 188, 144),
                        "LNER", "CALLS STEVENAGE")
                ],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                false),
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero));

        var scene = new RailBoardScene(new FixedRailSnapshotSource(snapshot));

        scene.Activate();
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromMilliseconds(50));
        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);

        scene.Elapsed(TimeSpan.FromSeconds(60));
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromSeconds(60));
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void RailBoardScene_ShowsGracefulNoDataBoard_WhenStationDataIsUnavailable()
    {
        var snapshot = new RailSceneSnapshot(
            new RailStationSnapshot(
                "CAM",
                "Cambridge",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero));

        var scene = new RailBoardScene(new FixedRailSnapshotSource(snapshot));

        scene.Activate();
        scene.Elapsed(TimeSpan.FromMilliseconds(50));

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);
    }

    [Fact]
    public void RailBoardScene_BuildsDepartureBoards_ForCambridgeAndConfiguredLondonCorridor()
    {
        var updatedAt = new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero);
        var snapshot = new RailSceneSnapshot(
            new RailStationSnapshot(
                "CAM",
                "Cambridge",
                [
                    CreateService("18:12", "London Kings Cross", "KGX", "P5", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON, STEVENAGE"),
                    CreateService("18:19", "London Liverpool Street", "LST", "P1", "+5", new Rgba32(255, 184, 64),
                        "Greater Anglia", "CALLS AUDLEY END"),
                    CreateService("18:24", "Finsbury Park", "FPK", "P3", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON")
                ],
                [
                    CreateService("18:02", "London Kings Cross", "KGX", "P4", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON"),
                    CreateService("18:05", "Finsbury Park", "FPK", "P6", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON")
                ],
                [],
                updatedAt,
                false),
            new RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [
                    CreateService("18:28", "Cambridge", "CBG", "P8", "+4", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS FINSBURY PARK, STEVENAGE")
                ],
                [
                    CreateService("18:25", "Cambridge", "CBG", "P9", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS STEVENAGE")
                ],
                [],
                updatedAt,
                false),
            updatedAt);

        var pages = InvokeBuildPages(snapshot);

        Assert.Equal(2, pages.Length);
        AssertBoardRows(pages[0], "Cambridge", "Departures", "London Kings Cross", "London Liverpool Street", "Finsbury Park");
        AssertBoardRows(pages[1], "London Kings Cross", "Departures", "Cambridge");
    }

    [Fact]
    public void RailBoardScene_IncludesThroughServicesThatCallAtCambridgeOnLondonBoards()
    {
        var updatedAt = new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero);
        var snapshot = new RailSceneSnapshot(
            new RailStationSnapshot(
                "CAM",
                "Cambridge",
                [],
                [],
                [],
                updatedAt,
                false),
            new RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [
                    CreateService("18:28", "Cambridge", "CBG", "P8", "+4", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS FINSBURY PARK, STEVENAGE"),
                    CreateService("18:31", "Kings Lynn", "KLN", "P9", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS FINSBURY PARK, STEVENAGE, CAMBRIDGE, ELY")
                ],
                [
                    CreateService("18:25", "Cambridge", "CBG", "P7", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS STEVENAGE, FINSBURY PARK"),
                    CreateService("18:27", "Kings Lynn", "KLN", "P10", "+2", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS CAMBRIDGE, STEVENAGE, FINSBURY PARK")
                ],
                [],
                updatedAt,
                false),
            updatedAt);

        var pages = InvokeBuildPages(snapshot);

        Assert.Equal(2, pages.Length);
        AssertBoardRows(pages[1], "London Kings Cross", "Departures", "Cambridge", "Kings Lynn");
    }

    [Fact]
    public void RailBoardScene_UsesGenericOriginAndDestinationEnvironmentVariables()
    {
        var originalEnabled = Environment.GetEnvironmentVariable("ADVENT_RAIL_ENABLED");
        var originalKey = Environment.GetEnvironmentVariable("ADVENT_RAIL_LDB_CONSUMER_KEY");
        var originalOrigin = Environment.GetEnvironmentVariable("ADVENT_RAIL_ORIGIN_CRS");
        var originalDestination = Environment.GetEnvironmentVariable("ADVENT_RAIL_DESTINATION_CRS");
        var originalLegacyOrigin = Environment.GetEnvironmentVariable("ADVENT_RAIL_CAMBRIDGE_CRS");
        var originalLegacyDestination = Environment.GetEnvironmentVariable("ADVENT_RAIL_KINGS_CROSS_CRS");
        var originalOriginLabel = Environment.GetEnvironmentVariable("ADVENT_RAIL_ORIGIN_LABEL");
        var originalDestinationLabel = Environment.GetEnvironmentVariable("ADVENT_RAIL_DESTINATION_LABEL");

        try
        {
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ENABLED", "true");
            Environment.SetEnvironmentVariable("ADVENT_RAIL_LDB_CONSUMER_KEY", "test-key");
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ORIGIN_CRS", "cbg");
            Environment.SetEnvironmentVariable("ADVENT_RAIL_DESTINATION_CRS", "lst");
            Environment.SetEnvironmentVariable("ADVENT_RAIL_CAMBRIDGE_CRS", null);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_KINGS_CROSS_CRS", null);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ORIGIN_LABEL", null);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_DESTINATION_LABEL", null);

            var options = RailBoardOptions.TryFromEnvironment();

            Assert.NotNull(options);
            Assert.Equal("CBG", options!.OriginCrs);
            Assert.Equal("LST", options.DestinationCrs);
            Assert.Equal("Cambridge", options.OriginLabel);
            Assert.Equal("London Liverpool Street", options.DestinationLabel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ENABLED", originalEnabled);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_LDB_CONSUMER_KEY", originalKey);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ORIGIN_CRS", originalOrigin);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_DESTINATION_CRS", originalDestination);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_CAMBRIDGE_CRS", originalLegacyOrigin);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_KINGS_CROSS_CRS", originalLegacyDestination);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_ORIGIN_LABEL", originalOriginLabel);
            Environment.SetEnvironmentVariable("ADVENT_RAIL_DESTINATION_LABEL", originalDestinationLabel);
        }
    }

    [Theory]
    [InlineData("CBG", null, "Cambridge")]
    [InlineData("KLN", "Kings Lynn", "Kings Lynn")]
    [InlineData("KGX", null, "London Kings Cross")]
    [InlineData(null, "London King's Cross", "London Kings Cross")]
    [InlineData(null, "Kings Cross", "London Kings Cross")]
    [InlineData(null, "Liverpool Street", "London Liverpool Street")]
    [InlineData(null, "Finsbury Pk", "Finsbury Park")]
    public void RailBoardScene_FormatsFullStationNames(string? crs, string? locationName, string expected)
    {
        var formatted = RailStationNames.DisplayName(crs, locationName);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("On time", 22, "On tm")]
    [InlineData("Cancelled", 22, "Can")]
    [InlineData("See front", 22, "See")]
    [InlineData("+14", 22, "+14")]
    public void RailBoardScene_BuildsCompactBoardIndicator(string status, int maxWidth, string expected)
    {
        var method = typeof(RailBoardScene).GetMethod("CompactBoardIndicator", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var compact = method!.Invoke(null, [status, maxWidth]);

        Assert.Equal(expected, compact);
    }

    [Fact]
    public void RailBoardScene_BuildsBoardTickerWithFullStationNames_WhenBoardIsEmpty()
    {
        var method = typeof(RailBoardScene).GetMethod("BuildBoardTicker", BindingFlags.Static | BindingFlags.NonPublic);
        var boardType = typeof(RailBoardScene).GetNestedType("BoardType", BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.NotNull(boardType);

        var station = new RailStationSnapshot(
            "KGX",
            "London King's Cross",
            [],
            [],
            [],
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
            false);

        var ticker = method!.Invoke(null,
            [station, Enum.Parse(boardType!, "Departures"), Array.Empty<RailServiceSnapshot>()]);

        Assert.Equal("London Kings Cross departures", ticker);
    }

    [Fact]
    public void RailBoardScene_BuildsCompactCallingText_WithFastCueAndFullNames()
    {
        var method = typeof(RailBoardScene).GetMethod("CompactBoardCallingText", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var fastService = CreateService(
            "18:12",
            "London Kings Cross",
            "KGX",
            "P5",
            "On time",
            new Rgba32(210, 188, 144),
            "Great Northern",
            "CALLS ROYSTON, STEVENAGE");

        var stoppingService = CreateService(
            "18:45",
            "Cambridge",
            "CBG",
            "P3",
            "On time",
            new Rgba32(210, 188, 144),
            "Great Northern",
            "CALLS FINSBURY PARK, KNEBWORTH, STEVENAGE, HITCHIN, LETCHWORTH, ROYSTON");

        // When isFast=true, prefix is "Fast via"
        var fastText = method!.Invoke(null, [fastService, true]);
        Assert.Equal("Fast via Royston, Stevenage", fastText);

        // When isFast=false (default), prefix is "Via"
        var defaultText = method.Invoke(null, [fastService, false]);
        Assert.Equal("Via Royston, Stevenage", defaultText);

        var stoppingText = method.Invoke(null, [stoppingService, false]);
        Assert.StartsWith("Via ", (string)stoppingText!);
    }

    [Fact]
    public void RailBoardScene_ClassifiesFastServicesAdaptively_BasedOnStopCountSpread()
    {
        var classifyMethod = typeof(RailBoardScene).GetMethod("ClassifyFastServices", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(classifyMethod);

        // Mix of fast (2 stops) and slow (6 stops) services
        var services = new[]
        {
            CreateService("18:12", "London Kings Cross", "KGX", "P5", "On time",
                new Rgba32(210, 188, 144), "Great Northern", "CALLS ROYSTON, STEVENAGE"),
            CreateService("18:19", "London Kings Cross", "KGX", "P1", "+5",
                new Rgba32(255, 184, 64), "Greater Anglia",
                "CALLS FINSBURY PARK, KNEBWORTH, STEVENAGE, HITCHIN, LETCHWORTH, ROYSTON"),
            CreateService("18:24", "London Kings Cross", "KGX", "P3", "On time",
                new Rgba32(210, 188, 144), "Great Northern", "CALLS ROYSTON, STEVENAGE, FINSBURY PARK")
        };

        var fastIndices = classifyMethod!.Invoke(null, [services]);
        var fastSet = Assert.IsAssignableFrom<IReadOnlySet<int>>(fastIndices);

        // Service 0 (2 stops) and service 2 (3 stops) should be fast relative to service 1 (6 stops)
        Assert.Contains(0, fastSet);
        Assert.DoesNotContain(1, fastSet);
        Assert.Contains(2, fastSet);
    }

    [Fact]
    public void RailBoardScene_ClassifiesFastServices_AllSameStops_NoneHighlighted()
    {
        var classifyMethod = typeof(RailBoardScene).GetMethod("ClassifyFastServices", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(classifyMethod);

        // All services have the same number of stops (2)
        var services = new[]
        {
            CreateService("18:12", "London Kings Cross", "KGX", "P5", "On time",
                new Rgba32(210, 188, 144), "Great Northern", "CALLS ROYSTON, STEVENAGE"),
            CreateService("18:19", "London Kings Cross", "KGX", "P1", "On time",
                new Rgba32(210, 188, 144), "Great Northern", "CALLS FINSBURY PARK, STEVENAGE")
        };

        var fastIndices = classifyMethod!.Invoke(null, [services]);
        var fastSet = Assert.IsAssignableFrom<IReadOnlySet<int>>(fastIndices);

        // All same stops — no contrast, none highlighted
        Assert.Empty(fastSet);
    }

    private static RailServiceSnapshot CreateService(
        string scheduledText,
        string locationText,
        string locationCode,
        string platformText,
        string statusText,
        Rgba32 statusColor,
        string operatorText,
        string callingText)
    {
        return new RailServiceSnapshot(
            scheduledText,
            locationText,
            locationCode,
            platformText,
            statusText,
            statusColor,
            operatorText,
            callingText,
            $"{locationText}  •  {operatorText}  •  {callingText}",
            DateTimeOffset.Parse($"2026-03-16T{scheduledText}:00+00:00"));
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

    private static object[] InvokeBuildPages(RailSceneSnapshot snapshot)
    {
        var method = typeof(RailBoardScene).GetMethod("BuildPages", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var pages = method!.Invoke(null, [snapshot]);
        var enumerable = Assert.IsAssignableFrom<IEnumerable>(pages);
        return enumerable.Cast<object>().ToArray();
    }

    private static void AssertBoardRows(object page, string stationName, string boardType, params string[] expectedLocations)
    {
        var pageType = page.GetType();
        Assert.Equal(stationName, pageType.GetProperty("StationName")?.GetValue(page));
        Assert.Equal(boardType, pageType.GetProperty("BoardType")?.GetValue(page)?.ToString());

        var rowsValue = pageType.GetProperty("Rows")?.GetValue(page);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<RailServiceSnapshot>>(rowsValue);

        Assert.Equal(expectedLocations, rows.Select(row => row.LocationText).ToArray());
    }

    private sealed class FixedRailSnapshotSource(RailSceneSnapshot snapshot) : IRailSnapshotSource
    {
        public bool TryGetSnapshot(out RailSceneSnapshot railSnapshot)
        {
            railSnapshot = snapshot;
            return true;
        }
    }
}
