using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailBoardSceneTests
{
    [Fact]
    public void RailBoardScene_RendersCompactPages_AndRunsLongerThanTwentySeconds()
    {
        ResetCache();

        var snapshot = new RailBoardScene.RailSceneSnapshot(
            new RailBoardScene.RailStationSnapshot(
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
                    new RailBoardScene.RailAlertSnapshot("Signal failure between Foxton and Royston. Delays expected.", 3)
                ],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                false),
            new RailBoardScene.RailStationSnapshot(
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

        var scene = new RailBoardScene((_, _) => Task.FromResult(snapshot));

        scene.Activate();
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromMilliseconds(50));
        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);

        scene.Elapsed(TimeSpan.FromSeconds(25));
        Assert.True(scene.IsActive);

        scene.Elapsed(TimeSpan.FromSeconds(40));
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void RailBoardScene_ShowsGracefulNoDataBoard_WhenStationDataIsUnavailable()
    {
        ResetCache();

        var snapshot = new RailBoardScene.RailSceneSnapshot(
            new RailBoardScene.RailStationSnapshot(
                "CAM",
                "Cambridge",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new RailBoardScene.RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero));

        var scene = new RailBoardScene((_, _) => Task.FromResult(snapshot));

        scene.Activate();
        scene.Elapsed(TimeSpan.FromMilliseconds(50));

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);
    }

    [Fact]
    public void RailBoardScene_BuildsFourCorridorBoards_ForCambridgeAndConfiguredLondonCorridor()
    {
        var updatedAt = new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero);
        var snapshot = new RailBoardScene.RailSceneSnapshot(
            new RailBoardScene.RailStationSnapshot(
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
                        "Great Northern", "CALLS ROYSTON"),
                    CreateService("18:09", "Norwich", "NRW", "P7", "On time", new Rgba32(210, 188, 144),
                        "Greater Anglia", "CALLS ELY")
                ],
                [],
                updatedAt,
                false),
            new RailBoardScene.RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [
                    CreateService("18:28", "Cambridge", "CBG", "P8", "+4", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS FINSBURY PARK, STEVENAGE"),
                    CreateService("18:34", "Leeds", "LDS", "P2", "On time", new Rgba32(210, 188, 144),
                        "LNER", "CALLS DONCASTER")
                ],
                [
                    CreateService("18:25", "Cambridge", "CBG", "P9", "On time", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS STEVENAGE"),
                    CreateService("18:39", "Edinburgh", "EDB", "P0", "On time", new Rgba32(210, 188, 144),
                        "LNER", "CALLS YORK")
                ],
                [],
                updatedAt,
                false),
            updatedAt);

        var pages = InvokeBuildPages(snapshot);

        Assert.Equal(4, pages.Length);
        AssertBoardRows(pages[0], "Cambridge", "Departures", "London Kings Cross", "London Liverpool Street", "Finsbury Park");
        AssertBoardRows(pages[1], "Cambridge", "Arrivals", "London Kings Cross", "Finsbury Park");
        AssertBoardRows(pages[2], "London Kings Cross", "Departures", "Cambridge");
        AssertBoardRows(pages[3], "London Kings Cross", "Arrivals", "Cambridge");
    }

    [Theory]
    [InlineData("CBG", null, "Cambridge")]
    [InlineData("KGX", null, "London Kings Cross")]
    [InlineData(null, "London King's Cross", "London Kings Cross")]
    [InlineData(null, "Kings Cross", "London Kings Cross")]
    [InlineData(null, "Liverpool Street", "London Liverpool Street")]
    [InlineData(null, "Finsbury Pk", "Finsbury Park")]
    public void RailBoardScene_FormatsFullStationNames(string? crs, string? locationName, string expected)
    {
        var method = typeof(RailBoardScene).GetMethod("FormatStationDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var formatted = method!.Invoke(null, [crs, locationName]);

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

        var station = new RailBoardScene.RailStationSnapshot(
            "KGX",
            "London King's Cross",
            [],
            [],
            [],
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
            false);

        var ticker = method!.Invoke(null,
            [station, Enum.Parse(boardType!, "Departures"), Array.Empty<RailBoardScene.RailServiceSnapshot>()]);

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
            "18:28",
            "Cambridge",
            "CBG",
            "P8",
            "+4",
            new Rgba32(255, 184, 64),
            "Great Northern",
            "CALLS FINSBURY PARK, STEVENAGE, ROYSTON");

        var fastText = method!.Invoke(null, [fastService]);
        var stoppingText = method.Invoke(null, [stoppingService]);

        Assert.Equal("Fast via Royston, Stevenage", fastText);
        Assert.Equal("Via Finsbury Park, Stevenage, Royston", stoppingText);
    }

    private static RailBoardScene.RailServiceSnapshot CreateService(
        string scheduledText,
        string locationText,
        string locationCode,
        string platformText,
        string statusText,
        Rgba32 statusColor,
        string operatorText,
        string callingText)
    {
        return new RailBoardScene.RailServiceSnapshot(
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

    private static void ResetCache()
    {
        var snapshotField = typeof(RailBoardScene).GetField("cachedSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
        var updatedAtField = typeof(RailBoardScene).GetField("cacheUpdatedAtUtc", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(snapshotField);
        Assert.NotNull(updatedAtField);
        snapshotField!.SetValue(null, null);
        updatedAtField!.SetValue(null, DateTimeOffset.MinValue);
    }

    private static object[] InvokeBuildPages(RailBoardScene.RailSceneSnapshot snapshot)
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
        var rows = Assert.IsAssignableFrom<IReadOnlyList<RailBoardScene.RailServiceSnapshot>>(rowsValue);

        Assert.Equal(expectedLocations, rows.Select(row => row.LocationText).ToArray());
    }
}
